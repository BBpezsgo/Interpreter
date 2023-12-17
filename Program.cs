namespace TheProgram
{
    public static class Program
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

                if (settings.IsTest) pauseAtEnd = false;
                if (settings.DoNotPause) pauseAtEnd = false;
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
