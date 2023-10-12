using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using LanguageCore.Brainfuck.Compiler;
using LanguageCore.Brainfuck.Renderer;
using LanguageCore.Runtime;
using Win32;

#nullable enable

namespace LanguageCore.Brainfuck
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct Value : IEquatable<Value>
    {
        readonly byte value;

        public byte V => value;

        public Value(byte v) => value = v;
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

        public char ToChar() => Convert.ToChar(value);

        public override bool Equals([NotNullWhen(true)] object? obj) =>
            obj is Value other &&
            Equals(other);
        public bool Equals(Value other) =>
            other.value == this.value;

        public override int GetHashCode() => HashCode.Combine(value);

        public override string ToString() => value.ToString();
        string GetDebuggerDisplay() => ToString();
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public class Memory<T>
    {
        readonly T[] memory;

        public T this[int i]
        {
            get
            {
                int i_ = i;
                if (i_ < 0) i_ += Length;
                return memory[i_];
            }
            internal set
            {
                int i_ = i;
                if (i_ < 0) i_ += Length;
                memory[i_] = value;
            }
        }
        public int Length => memory.Length;

        internal Memory(int size) => memory = new T[size];

        string GetDebuggerDisplay()
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

        public Value[] ToArray()
        {
            Value[] result = new Value[memory.Length];
            Array.Copy(memory, result, memory.Length);
            return result;
        }
    }

    public class Interpreter
    {
        char[] Code;
        public Memory<Value> Memory;

        Action<char> OnOutput;
        Func<char> OnInput;

        public int CodePointer { get; private set; }
        public int MemoryPointer { get; private set; }

        internal Value CurrentMemory => Memory[MemoryPointer];
        internal char CurrentInstruction => Code[CodePointer];

        internal bool OutOfCode => CodePointer >= Code.Length || CodePointer < 0;

        internal DebugInformation? DebugInfo = null;
        internal Tokenizing.Token[]? OriginalCode = null;
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

        /// <exception cref="BrainfuckRuntimeException"></exception>
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
                    { throw new BrainfuckRuntimeException($"Memory overflow", GetContext()); }
                    MemoryPointer++;
                    goto FinishInstruction;
                case '<':
                    if (MemoryPointer <= 0)
                    { throw new BrainfuckRuntimeException($"Memory underflow", GetContext()); }
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
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction == '[') depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unclosed bracket", GetContext());
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
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction == ']') depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unexpected closing bracket", GetContext());
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
                    throw new BrainfuckRuntimeException($"Unknown instruction {CurrentInstruction}", GetContext());
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
            Win32.Utilities.ConsoleListener.Start();

            Queue<char> inputBuffer = new();
            string outputBuffer = "";

            Win32.Utilities.ConsoleListener.KeyEvent += (e) =>
            {
                if (e.IsDown != 0)
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

            void Draw()
            {
                int line = 0;

                int center = CodePointer - halfWidth;
                lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
                int codePrintStart = Math.Max(0, lastCodePosition);
                int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
                DrawCode(renderer, codePrintStart, codePrintEnd, 0, line++, width);

                int memoryPrintStart = Math.Max(0, MemoryPointer - halfWidth);
                int memoryPrintEnd = Math.Min(Memory.Length - 1, MemoryPointer + (halfWidth - 1));
                DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
                DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
                DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);

                renderer.DrawText(0, line++, new string('─', width), ByteColor.Gray);

                DrawOriginalCode(renderer, 0, line, width, 15);
                height -= 15;
                line += 15;

                renderer.DrawText(0, line++, new string('─', width), ByteColor.Gray);

                DrawOutput(renderer, outputBuffer, 0, line++, width, height);

                renderer.RefreshConsole();
            }

            Draw();

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
                Draw();

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

            Draw();

            Win32.Utilities.ConsoleListener.Stop();
        }

        int StartToken;
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

            if (!DebugInfo.TryGetSourceLocation(CodePointer, out SourceCodeLocation sourceLocation)) return;

            for (int i = 0; i < OriginalCode.Length; i++)
            {
                if (OriginalCode[i].Position.Contains(sourceLocation.SourcePosition.Start) ||
                    OriginalCode[i].Position.Contains(sourceLocation.SourcePosition.End))
                {
                    StartToken = i;
                    break;
                }
            }

            if (StartToken == -1)
            { return; }

            StartToken = Math.Max(0, StartToken - 30);

            int startLine = OriginalCode[StartToken].Position.Start.Line;

            while (StartToken > 0 && OriginalCode[StartToken - 1].Position.Start.Line == startLine)
            {
                StartToken--;
            }

            for (int i = StartToken; i < OriginalCode.Length; i++)
            {
                var token = OriginalCode[i];

                int currentX = token.Position.Start.Character + x;
                int currentY = token.Position.Start.Line - startLine + y;

                if (currentY - y >= height)
                { return; }

                if (token.Position.End.Character >= width)
                { continue; }

                if (currentX < 0 || currentY < 0)
                { return; }
                if (currentX >= width || currentY >= height)
                { return; }

                string text = token.Content;
                for (int offset = 0; offset < text.Length; offset++)
                {
                    if (currentX + offset - 1 >= width) return;

                    byte foregroundColor = token.TokenType switch
                    {
                        Tokenizing.TokenType.LITERAL_NUMBER => ByteColor.BrightCyan,
                        Tokenizing.TokenType.LITERAL_HEX => ByteColor.BrightCyan,
                        Tokenizing.TokenType.LITERAL_BIN => ByteColor.BrightCyan,
                        Tokenizing.TokenType.LITERAL_FLOAT => ByteColor.BrightCyan,
                        Tokenizing.TokenType.LITERAL_STRING => ByteColor.BrightYellow,
                        Tokenizing.TokenType.LITERAL_CHAR => ByteColor.BrightYellow,
                        Tokenizing.TokenType.COMMENT => ByteColor.Green,
                        Tokenizing.TokenType.COMMENT_MULTILINE => ByteColor.Green,
                        _ => ByteColor.White,
                    };

                    foregroundColor = token.AnalyzedType switch
                    {
                        Tokenizing.TokenAnalysedType.Attribute => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalysedType.Type => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalysedType.Struct => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalysedType.Keyword => ByteColor.White,
                        Tokenizing.TokenAnalysedType.FunctionName => ByteColor.BrightYellow,
                        Tokenizing.TokenAnalysedType.VariableName => ByteColor.White,
                        Tokenizing.TokenAnalysedType.FieldName => ByteColor.White,
                        Tokenizing.TokenAnalysedType.ParameterName => ByteColor.White,
                        Tokenizing.TokenAnalysedType.Class => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalysedType.BuiltinType => ByteColor.BrightBlue,
                        Tokenizing.TokenAnalysedType.Enum => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalysedType.TypeParameter => ByteColor.Yellow,
                        _ => foregroundColor,
                    };

                    byte backgroundColor = ByteColor.Black;
                    if (sourceLocation.SourcePosition.Range.Contains(token.Position.Start))
                    { backgroundColor = ByteColor.Gray; }

                    renderer[currentX + offset - 1, currentY] = new CharInfo(text[offset], foregroundColor, backgroundColor);
                }
            }
        }

        void DrawCode(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                byte bg = (i == CodePointer) ? ByteColor.Silver : ByteColor.Black;
                byte fg = Code[i] switch
                {
                    '>' or '<' => ByteColor.BrightRed,
                    '+' or '-' => ByteColor.BrightBlue,
                    '[' or ']' => ByteColor.BrightGreen,
                    '.' or ',' => ByteColor.BrightMagenta,
                    _ => ByteColor.Silver,
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
                renderer.DrawText(x, y, textToPrint, ByteColor.Silver);
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
                { renderer.DrawText(x, y, textToPrint, ByteColor.BrightRed); }
                else if (Memory[m].V == 0)
                { renderer.DrawText(x, y, textToPrint, ByteColor.Silver); }
                else
                { renderer.DrawText(x, y, textToPrint, ByteColor.White); }

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
                { renderer.DrawText(x, y, "^   ", ByteColor.BrightRed); }
                else
                { renderer.DrawText(x, y, "    ", ByteColor.White); }

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
                    renderer[_x, _y] = new CharInfo(' ', ByteColor.White, ByteColor.Black);
                }
                else if (text[i] < 32 || text[i] > 127)
                {
                    renderer[_x, _y] = new CharInfo(' ', ByteColor.White, ByteColor.Black);
                }
                else
                {
                    renderer[_x, _y] = new CharInfo(text[i], ByteColor.White, ByteColor.Black);
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

        public void Reset()
        {
            this.CodePointer = 0;
            this.MemoryPointer = 0;
            this.Memory = new Memory<Value>(1024);
        }

        public RuntimeContext GetContext() => new(MemoryPointer, CodePointer);
    }
}
