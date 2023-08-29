using ProgrammingLanguage.BBCode.Compiler;
using ProgrammingLanguage.BBCode.Parser;
using ProgrammingLanguage.Core;
using ProgrammingLanguage.Errors;

using System;
using System.Collections.Generic;
using System.IO;

namespace ProgrammingLanguage.BBCode
{
    public class EasyCompiler
    {
        Dictionary<string, ExternalFunctionBase> externalFunctions;
        string BasePath;
        TokenizerSettings tokenizerSettings;
        Parser.ParserSettings parserSettings;
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
            Action<string, Output.LogType> printCallback
            )
        {
            string sourceCode = File.ReadAllText(file.FullName);

            Token[] tokens;

            {
                Tokenizer tokenizer = new(tokenizerSettings, printCallback);
                List<Warning> warnings = new();

                tokens = tokenizer.Parse(sourceCode, warnings, file.FullName);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }
            }

            tokens = tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);

            Parser.ParserResult parserResult;

            {
                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", Output.LogType.Debug); }

                List<Warning> warnings = new();
                Parser.Parser parser = new();

                parserResult = parser.Parse(tokens, warnings);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }

                if (parser.Errors.Count > 0)
                { throw new Errors.Exception("Failed to parse", parser.Errors[0].ToException()); }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", Output.LogType.Debug); }

                if (parserSettings.PrintInfo)
                { parserResult.WriteToConsole(); }
            }

            parserResult.SetFile(file.FullName);

            Compiler.Compiler.Result compilerResult;

            {
                compilerResult = Compiler.Compiler.Compile(
                    parserResult,
                    this.externalFunctions,
                    file,
                    Parser.ParserSettings.Default,
                    printCallback,
                    this.BasePath ?? "");

                foreach (Warning warning in compilerResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }

                if (compilerResult.Errors.Length > 0)
                { throw new Errors.Exception("Failed to compile", compilerResult.Errors[0].ToException()); }
            }

            CodeGenerator.Result codeGeneratorResult;

            {
                DateTime codeGenerationStarted = DateTime.Now;
                printCallback?.Invoke("Generating code ...", Output.LogType.Debug);

                codeGeneratorResult = CodeGenerator.Generate(
                    compilerResult,
                    compilerSettings,
                    printCallback,
                    Compiler.Compiler.CompileLevel.Minimal);

                foreach (Warning warning in codeGeneratorResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }

                foreach (var info in codeGeneratorResult.Informations)
                { printCallback?.Invoke(info.ToString(), Output.LogType.Normal); }

                foreach (var hint in codeGeneratorResult.Hints)
                { printCallback?.Invoke(hint.ToString(), Output.LogType.Normal); }

                if (codeGeneratorResult.Errors.Length > 0)
                { throw new Errors.Exception("Failed to compile", codeGeneratorResult.Errors[0].ToException()); }

                printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", Output.LogType.Debug);
            }

            return new Result()
            {
                TokenizerResult = tokens,
                ParserResult = parserResult,
                CompilerResult = compilerResult,
                CodeGeneratorResult = codeGeneratorResult,
            };
        }

        public static Result Compile(
            FileInfo file,
            Dictionary<string, ExternalFunctionBase> externalFunctions,
            TokenizerSettings tokenizerSettings,
            Parser.ParserSettings parserSettings,
            Compiler.Compiler.CompilerSettings compilerSettings,
            Action<string, Output.LogType> printCallback = null,
            string basePath = ""
            )
        {
            EasyCompiler easyCompiler = new()
            {
                externalFunctions = externalFunctions,
                BasePath = basePath,
                tokenizerSettings = tokenizerSettings,
                parserSettings = parserSettings,
                compilerSettings = compilerSettings,
            };
            return easyCompiler.Compile_(file, printCallback);
        }
    }
}
