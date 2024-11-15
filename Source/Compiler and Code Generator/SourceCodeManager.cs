#pragma warning disable IDE0028 // Simplify collection initialization

using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public delegate bool FileParser(
    Uri file,
    [NotNullWhen(true)] out string? content);

public readonly struct CollectedAST
{
    public Uri Uri { get; }
    public UsingDefinition? Using { get; }
    public TokenizerResult Tokens { get; }
    public ParserResult AST { get; }

    public CollectedAST(Uri file, UsingDefinition? @using, TokenizerResult tokens, ParserResult aST)
    {
        Uri = file;
        Using = @using;
        Tokens = tokens;
        AST = aST;
    }
}

public class SourceCodeManager
{
    readonly HashSet<Uri> AlreadyLoadedCodes;
    readonly DiagnosticsCollection Diagnostics;
    readonly PrintCallback? Print;
    readonly IEnumerable<string> PreprocessorVariables;
    readonly FileParser? FileParser;

    public SourceCodeManager(DiagnosticsCollection diagnostics, PrintCallback? printCallback, IEnumerable<string> preprocessorVariables, FileParser? fileParser)
    {
        AlreadyLoadedCodes = new();
        Diagnostics = diagnostics;
        Print = printCallback;
        PreprocessorVariables = preprocessorVariables;
        FileParser = fileParser;
    }

    bool FromWeb(
        Uri file,
        [NotNullWhen(true)] out Stream? content)
    {
        Print?.Invoke($"  Download file \"{file}\" ...", LogType.Debug);

        using HttpClient client = new();
        using Task<HttpResponseMessage> getTask = client.GetAsync(file, HttpCompletionOption.ResponseHeadersRead);
        try
        {
            getTask.Wait();
        }
        catch (AggregateException ex)
        {
            foreach (Exception error in ex.InnerExceptions)
            { Output.LogError(error.Message); }

            content = default;
            return false;
        }
        using HttpResponseMessage res = getTask.Result;

        if (!res.IsSuccessStatusCode)
        {
            Output.LogError($"HTTP {res.StatusCode}");

            content = default;
            return false;
        }

        content = res.Content.ReadAsStream();
        return true;
    }

    bool FromFile(
        Uri file,
        [NotNullWhen(true)] out Stream? content)
    {
        content = default;

        string path = file.AbsolutePath;

        if (path.StartsWith("/~")) path = path[1..];
        if (path.StartsWith('~'))
        { path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + path[1..]); }

        if (!File.Exists(path))
        { return false; }

        Print?.Invoke($"  Load local file \"{path}\" ...", LogType.Debug);

        content = File.OpenRead(path);

