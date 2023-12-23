namespace TheProgram
{
    public static class Program
    {
        public static void Main(string[] args)
        {
#if !AOT
            if (DevelopmentEntry.Start(args)) return;
#endif

            bool pauseAtEnd = true;

            if (ArgumentParser.Parse(out ProgramArguments arguments, args))
            {
                LanguageCore.Output.SetProgramArguments(arguments);

                try
                { Entry.Run(arguments); }
                catch (System.Exception exception)
                { LanguageCore.Output.LogError($"Unhandled exception: {exception}"); }

                if (arguments.DoNotPause) pauseAtEnd = false;
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
