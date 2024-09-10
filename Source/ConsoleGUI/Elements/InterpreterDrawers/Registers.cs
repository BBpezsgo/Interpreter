using LanguageCore.Runtime;
using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    bool _state_RegisterA_Hex;
    bool _state_RegisterB_Hex;
    bool _state_RegisterC_Hex;
    bool _state_RegisterD_Hex;

    void RegistersElement_OnBeforeDraw(InlineElement sender)
    {
        bool focused = _focusedElement == 0;
        sender.IsFocused = focused;

        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        HashSet<Register> loadIndicators = new();
        HashSet<Register> storeIndicators = new();

        if (Interpreter.NextInstruction.HasValue)
        {
            Instruction instruction = Interpreter.NextInstruction.Value;

            void HandleDestinationOperand(InstructionOperand operand)
            {
                if (operand.Type != InstructionOperandType.Register)
                { return; }
                storeIndicators.Add((Register)operand.Value.I32);
            }

            void HandleSourceOperand(InstructionOperand operand)
            {
                if (operand.Type != InstructionOperandType.Register)
                { return; }
                loadIndicators.Add((Register)operand.Value.I32);
            }

            switch (instruction.Opcode)
            {
                case Opcode.PopTo8:
                case Opcode.PopTo16:
                case Opcode.PopTo32:
                    HandleDestinationOperand(instruction.Operand1);
                    break;
                case Opcode.Call:
                case Opcode.CallExternal:
                case Opcode.Push:
                case Opcode.Throw:
                    HandleSourceOperand(instruction.Operand1);
                    break;
                case Opcode.Compare:
                    HandleSourceOperand(instruction.Operand1);
                    HandleSourceOperand(instruction.Operand2);
                    break;
                case Opcode.BitsNOT:
                    HandleDestinationOperand(instruction.Operand1);
                    HandleSourceOperand(instruction.Operand1);
                    break;
                case Opcode.LogicOR:
                case Opcode.LogicAND:
                case Opcode.BitsAND:
                case Opcode.BitsOR:
                case Opcode.BitsXOR:
                case Opcode.BitsShiftLeft:
                case Opcode.BitsShiftRight:
                case Opcode.MathAdd:
                case Opcode.MathSub:
                case Opcode.MathMult:
                case Opcode.MathDiv:
                case Opcode.MathMod:
                case Opcode.FMathAdd:
                case Opcode.FMathSub:
                case Opcode.FMathMult:
                case Opcode.FMathDiv:
                case Opcode.FMathMod:
                case Opcode.Move:
                case Opcode.FTo:
                case Opcode.FFrom:
                    HandleDestinationOperand(instruction.Operand1);
                    HandleSourceOperand(instruction.Operand2);
                    break;
            }
        }

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        b.AddText(' ', 2);
        b.AddText("CP:");
        if (loadIndicators.Contains(Register.CodePointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('○');
        }
        else if (storeIndicators.Contains(Register.CodePointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('●');
        }
        else
        {
            b.AddChar(' ');
        }
        b.ForegroundColor = CharColor.BrightCyan;
        b.AddText(Interpreter.BytecodeInterpreter.Registers.CodePointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        b.AddText(' ', 2);
        b.AddText("SP:");
        if (loadIndicators.Contains(Register.StackPointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('○');
        }
        else if (storeIndicators.Contains(Register.StackPointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('●');
        }
        else
        {
            b.AddChar(' ');
        }
        b.ForegroundColor = CharColor.BrightCyan;
        b.AddText(Interpreter.BytecodeInterpreter.Registers.StackPointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        b.AddText(' ', 2);
        b.AddText("BP:");
        if (loadIndicators.Contains(Register.BasePointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('○');
        }
        else if (storeIndicators.Contains(Register.BasePointer))
        {
            b.ForegroundColor = CharColor.Red;
            b.AddChar('●');
        }
        else
        {
            b.AddChar(' ');
        }
        b.ForegroundColor = CharColor.BrightCyan;
        b.AddText(Interpreter.BytecodeInterpreter.Registers.BasePointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        //  A:  4294967295       

        void DrawGeneralRegister(char name, int @long)
        {
            SmallRect rect = new(
                Rect.Left + b.CurrentColumn + 5,
                Rect.Top + b.CurrentLine + 1,
                18,
                0
            );

            if (rect.Contains(ConsoleMouse.LeftPressedAt) &&
                rect.Contains(ConsoleMouse.RecordedConsolePosition) &&
                ConsoleMouse.IsUp(MouseButton.Left))
            {
                switch (name)
                {
                    case 'A': _state_RegisterA_Hex = !_state_RegisterA_Hex; break;
                    case 'B': _state_RegisterB_Hex = !_state_RegisterB_Hex; break;
                    case 'C': _state_RegisterC_Hex = !_state_RegisterC_Hex; break;
                    case 'D': _state_RegisterD_Hex = !_state_RegisterD_Hex; break;
                    default: throw new UnreachableException();
                }
            }

            bool isHex = name switch
            {
                'A' => _state_RegisterA_Hex,
                'B' => _state_RegisterB_Hex,
                'C' => _state_RegisterC_Hex,
                'D' => _state_RegisterD_Hex,
                _ => throw new UnreachableException(),
            };

            Register regDW = name switch
            {
                'A' => Register.EAX,
                'B' => Register.EBX,
                'C' => Register.ECX,
                'D' => Register.EDX,
                _ => throw new UnreachableException(),
            };
            Register regW = name switch
            {
                'A' => Register.AX,
                'B' => Register.BX,
                'C' => Register.CX,
                'D' => Register.DX,
                _ => throw new UnreachableException(),
            };
            Register regH = name switch
            {
                'A' => Register.AH,
                'B' => Register.BH,
                'C' => Register.CH,
                'D' => Register.DH,
                _ => throw new UnreachableException(),
            };
            Register regL = name switch
            {
                'A' => Register.AL,
                'B' => Register.BL,
                'C' => Register.CL,
                'D' => Register.DL,
                _ => throw new UnreachableException(),
            };

            b.AddText(' ', 2);
            b.AddText(name);
            b.AddText(": ");
            if (loadIndicators.Contains(regDW) ||
                loadIndicators.Contains(regW) ||
                loadIndicators.Contains(regH) ||
                loadIndicators.Contains(regL))
            {
                b.ForegroundColor = CharColor.Red;
                b.AddChar('○');
            }
            else if (storeIndicators.Contains(regDW) ||
                     storeIndicators.Contains(regW) ||
                     storeIndicators.Contains(regH) ||
                     storeIndicators.Contains(regL))
            {
                b.ForegroundColor = CharColor.Red;
                b.AddChar('●');
            }
            else
            {
                b.AddChar(' ');
            }
            b.ForegroundColor = CharColor.BrightCyan;
            b.AddText(isHex ? "0x" + Convert.ToString(@long, 16) : @long.ToString());
            b.AddSpace(24);
            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        DrawGeneralRegister('A', Interpreter.BytecodeInterpreter.Registers.EAX);
        DrawGeneralRegister('B', Interpreter.BytecodeInterpreter.Registers.EBX);
        DrawGeneralRegister('C', Interpreter.BytecodeInterpreter.Registers.ECX);
        DrawGeneralRegister('D', Interpreter.BytecodeInterpreter.Registers.EDX);
    }
}
