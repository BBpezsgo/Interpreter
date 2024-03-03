namespace LanguageCore;

public class Stack<T> : List<T>, IReadOnlyStack<T>
{
    public T Last
    {
        get => this[^1];
        set => this[^1] = value;
    }

    T IReadOnlyStack<T>.this[Index index] => base[index];

    public Stack() { }
    public Stack(int capacity) : base(capacity) { }
    public Stack(IEnumerable<T> items) : base(items) { }

    /// <exception cref="InvalidOperationException"/>
    public void Pop(int count)
    {
        if (Count < count)
        { throw new InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({Count})"); }

        for (int i = 0; i < count; i++)
        { RemoveAt(Count - 1); }
    }

    /// <exception cref="InvalidOperationException"/>
    public void Pop(int count, ref T[] buffer)
    {
        if (Count < count)
        { throw new InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({Count})"); }

        buffer = new T[count];

        for (int i = 0; i < count; i++)
        {
            buffer[i] = this[^1];
            RemoveAt(Count - 1);
        }
    }

    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    public void Pop(int count, T[] buffer)
    {
        if (buffer.Length < count)
        { throw new ArgumentOutOfRangeException(nameof(count), count, $"Count ({count}) is larger than the size of the buffer ({buffer.Length})"); }

        if (Count < count)
        { throw new InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({Count})"); }

        for (int i = 0; i < count; i++)
        {
            buffer[i] = this[^1];
            RemoveAt(Count - 1);
        }
    }

    public void Push(T item) => Add(item);

    public void PushIf<TItem>(TItem? item) where TItem : struct, T
    {
        if (!item.HasValue) return;
        Add(item.Value);
    }

    /// <exception cref="InvalidOperationException"/>
    public T Pop()
    {
        if (Count == 0)
        { throw new InvalidOperationException($"Stack is empty"); }

        T val = this[^1];
        RemoveAt(Count - 1);
        return val;
    }

    public void PushRange(IEnumerable<T> values) => AddRange(values);
}

public interface IReadOnlyStack<T> : IReadOnlyList<T>
{
    public T Last { get; }

    public T this[Index index] { get; }

    public T[] ToArray();

    public bool Contains(T item);

    public void CopyTo(T[] array, int arrayIndex);
}
