using System;
using System.Collections.Generic;

namespace LanguageCore.Runtime
{
    public class DataStack : Stack<DataItem>
    {
        public DataStack() : base() { }

        public void DebugPrint()
        {
#if DEBUG
            for (int i = 0; i < Count; i++)
            {
                this[i].DebugPrint();
                Console.WriteLine();
            }
#endif
        }
    }
}
