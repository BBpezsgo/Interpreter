using Win32;
using Win32.Console;

namespace LanguageCore.Brainfuck;

public partial class Interpreter
{
    /*
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
    */

    
    protected override void DrawCode(IOnlySetterRenderer<ConsoleChar> renderer, Range<int> range, int x, int y, int width)
    {
        for (int i = range.Start; i <= range.End; i++)
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
            renderer.Set(x, y, new ConsoleChar(CompactCode.FromOpCode(Code[i]), fg, bg));

            if (x++ >= width)
            { return; }
        }

        while (x < width)
        {
            renderer.Set(x, y, new ConsoleChar(' '));
            x++;
        }
    }
}
