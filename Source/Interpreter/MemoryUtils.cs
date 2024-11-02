using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static unsafe class MemoryUtils
{
    public static void Clear(this nint v, int size)
    {
        for (int i = 0; i < size; i++)
        {
            ((byte*)v)[i] = 0;
        }
    }
    public static nint Slice(this nint v, int start, int _) => v + start;
    public static void CopyTo<T>(this ReadOnlySpan<T> span, nint target) where T : unmanaged
    {
        int length = span.Length * sizeof(T);
        fixed (void* ptr = span)
        {
            Buffer.MemoryCopy(ptr, (void*)target, length, length);
        }
    }
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
    public static void Set<T>(this nint v, T value) where T : unmanaged => *(T*)v = value;
    public static void Set<T>(this Span<byte> memory, T data) where T : unmanaged => *GetPtr<T>(memory) = data;
    public static void Set<T>(this Span<byte> memory, int ptr, T data) where T : unmanaged => *GetPtr<T>(memory, ptr) = data;
    public static T Get<T>(this Span<byte> memory, int ptr) where T : unmanaged => *GetPtr<T>(memory, ptr);
    public static void Set(this Span<byte> memory, int ptr, ReadOnlySpan<byte> data) => data.CopyTo(memory[ptr..]);
    public static Span<byte> Get(this Span<byte> memory, int ptr, int size) => memory.Slice(ptr, size);
}
