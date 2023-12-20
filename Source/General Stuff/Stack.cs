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
        public Stack(int capacity) => stack = new List<T>(capacity);
        public Stack(IEnumerable<T> items) => stack = new List<T>(items);

        /// <exception cref="System.InvalidOperationException"/>
        public void Pop(int count)
        {
            if (stack.Count < count)
            { throw new System.InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({stack.Count})"); }

            for (int i = 0; i < count; i++)
            { stack.RemoveAt(stack.Count - 1); }
        }

        /// <exception cref="System.InvalidOperationException"/>
        public void Pop(int count, ref T[] buffer)
        {
            if (stack.Count < count)
            { throw new System.InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({stack.Count})"); }

            buffer = new T[count];

            for (int i = 0; i < count; i++)
            {
                buffer[i] = stack[^1];
                stack.RemoveAt(stack.Count - 1);
            }
        }

        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.InvalidOperationException"/>
        public void Pop(int count, T[] buffer)
        {
            if (buffer.Length < count)
            { throw new System.ArgumentOutOfRangeException(nameof(count), count, $"Count ({count}) is larger than the size of the buffer ({buffer.Length})"); }

            if (stack.Count < count)
            { throw new System.InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({stack.Count})"); }

            for (int i = 0; i < count; i++)
            {
                buffer[i] = stack[^1];
                stack.RemoveAt(stack.Count - 1);
            }
        }

        public virtual void Push(T item) => stack.Add(item);

        public virtual void PushIf<TItem>(System.Nullable<TItem> item) where TItem : struct, T
        {
            if (!item.HasValue) return;
            stack.Add(item.Value);
        }

        /// <exception cref="System.InvalidOperationException"/>
        public virtual T Pop()
        {
            if (stack.Count == 0)
            { throw new System.InvalidOperationException($"Stack is empty"); }

            T val = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            return val;
        }

        public void Add(T item) => stack.Add(item);

        /// <exception cref="System.ArgumentOutOfRangeException"/>
        public void RemoveAt(int index) => stack.RemoveAt(index);

        public void PushRange(IEnumerable<T> values) => stack.AddRange(values);

        public void Set(IEnumerable<T> newValues)
        {
            stack.Clear();
            stack.AddRange(newValues);
        }

        public T[] ToArray() => stack.ToArray();

        public void Clear() => stack.Clear();

        public IEnumerator<T> GetEnumerator() => stack.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => stack.GetEnumerator();

        internal string GetDebuggerDisplay()
        {
            if (stack is null)
            { return "null"; }

            if (stack.Count == 0)
            { return "{ }"; }

            StringBuilder result = new(3 + stack.Count * 3);

            result.Append("{ ");

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

            result.Append(" }");

            return result.ToString();
        }

        public bool Contains(T item) => stack.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => stack.CopyTo(array, arrayIndex);

        public bool Remove(T item) => stack.Remove(item);
    }

    public interface IReadOnlyStack<T> : IReadOnlyList<T>
    {
        public T Last { get; }

        T this[System.Index index] { get; }

        public T[] ToArray();

        public bool Contains(T item);

        public void CopyTo(T[] array, int arrayIndex);
    }
}
