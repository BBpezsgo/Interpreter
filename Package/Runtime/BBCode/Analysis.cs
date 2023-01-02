
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
        public Compiler.Compiler.CompilerResult? compilerResult;
        public Warning[] Warnings;
        public Hint[] Hints;
        public Information[] Informations;
        public Token[] Tokens;
        public string[] FileReferences;

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
        public bool Compiled => compilerResult.HasValue;

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

        static Compiler.Compiler.CompilerResult Compile(ParserResult parserResult, List<Warning> warnings, List<Error> errors, System.IO.DirectoryInfo directory, List<Hint> hints, List<Information> informations)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();
            List<Statement_HashInfo> Hashes = new(parserResult.Hashes);

            parserResult.UsingsAnalytics.Clear();

            for (int i = 0; i < parserResult.Usings.Count; i++)
            {
                var usingItem = parserResult.Usings[i];
                var usingFile = directory.FullName + "\\" + usingItem.PathString + "." + Core.FileExtensions.Code;
                UsingAnalysis usingAnalysis = new()
                {
                    Path = usingFile
                };

                if (System.IO.File.Exists(usingFile))
                {
                    usingAnalysis.Found = true;
                    System.TimeSpan parseStarted = System.DateTime.Now.TimeOfDay;

                    string code = System.IO.File.ReadAllText(usingFile);

                    Tokenizer tokenizer = new(TokenizerSettings.Default);
                    (Token[] tokens, _) = tokenizer.Parse(code);

                    List<Error> parserErrors = new();
                    List<Warning> parserWarnings = new();

                    ParserResult? parserResult2_ = Parse(tokens, parserWarnings, parserErrors, usingFile.Replace('\\', '/'), out Exception parserFatalError, out _);

                    if (parserErrors.Count > 0)
                    { throw new Exception("Failed to parse", new Exception(parserErrors[0].Message, parserErrors[0].position)); }

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
                }

                parserResult.UsingsAnalytics.Add(usingAnalysis);
            }

            foreach (var func in parserResult.Functions)
            {
                var id = func.ID();
                if (Functions.ContainsKey(id))
                { throw new ParserException($"Function '{id}' already exists", func.Name); }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }

            CodeGenerator codeGenerator = new()
            { warnings = warnings, errors = errors, hints = hints, informations = informations };
            var codeGeneratorResult = codeGenerator.GenerateCode(Functions, Structs, Hashes.ToArray(), parserResult.GlobalVariables, BuiltinFunctions, BuiltinStructs, Compiler.Compiler.CompilerSettings.Default);

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
            ParserResult result;
            fatalError = null;

            try
            {
                result = parser.Parse(tokens, warnings);
            }
            catch (Exception error)
            {
                fatalError = error;

                if (parser.Errors.Count > 0)
                { errors.AddRange(parser.Errors); }

                modifyedTokens = tokens;
                return null;
            }

            result.SetFile(path);

            modifyedTokens = parser.Tokens;
            return result;
        }

        public static AnalysisResult Analyze(string code, System.IO.FileInfo file, string path)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            AnalysisResult result = new()
            {
                FileReferences = System.Array.Empty<string>(),
                TokenizerErrors = System.Array.Empty<Error>(),
                ParserErrors = System.Array.Empty<Error>(),
                CompilerErrors = System.Array.Empty<Error>(),
                Hints = System.Array.Empty<Hint>(),
                Informations = System.Array.Empty<Information>(),
            };

            try
            {
                Tokenizer tokenizer = new(TokenizerSettings.Default);
                return Analyze(tokenizer.Parse(code).Item1, file, path, out Token[] modifyedTokens);
            }
            catch (Exception error)
            {
                result.TokenizerFatalError = error;
                result.Warnings = System.Array.Empty<Warning>();
                result.Tokens = System.Array.Empty<Token>();
                return result;
            }
        }

        public static AnalysisResult Analyze(Token[] tokens, System.IO.FileInfo file, string path, out Token[] modifyedTokens)
        {
            if (path is null)
            { throw new System.ArgumentNullException(nameof(path)); }

            List<Error> errors = new();
            List<Warning> warnings = new();

            ParserResult? parserResult = Parse(tokens, warnings, errors, path, out Exception parserError, out modifyedTokens);

            if (errors.Count > 0 || parserError != null) return new AnalysisResult()
            {
                FileReferences = System.Array.Empty<string>(),

                CompilerErrors = System.Array.Empty<Error>(),
                Hints = System.Array.Empty<Hint>(),
                Informations = System.Array.Empty<Information>(),

                Tokens = modifyedTokens,
                TokenizerFatalError = null,
                TokenizerErrors = System.Array.Empty<Error>(),

                ParserErrors = errors.ToArray(),
                ParserFatalError = parserError,
            };
            if (!parserResult.HasValue) return new AnalysisResult()
            {
                FileReferences = System.Array.Empty<string>(),

                CompilerErrors = System.Array.Empty<Error>(),
                Hints = System.Array.Empty<Hint>(),
                Informations = System.Array.Empty<Information>(),

                Tokens = modifyedTokens,
                TokenizerFatalError = null,
                TokenizerErrors = System.Array.Empty<Error>(),

                ParserErrors = System.Array.Empty<Error>(),
                ParserFatalError = new Exception("Parse failed", new System.Exception("Parser result is null")),
            };

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
                        var usingFile = dir.FullName + "\\" + @using.PathString + "." + Core.FileExtensions.Code;
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

            AnalysisResult result = new()
            {
                FileReferences = System.Array.Empty<string>(),

                CompilerErrors = System.Array.Empty<Error>(),
                Hints = System.Array.Empty<Hint>(),
                Informations = System.Array.Empty<Information>(),

                Tokens = tokens,
                TokenizerErrors = System.Array.Empty<Error>(),
                TokenizerFatalError = null,

                ParserErrors = System.Array.Empty<Error>(),
                ParserFatalError = null,
                parserResult = parserResult_,
            };

            List<Error> compilerErrors = new();
            List<Warning> warnings = new(warnings_);
            List<Information> compilerInformations = new();
            List<Hint> compilerHints = new();

            try
            {
                result.compilerResult = Compile(parserResult_, warnings, compilerErrors, file?.Directory, compilerHints, compilerInformations);
            }
            catch (Exception error)
            {
                result.CompilerFatalError = error;
                result.CompilerErrors = compilerErrors.ToArray();
                result.Warnings = warnings.ToArray();
                result.Informations = compilerInformations.ToArray();
                result.Hints = compilerHints.ToArray();
                return result;
            }

            result.CompilerErrors = compilerErrors.ToArray();
            result.Warnings = warnings.ToArray();
            result.Informations = compilerInformations.ToArray();
            result.Hints = compilerHints.ToArray();
            return result;
        }

        public static CodeGenerator.Context GetContext(ParserResult parserResult, int position)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();

            foreach (var func in parserResult.Functions)
            {
                var id = func.ID();

                if (Functions.ContainsKey(id))
                { throw new ParserException($"Function '{id}' already exists", func.Name); }

                Functions.Add(id, func);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }

            CodeGenerator codeGenerator = new()
            { warnings = new List<Warning>() };

            return codeGenerator.GenerateCode(Functions, Structs, parserResult.Hashes, parserResult.GlobalVariables, BuiltinFunctions, BuiltinStructs, Compiler.Compiler.CompilerSettings.Default, position);
        }
    }
}
