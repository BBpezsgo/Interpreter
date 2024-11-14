using BytecodeHeapHeader = System.UInt32;
using BrainfuckHeapHeader = System.Byte;

namespace LanguageCore.Runtime;

public static class HeapUtils
{
    public static int GetUsedSize(ReadOnlySpan<byte> heap)
    {
        int used = 0;
        int i = 0;
        while (i + BytecodeHeapImplementation.HeaderSize < 127)
        {
            (int blockSize, bool blockIsUsed) = BytecodeHeapImplementation.GetHeader(heap, i);
            if (blockIsUsed) used += blockSize;
            i += blockSize + BytecodeHeapImplementation.HeaderSize;
        }
        return used;
    }

    public static string? GetString(ReadOnlySpan<byte> heap, int pointer)
    {
        if (pointer <= 0 || pointer > heap.Length)
        { return null; }
        StringBuilder result = new();
        for (int i = pointer; heap[i] != 0; i += sizeof(char))
        {
            if (i >= heap.Length) return null;
            result.Append((char)heap[i]);
        }
        return result.ToString();
    }
}

[ExcludeFromCodeCoverage]
public static class BytecodeHeapImplementation
{
    const BytecodeHeapHeader BlockStatusMask = 0x80000000;
    const BytecodeHeapHeader BlockSizeMask = 0x7fffffff;
    public const int HeaderSize = sizeof(BytecodeHeapHeader);

    public static (int Size, bool Allocated) GetHeader(ReadOnlySpan<byte> memory, int headerPointer) => (
        (memory.Get<BytecodeHeapHeader>(headerPointer) & BlockSizeMask).I32(),
        (memory.Get<BytecodeHeapHeader>(headerPointer) & BlockStatusMask) != 0
    );
}

[ExcludeFromCodeCoverage]
public static class BrainfuckHeapImplementation
{
    const BrainfuckHeapHeader BlockStatusMask = 0x80;
    const BrainfuckHeapHeader BlockSizeMask = 0x7f;
    public const int HeaderSize = sizeof(BrainfuckHeapHeader);

    public static (int Size, bool Allocated) GetHeader(ReadOnlySpan<byte> memory, int headerPointer) => (
        (memory.Get<BrainfuckHeapHeader>(headerPointer) & BlockSizeMask).I32(),
        (memory.Get<BrainfuckHeapHeader>(headerPointer) & BlockStatusMask) != 0
    );
}
