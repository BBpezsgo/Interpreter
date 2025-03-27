using Win32.Console;
using LanguageCore.Runtime;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void SourceCodeElement_OnBeforeDraw(InlineElement sender)
    {
        bool focused = _focusedElement == 1;
        sender.IsFocused = focused;

        // sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

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
        if (!Interpreter.DebugInformation.IsEmpty)
        {
            for (int i = 0; i < this.Interpreter.Registers.CodePointer - 5; i++)
            {
                if (Interpreter.DebugInformation.CodeComments.TryGetValue(i, out ImmutableArray<string> comments))
                {
                    for (int j = 0; j < comments.Length; j++)
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
        for (int i = Math.Max(0, this.Interpreter.Registers.CodePointer - 5); i < this.Interpreter.Code.Length; i++)
        {
            if (Interpreter.Registers.CodePointer == i) IsNextInstruction = true;

            Instruction instruction = Interpreter.Code[i];

            if (!Interpreter.DebugInformation.IsEmpty)
            {
                if (Interpreter.DebugInformation.CodeComments.TryGetValue(i, out ImmutableArray<string> comments))
                {
                    for (int j = 0; j < comments.Length; j++)
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

            void WritePointerOperand(string size, string? @base, int offset)
            {
                b.ForegroundColor = CharColor.Gray;
                b.AddText(size);
                b.AddText(' ');
                b.AddText('[');
                if (@base != null)
                {
                    b.ForegroundColor = CharColor.White;
                    b.AddText(@base);
                }
                if (offset > 0)
                {
                    b.ForegroundColor = CharColor.BrightCyan;
                    b.AddText('+');
                    b.AddText(offset.ToString());
                }
                else if (offset < 0)
                {
                    b.ForegroundColor = CharColor.BrightCyan;
                    b.AddText(offset.ToString());
                }
                b.ForegroundColor = CharColor.Gray;
                b.AddText(']');
            }

            void WriteOperand(InstructionOperand operand)
            {
                switch (operand.Type)
                {
                    case InstructionOperandType.Immediate8:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText("BYTE ");
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        break;
                    case InstructionOperandType.Immediate16:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText("WORD ");
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        break;
                    case InstructionOperandType.Immediate32:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText("DWORD ");
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        break;
                    case InstructionOperandType.Immediate64:
                        b.ForegroundColor = CharColor.Gray;
                        b.AddText("QWORD ");
                        b.ForegroundColor = CharColor.BrightCyan;
                        b.AddText(operand.Value.ToString());
                        break;
                    case InstructionOperandType.Pointer8: WritePointerOperand("BYTE", null, operand.Value); break;
                    case InstructionOperandType.Pointer16: WritePointerOperand("WORD", null, operand.Value); break;
                    case InstructionOperandType.Pointer32: WritePointerOperand("DWORD", null, operand.Value); break;
                    case InstructionOperandType.Register:
                        switch ((Register)operand.Value)
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
                    case InstructionOperandType.PointerBP8: WritePointerOperand("BYTE", "BP", operand.Value); break;
                    case InstructionOperandType.PointerBP16: WritePointerOperand("WORD", "BP", operand.Value); break;
                    case InstructionOperandType.PointerBP32: WritePointerOperand("DWORD", "BP", operand.Value); break;
                    case InstructionOperandType.PointerBP64: WritePointerOperand("QWORD", "BP", operand.Value); break;
                    case InstructionOperandType.PointerSP8: WritePointerOperand("BYTE", "SP", operand.Value); break;
                    case InstructionOperandType.PointerSP16: WritePointerOperand("WORD", "SP", operand.Value); break;
                    case InstructionOperandType.PointerSP32: WritePointerOperand("DWORD", "SP", operand.Value); break;

                    case InstructionOperandType.PointerEAX8: WritePointerOperand("BYTE", "EAX", operand.Value); break;
                    case InstructionOperandType.PointerEAX16: WritePointerOperand("WORD", "EAX", operand.Value); break;
                    case InstructionOperandType.PointerEAX32: WritePointerOperand("DWORD", "EAX", operand.Value); break;
                    case InstructionOperandType.PointerEAX64: WritePointerOperand("QWORD", "EAX", operand.Value); break;
                    case InstructionOperandType.PointerEBX8: WritePointerOperand("BYTE", "EBX", operand.Value); break;
                    case InstructionOperandType.PointerEBX16: WritePointerOperand("WORD", "EBX", operand.Value); break;
                    case InstructionOperandType.PointerEBX32: WritePointerOperand("DWORD", "EBX", operand.Value); break;
                    case InstructionOperandType.PointerEBX64: WritePointerOperand("QWORD", "EBX", operand.Value); break;
                    case InstructionOperandType.PointerECX8: WritePointerOperand("BYTE", "ECX", operand.Value); break;
                    case InstructionOperandType.PointerECX16: WritePointerOperand("WORD", "ECX", operand.Value); break;
                    case InstructionOperandType.PointerECX32: WritePointerOperand("DWORD", "ECX", operand.Value); break;
                    case InstructionOperandType.PointerECX64: WritePointerOperand("QWORD", "ECX", operand.Value); break;
                    case InstructionOperandType.PointerEDX8: WritePointerOperand("BYTE", "EDX", operand.Value); break;
                    case InstructionOperandType.PointerEDX16: WritePointerOperand("WORD", "EDX", operand.Value); break;
                    case InstructionOperandType.PointerEDX32: WritePointerOperand("DWORD", "EDX", operand.Value); break;
                    case InstructionOperandType.PointerEDX64: WritePointerOperand("QWORD", "EDX", operand.Value); break;

                    case InstructionOperandType.PointerRAX8: WritePointerOperand("BYTE", "RAX", operand.Value); break;
                    case InstructionOperandType.PointerRAX16: WritePointerOperand("WORD", "RAX", operand.Value); break;
                    case InstructionOperandType.PointerRAX32: WritePointerOperand("DWORD", "RAX", operand.Value); break;
                    case InstructionOperandType.PointerRAX64: WritePointerOperand("QWORD", "RAX", operand.Value); break;
                    case InstructionOperandType.PointerRBX8: WritePointerOperand("BYTE", "RBX", operand.Value); break;
                    case InstructionOperandType.PointerRBX16: WritePointerOperand("WORD", "RBX", operand.Value); break;
                    case InstructionOperandType.PointerRBX32: WritePointerOperand("DWORD", "RBX", operand.Value); break;
                    case InstructionOperandType.PointerRBX64: WritePointerOperand("QWORD", "RBX", operand.Value); break;
                    case InstructionOperandType.PointerRCX8: WritePointerOperand("BYTE", "RCX", operand.Value); break;
                    case InstructionOperandType.PointerRCX16: WritePointerOperand("WORD", "RCX", operand.Value); break;
                    case InstructionOperandType.PointerRCX32: WritePointerOperand("DWORD", "RCX", operand.Value); break;
                    case InstructionOperandType.PointerRCX64: WritePointerOperand("QWORD", "RCX", operand.Value); break;
                    case InstructionOperandType.PointerRDX8: WritePointerOperand("BYTE", "RDX", operand.Value); break;
                    case InstructionOperandType.PointerRDX16: WritePointerOperand("WORD", "RDX", operand.Value); break;
                    case InstructionOperandType.PointerRDX32: WritePointerOperand("DWORD", "RDX", operand.Value); break;
                    case InstructionOperandType.PointerRDX64: WritePointerOperand("QWORD", "RDX", operand.Value); break;
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
