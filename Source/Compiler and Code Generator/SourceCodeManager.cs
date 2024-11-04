#pragma warning disable IDE0028 // Simplify collection initialization

using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public delegate bool FileParser(Uri uri, TokenizerSettings? tokenizerSettings, [NotNullWhen(true)] out ParserResult ast);

public readonly struct CollectedAST
{
    public ParserResult ParserResult { get; }
    public Uri Uri { get; }
    public UsingDefinition? Using { get; }
    public ImmutableArray<Token> Tokens => ParserResult.OriginalTokens;

    public CollectedAST(ParserResult parserResult, Uri uri, UsingDefinition? @using)
    {
        ParserResult = parserResult;
        Uri = uri;
        Using = @using;
    }
}

public class SourceCodeManager
{
    readonly Dictionary<Uri, CollectedAST> AlreadyLoadedCodes;
    readonly DiagnosticsCollection? Diagnostics;
    readonly PrintCallback? Print;
    readonly IEnumerable<string> PreprocessorVariables;
    readonly FileParser? FileParser;

    public SourceCodeManager(DiagnosticsCollection? diagnostics, PrintCallback? printCallback, IEnumerable<string> preprocessorVariables, FileParser? fileParser)
    {
        AlreadyLoadedCodes = new Dictionary<Uri, CollectedAST>();
        Diagnostics = diagnostics;
        Print = printCallback;
        PreprocessorVariables = preprocessorVariables;
        FileParser = fileParser;
    }

    bool FromWeb(
        Uri uri,
        TokenizerSettings? tokenizerSettings,
        [NotNullWhen(true)] out ParserResult ast)
    {
        Print?.Invoke($"  Download file \"{uri}\" ...", LogType.Debug);

        using HttpClient client = new();
        using Task<HttpResponseMessage> getTask = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        try
        {
            getTask.Wait();
        }
        catch (AggregateException ex)
        {
            foreach (Exception error in ex.InnerExceptions)
            { Output.LogError(error.Message); }

            ast = default;
            return false;
        }
        using HttpResponseMessage res = getTask.Result;

        if (!res.IsSuccessStatusCode)
        {
            Output.LogError($"HTTP {res.StatusCode}");

            ast = default;
            return false;
        }

        TokenizerResult tokens;
        if (res.Content.Headers.ContentLength.HasValue)
        {
            using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, true);
            tokens = StreamTokenizer.Tokenize(res.Content.ReadAsStream(), PreprocessorVariables, uri, tokenizerSettings, progress, (int)res.Content.Headers.ContentLength.Value);
        }
        else
        {
            tokens = StreamTokenizer.Tokenize(res.Content.ReadAsStream(), PreprocessorVariables, uri, tokenizerSettings);
        }

        Diagnostics?.AddRange(tokens.Diagnostics);

        ast = Parser.Parser.Parse(tokens.Tokens, uri);

        Diagnostics?.AddRange(ast.Errors);

