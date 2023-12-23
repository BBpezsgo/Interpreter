global using EasyBrainfuckCompilerResult = (LanguageCore.Brainfuck.Generator.BrainfuckGeneratorResult GeneratorResult, LanguageCore.Tokenizing.Token[] Tokens);
using System;
using System.IO;

namespace LanguageCore.Brainfuck
{
    using Compiler;
    using Generator;
    using LanguageCore.Tokenizing;
    using Parser;

    [Flags]
    public enum EasyBrainfuckCompilerFlags : int
    {
        None = 0b_0000,

        PrintCompiled = 0b_0001,
        PrintCompiledMinimized = 0b_0010,
        WriteToFile = 0b_0100,

        PrintFinal = 0b_1000,
    }

    public static class EasyBrainfuckCompiler
    {
        public static EasyBrainfuckCompilerResult? Compile(FileInfo file, EasyBrainfuckCompilerFlags options, PrintCallback printCallback)
        {
            bool throwErrors = true;

            BrainfuckGeneratorResult generated;
            Token[] tokens;

            try
            {
                tokens = StreamTokenizer.Tokenize(file.FullName);
                ParserResult ast = Parser.Parse(tokens);
                CompilerResult compiled = Compiler.Compile(ast, null, file, null, printCallback);
                generated = CodeGeneratorForBrainfuck.Generate(compiled, BrainfuckGeneratorSettings.Default, printCallback);
                // printCallback?.Invoke($"Optimized {compilerResult.Optimizations} statements", LogType.Debug);
            }
            catch (LanguageException exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                string? arrows = exception.GetArrows();
                if (arrows != null)
                { Console.WriteLine(arrows); }

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception.ToString());

                Console.ResetColor();

                if (throwErrors) throw;
                else return null;
            }

            if (((int)options & (int)EasyBrainfuckCompilerFlags.PrintCompiled) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === COMPILED ===");
                BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            generated.Code = Minifier.Minify(generated.Code);

            if (((int)options & (int)EasyBrainfuckCompilerFlags.PrintCompiledMinimized) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === MINIFIED ===");
                BrainfuckCode.PrintCode(Simplifier.Simplify(generated.Code));
                Console.WriteLine();
                Console.ResetColor();
            }

            generated.Code = Minifier.Minify(BrainfuckCode.RemoveNoncodes(generated.Code));

            if (((int)options & (int)EasyBrainfuckCompilerFlags.PrintFinal) != 0)
            {
                Console.WriteLine();
                Console.WriteLine($" === FINAL ===");
                BrainfuckCode.PrintCode(generated.Code);
                Console.WriteLine();
                Console.ResetColor();
            }

            if (((int)options & (int)EasyBrainfuckCompilerFlags.WriteToFile) != 0)
            {
                string compiledFilePath = Path.Combine(Path.GetDirectoryName(file.FullName) ?? throw new InternalException($"Failed to get directory name of file \"{file.FullName}\""), Path.GetFileNameWithoutExtension(file.FullName) + ".bf");
                File.WriteAllText(compiledFilePath, generated.Code);
            }

            return (generated, tokens);
        }
    }
}
