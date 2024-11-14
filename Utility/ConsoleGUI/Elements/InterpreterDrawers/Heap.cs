using Win32.Console;
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

        Instruction? _instruction = Interpreter.Processor.NextInstruction;

        List<DataMovement> loadIndicators = new();
        List<DataMovement> storeIndicators = new();

        if (_instruction.HasValue)
        { GetDataMovementIndicators(_instruction.Value, loadIndicators, storeIndicators); }

        int nextHeader = 0;
        for (int i = 0; i < Interpreter.Processor.Memory.Length; i++)
        {
            byte item = Interpreter.Processor.Memory[i];
            bool isHeader = (nextHeader == i) && (Interpreter.Processor.Memory[i] != 0);
            (int, bool) header = default;

            if (isHeader)
            {
                header = BytecodeHeapImplementation.GetHeader(Interpreter.Processor.Memory, i);
                nextHeader += header.Item1 + BytecodeHeapImplementation.HeaderSize;
            }

            if (i < HeapScrollBar.Offset) continue;

            bool addLoadIndicator = false;
            bool addStoreIndicator = false;

            for (int j = loadIndicators.Count - 1; j >= 0; j--)
            {
                if (loadIndicators[j].Address == i)
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = CharColor.Silver;
                    addLoadIndicator = true;
                    break;
                }
                else if (loadIndicators[j].Contains(i))
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('|');
                    b.ForegroundColor = CharColor.Silver;
                    addLoadIndicator = true;
                    break;
                }
            }

            for (int j = storeIndicators.Count - 1; j >= 0; j--)
            {
                if (storeIndicators[j].Address == i)
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('●');
                    b.ForegroundColor = CharColor.Silver;
                    addStoreIndicator = true;
                    break;
                }
                else if (storeIndicators[j].Contains(i))
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('|');
                    b.ForegroundColor = CharColor.Silver;
                    addStoreIndicator = true;
                    break;
                }
            }

            int space = ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString(CultureInfo.InvariantCulture).Length;
            b.AddText(' ', space);

            b.ForegroundColor = CharColor.Silver;
            b.AddText(i.ToString(CultureInfo.InvariantCulture));
            b.ForegroundColor = CharColor.White;
            b.AddSpace(5);

            if (isHeader)
            {
                b.BackgroundColor = CharColor.Gray;
                b.AddText("HEADER | ");
                b.AddText(header.Item1.ToString());
                b.AddText(" | ");
                if (header.Item2)
                {
                    b.BackgroundColor = CharColor.BrightYellow;
                    b.ForegroundColor = CharColor.Black;
                }
                else
                {
                    b.BackgroundColor = CharColor.BrightGreen;
                    b.ForegroundColor = CharColor.White;
                }
                b.AddText(header.Item2 ? "USED" : "FREE");
            }
            else
            {
                if (item == 0)
                {
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText('0');
                }
                else
                {
                    b.ForegroundColor = CharColor.BrightCyan;
                    b.AddText(item.ToString());
                }
            }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        HeapScrollBar.Draw(b);
    }
}
