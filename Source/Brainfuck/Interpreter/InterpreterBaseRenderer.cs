using System.Threading;
using Win32;
using Win32.Console;
using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

public partial class InterpreterBase
{
    protected struct RendererContext
    {
        public IOnlySetterRenderer<ConsoleChar>? Renderer;
        public Queue<byte>? InputBuffer;
        public string? OutputBuffer;
        public int CodeDisplayPosition;
    }
}

public partial class InterpreterBase<TCode> : IDisposable
{
    bool _isDisposed;
    protected RendererContext _rendererContext;

    [ExcludeFromCodeCoverage]
    void Dispose(bool _)
    {
        if (_isDisposed) return;
        _rendererContext.Renderer = null;
        ConsoleListener.Stop();
        _isDisposed = true;
    }
    [ExcludeFromCodeCoverage]
    ~InterpreterBase() { Dispose(false); }
    [ExcludeFromCodeCoverage]
    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [ExcludeFromCodeCoverage]
    public void RunWithUI(bool autoTick = true, int wait = 0)
    {
        SetupUI();

        Draw();

        Thread.Sleep(10);
        _rendererContext.InputBuffer!.Clear();

        if (!autoTick || _isPaused)
        {
            while (_rendererContext.InputBuffer.Count == 0)
            { Thread.Sleep(100); }
            _rendererContext.InputBuffer.Dequeue();
            _isPaused = false;
        }

        Console.Clear();

        int steps = 0;

        while (Step())
        {
            if (wait < 0 && wait + steps++ < 0 && !_isPaused)
            { continue; }
            steps = 0;

            Draw();

            if (!autoTick || _isPaused)
            {
                while (_rendererContext.InputBuffer.Count == 0)
                { Thread.Sleep(100); }
                _rendererContext.InputBuffer.Dequeue();
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

    [ExcludeFromCodeCoverage]
    void SetupUI()
    {
        _rendererContext.Renderer ??= new AnsiRenderer();
        // _rendererContext.Renderer ??= new ConsoleRenderer((short)Console.WindowWidth, (short)Console.WindowHeight);

        ConsoleListener.Start();

        if (_rendererContext.InputBuffer is null)
        {
            _rendererContext.InputBuffer = new Queue<byte>();
            ConsoleListener.KeyEvent += (e) =>
            {
                if (e.IsDown != 0)
                { _rendererContext.InputBuffer.Enqueue(e.AsciiChar); }
            };
            OnInput = () =>
            {
                while (_rendererContext.InputBuffer.Count == 0)
                { Thread.Sleep(100); }
                return _rendererContext.InputBuffer.Dequeue();
            };
        }

        if (_rendererContext.OutputBuffer is null)
        {
            _rendererContext.OutputBuffer = string.Empty;
            OnOutput = (v) => _rendererContext.OutputBuffer += CharCode.GetChar(v);
        }

        _rendererContext.CodeDisplayPosition = 0;
    }

    /// <exception cref="NullReferenceException"/>
    [ExcludeFromCodeCoverage]
    public void Draw()
    {
        if (_rendererContext.Renderer is null)
        { throw new NullReferenceException($"{nameof(_rendererContext.Renderer)} is null"); }
        if (_rendererContext.OutputBuffer is null)
        { throw new NullReferenceException($"{nameof(_rendererContext.OutputBuffer)} is null"); }
        _rendererContext.Renderer.RefreshBufferSize();

        int width = _rendererContext.Renderer.Width;
        int height = _rendererContext.Renderer.Height;

        int line = 0;

        {
            const int Margin = 40;
            _rendererContext.CodeDisplayPosition = Math.Clamp(_rendererContext.CodeDisplayPosition, _codePointer - Margin, _codePointer);

            // if (_rendererContext.CodeDisplayPosition + _codePointer < Margin)
            // { _rendererContext.CodeDisplayPosition = Margin + _codePointer; }
            // 
            // if (_rendererContext.CodeDisplayPosition + _codePointer > width - Margin)
            // { _rendererContext.CodeDisplayPosition = Margin + _codePointer; }

            // int halfWidth = width / 2;
            // int center = _codePointer;
            // _rendererContext.CodeDisplayPosition = Math.Clamp(_rendererContext.CodeDisplayPosition, center - 20, center + 20);
            int codePrintStart = Math.Max(0, _rendererContext.CodeDisplayPosition);
            int codePrintEnd = Math.Min(Code.Length - 1, _rendererContext.CodeDisplayPosition + width - 1);
            DrawCode(_rendererContext.Renderer, new Range<int>(codePrintStart, codePrintEnd), 0, line++, width);
        }

        _rendererContext.Renderer.Text(0, line++, new string('─', width), CharColor.Gray);

        _rendererContext.Renderer.Text(1, line - 1, "|Memory|", CharColor.Gray);

        SmallRect memoryRect = new(0, line, width, 5 * 4);
        DrawMemory(_rendererContext.Renderer, (0, Memory.Length - 1), memoryRect);
        line += memoryRect.Height;

        _rendererContext.Renderer.Text(0, line++, $"Memory Pointer Range: {_memoryPointerRange}", CharColor.Gray);

        _rendererContext.Renderer.Text(0, line++, new string('─', width), CharColor.Gray);
        _rendererContext.Renderer.Text(1, line - 1, "|Original|", CharColor.Gray);

        SmallRect originalCodeRect = new(0, line, width, 15);
        DrawOriginalCode(_rendererContext.Renderer, originalCodeRect);
        height -= originalCodeRect.Height;
        line += originalCodeRect.Height;

        _rendererContext.Renderer.Text(0, line++, new string('─', width), CharColor.Gray);
        _rendererContext.Renderer.Text(1, line - 1, "|Output|", CharColor.Gray);
        SmallRect outputRect = new(0, line, width, height);
        DrawOutput(_rendererContext.Renderer, _rendererContext.OutputBuffer, outputRect);
        // line += outputRect.Height;

        /*
        SmallRect stackTraceRect = new(0, line, width, 10);
        DrawStackTrace(_rendererContext.Renderer, stackTraceRect);
        line += stackTraceRect.Height;
        */

        _rendererContext.Renderer.Render();
    }

    int StartToken;
    [ExcludeFromCodeCoverage]
    void DrawOriginalCode(IOnlySetterRenderer<ConsoleChar> renderer, SmallRect rect)
    {
        renderer.Fill(rect, default);
        rect.Top = Math.Max(rect.Top, (short)0);
        rect.Left = Math.Max(rect.Left, (short)0);
        rect.Bottom = Math.Min(rect.Bottom, (short)(renderer.Height - 1));
        rect.Right = Math.Min(rect.Right, (short)(renderer.Width - 1));

        if (DebugInfo == null) return;

        if (!DebugInfo.TryGetSourceLocation(_codePointer, out SourceCodeLocation sourceLocation)) return;

        if (sourceLocation.Uri == null)
        { return; }

        if (!DebugInfo.OriginalFiles.TryGetValue(sourceLocation.Uri, out ImmutableArray<Tokenizing.Token> originalCode))
        { return; }

        int _startToken = -1;
        for (int i = 0; i < originalCode.Length; i++)
        {
            Tokenizing.Token token = originalCode[i];

            if (!sourceLocation.SourcePosition.AbsoluteRange.Contains(token.Position.AbsoluteRange.Start))
            { continue; }
            if (!sourceLocation.SourcePosition.AbsoluteRange.Contains(token.Position.AbsoluteRange.End))
            { continue; }

            _startToken = i;
            break;
        }

        if (_startToken != -1)
        { StartToken = Math.Max(0, _startToken - 30); }

        int startLine = originalCode[StartToken].Position.Range.Start.Line;

        while (StartToken > 0 && originalCode[StartToken - 1].Position.Range.Start.Line == startLine)
        {
            StartToken--;
        }

        bool prevWasInside = false;

        for (int i = StartToken; i < originalCode.Length; i++)
        {
            Tokenizing.Token token = originalCode[i];
            Tokenizing.Token? prevToken = null;
            if (prevWasInside)
            { prevToken = originalCode[i - 1]; }
            prevWasInside = false;

            int currentX = token.Position.Range.Start.Character + rect.X;
            int currentY = token.Position.Range.Start.Line - startLine + rect.Y;

            if (currentX < 0 ||
                currentY < 0 ||
                currentX >= rect.Right ||
                currentY >= rect.Bottom)
            { return; }

            bool isInside = RangeUtils.Inside(sourceLocation.SourcePosition.Range, token.Position.Range);

            if (isInside &&
                prevToken != null &&
                prevToken.Position.Range.End.Line == token.Position.Range.Start.Line)
            {
                int from = prevToken.Position.Range.Start.Character + rect.X;
                for (int offset = from; offset < currentX; offset++)
                {
                    // renderer[offset, currentY].Background = CharColor.Gray;
                }
            }

            if (isInside)
            { prevWasInside = true; }

            byte foregroundColor = token.AnalyzedType switch
            {
                Tokenizing.TokenAnalyzedType.Attribute => CharColor.BrightGreen,
                Tokenizing.TokenAnalyzedType.Type => CharColor.BrightGreen,
                Tokenizing.TokenAnalyzedType.Struct => CharColor.BrightGreen,
                Tokenizing.TokenAnalyzedType.BuiltinType => CharColor.BrightBlue,
                Tokenizing.TokenAnalyzedType.Keyword => CharColor.BrightBlue,
                Tokenizing.TokenAnalyzedType.FunctionName => CharColor.BrightYellow,
                Tokenizing.TokenAnalyzedType.Statement => CharColor.BrightMagenta,
                Tokenizing.TokenAnalyzedType.VariableName => CharColor.White,
                Tokenizing.TokenAnalyzedType.ParameterName => CharColor.White,
                Tokenizing.TokenAnalyzedType.FieldName => CharColor.White,
                _ => token.TokenType switch
                {
                    Tokenizing.TokenType.LiteralNumber => CharColor.BrightCyan,
                    Tokenizing.TokenType.LiteralHex => CharColor.BrightCyan,
                    Tokenizing.TokenType.LiteralBinary => CharColor.BrightCyan,
                    Tokenizing.TokenType.LiteralFloat => CharColor.BrightCyan,
                    Tokenizing.TokenType.Identifier => CharColor.White,
                    _ => CharColor.Silver,
                },
            };
            byte backgroundColor = isInside ? CharColor.Gray : CharColor.Black;

            string text = token.ToOriginalString();
            for (int offset = 0; offset < text.Length; offset++)
            {
                if (currentX + offset >= rect.Right) return;

                renderer.Set(currentX + offset, currentY, new ConsoleChar(text[offset], foregroundColor, backgroundColor));
            }
        }
    }

    [ExcludeFromCodeCoverage]
    protected abstract void DrawCode(IOnlySetterRenderer<ConsoleChar> renderer, Range<int> range, int x, int y, int width);

    [ExcludeFromCodeCoverage]
    void DrawMemory(IOnlySetterRenderer<ConsoleChar> renderer, Range<int> range, SmallRect rect)
    {
        renderer.Fill(rect, default);

        int x = rect.Left;
        int y = rect.Top;

        int heapStart = Generator.BrainfuckGeneratorSettings.Default.HeapStart;
        // int heapEnd = heapStart + (Generator.BrainfuckGeneratorSettings.Default.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE);

        for (int i = range.Start; i <= range.End; i++)
        {
            int _x = x;
            int _y = y;

            if (_y + 3 >= rect.Bottom)
            { break; }

            { // Character
                char chr = CharCode.GetChar(Memory[i]);
                if (Memory[i] is < 32 or > 126)
                { chr = ' '; }

                string textToPrint = chr.ToString().PadRight(4, ' ');

                renderer.Text(_x, _y, textToPrint, CharColor.Silver);
                _y++;
            }

            { // Data
                string textToPrint = Memory[i].ToString(CultureInfo.InvariantCulture).PadRight(4, ' ');

                if (_memoryPointer == i)
                { renderer.Text(_x, _y, textToPrint, CharColor.BrightRed); }
                else if (Memory[i] == 0)
                { renderer.Text(_x, _y, textToPrint, CharColor.Gray); }
                else
                { renderer.Text(_x, _y, textToPrint, CharColor.White); }
                _y++;
            }

            { // Pointer
                byte fg = CharColor.White;
                byte bg = CharColor.Black;

                if (_memoryPointer == i)
                { fg = CharColor.BrightRed; }
                if (_memoryPointerRange.Contains(i))
                { bg = CharColor.Gray; }

                if (_memoryPointer == i)
                { renderer.Text(_x, _y, "^   ", fg, bg); }
                else
                { renderer.Text(_x, _y, "    ", fg, bg); }
                _y++;
            }

            { // Address
                // int j = (i - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE;
                int k = (i - heapStart) % HeapCodeHelper.BlockSize;

                bool showAddress = (i & 0b_1111) == 0;
                showAddress = showAddress || i == heapStart;

                bool showBackground = showAddress;
                showBackground = showBackground || (i > heapStart + 2 && k == HeapCodeHelper.DataOffset);

                if (showAddress || showBackground)
                {
                    byte fg = CharColor.Gray;
                    byte bg = CharColor.Black;

                    if (i == heapStart)
                    { bg = CharColor.Blue; }

                    if (i > heapStart + 2 &&
                        k == HeapCodeHelper.DataOffset)
                    {
                        bg = CharColor.Green;
                        fg = CharColor.White;
                    }

                    if (!showAddress)
                    { renderer.Text(_x, _y, $"    ", fg, bg); }
                    else
                    { renderer.Text(_x, _y, $"{i,-4}", fg, bg); }

                    // _y++;
                }
            }

            x += 4;
            if (x >= rect.Right)
            {
                x = rect.Left;
                y += 4;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    static void DrawOutput(IOnlySetterRenderer<ConsoleChar> renderer, string text, SmallRect rect)
    {
        int x = rect.X;
        int y = rect.Y;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                x = rect.X;
                y++;
                if (y >= rect.Height) break;
                else continue;
            }
            else if (text[i] == '\r')
            { }
            else if (text[i] == '\t')
            { renderer.Set(x, y, new ConsoleChar(' ', CharColor.White, CharColor.Black)); }
            else if (text[i] is < (char)32 or > (char)127)
            { renderer.Set(x, y, new ConsoleChar(' ', CharColor.White, CharColor.Black)); }
            else
            { renderer.Set(x, y, new ConsoleChar(text[i], CharColor.White, CharColor.Black)); }

            x++;
            if (x >= rect.Width)
            {
                x = rect.X;
                y++;
                if (y >= rect.Height) break;
            }
        }
    }

    [ExcludeFromCodeCoverage]
    void DrawStackTrace(IOnlySetterRenderer<ConsoleChar> renderer, SmallRect rect)
    {
        if (DebugInfo == null)
        { return; }

        renderer.Text(0, rect.Y, new string('─', rect.Width), CharColor.Gray);
        renderer.Text(1, rect.Y, "|Stacktrace|", CharColor.Gray);

        ImmutableArray<FunctionInformation> functionInfos = DebugInfo.GetFunctionInformationNested(_codePointer);

        ReadOnlySpan<FunctionInformation> functionInfos2;
        if (functionInfos.Length > 10)
        { functionInfos2 = functionInfos.AsSpan(functionInfos.Length - 10, 10); }
        else
        { functionInfos2 = functionInfos.AsSpan(); }

        for (int i = 0; i < rect.Height; i++)
        {
            renderer.Text(0, rect.Y + 1 + i, new string(' ', rect.Width));
            int fi = functionInfos2.Length - 1 - i;

            if (fi < 0 || fi >= functionInfos2.Length)
            { continue; }

            int x = 0;

            if (functionInfos2[fi].IsValid)
            {
                renderer.Text(0, rect.Y + 1 + i, functionInfos2[fi].ReadableIdentifier, CharColor.White);
                x += functionInfos2[fi].ReadableIdentifier!.Length;
            }
            else
            {
                renderer.Text(0, rect.Y + 1 + i, "<unknown>", CharColor.Gray);
                x += "<unknown>".Length;
            }

            x++;

            if (fi == 0)
            { renderer.Text(x, rect.Y + 1 + i, "(current)", CharColor.Gray); }
        }
    }
}
