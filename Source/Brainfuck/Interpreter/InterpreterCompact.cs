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

    public class InterpreterCompact
    {
        CompactCodeSegment[] Code;
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

        public InterpreterCompact(Uri url, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(OnOutput, OnInput)
            => new System.Net.Http.HttpClient().GetStringAsync(url).ContinueWith((code) => this.Code = CompactCode.Generate(ParseCode(code.Result))).Wait();
        public InterpreterCompact(FileInfo file, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(File.ReadAllText(file.FullName), OnOutput, OnInput) { }
        public InterpreterCompact(string? code, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
            : this(OnOutput, OnInput)
            => this.Code = CompactCode.Generate(ParseCode(code ?? throw new ArgumentNullException(nameof(code))));
        public InterpreterCompact(OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        {
            this.OnOutput = OnOutput ?? Interpreter.OnDefaultOutput;
            this.OnInput = OnInput ?? Interpreter.OnDefaultInput;

            this.Code = Array.Empty<CompactCodeSegment>();
            this.codePointer = 0;
            this.memoryPointer = 0;
            this.Memory = new byte[Interpreter.MEMORY_SIZE];
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

            switch (Code[codePointer].OpCode)
            {
                case OpCodesCompact.CLEAR:
                    Memory[memoryPointer] = 0;
                    break;
                case OpCodes.ADD:
                    Memory[memoryPointer] = (byte)(Memory[memoryPointer] + Code[codePointer].Count);
                    break;
                case OpCodes.SUB:
                    Memory[memoryPointer] = (byte)(Memory[memoryPointer] - Code[codePointer].Count);
                    break;
                case OpCodes.POINTER_R:
                    memoryPointer += Code[codePointer].Count;
                    if (memoryPointer >= Memory.Length)
                    { throw new BrainfuckRuntimeException($"Memory overflow", GetContext()); }
                    break;
                case OpCodes.POINTER_L:
                    memoryPointer -= Code[codePointer].Count;
                    if (memoryPointer < 0)
                    { throw new BrainfuckRuntimeException($"Memory underflow", GetContext()); }
                    break;
                case OpCodes.BRANCH_START:
                    if (Code[codePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    if (Memory[memoryPointer] == 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            codePointer++;
                            if (OutOfCode) break;
                            if (Code[codePointer].OpCode == OpCodes.BRANCH_END)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (Code[codePointer].OpCode == OpCodes.BRANCH_START) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unclosed bracket", GetContext());
                    }
                    break;
                case OpCodes.BRANCH_END:
                    if (Code[codePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    if (Memory[memoryPointer] != 0)
                    {
                        int depth = 0;
                        while (!OutOfCode)
                        {
                            codePointer--;
                            if (OutOfCode) break;
                            if (Code[codePointer].OpCode == OpCodes.BRANCH_START)
                            {
                                if (depth == 0) goto FinishInstruction;
                                if (depth < 0) throw new BrainfuckRuntimeException($"Wat", GetContext());
                                depth--;
                            }
                            else if (Code[codePointer].OpCode == OpCodes.BRANCH_END) depth++;
                        }
                        throw new BrainfuckRuntimeException($"Unexpected closing bracket", GetContext());
                    }
                    break;
                case OpCodes.OUT:
                    if (Code[codePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    OnOutput?.Invoke(Memory[memoryPointer]);
                    break;
                case OpCodes.IN:
                    if (Code[codePointer].Count != 1)
                    { throw new NotImplementedException(); }
                    Memory[memoryPointer] = OnInput?.Invoke() ?? 0;
                    break;
                case OpCodes.DEBUG:
                    if (Code[codePointer].Count != 1)
                    { throw new NotImplementedException(); }
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

                if (DebugInfo != null)
                {
                    renderer.Text(0, line++, new string('─', width), ByteColor.Gray);

                    FunctionInformations[] functionInfos = DebugInfo.GetFunctionInformationsNested(codePointer);

                    Span<FunctionInformations> functionInfos2;
                    if (functionInfos.Length > 10)
                    { functionInfos2 = functionInfos.AsSpan(functionInfos.Length - 10, 10); }
                    else
                    { functionInfos2 = functionInfos.AsSpan(); }

                    for (int i = 0; i < 10; i++)
                    {
                        renderer.Text(0, line + i, new string(' ', width));
                        int fi = functionInfos2.Length - 1 - i;

                        if (fi < 0 || fi >= functionInfos2.Length)
                        { continue; }

                        int x = 0;

                        if (functionInfos2[fi].IsValid)
                        {
                            renderer.Text(0, line + i, functionInfos2[fi].ReadableIdentifier, ByteColor.White);
                            x += functionInfos2[fi].ReadableIdentifier.Length;
                        }
                        else
                        {
                            renderer.Text(0, line + i, "<unknown>", ByteColor.Gray);
                            x += "<unknown>".Length;
                        }

                        x++;

                        if (fi == 0)
                        { renderer.Text(x, line + i, "(current)", ByteColor.Gray); }
                    }
                    line += 10;
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

                    renderer[x, y] = new ConsoleChar(c, fg, bg);
                    if (x++ >= width) return;
                }

                if (Code[i].Count != 1)
                {
                    renderer.Text(ref x, y, Code[i].Count.ToString(), ByteColor.BrightYellow, bg);
                    if (x >= width) return;
                }
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
