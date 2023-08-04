using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ProgrammingLanguage.Core
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal class Stack<T> : IReadOnlyStack<T>
    {
        readonly List<T> stack;

        public Stack() => stack = new List<T>();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public virtual T Pop()
        {
            T val = this.stack[^1];
            this.stack.RemoveAt(this.stack.Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public virtual void Push(T value) => this.stack.Add(value);
        /// <summary>Removes a specific item</summary>
        public virtual void RemoveAt(int index) => this.stack.RemoveAt(index);
        /// <returns>The number of items</returns>
        public virtual int Count => this.stack.Count;
        /// <summary>Adds a list to the end</summary>
        public virtual void PushRange(List<T> list) => PushRange(list.ToArray());
        /// <summary>Adds an array to the end</summary>
        public virtual void PushRange(T[] list)
        { foreach (T item in list) Push(item); }

        /// <returns>The last item</returns>
        public virtual T Last => this.stack[^1];

        public virtual T[] ToArray() => this.stack.ToArray();

        public virtual T this[int i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }

        public virtual T this[System.Index i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }

        public virtual void Clear() => this.stack.Clear();

        public IEnumerator<T> GetEnumerator() => this.stack.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.stack.GetEnumerator();

        private string GetDebuggerDisplay()
        {
            if (stack is null)
            { return "null"; }

            string result = "";

            for (int i = 0; i < stack.Count; i++)
            {
                if (i > 0)
                { result += ", "; }

                if (result.Length > 30)
                {
                    result += "...";
                    break;
                }

                result += stack[i].ToString();
            }

            return $"[ {result.Trim()} ]";
        }
    }

    public interface IReadOnlyStack<T> : IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        public T Last { get; }

        public T[] ToArray();
    }
}
