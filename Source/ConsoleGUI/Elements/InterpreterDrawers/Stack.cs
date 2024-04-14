using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void GetDataMovementIndicators(Instruction instruction, List<int> loadIndicators, List<int> storeIndicators)
    {
        if (instruction.Opcode == Opcode.StackStore)
        {
            storeIndicators.Add(Interpreter.BytecodeInterpreter.GetAddress(instruction));
        }

        if (instruction.Opcode == Opcode.StackStore ||
            instruction.Opcode == Opcode.HeapSet)
        {
            if (instruction.AddressingMode == AddressingMode.Runtime)
            { loadIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer - (2 * BytecodeProcessor.StackDirection)); }
            else
            { loadIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer - (1 * BytecodeProcessor.StackDirection)); }
        }

        if (instruction.Opcode == Opcode.StackLoad)
        {
            loadIndicators.Add(Interpreter.BytecodeInterpreter.GetAddress(instruction));
            storeIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer);
        }

        if (instruction.Opcode == Opcode.Push ||
            instruction.Opcode == Opcode.GetBasePointer ||
            instruction.Opcode == Opcode.HeapGet)
        { storeIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer); }

        if (instruction.Opcode == Opcode.Pop)
        { loadIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer - BytecodeProcessor.StackDirection); }

        if (instruction.Opcode == Opcode.MathAdd ||
            instruction.Opcode == Opcode.MathDiv ||
            instruction.Opcode == Opcode.MathMod ||
            instruction.Opcode == Opcode.MathMult ||
            instruction.Opcode == Opcode.MathSub ||
            instruction.Opcode == Opcode.BitsAND ||
            instruction.Opcode == Opcode.BitsOR ||
            instruction.Opcode == Opcode.LogicAND ||
            instruction.Opcode == Opcode.LogicOR)
        {
            loadIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer - (1 * BytecodeProcessor.StackDirection));
            storeIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer - (2 * BytecodeProcessor.StackDirection));
        }
    }

    void StackElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        CollectedScopeInfo stackDebugInfo;
        if (Interpreter.DebugInformation is not null)
        { stackDebugInfo = Interpreter.DebugInformation.GetScopeInformations(Interpreter.BytecodeInterpreter.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        ImmutableArray<int> savedBasePointers = BytecodeProcessor.TraceBasePointers(Interpreter.BytecodeInterpreter.Memory, Interpreter.BytecodeInterpreter.Registers.BasePointer);

        List<int> loadIndicators = new();
        List<int> storeIndicators = new();

        if (Interpreter.NextInstruction.HasValue)
        { GetDataMovementIndicators(Interpreter.NextInstruction.Value, loadIndicators, storeIndicators); }

        void DrawElement(int address, DataItem item)
        {
            if (Interpreter.BytecodeInterpreter.Registers.BasePointer == address)
            {
                b.ForegroundColor = CharColor.BrightBlue;
                b.AddText('►');
                b.ForegroundColor = CharColor.Silver;
            }
            else if (savedBasePointers.Contains(address))
            {
                b.ForegroundColor = CharColor.Silver;
                b.AddText('►');
                b.ForegroundColor = CharColor.Silver;
            }
            else
            {
                b.ForegroundColor = CharColor.Silver;
                b.AddText(' ');
            }

            bool loadIndicatorShown = false;
            for (int i = loadIndicators.Count - 1; i >= 0; i--)
            {
                if (loadIndicators[i] != address) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('○');
                b.ForegroundColor = CharColor.Silver;
                loadIndicators.RemoveAt(i);
                loadIndicatorShown = true;
                break;
            }

            bool storeIndicatorShown = false;
            for (int i = storeIndicators.Count - 1; i >= 0; i--)
            {
                if (storeIndicators[i] != address) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('●');
                b.ForegroundColor = CharColor.Silver;
                storeIndicators.RemoveAt(i);
                storeIndicatorShown = true;
                break;
            }

            if (!loadIndicatorShown && !storeIndicatorShown)
            { b.AddText(' '); }

            b.AddText(address.ToString(CultureInfo.InvariantCulture));
            b.AddSpace(7);

            b.ForegroundColor = CharColor.Silver;
            b.BackgroundColor = CharColor.Black;

            if (item.IsNull)
            {
                b.ForegroundColor = CharColor.Gray;
                b.AddText("<null>");
                return;
            }

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

        Range<int> interval = Interpreter.BytecodeInterpreter.GetStackInterval(out bool isReversed);

        interval = LanguageCore.Range.Intersect(interval, interval.Offset(isReversed ? -StackScrollBar.Offset : StackScrollBar.Offset));

        IEnumerable<int> enumerator = interval.ForEach();
        if (isReversed)
        { enumerator = enumerator.Reverse(); }
        foreach (int i in enumerator)
        {
            DataItem item = this.Interpreter.BytecodeInterpreter.Memory[i];

            if (stackDebugInfo.TryGet(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart, i, out StackElementInformations itemDebugInfo))
            {
                Range<int> range = itemDebugInfo.GetRange(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart);

                if (itemDebugInfo.Kind == StackElementKind.Variable ||
                    itemDebugInfo.Kind == StackElementKind.Parameter ||
                    itemDebugInfo.Kind == StackElementKind.Internal)
                {
                    if (range.Start == range.End)
                    {
                        b.ForegroundColor = CharColor.Silver;

                        DrawElement(i, item);

                        b.ForegroundColor = CharColor.Gray;
                        b.AddText($" ({itemDebugInfo.Kind}) {itemDebugInfo.Tag}");
                    }
                    else if (range.Start == i)
                    {
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText($" ({itemDebugInfo.Kind}) {itemDebugInfo.Tag} {{");

                        b.BackgroundColor = CharColor.Black;
                        b.FinishLine();
                        b.ForegroundColor = CharColor.Silver;

                        DrawElement(i, item);
                    }
                    else if (range.End == i)
                    {
                        DrawElement(i, item);

                        b.BackgroundColor = CharColor.Black;
                        b.FinishLine();
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(' ');
                        b.AddText('}');
                    }
                    else
                    {
                        DrawElement(i, item);
                    }
                }
                else if (itemDebugInfo.Kind == StackElementKind.Internal)
                {
                    DrawElement(i, item);

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(' ');
                    b.AddText(itemDebugInfo.Tag);
                }
                else
                {
                    DrawElement(i, item);
                }
            }
            else
            {
                DrawElement(i, item);
            }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        int nextEmpty;
        if (interval.Size() == 0)
        {
            nextEmpty = Interpreter.BytecodeInterpreter.Registers.StackPointer;
        }
        else
        {
            if (isReversed)
            { nextEmpty = enumerator.Last() - 1; }
            else
            { nextEmpty = enumerator.Last() + 1; }
        }

        DrawElement(nextEmpty, DataItem.Null);

        StackScrollBar.Draw(b);
    }
}
