using System;
using System.Collections.Generic;
using System.IO;

namespace LanguageCore.BBCode
{
    using Compiler;
    using Parser;
    using LanguageCore.Tokenizing;
    using LanguageCore.Runtime;

    public class EasyCompiler
    {
        Dictionary<string, ExternalFunctionBase>? externalFunctions;
        string? BasePath;
        TokenizerSettings tokenizerSettings;
        ParserSettings parserSettings;
        Compiler.Compiler.CompilerSettings compilerSettings;

        public struct Result
        {
            public CodeGenerator.Result CodeGeneratorResult;
            public Compiler.Compiler.Result CompilerResult;
            public ParserResult ParserResult;
            public Token[] TokenizerResult;
        }

        Result Compile_(
            FileInfo file,
            PrintCallback? printCallback
            )
        {
            string sourceCode = File.ReadAllText(file.FullName);

            Token[] tokens;

            {
                Tokenizer tokenizer = new(tokenizerSettings, printCallback);
                List<Warning> warnings = new();

                tokens = tokenizer.Parse(sourceCode, warnings, file.FullName);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }
            }

            ParserResult parserResult;

            {
                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", LogType.Debug); }

                parserResult = Parser.Parse(tokens);

                if (parserResult.Errors.Length > 0)
                { throw new LanguageException("Failed to parse", parserResult.Errors[0].ToException()); }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", LogType.Debug); }

                if (parserSettings.PrintInfo)
                { parserResult.WriteToConsole(); }
            }

            parserResult.SetFile(file.FullName);

            Compiler.Compiler.Result compilerResult;

            {
                compilerResult = Compiler.Compiler.Compile(
                    parserResult,
                    this.externalFunctions ?? new Dictionary<string, ExternalFunctionBase>(),
                    file,
                    ParserSettings.Default,
                    printCallback,
                    this.BasePath);

                foreach (Warning warning in compilerResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }

                if (compilerResult.Errors.Length > 0)
                { throw new LanguageException("Failed to compile", compilerResult.Errors[0].ToException()); }
            }

            CodeGenerator.Result codeGeneratorResult;

            {
                DateTime codeGenerationStarted = DateTime.Now;
                printCallback?.Invoke("Generating code ...", LogType.Debug);

                codeGeneratorResult = CodeGenerator.Generate(
                    compilerResult,
                    compilerSettings,
                    printCallback,
                    Compiler.Compiler.CompileLevel.Minimal);

                foreach (Warning warning in codeGeneratorResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }

                foreach (var info in codeGeneratorResult.Informations)
                { printCallback?.Invoke(info.ToString(), LogType.Normal); }

                foreach (var hint in codeGeneratorResult.Hints)
                { printCallback?.Invoke(hint.ToString(), LogType.Normal); }

                if (codeGeneratorResult.Errors.Length > 0)
                { throw new LanguageException("Failed to compile", codeGeneratorResult.Errors[0].ToException()); }

                printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", LogType.Debug);
            }

            return new Result()
            {
                TokenizerResult = tokens,
                ParserResult = parserResult,
                CompilerResult = compilerResult,
                CodeGeneratorResult = codeGeneratorResult,
            };
        }

        /// <exception cref="CompilerException"></exception>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="LanguageException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static CodeGenerator.Result? Compile(
            FileInfo file,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            TokenizerSettings tokenizerSettings,
            ParserSettings parserSettings,
            Compiler.Compiler.CompilerSettings compilerSettings,
            bool handleErrors,
            PrintCallback? printCallback = null,
            string? basePath = null)
        {
            try
            {
                CodeGenerator.Result codeGeneratorResult = EasyCompiler.Compile(
                    file,
                    externalFunctions,
                    tokenizerSettings,
                    parserSettings,
                    compilerSettings,
                    printCallback,
                    basePath
                    ).CodeGeneratorResult;

                if (compilerSettings.PrintInstructions)
                { codeGeneratorResult.PrintInstructions(); }

                return codeGeneratorResult;
            }
            catch (LanguageException error)
            {
                printCallback?.Invoke(error.ToString(), LogType.Error);
                Debug.LogError(error);

                if (!handleErrors) throw;
            }
            catch (Exception error)
            {
                printCallback?.Invoke(error.ToString(), LogType.Error);
                Debug.LogError(error);

                if (!handleErrors) throw;
            }

            return null;
        }

        public static Result Compile(
            FileInfo file,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            TokenizerSettings tokenizerSettings,
            ParserSettings parserSettings,
            Compiler.Compiler.CompilerSettings compilerSettings,
            PrintCallback? printCallback = null,
            string? basePath = null
            ) => new EasyCompiler()
        {
            externalFunctions = externalFunctions,
            BasePath = basePath,
            tokenizerSettings = tokenizerSettings,
            parserSettings = parserSettings,
            compilerSettings = compilerSettings,
        }.Compile_(file, printCallback);
    }
}
