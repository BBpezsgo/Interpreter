using Win32.Console;

namespace ConsoleGUI;

public partial class InterpreterElement
{
    void StateElement_OnBeforeDraw(InlineElement sender)
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
}
