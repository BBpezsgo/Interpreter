using System.Runtime.CompilerServices;

namespace LanguageCore.Runtime;

public static class HeapUtils
{
    public static int GetUsedSize(ReadOnlySpan<RuntimeValue> heap)
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

    public static string? GetString(ReadOnlySpan<RuntimeValue> heap, int pointer)
    {
        if (pointer == 0)
        { return null; }
        StringBuilder result = new();
        for (int i = pointer; heap[i].Int != 0; i += BytecodeProcessor.RealStack ? sizeof(char) : 1)
        { result.Append(heap[i].Char); }
        return result.ToString();
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

public static class HeapImplementation
{
    const int BlockSizeMask = 0b_0000_0000_0000_0000_1111_1111_1111_1111;
    const int BlockStatusMask = 0b_0000_0000_0000_1111_0000_0000_0000_0000;
    const int JoinFreeBlocksIterations = 2;
    public const int HeaderSize = 1;

    public static RuntimeValue GetHeader(int size, bool used) => new((size & BlockSizeMask) | (used ? BlockStatusMask : 0));

    public static (ushort, bool) GetHeader(RuntimeValue header)
    {
        if (header.Int >= 127)
        {
            header = header.Int - 127;
            return ((ushort)header.Int, true);
        }
        else
        {
            return ((ushort)header.Int, false);
        }

        // ((ushort)(header.VInt & BlockSizeMask), (header.VInt & BlockStatusMask) != 0);
    }

    public static void Init(Span<RuntimeValue> heap)
    {
        heap[0] = GetHeader((ushort)(heap.Length - 1), false);
    }

    static void FixSize(ref int size)
    {
        if (size <= 0)
        { size = 1; }
    }

    public static int Allocate(Span<RuntimeValue> heap, int sizeNeed)
    {
        if (sizeNeed is < ushort.MinValue or > ushort.MaxValue)
        { throw new OverflowException(); }

        FixSize(ref sizeNeed);

        int endlessSafe = heap.Length;
        int headerPointer = 0;
        while (headerPointer < heap.Length)
        {
            (ushort blockSize, bool blockUsed) = GetHeader(heap[headerPointer]);
            int dataPointer = headerPointer + HeaderSize;

            if (!blockUsed)
            {
                // If the block's size is perfect
                if (blockSize == sizeNeed)
                {
                    // Update the current block's header
                    heap[headerPointer] = GetHeader(sizeNeed, true);

                    return dataPointer;
                }

                // If the block's size is larger than needed
                if (blockSize > sizeNeed)
                {
                    // Update the current block's header
                    heap[headerPointer] = GetHeader(sizeNeed, true);

                    int nextHeaderPointer = dataPointer + sizeNeed;

                    // If the next block is exists
                    if (nextHeaderPointer < heap.Length)
                    {
                        // Calculate remaining size
                        int _remainingSize = blockSize - sizeNeed - HeaderSize;

                        // If a larger block is allocated than needed
                        if (_remainingSize >= 0)
                        {
                            ushort remainingSize = (ushort)_remainingSize;
                            // Update the next block's header
                            heap[nextHeaderPointer] = GetHeader(remainingSize, false);
                        }
                    }

                    return dataPointer;
                }
            }

            headerPointer += blockSize + HeaderSize;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }

        throw new RuntimeException($"HEAP error: Failed to find free space (size {sizeNeed})");
    }

    public static void Deallocate(Span<RuntimeValue> heap, int pointer)
    {
        int headerPointer = pointer - HeaderSize;

        (ushort blockSize, _) = GetHeader(heap[headerPointer]);
        heap[headerPointer] = GetHeader(blockSize, false);

        Clear(heap, pointer, blockSize);

        for (int i = 0; i < JoinFreeBlocksIterations; i++)
        {
            bool joined = JoinFreeBlocks(heap);
            if (!joined) break;
        }
    }

    /// <returns>
    /// <see langword="true"/> if any block has been joined, <see langword="false"/> otherwise
    /// </returns>
    /// <exception cref="EndlessLoopException"/>
    static bool JoinFreeBlocks(Span<RuntimeValue> heap)
    {
        int endlessSafe = heap.Length;

        int offset = 0;
        int prevBlockSize = 0;
        while (offset < heap.Length)
        {
            (int blockSize, bool blockUsed) = GetHeader(heap[offset]);
            int prevOffset = offset - prevBlockSize - HeaderSize;

            // This is a free block that is not at the beginning of the heap
            if (offset != 0 && !blockUsed)
            {
                (_, bool prevBlockUsed) = GetHeader(heap[prevOffset]);

                // The previous block is also free
                if (!prevBlockUsed)
                {
                    // Update the previous block's header
                    heap[prevOffset] = GetHeader((ushort)((ushort)blockSize + (ushort)prevBlockSize + (ushort)HeaderSize), false);
                    // Remove the current block's header
                    heap[offset] = default;

                    return true;
                }
            }

            prevBlockSize = blockSize;

            // Jump to the next block's header offset
            offset += blockSize + HeaderSize;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }
        return false;
    }

    static void Clear(Span<RuntimeValue> heap, int from, int length)
    {
        for (int i = from; i < from + length; i++)
        { heap[i] = default; }
    }
}
