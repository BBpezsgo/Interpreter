using System.Collections.Generic;
using System.IO;

namespace LanguageCore.BBCode.Compiler
{
    using LanguageCore.Parser;
    using LanguageCore.Tokenizing;

    internal class SourceCodeManager
    {
        readonly List<string> AlreadyCompiledCodes;
        readonly List<Warning> Warnings;
        readonly List<Error> Errors;
        PrintCallback? Print;

        public SourceCodeManager()
        {
            AlreadyCompiledCodes = new List<string>();
            Warnings = new List<Warning>();
            Errors = new List<Error>();
            Print = null;
        }

        internal struct CollectedAST
        {
            internal ParserResult ParserResult;
            internal string Path;
            internal UsingDefinition UsingDefinition;
        }

        internal struct Result
        {
            internal CollectedAST[] CollectedASTs;

            internal Warning[] Warnings;
            internal Error[] Errors;
        }

        (string?, string?) LoadSourceCode(
            UsingDefinition @using,
            FileInfo file,
            string? basePath)
        {
            if (@using.IsUrl)
            {
                if (!System.Uri.TryCreate(@using.Path[0].Content, System.UriKind.Absolute, out var uri))
                { throw new SyntaxException($"Invalid uri \"{@using.Path[0].Content}\"", @using.Path[0], file.FullName); }

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
                System.Net.Http.HttpClient httpClient = new();
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
                string? path = null;

                if (string.IsNullOrEmpty(basePath))
                {
                    FileInfo[]? configFiles = file.Directory?.GetFiles("config.json");
                    if (configFiles != null && configFiles.Length == 1)
                    {
                        System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFiles[0].FullName));
                        if (document.RootElement.TryGetProperty("base", out var v))
                        {
                            string? b = v.GetString();
                            if (b != null) basePath = b;
                        }
                    }
                }

                string filename = @using.PathString.Replace("/", "\\");
                if (!filename.EndsWith(".bbc")) filename += ".bbc";

                List<string> searchForThese = new();
                try
                { searchForThese.Add(Path.GetFullPath((basePath?.Replace("/", "\\") ?? string.Empty) + filename, file.Directory!.FullName!)); }
                catch (System.Exception) { }
                try
                { searchForThese.Add(Path.GetFullPath(filename, file.Directory!.FullName!)); }
                catch (System.Exception) { }

                for (int i = 0; i < searchForThese.Count; i++)
                {
                    path = searchForThese[i];
                    if (!File.Exists(path))
                    { path = null; }
                    else
                    { break; }
                }

                if (path == null)
                {
                    Errors.Add(new Error($"File \"{path}\" not found", new Position(@using.Path), file.FullName));
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
            FileInfo file,
            ParserSettings parserSettings,
            string? basePath)
        {
            (string? content, string? path) = LoadSourceCode(@using, file, basePath);

            if (content == null) return System.Array.Empty<CollectedAST>();
            if (path == null) throw new InternalException($"Collected source code file does not have a path (wtf?)");

            List<CollectedAST> collectedASTs = new();

            Print?.Invoke($" Parse file \"{path}\" ...", LogType.Debug);
            ParserResult parserResult2;
            {

                Tokenizer tokenizer = new(TokenizerSettings.Default);
                Token[] tokens = tokenizer.Parse(content, Warnings);

                System.DateTime parseStarted = System.DateTime.Now;
                Print?.Invoke("  Parsing ...", LogType.Debug);

                parserResult2 = Parser.Parse(tokens);

                if (parserResult2.Errors.Length > 0)
                { throw new LanguageException("Failed to parse", parserResult2.Errors[0].ToException()); }

                Print?.Invoke($"  Parsed in {(System.DateTime.Now - parseStarted).TotalMilliseconds} ms", LogType.Debug);
            }

            parserResult2.SetFile(path);

            collectedASTs.Add(new CollectedAST()
            {
                ParserResult = parserResult2,
                Path = path,
                UsingDefinition = @using,
            });

            if (parserSettings.PrintInfo)
            { parserResult2.WriteToConsole($"PARSER INFO FOR '{@using.PathString}'"); }

            foreach (UsingDefinition using_ in parserResult2.Usings)
            {
                collectedASTs.AddRange(ProcessFile(using_, file, parserSettings, basePath));
            }

            return collectedASTs.ToArray();
        }

        Result Entry(
            ParserResult parserResult,
            FileInfo file,
            ParserSettings parserSettings,
            string? basePath)
        {
            if (parserResult.Usings.Length > 0)
            { Print?.Invoke("Parse usings ...", LogType.Debug); }

            List<CollectedAST> collectedASTs = new();

            foreach (UsingDefinition usingItem in parserResult.Usings)
            {
                collectedASTs.AddRange(ProcessFile(usingItem, file, parserSettings, basePath));
                if (Errors.Count > 0)
                { throw new System.Exception($"Failed to compile file {usingItem.PathString}", Errors[0].ToException()); }
            }

            return new Result()
            {
                CollectedASTs = collectedASTs.ToArray(),

                Warnings = Warnings.ToArray(),
                Errors = Errors.ToArray(),
            };
        }

        internal static Result Collect(
            ParserResult parserResult,
            FileInfo file,
            ParserSettings parserSettings,
            PrintCallback? printCallback = null,
            string? basePath = null)
        {
            SourceCodeManager sourceCodeManager = new()
            { Print = printCallback, };
            sourceCodeManager.AlreadyCompiledCodes.Add(file.FullName);
            return sourceCodeManager.Entry(
                parserResult,
                file,
                parserSettings,
                basePath
                );
        }
    }
}
