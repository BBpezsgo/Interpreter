using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static class HeapUtils
{
    public static unsafe string? GetString(ReadOnlySpan<byte> heap, int pointer)
    {
        if (pointer <= 0 || pointer >= heap.Length)
        { return null; }
        fixed (byte* ptr = heap)
        {
            return Marshal.PtrToStringUni((nint)ptr + pointer);
        }
    }
}

[ExcludeFromCodeCoverage]
public static class BytecodeHeapImplementation
{
    const uint BlockStatusMask = 0x80000000;
    const uint BlockSizeMask = 0x7fffffff;
    public const int HeaderSize = sizeof(uint);

    public static int GetUsedSize(ReadOnlySpan<byte> heap)
    {
        int used = 0;
        int i = 0;
        while (i + HeaderSize < 127)
        {
            (int blockSize, bool blockIsUsed) = GetHeader(heap, i);
            if (blockIsUsed) used += blockSize;
            i += blockSize + HeaderSize;
        }
        return used;
    }

    public static (int Size, bool Allocated) GetHeader(ReadOnlySpan<byte> memory, int headerPointer) => (
        (memory.Get<uint>(headerPointer) & BlockSizeMask).I32(),
        (memory.Get<uint>(headerPointer) & BlockStatusMask) != 0
    );
}

[ExcludeFromCodeCoverage]
public static class BrainfuckHeapImplementation
{
    const byte BlockStatusMask = 0x80;
    const byte BlockSizeMask = 0x7f;
    public const int HeaderSize = sizeof(byte);

    public static int GetUsedSize(ReadOnlySpan<byte> heap)
    {
        int used = 0;
        int i = 0;
        while (i + HeaderSize < 127)
        {
            (int blockSize, bool blockIsUsed) = GetHeader(heap, i);
            if (blockIsUsed) used += blockSize;
            i += blockSize + HeaderSize;
        }
        return used;
    }

    public static (int Size, bool Allocated) GetHeader(ReadOnlySpan<byte> memory, int headerPointer) => (
        (memory.Get<byte>(headerPointer) & BlockSizeMask).I32(),
        (memory.Get<byte>(headerPointer) & BlockStatusMask) != 0
    );
}
