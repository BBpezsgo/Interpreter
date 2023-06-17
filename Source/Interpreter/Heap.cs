using System;
using System.Linq;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Errors;

    internal class HEAP : IHeap
    {
        readonly DataItem[] heap;

        internal HEAP(int size = 0)
        {
            this.heap = new DataItem[size];

            this.heap[0] = GetHeader(size - 1, false);
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

        const int BLOCK_SIZE_MASK = int.MaxValue << 1;
        const int BLOCK_STATUS_MASK = 1;

        static DataItem GetHeader(int size, bool used) => new((size & BLOCK_SIZE_MASK) | ((used ? 1 : 0) & BLOCK_STATUS_MASK));
        static (int, bool) GetHeader(DataItem header) => (header.ValueInt & BLOCK_SIZE_MASK, (header.ValueInt & BLOCK_STATUS_MASK) != 0);

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
                        Console.Write(heap[j]);
                        Console.Write(" ");
                    }
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

        static int FixSize(int size)
        {
            if ((size % 2) != 0)
            { size++; }
            return size;
        }

        public void AllocateBlock(int headerPointer, int size)
        {
            if (size != (size & BLOCK_SIZE_MASK))
            { throw new RuntimeException($"HEAP error: Invalid size {size} while allocating block {headerPointer}"); }

            int oldSize = heap[headerPointer].ValueInt & BLOCK_SIZE_MASK;

            heap[headerPointer] = GetHeader(size, true);

            if (headerPointer + 1 + size < heap.Length)
            {
                int newSize = FixSize(oldSize - size - 1);

                if (newSize != (newSize & BLOCK_SIZE_MASK))
                { throw new RuntimeException($"HEAP error: Invalid size {newSize} while allocating block {headerPointer}"); }

                heap[headerPointer + 1 + size] = GetHeader(newSize, false);
            }
        }

        public int Allocate(int size)
        {
            size = FixSize(size);
            int endlessSafe = heap.Length;
            int i = 0;
            while (i < heap.Length)
            {
                (int blockSize, bool blockUsed) = GetHeader(heap[i]);

                if (blockSize >= size && !blockUsed)
                {
                    AllocateBlock(i, size);
                    return i + 1;
                }

                i += blockSize + 1;

                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }
            throw new RuntimeException($"HEAP error: Failed to get find free space (size {size})");
        }

        public void Deallocate(int pointer)
        {
            (int blockSize, _) = GetHeader(heap[pointer - 1]);
            heap[pointer - 1] = GetHeader(blockSize, false);

            Clear(pointer, blockSize);

            JoinFreeBlocks();
        }

        public bool JoinFreeBlocks()
        {
            int endlessSafe = heap.Length;
            int i = 0;
            int prevBlockSize = 0;
            while (i < heap.Length)
            {
                (int blockSize, bool blockUsed) = GetHeader(heap[i]);

                if (i != 0 && !blockUsed)
                {
                    (_, bool prevBlockUsed) = GetHeader(heap[i - prevBlockSize - 1]);

                    if (!prevBlockUsed)
                    {
                        heap[i - prevBlockSize - 1] = GetHeader(blockSize + prevBlockSize + 1, false);
                        heap[i] = DataItem.Null;
                        return true;
                    }
                }

                prevBlockSize = blockSize;
                i += blockSize + 1;

                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }
            return false;
        }

        public void Clear(int from, int length)
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
