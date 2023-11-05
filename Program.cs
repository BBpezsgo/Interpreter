namespace TheProgram
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (DevelopmentEntry.Start(args)) return;

            if (!ArgumentParser.Parse(out ArgumentParser.Settings settings, args))
            { return; }

            switch (settings.RunType)
            {
                case ArgumentParser.RunType.Debugger:
#if AOT
                    LanguageCore.Output.LogError($"System.Text.Json isn't avaliable in AOT mode");
#else
                    _ = new Debugger(settings);
#endif
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

            if (!settings.IsTest || settings.PauseAtEnd)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }
        }
    }
}
