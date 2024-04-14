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
    public bool Invisible { get; }

    public BreakPointJump(int instruction, bool invisible = false)
    {
        Instruction = instruction;
        Invisible = invisible;
    }

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
public sealed partial class InterpreterElement : WindowElement
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

        InterpreterTimer = new MainThreadTimer(0);
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
        int maxIterations = 32;
        while (maxIterations-- > 0)
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

    public override void Tick(double deltaTime)
    {
        OnInterpreterTimer();
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
            if (CurrentJump is null ||
                (CurrentJump is InstructionCountJump instructionCountJump1 && instructionCountJump1.Current == instructionCountJump1.Count))
            {
                CurrentJump = new BreakPointJump(Interpreter.BytecodeInterpreter.Registers.CodePointer + 1, true);
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

        if (e.IsDown == 1 && (e.VirtualKeyCode == Win32.VirtualKeyCode.Return))
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
