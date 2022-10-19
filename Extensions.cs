using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameCoding
{
    public static class StringEx
    {
        public static string Repeat(this string v, int count)
        {
            string output = "";
            for (uint i = 0; i < count; i++)
            {
                output += v;
            }
            return output;
        }
    }

    public static class ConsoleColor
    {
        public static void WriteLine(string message, System.ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
