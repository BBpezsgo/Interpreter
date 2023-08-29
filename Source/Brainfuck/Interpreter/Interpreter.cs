using ProgrammingLanguage.Brainfuck.Compiler;
using ProgrammingLanguage.Brainfuck.Renderer;
using ProgrammingLanguage.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

#nullable enable

namespace ProgrammingLanguage.Brainfuck
{
    readonly struct Value
    {
        readonly byte value;

        public byte V => value;

        internal Value(byte v) => value = v;
        public Value(int v) => this.value = (byte)v;

        public static Value operator +(Value a, Value b) => a + b.value;
        public static Value operator -(Value a, Value b) => a - b.value;

        public static Value operator +(Value a, int b)
        {
            if (false && a.value + b > byte.MaxValue)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Overflow");
                Console.ResetColor();
            }
            return new(a.value + b);
        }
        public static Value operator -(Value a, int b)
        {
            if (false && a.value - b < byte.MinValue)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Overflow");
                Console.ResetColor();
            }
            return new(a.value - b);
        }

        public static Value operator ++(Value a) => a + 1;
        public static Value operator --(Value a) => a - 1;

        public static bool operator ==(Value a, Value b) => a == b.value;
        public static bool operator !=(Value a, Value b) => a != b.value;

        public static bool operator ==(Value a, int b) => a.value == b;
        public static bool operator !=(Value a, int b) => a.value != b;

        internal char ToChar() => Convert.ToChar(value);

        public override bool Equals([NotNullWhen(true)] object? obj)
            => obj is Value value &&
            value.value == this.value;
        public override int GetHashCode() => HashCode.Combine(value);

        public override string ToString() => value.ToString();
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    class Memory<T>
    {
        readonly T[] memory;

        internal T this[int i]
        {
            get
            {
                int i_ = i;
                if (i_ < 0) i_ += Length;
                return memory[i_];
            }
            set
            {
                int i_ = i;
                if (i_ < 0) i_ += Length;
                memory[i_] = value;
            }
        }
        internal int Length => memory.Length;

        internal Memory(int size) => memory = new T[size];

        private string GetDebuggerDisplay()
        {
            string result = "";

            for (int i = 0; i < Length; i++)
            {
                if (i > 10)
                {
                    result += " ...";
                    break;
                }
                result += this[i]?.ToString() + " ";
            }

            return result.Trim();
        }
    }

    public class Interpreter
    {
        char[] Code;
        internal Memory<Value> Memory;

        Action<char> OnOutput;
        Func<char> OnInput;

        internal int CodePointer { get; private set; }
        internal int MemoryPointer { get; private set; }

        internal Value CurrentMemory => Memory[MemoryPointer];
        internal char CurrentInstruction => Code[CodePointer];

        internal bool OutOfCode => CodePointer >= Code.Length || CodePointer < 0;

        internal DebugInfo[]? DebugInfo = null;
        internal BBCode.Token[]? OriginalCode = null;
        bool isPaused;

        internal bool IsPaused => isPaused;

        void OnDefaultOutput(char data) => Console.Write(data);
        char OnDefaultInput() => Console.ReadKey(true).KeyChar;

		public Interpreter(Uri url, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => new System.Net.Http.HttpClient().GetStringAsync(url).ContinueWith((code) => this.Code = ParseCode(code.Result)).Wait();
		public Interpreter(FileInfo file, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(File.ReadAllText(file.FullName), OnOutput, OnInput) { }
		public Interpreter(string code, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => this.Code = ParseCode(code);
		public Interpreter(Action<char>? OnOutput = null, Func<char>? OnInput = null)
        {
            this.OnOutput = OnOutput ?? OnDefaultOutput;
            this.OnInput = OnInput ?? OnDefaultInput;

            this.Code = Array.Empty<char>();
            this.CodePointer = 0;
            this.MemoryPointer = 0;
            this.Memory = new Memory<Value>(1024);
        }

        static char[] ParseCode(string code)
        {
            List<char> Code = new();
            for (int i = 0; i < code.Length; i++)
            {
                if (!Utils.CodeCharacters.Contains(code[i]))
                { continue; }

                Code.Add(code[i]);
            }
            return Code.ToArray();
        }

        public static void Run(string code)
        {
            Interpreter interpreter = new(code);
            while (interpreter.Step()) ;
        }
		public static void Run(string code, int limit)
        {
            Interpreter interpreter = new(code);
            int i = 0;
            while (true)
            {
                if (!interpreter.Step()) break;
                i++;
                if (i >= limit)
                {
                    i = 0;
                    Thread.Sleep(10);
                }
            }
        }

		/// <exception cref="BrainfuckException"></exception>
		public bool Step()
        {
            if (OutOfCode) return false;

            switch (CurrentInstruction)
            {
                case '+':
                    Memory[MemoryPointer]++;
                    goto FinishInstruction;
                case '-':
                    Memory[MemoryPointer]--;
                    goto FinishInstruction;
                case '>':
                    if (MemoryPointer >= Memory.Length)
                    { throw new BrainfuckException($"Memory overflow"); }
                    MemoryPointer++;
                    goto FinishInstruction;
                case '<':
                    if (MemoryPointer <= 0)
                    { throw new BrainfuckException($"Memory underflow"); }
                    MemoryPointer--;
                    goto FinishInstruction;
                case '[':
                    if (CurrentMemory == 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer++;
                            if (OutOfCode) break;
                            if (CurrentInstruction == ']')
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckException($"Wat at {CodePointer}");
                                depth--;
                            }
                            else if (CurrentInstruction == '[') depth++;
                        }
                        throw new BrainfuckException($"Unclosed bracket at {CodePointer}");
                    }
                    goto FinishInstruction;
                case ']':
                    if (CurrentMemory != 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer--;
                            if (OutOfCode) break;
                            if (CurrentInstruction == '[')
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckException($"Wat at {CodePointer}");
                                depth--;
                            }
                            else if (CurrentInstruction == ']') depth++;
                        }
                        throw new BrainfuckException($"Unexpected closing bracket {CodePointer}");
                    }
                    goto FinishInstruction;
                case '.':
                    OnOutput?.Invoke(CharCode.GetChar(CurrentMemory.V));
                    goto FinishInstruction;
                case ',':
                    Memory[MemoryPointer] = new Value(CharCode.GetByte(OnInput?.Invoke() ?? '\0'));
                    goto FinishInstruction;
                case '$':
                    isPaused = true;
                    goto FinishInstruction;
                default:
                    throw new BrainfuckException($"Unknown instruction {CurrentInstruction}");
            }

        FinishInstruction:
            CodePointer++;
            return !OutOfCode;
        }

		public void Run()
        { while (Step()) ; }

        internal void RunWithUI(bool autoTick = true, int wait = 0)
        {
            Console.Clear();

            int width = Console.WindowWidth;
            int height = Console.WindowHeight;

            using ConsoleRenderer renderer = new(width, height);
            using ConsoleListener input = new();

            Queue<char> inputBuffer = new();
            string outputBuffer = "";

            input.KeyEvent += (e) =>
            {
                if (!e.IsDown)
                { inputBuffer.Enqueue(e.UnicodeChar); }
            };

            OnOutput = (v) => outputBuffer += v;
            OnInput = () =>
            {
                while (inputBuffer.Count == 0)
                { Thread.Sleep(100); }
                return inputBuffer.Dequeue();
            };

            int lastCodePosition = 0;

            int halfWidth = width / 2;

            {
                int center = CodePointer - halfWidth;
                lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
                int codePrintStart = Math.Max(0, lastCodePosition);
                int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
                DrawCode(renderer, codePrintStart, codePrintEnd, 0, 1, width);

                int memoryPrintStart = Math.Max(0, MemoryPointer - halfWidth);
                int memoryPrintEnd = Math.Min(Memory.Length - 1, MemoryPointer + (halfWidth - 1));
                DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, 2, width);
                DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, 3, width);
                DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, 4, width);
                DrawOutput(renderer, outputBuffer, 0, 5, width, height);

                renderer.RefreshConsole();
            }

            Thread.Sleep(100);
            inputBuffer.Clear();

            if (!autoTick || isPaused)
            {
                while (inputBuffer.Count == 0)
                { Thread.Sleep(100); }
                inputBuffer.Dequeue();
                isPaused = false;
            }

            while (Step())
            {
                {
                    int y = 0;
                    // DrawOriginalCode(renderer, 0, y, width, 30);
                    // y += 30;

                    int center = CodePointer - halfWidth;
                    lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
                    int codePrintStart = Math.Max(0, lastCodePosition);
                    int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
                    DrawCode(renderer, codePrintStart, codePrintEnd, 0, y++, width);

                    int memoryPrintStart = Math.Max(0, MemoryPointer - halfWidth);
                    int memoryPrintEnd = Math.Min(Memory.Length - 1, MemoryPointer + (halfWidth - 1));
                    DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, y++, width);
                    DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, y++, width);
                    DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, y++, width);
                    DrawOutput(renderer, outputBuffer, 0, y++, width, height);

                    renderer.RefreshConsole();
                }

                if (!autoTick || isPaused)
                {
                    while (inputBuffer.Count == 0)
                    { Thread.Sleep(100); }
                    inputBuffer.Dequeue();
                    isPaused = false;
                }
                else if (wait > 0)
                {
                    Thread.Sleep(wait);
                }
            }

            {
                int center = CodePointer - halfWidth;
                lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
                int codePrintStart = Math.Max(0, lastCodePosition);
                int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
                DrawCode(renderer, codePrintStart, codePrintEnd, 0, 1, width);

                int memoryPrintStart = Math.Max(0, MemoryPointer - halfWidth);
                int memoryPrintEnd = Math.Min(Memory.Length - 1, MemoryPointer + (halfWidth - 1));
                DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, 2, width);
                DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, 3, width);
                DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, 4, width);
                DrawOutput(renderer, outputBuffer, 0, 5, width, height);

                renderer.RefreshConsole();
            }
        }

        void DrawOriginalCode(ConsoleRenderer renderer, int x, int y, int width, int height)
        {
            for (int _x = x; _x < width + x; _x++)
            {
                for (int _y = y; _y < height + y; _y++)
                {
                    renderer[_x, _y] = new CharInfo(' ');
                }
            }

            if (DebugInfo == null) return;
            if (OriginalCode == null) return;

            DebugInfo? c = null;
            for (int i = 0; i < DebugInfo.Length; i++)
            {
                if (DebugInfo[i].InstructionStart >= CodePointer &&
                    DebugInfo[i].InstructionEnd <= CodePointer)
                {
                    c = DebugInfo[i];
                }
            }

            if (c == null) return;

            int tokenI = -1;
            for (int i = 0; i < OriginalCode.Length; i++)
            {
                if (OriginalCode[i].Position.Contains(c.Position.Start) ||
                    OriginalCode[i].Position.Contains(c.Position.End))
                {
                    tokenI = i;
                    break;
                }
            }

            if (tokenI == -1) return;

            tokenI = Math.Max(0, tokenI - 30);

            int startLine = OriginalCode[tokenI].Position.Start.Line;

            for (int i = tokenI; i < OriginalCode.Length; i++)
            {
                int currentX = OriginalCode[i].Position.Start.Character + x;
                int currentY = OriginalCode[i].Position.Start.Line - startLine + y;

                if (currentY - y >= height)
                {
                    return;
                }

                if (OriginalCode[i].Position.End.Character < width)
                {
                    string text = OriginalCode[i].Content;
                    {
                        if (currentX < 0 || currentY < 0) return;
                        if (currentX >= width || currentY >= height) return;

                        for (int offset = 0; offset < text.Length; offset++)
                        {
                            if (currentX + offset >= width) return;

                            var foregroundColor = ForegroundColor.White;
                            var backgroundColor = BackgroundColor.Black;

                            SinglePosition singlePosition = new(OriginalCode[i].Position.Start.Line, OriginalCode[i].Position.Start.Character + offset);

                            if (c.Position.Range.Contains(singlePosition))
                            {
                                backgroundColor = BackgroundColor.Green;
                            }

                            renderer[currentX + offset, currentY] = new CharInfo(text[offset], foregroundColor, backgroundColor);
                        }
                    }
                }
            }
        }

        void DrawCode(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                BackgroundColor bg = (i == CodePointer) ? BackgroundColor.Gray : BackgroundColor.Black;
                ForegroundColor fg = Code[i] switch
                {
                    '>' or '<' => ForegroundColor.Red,
                    '+' or '-' => ForegroundColor.Blue,
                    '[' or ']' => ForegroundColor.Green,
                    '.' or ',' => ForegroundColor.Magenta,
                    _ => ForegroundColor.Gray,
                };
                renderer[x, y] = new CharInfo(Code[i], fg, bg);

                if (x++ >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawMemoryChars(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                char chr = CharCode.GetChar(Memory[i].V);
                if (Memory[i].V < 32) chr = ' ';
                if (Memory[i].V > 126) chr = ' ';
                chr = chr switch
                {
                    '\0' => ' ',
                    '\r' => ' ',
                    '\n' => ' ',
                    '\t' => ' ',
                    _ => chr,
                };

                string textToPrint = chr.ToString().PadRight(4, ' ');
                renderer.DrawText(x, y, textToPrint, ForegroundColor.Gray, BackgroundColor.Black);
                x += textToPrint.Length;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawMemoryRaw(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                string textToPrint = Memory[m].V.ToString().PadRight(4, ' ');

                if (MemoryPointer == m)
                { renderer.DrawText(x, y, textToPrint, ForegroundColor.Red, BackgroundColor.Black); }
                else if (Memory[m].V == 0)
                { renderer.DrawText(x, y, textToPrint, ForegroundColor.Gray, BackgroundColor.Black); }
                else
                { renderer.DrawText(x, y, textToPrint, ForegroundColor.White, BackgroundColor.Black); }

                x += textToPrint.Length;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawMemoryPointer(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                if (MemoryPointer == m)
                { renderer.DrawText(x, y, "^   ", ForegroundColor.Red); }
                else
                { renderer.DrawText(x, y, "    "); }

                x += 4;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawOutput(ConsoleRenderer renderer, string text, int x, int y, int width, int height)
        {
            int _x = x;
            int _y = y;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    _x = x;
                    _y++;
                    if (_y >= height) break;
                    continue;
                }
                else if (text[i] == '\r')
                {

                }
                else if (text[i] == '\t')
                {
                    renderer[_x, _y] = new CharInfo(' ', ForegroundColor.White, BackgroundColor.Black);
                }
                else if (text[i] < 32 || text[i] > 127)
                {
                    renderer[_x, _y] = new CharInfo(' ', ForegroundColor.White, BackgroundColor.Black);
                }
                else
                {
                    renderer[_x, _y] = new CharInfo(text[i], ForegroundColor.White, BackgroundColor.Black);
                }

                _x++;
                if (_x >= width)
                {
                    _x = x;
                    _y++;
                    if (_y >= height) break;
                }
            }
        }

        internal void Reset()
        {
            this.CodePointer = 0;
            this.MemoryPointer = 0;
            this.Memory = new Memory<Value>(1024);
        }

        [Serializable]
        public class BrainfuckException : Exception
        {
            public BrainfuckException() { }
            public BrainfuckException(string message) : base(message) { }
            public BrainfuckException(string message, Exception inner) : base(message, inner) { }
            protected BrainfuckException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
    }
}
