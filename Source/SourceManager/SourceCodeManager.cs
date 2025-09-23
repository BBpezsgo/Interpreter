﻿using System.IO;
using System.Threading.Tasks;
using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public class ImportIndex
{
    public readonly List<ImportIndex> Children = new();

    public void ToList(List<ImportIndex> list)
    {
        list.Add(this);
        foreach (ImportIndex child in Children) child.ToList(list);
    }

    public ImportIndex Next()
    {
        ImportIndex importIndex = new();
        Children.Add(importIndex);
        return importIndex;
    }
}

public readonly record struct ParsedFile(
    Uri File,
    UsingDefinition? Using,
    TokenizerResult Tokens,
    ParserResult AST,
    ImportIndex Index
);

record PendingFile(
    Uri Uri,
    UsingDefinition? Initiator,
#if UNITY
    UnityEngine.Awaitable<Stream> Task,
#else
    Task<Stream> Task,
#endif
    ImportIndex Index
);

public class SourceCodeManager
{
    readonly HashSet<Uri> CompiledUris;
    readonly DiagnosticsCollection Diagnostics;
    readonly IEnumerable<string> PreprocessorVariables;
    readonly ImmutableArray<ISourceProvider> SourceProviders;
    readonly TokenizerSettings TokenizerSettings;

    readonly List<PendingFile> PendingFiles;
    readonly List<ParsedFile> ParsedFiles;

    public SourceCodeManager(DiagnosticsCollection diagnostics, IEnumerable<string> preprocessorVariables, ImmutableArray<ISourceProvider> sourceProviders, TokenizerSettings? tokenizerSettings)
    {
        CompiledUris = new();
        Diagnostics = diagnostics;
        PreprocessorVariables = preprocessorVariables;
        PendingFiles = new();
        ParsedFiles = new();
        SourceProviders = sourceProviders;
        TokenizerSettings = tokenizerSettings ?? TokenizerSettings.Default;
    }

    void ProcessPendingFiles()
    {
        for (int i = 0; i < PendingFiles.Count; i++)
        {
#pragma warning disable IDE0008 // Use explicit type
            var awaiter = PendingFiles[i].Task.GetAwaiter();
#pragma warning restore IDE0008
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

            TokenizerResult tokens;
            using (StreamReader reader = new(content))
            {
                tokens = Tokenizer.Tokenize(
                    reader.ReadToEnd(),
                    Diagnostics,
                    PreprocessorVariables,
                    finishedFile.Uri,
                    TokenizerSettings);
            }

            ParserResult ast = Parser.Parser.Parse(tokens.Tokens, finishedFile.Uri, Diagnostics);

            if (ast.Usings.Any())
            { Output.LogDebug("Loading files ..."); }

            ParsedFiles.Add(new ParsedFile(finishedFile.Uri, finishedFile.Initiator, tokens, ast, finishedFile.Index));

            foreach (UsingDefinition @using in ast.Usings)
            {
                LoadSource(@using, finishedFile.Index, @using.PathString, finishedFile.Uri, out _);
            }
        }
    }

