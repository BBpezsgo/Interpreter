﻿namespace TheProgram
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (DevelopmentEntry.Start(args)) return;

            bool pauseAtEnd = true;

            if (ArgumentParser.Parse(out ArgumentParser.Settings settings, args))
            {
                try
                { Entry.Run(settings); }
                catch (System.Exception exception)
                { LanguageCore.Output.LogError($"Unhandled exception: {exception}"); }

                pauseAtEnd = !settings.IsTest || settings.PauseAtEnd;
            }

            if (pauseAtEnd)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }
        }
    }
}
