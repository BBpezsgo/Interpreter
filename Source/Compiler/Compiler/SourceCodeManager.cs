using System.Collections.Generic;
using System.IO;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class SourceCodeManager
    {
        List<string> AlreadyCompiledCodes;
        List<Warning> Warnings;
        List<Error> Errors;
        System.Action<string, Output.LogType> PrintCallback;

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

        (string, string) LoadSourceCode(
            UsingDefinition @using,
            FileInfo file,
            string basePath)
        {
            if (@using.IsUrl)
            {
                if (!System.Uri.TryCreate(@using.Path[0].Content, System.UriKind.Absolute, out var uri))
                { throw new SyntaxException($"Invalid uri \"{@using.Path[0].Content}\"", @using.Path[0], file.FullName); }

                string path = uri.ToString();

                @using.CompiledUri = path;

                if (AlreadyCompiledCodes.Contains(path))
                {
                    PrintCallback?.Invoke($" Skip file \"{path}\" ...", Output.LogType.Debug);
                    return (null, path);
                }
                AlreadyCompiledCodes.Add(path);

                PrintCallback?.Invoke($" Download file \"{path}\" ...", Output.LogType.Debug);
                System.DateTime started = System.DateTime.Now;
                System.Net.Http.HttpClient httpClient = new();
                System.Threading.Tasks.Task<string> req;
                try
                {
                    req = httpClient.GetStringAsync(uri);
                }
                catch (System.Net.Http.HttpRequestException ex)
                {
                    throw new Exception($"HTTP GET Error", ex);
                }
                req.Wait();
                @using.DownloadTime = (System.DateTime.Now - started).TotalMilliseconds;

                PrintCallback?.Invoke($" File \"{path}\" downloaded", Output.LogType.Debug);

                return (req.Result, path);
            }
            else
            {
                string path = null;

                if (string.IsNullOrEmpty(basePath))
                {
                    FileInfo[] configFiles = file.Directory.GetFiles("config.json");
                    if (configFiles.Length == 1)
                    {
                        System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFiles[0].FullName));
                        if (document.RootElement.TryGetProperty("base", out var v))
                        {
                            string b = v.GetString();
                            if (b != null) basePath = b;
                        }
                    }
                }

                string filename = @using.PathString.Replace("/", "\\");
                if (!filename.EndsWith("." + FileExtensions.Code)) filename += "." + FileExtensions.Code;

                List<string> searchForThese = new();
                try
                { searchForThese.Add(Path.GetFullPath(basePath.Replace("/", "\\") + filename, file.Directory.FullName)); }
                catch (System.Exception) { }
                try
                { searchForThese.Add(Path.GetFullPath(filename, file.Directory.FullName)); }
                catch (System.Exception) { }

                bool found = false;
                for (int i = 0; i < searchForThese.Count; i++)
                {
                    path = searchForThese[i];
                    if (!File.Exists(path))
                    { continue; }
                    else
                    { found = true; break; }
                }

                @using.CompiledUri = path;

                if (!found)
                {
                    Errors.Add(new Error($"File \"{path}\" not found", new Position(@using.Path), file.FullName));
                    return (null, path);
                }

                if (AlreadyCompiledCodes.Contains(path))
                {
                    PrintCallback?.Invoke($" Skip file \"{path}\" ...", Output.LogType.Debug);
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
            string basePath)
        {
            (string content, string path) = LoadSourceCode(@using, file, basePath);

            if (content == null) return System.Array.Empty<CollectedAST>();

            List<CollectedAST> collectedASTs = new();

            PrintCallback?.Invoke($" Parse file \"{path}\" ...", Output.LogType.Debug);
            ParserResult parserResult2;
            {

                var tokenizer = new Tokenizer(TokenizerSettings.Default);
                var tokens = tokenizer.Parse(content, Warnings);
                tokens = tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);

                System.DateTime parseStarted = System.DateTime.Now;
                PrintCallback?.Invoke("  Parsing ...", Output.LogType.Debug);

                Parser parser = new();
                parserResult2 = parser.Parse(tokens, Warnings);

                if (parser.Errors.Count > 0)
                { throw new Exception("Failed to parse", parser.Errors[0].ToException()); }

                PrintCallback?.Invoke($"  Parsed in {(System.DateTime.Now - parseStarted).TotalMilliseconds} ms", Output.LogType.Debug);
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
            string basePath)
        {
            if (parserResult.Usings.Length > 0)
            { PrintCallback?.Invoke("Parse usings ...", Output.LogType.Debug); }

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
            System.Action<string, Output.LogType> printCallback = null,
            string basePath = "")
        {
            SourceCodeManager sourceCodeManager = new()
            {
                AlreadyCompiledCodes = new() { file.FullName },
                Errors = new List<Error>(),
                Warnings = new List<Warning>(),
                PrintCallback = printCallback,
            };

            return sourceCodeManager.Entry(
                parserResult,
                file,
                parserSettings,
                basePath
                );
        }
    }
}