        return true;
    }

    bool FromFile(
        string path,
        TokenizerSettings? tokenizerSettings,
        [NotNullWhen(true)] out ParserResult ast)
    {
        ast = default;

        if (path.StartsWith("/~")) path = path[1..];
        if (path.StartsWith('~'))
        { path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + path[1..]); }

        if (!File.Exists(path))
        { return false; }

        Print?.Invoke($"  Load local file \"{path}\" ...", LogType.Debug);

        TokenizerResult tokens = StreamTokenizer.Tokenize(path, PreprocessorVariables, tokenizerSettings);
        Diagnostics?.AddRange(tokens.Diagnostics);

        ast = Parser.Parser.Parse(tokens.Tokens, Utils.ToFileUri(path));
        Diagnostics?.AddRange(ast.Errors);

        return true;
    }

    bool FromAnywhere(
        UsingDefinition? @using,
        Uri uri,
        TokenizerSettings? tokenizerSettings,
        out ParserResult? ast)
    {
        if (@using is not null)
        {
            if (uri.IsFile)
            { @using.CompiledUri = uri.AbsolutePath; }
            else
            { @using.CompiledUri = uri.ToString(); }
        }

        ast = default;

        if (AlreadyLoadedCodes.ContainsKey(uri))
        { return true; }

        if (FileParser is not null && FileParser.Invoke(uri, tokenizerSettings, out ParserResult ast1))
        {
            ast = ast1;
            AlreadyLoadedCodes.Add(uri, new CollectedAST(ast1, uri, @using));
            return true;
        }

        if (uri.Scheme is "https" or "http")
        {
            if (FromWeb(uri, tokenizerSettings, out ParserResult ast2))
            {
                ast = ast2;
                AlreadyLoadedCodes.Add(uri, new CollectedAST(ast2, uri, @using));
                return true;
            }
        }
        else if (uri.IsFile)
        {
            if (FromFile(uri.AbsolutePath, tokenizerSettings, out ParserResult ast2))
            {
                ast = ast2;
                AlreadyLoadedCodes.Add(uri, new CollectedAST(ast2, uri, @using));
                return true;
            }
        }

        return false;
    }

    static IEnumerable<Uri> GetSearches(string @using, Uri? parent, string? basePath)
    {
        if (!@using.EndsWith($".{LanguageConstants.LanguageExtension}", StringComparison.Ordinal)) @using += $".{LanguageConstants.LanguageExtension}";

        if (Uri.TryCreate(parent, @using, out Uri? uri))
        { yield return uri; }

        if (parent is not null &&
            basePath is not null &&
            Uri.TryCreate(new Uri(parent, basePath), @using, out uri))
        { yield return uri; }

        if (parent is not null &&
            !parent.IsFile)
        { yield break; }

        if (@using.StartsWith("/~")) @using = @using[1..];
        if (@using.StartsWith('~'))
        { @using = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "." + @using[1..]); }

        if (parent != null)
        {
            FileInfo file = new(parent.AbsolutePath);

            if (file.Directory is null)
            { throw new InternalExceptionWithoutContext($"File \"{file}\" doesn't have a directory"); }

            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, file.Directory.FullName), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, file.Directory.FullName), UriKind.Absolute);
        }
        else
        {
            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, Environment.CurrentDirectory), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, Environment.CurrentDirectory), UriKind.Absolute);
        }
    }

    bool FromAnywhere(
        UsingDefinition? @using,
        IEnumerable<Uri> searchForThese,
        TokenizerSettings? tokenizerSettings,
        out ParserResult? ast,
        [NotNullWhen(true)] out Uri? uri)
    {
        foreach (Uri item in searchForThese)
        {
            if (FromAnywhere(@using, item, tokenizerSettings, out ast))
            {
                uri = item;
                return true;
            }
        }

        ast = default;
        uri = default;
        return false;
    }

    Dictionary<Uri, CollectedAST> ProcessFile(
        UsingDefinition @using,
        Uri? parent,
        string? basePath,
        TokenizerSettings? tokenizerSettings)
    {
        if (!FromAnywhere(@using, GetSearches(@using.PathString, parent, basePath), tokenizerSettings, out ParserResult? ast, out Uri? path))
        {
            Diagnostics?.Add(Diagnostic.Error($"File \"{@using.PathString}\" not found", new Position(@using.Path.As<IPositioned>().Or(@using)), @using.File));
            return new Dictionary<Uri, CollectedAST>();
        }

        if (!ast.HasValue)
        { return new Dictionary<Uri, CollectedAST>(); }

        Dictionary<Uri, CollectedAST> collectedASTs = new();
        collectedASTs.Add(path, new CollectedAST(ast.Value, path, @using));

        foreach (UsingDefinition @using_ in ast.Value.Usings)
        { collectedASTs.AddRange(ProcessFile(@using_, path, basePath, tokenizerSettings)); }

        return collectedASTs;
    }

    Dictionary<Uri, CollectedAST> ProcessAdditionalImport(
        string file,
        Uri? parent,
        string? basePath,
        TokenizerSettings? tokenizerSettings)
    {
        if (!FromAnywhere(null, GetSearches(file, parent, basePath), tokenizerSettings, out ParserResult? ast, out Uri? uri))
        {
            Diagnostics?.Add(DiagnosticWithoutContext.Error($"File \"{file}\" not found"));
            return new Dictionary<Uri, CollectedAST>();
        }

        if (!ast.HasValue)
        { return new Dictionary<Uri, CollectedAST>(); }

        Dictionary<Uri, CollectedAST> collectedASTs = new();
        collectedASTs.Add(uri, new CollectedAST(ast.Value, uri, null));

        foreach (UsingDefinition @using_ in ast.Value.Usings)
        { collectedASTs.AddRange(ProcessFile(@using_, uri, null, tokenizerSettings)); }

        return collectedASTs;
    }

    ImmutableDictionary<Uri, CollectedAST> Entry(
        Uri file,
        string? basePath,
        TokenizerSettings? tokenizerSettings,
        IEnumerable<string>? additionalImports)
    {
        if (!FromAnywhere(null, file, tokenizerSettings, out ParserResult? ast))
        { throw new InternalExceptionWithoutContext($"File \"{file}\" not found"); }

        if (!ast.HasValue)
        { throw new InternalExceptionWithoutContext(); }

        if (ast.Value.Usings.Any())
        { Print?.Invoke("Loading used files ...", LogType.Debug); }

        Dictionary<Uri, CollectedAST> collectedASTs = new();
        collectedASTs.Add(file, new CollectedAST(ast.Value, file, null));

        foreach (UsingDefinition @using in ast.Value.Usings)
        { collectedASTs.AddRange(ProcessFile(@using, file, basePath, tokenizerSettings)); }

        if (additionalImports is not null)
        {
            foreach (string additionalImport in additionalImports)
            { collectedASTs.AddRange(ProcessAdditionalImport(additionalImport, file, basePath, tokenizerSettings)); }
        }

        Diagnostics?.Throw();

        return collectedASTs.ToImmutableDictionary();
    }

    public static ImmutableDictionary<Uri, CollectedAST> Collect(
        Uri file,
        PrintCallback? printCallback,
        string? basePath,
        DiagnosticsCollection? diagnostics,
        IEnumerable<string> preprocessorVariables,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser,
        IEnumerable<string>? additionalImports)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, printCallback, preprocessorVariables, fileParser);
        return sourceCodeManager.Entry(file, basePath, tokenizerSettings, additionalImports);
    }

    public static ImmutableDictionary<Uri, CollectedAST> Collect(
        FileInfo file,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback,
        string? basePath,
        DiagnosticsCollection? diagnostics,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser,
        IEnumerable<string>? additionalImports)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, printCallback, preprocessorVariables, fileParser);
        return sourceCodeManager.Entry(new Uri(file.FullName, UriKind.Absolute), basePath, tokenizerSettings, additionalImports);
    }
}
