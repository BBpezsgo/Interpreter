using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            var file = new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location);
            var projectFolder = file.Directory.Parent.Parent.Parent.FullName;
            // IngameCoding.Core.EasyInterpreter.Run("-throw-errors", "-c-print-instructions", "true", "-p-print-info", "true", projectFolder + "\\TestFiles\\test1.bbc");
            IngameCoding.Core.EasyInterpreter.Run("-throw-errors", "-hide-debug", "-hide-system", projectFolder + "\\TestFiles\\test6.bbc");
#else
            IngameCoding.Core.EasyInterpreter.Run(args);
#endif
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
        }
    }
}
