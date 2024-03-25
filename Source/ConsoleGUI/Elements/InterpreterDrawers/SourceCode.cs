using LanguageCore;
using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
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
}
