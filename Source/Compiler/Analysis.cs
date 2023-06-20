﻿
namespace IngameCoding.BBCode.Analysis
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    public struct AnalysisResult
    {
        public Exception TokenizerFatalError;
        public Exception ParserFatalError;
        public Exception CompilerFatalError;

        public Error[] TokenizerErrors;
        public Error[] ParserErrors;
        public Error[] CompilerErrors;

        public Warning[] TokenizerWarnings;
        public SimpleToken[] TokenizerInicodeChars;

        public ParserResult ParserResult => parserResult ?? throw new System.NullReferenceException();
        public ParserResult? parserResult;
        public Compiler.CompilerResult CompilerResult => compilerResult ?? throw new System.NullReferenceException();
        public Compiler.CompilerResult compilerResult;
        public Warning[] Warnings;
        public Hint[] Hints;
        public Information[] Informations;
        public Token[] Tokens;
        public string[] FileReferences;

        public AnalysisResult SetTokenizerResult(Exception fatalError, Error[] errors)
        {
            this.Tokens = null;
            this.TokenizerFatalError = fatalError;
            if (errors != null) this.TokenizerErrors = errors;

            return this;
        }

        public AnalysisResult SetParserResult(Exception fatalError, Error[] errors, Warning[] warnings)
        {
            this.parserResult = null;
            this.ParserFatalError = fatalError;
            if (errors != null) this.ParserErrors = errors;
            if (warnings != null) this.Warnings = warnings;

            return this;
        }

        public AnalysisResult SetCompilerResult(Exception fatalError, Error[] errors, Warning[] warnings, Information[] informations, Hint[] hints)
        {
            this.compilerResult = null;
            this.CompilerFatalError = fatalError;
            if (errors != null) this.CompilerErrors = errors;
            if (warnings != null) this.Warnings = warnings;
            if (informations != null) this.Informations = informations;
            if (hints != null) this.Hints = hints;

            return this;
        }

        public AnalysisResult SetTokenizerResult(Token[] tokens, Error[] errors)
        {
            this.Tokens = tokens;
            this.TokenizerFatalError = null;
            if (errors != null) this.TokenizerErrors = errors;

            return this;
        }

        public AnalysisResult SetParserResult(ParserResult parserResult, Error[] errors, Warning[] warnings)
        {
            this.parserResult = parserResult;
            this.ParserFatalError = null;
            if (errors != null) this.ParserErrors = errors;
            if (warnings != null) this.Warnings = warnings;

            return this;
        }

        public AnalysisResult SetCompilerResult(Compiler.CompilerResult compilerResult, Error[] errors, Warning[] warnings, Information[] informations, Hint[] hints)
        {
            this.compilerResult = compilerResult;
            this.CompilerFatalError = null;
            if (errors != null) this.CompilerErrors = errors;
            if (warnings != null) this.Warnings = warnings;
            if (informations != null) this.Informations = informations;
            if (hints != null) this.Hints = hints;

            return this;
        }

        public static AnalysisResult Empty() => new()
        {
            CompilerErrors = System.Array.Empty<Error>(),
            CompilerFatalError = null,
            FileReferences = System.Array.Empty<string>(),
            Hints = System.Array.Empty<Hint>(),
            Informations = System.Array.Empty<Information>(),
            ParserErrors = System.Array.Empty<Error>(),
            ParserFatalError = null,
            TokenizerErrors = System.Array.Empty<Error>(),
            TokenizerFatalError = null,
            Tokens = null,
            Warnings = System.Array.Empty<Warning>(),
            TokenizerWarnings = System.Array.Empty<Warning>(),
            TokenizerInicodeChars = System.Array.Empty<SimpleToken>(),
        };

        public bool Tokenized => Tokens != null;
        public bool Parsed => parserResult.HasValue;
        public bool Compiled => compilerResult != null;

        public bool TokenizingSuccess => TokenizerFatalError == null && TokenizerErrors.Length == 0;
        public bool ParsingSuccess => ParserFatalError == null && ParserErrors.Length == 0 && TokenizingSuccess;
        public bool CompilingSuccess => CompilerFatalError == null && CompilerErrors.Length == 0 && ParsingSuccess;

        public void CheckFilePaths(System.Action<string> NotSetCallback)
        {
            if (this.Compiled) return;
            if (!this.Parsed) return;
            this.ParserResult.CheckFilePaths(NotSetCallback);
        }
    }

    public class Analysis
    {
        static Dictionary<string, BuiltinFunction> BuiltinFunctions => new();

        public static AnalysisResult Analyze(string code, FileInfo file, string path)
        {
            if (path is null) throw new System.ArgumentNullException(nameof(path));

            AnalysisResult result = AnalysisResult.Empty();
            List<Warning> tokenizerWarnings = new();
            try
            {
                Tokenizer tokenizer = new(TokenizerSettings.Default);
                result = Analyze(tokenizer.Parse(code, tokenizerWarnings, path, out var unicodeChars), file, path);
                result.Tokens = result.Tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);
                result.TokenizerWarnings = tokenizerWarnings.ToArray();
                result.TokenizerInicodeChars = unicodeChars;
            }
            catch (Exception error)
            {
                result.TokenizerFatalError = error;
            }
            catch (System.Exception error)
            {
                result.TokenizerFatalError = new Exception(error.Message, error);
            }
            return result;
        }

        public static string[] FileReferences(System.IO.FileInfo file, string path)
        {
            List<string> fileReferences = new();

            if (file != null)
            {
                System.IO.DirectoryInfo dir = file.Directory;
                System.IO.FileInfo[] files = dir.GetFiles();
                foreach (var file_ in files)
                {
                    if (file_.Extension.ToLower() != ".bbc") continue;
                    string code_ = System.IO.File.ReadAllText(file_.FullName);
                    Tokenizer tokenizer = new(TokenizerSettings.Default);
                    Token[] tokens_ = tokenizer.Parse(code_);
                    tokens_ = tokens_.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);
                    if (tokens_ == null) continue;
                    if (tokens_.Length < 3) continue;
                    Parser parser = new();
                    ParserResultHeader codeHeader = parser.ParseCodeHeader(tokens_, new List<Warning>());
                    for (int i = 0; i < codeHeader.Usings.Count; i++)
                    {
                        var @using = codeHeader.Usings[i];
                        var usingFile = dir.FullName + "\\" + @using.PathString + "." + FileExtensions.Code;
                        if (!System.IO.File.Exists(usingFile)) continue;
                        if (usingFile.Replace('\\', '/') != path) continue;
                        fileReferences.Add(file_.FullName);
                    }
                }
            }

            return fileReferences.ToArray();
        }

        static Compiler.CompilerResult Compile(ParserResult parserResult, List<Warning> warnings, List<Error> errors, System.IO.DirectoryInfo directory, string path, List<Hint> hints, List<Information> informations)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();
            Dictionary<string, ClassDefinition> Classes = new();
            List<Statement_HashInfo> Hashes = new(parserResult.Hashes);

            parserResult.UsingsAnalytics.Clear();

            List<string> parsedUsings = new();

            string basePath = null;

            if (string.IsNullOrEmpty(basePath))
            {
                FileInfo[] configFiles = directory.GetFiles("config.json");
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

            for (int i = 0; i < parserResult.Usings.Length; i++)
            {
                var usingItem = parserResult.Usings[i];
                var usingFile = directory.FullName + "\\" + usingItem.PathString + "." + FileExtensions.Code;

                UsingAnalysis usingAnalysis = new()
                {
                    Path = usingFile,
                    ParseTime = -1d,
                };

                List<string> searchForThese = new();

                if (basePath != null)
                {
                    try
                    { searchForThese.Add(Path.GetFullPath(basePath.Replace("/", "\\") + usingItem.PathString + "." + FileExtensions.Code, directory.FullName)); }
                    catch (System.Exception) { }
                }

                try
                { searchForThese.Add(Path.GetFullPath(usingItem.PathString + "." + FileExtensions.Code, directory.FullName)); }
                catch (System.Exception) { }

                bool found = false;
                for (int j = 0; j < searchForThese.Count; j++)
                {
                    usingFile = searchForThese[j];
                    if (!File.Exists(usingFile))
                    { continue; }
                    else
                    { found = true; break; }
                }

                if (found)
                {
                    usingAnalysis.Found = true;
                    usingItem.CompiledUri = usingFile;

                    if (parsedUsings.Contains(usingFile))
                    {
                        warnings.Add(new Warning($"File \"{usingFile}\" already imported", new Position(usingItem.Path), path));
                        parserResult.UsingsAnalytics.Add(usingAnalysis);
                        continue;
                    }
                    parsedUsings.Add(usingFile);

                    System.TimeSpan parseStarted = System.DateTime.Now.TimeOfDay;

                    string code = File.ReadAllText(usingFile);

                    Tokenizer tokenizer = new(TokenizerSettings.Default);
                    Token[] tokens = tokenizer.Parse(code);
                    tokens = tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);

                    List<Error> parserErrors = new();
                    List<Warning> parserWarnings = new();

                    ParserResult? parserResult2_ = Parse(tokens, parserWarnings, parserErrors, usingFile.Replace('\\', '/'), out Exception parserFatalError, out _);

                    if (parserErrors.Count > 0)
                    { throw new Exception("Failed to parse", parserErrors[0].ToException()); }

                    if (parserFatalError != null)
                    { throw new Exception("Failed to parse", parserFatalError); }

                    if (!parserResult2_.HasValue)
                    { throw new Exception("Failed to parse", new System.Exception("Result is null")); }

                    ParserResult parserResult2_v = parserResult2_.Value;

                    parserResult2_v.SetFile(usingFile);

                    foreach (var func in parserResult2_v.Functions)
                    {
                        var id = func.ID();
                        if (Functions.ContainsKey(id)) continue;
                        func.Statements = System.Array.Empty<Statement>();
                        Functions.Add(id, func);
                    }

                    foreach (var @struct in parserResult2_v.Structs)
                    {
                        if (Classes.ContainsKey(@struct.Name.Content) || Structs.ContainsKey(@struct.Name.Content))
                        {
                            errors.Add(new Error($"Type '{@struct.Name.Content}' already exists", @struct.Name));
                        }
                        else
                        {
                            Structs.Add(@struct.Name.Content, @struct);
                        }
                    }

                    foreach (var @class in parserResult2_v.Classes)
                    {
                        if (Classes.ContainsKey(@class.Name.Content) || Structs.ContainsKey(@class.Name.Content))
                        {
                            errors.Add(new Error($"Type '{@class.Name.Content}' already exists", @class.Name));
                        }
                        else
                        {
                            Classes.Add(@class.Name.Content, @class);
                        }
                    }

                    Hashes.AddRange(parserResult2_v.Hashes);

                    System.TimeSpan parseTime = System.DateTime.Now.TimeOfDay - parseStarted;
                    usingAnalysis.ParseTime = parseTime.TotalMilliseconds;
                }
                else
                {
                    usingAnalysis.Found = false;

                    System.IO.FileInfo[] files = directory.GetFiles("*.bbc", System.IO.SearchOption.TopDirectoryOnly);
                    int largestSimilarityI = -1;
                    int largestSimilarity = int.MaxValue;
                    for (int fileI = 0; fileI < files.Length; fileI++)
                    {
                        string file = files[fileI].Name;
                        if (!file.EndsWith("." + FileExtensions.Code)) continue;
                        file = file[..^("." + FileExtensions.Code).Length];
                        int similarity = file.LevenshteinDis(usingItem.PathString);
                        if (largestSimilarityI == -1)
                        {
                            largestSimilarityI = fileI;
                            largestSimilarity = similarity;
                            continue;
                        }
                        if (largestSimilarity > similarity)
                        {
                            largestSimilarityI = fileI;
                            largestSimilarity = similarity;
                        }
                    }

                    if (largestSimilarityI != -1)
                    {
                        string hintedFile = files[largestSimilarityI].Name[..^("." + FileExtensions.Code).Length];
                        warnings.Add(new Warning($"File \"{usingItem.PathString + "." + FileExtensions.Code}\" is not found in the current directory. Did you mean \"{hintedFile}\"?", new Position(usingItem.Path), path));
                    }
                    else
                    {
                        warnings.Add(new Warning($"File \"{usingItem.PathString + "." + FileExtensions.Code}\" is not found in the current directory.", new Position(usingItem.Path), path));
                    }
                }

                parserResult.UsingsAnalytics.Add(usingAnalysis);
            }

            foreach (var func in parserResult.Functions)
            {
                var id = func.ID();
                if (Functions.ContainsKey(id))
                { throw new CompilerException($"Function '{id}' already exists", func.Identifier, path); }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                if (Classes.ContainsKey(@struct.Name.Content) || Structs.ContainsKey(@struct.Name.Content))
                {
                    errors.Add(new Error($"Type '{@struct.Name.Content}' already exists", @struct.Name));
                }
                else
                {
                    Structs.Add(@struct.Name.Content, @struct);
                }
            }
            foreach (var @class in parserResult.Classes)
            {
                if (Classes.ContainsKey(@class.Name.Content) || Structs.ContainsKey(@class.Name.Content))
                {
                    errors.Add(new Error($"Type '{@class.Name.Content}' already exists", @class.Name));
                }
                else
                {
                    Classes.Add(@class.Name.Content, @class);
                }
            }

            var compilerResult1 = Compiler.Compile(
                parserResult,
                BuiltinFunctions,
                null,
                ParserSettings.Default,
                null,
                basePath);

            warnings.AddRange(compilerResult1.Warnings);
            errors.AddRange(compilerResult1.Errors);

            var codeGeneratorResult = CodeGenerator.Generate(compilerResult1, Compiler.CompilerSettings.Default, null, Compiler.CompileLevel.Exported);

            hints.AddRange(codeGeneratorResult.Hints);
            informations.AddRange(codeGeneratorResult.Informations);
            warnings.AddRange(codeGeneratorResult.Warnings);
            errors.AddRange(codeGeneratorResult.Errors);

            return new Compiler.CompilerResult()
            {
                compiledStructs = codeGeneratorResult.Structs.ToArray(),
                compiledFunctions = codeGeneratorResult.Functions.ToArray(),
            };
        }

        static ParserResult? Parse(Token[] tokens, List<Warning> warnings, List<Error> errors, string path, out Exception fatalError, out Token[] modifyedTokens)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            Parser parser = new();
            fatalError = null;

            try
            {
                ParserResult result = parser.Parse(tokens, warnings);
                result.SetFile(path);
                errors.AddRange(parser.Errors);
                modifyedTokens = tokens;

                return result;
            }
            catch (Exception error)
            {
                fatalError = error;
                errors.AddRange(parser.Errors);
                modifyedTokens = tokens;

                return null;
            }
            catch (System.Exception error)
            {
                fatalError = new Exception(error.Message, error);
                errors.AddRange(parser.Errors);
                modifyedTokens = tokens;

                return null;
            }
        }

        static AnalysisResult Analyze(Token[] tokens, System.IO.FileInfo file, string path)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            List<Error> errors = new();
            List<Warning> warnings = new();

            ParserResult? parserResult = Parse(tokens, warnings, errors, path, out Exception parserError, out var modifyedTokens);

            if (errors.Count > 0 || parserError != null)
                return AnalysisResult
                    .Empty()
                    .SetTokenizerResult(modifyedTokens, null)
                    .SetParserResult(parserError, errors.ToArray(), warnings.ToArray());
            if (!parserResult.HasValue)
                return AnalysisResult
                    .Empty()
                    .SetTokenizerResult(modifyedTokens, null)
                    .SetParserResult(new Exception("Parse failed", new System.Exception("Parser result is null")), errors.ToArray(), warnings.ToArray());

            parserResult.Value.SetFile(path);

            return Analyze(modifyedTokens, parserResult.Value, warnings.ToArray(), file, path);
        }

        static AnalysisResult Analyze(Token[] tokens, ParserResult parserResult, Warning[] warnings_, System.IO.FileInfo file, string path)
        {
            if (string.IsNullOrEmpty(path))
            { throw new System.ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path)); }

            var parserResult_ = parserResult;
            parserResult_.SetFile(path);

            List<Error> compilerErrors = new();
            List<Warning> warnings = new(warnings_);
            List<Information> compilerInformations = new();
            List<Hint> compilerHints = new();

            try
            {
                var compilerResult = Compile(parserResult_, warnings, compilerErrors, file?.Directory, path, compilerHints, compilerInformations);
                return AnalysisResult
                    .Empty()
                    .SetTokenizerResult(tokens, null)
                    .SetParserResult(parserResult, null, null)
                    .SetCompilerResult(compilerResult, compilerErrors.ToArray(), warnings.ToArray(), compilerInformations.ToArray(), compilerHints.ToArray());
            }
            catch (Exception error)
            {
                return AnalysisResult
                   .Empty()
                   .SetTokenizerResult(tokens, null)
                   .SetParserResult(parserResult, null, null)
                   .SetCompilerResult(error, compilerErrors.ToArray(), warnings.ToArray(), compilerInformations.ToArray(), compilerHints.ToArray());
            }
            catch (System.Exception error)
            {
                return AnalysisResult
                   .Empty()
                   .SetTokenizerResult(tokens, null)
                   .SetParserResult(parserResult, null, null)
                   .SetCompilerResult(new Exception(error.Message, error), compilerErrors.ToArray(), warnings.ToArray(), compilerInformations.ToArray(), compilerHints.ToArray());
            }
        }
    }
}
