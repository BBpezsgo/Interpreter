using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameCoding.Bytecode
{
    static class Extensions
    {
        internal static string[] ToStringArray(this DataItem[] items)
        {
            string[] strings = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                strings[i] = items[i].ToString();
            }
            return strings;
        }
    }
}
