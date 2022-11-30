using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG

#if false
            var file = "test9.bbc";
            IngameCoding.Core.EasyInterpreter.Run(ArgumentParser.Parse("-throw-errors", ProjectFolder() + "\\TestFiles\\" + file).Value);
#else
            DebugTest.Run(ArgumentParser.Parse(args).Value);
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
