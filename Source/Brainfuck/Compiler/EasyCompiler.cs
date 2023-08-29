using ProgrammingLanguage.BBCode;
using ProgrammingLanguage.Brainfuck.Compiler;
using ProgrammingLanguage.Core;
using ProgrammingLanguage.Errors;

using System;
using System.Collections.Generic;
using System.IO;

namespace ProgrammingLanguage.Brainfuck
{
    public class EasyCompiler
    {
        string BasePath;
        BBCode.Compiler.Compiler.CompilerSettings compilerSettings;
        CodeGenerator.Settings generatorSettings;

        public struct Result
        {
            public CodeGenerator.Result CodeGeneratorResult;
        }

        Result Compile_(
            FileInfo file,
            Action<string, Output.LogType> printCallback
            )
        {
            string sourceCode = File.ReadAllText(file.FullName);

            Token[] tokens;

            {
                BBCode.Tokenizer tokenizer = new(TokenizerSettings.Default, printCallback);
                List<Warning> warnings = new();

                tokens = tokenizer.Parse(sourceCode, warnings, file.FullName);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }
            }

            tokens = tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);

            BBCode.Parser.ParserResult parserResult;

            {
                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", Output.LogType.Debug); }

                List<Warning> warnings = new();
                BBCode.Parser.Parser parser = new();

                parserResult = parser.Parse(tokens, warnings);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }

                if (parser.Errors.Count > 0)
                { throw new Errors.Exception("Failed to parse", parser.Errors[0].ToException()); }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", Output.LogType.Debug); }
            }

            parserResult.SetFile(file.FullName);

            BBCode.Compiler.Compiler.Result compilerResult;

            {
                compilerResult = BBCode.Compiler.Compiler.Compile(
                    parserResult,
                    new Dictionary<string, ExternalFunctionBase>(),
                    file,
                    BBCode.Parser.ParserSettings.Default,
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
                    generatorSettings,
                    printCallback);

                foreach (Warning warning in codeGeneratorResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), Output.LogType.Warning); }

                if (codeGeneratorResult.Errors.Length > 0)
                { throw new Errors.Exception("Failed to compile", codeGeneratorResult.Errors[0].ToException()); }

                printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", Output.LogType.Debug);
            }

            return new Result()
            {
                CodeGeneratorResult = codeGeneratorResult,
            };
        }

        public static Result Compile(
            FileInfo file,
            BBCode.Compiler.Compiler.CompilerSettings compilerSettings,
            CodeGenerator.Settings generatorSettings,
            Action<string, Output.LogType> printCallback = null,
            string basePath = ""
            )
        {
            EasyCompiler easyCompiler = new()
            {
                BasePath = basePath,
                compilerSettings = compilerSettings,
                generatorSettings = generatorSettings,
            };
            return easyCompiler.Compile_(file, printCallback);
        }
    }
}
