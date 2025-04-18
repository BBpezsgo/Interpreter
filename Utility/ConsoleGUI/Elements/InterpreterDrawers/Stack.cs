﻿using Win32.Console;
using LanguageCore;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void StackElement_OnBeforeDraw(InlineElement sender)
    {
        bool focused = _focusedElement == 2;
        sender.IsFocused = focused;

        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        CollectedScopeInfo stackDebugInfo;
        if (!Interpreter.DebugInformation.IsEmpty)
        { stackDebugInfo = Interpreter.DebugInformation.GetScopeInformation(Interpreter.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        ReadOnlySpan<CallTraceItem> callTrace = DebugUtils.TraceStack(Interpreter.Memory, Interpreter.Registers.BasePointer, Interpreter.DebugInformation.IsEmpty ? null : Interpreter.DebugInformation.StackOffsets);

        List<DataMovement> loadIndicators = new();
        List<DataMovement> storeIndicators = new();

        if (Interpreter.NextInstruction.HasValue)
        { GetDataMovementIndicators(Interpreter.NextInstruction.Value, loadIndicators, storeIndicators); }

        void DrawElement(int address, byte item, ReadOnlySpan<CallTraceItem> callTrace)
        {
            if (Interpreter.Registers.BasePointer == address)
            {
                b.ForegroundColor = CharColor.BrightBlue;
                b.AddText('►');
                b.ForegroundColor = CharColor.Silver;
            }
            else if (callTrace.Contains(v => v.BasePointer == address))
            {
                b.ForegroundColor = CharColor.Silver;
                b.AddText('►');
                b.ForegroundColor = CharColor.Silver;
            }
            else if (Interpreter.Registers.StackPointer == address)
            {
                b.ForegroundColor = CharColor.Yellow;
                b.AddText('►');
                b.ForegroundColor = CharColor.Silver;
            }
            else
            {
                b.ForegroundColor = CharColor.Silver;
                b.AddText(' ');
            }

            bool dataMovementShown = false;
            for (int i = loadIndicators.Count - 1; i >= 0; i--)
            {
                if (loadIndicators[i].Address == address)
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = CharColor.Silver;
                    dataMovementShown = true;
                    break;
                }
                else if (loadIndicators[i].Contains(address))
                {
                    b.ForegroundColor = CharColor.BrightRed;
                    b.AddText('|');
                    b.ForegroundColor = CharColor.Silver;
                    dataMovementShown = true;
                    break;
                }
            }

            if (!dataMovementShown)
            {
                for (int i = storeIndicators.Count - 1; i >= 0; i--)
                {
                    if (storeIndicators[i].Address == address)
                    {
                        b.ForegroundColor = CharColor.BrightRed;
                        b.AddText('●');
                        b.ForegroundColor = CharColor.Silver;
                        dataMovementShown = true;
                        break;
                    }
                    else if (storeIndicators[i].Contains(address))
                    {
                        b.ForegroundColor = CharColor.BrightRed;
                        b.AddText('|');
                        b.ForegroundColor = CharColor.Silver;
                        dataMovementShown = true;
                        break;
                    }
                }
            }

            if (!dataMovementShown)
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
                b.AddText(item.ToString());
            }
        }

        void DrawType(GeneralType type)
        {
            switch (type)
            {
                case ArrayType v:
                    DrawType(v.Of);

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText('[');

                    if (v.ComputedLength.HasValue)
                    {
                        b.ForegroundColor = CharColor.Silver;
                        b.AddText(v.ComputedLength.Value.ToString());
                    }
                    else if (v.Length is not null)
                    {
                        b.ForegroundColor = CharColor.Silver;
                        b.AddText('?');
                    }

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(']');

                    break;
                case BuiltinType v:
                    b.ForegroundColor = CharColor.BrightBlue;
                    b.AddText(v.ToString());

                    break;
                case FunctionType v:
                    DrawType(v.ReturnType);

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText('(');

                    for (int i = 0; i < v.Parameters.Length; i++)
                    {
                        if (i > 0)
                        {
                            b.ForegroundColor = CharColor.Gray;
                            b.AddText(", ");
                        }

                        DrawType(v.Parameters[i]);
                    }

                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(')');

                    break;
                case GenericType v:
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(v.ToString());

                    break;
                case PointerType v:
                    DrawType(v.To);

                    b.ForegroundColor = CharColor.Silver;
                    b.AddText('*');

                    break;
                case StructType v:
                    b.ForegroundColor = CharColor.BrightGreen;
                    b.AddText(v.Struct.Identifier.Content);

                    if (!v.TypeArguments.IsEmpty)
                    {
                        b.ForegroundColor = CharColor.Gray;
                        b.AddChar('<');
                        b.AddText(string.Join(", ", v.TypeArguments.Values));
                        b.AddChar('>');
                    }
                    else if (v.Struct.Template is not null)
                    {
                        b.ForegroundColor = CharColor.Gray;
                        b.AddChar('<');
                        b.AddText(string.Join(", ", v.Struct.Template.Parameters));
                        b.AddChar('>');
                    }

                    break;
                case AliasType v:
                    b.ForegroundColor = CharColor.BrightGreen;
                    b.AddText(v.ToString());
                    break;
                default:
                    b.ForegroundColor = CharColor.Gray;
                    b.AddText(type.ToString());
                    break;
            }
        }

        void DrawElementWInfo(int address, byte item, ReadOnlySpan<CallTraceItem> callTrace, StackElementInformation info)
        {
            Range<int> range = info.GetRange(Interpreter.Registers.BasePointer, Interpreter.StackStart);

            if (range.Start == range.End)
            {
                b.ForegroundColor = CharColor.Silver;

                DrawElement(address, item, callTrace);

                b.ForegroundColor = CharColor.Gray;
                b.AddText($" ({info.Kind}) ");

                DrawType(info.Type);

                b.ForegroundColor = info.Kind switch
                {
                    StackElementKind.Internal => CharColor.Gray,
                    StackElementKind.Variable => CharColor.Silver,
                    StackElementKind.Parameter => CharColor.Silver,
                    _ => throw new UnreachableException(),
                };
                b.AddText(' ');
                b.AddText(info.Identifier);
            }
            else if (range.Start == address)
            {
                b.ForegroundColor = CharColor.Gray;
                b.AddText($" ({info.Kind}) ");

                DrawType(info.Type);

                b.ForegroundColor = info.Kind switch
                {
                    StackElementKind.Internal => CharColor.Gray,
                    StackElementKind.Variable => CharColor.Silver,
                    StackElementKind.Parameter => CharColor.Silver,
                    _ => throw new UnreachableException(),
                };
                b.AddText(' ');
                b.AddText(info.Identifier);

                b.ForegroundColor = CharColor.Gray;
                b.AddText(" {");

                b.BackgroundColor = CharColor.Black;
                b.FinishLine();
                b.ForegroundColor = CharColor.Silver;

                DrawElement(address, item, callTrace);
            }
            else if (range.End == address)
            {
                DrawElement(address, item, callTrace);

                b.BackgroundColor = CharColor.Black;
                b.FinishLine();
                b.ForegroundColor = CharColor.Gray;
                b.AddText(' ');
                b.AddText('}');
            }
            else
            {
                DrawElement(address, item, callTrace);
            }
        }

        Range<int> interval = new(Interpreter.StackStart, Interpreter.Registers.StackPointer);
        bool isReversed = ProcessorState.StackDirection <= 0;

        interval = RangeUtils.Intersect(interval, interval.Offset(isReversed ? -StackScrollBar.Offset : StackScrollBar.Offset));

        IEnumerable<int> enumerator = interval.ForEach();
        if (isReversed)
        { enumerator = enumerator.Reverse(); }

        foreach (int i in enumerator)
        {
            byte item = Interpreter.Memory[i];

            if (stackDebugInfo.TryGet(Interpreter.Registers.BasePointer, Interpreter.StackStart, i, out StackElementInformation itemDebugInfo))
            { DrawElementWInfo(i, item, callTrace, itemDebugInfo); }
            else
            { DrawElement(i, item, callTrace); }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        const int EmptyCount = 32;

        for (int i = 1; i <= EmptyCount; i++)
        {
            int nextEmpty;
            if (interval.Size() == 0)
            {
                if (isReversed)
                { nextEmpty = Interpreter.Registers.StackPointer - (i - 1); }
                else
                { nextEmpty = Interpreter.Registers.StackPointer + (i - 1); }
            }
            else
            {
                if (isReversed)
                { nextEmpty = enumerator.Last() - i; }
                else
                { nextEmpty = enumerator.Last() + i; }
            }

            if (nextEmpty < 0 || nextEmpty >= Interpreter.Memory.Length)
            { break; }

            byte item = Interpreter.Memory[nextEmpty];

            if (stackDebugInfo.TryGet(Interpreter.Registers.BasePointer, Interpreter.StackStart, nextEmpty, out StackElementInformation itemDebugInfo))
            { DrawElementWInfo(nextEmpty, item, callTrace, itemDebugInfo); }
            else
            { DrawElement(nextEmpty, item, callTrace); }

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        StackScrollBar.Draw(b);
    }
}
