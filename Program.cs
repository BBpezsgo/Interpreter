using IngameCoding.Core;

namespace TheProgram
{
    internal class Program
    {
        static readonly string DebugFile = "D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test2.bbc";

        static void Main(string[] args)
        {
            EasyInterpreter.Run(DebugFile);
            Console.WriteLine("\n\nPress any key to exit");
            Console.ReadKey();
        }
    }
}