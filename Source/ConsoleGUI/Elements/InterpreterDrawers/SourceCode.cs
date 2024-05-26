using Win32.Console;

namespace ConsoleGUI;

using LanguageCore.Runtime;

public partial class InterpreterElement
{
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
                !breakPointJump.Invisible &&
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

            LinePrefix((i + 1).ToString());
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

            int operandCount = instruction.Opcode.ParameterCount();

            void WriteOperand(InstructionOperand operand)
            {
                switch (operand.Type)
                {
                    case InstructionOperandType.Immediate8:
                    case InstructionOperandType.Immediate16:
                    case InstructionOperandType.Immediate32:
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        break;
                    case InstructionOperandType.Pointer:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.Register:
                        switch ((Register)operand.Value.Int)
                        {
                            case Register.CodePointer:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("CP");
                                break;
                            case Register.StackPointer:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("SP");
                                break;
                            case Register.BasePointer:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("BP");
                                break;
                            case Register.EAX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("EAX");
                                break;
                            case Register.AX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("AX");
                                break;
                            case Register.AH:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("AH");
                                break;
                            case Register.AL:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("AL");
                                break;
                            case Register.EBX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("EBX");
                                break;
                            case Register.BX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("BX");
                                break;
                            case Register.BH:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("BH");
                                break;
                            case Register.BL:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("BL");
                                break;
                            case Register.ECX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("ECX");
                                break;
                            case Register.CX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("CX");
                                break;
                            case Register.CH:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("CH");
                                break;
                            case Register.CL:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("CL");
                                break;
                            case Register.EDX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("EDX");
                                break;
                            case Register.DX:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("DX");
                                break;
                            case Register.DH:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("DH");
                                break;
                            case Register.DL:
                                b.ForegroundColor = CharColor.White;
                                b.AddText("DL");
                                break;
                            default: throw new UnreachableException();
                        }
                        break;
                    case InstructionOperandType.PointerBP:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("BP");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.PointerSP:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("SP");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.PointerEAX:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("EAX");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.PointerEBX:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("EBX");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.PointerECX:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("ECX");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                    case InstructionOperandType.PointerEDX:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText('[');
                        b.ForegroundColor = CharColor.White;
                        b.AddText("EDX");
                        if (operand.Value.Int > 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText('+');
                            b.AddText(operand.Value.Int.ToString());
                        }
                        else if (operand.Value.Int < 0)
                        {
                            b.ForegroundColor = CharColor.BrightCyan;
                            b.AddText(operand.Value.Int.ToString());
                        }
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText(']');
                        break;
                }
            }

            if (operandCount >= 1)
            {
                WriteOperand(instruction.Operand1);
                b.AddText(' ');
            }

            if (operandCount >= 2)
            {
                b.ForegroundColor = CharColor.BrightCyan;
                WriteOperand(instruction.Operand2);
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
