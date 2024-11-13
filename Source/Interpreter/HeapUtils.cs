namespace LanguageCore.Runtime;

public static class HeapUtils
{
    public static int GetUsedSize(ReadOnlySpan<byte> heap)
    {
        int used = 0;

        int endlessSafe = heap.Length;
        int i = 0;
        int blockIndex = 0;
        while (i + 1 < 127)
        {
            (int blockSize, bool blockIsUsed) = HeapImplementation.GetHeader(heap[i]);

            if (blockIsUsed)
            { used += blockSize; }

            i += blockSize + 1;
            blockIndex++;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }

        return used;
    }

    public static string? GetString(ReadOnlySpan<byte> heap, int pointer)
    {
        if (pointer == 0)
        { return null; }
        StringBuilder result = new();
        for (int i = pointer; heap[i] != 0; i += sizeof(char))
        { result.Append((char)heap[i]); }
        return result.ToString();
    }
}

[ExcludeFromCodeCoverage]
public static class HeapImplementation
{
    const byte BlockSizeMask = 0b_0_1111111;
    const byte BlockStatusMask = 0b_1_0000000;
    public const int HeaderSize = sizeof(byte);

    public static (int Size, bool Allocated) GetHeader(byte header) => (
        header & BlockSizeMask,
        (header & BlockStatusMask) != 0
    );
}
