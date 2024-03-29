using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LanguageCore.Compiler;

using Parser;
using Tokenizing;

public readonly struct CollectedAST
{
    public readonly ParserResult ParserResult;
    public readonly Uri Uri;
    public readonly UsingDefinition Using;
    public readonly ImmutableArray<Token> Tokens;

    public CollectedAST(ParserResult parserResult, Uri uri, UsingDefinition @using, ImmutableArray<Token> tokens)
    {
        ParserResult = parserResult;
        Uri = uri;
        Using = @using;
        Tokens = tokens;
    }
}

public class SourceCodeManager
{
    readonly List<Uri> AlreadyLoadedCodes;
    readonly AnalysisCollection? AnalysisCollection;
    readonly PrintCallback? Print;
    readonly IEnumerable<string> PreprocessorVariables;

    public SourceCodeManager(AnalysisCollection? analysisCollection, PrintCallback? printCallback, IEnumerable<string> preprocessorVariables)
    {
        AlreadyLoadedCodes = new List<Uri>();
        AnalysisCollection = analysisCollection;
        Print = printCallback;
        PreprocessorVariables = preprocessorVariables;
    }

    bool IsAlreadyLoaded(Uri uri)
    {
        foreach (Uri item in AlreadyLoadedCodes)
        {
            if (uri.Equals(item))
            { return true; }
        }
        return false;
    }

    static JsonDocument? LoadConfigFile(string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
        { return null; }

        if (!Directory.Exists(directoryPath))
        { return null; }

        string[]? configFiles = Directory.GetFiles(directoryPath, "config.json", SearchOption.TopDirectoryOnly);

        if (configFiles is null || configFiles.Length != 1)
        { return null; }

        return JsonDocument.Parse(File.ReadAllText(configFiles[0]));
    }

    bool FromWeb(Uri uri, TokenizerSettings? tokenizerSettings, out ParserResult ast)
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

        AnalysisCollection?.Warnings.AddRange(tokens.Warnings);

        ast = Parser.Parse(tokens, uri);

        AnalysisCollection?.Errors.AddRange(ast.Errors);

