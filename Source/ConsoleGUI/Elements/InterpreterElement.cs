using System.Drawing;
using System.Runtime.Versioning;
using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

interface IJump
{
    public bool IsPaused { get; set; }
    public bool ShouldDelete { get; }
    public bool ShouldJump(Interpreter interpreter);
    public void Tick();
}

class InstructionCountJump : IJump
{
    public bool IsPaused { get; set; }
    public int Count { get; private set; }
    public int Current { get; private set; }
    public bool ShouldDelete => false;

    public InstructionCountJump(int count)
    {
        Count = count;
        Current = 0;
    }

    public void Reset(int newCount)
    {
        Count = newCount;
        Current = 0;
    }

    public bool ShouldJump(Interpreter interpreter)
    {
        return Current < Count;
    }

    public void Tick()
    {
        Current++;
    }

    public override string ToString() => (Count - Current).ToString();
}

class BreakPointJump : IJump
{
    public bool IsPaused { get; set; }
    public int Instruction { get; }
    public bool ShouldDelete { get; private set; }

    public BreakPointJump(int instruction) => Instruction = instruction;

    public bool ShouldJump(Interpreter interpreter)
    {
        if (interpreter.BytecodeInterpreter.Registers.CodePointer == Instruction)
        {
            ShouldDelete = true;
            return false;
        }

        return true;
    }

    public void Tick()
    {

    }
}

[SupportedOSPlatform("windows")]
public sealed class InterpreterElement : WindowElement
{
    readonly InterpreterDebuggabble Interpreter;

    readonly ScrollBar HeapScrollBar;
    readonly ScrollBar StackScrollBar;

    IJump? CurrentJump;

    readonly MainThreadTimer InterpreterTimer;
    readonly StandardIOElement ConsolePanel;

    public InterpreterElement(InterpreterDebuggabble interpreter)
    {
        Interpreter = interpreter;

        ClearBuffer();

        InlineElement statePanel = new()
        {
            HasBorder = true,
            Title = "State",
            Layout = new InlineLayout(InlineLayoutSizeMode.Fixed, 4),
        };
        statePanel.OnBeforeDraw += StateElement_OnBeforeDraw;

        InlineElement codePanel = new()
        {
            HasBorder = true,
            Title = "Code",
        };
        codePanel.OnBeforeDraw += SourceCodeElement_OnBeforeDraw;
        codePanel.OnMouseEventInvoked += SourceCodeElement_OnMouse;

        ConsolePanel = new StandardIOElement
        {
            HasBorder = true,
            Title = "Console",
        };

        InlineElement stackPanel = new()
        {
            HasBorder = true,
            Title = "Stack",
            Layout = InlineLayout.Stretchy(130),
        };
        stackPanel.OnBeforeDraw += StackElement_OnBeforeDraw;

        StackScrollBar = new ScrollBar((sender) => (0, Interpreter.BytecodeInterpreter.Registers.StackPointer - Interpreter.BytecodeInterpreter.StackStart + 30), stackPanel);

        stackPanel.OnMouseEventInvoked += StackScrollBar.FeedEvent;
        stackPanel.OnKeyEventInvoked += StackScrollBar.FeedEvent;

        InlineElement heapPanel = new()
        {
            HasBorder = true,
            Title = "HEAP",
        };

        HeapScrollBar = new ScrollBar((sender) => (0, Interpreter.BytecodeInterpreter.Memory.Length - 3), heapPanel);

        heapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;
        heapPanel.OnMouseEventInvoked += HeapScrollBar.FeedEvent;
        heapPanel.OnKeyEventInvoked += HeapScrollBar.FeedEvent;

        InlineElement callStackPanel = new()
        {
            HasBorder = true,
            Title = "Call Stack",
        };
        callStackPanel.OnBeforeDraw += CallstackElement_OnBeforeDraw;

        Elements = new Element[]
        {
            new HorizontalLayoutElement()
            {
                Rect = new Rectangle(0, 0, Console.WindowWidth, Console.WindowHeight),
                Elements = new Element[]
                {
                    new VerticalLayoutElement()
                    {
                        Elements = new Element[]
                        {
                            statePanel,
                            ConsolePanel,
                            codePanel,
                        }
                    },
                    new VerticalLayoutElement()
                    {
                        Elements = new Element[]
                        {
                            stackPanel,
                            heapPanel,
                            callStackPanel,
                        }
                    }
                }
            }
        };

        HasBorder = false;

        InterpreterTimer = new MainThreadTimer(200);
        InterpreterTimer.Elapsed += OnInterpreterTimer;
        InterpreterTimer.Enabled = true;

        Interpreter.OnOutput += (_, p1, p2) => PrintOutput(p1, p2);

        Interpreter.OnStdOut += (sender, data) => ConsolePanel.Write(char.ToString(data));
        Interpreter.OnStdError += (sender, data) => ConsolePanel.Write(char.ToString(data), CharColor.BrightRed);

        Interpreter.OnNeedInput += (_) => ConsolePanel.BeginRead();

        ConsolePanel.OnInput += Interpreter.OnInput;
    }

