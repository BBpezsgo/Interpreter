﻿namespace TheProgram
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0 && DevelopmentEntry.Start()) return;

            if (!ArgumentParser.Parse(out ArgumentParser.Settings settings, args)) throw new System.Exception($"Invalid arguments");

            switch (settings.RunType)
            {
                case ArgumentParser.RunType.Debugger:
                    _ = new Debugger(settings);
                    break;
                case ArgumentParser.RunType.Normal:
                    if (settings.ConsoleGUI)
                    {
                        ConsoleGUI.ConsoleGUI gui = new()
                        {
                            FilledElement = new ConsoleGUI.InterpreterElement(settings.File.FullName, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.HandleErrors, settings.BasePath)
                        };
                        while (!gui.Destroyed)
                        { gui.Tick(); }
                    }
                    else
                    {
                        LanguageCore.Runtime.EasyInterpreter.Run(settings);
                    }
                    break;
                case ArgumentParser.RunType.Brainfuck:

                    Brainfuck.ProgramUtils.CompileOptions compileOptions;
                    if (settings.compilerSettings.PrintInstructions)
                    { compileOptions = Brainfuck.ProgramUtils.CompileOptions.PrintCompiledMinimized; }
                    else
                    { compileOptions = Brainfuck.ProgramUtils.CompileOptions.None; }

                    if (settings.ConsoleGUI)
                    { Brainfuck.ProgramUtils.Run(settings, Brainfuck.RunKind.UI, Brainfuck.PrintFlags.None, compileOptions); }
                    else
                    { Brainfuck.ProgramUtils.Run(settings, Brainfuck.RunKind.Default, Brainfuck.PrintFlags.None, compileOptions); }
                    break;
                default: throw new System.NotImplementedException();
            }

            if (!settings.IsTest)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }
        }
    }
}