        return true;
    }

    bool FromAnywhere(
        UsingDefinition? initiator,
        Uri file,
        out Stream? content)
    {
        if (initiator is not null)
        {
            if (file.IsFile)
            { initiator.CompiledUri = file.AbsolutePath; }
            else
            { initiator.CompiledUri = file.ToString(); }
        }

        content = null;

        if (AlreadyLoadedCodes.Contains(file))
        { return true; }

        if (FileParser is not null && FileParser.Invoke(file, out string? stringContent))
        {
            content = new MemoryStream(Encoding.UTF8.GetBytes(stringContent));
            AlreadyLoadedCodes.Add(file);
            return true;
        }

        if (file.Scheme is "https" or "http")
        {
            if (FromWeb(file, out content))
            {
                AlreadyLoadedCodes.Add(file);
                return true;
            }
        }
        else if (file.IsFile)
        {
            if (FromFile(file, out content))
            {
                AlreadyLoadedCodes.Add(file);
                return true;
            }
        }

        return false;
    }

    static IEnumerable<Uri> GetFileQuery(string @using, Uri? parent, string? basePath)
    {
        if (!@using.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal)) @using += $".{LanguageConstants.LanguageExtension}";

        if (Uri.TryCreate(parent, @using, out Uri? file))
        { yield return file; }

        if (parent is not null &&
            basePath is not null &&
            Uri.TryCreate(new Uri(parent, basePath), @using, out file))
        { yield return file; }

        if (parent is not null &&
            !parent.IsFile)
        { yield break; }

        if (@using.StartsWith("/~")) @using = @using[1..];
        if (@using.StartsWith('~'))
        { @using = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + @using[1..]); }

        if (parent != null)
        {
            FileInfo fileInfo = new(parent.AbsolutePath);

            if (fileInfo.Directory is null)
            { throw new InternalExceptionWithoutContext($"File \"{fileInfo}\" doesn't have a directory"); }

            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, fileInfo.Directory.FullName), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, fileInfo.Directory.FullName), UriKind.Absolute);
        }
        else
        {
            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, Environment.CurrentDirectory), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, Environment.CurrentDirectory), UriKind.Absolute);
        }
    }

    bool FromAnywhere(
        UsingDefinition? initiator,
        IEnumerable<Uri> query,
        out Stream? content,
        [NotNullWhen(true)] out Uri? file)
    {
        foreach (Uri item in query)
        {
            if (FromAnywhere(initiator, item, out content))
            {
                file = item;
                return true;
            }
        }

        content = null;
        file = null;
        return false;
    }

    List<CollectedAST> CollectAll(
        UsingDefinition? initiator,
        Stream content,
        Uri file,
        string? basePath,
        TokenizerSettings? tokenizerSettings)
    {
        TokenizerResult tokens = StreamTokenizer.Tokenize(
            content,
            Diagnostics,
            PreprocessorVariables,
            file,
            tokenizerSettings,
            null,
            null);

        ParserResult ast = Parser.Parser.Parse(tokens.Tokens, file, Diagnostics);

        if (ast.Usings.Any())
        { Print?.Invoke("Loading files ...", LogType.Debug); }

        List<CollectedAST> collectedASTs = new();
        collectedASTs.Add(new CollectedAST(file, initiator, tokens, ast));

        foreach (UsingDefinition @using in ast.Usings)
        {
            IEnumerable<Uri> query = GetFileQuery(@using.PathString, file, basePath);
            if (!FromAnywhere(@using, query, out Stream? subContent, out Uri? subFile) ||
                subContent is null)
            {
                if (subFile is not null && AlreadyLoadedCodes.Contains(subFile)) continue;
                Diagnostics.Add(Diagnostic.Critical($"File \"{@using.PathString}\" not found", @using));
                continue;
            }

            collectedASTs.AddRange(CollectAll(@using, subContent, subFile, basePath, tokenizerSettings));
        }

        return collectedASTs;
    }

    ImmutableArray<CollectedAST> Entry(
        Uri file,
        string? basePath,
        TokenizerSettings? tokenizerSettings,
        IEnumerable<string>? additionalImports)
    {
        if (!FromAnywhere(null, file, out Stream? content) ||
            content is null)
        { throw new InternalExceptionWithoutContext($"File \"{file}\" not found"); }

        List<CollectedAST> collected = CollectAll(null, content, file, basePath, tokenizerSettings);

        if (additionalImports is not null)
        {
            foreach (string additionalImport in additionalImports)
            {
                IEnumerable<Uri> query = GetFileQuery(additionalImport, null, basePath);
                if (!FromAnywhere(null, query, out Stream? subContent, out Uri? subFile) ||
                    subContent is null)
                {
                    Diagnostics.Add(DiagnosticWithoutContext.Critical($"File \"{file}\" not found"));
                    continue;
                }

                collected.AddRange(CollectAll(null, subContent, subFile, basePath, tokenizerSettings));
            }
        }

        Diagnostics.Throw();

        return collected.ToImmutableArray();
    }

    public static ImmutableArray<CollectedAST> Collect(
        Uri file,
        PrintCallback? printCallback,
        string? basePath,
        DiagnosticsCollection diagnostics,
        IEnumerable<string> preprocessorVariables,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser,
        IEnumerable<string>? additionalImports)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, printCallback, preprocessorVariables, fileParser);
        return sourceCodeManager.Entry(file, basePath, tokenizerSettings, additionalImports);
    }

    public static ImmutableArray<CollectedAST> Collect(
        FileInfo file,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback,
        string? basePath,
        DiagnosticsCollection diagnostics,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser,
        IEnumerable<string>? additionalImports)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, printCallback, preprocessorVariables, fileParser);
        return sourceCodeManager.Entry(new Uri(file.FullName, UriKind.Absolute), basePath, tokenizerSettings, additionalImports);
    }
}
