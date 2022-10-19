using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IngameCoding
{
    internal static class Debug
    {
        internal static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        internal static void LogException(Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error.MessageAll);
        }

        internal static void LogException(System.Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error.ToString());
        }

        internal static void LogWarning(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}
