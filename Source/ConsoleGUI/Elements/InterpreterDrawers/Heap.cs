using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void HeapElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (this.Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        Instruction? _instruction = Interpreter.NextInstruction;

        List<int> loadIndicators = new();
        List<int> storeIndicators = new();

        if (_instruction.HasValue)
        {
            Instruction instruction = _instruction.Value;

            if (instruction.Opcode == Opcode.HeapSet)
            {
                if (instruction.AddressingMode == AddressingMode.Runtime)
                { storeIndicators.Add(Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - BytecodeProcessor.StackDirection].VInt); }
                else
                { storeIndicators.Add((int)instruction.Parameter); }
            }

            if (instruction.Opcode == Opcode.HeapGet)
            {
                if (instruction.AddressingMode == AddressingMode.Runtime)
                {
                    if (this.Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - BytecodeProcessor.StackDirection].Type == RuntimeType.Integer)
                    { loadIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - BytecodeProcessor.StackDirection].VInt); }
                }
                else
                { loadIndicators.Add((int)instruction.Parameter); }
            }
        }

        int nextHeader = 0;
        for (int i = 0; i < this.Interpreter.BytecodeInterpreter.Memory.Length; i++)
        {
            DataItem item = this.Interpreter.BytecodeInterpreter.Memory[i];
            bool isHeader = (nextHeader == i) && (!this.Interpreter.BytecodeInterpreter.Memory[i].IsNull);
            (int, bool) header = (default, default);

            if (isHeader)
            {
                header = HeapUtils.GetHeader(item);
                nextHeader += header.Item1 + HeapUtils.HeaderSize;
            }

            if (i < HeapScrollBar.Offset) continue;

            bool addLoadIndicator = false;
            bool addStoreIndicator = false;

            for (int j = loadIndicators.Count - 1; j >= 0; j--)
            {
                if (loadIndicators[j] != i) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('○');
                b.ForegroundColor = CharColor.Silver;
                loadIndicators.RemoveAt(j);
                addLoadIndicator = true;
                break;
            }

            for (int j = storeIndicators.Count - 1; j >= 0; j--)
            {
                if (storeIndicators[j] != i) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('●');
                b.ForegroundColor = CharColor.Silver;
                storeIndicators.RemoveAt(j);
                addStoreIndicator = true;
                break;
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
                b.AddText(header.Item1.ToString(CultureInfo.InvariantCulture));
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
                if (item.IsNull)
                {
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText("<null>");
                }
                else
                {
                    switch (item.Type)
                    {
                        case RuntimeType.Byte:
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(item.VByte.ToString(CultureInfo.InvariantCulture));
                            break;
                        case RuntimeType.Integer:
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(item.VInt.ToString(CultureInfo.InvariantCulture));
                            break;
                        case RuntimeType.Single:
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(item.VSingle.ToString(CultureInfo.InvariantCulture));
                            b.AddText('f');
                            break;
                        case RuntimeType.Char:
                            b.ForegroundColor = CharColor.BrightYellow;
                            b.AddText('\'');
                            b.AddText(item.VChar.Escape());
                            b.AddText('\'');
                            break;
                        default:
                            b.ForegroundColor = CharColor.Silver;
                            b.AddText('?');
                            break;
                    }
                }
            }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        HeapScrollBar.Draw(b);
    }
}