    void OnInterpreterTimer()
    {
        if (CurrentJump is null) return;
        if (CurrentJump.IsPaused) return;
        if (CurrentJump.ShouldDelete) { CurrentJump = null; return; }
        if (!CurrentJump.ShouldJump(Interpreter)) return;

        CurrentJump?.Tick();
        Interpreter.DoUpdate();
        if (Interpreter.BytecodeInterpreter.IsDone)
        {
            ConsoleGUI.Instance?.Destroy();
            return;
        }
        if (ConsoleGUI.Instance != null) ConsoleGUI.Instance.NextRefreshConsole = true;
    }

    void PrintOutput(string message, LogType logType)
    {
        ConsolePanel.Write(message, logType switch
        {
            LogType.Normal => CharColor.Silver,
            LogType.Warning => CharColor.BrightYellow,
            LogType.Error => CharColor.BrightRed,
            LogType.Debug => CharColor.Silver,
            _ => CharColor.Silver,
        });
        ConsolePanel.Write("\n");
    }

    public override void Tick(double deltaTime) => InterpreterTimer.Tick(deltaTime);

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
                if (callframe.IsMacro)
                {
                    sender.DrawBuffer.ForegroundColor = CharColor.BrightBlue;
                    sender.DrawBuffer.AddText("macro ");
                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                }

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

                if (callframe.IsMacro)
                {
                    sender.DrawBuffer.ForegroundColor = CharColor.BrightBlue;
                    sender.DrawBuffer.AddText("macro ");
                    sender.DrawBuffer.ForegroundColor = CharColor.Silver;
                }

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

