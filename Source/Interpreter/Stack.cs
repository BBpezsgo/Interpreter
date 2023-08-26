using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.Bytecode
{
    using Core;

    internal class DataStack : Stack<DataItem>
    {
        internal int UsedVirtualMemory
        {
            get
            {
                static int CalculateItemSize(DataItem item) => item.Type switch
                {
                    RuntimeType.INT => 4,
                    RuntimeType.FLOAT => 4,
                    _ => throw new NotImplementedException(),
                };

                int result = 0;
                for (int i = 0; i < Count; i++)
                { result += CalculateItemSize(this[i]); }
                return result;
            }
        }

        public DataStack() : base() { }

        public void Destroy() => base.Clear();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public override DataItem Pop()
        {
            DataItem val = this[^1];
            RemoveAt(Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public override void Push(DataItem value)
        {
            var item = value;
            base.Push(item);
        }
        /// <summary>Adds a list to the end</summary>
        public override void PushRange(List<DataItem> list) => PushRange(list.ToArray());
        /// <summary>Adds an array to the end</summary>
        public override void PushRange(DataItem[] list)
        { foreach (DataItem item in list) Push(item); }
        /// <summary>Adds a list to the end</summary>
        public void PushRange(DataItem[] list, string tag)
        {
            DataItem[] newList = new DataItem[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                DataItem item = list[i];
                item.Tag = tag ?? item.Tag;
                newList[i] = item;
            }
            PushRange(newList);
        }
        /// <summary>Sets a specific item's value</summary>
        public void Set(int index, DataItem val, bool overrideTag = false)
        {
            DataItem item = val;
            if (!overrideTag)
            {
                item.Tag = this[index].Tag;
            }
            this[index] = item;
        }

        public void DebugPrint()
        {
#if DEBUG
            for (int i = 0; i < Count; i++)
            {
                this[i].DebugPrint();
                if (this[i].Tag != null)
                {
                    Console.Write(' ');
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(this[i].Tag);
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
#endif
        }
    }
}
