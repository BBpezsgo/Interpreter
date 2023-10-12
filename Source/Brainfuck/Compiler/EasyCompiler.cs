using System;
using System.Collections.Generic;
using System.IO;

namespace LanguageCore.Brainfuck
{
    using Compiler;
    using LanguageCore.Runtime;

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
            PrintCallback printCallback
            )
        {
            string sourceCode = File.ReadAllText(file.FullName);

            Tokenizing.Token[] tokens;

            {
                Tokenizing.Tokenizer tokenizer = new(Tokenizing.TokenizerSettings.Default, printCallback);
                List<Warning> warnings = new();

                tokens = tokenizer.Parse(sourceCode, warnings, file.FullName);

                foreach (Warning warning in warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }
            }

            Parser.ParserResult parserResult;

            {
                DateTime parseStarted = DateTime.Now;
                if (printCallback != null)
                { printCallback?.Invoke("Parsing ...", LogType.Debug); }

                parserResult = Parser.Parser.Parse(tokens);

                if (parserResult.Errors.Length > 0)
                { throw new LanguageException("Failed to parse", parserResult.Errors[0].ToException()); }

                if (printCallback != null)
                { printCallback?.Invoke($"Parsed in {(DateTime.Now - parseStarted).TotalMilliseconds} ms", LogType.Debug); }
            }

            parserResult.SetFile(file.FullName);

            BBCode.Compiler.Compiler.Result compilerResult;

            {
                compilerResult = BBCode.Compiler.Compiler.Compile(
                    parserResult,
                    new Dictionary<string, ExternalFunctionBase>(),
                    file,
                    Parser.ParserSettings.Default,
                    printCallback,
                    this.BasePath ?? "");

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
                    generatorSettings,
                    printCallback);

                foreach (Warning warning in codeGeneratorResult.Warnings)
                { printCallback?.Invoke(warning.ToString(), LogType.Warning); }

                if (codeGeneratorResult.Errors.Length > 0)
                { throw new LanguageException("Failed to compile", codeGeneratorResult.Errors[0].ToException()); }

                printCallback?.Invoke($"Code generated in {(DateTime.Now - codeGenerationStarted).TotalMilliseconds} ms", LogType.Debug);
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
            PrintCallback printCallback = null,
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
