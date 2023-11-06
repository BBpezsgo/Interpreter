#define ENABLE_DEBUG

using LanguageCore.Tokenizing;

namespace TheProgram
{
    public static class DevelopmentEntry
    {
#if !DEBUG || !ENABLE_DEBUG
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060")]
        public static bool Start(string[] args) => false;
#else
        [System.Diagnostics.CodeAnalysis.RequiresAssemblyFiles]
        public static bool Start(string[] args)
        {
            string path = System.IO.Path.Combine(TestConstants.TestFilesPath, "test11.bbc");

            string[] generatedArgs = new string[]
            {
                // "--throw-errors",
                "--basepath \"../CodeFiles/\"",
                // "--hide-debug",
                "--hide-system",
                // "--dont-optimize",
                "--console-gui",
                "--brainfuck",
                // "--no-nullcheck",
                "--heap-size 2048",
                $"\"{path}\""
            };

            string[] concatenatedArgs = new string[args.Length + generatedArgs.Length];
            args.CopyTo(concatenatedArgs, 0);
            generatedArgs.CopyTo(concatenatedArgs, args.Length);

            if (!ArgumentParser.Parse(out ArgumentParser.Settings settings, concatenatedArgs)) return true;

            try
            { Entry.Run(settings); }
            catch (System.Exception exception)
            { LanguageCore.Output.LogError($"Unhandled exception: {exception}"); }

            if (settings.PauseAtEnd)
            {
                System.Console.WriteLine();
                System.Console.WriteLine();
                System.Console.WriteLine("Press any key to exit");
                System.Console.ReadKey();
            }

            return true;
        }
#endif
    }
}
