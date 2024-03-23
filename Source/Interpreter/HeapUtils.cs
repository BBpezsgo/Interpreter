namespace LanguageCore.Runtime;

public static class HeapUtils
{
    const int BlockSizeMask = 0b_0000_0000_0000_0000_1111_1111_1111_1111;
    const int BlockStatusMask = 0b_0000_0000_0000_1111_0000_0000_0000_0000;
    const int JoinFreeBlocksIterations = 2;
    public const int HeaderSize = 1;

    public static void DebugPrint(IReadOnlyList<DataItem> heap)
    {
        int endlessSafe = heap.Count;
        int i = 0;
        int blockIndex = 0;
        while (i + 1 < heap.Count)
        {
            (int blockSize, bool blockIsUsed) = GetHeader(heap[i]);

            Console.Write($"BLOCK {blockIndex} ({i}): ");

            Console.Write($"SIZE: {blockSize} ");

            if (blockIsUsed)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("USED");
                Console.ResetColor();
                Console.Write(" :");
                Console.WriteLine();

                for (int j = i + 1; j < (blockSize + i + 1); j++)
                {
                    heap[j].DebugPrint();
                    Console.Write(" ");
                }
                Console.WriteLine();
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("FREE");
                Console.ResetColor();
                Console.WriteLine();
            }

            i += blockSize + 1;
            blockIndex++;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }
    }

    public static int GetUsedSize(IReadOnlyList<DataItem> heap)
    {
        int used = 0;

        int endlessSafe = heap.Count;
        int i = 0;
        int blockIndex = 0;
        while (i + 1 < heap.Count)
        {
            (int blockSize, bool blockIsUsed) = GetHeader(heap[i]);

            if (blockIsUsed)
            { used += blockSize; }

            i += blockSize + 1;
            blockIndex++;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }

        return used;
    }

    public static int GetFreeSize(IReadOnlyList<DataItem> heap)
    {
        int free = 0;

        int endlessSafe = heap.Count;
        int i = 0;
        int blockIndex = 0;
        while (i + 1 < heap.Count)
        {
            (int blockSize, bool blockIsUsed) = GetHeader(heap[i]);

            if (!blockIsUsed)
            { free += blockSize; }

            i += blockSize + 1;
            blockIndex++;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }

        return free;
    }

    public static DataItem[] GetData(IReadOnlyList<DataItem> heap, int start, int length)
    {
        DataItem[] result = new DataItem[length];
        for (int i = 0; i < length; i++)
        { result[i] = heap[start + i]; }
        return result;
    }

    public static void GetData(IReadOnlyList<DataItem> heap, int start, Span<DataItem> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        { buffer[i] = heap[start + i]; }
    }

    public static string? GetString(IReadOnlyList<DataItem> heap, int pointer)
    {
        if (pointer == 0)
        { return null; }
        StringBuilder result = new();
        for (int i = pointer; heap[i]; i++)
        { result.Append((char)heap[i]); }
        return result.ToString();
    }

    public static DataItem GetHeader(int size, bool used)
        => new((size & BlockSizeMask) | (used ? BlockStatusMask : 0));
    public static (ushort, bool) GetHeader(DataItem header)
        => ((ushort)(header.VInt & BlockSizeMask), (header.VInt & BlockStatusMask) != 0);

    public static void Init(ArraySegment<DataItem> heap)
    {
        heap[0] = GetHeader((ushort)(heap.Count - 1), false);
    }

    static void FixSize(ref int size)
    {
        if (size <= 0)
        { size = 1; }
    }

    public static int Allocate(ArraySegment<DataItem> heap, int sizeNeed)
    {
        if (sizeNeed < ushort.MinValue || sizeNeed > ushort.MaxValue)
        { throw new OverflowException(); }

        FixSize(ref sizeNeed);

        int endlessSafe = heap.Count;
        int headerPointer = 0;
        while (headerPointer < heap.Count)
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
                    if (nextHeaderPointer < heap.Count)
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

    public static void Deallocate(ArraySegment<DataItem> heap, int pointer)
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
    static bool JoinFreeBlocks(ArraySegment<DataItem> heap)
    {
        int endlessSafe = heap.Count;

        int offset = 0;
        int prevBlockSize = 0;
        while (offset < heap.Count)
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
                    heap[offset] = DataItem.Null;

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

    static void Clear(ArraySegment<DataItem> heap, int from, int length)
    {
        for (int i = from; i < from + length; i++)
        { heap[i] = DataItem.Null; }
    }
}
