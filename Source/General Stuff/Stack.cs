using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Stack<T> : IReadOnlyStack<T>, ICollection<T>
    {
        readonly List<T> stack;

        public int Count => this.stack.Count;

        public T Last
        {
            get => this.stack[^1];
            set => this.stack[^1] = value;
        }

        public bool IsReadOnly => false;

        public T this[int i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }
        public T this[System.Index i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }

        public Stack() => stack = new List<T>();

        public void Pop(int count)
        {
            for (int i = 0; i < count; i++)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }

        public virtual void Push(T item) => stack.Add(item);
        public virtual T Pop()
        {
            T val = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            return val;
        }

        public void Add(T item) => Push(item);

        public void RemoveAt(int index) => stack.RemoveAt(index);

        public void PushRange(IEnumerable<T> list)
        {
            foreach (T item in list)
            { Push(item); }
        }
        public void PushRange(List<T> list) => PushRange(list.ToArray());
        public void PushRange(T[] list)
        {
            for (int i = 0; i < list.Length; i++)
            { Push(list[i]); }
        }

        public T[] ToArray() => stack.ToArray();

        public void Clear() => stack.Clear();

        public IEnumerator<T> GetEnumerator() => stack.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => stack.GetEnumerator();

        string GetDebuggerDisplay()
        {
            if (stack is null)
            { return "null"; }

            if (stack.Count == 0)
            { return "{ }"; }

            StringBuilder result = new(3 + stack.Count * 3);

            result.Append('{');

            for (int i = 0; i < stack.Count; i++)
            {
                if (i > 0)
                { result.Append(", "); }

                if (result.Length > 30)
                {
                    result.Append("...");
                    break;
                }

                result.Append(stack[i]?.ToString() ?? "null");
            }

            result.Append('}');

            return result.ToString();
        }

        public bool Contains(T item) => stack.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => stack.CopyTo(array, arrayIndex);

        public bool Remove(T item) => stack.Remove(item);
    }

    public interface IReadOnlyStack<T> : IReadOnlyCollection<T>, IReadOnlyList<T>
    {
        public T Last { get; }

        public T[] ToArray();

        public bool Contains(T item);
    }
}
