using System.IO;
using System.Runtime.Versioning;
using Win32;
using Thread = System.Threading.Thread;

namespace LanguageCore.Brainfuck;

using Runtime;

public class InterpreterCompact : InterpreterBase<CompactCodeSegment>
{
    public DebugInformation? DebugInfo;
    public Tokenizing.Token[]? OriginalCode;

    ConsoleRenderer? _renderer;
    Queue<byte>? inputBuffer;
    string? outputBuffer;
    int _lastCodePosition = 0;

    public InterpreterCompact(Uri url, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        : base(url, OnOutput, OnInput) { }

    public InterpreterCompact(FileInfo file, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        : base(file, OnOutput, OnInput) { }

    public InterpreterCompact(string code, OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        : base(code, OnOutput, OnInput) { }

    public InterpreterCompact(OutputCallback? OnOutput = null, InputCallback? OnInput = null)
        : base(OnOutput, OnInput) { }

    protected override CompactCodeSegment[] ParseCode(string code)
    {
        List<char> Code = new(code.Length);
        for (int i = 0; i < code.Length; i++)
        {
            if (!BrainfuckCode.CodeCharacters.Contains(code[i]))
            { continue; }

            Code.Add(code[i]);
        }
        return CompactCode.Generate(Code.ToArray());
    }

    public static void Run(string code) => new InterpreterCompact(code).Run();
    public static void Run(string code, int limit) => new InterpreterCompact(code).Run(limit);

    /// <exception cref="BrainfuckRuntimeException"/>
    /// <exception cref="NotImplementedException"/>
    protected override void Evaluate(CompactCodeSegment instruction)
    {
        switch (instruction.OpCode)
        {
            case OpCodesCompact.CLEAR:
                Memory[_memoryPointer] = 0;
                break;
            case OpCodes.ADD:
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] + instruction.Count);
                break;
            case OpCodes.SUB:
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] - instruction.Count);
                break;
            case OpCodes.POINTER_R:
                _memoryPointer += instruction.Count;
                _memoryPointerRange = Range.Union(_memoryPointerRange, _memoryPointer);
                if (_memoryPointer >= Memory.Length)
                { throw new BrainfuckRuntimeException($"Memory overflow", CurrentContext); }
                break;
            case OpCodes.POINTER_L:
                _memoryPointer -= instruction.Count;
                _memoryPointerRange = Range.Union(_memoryPointerRange, _memoryPointer);
                if (_memoryPointer < 0)
                { throw new BrainfuckRuntimeException($"Memory underflow", CurrentContext); }
                break;
            case OpCodes.BRANCH_START:
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodes.BRANCH_END)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodes.BRANCH_START)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unclosed bracket", CurrentContext);
                }
                break;
            case OpCodes.BRANCH_END:
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodes.BRANCH_START)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodes.BRANCH_END)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unexpected closing bracket", CurrentContext);
                }
                break;
            case OpCodes.OUT:
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            case OpCodes.IN:
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            default:
                throw new BrainfuckRuntimeException($"Unknown instruction {instruction}", CurrentContext);
        }
    }

    [SupportedOSPlatform("windows")]
    public void RunWithUI(bool autoTick = true, int wait = 0)
    {
        SetupUI();

        Draw();

        Thread.Sleep(10);
        inputBuffer!.Clear();

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
    }

    [SupportedOSPlatform("windows")]
    public void SetupUI()
    {
        short width = ConsoleHandler.WindowWidth;
        short height = ConsoleHandler.WindowHeight;

        _renderer ??= new ConsoleRenderer(width, height);
        ConsoleListener.Start();

        if (inputBuffer is null)
        {
            inputBuffer = new Queue<byte>();
            ConsoleListener.KeyEvent += (e) =>
            {
                if (e.IsDown != 0)
                { inputBuffer.Enqueue(e.AsciiChar); }
            };
            OnInput = () =>
            {
                while (inputBuffer.Count == 0)
                { Thread.Sleep(100); }
                return inputBuffer.Dequeue();
            };
        }

        if (outputBuffer is null)
        {
            outputBuffer = string.Empty;
            OnOutput = (v) => outputBuffer += CharCode.GetChar(v);
        }

        _lastCodePosition = 0;
    }

    /// <exception cref="NullReferenceException"/>
    [SupportedOSPlatform("windows")]
    public void Draw()
    {
        if (_renderer is null)
        { throw new NullReferenceException($"{nameof(_renderer)} is null"); }
        if (outputBuffer is null)
        { throw new NullReferenceException($"{nameof(outputBuffer)} is null"); }

        int width = _renderer.Width;
        int height = _renderer.Height;
        int halfWidth = width / 2;

        int line = 0;

        int center = _codePointer - halfWidth;
        _lastCodePosition = Math.Clamp(_lastCodePosition, center - 20, center + 20);
        int codePrintStart = Math.Max(0, _lastCodePosition);
        int codePrintEnd = Math.Min(Code.Length - 1, _lastCodePosition + width - 1);
        DrawCode(_renderer, codePrintStart, codePrintEnd, 0, line++, width);

        int memoryPrintStart = Math.Max(0, _memoryPointer - halfWidth);
        int memoryPrintEnd = Math.Min(Memory.Length - 1, _memoryPointer + (halfWidth - 1));
        DrawMemoryChars(_renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
        DrawMemoryRaw(_renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);
        DrawMemoryPointer(_renderer, memoryPrintStart, memoryPrintEnd, 0, line++, width);

        _renderer.Text(0, line++, $"Memory Pointer Range: {_memoryPointerRange}", CharColor.Gray);

        _renderer.Text(0, line++, new string('─', width), CharColor.Gray);

        DrawOriginalCode(_renderer, 0, line, width, 15);
        height -= 15;
        line += 15;

        _renderer.Text(0, line++, new string('─', width), CharColor.Gray);

        DrawOutput(_renderer, outputBuffer, 0, line++, width, height);

        if (DebugInfo != null)
        {
            _renderer.Text(0, line++, new string('─', width), CharColor.Gray);

            FunctionInformations[] functionInfos = DebugInfo.GetFunctionInformationsNested(_codePointer);

            Span<FunctionInformations> functionInfos2;
            if (functionInfos.Length > 10)
            { functionInfos2 = functionInfos.AsSpan(functionInfos.Length - 10, 10); }
            else
            { functionInfos2 = functionInfos.AsSpan(); }

            for (int i = 0; i < 10; i++)
            {
                _renderer.Text(0, line + i, new string(' ', width));
                int fi = functionInfos2.Length - 1 - i;

                if (fi < 0 || fi >= functionInfos2.Length)
                { continue; }

                int x = 0;

                if (functionInfos2[fi].IsValid)
                {
                    _renderer.Text(0, line + i, functionInfos2[fi].ReadableIdentifier, CharColor.White);
                    x += functionInfos2[fi].ReadableIdentifier.Length;
                }
                else
                {
                    _renderer.Text(0, line + i, "<unknown>", CharColor.Gray);
                    x += "<unknown>".Length;
                }

                x++;

                if (fi == 0)
                { _renderer.Text(x, line + i, "(current)", CharColor.Gray); }
            }
            // line += 10;
        }

        _renderer.Render();
    }

    protected override void DisposeManaged()
    {
        _renderer = null;
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
                OpCodesCompact.CLEAR => "[-]",
                _ => string.Empty,
            };

            for (int x2 = 0; x2 < code.Length; x2++)
            {
                char c = code[x2];

                byte fg = c switch
                {
                    '>' or '<' => CharColor.BrightRed,
                    '+' or '-' => CharColor.BrightBlue,
                    '[' or ']' => CharColor.BrightGreen,
                    '.' or ',' => CharColor.BrightMagenta,
                    _ => CharColor.Silver,
                };

                renderer[x, y] = new ConsoleChar(c, fg, bg);
                if (x++ >= width) return;
            }

            if (Code[i].Count != 1)
            {
                renderer.Text(ref x, y, Code[i].Count.ToString(CultureInfo.InvariantCulture), CharColor.BrightYellow, bg);
                if (x >= width) return;
            }
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
            byte fg = CharColor.White;
            byte bg = CharColor.Black;

            if (_memoryPointer == m)
            { fg = CharColor.BrightRed; }
            if (_memoryPointerRange.Contains(m))
            { bg = CharColor.Gray; }

            if (_memoryPointer == m)
            { renderer.Text(x, y, "^   ", fg, bg); }
            else
            { renderer.Text(x, y, "    ", fg, bg); }

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
            {

            }
            else if (text[i] == '\t')
            {
                renderer[_x, _y] = new ConsoleChar(' ', CharColor.White, CharColor.Black);
            }
            else if (text[i] < 32 || text[i] > 127)
            {
                renderer[_x, _y] = new ConsoleChar(' ', CharColor.White, CharColor.Black);
            }
            else
            {
                renderer[_x, _y] = new ConsoleChar(text[i], CharColor.White, CharColor.Black);
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
}
