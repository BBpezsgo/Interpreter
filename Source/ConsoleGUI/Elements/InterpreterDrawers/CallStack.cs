using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    private void CallstackElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        sender.DrawBuffer.ResetColor();

        ImmutableArray<int> calltraceRaw = BytecodeProcessor.TraceCalls(Interpreter.BytecodeInterpreter.Memory, Interpreter.BytecodeInterpreter.Registers.BasePointer);

        FunctionInformations[] callstack;
        if (Interpreter.DebugInformation is not null)
        { callstack = Interpreter.DebugInformation.GetFunctionInformations(calltraceRaw).ToArray(); }
        else
        { callstack = new FunctionInformations[calltraceRaw.Length]; }

        int i;
        for (i = 0; i < callstack.Length; i++)
        {
            FunctionInformations callframe = callstack[i];

            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
            sender.DrawBuffer.AddText(' ');

            sender.DrawBuffer.AddText(' ', 3 - i.ToString(CultureInfo.InvariantCulture).Length);

            sender.DrawBuffer.AddText(i.ToString(CultureInfo.InvariantCulture));
            sender.DrawBuffer.AddSpace(5);

            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
            sender.DrawBuffer.BackgroundColor = CharColor.Black;

            if (!callframe.IsValid)
            {
                sender.DrawBuffer.ForegroundColor = CharColor.BrightCyan;
                sender.DrawBuffer.AddText(calltraceRaw[i].ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                if (callframe.ReadableIdentifier.Contains('(', StringComparison.Ordinal))
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
            }

            sender.DrawBuffer.BackgroundColor = CharColor.Black;
            sender.DrawBuffer.FinishLine();
            sender.DrawBuffer.ForegroundColor = CharColor.Silver;
        }

        if (Interpreter.DebugInformation is not null)
        {
            FunctionInformations callframe = Interpreter.DebugInformation.GetFunctionInformations(this.Interpreter.BytecodeInterpreter.Registers.CodePointer);

            if (callframe.IsValid)
            {
                sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                sender.DrawBuffer.AddText(' ');

                sender.DrawBuffer.AddText(' ', 3 - (i + 1).ToString(CultureInfo.InvariantCulture).Length);

                sender.DrawBuffer.AddText((i + 1).ToString(CultureInfo.InvariantCulture));
                sender.DrawBuffer.AddSpace(5);

                sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                sender.DrawBuffer.BackgroundColor = CharColor.Black;

                if (callframe.ReadableIdentifier.Contains('(', StringComparison.Ordinal))
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
