using System.Collections.Generic;
using System.IO;

namespace LanguageCore.Compiler
{
    using System.Linq;
    using Parser;
    using Tokenizing;

    public readonly struct CollectedAST
    {
        public readonly ParserResult ParserResult;
        public readonly string Path;
        public readonly UsingDefinition UsingDefinition;

        public CollectedAST(ParserResult parserResult, string path, UsingDefinition usingDefinition)
        {
            ParserResult = parserResult;
            Path = path;
            UsingDefinition = usingDefinition;
        }
    }

    public readonly struct CollectorResult
    {
        public readonly CollectedAST[] CollectedASTs;

        public CollectorResult(IEnumerable<CollectedAST> collectedASTs)
        {
            CollectedASTs = collectedASTs.ToArray();
        }

        public static CollectorResult Empty => new([]);
    }

    public class SourceCodeManager
    {
        readonly List<string> AlreadyCompiledCodes;
        readonly AnalysisCollection? AnalysisCollection;
        readonly PrintCallback? Print;

        public SourceCodeManager(AnalysisCollection? analysisCollection, PrintCallback? printCallback)
        {
            AlreadyCompiledCodes = new List<string>();
            AnalysisCollection = analysisCollection;
            Print = printCallback;
        }

        (string? Text, string? Path) LoadSourceCode(
            UsingDefinition @using,
            FileInfo? file,
            string? basePath)
        {
            if (@using.IsUrl)
            {
                if (!System.Uri.TryCreate(@using.Path[0].Content, System.UriKind.Absolute, out System.Uri? uri))
                { throw new SyntaxException($"Invalid uri \"{@using.Path[0].Content}\"", @using.Path[0], file?.FullName); }

                string path = uri.ToString();

                @using.CompiledUri = path;

                if (AlreadyCompiledCodes.Contains(path))
                {
                    Print?.Invoke($" Skip file \"{path}\" ...", LogType.Debug);
                    return (null, path);
                }
                AlreadyCompiledCodes.Add(path);

                Print?.Invoke($" Download file \"{path}\" ...", LogType.Debug);
                System.DateTime started = System.DateTime.Now;
                using System.Net.Http.HttpClient httpClient = new();
                System.Threading.Tasks.Task<string> req;
                try
                {
                    req = httpClient.GetStringAsync(uri);
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    throw new LanguageException($"HTTP GET Error", ex);
                }
                req.Wait();
                @using.DownloadTime = (System.DateTime.Now - started).TotalMilliseconds;

                Print?.Invoke($" File \"{path}\" downloaded", LogType.Debug);

                return (req.Result, path);
            }
            else
            {
                if (string.IsNullOrEmpty(basePath))
                {
                    FileInfo[]? configFiles = file?.Directory?.GetFiles("config.json");
                    if (configFiles != null && configFiles.Length == 1)
                    {
                        System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFiles[0].FullName));
                        if (document.RootElement.TryGetProperty("base", out System.Text.Json.JsonElement v))
                        {
                            string? b = v.GetString();
                            if (b != null) basePath = b;
                        }
                    }
                }

                string filename = @using.PathString;
                if (!filename.EndsWith(".bbc", System.StringComparison.Ordinal)) filename += ".bbc";

                List<string> searchForThese = new();

                if (file != null)
                {
                    if (basePath != null)
                    { searchForThese.Add(Path.Combine(Path.GetFullPath(basePath, file.Directory!.FullName), filename)); }

                    searchForThese.Add(Path.GetFullPath(filename, file.Directory!.FullName));
                }
                else
                {
                    if (basePath != null)
                    { searchForThese.Add(Path.Combine(Path.GetFullPath(basePath, System.Environment.CurrentDirectory), filename)); }

                    searchForThese.Add(Path.GetFullPath(filename, System.Environment.CurrentDirectory));
                }

                string? path = null;

                for (int i = 0; i < searchForThese.Count; i++)
                {
                    path = searchForThese[i];
                    if (File.Exists(path))
                    { break; }
                }

                if (path == null)
                {
                    AnalysisCollection?.Errors.Add(new Error($"File \"{path}\" not found", new Position(@using.Path), file?.FullName));
                    return (null, path);
                }

                @using.CompiledUri = path;

                if (AlreadyCompiledCodes.Contains(path))
                {
                    Print?.Invoke($" Skip file \"{path}\" ...", LogType.Debug);
                    return (null, path);
                }
                AlreadyCompiledCodes.Add(path);

                return (File.ReadAllText(path), path);
            }
        }

        CollectedAST[] ProcessFile(
            UsingDefinition @using,
            FileInfo? file,
            string? basePath)
        {
            (string? content, string? path) = LoadSourceCode(@using, file, basePath);

            if (content == null) return System.Array.Empty<CollectedAST>();
            if (path == null) throw new InternalException($"Collected source code file does not have a path (wtf?)");

            List<CollectedAST> collectedASTs = new();

            Print?.Invoke($" Parse file \"{path}\" ...", LogType.Debug);
            ParserResult parserResult2;
            {

                TokenizerResult tokenizerResult = StringTokenizer.Tokenize(content);
                AnalysisCollection?.Warnings.AddRange(tokenizerResult.Warnings);

                System.DateTime parseStarted = System.DateTime.Now;
                Print?.Invoke("  Parsing ...", LogType.Debug);

                parserResult2 = Parser.Parse(tokenizerResult.Tokens);

                if (parserResult2.Errors.Length > 0)
                { throw new LanguageException("Failed to parse", parserResult2.Errors[0].ToException()); }

                Print?.Invoke($"  Parsed in {(System.DateTime.Now - parseStarted).TotalMilliseconds} ms", LogType.Debug);
            }

            parserResult2.SetFile(path);

            collectedASTs.Add(new CollectedAST(parserResult2, path, @using));

            foreach (UsingDefinition using_ in parserResult2.Usings)
            {
                collectedASTs.AddRange(ProcessFile(using_, file, basePath));
            }

            return collectedASTs.ToArray();
        }

        CollectorResult Entry(
            UsingDefinition[] usings,
            FileInfo? file,
            string? basePath)
        {
            if (usings.Length > 0)
            { Print?.Invoke("Parse usings ...", LogType.Debug); }

            List<CollectedAST> collectedASTs = new();

            foreach (UsingDefinition usingItem in usings)
            {
                collectedASTs.AddRange(ProcessFile(usingItem, file, basePath));
            }

            return new CollectorResult(collectedASTs);
        }

        public static CollectorResult Collect(
            UsingDefinition[] usings,
            FileInfo? file,
            PrintCallback? printCallback = null,
            string? basePath = null,
            AnalysisCollection? analysisCollection = null)
        {
            SourceCodeManager sourceCodeManager = new(analysisCollection, printCallback);
            if (file != null) sourceCodeManager.AlreadyCompiledCodes.Add(file.FullName);
            return sourceCodeManager.Entry(
                usings,
                file,
                basePath);
        }
    }
}
