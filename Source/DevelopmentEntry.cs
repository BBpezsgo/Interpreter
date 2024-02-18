#define ENABLE_DEBUG

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
            // string path = System.IO.Path.Combine(TestConstants.TestFilesPath, "..", "Examples", "calc.bbc");
            string path = System.IO.Path.Combine(TestConstants.TestFilesPath, "40.bbc");

            string[] generatedArgs =
            [
                // "--throw-errors",
                "--basepath \"../StandardLibrary/\"",
                // "--hide-debug",
                "--hide-system",
                // "--dont-optimize",
                // "--console-gui",
                // "--print-instructions",
                "--brainfuck",
                // "--il",
                // "--asm",
                // "--no-nullcheck",
                // "--heap-size 2048",
                "--no-pause",
                $"\"{path}\""
            ];

            string[] concatenatedArgs = new string[args.Length + generatedArgs.Length];
            args.CopyTo(concatenatedArgs, 0);
            generatedArgs.CopyTo(concatenatedArgs, args.Length);

            if (!ArgumentParser.Parse(out ProgramArguments settings, concatenatedArgs)) return true;

            try
            { Entry.Run(settings); }
            catch (System.Exception exception)
            { LanguageCore.Output.LogError($"Unhandled exception: {exception}"); }

            if (!settings.DoNotPause)
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
