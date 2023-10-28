using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using LanguageCore.Runtime;
using Win32;

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
            string result = string.Empty;

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
        byte[] Code;
        public Memory<Value> Memory;

        Action<char> OnOutput;
        Func<char> OnInput;

        public int CodePointer { get; private set; }
        public int MemoryPointer { get; private set; }

        internal Value CurrentMemory => Memory[MemoryPointer];
        internal byte CurrentInstruction => Code[CodePointer];
        internal char CurrentInstructionC => CompactCode.OpCode(Code[CodePointer]);

        internal bool OutOfCode => CodePointer >= Code.Length || CodePointer < 0;

        internal DebugInformation? DebugInfo = null;
        internal Tokenizing.Token[]? OriginalCode = null;
        bool isPaused;

        internal bool IsPaused => isPaused;

        void OnDefaultOutput(char data) => Console.Write(data);
        char OnDefaultInput() => Console.ReadKey(true).KeyChar;

        public Interpreter(Uri url, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => new System.Net.Http.HttpClient().GetStringAsync(url).ContinueWith((code) => this.Code = CompactCode.OpCode(ParseCode(code.Result))).Wait();
        public Interpreter(FileInfo file, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(File.ReadAllText(file.FullName), OnOutput, OnInput) { }
        public Interpreter(string? code, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => this.Code = CompactCode.OpCode(ParseCode(code ?? throw new ArgumentNullException(nameof(code))));
        public Interpreter(Action<char>? OnOutput = null, Func<char>? OnInput = null)
        {
            this.OnOutput = OnOutput ?? OnDefaultOutput;
            this.OnInput = OnInput ?? OnDefaultInput;

            this.Code = Array.Empty<byte>();
            this.CodePointer = 0;
            this.MemoryPointer = 0;
            this.Memory = new Memory<Value>(1024);
        }

        static char[] ParseCode(string code)
        {
            List<char> Code = new(code.Length);
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

            switch (Code[CodePointer])
            {
                case 3: // '+'
                    Memory[MemoryPointer]++;
                    goto FinishInstruction;
                case 4: // '-'
                    Memory[MemoryPointer]--;
                    goto FinishInstruction;
                case 1: // '>'
                    if (MemoryPointer >= Memory.Length)
                    { throw new BrainfuckRuntimeException($"Memory overflow", GetContext()); }
                    MemoryPointer++;
                    goto FinishInstruction;
                case 2: // '<'
                    if (MemoryPointer <= 0)
                    { throw new BrainfuckRuntimeException($"Memory underflow", GetContext()); }
                    MemoryPointer--;
                    goto FinishInstruction;
                case 5: // '['
                    if (CurrentMemory == 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer++;
                            if (OutOfCode) break;
                            if (CurrentInstruction == 6)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction == 5) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unclosed bracket", GetContext());
                    }
                    goto FinishInstruction;
                case 6: // ']'
                    if (CurrentMemory != 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer--;
                            if (OutOfCode) break;
                            if (CurrentInstruction == 5)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction == 6) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unexpected closing bracket", GetContext());
                    }
                    goto FinishInstruction;
                case 7: // '.'
                    OnOutput?.Invoke(CharCode.GetChar(CurrentMemory.V));
                    goto FinishInstruction;
                case 8: // ','
                    Memory[MemoryPointer] = new Value(CharCode.GetByte(OnInput?.Invoke() ?? '\0'));
                    goto FinishInstruction;
                case 9: // '$'
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

            using Win32.Utilities.ConsoleRenderer renderer = new(null, width, height);
            Win32.Utilities.ConsoleListener.Start();
            GUI.ConsoleRenderer = renderer;

            Queue<char> inputBuffer = new();
            string outputBuffer = string.Empty;

            Win32.Utilities.ConsoleListener.KeyEvent += Win32.Utilities.Keyboard.Feed;
            Win32.Utilities.ConsoleListener.MouseEvent += Win32.Utilities.Mouse.Feed;

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

                GUI.Label(0, line++, new string('─', width), ByteColor.Gray);

                DrawOriginalCode(renderer, 0, line, width, 15);
                height -= 15;
                line += 15;

                GUI.Label(0, line++, new string('─', width), ByteColor.Gray);

                DrawOutput(renderer, outputBuffer, 0, line++, width, height);

                renderer.Render();
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
        void DrawOriginalCode(Win32.Utilities.ConsoleRenderer renderer, int x, int y, int width, int height)
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

                if (currentX < 0 || currentY < 0)
                { return; }
                if (currentY >= height)
                { return; }

                string text = token.ToOriginalString();
                for (int offset = 0; offset < text.Length; offset++)
                {
                    if (currentX + offset - 1 >= width) return;

                    byte foregroundColor = ByteColor.Silver;

                    /*
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
                        Tokenizing.TokenAnalyzedType.Attribute => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Type => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Struct => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Keyword => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.FunctionName => ByteColor.BrightYellow,
                        Tokenizing.TokenAnalyzedType.VariableName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.FieldName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.ParameterName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.Class => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.BuiltinType => ByteColor.BrightBlue,
                        Tokenizing.TokenAnalyzedType.Enum => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.TypeParameter => ByteColor.Yellow,
                        _ => foregroundColor,
                    };
                    */

                    byte backgroundColor = ByteColor.Black;
                    if (sourceLocation.SourcePosition.Range.Contains(token.Position.Start))
                    { backgroundColor = ByteColor.Gray; }

                    renderer[currentX + offset - 1, currentY] = new CharInfo(text[offset], foregroundColor, backgroundColor);
                }
            }
        }

        void DrawCode(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                byte bg = (i == CodePointer) ? ByteColor.Silver : ByteColor.Black;
                byte fg = CompactCode.OpCode(Code[i]) switch
                {
                    '>' or '<' => ByteColor.BrightRed,
                    '+' or '-' => ByteColor.BrightBlue,
                    '[' or ']' => ByteColor.BrightGreen,
                    '.' or ',' => ByteColor.BrightMagenta,
                    _ => ByteColor.Silver,
                };
                renderer[x, y] = new CharInfo(CompactCode.OpCode(Code[i]), fg, bg);

                if (x++ >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawMemoryChars(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
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
                GUI.Label(x, y, textToPrint, ByteColor.Silver);
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

        void DrawMemoryRaw(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                string textToPrint = Memory[m].V.ToString().PadRight(4, ' ');

                if (MemoryPointer == m)
                { GUI.Label(x, y, textToPrint, ByteColor.BrightRed); }
                else if (Memory[m].V == 0)
                { GUI.Label(x, y, textToPrint, ByteColor.Silver); }
                else
                { GUI.Label(x, y, textToPrint, ByteColor.White); }

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

        void DrawMemoryPointer(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                if (MemoryPointer == m)
                { GUI.Label(x, y, "^   ", ByteColor.BrightRed); }
                else
                { GUI.Label(x, y, "    ", ByteColor.White); }

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

        void DrawOutput(Win32.Utilities.ConsoleRenderer renderer, string text, int x, int y, int width, int height)
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

    public class InterpreterCompact
    {
        CompactCodeSegment[] Code;
        public Memory<Value> Memory;

        Action<char> OnOutput;
        Func<char> OnInput;

        public int CodePointer { get; private set; }
        public int MemoryPointer { get; private set; }

        internal Value CurrentMemory => Memory[MemoryPointer];
        internal CompactCodeSegment CurrentInstruction => Code[CodePointer];

        internal bool OutOfCode => CodePointer >= Code.Length || CodePointer < 0;

        internal DebugInformation? DebugInfo = null;
        internal Tokenizing.Token[]? OriginalCode = null;
        bool isPaused;

        internal bool IsPaused => isPaused;

        void OnDefaultOutput(char data) => Console.Write(data);
        char OnDefaultInput() => Console.ReadKey(true).KeyChar;

        public InterpreterCompact(Uri url, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => new System.Net.Http.HttpClient().GetStringAsync(url).ContinueWith((code) => this.Code = CompactCode.Generate(ParseCode(code.Result))).Wait();
        public InterpreterCompact(FileInfo file, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(File.ReadAllText(file.FullName), OnOutput, OnInput) { }
        public InterpreterCompact(string? code, Action<char>? OnOutput = null, Func<char>? OnInput = null) : this(OnOutput, OnInput) => this.Code = CompactCode.Generate(ParseCode(code ?? throw new ArgumentNullException(nameof(code))));
        public InterpreterCompact(Action<char>? OnOutput = null, Func<char>? OnInput = null)
        {
            this.OnOutput = OnOutput ?? OnDefaultOutput;
            this.OnInput = OnInput ?? OnDefaultInput;

            this.Code = Array.Empty<CompactCodeSegment>();
            this.CodePointer = 0;
            this.MemoryPointer = 0;
            this.Memory = new Memory<Value>(1024);
        }

        static char[] ParseCode(string code)
        {
            List<char> Code = new(code.Length);
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
            InterpreterCompact interpreter = new(code);
            while (interpreter.Step()) ;
        }
        public static void Run(string code, int limit)
        {
            InterpreterCompact interpreter = new(code);
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

            switch (Code[CodePointer].OpCode)
            {
                case OpCodesCompact.CLEAR:
                    Memory[MemoryPointer] = new Value(0);
                    goto FinishInstruction;
                case OpCodes.ADD:
                    Memory[MemoryPointer] += Code[CodePointer].Count;
                    goto FinishInstruction;
                case OpCodes.SUB:
                    Memory[MemoryPointer] -= Code[CodePointer].Count;
                    goto FinishInstruction;
                case OpCodes.POINTER_R:
                    if (MemoryPointer >= Memory.Length)
                    { throw new BrainfuckRuntimeException($"Memory overflow", GetContext()); }
                    MemoryPointer += Code[CodePointer].Count;
                    goto FinishInstruction;
                case OpCodes.POINTER_L:
                    if (MemoryPointer <= 0)
                    { throw new BrainfuckRuntimeException($"Memory underflow", GetContext()); }
                    MemoryPointer -= Code[CodePointer].Count;
                    goto FinishInstruction;
                case OpCodes.BRANCH_START:
                    if (Code[CodePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    if (CurrentMemory == 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer++;
                            if (OutOfCode) break;
                            if (CurrentInstruction.OpCode == OpCodes.BRANCH_END)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction.OpCode == OpCodes.BRANCH_START) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unclosed bracket", GetContext());
                    }
                    goto FinishInstruction;
                case OpCodes.BRANCH_END:
                    if (Code[CodePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    if (CurrentMemory != 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            CodePointer--;
                            if (OutOfCode) break;
                            if (CurrentInstruction.OpCode == OpCodes.BRANCH_START)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (CurrentInstruction.OpCode == OpCodes.BRANCH_END) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unexpected closing bracket", GetContext());
                    }
                    goto FinishInstruction;
                case OpCodes.OUT:
                    if (Code[CodePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    OnOutput?.Invoke(CharCode.GetChar(CurrentMemory.V));
                    goto FinishInstruction;
                case OpCodes.IN:
                    if (Code[CodePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    Memory[MemoryPointer] = new Value(CharCode.GetByte(OnInput?.Invoke() ?? '\0'));
                    goto FinishInstruction;
                case OpCodes.DEBUG:
                    if (Code[CodePointer].Count != 1)
                    { throw new NotImplementedException(); }
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

            using Win32.Utilities.ConsoleRenderer renderer = new(null, width, height);
            Win32.Utilities.ConsoleListener.Start();
            GUI.ConsoleRenderer = renderer;

            Queue<char> inputBuffer = new();
            string outputBuffer = string.Empty;

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

                GUI.Label(0, line++, new string('─', width), ByteColor.Gray);

                DrawOriginalCode(renderer, 0, line, width, 15);
                height -= 15;
                line += 15;

                GUI.Label(0, line++, new string('─', width), ByteColor.Gray);

                DrawOutput(renderer, outputBuffer, 0, line++, width, height);

                renderer.Render();
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
        void DrawOriginalCode(Win32.Utilities.ConsoleRenderer renderer, int x, int y, int width, int height)
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

                if (currentX < 0 || currentY < 0)
                { return; }
                if (currentY >= height)
                { return; }

                string text = token.ToOriginalString();
                for (int offset = 0; offset < text.Length; offset++)
                {
                    if (currentX + offset - 1 >= width) return;

                    byte foregroundColor = ByteColor.Silver;

                    /*
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
                        Tokenizing.TokenAnalyzedType.Attribute => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Type => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Struct => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Keyword => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.FunctionName => ByteColor.BrightYellow,
                        Tokenizing.TokenAnalyzedType.VariableName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.FieldName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.ParameterName => ByteColor.White,
                        Tokenizing.TokenAnalyzedType.Class => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.BuiltinType => ByteColor.BrightBlue,
                        Tokenizing.TokenAnalyzedType.Enum => ByteColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.TypeParameter => ByteColor.Yellow,
                        _ => foregroundColor,
                    };
                    */

                    byte backgroundColor = ByteColor.Black;
                    if (sourceLocation.SourcePosition.Range.Contains(token.Position.Start))
                    { backgroundColor = ByteColor.Gray; }

                    renderer[currentX + offset - 1, currentY] = new CharInfo(text[offset], foregroundColor, backgroundColor);
                }
            }
        }

        void DrawCode(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                byte bg = (i == CodePointer) ? ByteColor.Silver : ByteColor.Black;

                string code = Code[i].OpCode switch
                {
                    OpCodes.POINTER_R => ">",
                    OpCodes.POINTER_L => "<",
                    OpCodes.ADD => "+",
                    OpCodes.SUB => "-",
                    OpCodes.BRANCH_START => "[",
                    OpCodes.BRANCH_END => "]",
                    OpCodes.OUT => ".",
                    OpCodes.IN => ",",
                    OpCodes.DEBUG => "$",
                    OpCodesCompact.CLEAR => "[-]",
                    _ => string.Empty,
                };

                for (int x2 = 0; x2 < code.Length; x2++)
                {
                    char c = code[x2];

                    byte fg = c switch
                    {
                        '>' or '<' => ByteColor.BrightRed,
                        '+' or '-' => ByteColor.BrightBlue,
                        '[' or ']' => ByteColor.BrightGreen,
                        '.' or ',' => ByteColor.BrightMagenta,
                        _ => ByteColor.Silver,
                    };

                    renderer[x, y] = new CharInfo(c, fg, bg);
                    if (x++ >= width) return;
                }

                if (Code[i].Count != 1)
                {
                    GUI.Label(ref x, y, Code[i].Count.ToString(), CharInfoAttribute.Make(bg, ByteColor.BrightYellow));
                    if (x >= width) return;
                }
            }

            while (x < width)
            {
                renderer[x, y] = new CharInfo(' ');
                x++;
            }
        }

        void DrawMemoryChars(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
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
                GUI.Label(x, y, textToPrint, ByteColor.Silver);
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

        void DrawMemoryRaw(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                string textToPrint = Memory[m].V.ToString().PadRight(4, ' ');

                if (MemoryPointer == m)
                { GUI.Label(x, y, textToPrint, ByteColor.BrightRed); }
                else if (Memory[m].V == 0)
                { GUI.Label(x, y, textToPrint, ByteColor.Silver); }
                else
                { GUI.Label(x, y, textToPrint, ByteColor.White); }

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

        void DrawMemoryPointer(Win32.Utilities.ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                if (MemoryPointer == m)
                { GUI.Label(x, y, "^   ", ByteColor.BrightRed); }
                else
                { GUI.Label(x, y, "    ", ByteColor.White); }

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

        void DrawOutput(Win32.Utilities.ConsoleRenderer renderer, string text, int x, int y, int width, int height)
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
