using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static unsafe class MemoryUtils
{
    #region nint

    public static void Clear(this nint memory, int size)
    {
        for (int i = 0; i < size; i++)
        {
            ((byte*)memory)[i] = 0;
        }
    }

    public static nint Slice(this nint memory, int start, int _) => memory + start;

    public static void Set<T>(this nint memory, T value) where T : unmanaged => *(T*)memory = value;
    public static void Set<T>(this nint memory, int ptr, T data) where T : unmanaged => *(T*)(memory + ptr) = data;
    public static void Set(this nint memory, int ptr, ReadOnlySpan<byte> data) => data.CopyTo(memory + ptr);

    public static T To<T>(this nint memory) where T : unmanaged => *(T*)memory;
    public static T Get<T>(this nint memory, int ptr) where T : unmanaged => *(T*)(memory + ptr);

    #endregion

    #region ReadOnlySpan

    public static void CopyTo<T>(this ReadOnlySpan<T> span, nint target) where T : unmanaged
    {
        int length = span.Length * sizeof(T);
        fixed (void* ptr = span)
        {
            Buffer.MemoryCopy(ptr, (void*)target, length, length);
        }
    }
    public static T* GetPtr<T>(this ReadOnlySpan<byte> memory, int ptr) where T : unmanaged => ptr >= 0 && ptr < memory.Length ? (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory[ptr..])) : throw new RuntimeException($"Memory access violation (pointer {ptr} was out of range)");
    public static T* GetPtr<T>(this ReadOnlySpan<byte> memory) where T : unmanaged => (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory));

    public static T To<T>(this ReadOnlySpan<byte> memory) where T : unmanaged { fixed (byte* ptr = memory) return *(T*)ptr; }
    public static T Get<T>(this ReadOnlySpan<byte> memory, int ptr) where T : unmanaged => *GetPtr<T>(memory, ptr);
    public static ReadOnlySpan<byte> Get(this ReadOnlySpan<byte> memory, int ptr, int size) => memory.Slice(ptr, size);

    #endregion

    #region Span

    public static void CopyTo<T>(this Span<T> span, nint target) where T : unmanaged
    {
        int length = span.Length * sizeof(T);
        fixed (void* ptr = span)
        {
            Buffer.MemoryCopy(ptr, (void*)target, length, length);
        }
    }
    public static T* GetPtr<T>(this Span<byte> memory, int ptr) where T : unmanaged => ptr >= 0 && ptr < memory.Length ? (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory[ptr..])) : throw new RuntimeException($"Memory access violation (pointer {ptr} was out of range)");
    public static T* GetPtr<T>(this Span<byte> memory) where T : unmanaged => (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory));
    public static void Set<T>(this Span<byte> memory, T data) where T : unmanaged => *GetPtr<T>(memory) = data;
    public static void Set<T>(this Span<byte> memory, int ptr, T data) where T : unmanaged => *GetPtr<T>(memory, ptr) = data;
    public static void Set(this Span<byte> memory, int ptr, ReadOnlySpan<byte> data) => data.CopyTo(memory[ptr..]);

    public static T To<T>(this Span<byte> memory) where T : unmanaged { fixed (byte* ptr = memory) return *(T*)ptr; }
    public static T Get<T>(this Span<byte> memory, int ptr) where T : unmanaged => *GetPtr<T>(memory, ptr);
    public static Span<byte> Get(this Span<byte> memory, int ptr, int size) => memory.Slice(ptr, size);

    #endregion

    #region ImmutableArray

    public static T To<T>(this ImmutableArray<byte> memory) where T : unmanaged { fixed (byte* ptr = memory.AsSpan()) return *(T*)ptr; }

    #endregion

    #region byte[]

    public static T To<T>(this byte[] memory) where T : unmanaged { fixed (byte* ptr = memory) return *(T*)ptr; }

    #endregion

    public static byte[] ToBytes<T>(this T v) where T : unmanaged => AsBytes(ref v).ToArray();
    public static Span<byte> AsBytes<T>(ref this T v) where T : unmanaged => new(Unsafe.AsPointer(ref v), sizeof(T));
}
