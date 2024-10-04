using System.Drawing;
using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
readonly struct DataMovement
{
    public readonly int Address;
    public readonly int Size;

    public DataMovement(int address, int size)
    {
        Address = address;
        Size = size;
    }

    public bool Contains(int address) => address >= Address && address < Address + Size;
}

interface IJump
{
    public bool IsPaused { get; set; }
    public bool ShouldDelete { get; }
    public bool ShouldJump(BytecodeProcessorEx interpreter);
    public void Tick();
}

[ExcludeFromCodeCoverage]
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

    public bool ShouldJump(BytecodeProcessorEx interpreter)
    {
        return Current < Count;
    }

    public void Tick()
    {
        Current++;
    }

    public override string ToString() => (Count - Current).ToString();
}

[ExcludeFromCodeCoverage]
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

    public bool ShouldJump(BytecodeProcessorEx interpreter)
    {
        if (interpreter.Processor.Registers.CodePointer == Instruction)
        {
            ShouldDelete = true;
            return false;
        }

        return true;
    }

    public void Tick() { }
}

[ExcludeFromCodeCoverage]
public sealed partial class InterpreterElement : WindowElement
{
    readonly BytecodeProcessorEx Interpreter;

    readonly ScrollBar HeapScrollBar;
    readonly ScrollBar StackScrollBar;

    IJump? CurrentJump;

    readonly MainThreadTimer InterpreterTimer;
    readonly StandardIOElement ConsolePanel;

    int _focusedElement;

    public InterpreterElement(BytecodeProcessorEx interpreter)
    {
        Interpreter = interpreter;

        ClearBuffer();

        InlineElement registersPanel = new()
        {
            HasBorder = true,
            Title = "Registers",
            Layout = new InlineLayout(InlineLayoutSizeMode.Fixed, 7),
        };
        registersPanel.OnBeforeDraw += RegistersElement_OnBeforeDraw;

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

        StackScrollBar = new ScrollBar((sender) => (0, Interpreter.Processor.Registers.StackPointer + (30 * BytecodeProcessor.StackDirection)), stackPanel);

        stackPanel.OnMouseEventInvoked += StackScrollBar.FeedEvent;
        stackPanel.OnKeyEventInvoked += (sender, e) =>
        {
            if (_focusedElement == 2)
            {
                StackScrollBar.FeedEvent(sender, e);
            }
        };

        InlineElement heapPanel = new()
        {
            HasBorder = true,
            Title = "HEAP",
        };

        HeapScrollBar = new ScrollBar((sender) => (0, Interpreter.Processor.Memory.Length - 3), heapPanel);

        heapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;
        heapPanel.OnMouseEventInvoked += HeapScrollBar.FeedEvent;
        heapPanel.OnKeyEventInvoked += (sender, e) =>
        {
            if (_focusedElement == 3)
            {
                HeapScrollBar.FeedEvent(sender, e);
            }
        };

        InlineElement callStackPanel = new()
        {
            HasBorder = true,
            Title = "Call Stack",
        };
        callStackPanel.OnBeforeDraw += CallStackElement_OnBeforeDraw;

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
                            registersPanel,
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

        Interpreter.IO.OnStdOut += (data) => ConsolePanel.Write(char.ToString(data));
        Interpreter.IO.OnNeedInput += () => ConsolePanel.BeginRead();

        ConsolePanel.OnInput += Interpreter.IO.SendKey;
    }

    void GetDataMovementIndicators(Instruction instruction, List<DataMovement> loadIndicators, List<DataMovement> storeIndicators)
    {
        switch (instruction.Opcode)
        {
            case Opcode.Push:
            {
                int size = (int)instruction.Operand1.BitWidth;
                int address = Interpreter.Processor.Registers.StackPointer + (size * BytecodeProcessor.StackDirection);
                storeIndicators.Add(new DataMovement(address, size));

                if (Interpreter.Processor.ResolveAddress(instruction.Operand1, out address))
                {
                    loadIndicators.Add(new DataMovement(address, size));
                }

                return;
            }
            case Opcode.Pop8:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 1;
                loadIndicators.Add(new DataMovement(address, size));
                return;
            }
            case Opcode.Pop16:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 2;
                loadIndicators.Add(new DataMovement(address, size));
                return;
            }
            case Opcode.Pop32:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 4;
                loadIndicators.Add(new DataMovement(address, size));
                return;
            }
            case Opcode.PopTo8:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 1;
                loadIndicators.Add(new DataMovement(address, size));

                if (Interpreter.Processor.ResolveAddress(instruction.Operand1, out address))
                { storeIndicators.Add(new DataMovement(address, size)); }

                return;
            }
            case Opcode.PopTo16:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 2;
                loadIndicators.Add(new DataMovement(address, size));

                if (Interpreter.Processor.ResolveAddress(instruction.Operand1, out address))
                { storeIndicators.Add(new DataMovement(address, size)); }

                return;
            }
            case Opcode.PopTo32:
            {
                int address = Interpreter.Processor.Registers.StackPointer;
                const int size = 4;
                loadIndicators.Add(new DataMovement(address, size));

                if (Interpreter.Processor.ResolveAddress(instruction.Operand1, out address))
                { storeIndicators.Add(new DataMovement(address, size)); }

                return;
            }
            case Opcode.Move:
            {
                if (instruction.Operand1.BitWidth == instruction.Operand2.BitWidth)
                {
                    int size = (int)instruction.Operand1.BitWidth;

                    if (Interpreter.Processor.ResolveAddress(instruction.Operand2, out int address))
                    { loadIndicators.Add(new DataMovement(address, size)); }

                    if (Interpreter.Processor.ResolveAddress(instruction.Operand1, out address))
                    { storeIndicators.Add(new DataMovement(address, size)); }
                }

                return;
            }
        }
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
            try
            {
                Interpreter.Tick();
            }
            catch (UserException error)
            {
                PrintOutput($"User Exception: {error}", LogType.Error);
            }
            catch (RuntimeException error)
            {
                PrintOutput($"Runtime Exception: {error}", LogType.Error);
            }
            catch (Exception error)
            {
                PrintOutput($"Internal Exception: {new RuntimeException(error.Message, error, Interpreter.Processor.GetContext(), Interpreter.DebugInformation)}", LogType.Error);
            }
            if (Interpreter.Processor.IsDone)
            {
                ConsoleGUI.Instance?.Dispose();
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

        if (e.IsDown == 1 && e.VirtualKeyCode == Win32.VirtualKeyCode.Tab)
        {
            if (e.ControlKeyState.HasFlag(Win32.ControlKeyState.Shift))
            { _focusedElement--; }
            else
            { _focusedElement++; }

            if (_focusedElement >= 5) _focusedElement = 0;
            if (_focusedElement < 0) _focusedElement = 4;
        }

        if (e.IsDown == 1 && (e.VirtualKeyCode == Win32.VirtualKeyCode.Space))
        {
            if (CurrentJump is null ||
                (CurrentJump is InstructionCountJump instructionCountJump1 && instructionCountJump1.Current == instructionCountJump1.Count))
            {
                CurrentJump = new BreakPointJump(Interpreter.Processor.Registers.CodePointer + 1, true);
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

    public override void RefreshSize()
    {
        base.RefreshSize();
        Elements[0].Rect = Rect;
        Elements[0].RefreshSize();
    }
}