    bool LoadSource(UsingDefinition? initiator, ImportIndex initiatorIndex, string requestedFile, Uri? currentFile, [NotNullWhen(true)] out Uri? resolvedUri)
    {
        resolvedUri = null;
        bool wasHandlerFound = false;

        foreach (ISourceProvider sourceProvider in SourceProviders)
        {
            if (sourceProvider is ISourceQueryProvider queryProvider)
            {
                foreach (Uri query in queryProvider.GetQuery(requestedFile, currentFile))
                {
                    if (CompiledUris.Contains(query))
                    {
                        resolvedUri = query;
                        return true;
                    }
                }
            }

            if (sourceProvider is ISourceProviderSync providerSync)
            {
                SourceProviderResultSync res = providerSync.TryLoad(requestedFile, currentFile);
                switch (res.Type)
                {
                    case SourceProviderResultType.Success:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        if (CompiledUris.Contains(resolvedUri))
                        { return true; }
                        CompiledUris.Add(resolvedUri);
                        if (initiator is not null) initiator.CompiledUri = resolvedUri.ToString();

                        if (res.Stream is null)
                        {
                            if (initiator is null)
                            { Diagnostics.Add(DiagnosticWithoutContext.Error($"Invalid handler for \"{resolvedUri}\": resulted in success but not provided a data stream")); }
                            else
                            { Diagnostics.Add(Diagnostic.Error($"Invalid handler for \"{resolvedUri}\": resulted in success but not provided a data stream", initiator.Position, initiator.File)); }
                            return false;
                        }
#if UNITY
                        UnityEngine.AwaitableCompletionSource<Stream> task = new();
                        task.SetResult(res.Stream);
                        PendingFiles.Add(new PendingFile(resolvedUri, initiator, task.Awaitable, initiatorIndex.Next()));
#else
                        PendingFiles.Add(new PendingFile(resolvedUri, initiator, Task.FromResult(res.Stream), initiatorIndex.Next()));
#endif
                        return true;
                    case SourceProviderResultType.NotFound:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        continue;
                    case SourceProviderResultType.Error:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        if (initiator is null)
                        { Diagnostics.Add(DiagnosticWithoutContext.Error(res.ErrorMessage ?? $"Failed to load \"{resolvedUri?.ToString() ?? requestedFile}\"")); }
                        else
                        { Diagnostics.Add(Diagnostic.Error(res.ErrorMessage ?? $"Failed to load \"{resolvedUri?.ToString() ?? requestedFile}\"", initiator.Position, initiator.File)); }
                        return false;
                    case SourceProviderResultType.NextHandler:
                        continue;
                }
            }
            else if (sourceProvider is ISourceProviderAsync providerAsync)
            {
                SourceProviderResultAsync res = providerAsync.TryLoad(requestedFile, currentFile);
                switch (res.Type)
                {
                    case SourceProviderResultType.Success:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        if (CompiledUris.Contains(resolvedUri))
                        { return true; }
                        CompiledUris.Add(resolvedUri);

                        if (initiator is not null) initiator.CompiledUri = resolvedUri.ToString();
                        if (res.Stream is null)
                        {
                            Diagnostics.Add(Diagnostic.Internal($"Invalid handler for \"{resolvedUri}\": resulted in success but not provided a data stream", initiator?.Position, initiator?.File));
                            return false;
                        }
                        PendingFiles.Add(new PendingFile(resolvedUri, initiator, res.Stream, initiatorIndex.Next()));
                        return true;
                    case SourceProviderResultType.NotFound:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        continue;
                    case SourceProviderResultType.Error:
                        resolvedUri = res.ResolvedUri!;
                        wasHandlerFound = true;
                        if (initiator is null)
                        { Diagnostics.Add(DiagnosticWithoutContext.Error(res.ErrorMessage ?? $"Failed to load \"{resolvedUri?.ToString() ?? requestedFile}\"")); }
                        else
                        { Diagnostics.Add(Diagnostic.Error(res.ErrorMessage ?? $"Failed to load \"{resolvedUri?.ToString() ?? requestedFile}\"", initiator.Position, initiator.File)); }
                        return false;
                    case SourceProviderResultType.NextHandler:
                        continue;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        if (initiator is not null)
        {
            if (wasHandlerFound)
            { Diagnostics.Add(Diagnostic.Error($"File \"{requestedFile}\" not found", initiator)); }
            else
            { Diagnostics.Add(Diagnostic.Error($"No handler exists for \"{requestedFile}\"", initiator)); }
        }
        else
        {
            if (wasHandlerFound)
            { Diagnostics.Add(DiagnosticWithoutContext.Error($"File \"{requestedFile}\" not found")); }
            else
            { Diagnostics.Add(DiagnosticWithoutContext.Error($"No handler exists for \"{requestedFile}\"")); }
        }
        return false;
    }

#if UNITY
    public static UnityEngine.Awaitable<Stream>? LoadSource(IEnumerable<ISourceProvider>? sourceProviders, string requestedFile, Uri? currentFile = null)
#else
    public static Task<Stream>? LoadSource(IEnumerable<ISourceProvider>? sourceProviders, string requestedFile, Uri? currentFile = null)
#endif
    {
        if (sourceProviders is null) return null;
        foreach (ISourceProvider sourceProvider in sourceProviders)
        {
            if (sourceProvider is ISourceProviderSync providerSync)
            {
                SourceProviderResultSync res = providerSync.TryLoad(requestedFile, currentFile);
                switch (res.Type)
                {
                    case SourceProviderResultType.Success:
                        if (res.Stream is null) return null;
#if UNITY
                        UnityEngine.AwaitableCompletionSource<Stream> task = new();
                        task.SetResult(res.Stream);
                        return task.Awaitable;
#else
                        return Task.FromResult(res.Stream);
#endif
                    case SourceProviderResultType.NotFound:
                        continue;
                    case SourceProviderResultType.Error:
                        return null;
                    case SourceProviderResultType.NextHandler:
                        continue;
                }
            }
            else if (sourceProvider is ISourceProviderAsync providerAsync)
            {
                SourceProviderResultAsync res = providerAsync.TryLoad(requestedFile, currentFile);
                switch (res.Type)
                {
                    case SourceProviderResultType.Success:
                        return res.Stream;
                    case SourceProviderResultType.NotFound:
                        continue;
                    case SourceProviderResultType.Error:
                        return null;
                    case SourceProviderResultType.NextHandler:
                        continue;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        return null;
    }

    public static string? LoadSourceSync(IEnumerable<ISourceProvider>? sourceProviders, string requestedFile, Uri? currentFile = null)
    {
        try
        {
            if (sourceProviders is null) return null;
#if UNITY
            UnityEngine.Awaitable<Stream>? res = LoadSource(sourceProviders, requestedFile, currentFile);
            if (res is null) return null;

            UnityEngine.Awaitable<Stream>.Awaiter awaiter = res.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnityEngine.Debug.LogWarning($"Async source providers are not supported in Unity. There is currently an async operation running in the background, and I can't do much about it.");
                return null;
            }

            using StreamReader reader = new(awaiter.GetResult());
            return reader.ReadToEnd();
#else
            Task<Stream>? res = LoadSource(sourceProviders, requestedFile, currentFile);
            if (res is null) return null;

            res.Wait();
            if (res.Result is null) return null;

            using StreamReader reader = new(res.Result);
            return reader.ReadToEnd();
#endif
        }
        catch (Exception)
        {
            return null;
        }
    }

    SourceCodeManagerResult Entry(string? file, IEnumerable<string>? additionalImports)
    {
        Uri? resolvedEntry = null;

        ImportIndex rootIndex = new();

        if (file is not null)
        {
            if (!LoadSource(null, rootIndex, file, null, out resolvedEntry))
            {
                if (resolvedEntry is null)
                { Uri.TryCreate(file, UriKind.RelativeOrAbsolute, out resolvedEntry); }

                return new()
                {
                    ParsedFiles = ImmutableArray<ParsedFile>.Empty,
                    ResolvedEntry = resolvedEntry,
                };
            }
        }

        if (additionalImports is not null)
        {
            foreach (string additionalImport in additionalImports)
            {
                LoadSource(null, rootIndex, additionalImport, null, out _);
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

        List<ImportIndex> importIndices = new(ParsedFiles.Count + 1);
        rootIndex.ToList(importIndices);

        List<ParsedFile> sortedParsedFiles = new(ParsedFiles.Count);

        foreach (ImportIndex importIndex in importIndices.Skip(1)) // Skip root
        {
            ParsedFile? found = default;
            foreach (ParsedFile parsedFile in ParsedFiles)
            {
                if (parsedFile.Index != importIndex) continue;
                found = parsedFile;
                break;
            }
            if (!found.HasValue) continue; // FIXME: is this correct?
            sortedParsedFiles.Add(found.Value);
        }

        return new()
        {
            ParsedFiles = sortedParsedFiles.ToImmutableArray(),
            ResolvedEntry = resolvedEntry,
        };
    }

    public static SourceCodeManagerResult Collect(
        string? file,
        DiagnosticsCollection diagnostics,
        IEnumerable<string> preprocessorVariables,
        IEnumerable<string>? additionalImports,
        ImmutableArray<ISourceProvider> sourceProviders,
        TokenizerSettings? tokenizerSettings)
    {
        SourceCodeManager sourceCodeManager = new(diagnostics, preprocessorVariables, sourceProviders, tokenizerSettings);
        return sourceCodeManager.Entry(file, additionalImports);
    }
}
