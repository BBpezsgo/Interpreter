
namespace IngameCoding.BBCode
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    public struct AnalysisResult
    {
        public Exception TokenizerFatalError;
        public Exception ParserFatalError;
        public Exception CompilerFatalError;

        public Error[] TokenizerErrors;
        public Error[] ParserErrors;
        public Error[] CompilerErrors;

        public ParserResult ParserResult => parserResult ?? throw new System.NullReferenceException();
        public ParserResult? parserResult;
        public Compiler.Compiler.CompilerResult CompilerResult => compilerResult ?? throw new System.NullReferenceException();
        public Compiler.Compiler.CompilerResult compilerResult;
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

        public AnalysisResult SetCompilerResult(Compiler.Compiler.CompilerResult compilerResult, Error[] errors, Warning[] warnings, Information[] informations, Hint[] hints)
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
        };

        public bool Tokenized => Tokens != null;
        public bool Parsed => parserResult.HasValue;
        public bool Compiled => compilerResult != null;

        public bool TokenizingSuccess => TokenizerFatalError == null && TokenizerErrors.Length == 0;
        public bool ParsingSuccess => ParserFatalError == null && ParserErrors.Length == 0 && TokenizingSuccess;
        public bool CompilingSuccess => CompilerFatalError == null && CompilerErrors.Length == 0 && ParsingSuccess;

        public void CheckFilePaths(System.Action<string> NotSetCallback)
        {
            if (this.Compiled)
            { this.CompilerResult.CheckFilePaths(NotSetCallback); return; }
            if (!this.Parsed) return;
            this.ParserResult.CheckFilePaths(NotSetCallback);
        }
    }

    public class Analysis
    {
        static Dictionary<string, BuiltinFunction> BuiltinFunctions => new();
        static Dictionary<string, System.Func<IStruct>> BuiltinStructs => new();

        static Compiler.Compiler.CompilerResult Compile(ParserResult parserResult, List<Warning> warnings, List<Error> errors, System.IO.DirectoryInfo directory, string path, List<Hint> hints, List<Information> informations)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();
            Dictionary<string, ClassDefinition> Classes = new();
            List<Statement_HashInfo> Hashes = new(parserResult.Hashes);

            parserResult.UsingsAnalytics.Clear();

            List<string> parsedUsings = new();

            for (int i = 0; i < parserResult.Usings.Count; i++)
            {
                var usingItem = parserResult.Usings[i];
                var usingFile = directory.FullName + "\\" + usingItem.PathString + "." + FileExtensions.Code;
                UsingAnalysis usingAnalysis = new()
                {
                    Path = usingFile,
                    ParseTime = -1d,
                };

                if (System.IO.File.Exists(usingFile))
                {
                    usingAnalysis.Found = true;

                    if (parsedUsings.Contains(usingFile))
                    {
                        warnings.Add(new Warning($"File \"{usingFile}\" already imported", new Position(usingItem.Path), path));
                        parserResult.UsingsAnalytics.Add(usingAnalysis);
                        continue;
                    }
                    parsedUsings.Add(usingFile);

                    System.TimeSpan parseStarted = System.DateTime.Now.TimeOfDay;

                    string code = System.IO.File.ReadAllText(usingFile);

                    Tokenizer tokenizer = new(TokenizerSettings.Default);
                    (Token[] tokens, _) = tokenizer.Parse(code);

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
                        func.Statements?.Clear();
                        Functions.Add(id, func);
                    }

                    foreach (var @struct in parserResult2_v.Structs)
                    {
                        if (Structs.ContainsKey(@struct.Key)) continue;
                        Structs.Add(@struct.Key, @struct.Value);
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
                { throw new CompilerException($"Function '{id}' already exists", func.Name, path); }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }
            foreach (var @class in parserResult.Classes)
            {
                Classes.Add(@class.Key, @class.Value);
            }

            CodeGenerator codeGenerator = new()
            { warnings = warnings, errors = errors, hints = hints, informations = informations };
            var codeGeneratorResult = codeGenerator.GenerateCode(Functions, Structs, Classes, Hashes.ToArray(), parserResult.GlobalVariables, BuiltinFunctions, BuiltinStructs, Compiler.Compiler.CompilerSettings.Default, null, Compiler.Compiler.CompileLevel.Exported);

            var compilerResult = new Compiler.Compiler.CompilerResult()
            {
                compiledStructs = codeGeneratorResult.compiledStructs,
                compiledFunctions = codeGeneratorResult.compiledFunctions,
                compiledGlobalVariables = codeGenerator.compiledGlobalVariables,
                compiledVariables = codeGenerator.compiledVariables,
            };

            return compilerResult;
        }

        static ParserResult? Parse(Token[] tokens, List<Warning> warnings, List<Error> errors, string path, out Exception fatalError, out Token[] modifyedTokens)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            Parser.Parser parser = new();
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
        }

        public static AnalysisResult Analyze(string code, System.IO.FileInfo file, string path)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            AnalysisResult result = AnalysisResult.Empty();

            try
            {
                Tokenizer tokenizer = new(TokenizerSettings.Default);
                return Analyze(tokenizer.Parse(code).Item1, file, path);
            }
            catch (Exception error)
            {
                result.TokenizerFatalError = error;
                return result;
            }
        }

        public static AnalysisResult Analyze(Token[] tokens, System.IO.FileInfo file, string path)
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
                    (Token[] tokens_, _) = tokenizer.Parse(code_);
                    if (tokens_ == null) continue;
                    if (tokens_.Length < 3) continue;
                    Parser.Parser parser = new();
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

        public static AnalysisResult Analyze(Token[] tokens, ParserResult parserResult, Warning[] warnings_, System.IO.FileInfo file, string path)
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
        }
    }
}
