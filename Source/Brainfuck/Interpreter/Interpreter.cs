using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Win32;
using Win32.LowLevel;

namespace LanguageCore.Brainfuck
{
    using Runtime;
    using Thread = System.Threading.Thread;

    public delegate void OutputCallback(byte data);
    public delegate byte InputCallback();

    public class Interpreter
    {
        public const int MEMORY_SIZE = 1024;

        byte[] Code;
        public byte[] Memory;

        OutputCallback OnOutput;
        InputCallback OnInput;

        int codePointer;
        int memoryPointer;

        public int CodePointer => codePointer;
        public int MemoryPointer => memoryPointer;

        public bool OutOfCode => codePointer >= Code.Length || codePointer < 0;

        public DebugInformation? DebugInfo;
        public Tokenizing.Token[]? OriginalCode;
        bool isPaused;

        public bool IsPaused => isPaused;

        public static void OnDefaultOutput(byte data) => Console.Write(CharCode.GetChar(data));
        public static byte OnDefaultInput() => CharCode.GetByte(Console.ReadKey(true).KeyChar);

        public Interpreter(Uri url, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(OnOutput, OnInput)
            => new System.Net.Http.HttpClient().GetStringAsync(url).ContinueWith((code) => this.Code = CompactCode.OpCode(ParseCode(code.Result))).Wait();
        public Interpreter(FileInfo file, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(File.ReadAllText(file.FullName), OnOutput, OnInput) { }
        public Interpreter(string? code, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(OnOutput, OnInput)
            => this.Code = CompactCode.OpCode(ParseCode(code ?? throw new ArgumentNullException(nameof(code))));
        public Interpreter(OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        {
            this.OnOutput = OnOutput ?? OnDefaultOutput;
            this.OnInput = OnInput ?? OnDefaultInput;

            this.Code = Array.Empty<byte>();
            this.codePointer = 0;
            this.memoryPointer = 0;
            this.Memory = new byte[MEMORY_SIZE];
        }

        static char[] ParseCode(string code)
        {
            List<char> Code = new(code.Length);
            for (int i = 0; i < code.Length; i++)
            {
                if (!BrainfuckCode.CodeCharacters.Contains(code[i]))
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

            switch (Code[codePointer])
            {
                case OpCodes.ADD:
                    Memory[memoryPointer]++;
                    break;
                case OpCodes.SUB:
                    Memory[memoryPointer]--;
                    break;
                case OpCodes.POINTER_R:
                    if (memoryPointer++ >= Memory.Length)
                    { throw new BrainfuckRuntimeException($"Memory overflow", GetContext()); }
                    break;
                case OpCodes.POINTER_L:
                    if (memoryPointer-- <= 0)
                    { throw new BrainfuckRuntimeException($"Memory underflow", GetContext()); }
                    break;
                case OpCodes.BRANCH_START:
                    if (Memory[memoryPointer] == 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            codePointer++;
                            if (OutOfCode) break;
                            if (Code[codePointer] == OpCodes.BRANCH_END)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (Code[codePointer] == OpCodes.BRANCH_START) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unclosed bracket", GetContext());
                    }
                    break;
                case OpCodes.BRANCH_END:
                    if (Memory[memoryPointer] != 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            codePointer--;
                            if (OutOfCode) break;
                            if (Code[codePointer] == OpCodes.BRANCH_START)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (Code[codePointer] == OpCodes.BRANCH_END) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unexpected closing bracket", GetContext());
                    }
                    break;
                case OpCodes.OUT:
                    OnOutput?.Invoke(Memory[memoryPointer]);
                    break;
                case OpCodes.IN:
                    Memory[memoryPointer] = OnInput?.Invoke() ?? 0;
                    break;
                case OpCodes.DEBUG:
                    isPaused = true;
                    break;
                default:
                    throw new BrainfuckRuntimeException($"Unknown instruction {Code[codePointer]}", GetContext());
            }

        FinishInstruction:
            codePointer++;
            return !OutOfCode;
        }

        public void Run()
        { while (Step()) ; }

        public void RunWithUI(bool autoTick = true, int wait = 0)
        {
            Console.Clear();

            short width = ConsoleHandler.WindowWidth;
            short height = ConsoleHandler.WindowHeight;

            ConsoleRenderer renderer = new(width, height);
            ConsoleListener.Start();

            Queue<byte> inputBuffer = new();
            string outputBuffer = string.Empty;

            ConsoleListener.KeyEvent += Keyboard.Feed;
            ConsoleListener.MouseEvent += Mouse.Feed;

            ConsoleListener.KeyEvent += (e) =>
            {
                if (e.IsDown != 0)
                { inputBuffer.Enqueue(e.AsciiChar); }
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

                int center = codePointer - halfWidth;
                lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
                int codePrintStart = Math.Max(0, lastCodePosition);
                int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
                DrawCode(renderer, codePrintStart, codePrintEnd, 0, line++, width);

                int memoryPrintStart = Math.Max(0, memoryPointer - halfWidth);
                int memoryPrintEnd = Math.Min(Memory.Length - 1, memoryPointer + (halfWidth - 1));
                DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
                DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
                DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);

                renderer.Text(0, line++, new string('─', width), ByteColor.Gray);

                DrawOriginalCode(renderer, 0, line, width, 15);
                height -= 15;
                line += 15;

                renderer.Text(0, line++, new string('─', width), ByteColor.Gray);

                DrawOutput(renderer, outputBuffer, 0, line++, width, height);

                renderer.Text(0, line++, new string('─', width), ByteColor.Gray);

                renderer.Text(0, line, new string(' ', width));

                if (DebugInfo != null)
                {
                    FunctionInformations functionInfo = DebugInfo.GetFunctionInformations(codePointer);
                    if (functionInfo.IsValid)
                    { renderer.Text(0, line++, functionInfo.ReadableIdentifier, ByteColor.White); }
                }

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

            ConsoleListener.Stop();
        }

        int StartToken;
        void DrawOriginalCode(ConsoleRenderer renderer, int x, int y, int width, int height)
        {
            for (int _x = x; _x < width + x; _x++)
            {
                for (int _y = y; _y < height + y; _y++)
                {
                    renderer[_x, _y] = new ConsoleChar(' ');
                }
            }

            if (DebugInfo == null) return;
            if (OriginalCode == null) return;

            if (!DebugInfo.TryGetSourceLocation(codePointer, out SourceCodeLocation sourceLocation)) return;

            for (int i = 0; i < OriginalCode.Length; i++)
            {
                if (OriginalCode[i].Position.Range.Contains(sourceLocation.SourcePosition.Range.Start) ||
                    OriginalCode[i].Position.Range.Contains(sourceLocation.SourcePosition.Range.End))
                {
                    StartToken = i;
                    break;
                }
            }

            if (StartToken == -1)
            { return; }

            StartToken = Math.Max(0, StartToken - 30);

            int startLine = OriginalCode[StartToken].Position.Range.Start.Line;

            while (StartToken > 0 && OriginalCode[StartToken - 1].Position.Range.Start.Line == startLine)
            {
                StartToken--;
            }

            for (int i = StartToken; i < OriginalCode.Length; i++)
            {
                Tokenizing.Token token = OriginalCode[i];

                int currentX = token.Position.Range.Start.Character + x;
                int currentY = token.Position.Range.Start.Line - startLine + y;

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
                    if (sourceLocation.SourcePosition.Range.Contains(token.Position.Range.Start))
                    { backgroundColor = ByteColor.Gray; }

                    renderer[currentX + offset - 1, currentY] = new ConsoleChar(text[offset], foregroundColor, backgroundColor);
                }
            }
        }

        void DrawCode(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                byte bg = (i == codePointer) ? ByteColor.Silver : ByteColor.Black;
                byte fg = CompactCode.OpCode(Code[i]) switch
                {
                    '>' or '<' => ByteColor.BrightRed,
                    '+' or '-' => ByteColor.BrightBlue,
                    '[' or ']' => ByteColor.BrightGreen,
                    '.' or ',' => ByteColor.BrightMagenta,
                    _ => ByteColor.Silver,
                };
                renderer[x, y] = new ConsoleChar(CompactCode.OpCode(Code[i]), fg, bg);

                if (x++ >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new ConsoleChar(' ');
                x++;
            }
        }

        void DrawMemoryChars(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int i = start; i <= end; i++)
            {
                char chr = CharCode.GetChar(Memory[i]);
                if (Memory[i] < 32) chr = ' ';
                if (Memory[i] > 126) chr = ' ';
                chr = chr switch
                {
                    '\0' => ' ',
                    '\r' => ' ',
                    '\n' => ' ',
                    '\t' => ' ',
                    _ => chr,
                };

                string textToPrint = chr.ToString().PadRight(4, ' ');
                renderer.Text(x, y, textToPrint, ByteColor.Silver);
                x += textToPrint.Length;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new ConsoleChar(' ');
                x++;
            }
        }

        void DrawMemoryRaw(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                string textToPrint = Memory[m].ToString().PadRight(4, ' ');

                if (memoryPointer == m)
                { renderer.Text(x, y, textToPrint, ByteColor.BrightRed); }
                else if (Memory[m] == 0)
                { renderer.Text(x, y, textToPrint, ByteColor.Silver); }
                else
                { renderer.Text(x, y, textToPrint, ByteColor.White); }

                x += textToPrint.Length;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new ConsoleChar(' ');
                x++;
            }
        }

        void DrawMemoryPointer(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
        {
            for (int m = start; m <= end; m++)
            {
                if (memoryPointer == m)
                { renderer.Text(x, y, "^   ", ByteColor.BrightRed); }
                else
                { renderer.Text(x, y, "    ", ByteColor.White); }

                x += 4;

                if (x >= width)
                { return; }
            }

            while (x < width)
            {
                renderer[x, y] = new ConsoleChar(' ');
                x++;
            }
        }

        static void DrawOutput(ConsoleRenderer renderer, string text, int x, int y, int width, int height)
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
                    renderer[_x, _y] = new ConsoleChar(' ', ByteColor.White, ByteColor.Black);
                }
                else if (text[i] < 32 || text[i] > 127)
                {
                    renderer[_x, _y] = new ConsoleChar(' ', ByteColor.White, ByteColor.Black);
                }
                else
                {
                    renderer[_x, _y] = new ConsoleChar(text[i], ByteColor.White, ByteColor.Black);
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
            this.codePointer = 0;
            this.memoryPointer = 0;
            Array.Clear(this.Memory);
        }

        public RuntimeContext GetContext() => new(memoryPointer, codePointer);
    }
}
