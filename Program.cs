using IngameCoding.Core;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
            EasyInterpreter.Run(args);
            Console.WriteLine("\n\nPress any key to exit");
            Console.ReadKey();
        }
    }
}