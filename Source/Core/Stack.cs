using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.Core
{
    internal class Stack<T>
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
        public virtual T Last() => this.stack.Last();

        public virtual T[] ToArray() => this.stack.ToArray();

        public virtual T this[int i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }

        public virtual void Clear() => this.stack.Clear();
    }
}
