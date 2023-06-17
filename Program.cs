namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (DevelopmentEntry.Start()) return;

            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) goto ExitProgram;

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.ConsoleGUI:
                    System.Console.ForegroundColor = System.ConsoleColor.Red;
                    System.Console.WriteLine($"{settings.Value.RunType} mode is only available during development!");
                    System.Console.ResetColor();
                    return;
                case ArgumentParser.RunType.Debugger:
                    // unused = new Debugger(settings.Value);
                    throw new System.NotImplementedException();
                    // return;
                case ArgumentParser.RunType.Tester:
                    IngameCoding.Tester.Tester.RunTestFile(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    IngameCoding.Core.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    // IngameCoding.CompileIntoFile.Compile(settings.Value);
                    throw new System.NotImplementedException();
                case ArgumentParser.RunType.Decompile:
                    // IngameCoding.Core.EasyInterpreter.RunCompiledFile(settings.Value);
                    throw new System.NotImplementedException();
            }

        ExitProgram:
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine("Press any key to exit");
            System.Console.ReadKey();
        }
    }
}
