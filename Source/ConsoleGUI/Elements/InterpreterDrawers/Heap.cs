using Win32.Console;

namespace ConsoleGUI;

using LanguageCore.Runtime;

public partial class InterpreterElement
{
    void HeapElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        Instruction? _instruction = Interpreter.NextInstruction;

        List<DataMovement> loadIndicators = new();
        List<DataMovement> storeIndicators = new();

        if (_instruction.HasValue)
        { GetDataMovementIndicators(_instruction.Value, loadIndicators, storeIndicators); }

        int nextHeader = 0;
        for (int i = 0; i < Interpreter.BytecodeInterpreter.Memory.Length; i++)
        {
            RuntimeValue item = Interpreter.BytecodeInterpreter.Memory[i];
            bool isHeader = (nextHeader == i) && (Interpreter.BytecodeInterpreter.Memory[i] != 0);
            (int, bool) header = (default, default);

            if (isHeader)
            {
                header = HeapImplementation.GetHeader(item);
                nextHeader += header.Item1 + HeapImplementation.HeaderSize;
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
                    b.AddText(item.Int.ToString());
                }
            }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        HeapScrollBar.Draw(b);
    }
}
