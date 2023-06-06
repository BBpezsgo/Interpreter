using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    static class Extensions
    {
        internal static string GetTypeText(this DataItem val) => val.type.ToString();
    }
}
