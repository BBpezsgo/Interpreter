using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void RegistersElement_OnBeforeDraw(InlineElement sender)
    {
        sender.ClearBuffer();
        sender.DrawBuffer.StepTo(0);

        if (Interpreter.BytecodeInterpreter == null) return;

        DrawBuffer b = sender.DrawBuffer;

        b.ResetColor();

        b.AddText(' ', 2);
        b.AddText("CP: ");
        b.AddText(Interpreter.BytecodeInterpreter.Registers.CodePointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        b.AddText(' ', 2);
        b.AddText("SP: ");
        b.AddText(Interpreter.BytecodeInterpreter.Registers.StackPointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        b.AddText(' ', 2);
        b.AddText("BP: ");
        b.AddText(Interpreter.BytecodeInterpreter.Registers.BasePointer.ToString());
        b.BackgroundColor = CharColor.Black;
        b.FinishLine();
        b.ForegroundColor = CharColor.Silver;

        //  A: | 4294967295       |
        //     |        | 65535   |
        //     |        | 255| 255|

        void DrawGeneralRegister(char name, int @long, int @short, int high, int low)
        {
            b.AddText(' ', 2);
            b.AddText(name);
            b.AddText(": | ");
            b.AddText(@long.ToString());
            b.AddSpace(24);
            b.AddText("|");
            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;

            b.AddText(' ', 2);
            b.AddText("   | ");
            b.AddSpace(14);
            b.AddText("| ");
            b.AddText(@short.ToString());
            b.AddSpace(24);
            b.AddText("|");
            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;

            b.AddText(' ', 2);
            b.AddText("   | ");
            b.AddSpace(14);
            b.AddText("| ");
            b.AddText(high.ToString());
            b.AddSpace(19);
            b.AddText("| ");
            b.AddText(low.ToString());
            b.AddSpace(24);
            b.AddText("|");
            b.BackgroundColor = CharColor.Black;
            b.FinishLine();
            b.ForegroundColor = CharColor.Silver;
        }

        DrawGeneralRegister('A',
            Interpreter.BytecodeInterpreter.Registers.EAX,
            Interpreter.BytecodeInterpreter.Registers.AX,
            Interpreter.BytecodeInterpreter.Registers.AH,
            Interpreter.BytecodeInterpreter.Registers.AL);

        DrawGeneralRegister('B',
            Interpreter.BytecodeInterpreter.Registers.EBX,
            Interpreter.BytecodeInterpreter.Registers.BX,
            Interpreter.BytecodeInterpreter.Registers.BH,
            Interpreter.BytecodeInterpreter.Registers.BL);

        DrawGeneralRegister('C',
            Interpreter.BytecodeInterpreter.Registers.ECX,
            Interpreter.BytecodeInterpreter.Registers.CX,
            Interpreter.BytecodeInterpreter.Registers.CH,
            Interpreter.BytecodeInterpreter.Registers.CL);

        DrawGeneralRegister('D',
            Interpreter.BytecodeInterpreter.Registers.EDX,
            Interpreter.BytecodeInterpreter.Registers.DX,
            Interpreter.BytecodeInterpreter.Registers.DH,
            Interpreter.BytecodeInterpreter.Registers.DL);
    }
}
