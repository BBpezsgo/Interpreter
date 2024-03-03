using System.IO;
using System.Runtime.Versioning;
using Win32;
using Thread = System.Threading.Thread;

namespace LanguageCore.Brainfuck;

using Runtime;

public class Interpreter : InterpreterBase<byte>
{
    public DebugInformation? DebugInfo;
    public Tokenizing.Token[]? OriginalCode;

    public Interpreter(Uri uri, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(uri, onOutput, onInput) { }

    public Interpreter(FileInfo file, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(file, onOutput, onInput) { }

    public Interpreter(string code, OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(code, onOutput, onInput) { }

    protected override byte[] ParseCode(string code)
    {
        List<char> Code = new(code.Length);
        for (int i = 0; i < code.Length; i++)
        {
            if (!BrainfuckCode.CodeCharacters.Contains(code[i]))
            { continue; }

            Code.Add(code[i]);
        }
        return CompactCode.ToOpCode(Code.ToArray());
    }

    public static void Run(string code)
    {
        InterpreterBase<byte> interpreter = new Interpreter(code);
        interpreter.Run();
    }

    public static void Run(string code, int limit)
    {
        InterpreterBase<byte> interpreter = new Interpreter(code);
        interpreter.Run(limit);
    }

    /// <exception cref="BrainfuckRuntimeException"/>
    protected override void Evaluate(byte instruction)
    {
        switch (instruction)
        {
            case OpCodes.ADD:
                Memory[_memoryPointer]++;
                break;
            case OpCodes.SUB:
                Memory[_memoryPointer]--;
                break;
            case OpCodes.POINTER_R:
                if (_memoryPointer++ >= Memory.Length)
                { throw new BrainfuckRuntimeException($"Memory overflow", CurrentContext); }
                break;
            case OpCodes.POINTER_L:
                if (_memoryPointer-- <= 0)
                { throw new BrainfuckRuntimeException($"Memory underflow", CurrentContext); }
                break;
            case OpCodes.BRANCH_START:
                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BRANCH_END)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BRANCH_START)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unclosed bracket", CurrentContext);
                }
                break;
            case OpCodes.BRANCH_END:
                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BRANCH_START)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BRANCH_END)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unexpected closing bracket", CurrentContext);
                }
                break;
            case OpCodes.OUT:
                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            case OpCodes.IN:
                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            default:
                throw new BrainfuckRuntimeException($"Unknown instruction {Code[_codePointer]}", CurrentContext);
        }
    }

    [SupportedOSPlatform("windows")]
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

            int center = _codePointer - halfWidth;
            lastCodePosition = Math.Clamp(lastCodePosition, center - 20, center + 20);
            int codePrintStart = Math.Max(0, lastCodePosition);
            int codePrintEnd = Math.Min(Code.Length - 1, lastCodePosition + width - 1);
            DrawCode(renderer, codePrintStart, codePrintEnd, 0, line++, width);

            int memoryPrintStart = Math.Max(0, _memoryPointer - halfWidth);
            int memoryPrintEnd = Math.Min(Memory.Length - 1, _memoryPointer + (halfWidth - 1));
            DrawMemoryChars(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
            DrawMemoryRaw(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
            DrawMemoryPointer(renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);

            renderer.Text(0, line++, new string('─', width), CharColor.Gray);

            DrawOriginalCode(renderer, 0, line, width, 15);
            height -= 15;
            line += 15;

            renderer.Text(0, line++, new string('─', width), CharColor.Gray);

            DrawOutput(renderer, outputBuffer, 0, line++, width, height);

            renderer.Text(0, line++, new string('─', width), CharColor.Gray);

            renderer.Text(0, line, new string(' ', width));

            if (DebugInfo != null)
            {
                FunctionInformations functionInfo = DebugInfo.GetFunctionInformations(_codePointer);
                if (functionInfo.IsValid)
                { renderer.Text(0, line++, functionInfo.ReadableIdentifier, CharColor.White); }
            }

            renderer.Render();
        }

        Draw();

        Thread.Sleep(100);
        inputBuffer.Clear();

        if (!autoTick || _isPaused)
        {
            while (inputBuffer.Count == 0)
            { Thread.Sleep(100); }
            inputBuffer.Dequeue();
            _isPaused = false;
        }

        while (Step())
        {
            Draw();

            if (!autoTick || _isPaused)
            {
                while (inputBuffer.Count == 0)
                { Thread.Sleep(100); }
                inputBuffer.Dequeue();
                _isPaused = false;
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
    [SupportedOSPlatform("windows")]
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

        if (!DebugInfo.TryGetSourceLocation(_codePointer, out SourceCodeLocation sourceLocation)) return;

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

                byte foregroundColor = CharColor.Silver;

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

                byte backgroundColor = CharColor.Black;
                if (sourceLocation.SourcePosition.Range.Contains(token.Position.Range.Start))
                { backgroundColor = CharColor.Gray; }

                renderer[currentX + offset - 1, currentY] = new ConsoleChar(text[offset], foregroundColor, backgroundColor);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    void DrawCode(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
    {
        for (int i = start; i <= end; i++)
        {
            byte bg = (i == _codePointer) ? CharColor.Silver : CharColor.Black;
            byte fg = CompactCode.FromOpCode(Code[i]) switch
            {
                '>' or '<' => CharColor.BrightRed,
                '+' or '-' => CharColor.BrightBlue,
                '[' or ']' => CharColor.BrightGreen,
                '.' or ',' => CharColor.BrightMagenta,
                _ => CharColor.Silver,
            };
            renderer[x, y] = new ConsoleChar(CompactCode.FromOpCode(Code[i]), fg, bg);

            if (x++ >= width)
            { return; }
        }

        while (x < width)
        {
            renderer[x, y] = new ConsoleChar(' ');
            x++;
        }
    }

    [SupportedOSPlatform("windows")]
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
            renderer.Text(x, y, textToPrint, CharColor.Silver);
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

    [SupportedOSPlatform("windows")]
    void DrawMemoryRaw(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
    {
        for (int m = start; m <= end; m++)
        {
            string textToPrint = Memory[m].ToString(CultureInfo.InvariantCulture).PadRight(4, ' ');

            if (_memoryPointer == m)
            { renderer.Text(x, y, textToPrint, CharColor.BrightRed); }
            else if (Memory[m] == 0)
            { renderer.Text(x, y, textToPrint, CharColor.Silver); }
            else
            { renderer.Text(x, y, textToPrint, CharColor.White); }

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

    [SupportedOSPlatform("windows")]
    void DrawMemoryPointer(ConsoleRenderer renderer, int start, int end, int x, int y, int width)
    {
        for (int m = start; m <= end; m++)
        {
            if (_memoryPointer == m)
            { renderer.Text(x, y, "^   ", CharColor.BrightRed); }
            else
            { renderer.Text(x, y, "    ", CharColor.White); }

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

    [SupportedOSPlatform("windows")]
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
            { }
            else if (text[i] == '\t')
            { renderer[_x, _y] = new ConsoleChar(' ', CharColor.White, CharColor.Black); }
            else if (text[i] < 32 || text[i] > 127)
            { renderer[_x, _y] = new ConsoleChar(' ', CharColor.White, CharColor.Black); }
            else
            { renderer[_x, _y] = new ConsoleChar(text[i], CharColor.White, CharColor.Black); }

            _x++;
            if (_x >= width)
            {
                _x = x;
                _y++;
                if (_y >= height) break;
            }
        }
    }
}
