using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            // IngameCoding.Core.EasyInterpreter.Run("-throw-errors", "-c-print-instructions", "true", "-p-print-info", "true", "D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test1.bbc");
            IngameCoding.Core.EasyInterpreter.Run("-throw-errors", "-hide-debug", "-hide-system", "D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test1.bbc");
#else
            IngameCoding.Core.EasyInterpreter.Run(args);
#endif
            Console.WriteLine("\n\r\n\rPress any key to exit");
            Console.ReadKey();
        }
    }
}
