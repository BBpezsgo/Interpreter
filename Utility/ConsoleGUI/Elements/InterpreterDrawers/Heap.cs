using LanguageCore.Runtime;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void HeapElement_OnBeforeDraw(InlineElement sender)
    {
        bool focused = _focusedElement == 3;
        sender.IsFocused = focused;

        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        Instruction? _instruction = Interpreter.NextInstruction;

        List<DataMovement> loadIndicators = new();
        List<DataMovement> storeIndicators = new();

        if (_instruction.HasValue)
        { GetDataMovementIndicators(_instruction.Value, loadIndicators, storeIndicators); }

        int nextHeader = 0;
        for (int i = 0; i < Interpreter.Memory.Length; i++)
        {
            byte item = Interpreter.Memory[i];
            bool isHeader = (nextHeader == i) && (Interpreter.Memory[i] != 0);
            (int, bool) header = default;

            if (isHeader)
            {
                header = BytecodeHeapImplementation.GetHeader(Interpreter.Memory, i);
                nextHeader += header.Item1 + BytecodeHeapImplementation.HeaderSize;
            }

            if (i < HeapScrollBar.Offset) continue;

            bool addLoadIndicator = false;
            bool addStoreIndicator = false;

            for (int j = loadIndicators.Count - 1; j >= 0; j--)
            {
                if (loadIndicators[j].Address == i)
                {
                    b.ForegroundColor = CLI.AnsiColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = CLI.AnsiColor.Silver;
                    addLoadIndicator = true;
                    break;
                }
                else if (loadIndicators[j].Contains(i))
                {
                    b.ForegroundColor = CLI.AnsiColor.BrightRed;
                    b.AddText('|');
                    b.ForegroundColor = CLI.AnsiColor.Silver;
                    addLoadIndicator = true;
                    break;
                }
            }

            for (int j = storeIndicators.Count - 1; j >= 0; j--)
            {
                if (storeIndicators[j].Address == i)
                {
                    b.ForegroundColor = CLI.AnsiColor.BrightRed;
                    b.AddText('●');
                    b.ForegroundColor = CLI.AnsiColor.Silver;
                    addStoreIndicator = true;
                    break;
                }
                else if (storeIndicators[j].Contains(i))
                {
                    b.ForegroundColor = CLI.AnsiColor.BrightRed;
                    b.AddText('|');
                    b.ForegroundColor = CLI.AnsiColor.Silver;
                    addStoreIndicator = true;
                    break;
                }
            }

            int space = ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString(CultureInfo.InvariantCulture).Length;
            b.AddText(' ', space);

            b.ForegroundColor = CLI.AnsiColor.Silver;
            b.AddText(i.ToString(CultureInfo.InvariantCulture));
            b.ForegroundColor = CLI.AnsiColor.White;
            b.AddSpace(5);

            if (isHeader)
            {
                b.BackgroundColor = CLI.AnsiColor.Gray;
                b.AddText("HEADER | ");
                b.AddText(header.Item1.ToString());
                b.AddText(" | ");
                if (header.Item2)
                {
                    b.BackgroundColor = CLI.AnsiColor.BrightYellow;
                    b.ForegroundColor = CLI.AnsiColor.Black;
                }
                else
                {
                    b.BackgroundColor = CLI.AnsiColor.BrightGreen;
                    b.ForegroundColor = CLI.AnsiColor.White;
                }
                b.AddText(header.Item2 ? "USED" : "FREE");
            }
            else
            {
                if (item == 0)
                {
                    b.ForegroundColor = CLI.AnsiColor.Gray;
                    b.AddText('0');
                }
                else
                {
                    b.ForegroundColor = CLI.AnsiColor.BrightCyan;
                    b.AddText(item.ToString());
                }
            }

            b.BackgroundColor = CLI.AnsiColor.Black;
            b.FinishLine();
            b.ForegroundColor = CLI.AnsiColor.Silver;
        }

        HeapScrollBar.Draw(b);
    }
}
