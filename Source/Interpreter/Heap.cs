using System;
using System.Linq;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Errors;

    using System.Collections.Generic;

    internal class HEAP : IHeap
    {
        readonly DataItem[] heap;

        internal HEAP(int size = 0)
        {
            ushort _size = (ushort)size;
            FixSize(ref _size);

            this.heap = new DataItem[_size];

            this.heap[0] = GetHeader((ushort)(_size - 1), false);
        }

        public int Size => this.heap.Length;
        public DataItem this[int i]
        {
            get
            {
                if (i < 0) throw new RuntimeException($"Null pointer!");
                if (i >= heap.Length) throw new RuntimeException($"Pointer points ouf of memory bounds. Possibly out of HEAP memory.");
                return heap[i];
            }
            set
            {
                if (i < 0) throw new RuntimeException($"Null pointer!");
                if (i >= heap.Length) return; // throw new RuntimeException($"Pointer points ouf of memory bounds. Possibly out of HEAP memory.");
                heap[i] = value;
            }
        }

        public DataItem[] ToArray() => heap.ToList().ToArray();

        internal string GetString(int start, int length)
        {
            int end = start + length;
            string result = "";
            for (int i = start; i < end; i++)
            {
                if (this[i].type != RuntimeType.CHAR)
                {
                    throw new InternalException($"Unexpected data type {this[i].type}, expected {nameof(RuntimeType.CHAR)}");
                }
                result += this[i].ValueChar;
            }
            return result;
        }
        internal string GetStringByPointer(int pointer)
        {
            int subpointer = this[pointer].ValueInt;
            int length = this[pointer + 1].ValueInt;
            return GetString(subpointer, length);
        }

        const int BLOCK_SIZE_MASK = 0b_0000_0000_0000_0000_1111_1111_1111_1111;
        const int BLOCK_STAT_MASK = 0b_0000_0000_0000_1111_0000_0000_0000_0000;

        public static DataItem GetHeader(ushort size, bool used)
            => new((size & BLOCK_SIZE_MASK) | (used ? BLOCK_STAT_MASK : 0));
        public static (ushort, bool) GetHeader(DataItem header)
            => ((ushort)(header.ValueInt & BLOCK_SIZE_MASK), (header.ValueInt & BLOCK_STAT_MASK) != 0);

        static void FixSize(ref ushort size)
        {
            while (size == 0)
            { size++; }
        }

        public void DebugPrint()
        {
#if DEBUG
            int endlessSafe = heap.Length;
            int i = 0;
            int blockIndex = 0;
            while (i + 1 < heap.Length)
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
#endif
        }

        public const int BLOCK_HEADER_SIZE = 1;

        public int Allocate(int sizeNeed)
        {
            if (sizeNeed < ushort.MinValue || sizeNeed > ushort.MaxValue)
            { throw new OverflowException(); }

            return Allocate((ushort)sizeNeed);
        }

        int Allocate(ushort sizeNeed)
        {
            FixSize(ref sizeNeed);
            // if (sizeNeed <= 0)
            // { throw new RuntimeException($"HEAP error: Invalid size {sizeNeed} while allocating block"); }

            int endlessSafe = heap.Length;
            int headerPointer = 0;
            while (headerPointer < heap.Length)
            {
                (ushort blockSize, bool blockUsed) = GetHeader(heap[headerPointer]);
                int dataPointer = headerPointer + BLOCK_HEADER_SIZE;

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
                            // Calculate remaing size
                            int _remaingSize = blockSize - sizeNeed - BLOCK_HEADER_SIZE;

                            // If a larger block is allocated than needed
                            if (_remaingSize >= 0)
                            {
                                ushort remaingSize = (ushort)_remaingSize;
                                // Update the next block's header
                                heap[nextHeaderPointer] = GetHeader(remaingSize, false);
                            }
                        }

                        return dataPointer;
                    }
                }

                headerPointer += blockSize + BLOCK_HEADER_SIZE;

                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }

            throw new RuntimeException($"HEAP error: Failed to find free space (size {sizeNeed})");
        }

        public void Deallocate(int pointer)
        {
            int headerPointer = pointer - BLOCK_HEADER_SIZE;

            (ushort blockSize, _) = GetHeader(heap[headerPointer]);
            heap[headerPointer] = GetHeader(blockSize, false);

            Clear(pointer, blockSize);

            int joinIterations = 2;
            for (int i = 0; i < joinIterations; i++)
            {
                bool joined = JoinFreeBlocks();
                if (!joined) break;
            }
        }

        /// <returns>
        /// <see langword="true"/> if any block has been joined, <see langword="false"/> otherwise
        /// </returns>
        /// <exception cref="EndlessLoopException"/>
        bool JoinFreeBlocks()
        {
            int endlessSafe = heap.Length;

            int offset = 0;
            int prevBlockSize = 0;
            while (offset < heap.Length)
            {
                (int blockSize, bool blockUsed) = GetHeader(heap[offset]);
                int prevOffset = offset - prevBlockSize - BLOCK_HEADER_SIZE;

                // This is a free block that is not at the begining of the heap
                if (offset != 0 && !blockUsed)
                {
                    (_, bool prevBlockUsed) = GetHeader(heap[prevOffset]);

                    // The previous block is also free
                    if (!prevBlockUsed)
                    {
                        // Update the previous block's header
                        heap[prevOffset] = GetHeader((ushort)((ushort)blockSize + (ushort)prevBlockSize + (ushort)BLOCK_HEADER_SIZE), false);
                        // Remove the current block's header
                        heap[offset] = DataItem.Null;

                        return true;
                    }
                }

                prevBlockSize = blockSize;

                // Jump to the next block's header offset
                offset += blockSize + BLOCK_HEADER_SIZE;

                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }
            return false;
        }

        void Clear(int from, int length)
        {
            for (int i = from; i < from + length; i++)
            { heap[i] = DataItem.Null; }
        }
    }

    public interface IHeap : IReadOnlyHeap
    {
        public new DataItem this[int i] { get; set; }
        public int Allocate(int size);
        public void Deallocate(int pointer);
    }

    public interface IReadOnlyHeap
    {
        public void DebugPrint();
        public DataItem[] ToArray();
        public DataItem this[int i] { get; }
        public int Size { get; }
    }
}
