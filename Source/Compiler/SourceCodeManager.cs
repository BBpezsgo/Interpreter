using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

#if UNITY
public delegate UnityEngine.Awaitable<Stream?>? FileParser(Uri file);
#else
public delegate Task<Stream?>? FileParser(Uri file);
#endif

public readonly record struct ParsedFile(
    Uri File,
    UsingDefinition? Using,
    TokenizerResult Tokens,
    ParserResult AST
);

record PendingFile(
    Uri Uri,
    UsingDefinition? Initiator,
#if UNITY
    UnityEngine.Awaitable<Stream?> Task
#else
    Task<Stream?> Task
#endif
);

public class SourceCodeManager
{
    readonly HashSet<Uri> CompiledUris;
    readonly DiagnosticsCollection Diagnostics;
    readonly IEnumerable<string> PreprocessorVariables;
    readonly FileParser? FileParser;
    readonly List<PendingFile> PendingFiles;
    readonly List<ParsedFile> ParsedFiles;
    readonly TokenizerSettings? TokenizerSettings;
    readonly string? BasePath;

    public SourceCodeManager(DiagnosticsCollection diagnostics, IEnumerable<string> preprocessorVariables, FileParser? fileParser, TokenizerSettings? tokenizerSettings, string? basePath)
    {
        CompiledUris = new();
        Diagnostics = diagnostics;
        PreprocessorVariables = preprocessorVariables;
        FileParser = fileParser;
        PendingFiles = new();
        ParsedFiles = new();
        TokenizerSettings = tokenizerSettings;
        BasePath = basePath;
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

        string? directory = parent == null ? null : (new FileInfo(parent.AbsolutePath).Directory?.FullName);

        if (directory is not null)
        {
            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, directory), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, directory), UriKind.Absolute);
        }
        else
        {
            if (basePath != null)
            { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, Environment.CurrentDirectory), @using), UriKind.Absolute); }

            yield return new Uri(Path.GetFullPath(@using, Environment.CurrentDirectory), UriKind.Absolute);
        }
    }

#if UNITY
    void ProcessPendingFiles()
    {
        for (int i = 0; i < PendingFiles.Count; i++)
        {
            UnityEngine.Awaitable<Stream?>.Awaiter awaiter = PendingFiles[i].Task.GetAwaiter();
            if (!awaiter.IsCompleted) break;
            PendingFile finishedFile = PendingFiles[i];
            PendingFiles.RemoveAt(i--);

            Stream? content;

            try
            {
                content = awaiter.GetResult();
            }
            catch (FileNotFoundException)
            {
                content = null;
            }
            catch (Exception ex)
            {
                if (finishedFile.Initiator is null)
                { Diagnostics.Add(DiagnosticWithoutContext.Critical(ex.Message)); }
                else
                { Diagnostics.Add(Diagnostic.Critical(ex.Message, finishedFile.Initiator)); }
                break;
            }

            if (content is null)
            {
                if (finishedFile.Initiator is null)
                { Diagnostics.Add(DiagnosticWithoutContext.Critical($"File \"{finishedFile.Uri}\" not found")); }
                else
                { Diagnostics.Add(Diagnostic.Critical($"File \"{finishedFile.Uri}\" not found", finishedFile.Initiator)); }
                break;
            }

            TokenizerResult tokens = StreamTokenizer.Tokenize(
                content,
                Diagnostics,
                PreprocessorVariables,
                finishedFile.Uri,
                TokenizerSettings,
                null,
                null);

            ParserResult ast = Parser.Parser.Parse(tokens.Tokens, finishedFile.Uri, Diagnostics);

            if (ast.Usings.Any())
            { Output.LogDebug("Loading files ..."); }

            ParsedFiles.Add(new ParsedFile(finishedFile.Uri, finishedFile.Initiator, tokens, ast));

            foreach (UsingDefinition @using in ast.Usings)
            {
                IEnumerable<Uri> query = GetFileQuery(@using.PathString, finishedFile.Uri, BasePath);
                if (!FromAnywhere(@using, query, out _))
                {
                    Diagnostics.Add(Diagnostic.Critical($"File \"{@using.PathString}\" not found", @using));
                    continue;
                }
            }
        }
    }
