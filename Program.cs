using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG

#if true
            var file = "test10.bbc";
            IngameCoding.Core.EasyInterpreter.Run(ArgumentParser.Parse("-throw-errors", "-c-print-instructions", "true", ProjectFolder() + "\\TestFiles\\" + file).Value);
#else
            var settings = ArgumentParser.Parse(args).Value;
            if (settings.Debug)
            {
                DebugTest.Run(settings);
            }
            else if (settings.CodeEditor)
            {
                CodeEditor.Run(settings);
            }
#endif

#else
            var settings = ArgumentParser.Parse(args);
            if (!settings.HasValue) goto ExitProgram;

            if (settings.Value.Debug)
            {
                DebugTest.Run(settings.Value);
                return;
            }
            else
            {
                IngameCoding.Core.EasyInterpreter.Run(settings.Value);
            }

        ExitProgram:
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
#endif
        }

        static string ProjectFolder()
        {
            var file = new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location);
            return file.Directory.Parent.Parent.Parent.FullName;
        }
    }
}
