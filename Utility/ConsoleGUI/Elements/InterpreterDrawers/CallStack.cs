﻿using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void CallStackElement_OnBeforeDraw(InlineElement sender)
    {
        bool focused = _focusedElement == 4;
        sender.IsFocused = focused;

        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        sender.DrawBuffer.ResetColor();

        ReadOnlySpan<CallTraceItem> callTraceRaw = DebugUtils.TraceStack(Interpreter.Processor.Memory, Interpreter.Processor.Registers.BasePointer, Interpreter.DebugInformation.IsEmpty ? null : Interpreter.DebugInformation.StackOffsets);

        FunctionInformation[] callStack;
        if (!Interpreter.DebugInformation.IsEmpty)
        { callStack = Interpreter.DebugInformation.GetFunctionInformation(callTraceRaw).ToArray(); }
        else
        { callStack = new FunctionInformation[callTraceRaw.Length]; }

        int i;
        for (i = 0; i < callStack.Length; i++)
        {
            FunctionInformation callFrame = callStack[i];

            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
            sender.DrawBuffer.AddText(' ');

            sender.DrawBuffer.AddText(' ', 3 - i.ToString(CultureInfo.InvariantCulture).Length);

            sender.DrawBuffer.AddText(i.ToString(CultureInfo.InvariantCulture));
            sender.DrawBuffer.AddSpace(5);

            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
            sender.DrawBuffer.BackgroundColor = CharColor.Black;

            if (!callFrame.IsValid)
            {
                sender.DrawBuffer.ForegroundColor = CharColor.BrightCyan;
                sender.DrawBuffer.AddText(callTraceRaw[i].InstructionPointer.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                if (callFrame.ReadableIdentifier!.Contains('(', StringComparison.Ordinal))
                {
                    string functionName = callFrame.ReadableIdentifier[..callFrame.ReadableIdentifier.IndexOf('(', StringComparison.Ordinal)];

                    sender.DrawBuffer.ForegroundColor = CharColor.BrightYellow;
                    sender.DrawBuffer.AddText(functionName);

                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                    sender.DrawBuffer.AddChar('(');

                    string parameters = callFrame.ReadableIdentifier[(callFrame.ReadableIdentifier.IndexOf('(', StringComparison.Ordinal) + 1)..callFrame.ReadableIdentifier.IndexOf(')', StringComparison.Ordinal)];

                    List<string> parameters2;
                    if (!parameters.Contains(',', StringComparison.Ordinal))
                    {
                        parameters2 = new List<string>() { parameters };
                    }
                    else
                    {
                        parameters2 = new List<string>();
                        string[] splitted = parameters.Split(',');
                        for (int j = 0; j < splitted.Length; j++)
                        { parameters2.Add(splitted[j].Trim()); }
                    }

                    for (int j = 0; j < parameters2.Count; j++)
                    {
                        if (j > 0)
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                            sender.DrawBuffer.AddText(',');
                            sender.DrawBuffer.AddText(' ');
                        }

                        string param = parameters2[j];
                        if (TypeKeywords.List.Contains(param))
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.BrightBlue;
                        }
                        else
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                        }
                        sender.DrawBuffer.AddText(param);
                    }

                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                    sender.DrawBuffer.AddChar(')');

                    sender.DrawBuffer.ResetColor();
                }
                else
                {
                    sender.DrawBuffer.AddText(callFrame.ReadableIdentifier);
                }
            }

            sender.DrawBuffer.BackgroundColor = CharColor.Black;
            sender.DrawBuffer.FinishLine();
            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
        }

        if (!Interpreter.DebugInformation.IsEmpty)
        {
            FunctionInformation callframe = Interpreter.DebugInformation.GetFunctionInformation(this.Interpreter.Processor.Registers.CodePointer);

            if (callframe.IsValid)
            {
                sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                sender.DrawBuffer.AddText(' ');

                sender.DrawBuffer.AddText(' ', 3 - (i + 1).ToString(CultureInfo.InvariantCulture).Length);

                sender.DrawBuffer.AddText((i + 1).ToString(CultureInfo.InvariantCulture));
                sender.DrawBuffer.AddSpace(5);

                sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                sender.DrawBuffer.BackgroundColor = CharColor.Black;

                if (callframe.ReadableIdentifier!.Contains('(', StringComparison.Ordinal))
                {
                    string functionName = callframe.ReadableIdentifier[..callframe.ReadableIdentifier.IndexOf('(', StringComparison.Ordinal)];

                    sender.DrawBuffer.ForegroundColor = CharColor.BrightYellow;
                    sender.DrawBuffer.AddText(functionName);

                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                    sender.DrawBuffer.AddChar('(');

                    string parameters = callframe.ReadableIdentifier[(callframe.ReadableIdentifier.IndexOf('(', StringComparison.Ordinal) + 1)..callframe.ReadableIdentifier.IndexOf(')', StringComparison.Ordinal)];

                    List<string> parameters2;
                    if (!parameters.Contains(',', StringComparison.Ordinal))
                    {
                        parameters2 = new List<string>() { parameters };
                    }
                    else
                    {
                        parameters2 = new List<string>();
                        string[] splitted = parameters.Split(',');
                        for (int j = 0; j < splitted.Length; j++)
                        { parameters2.Add(splitted[j].Trim()); }
                    }

                    for (int j = 0; j < parameters2.Count; j++)
                    {
                        if (j > 0)
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                            sender.DrawBuffer.AddText(',');
                            sender.DrawBuffer.AddText(' ');
                        }

                        string param = parameters2[j];
                        if (TypeKeywords.List.Contains(param))
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.BrightBlue;
                        }
                        else
                        {
                            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                        }
                        sender.DrawBuffer.AddText(param);
                    }

                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                    sender.DrawBuffer.AddChar(')');

                    sender.DrawBuffer.ResetColor();
                }
                else
                {
                    sender.DrawBuffer.AddText(callframe.ReadableIdentifier);
                }

                sender.DrawBuffer.ForegroundColor = CharColor.Gray;
                sender.DrawBuffer.AddText(" (current)");

                sender.DrawBuffer.BackgroundColor = CharColor.Black;
                sender.DrawBuffer.FinishLine();
                sender.DrawBuffer.ForegroundColor = CharColor.Silver;
            }
        }
    }
}