#else
    void ProcessPendingFiles()
    {
        for (int i = 0; i < PendingFiles.Count; i++)
        {
            if (!PendingFiles[i].Task.IsCompleted) break;
            PendingFile finishedFile = PendingFiles[i];
            PendingFiles.RemoveAt(i--);

            if (finishedFile.Task.IsCanceled)
            {
                if (finishedFile.Initiator is null)
                { Diagnostics.Add(DiagnosticWithoutContext.Critical($"Failed to collect the necessary files: operation cancelled")); }
                else
                { Diagnostics.Add(Diagnostic.Critical($"Failed to collect the necessary files: operation cancelled", finishedFile.Initiator)); }
                break;
            }

            if (finishedFile.Task.IsFaulted)
            {
                if (finishedFile.Initiator is null)
                { Diagnostics.Add(DiagnosticWithoutContext.Critical($"Failed to collect the necessary files: {finishedFile.Task.Exception}")); }
                else
                { Diagnostics.Add(Diagnostic.Critical($"Failed to collect the necessary files: {finishedFile.Task.Exception}", finishedFile.Initiator)); }
                break;
            }

            Stream? content = finishedFile.Task.Result;

            if (content is null)
            {
                if (finishedFile.Initiator is null)
                { Diagnostics.Add(DiagnosticWithoutContext.Critical($"File \"{finishedFile.Uri}\" not found")); }
                else
                { Diagnostics.Add(Diagnostic.Critical($"File \"{finishedFile.Uri}\" not found", finishedFile.Initiator)); }
                break;
            }

            TokenizerResult tokens = StreamTokenizer.Tokenize(
                content,
                Diagnostics,
                PreprocessorVariables,
                finishedFile.Uri,
                TokenizerSettings,
                null,
                null);

            ParserResult ast = Parser.Parser.Parse(tokens.Tokens, finishedFile.Uri, Diagnostics);

            if (ast.Usings.Any())
            { Output.LogDebug("Loading files ..."); }

            ParsedFiles.Add(new ParsedFile(finishedFile.Uri, finishedFile.Initiator, tokens, ast));

            foreach (UsingDefinition @using in ast.Usings)
            {
                IEnumerable<Uri> query = GetFileQuery(@using.PathString, finishedFile.Uri, BasePath);
                if (!FromAnywhere(@using, query, out _))
                {
                    Diagnostics.Add(Diagnostic.Critical($"File \"{@using.PathString}\" not found", @using));
                    continue;
                }
            }
        }
    }
#endif

    static bool FromWeb(
        Uri file,
        [NotNullWhen(true)] out Stream? content)
    {
        Output.LogInfo($"  Download file \"{file}\" ...");

        using HttpClient client = new();
        using Task<HttpResponseMessage> getTask = client.GetAsync(file);
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

        Task<byte[]> _content = res.Content.ReadAsByteArrayAsync();
        _content.Wait();
        content = new MemoryStream(_content.Result);
        return true;
    }

    static bool FromFile(
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

        Output.LogDebug($"  Load local file \"{path}\" ...");

        content = File.OpenRead(path);

        return true;
    }

    bool FromAnywhere(
        UsingDefinition? initiator,
        Uri file)
    {
        if (initiator is not null)
        {
            if (file.IsFile)
            { initiator.CompiledUri = file.AbsolutePath; }
            else
            { initiator.CompiledUri = file.ToString(); }
        }

        if (CompiledUris.Contains(file))
        { return true; }

        if (FileParser is not null)
        {
#pragma warning disable IDE0008 // Use explicit type
            var task = FileParser.Invoke(file);
#pragma warning restore IDE0008
            if (task is not null)
            {
                CompiledUris.Add(file);
                PendingFiles.Add(new PendingFile(file, initiator, task));
                return true;
            }
        }

        if (file.Scheme is "https" or "http")
        {
            if (FromWeb(file, out Stream? content))
            {
                CompiledUris.Add(file);
#if UNITY
                UnityEngine.AwaitableCompletionSource<Stream?> task = new();
                task.SetResult(content);
                PendingFiles.Add(new PendingFile(file, initiator, task.Awaitable));
#else
                Task<Stream?> task = Task.FromResult<Stream?>(content);
                PendingFiles.Add(new PendingFile(file, initiator, task));
#endif
                return true;
            }
        }
        else if (file.IsFile)
        {
            if (FromFile(file, out Stream? content))
            {
                CompiledUris.Add(file);
#if UNITY
                UnityEngine.AwaitableCompletionSource<Stream?> task = new();
                task.SetResult(content);
                PendingFiles.Add(new PendingFile(file, initiator, task.Awaitable));
#else
                Task<Stream?> task = Task.FromResult<Stream?>(content);
                PendingFiles.Add(new PendingFile(file, initiator, task));
#endif
                return true;
            }
        }

        return false;
    }

    bool FromAnywhere(
        UsingDefinition? initiator,
        IEnumerable<Uri> query,
        [NotNullWhen(true)] out Uri? uri)
    {
        foreach (Uri item in query)
        {
            if (FromAnywhere(initiator, item))
            {
                uri = item;
                return true;
            }
        }

        uri = null;
        return false;
    }

    ImmutableArray<ParsedFile> Entry(
        Uri? file,
        IEnumerable<string>? additionalImports)
    {
        if (file is not null)
        {
            if (!FromAnywhere(null, file))
            {
                Diagnostics.Add(DiagnosticWithoutContext.Critical($"File \"{file}\" not found"));
                return ImmutableArray<ParsedFile>.Empty;
            }
        }

        if (additionalImports is not null)
        {
            foreach (string additionalImport in additionalImports)
            {
                IEnumerable<Uri> query = GetFileQuery(additionalImport, null, BasePath);
                if (!FromAnywhere(null, query, out _))
                {
                    Diagnostics.Add(DiagnosticWithoutContext.Critical($"File \"{additionalImport}\" not found"));
                    continue;
                }
            }
        }

        while (PendingFiles.Count > 0)
        {
#if UNITY
#else
            PendingFiles[0].Task.Wait();
#endif
            ProcessPendingFiles();
        }

        return ParsedFiles.ToImmutableArray();
    }

    public static ImmutableArray<ParsedFile> Collect(
        Uri? file,
        string? basePath,
        DiagnosticsCollection diagnostics,
        IEnumerable<string> preprocessorVariables,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser,
        IEnumerable<string>? additionalImports)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, preprocessorVariables, fileParser, tokenizerSettings, basePath);
        return sourceCodeManager.Entry(file, additionalImports);
    }
}
