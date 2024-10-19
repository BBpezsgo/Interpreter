using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static class MemoryUtils
{
    public static unsafe T* GetPtr<T>(this Span<byte> memory, int ptr) where T : unmanaged => ptr >= 0 && ptr < memory.Length ? (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory[ptr..])) : throw new RuntimeException($"Memory access violation (pointer {ptr} was out of range)");

    public static unsafe T Get<T>(this byte[] memory, int ptr) where T : unmanaged => *GetPtr<T>(memory, ptr);

    public static unsafe RuntimeValue GetData(this byte[] memory, int address, BitWidth size) => size switch
    {
        BitWidth._8 => (RuntimeValue)memory.Get<byte>(address),
        BitWidth._16 => (RuntimeValue)memory.Get<ushort>(address),
        BitWidth._32 => (RuntimeValue)memory.Get<int>(address),
        _ => throw new UnreachableException(),
    };

    public static unsafe void Set<T>(this Span<byte> memory, int ptr, T data) where T : unmanaged => *GetPtr<T>(memory, ptr) = data;
    public static unsafe T Get<T>(this Span<byte> memory, int ptr) where T : unmanaged => *GetPtr<T>(memory, ptr);

    public static unsafe void Set(this Span<byte> memory, int ptr, ReadOnlySpan<byte> data) => data.CopyTo(memory[ptr..]);
    public static unsafe Span<byte> Get(this Span<byte> memory, int ptr, int size) => memory.Slice(ptr, size);

    public static unsafe void SetData(this Span<byte> memory, int address, RuntimeValue data, BitWidth size)
    {
        switch (size)
        {
            case BitWidth._8:
                memory.Set(address, data.U8);
                break;
            case BitWidth._16:
                memory.Set(address, data.U16);
                break;
            case BitWidth._32:
                memory.Set(address, data.U32);
                break;
            default:
                throw new UnreachableException();
        }
    }
    public static unsafe RuntimeValue GetData(this Span<byte> memory, int address, BitWidth size) => size switch
    {
        BitWidth._8 => (RuntimeValue)memory.Get<byte>(address),
        BitWidth._16 => (RuntimeValue)memory.Get<ushort>(address),
        BitWidth._32 => (RuntimeValue)memory.Get<int>(address),
        _ => throw new UnreachableException(),
    };
}
