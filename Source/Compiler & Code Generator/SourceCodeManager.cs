using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace LanguageCore.Compiler
{
    using System.Diagnostics.CodeAnalysis;
    using Parser;
    using Tokenizing;

    public readonly struct CollectedAST
    {
        public readonly ParserResult ParserResult;
        public readonly string Path;
        public readonly UsingDefinition Using;

        public CollectedAST(ParserResult parserResult, string path, UsingDefinition @using)
        {
            ParserResult = parserResult;
            Path = path;
            Using = @using;
        }
    }

    public readonly struct CollectorResult
    {
        public readonly CollectedAST[] CollectedASTs;

        public CollectorResult(
            IEnumerable<CollectedAST> collectedASTs)
        {
            CollectedASTs = collectedASTs.ToArray();
        }

        public static CollectorResult Empty => new(
            Enumerable.Empty<CollectedAST>());
    }

    public class SourceCodeManager
    {
        readonly List<Uri> AlreadyLoadedCodes;
        readonly AnalysisCollection? AnalysisCollection;
        readonly PrintCallback? Print;

        public SourceCodeManager(AnalysisCollection? analysisCollection, PrintCallback? printCallback)
        {
            AlreadyLoadedCodes = new List<Uri>();
            AnalysisCollection = analysisCollection;
            Print = printCallback;
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

        bool FromWeb(Uri uri, out ParserResult ast)
        {
            Print?.Invoke($" Download file \"{uri}\" ...", LogType.Debug);

            using HttpClient client = new();
            using HttpResponseMessage res = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
            res.EnsureSuccessStatusCode();

            TokenizerResult tokens;
            if (res.Content.Headers.ContentLength.HasValue)
            {
                using ConsoleProgressBar progress = new(ConsoleColor.DarkGray, true);
                tokens = StreamTokenizer.Tokenize(res.Content.ReadAsStream(), progress, (int)res.Content.Headers.ContentLength.Value);
            }
            else
            {
                tokens = StreamTokenizer.Tokenize(res.Content.ReadAsStream());
            }

            AnalysisCollection?.Warnings.AddRange(tokens.Warnings);

            ast = Parser.Parse(tokens);
            AnalysisCollection?.Errors.AddRange(ast.Errors);

            return true;
        }

        bool FromFile(string path, out ParserResult ast)
        {
            ast = default;

            if (!File.Exists(path))
            { return false; }

            Print?.Invoke($" Load local file \"{path}\" ...", LogType.Debug);

            TokenizerResult tokens = StreamTokenizer.Tokenize(path);
            AnalysisCollection?.Warnings.AddRange(tokens.Warnings);

            ast = Parser.Parse(tokens);
            AnalysisCollection?.Errors.AddRange(ast.Errors);

            return true;
        }

        bool FromAnywhere(UsingDefinition @using, Uri uri, out ParserResult? ast)
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
                success = FromFile(filePath, out ParserResult _ast);
                if (success)
                {
                    _ast.SetFile(uri);
                    ast = _ast;
                }
            }
            else
            {
                success = FromWeb(uri, out ParserResult _ast);
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

                if (basePath != null)
                { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, file.Directory!.FullName), filename), UriKind.Absolute); }

                yield return new Uri(Path.GetFullPath(filename, file.Directory!.FullName), UriKind.Absolute);
            }
            else
            {
                if (basePath != null)
                { yield return new Uri(Path.Combine(Path.GetFullPath(basePath, Environment.CurrentDirectory), filename), UriKind.Absolute); }

                yield return new Uri(Path.GetFullPath(filename, Environment.CurrentDirectory), UriKind.Absolute);
            }
        }

        bool ProcessFiles(UsingDefinition @using, IEnumerable<Uri> searchForThese, out ParserResult? ast, [NotNullWhen(true)] out Uri? uri)
        {
            foreach (Uri item in searchForThese)
            {
                if (FromAnywhere(@using, item, out ast))
                {
                    uri = item;
                    return true;
                }
            }

            ast = default;
            uri = default;
            return false;
        }

        CollectedAST[] ProcessFile(
            UsingDefinition @using,
            Uri? parent,
            string? basePath)
        {
            if (!ProcessFiles(@using, GetSearches(@using, parent, basePath), out ParserResult? ast, out Uri? path))
            {
                AnalysisCollection?.Errors.Add(new Error($"File \"{@using.PathString}\" not found", new Position(@using.Path), parent));
                return Array.Empty<CollectedAST>();
            }

            if (!ast.HasValue)
            { return Array.Empty<CollectedAST>(); }

            List<CollectedAST> collectedASTs = new();

            collectedASTs.Add(new CollectedAST(ast.Value, path.ToString(), @using));

            foreach (UsingDefinition using_ in ast.Value.Usings)
            { collectedASTs.AddRange(ProcessFile(using_, path, null)); }

            return collectedASTs.ToArray();
        }

        CollectorResult Entry(
            UsingDefinition[] usings,
            Uri? file,
            string? basePath)
        {
            if (usings.Length > 0)
            { Print?.Invoke("Parse usings ...", LogType.Debug); }

            List<CollectedAST> collectedASTs = new();

            foreach (UsingDefinition usingItem in usings)
            { collectedASTs.AddRange(ProcessFile(usingItem, file, basePath)); }

            AnalysisCollection?.Throw();

            return new CollectorResult(collectedASTs);
        }

        public static CollectorResult Collect(
            UsingDefinition[] usings,
            Uri? file,
            PrintCallback? printCallback = null,
            string? basePath = null,
            AnalysisCollection? analysisCollection = null)
        {
            SourceCodeManager sourceCodeManager = new(analysisCollection, printCallback);
            if (file != null) sourceCodeManager.AlreadyLoadedCodes.Add(file);
            return sourceCodeManager.Entry(usings, file, basePath);
        }

        public static CollectorResult Collect(
            UsingDefinition[] usings,
            FileInfo? file,
            PrintCallback? printCallback = null,
            string? basePath = null,
            AnalysisCollection? analysisCollection = null)
        {
            Uri? fileUri = file is null ? null : new Uri(file.FullName, UriKind.Absolute);
            SourceCodeManager sourceCodeManager = new(analysisCollection, printCallback);
            if (fileUri != null) sourceCodeManager.AlreadyLoadedCodes.Add(fileUri);
            return sourceCodeManager.Entry(usings, fileUri, basePath);
        }
    }
}
