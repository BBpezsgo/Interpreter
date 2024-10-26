using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageCore;

public interface IDuplicatable<T> where T : notnull
{
    public T Duplicate();
}

public static class Utils
{
#if !NET_STANDARD
    public static Uri AssemblyFile => new(Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, Process.GetCurrentProcess().ProcessName + ".exe"), UriKind.Absolute);

    /// <exception cref="ArgumentOutOfRangeException"/>
    public static unsafe void Write<T>(this Span<byte> buffer, in T value) where T : unmanaged => MemoryMarshal.Write(buffer, in value);
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static unsafe T Read<T>(this Span<byte> buffer) where T : unmanaged => MemoryMarshal.Read<T>(buffer);
#endif

    public static unsafe ReadOnlySpan<byte> ToBytes<T>(this T v) where T : unmanaged => new(&v, sizeof(T));
    public static unsafe ReadOnlySpan<byte> AsBytes<T>(ref this T v) where T : unmanaged => new(Unsafe.AsPointer(ref v), sizeof(T));
    public static unsafe T To<T>(this nint v) where T : unmanaged => *(T*)v;
    public static unsafe T To<T>(this Span<byte> v) where T : unmanaged { fixed (byte* ptr = v) return *(T*)ptr; }
    public static unsafe T To<T>(this ReadOnlySpan<byte> v) where T : unmanaged { fixed (byte* ptr = v) return *(T*)ptr; }
    public static unsafe T To<T>(this ImmutableArray<byte> v) where T : unmanaged { fixed (byte* ptr = v.AsSpan()) return *(T*)ptr; }

    /// <summary>
    /// Source: <see href="https://stackoverflow.com/questions/3855956/check-if-an-executable-exists-in-the-windows-path"/>
    /// </summary>
    public static bool GetFullPath(string fileName, [NotNullWhen(true)] out string? fullPath) => (fullPath = GetFullPath(fileName)) is not null;
    /// <summary>
    /// Source: <see href="https://stackoverflow.com/questions/3855956/check-if-an-executable-exists-in-the-windows-path"/>
    /// </summary>
    public static string? GetFullPath(string fileName)
    {
        if (File.Exists(fileName))
        { return Path.GetFullPath(fileName); }

        string? values = Environment.GetEnvironmentVariable("PATH");
        if (values is null) return null;

        foreach (string path in values.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            { return fullPath; }
        }

        return null;
    }

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

    public static bool SequenceEquals<T1, T2>(IEnumerable<T1> collectionA, IEnumerable<T2> collectionB, Func<int, T1, T2, bool> checker)
    {
        using IEnumerator<T1> e1 = collectionA.GetEnumerator();
        using IEnumerator<T2> e2 = collectionB.GetEnumerator();

        int i = 0;

        while (e1.MoveNext())
        {
            if (!(e2.MoveNext() && checker.Invoke(i++, e1.Current, e2.Current)))
            { return false; }
        }

        return !e2.MoveNext();
    }

    public static bool PowerOf2(int n) => n != 0 && (n & (n - 1)) == 0;

    public static Uri ToFileUri(string path) => new UriBuilder()
    {
        Host = null,
        Scheme = Uri.UriSchemeFile,
        Path = path,
    }.Uri;
}
