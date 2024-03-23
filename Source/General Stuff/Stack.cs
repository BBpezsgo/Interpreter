namespace LanguageCore;

public class Stack<T> : List<T>
{
    public T Last
    {
        /// <exception cref="InvalidOperationException"/>
        get => Count > 0 ? this[^1] : throw new InvalidOperationException("Stack is empty");
        set => this[^1] = value;
    }

    public Stack() { }
    public Stack(int capacity) : base(capacity) { }
    public Stack(IEnumerable<T> items) : base(items) { }
}

public static class StackUtils
{
    /// <exception cref="InvalidOperationException"/>
    public static T Last<T>(this IList<T> list)
    {
        if (list.Count == 0)
        { throw new InvalidOperationException("Stack is empty"); }
        return list[^1];
    }

    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="NotSupportedException"/>
    public static void Pop<T>(this IList<T> list, int count)
    {
        if (list.Count < count)
        { throw new InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({list.Count})"); }

        for (int i = 0; i < count; i++)
        { list.RemoveAt(list.Count - 1); }
    }

    /// <exception cref="NotSupportedException"/>
    public static void Push<T>(this ICollection<T> list, T item) => list.Add(item);

    /// <exception cref="NotSupportedException"/>
    public static void PushIf<T>(this ICollection<T> list, T? item) where T : struct
    {
        if (!item.HasValue) return;
        list.Add(item.Value);
    }

    /// <exception cref="NotSupportedException"/>
    public static void PushIf<T>(this ICollection<T> list, T? item) where T : class
    {
        if (item is null) return;
        list.Add(item);
    }

    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="NotSupportedException"/>
    public static T Pop<T>(this IList<T> list)
    {
        if (list.Count == 0)
        { throw new InvalidOperationException("Stack is empty"); }

        T val = list[^1];
        list.RemoveAt(list.Count - 1);
        return val;
    }

    public static void PushRange<T>(this List<T> list, IEnumerable<T> values) => list.AddRange(values);
}
