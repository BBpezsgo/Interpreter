namespace LanguageCore;

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
            try
            { Entry.Run(arguments); }
            catch (Exception exception)
            { Output.LogError($"Unhandled exception: {exception}"); }

            if (arguments.DoNotPause) pauseAtEnd = false;
        }

        if (pauseAtEnd)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
