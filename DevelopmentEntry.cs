#define ENABLE_DEBUG
#define RELEASE_TEST_

using System;
using System.Diagnostics;

namespace TheProgram
{
    internal static class DevelopmentEntry
    {
#if (!DEBUG || !ENABLE_DEBUG) && !RELEASE_TEST
        internal static bool Start() => false;
#else
        internal static bool Start()
        {
            string[] args = Array.Empty<string>();

#if DEBUG && ENABLE_DEBUG

            //string path = TestConstants.ExampleFilesPath + "hello-world.bbc";
            string path = TestConstants.TestFilesPath + "test33.bbc";

            if (args.Length == 0) args = new string[]
            {
                // "-throw-errors",
                "-basepath \"../CodeFiles/\"",
                // "-c-print-instructions true",
                // "-c-remove-unused-functions 5",
                // "-hide-debug",
                "-hide-system",
                //"-c-generate-comments false",
                // "-no-debug-info",
                // "-dont-optimize",
                "-debug",
                // "-test",
                // "-console-gui",
                // "-brainfuck",
                "-heap 2048",
                "-bc-instruction-limit " + int.MaxValue.ToString(),
                $"\"{path}\""
            };
#endif
#if RELEASE_TEST
            if (args.Length == 0) args = new string[]
            {
                "\"D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\helloworld.bbc\""
            };
#endif

            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) return true;

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.ConsoleGUI:
                    ConsoleGUI.ConsoleGUI gui = new()
                    {
                        FilledElement = new ConsoleGUI.InterpreterElement(path, settings.Value.compilerSettings, settings.Value.parserSettings, settings.Value.bytecodeInterpreterSettings, settings.Value.HandleErrors, settings.Value.BasePath)
                    };
                    while (!gui.Destroyed)
                    { gui.Tick(); }
                    return true;
                case ArgumentParser.RunType.Debugger:
                    _ = new Debugger(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    ProgrammingLanguage.Core.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    ProgrammingLanguage.BBCode.EasyCompiler.Result yeah = ProgrammingLanguage.BBCode.EasyCompiler.Compile(new System.IO.FileInfo(path), new System.Collections.Generic.Dictionary<string, ProgrammingLanguage.Core.ExternalFunctionBase>(), ProgrammingLanguage.BBCode.TokenizerSettings.Default, settings.Value.parserSettings, settings.Value.compilerSettings, null, settings.Value.BasePath);
                    ProgrammingLanguage.Bytecode.Instruction[] yeahCode = yeah.CodeGeneratorResult.Code;
                    System.IO.File.WriteAllBytes(settings.Value.CompileOutput, DataUtilities.Serializer.SerializerStatic.Serialize(yeahCode));
                    break;
                case ArgumentParser.RunType.Decompile:
                    throw new NotImplementedException();
                case ArgumentParser.RunType.Brainfuck:
                    Brainfuck.ProgramUtils.Run(settings.Value, Brainfuck.RunKind.Default, Brainfuck.PrintFlags.None);
                    break;
                case ArgumentParser.RunType.IL:
                    {
                        ProgrammingLanguage.BBCode.Tokenizer tokenizer = new(ProgrammingLanguage.BBCode.TokenizerSettings.Default, null); ;
                        ProgrammingLanguage.BBCode.Token[] tokens = tokenizer.Parse(System.IO.File.ReadAllText(settings.Value.File.FullName));

                        ProgrammingLanguage.BBCode.Parser.ParserResult ast = ProgrammingLanguage.BBCode.Parser.Parser.Parse(tokens);

                        ProgrammingLanguage.BBCode.Compiler.Compiler.Result compiled = ProgrammingLanguage.BBCode.Compiler.Compiler.Compile(ast, new System.Collections.Generic.Dictionary<string, ProgrammingLanguage.Core.ExternalFunctionBase>(), settings.Value.File, ProgrammingLanguage.BBCode.Parser.ParserSettings.Default, null, settings.Value.BasePath);

                        ProgrammingLanguage.IL.Compiler.CodeGenerator.Result code = ProgrammingLanguage.IL.Compiler.CodeGenerator.Generate(compiled, settings.Value.compilerSettings, default, null);

                        System.Reflection.Assembly assembly = code.Assembly;

                        break;
                    }
            }

            return true;
        }
#endif
    }
}
