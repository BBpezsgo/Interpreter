﻿using System.Runtime.InteropServices;
using Win32.Console;

namespace ConsoleGUI;

using LanguageCore;
using LanguageCore.Runtime;

public partial class InterpreterElement
{
    void GetDataMovementIndicators(Instruction instruction, List<int> loadIndicators, List<int> storeIndicators)
    {
        if (instruction.Opcode is
            Opcode.Push)
        {
            storeIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer + (BytecodeProcessor.StackDirection * BytecodeProcessor.StackPointerOffset));
        }

        if (instruction.Opcode is
            Opcode.Pop or
            Opcode.PopTo)
        {
            loadIndicators.Add(Interpreter.BytecodeInterpreter.Registers.StackPointer + (BytecodeProcessor.StackDirection * (BytecodeProcessor.StackPointerOffset - 1)));
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
        { stackDebugInfo = Interpreter.DebugInformation.GetScopeInformation(Interpreter.BytecodeInterpreter.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        ReadOnlySpan<int> savedBasePointers = DebugUtils.TraceBasePointers(ImmutableCollectionsMarshal.AsImmutableArray(Interpreter.BytecodeInterpreter.Memory), Interpreter.BytecodeInterpreter.Registers.BasePointer);

        List<int> loadIndicators = new();
        List<int> storeIndicators = new();

        if (Interpreter.NextInstruction.HasValue)
        { GetDataMovementIndicators(Interpreter.NextInstruction.Value, loadIndicators, storeIndicators); }

        void DrawElement(int address, RuntimeValue item, ReadOnlySpan<int> savedBasePointers)
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

            b.AddText(address.ToString());
            b.AddSpace(7);

            b.ForegroundColor = CharColor.Silver;
            b.BackgroundColor = CharColor.Black;

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

        void DrawElementWInfo(int address, RuntimeValue item, ReadOnlySpan<int> savedBasePointers, StackElementInformation info)
        {
            Range<int> range = info.GetRange(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart);

            if (info.Kind is
                StackElementKind.Variable or
                StackElementKind.Parameter or
                StackElementKind.Internal)
            {
                if (range.Start == range.End)
                {
                    b.ForegroundColor = CharColor.Silver;

                    DrawElement(address, item, savedBasePointers);

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText($" ({info.Kind}) {info.Tag}");
                }
                else if (range.Start == address)
                {
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText($" ({info.Kind}) {info.Tag} {{");

                    b.BackgroundColor = CharColor.Black;
                    b.FinishLine();
                    b.ForegroundColor = CharColor.Silver;

                    DrawElement(address, item, savedBasePointers);
                }
                else if (range.End == address)
                {
                    DrawElement(address, item, savedBasePointers);

                    b.BackgroundColor = CharColor.Black;
                    b.FinishLine();
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(' ');
                    b.AddText('}');
                }
                else
                {
                    DrawElement(address, item, savedBasePointers);
                }
            }
            else if (info.Kind == StackElementKind.Internal)
            {
                DrawElement(address, item, savedBasePointers);

                b.ForegroundColor = CharColor.Gray;
                b.AddText(' ');
                b.AddText(info.Tag);
            }
            else
            {
                DrawElement(address, item, savedBasePointers);
            }
        }

        Range<int> interval = Interpreter.BytecodeInterpreter.GetStackInterval(out bool isReversed);

        interval = Range.Intersect(interval, interval.Offset(isReversed ? -StackScrollBar.Offset : StackScrollBar.Offset));

        IEnumerable<int> enumerator = interval.ForEach();
        if (isReversed)
        { enumerator = enumerator.Reverse(); }

        foreach (int i in enumerator)
        {
            RuntimeValue item = Interpreter.BytecodeInterpreter.Memory[i];

            if (stackDebugInfo.TryGet(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart, i, out StackElementInformation itemDebugInfo))
            { DrawElementWInfo(i, item, savedBasePointers, itemDebugInfo); }
            else
            { DrawElement(i, item, savedBasePointers); }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        const int EmptyCount = 2;

        for (int i = 1; i <= EmptyCount; i++)
        {
            int nextEmpty;
            if (interval.Size() == 0)
            {
                if (isReversed)
                { nextEmpty = Interpreter.BytecodeInterpreter.Registers.StackPointer - (i - 1); }
                else
                { nextEmpty = Interpreter.BytecodeInterpreter.Registers.StackPointer + (i - 1); }
            }
            else
            {
                if (isReversed)
                { nextEmpty = enumerator.Last() - i; }
                else
                { nextEmpty = enumerator.Last() + i; }
            }

            RuntimeValue item = Interpreter.BytecodeInterpreter.Memory[nextEmpty];

            if (stackDebugInfo.TryGet(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart, nextEmpty, out StackElementInformation itemDebugInfo))
            { DrawElementWInfo(nextEmpty, item, savedBasePointers, itemDebugInfo); }
            else
            { DrawElement(nextEmpty, item, savedBasePointers); }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        StackScrollBar.Draw(b);
    }
}
