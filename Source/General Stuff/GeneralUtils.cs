using System.IO;

namespace LanguageCore;

public interface IDuplicatable<T> : ICloneable
    where T : notnull
{
    public T Duplicate();

    object ICloneable.Clone() => Duplicate();
}

public static class Utils
{
    public static Uri AssemblyFile => new(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, Process.GetCurrentProcess().ProcessName + ".exe"), UriKind.Absolute);

    public static T Max<T>(T a, T b)
        where T : IComparisonOperators<T, T, bool>
        => a > b ? a : b;

    public static T Min<T>(T a, T b)
        where T : IComparisonOperators<T, T, bool>
        => a < b ? a : b;

    public static bool SequenceEquals<T1, T2>(ImmutableArray<T1> collectionA, ImmutableArray<T2> collectionB)
        where T1 : IEquatable<T2>
    {
        if (collectionA.Length != collectionB.Length) return false;

        for (int i = 0; i < collectionA.Length; i++)
        {
            T1 a = collectionA[i];
            T2 b = collectionB[i];

            if (!a.Equals(b)) return false;
        }

        return true;
    }

    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB)
        where T1 : IEquatable<T2>
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && e1.Current.Equals(e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB, Func<T1, T2, bool> checker)
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && checker.Invoke(e1.Current, e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    public static bool PowerOf2(int n) => n != 0 && (n & (n - 1)) == 0;
}