    private void StateElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (this.Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        b.AddText(' ', 2);
        b.AddText("IsRunning: ");
        b.AddText((!this.Interpreter.BytecodeInterpreter.IsDone).ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        b.AddText(' ', 2);

        if (this.Interpreter.StackOperation)
        {
            b.BackgroundColor = CharColor.White;
            b.ForegroundColor = CharColor.Black;
            b.AddText("STACK");
            b.BackgroundColor = CharColor.Black;
            b.ForegroundColor = CharColor.Silver;
        }
        else
        {
            b.AddText("STACK");
        }

        b.AddText(' ', 2);

        if (this.Interpreter.HeapOperation)
        {
            b.BackgroundColor = CharColor.White;
            b.ForegroundColor = CharColor.Black;
            b.AddText("HEAP");
            b.BackgroundColor = CharColor.Black;
            b.ForegroundColor = CharColor.Silver;
        }
        else
        {
            b.AddText("HEAP");
        }

        b.AddText(' ', 2);

        if (this.Interpreter.AluOperation)
        {
            b.BackgroundColor = CharColor.White;
            b.ForegroundColor = CharColor.Black;
            b.AddText("ALU");
            b.BackgroundColor = CharColor.Black;
            b.ForegroundColor = CharColor.Silver;
        }
        else
        {
            b.AddText("ALU");
        }

        b.AddText(' ', 2);

        if (this.Interpreter.ExternalFunctionOperation)
        {
            b.BackgroundColor = CharColor.White;
            b.ForegroundColor = CharColor.Black;
            b.AddText("EXTERNAL");
            b.BackgroundColor = CharColor.Black;
            b.ForegroundColor = CharColor.Silver;
        }
        else
        {
            b.AddText("EXTERNAL");
        }

        b.AddText(' ', 2);

        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;
    }

    private void HeapElement_OnBeforeDraw(InlineElement sender)
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
                { storeIndicators.Add(Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - 1].VInt); }
                else
                { storeIndicators.Add((int)instruction.Parameter); }
            }

            if (instruction.Opcode == Opcode.HeapGet)
            {
                if (instruction.AddressingMode == AddressingMode.Runtime)
                {
                    if (this.Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - 1].Type == RuntimeType.Integer)
                    { loadIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory[Interpreter.BytecodeInterpreter.Registers.StackPointer - 1].VInt); }
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
    private void StackElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (this.Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        CollectedScopeInfo stackDebugInfo;
        if (Interpreter.DebugInformation is not null)
        { stackDebugInfo = Interpreter.DebugInformation.GetScopeInformations(Interpreter.BytecodeInterpreter.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        Instruction? instruction_ = this.Interpreter.NextInstruction;

        int stackSize = Interpreter.BytecodeInterpreter.Registers.StackPointer - Interpreter.BytecodeInterpreter.StackStart;

        ImmutableArray<int> savedBasePointers = BytecodeProcessor.TraceBasePointers(Interpreter.BytecodeInterpreter.Memory, Interpreter.BytecodeInterpreter.Registers.BasePointer);

        bool basepointerShown = false;

        List<int> loadIndicators = new();
        List<int> storeIndicators = new();

        if (instruction_.HasValue)
        {
            Instruction instruction = instruction_.Value;

            if (instruction.Opcode == Opcode.StackStore)
            {
                storeIndicators.Add(this.Interpreter.BytecodeInterpreter.GetAddress(instruction.Parameter.Integer ?? 0, instruction.AddressingMode));
            }

            if (instruction.Opcode == Opcode.StackStore ||
                instruction.Opcode == Opcode.HeapSet)
            {
                if (instruction.AddressingMode == AddressingMode.Runtime)
                { loadIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize - 2); }
                else
                { loadIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize - 1); }
            }

            if (instruction.Opcode == Opcode.StackLoad)
            {
                loadIndicators.Add(this.Interpreter.BytecodeInterpreter.GetAddress(instruction.Parameter.Integer ?? 0, instruction.AddressingMode));
                storeIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize);
            }

            if (instruction.Opcode == Opcode.Push ||
                instruction.Opcode == Opcode.GetBasePointer ||
                instruction.Opcode == Opcode.HeapGet)
            { storeIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize); }

            if (instruction.Opcode == Opcode.Pop)
            { loadIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize - 1); }

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
                loadIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize - 1);
                storeIndicators.Add(Interpreter.BytecodeInterpreter.StackStart + stackSize - 2);
            }
        }

        int stackDrawStart = StackScrollBar.Offset;
        int stackDrawEnd = stackSize;

        int notVisible = Math.Max(stackDrawEnd - (sender.Rect.Height - 3), 0);

        stackDrawStart += notVisible;

        stackDrawStart += Interpreter.BytecodeInterpreter.StackStart;
        stackDrawEnd += Interpreter.BytecodeInterpreter.StackStart;

        void DrawElement(int address, DataItem item)
        {
            if (Interpreter.BytecodeInterpreter.Registers.BasePointer == address)
            {
                b.ForegroundColor = CharColor.BrightBlue;
                b.AddText('►');
                basepointerShown = true;
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
            for (int j = loadIndicators.Count - 1; j >= 0; j--)
            {
                if (loadIndicators[j] != address) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('○');
                b.ForegroundColor = CharColor.Silver;
                loadIndicators.RemoveAt(j);
                loadIndicatorShown = true;
                break;
            }

            bool storeIndicatorShown = false;
            for (int j = storeIndicators.Count - 1; j >= 0; j--)
            {
                if (storeIndicators[j] != address) continue;
                b.ForegroundColor = CharColor.BrightRed;
                b.AddText('●');
                b.ForegroundColor = CharColor.Silver;
                storeIndicators.RemoveAt(j);
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

        int i;
        for (i = stackDrawStart; i < stackDrawEnd; i++)
        {
            DataItem item = this.Interpreter.BytecodeInterpreter.Memory[i];

            if (stackDebugInfo.TryGet(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart, i, out StackElementInformations itemDebugInfo))
            {
                MutableRange<int> range = itemDebugInfo.GetRange(Interpreter.BytecodeInterpreter.Registers.BasePointer, Interpreter.BytecodeInterpreter.StackStart);

                if (itemDebugInfo.Kind == StackElementKind.Variable ||
                    itemDebugInfo.Kind == StackElementKind.Parameter)
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

        while (
            (
                !basepointerShown &&
                i <= this.Interpreter.BytecodeInterpreter.Registers.BasePointer
            ) ||
            loadIndicators.Count > 0 ||
            storeIndicators.Count > 0)
        {
            if (this.Interpreter.BytecodeInterpreter.Registers.BasePointer == i)
            {
                b.ForegroundColor = CharColor.BrightBlue;
                b.AddText('►');
                // basepointerShown = true;
                b.ForegroundColor = CharColor.Silver;
            }
            else
            {
                b.ForegroundColor = CharColor.Silver;
                b.AddText(' ');
            }

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

            b.AddText(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString(CultureInfo.InvariantCulture).Length);

            b.AddText(i.ToString(CultureInfo.InvariantCulture));
            b.AddSpace(5);

            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;

            // i++;

            break;
        }

        StackScrollBar.Draw(b);
    }

    void SourceCodeElement_OnMouse(InlineElement sender, MouseEvent e)
    {

    }

    void SourceCodeElement_OnBeforeDraw(InlineElement sender)
    {
        // sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (this.Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        void LinePrefix(string lineNumber)
        {
            b.AddText(' ', 4 - lineNumber.Length);
            b.ForegroundColor = CharColor.Silver;
            b.AddText(lineNumber);
            b.ForegroundColor = CharColor.Silver;
            b.AddSpace(5);
        }

        int indent = 0;
        if (Interpreter.DebugInformation is not null)
        {
            for (int i = 0; i < this.Interpreter.BytecodeInterpreter.Registers.CodePointer - 5; i++)
            {
                if (Interpreter.DebugInformation.CodeComments.TryGetValue(i, out List<string>? comments))
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        if (!comments[j].EndsWith("{ }", StringComparison.Ordinal) && comments[j].EndsWith('}'))
                        { indent--; }
                        if (!comments[j].EndsWith("{ }", StringComparison.Ordinal) && comments[j].EndsWith('{'))
                        { indent++; }
                    }
                }
            }
        }

        bool IsNextInstruction = false;
        for (int i = Math.Max(0, this.Interpreter.BytecodeInterpreter.Registers.CodePointer - 5); i < this.Interpreter.BytecodeInterpreter.Code.Length; i++)
        {
            if (Interpreter.BytecodeInterpreter.Registers.CodePointer == i) IsNextInstruction = true;

            Instruction instruction = Interpreter.BytecodeInterpreter.Code[i];

            if (this.Interpreter.DebugInformation is not null)
            {
                if (this.Interpreter.DebugInformation.CodeComments.TryGetValue(i, out List<string>? comments))
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        string comment = comments[j];

                        if (!comment.EndsWith("{ }", StringComparison.Ordinal) && comment.EndsWith('}'))
                        {
                            indent--;
                        }

                        LinePrefix(string.Empty);
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(' ', Math.Max(0, indent * 2));
                        b.AddText(comment);
                        b.ForegroundColor = CharColor.Silver;
                        b.BackgroundColor = CharColor.Black;
                        b.FinishLine();

                        if (!comment.EndsWith("{ }", StringComparison.Ordinal) && comment.EndsWith('{'))
                        {
                            indent++;
                        }
                    }
                }
            }

            if (CurrentJump is BreakPointJump breakPointJump &&
                breakPointJump.Instruction == i)
            { b.BackgroundColor = CharColor.Red; }

            if (sender.Rect.Contains(ConsoleMouse.RecordedConsolePosition) &&
                ConsoleMouse.RecordedConsolePosition.Y - sender.Rect.Top - 1 == b.CurrentLine &&
                ConsoleMouse.RecordedConsolePosition.X > sender.Rect.Left &&
                ConsoleMouse.RecordedConsolePosition.X <= sender.Rect.Left + 5)
            {
                if (ConsoleMouse.IsPressed(MouseButton.Left) &&
                    ConsoleMouse.LeftPressedAt.Y - sender.Rect.Top - 1 == b.CurrentLine)
                {
                    b.BackgroundColor = CharColor.BrightRed;
                }
                else
                {
                    b.BackgroundColor = CharColor.Red;
                }

                if (ConsoleMouse.IsUp(MouseButton.Left) &&
                    ConsoleMouse.LeftPressedAt.Y - sender.Rect.Top - 1 == b.CurrentLine)
                {
                    if (CurrentJump is BreakPointJump breakPointJump2 &&
                        breakPointJump2.Instruction == i)
                    {
                        CurrentJump = null;
                    }
                    else
                    {
                        CurrentJump = new BreakPointJump(i)
                        { IsPaused = !ConsoleKeyboard.IsActive(Win32.VirtualKeyCode.Control) };
                    }
                }
            }

            LinePrefix((i + 1).ToString(CultureInfo.InvariantCulture));
            b.BackgroundColor = CharColor.Black;

            b.ForegroundColor = CharColor.BrightYellow;
            b.AddText(' ', Math.Max(0, indent * 2));
            b.AddText(' ');
            if (IsNextInstruction)
            {
                IsNextInstruction = false;
                b.BackgroundColor = CharColor.BrightRed;
            }
            b.AddText(instruction.Opcode.ToString());
            b.AddText(' ');

            if (instruction.Opcode == Opcode.StackLoad ||
                instruction.Opcode == Opcode.StackStore ||
                instruction.Opcode == Opcode.HeapGet ||
                instruction.Opcode == Opcode.HeapSet)
            {
                b.AddText(instruction.AddressingMode.ToString());
                b.AddText(' ');
            }

            if (!instruction.Parameter.IsNull)
            {
                switch (instruction.Parameter.Type)
                {
                    case RuntimeType.Byte:
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(instruction.Parameter.VByte.ToString(CultureInfo.InvariantCulture));
                        b.AddText(' ');
                        break;
                    case RuntimeType.Integer:
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(instruction.Parameter.VInt.ToString(CultureInfo.InvariantCulture));
                        b.AddText(' ');
                        break;
                    case RuntimeType.Single:
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(instruction.Parameter.VSingle.ToString(CultureInfo.InvariantCulture));
                        b.AddText('f');
                        b.AddText(' ');
                        break;
                    case RuntimeType.Char:
                        b.ForegroundColor = CharColor.BrightYellow;
                        b.AddText('\'');
                        b.AddText(instruction.Parameter.VChar.Escape());
                        b.AddText('\'');
                        b.AddText(' ');
                        break;
                    default:
                        b.ForegroundColor = CharColor.White;
                        b.AddText(instruction.Parameter.ToString(CultureInfo.InvariantCulture));
                        b.AddText(' ');
                        break;
                }
            }
            b.BackgroundColor = CharColor.Black;

            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        if (CurrentJump is not null)
        {
            string t = CurrentJump.IsPaused ? $" Jump: {CurrentJump} " : $" Jumping: {CurrentJump} ";
            b.ForegroundColor = CharColor.Black;
            b.BackgroundColor = CharColor.White;
            b.SetText(t, sender.Rect.Right - (2 + t.Length));
        }

        b.FillRemaining();
    }

    public override void OnMouseEvent(MouseEvent e)
    {
        base.OnMouseEvent(e);
        Elements.OnMouseEvent(e);
    }

    public override void OnKeyEvent(KeyEvent e)
    {
        Debug.WriteLine(e.ToString());

        base.OnKeyEvent(e);
        Elements.OnKeyEvent(e);

        if (e.IsDown == 1 && (e.VirtualKeyCode == Win32.VirtualKeyCode.Tab || e.VirtualKeyCode == Win32.VirtualKeyCode.Space))
        {
            if (CurrentJump is null)
            {
                CurrentJump = new InstructionCountJump(1);
                return;
            }

            if (CurrentJump is InstructionCountJump instructionCountJump &&
                instructionCountJump.Current == instructionCountJump.Count)
            {
                instructionCountJump.Reset(1);
                return;
            }

            CurrentJump.IsPaused = !CurrentJump.IsPaused;

            return;
        }

        if (e.IsDown != 0 && e.VirtualKeyCode == Win32.VirtualKeyCode.Add)
        {
            if (CurrentJump is null)
            {
                CurrentJump = new InstructionCountJump(1)
                { IsPaused = true };
                return;
            }

            if (CurrentJump is InstructionCountJump instructionCountJump)
            {
                instructionCountJump.Reset(instructionCountJump.Count + 1);
            }

            return;
        }

        if (e.IsDown != 0 && e.VirtualKeyCode == Win32.VirtualKeyCode.Subtract)
        {
            if (CurrentJump is InstructionCountJump instructionCountJump)
            {
                if (instructionCountJump.Count <= 1)
                {
                    CurrentJump = null;
                }
                else
                {
                    instructionCountJump.Reset(instructionCountJump.Count - 1);
                }
            }

            return;
        }

        if (e.IsDown != 0 && e.VirtualKeyCode == Win32.VirtualKeyCode.Multiply)
        {
            CurrentJump = new InstructionCountJump(int.MaxValue)
            { IsPaused = true };

            return;
        }

        if (e.IsDown != 0 && e.VirtualKeyCode == Win32.VirtualKeyCode.Divide)
        {
            CurrentJump = null;

            return;
        }
    }

    public override void OnDestroy()
    {
        this.Interpreter.Dispose();
    }

    public override void RefreshSize()
    {
        base.RefreshSize();
        Elements[0].Rect = Rect;
        Elements[0].RefreshSize();
    }
}
