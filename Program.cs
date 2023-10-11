namespace TheProgram
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0 && DevelopmentEntry.Start()) return;

            ArgumentParser.Settings? settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) throw new System.Exception($"Invalid arguments");

            switch (settings.Value.RunType)
            {
                case ArgumentParser.RunType.ConsoleGUI:
                    System.Console.ForegroundColor = System.ConsoleColor.Red;
                    System.Console.WriteLine($"{settings.Value.RunType} mode is only available during development!");
                    System.Console.ResetColor();
                    return;
                case ArgumentParser.RunType.Debugger:
                    _ = new Debugger(settings.Value);
                    break;
                case ArgumentParser.RunType.Normal:
                    LanguageCore.Runtime.EasyInterpreter.Run(settings.Value);
                    break;
                case ArgumentParser.RunType.Compile:
                    throw new System.NotImplementedException();
                case ArgumentParser.RunType.Decompile:
                    throw new System.NotImplementedException();
                case ArgumentParser.RunType.Brainfuck:
                    Brainfuck.ProgramUtils.Run(settings.Value, Brainfuck.RunKind.Default, Brainfuck.PrintFlags.None);
                    break;
            }

            if (!settings.Value.IsTest)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }
        }
    }
}
