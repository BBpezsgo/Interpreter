﻿using System.Runtime.InteropServices;

namespace LanguageCore;

public class Stack<T> : List<T>
{
    public T Last
    {
        get => Count > 0 ? this[^1] : throw new InvalidOperationException("Stack is empty");
        set => this[^1] = value;
    }

    public T? LastOrDefault => Count > 0 ? this[^1] : default;

    public ref T LastRef
    {
        get
        {
            if (Count == 0) throw new InvalidOperationException("Stack is empty");
            return ref CollectionsMarshal.AsSpan(this)[^1];
        }
    }

    public Stack() { }
    public Stack(int capacity) : base(capacity) { }
    public Stack(IEnumerable<T> items) : base(items) { }
}

public static class StackUtils
{
    public static void Pop<T>(this List<T> list, int count)
    {
        if (list.Count < count)
        { throw new InvalidOperationException($"Count ({count}) is larger than the number of items in the stack ({list.Count})"); }

        for (int i = 0; i < count; i++)
        { list.RemoveAt(list.Count - 1); }
    }

    public static void Push<T>(this List<T> list, T item) => list.Add(item);

    public static void PushIf<T>(this List<T> list, T? item) where T : struct
    {
        if (!item.HasValue) return;
        list.Add(item.Value);
    }

    public static void PushIf<T>(this List<T> list, T? item) where T : class
    {
        if (item is null) return;
        list.Add(item);
    }

    public static T Pop<T>(this List<T> list)
    {
        if (list.Count == 0)
        { throw new InvalidOperationException("Stack is empty"); }

        T val = list[^1];
        list.RemoveAt(list.Count - 1);
        return val;
    }

    public static void PushRange<T>(this List<T> list, IEnumerable<T> values) => list.AddRange(values);
}
