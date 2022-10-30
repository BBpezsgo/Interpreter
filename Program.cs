using IngameCoding.Core;
using System;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            EasyInterpreter.Run("-throw-errors", "D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test0.bcc");
#else
            EasyInterpreter.Run(args);
#endif
            Console.WriteLine("\n\nPress any key to exit");
            Console.ReadKey();
        }
    }
}