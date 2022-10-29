using IngameCoding.Core;

namespace TheProgram
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            EasyInterpreter.Run("-throw-errors", "D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles\\test4.bbc");
#else
            EasyInterpreter.Run(args);
#endif
            Console.WriteLine("\n\nPress any key to exit");
            Console.ReadKey();
        }
    }
}