        return true;
    }

    bool FromFile(string path, TokenizerSettings? tokenizerSettings, out ParserResult ast)
    {
        ast = default;

        if (!File.Exists(path))
        { return false; }

        Print?.Invoke($"  Load local file \"{path}\" ...", LogType.Debug);

        TokenizerResult tokens = StreamTokenizer.Tokenize(path, PreprocessorVariables, tokenizerSettings);
        AnalysisCollection?.Warnings.AddRange(tokens.Warnings);

        ast = Parser.Parse(tokens, new Uri(path, UriKind.Absolute));
        AnalysisCollection?.Errors.AddRange(ast.Errors);

        return true;
    }

    bool FromAnywhere(UsingDefinition @using, Uri uri, TokenizerSettings? tokenizerSettings, out ParserResult? ast)
    {
        if (uri.IsFile)
        { @using.CompiledUri = uri.LocalPath; }
        else
        { @using.CompiledUri = uri.ToString(); }
        ast = null;

        if (IsAlreadyLoaded(uri))
        { return true; }

        bool success;

        if (uri.IsFile)
        {
            string filePath = uri.LocalPath;
            success = FromFile(filePath, tokenizerSettings, out ParserResult _ast);
            if (success)
            {
                _ast.SetFile(uri);
                ast = _ast;
            }
        }
        else
        {
            success = FromWeb(uri, tokenizerSettings, out ParserResult _ast);
            if (success)
            {
                _ast.SetFile(uri);
                ast = _ast;
            }
        }

        if (success)
        {
            AlreadyLoadedCodes.Add(uri);
        }

        return success;
    }

    static IEnumerable<Uri> GetSearches(UsingDefinition @using, Uri? parent, string? basePath)
    {
        if (parent is not null &&
            parent.IsFile)
        {
            FileInfo file = new(parent.LocalPath);
            JsonDocument? config = LoadConfigFile(file.Directory?.FullName);
            if (config != null &&
                config.RootElement.TryGetProperty("base", out JsonElement v))
            {
                string? b = v.GetString();
                if (b != null) basePath = b;
            }
        }

        string filename = @using.PathString;
        if (!filename.EndsWith(".bbc", StringComparison.Ordinal)) filename += ".bbc";

        if (Uri.TryCreate(parent, filename, out Uri? uri))
        { yield return uri; }

        if (parent is not null &&
            basePath is not null &&
            Uri.TryCreate(new Uri(parent, basePath), filename, out uri))
        { yield return uri; }

        if (parent is not null &&
            !parent.IsFile)
        { yield break; }

        if (parent != null)
        {
            FileInfo file = new(parent.LocalPath);

            if (file.Directory is null)
            { throw new InternalException($"File \"{file}\" doesn't have a directory"); }

            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, file.Directory.FullName), filename), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(filename, file.Directory.FullName), UriKind.Absolute);
        }
        else
        {
            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, Environment.CurrentDirectory), filename), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(filename, Environment.CurrentDirectory), UriKind.Absolute);
        }
    }

    bool ProcessFiles(UsingDefinition @using, IEnumerable<Uri> searchForThese, TokenizerSettings? tokenizerSettings, out ParserResult? ast, [NotNullWhen(true)] out Uri? uri)
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

    IReadOnlyList<CollectedAST> ProcessFile(
        UsingDefinition @using,
        Uri? parent,
        string? basePath,
        TokenizerSettings? tokenizerSettings)
    {
        if (!ProcessFiles(@using, GetSearches(@using, parent, basePath), tokenizerSettings, out ParserResult? ast, out Uri? path))
        {
            AnalysisCollection?.Errors.Add(new Error($"File \"{@using.PathString}\" not found", new Position(@using.Path), parent));
            return Array.Empty<CollectedAST>();
        }

        if (!ast.HasValue)
        { return Array.Empty<CollectedAST>(); }

        List<CollectedAST> collectedASTs = new();

        collectedASTs.Add(new CollectedAST(ast.Value, path, @using, ast.Value.OriginalTokens));

        foreach (UsingDefinition using_ in ast.Value.Usings)
        { collectedASTs.AddRange(ProcessFile(using_, path, null, tokenizerSettings)); }

        return collectedASTs;
    }

    ImmutableArray<CollectedAST> Entry(
        IEnumerable<UsingDefinition> usings,
        Uri? file,
        string? basePath,
        TokenizerSettings? tokenizerSettings)
    {
        if (usings.Any())
        { Print?.Invoke("Loading used files ...", LogType.Debug); }

        List<CollectedAST> collectedASTs = new();

        foreach (UsingDefinition usingItem in usings)
        { collectedASTs.AddRange(ProcessFile(usingItem, file, basePath, tokenizerSettings)); }

        AnalysisCollection?.Throw();

        return collectedASTs.ToImmutableArray();
    }

    public static ImmutableArray<CollectedAST> Collect(
        IEnumerable<UsingDefinition> usings,
        Uri? file,
        PrintCallback? printCallback,
        string? basePath,
        AnalysisCollection? analysisCollection,
        IEnumerable<string> preprocessorVariables,
        TokenizerSettings? tokenizerSettings)
    {
        SourceCodeManager sourceCodeManager = new(analysisCollection, printCallback, preprocessorVariables);
        if (file != null) sourceCodeManager.AlreadyLoadedCodes.Add(file);
        return sourceCodeManager.Entry(usings, file, basePath, tokenizerSettings);
    }

    public static ImmutableArray<CollectedAST> Collect(
        IEnumerable<UsingDefinition> usings,
        FileInfo? file,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback,
        string? basePath,
        AnalysisCollection? analysisCollection,
        TokenizerSettings? tokenizerSettings)
    {
        Uri? fileUri = file is null ? null : new Uri(file.FullName, UriKind.Absolute);
        SourceCodeManager sourceCodeManager = new(analysisCollection, printCallback, preprocessorVariables);
        if (fileUri != null) sourceCodeManager.AlreadyLoadedCodes.Add(fileUri);
        return sourceCodeManager.Entry(usings, fileUri, basePath, tokenizerSettings);
    }
}
