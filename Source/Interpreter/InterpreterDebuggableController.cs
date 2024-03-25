
namespace LanguageCore.Runtime;

public class InterpreterDebuggabble : Interpreter
{
    public InterpreterDebuggabble(bool handleErrors, BytecodeInterpreterSettings settings, ImmutableArray<Instruction> program, DebugInformation? debugInformation) : base(handleErrors, settings, program, debugInformation)
    { }

    public int AbsoluteBreakpoint { get; set; } = int.MinValue;
    public int Breakpoint
    {
        get
        {
            if (DebugInformation is null ||
                !DebugInformation.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return -1; }

            return sourceLocation.SourcePosition.Range.Start.Line;
        }
        set
        {
            if (DebugInformation is null ||
                !DebugInformation.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return; }

            AbsoluteBreakpoint = sourceLocation.SourcePosition.Range.Start.Line;
        }
    }
    public bool StackOperation { get; private set; }
    public bool HeapOperation { get; private set; }
    public bool ExternalFunctionOperation { get; private set; }
    public bool AluOperation { get; private set; }

    public void DoUpdate()
    {
        Instruction? nextInstruction_ = NextInstruction;
        if (nextInstruction_.HasValue)
        {
            Instruction nextInstruction = nextInstruction_.Value;

            ExternalFunctionOperation =
                nextInstruction.Opcode == Opcode.CallExternal;

            StackOperation =
                nextInstruction.Opcode == Opcode.StackStore ||
                nextInstruction.Opcode == Opcode.Push ||
                nextInstruction.Opcode == Opcode.Pop ||
                nextInstruction.Opcode == Opcode.StackLoad;

            HeapOperation =
                nextInstruction.Opcode == Opcode.Allocate ||
                nextInstruction.Opcode == Opcode.Free ||
                nextInstruction.Opcode == Opcode.HeapGet ||
                nextInstruction.Opcode == Opcode.HeapSet;

            AluOperation =
                nextInstruction.Opcode == Opcode.BitsShiftLeft ||
                nextInstruction.Opcode == Opcode.BitsShiftRight ||

                nextInstruction.Opcode == Opcode.BitsAND ||
                nextInstruction.Opcode == Opcode.LogicEQ ||
                nextInstruction.Opcode == Opcode.LogicLT ||
                nextInstruction.Opcode == Opcode.LogicLTEQ ||
                nextInstruction.Opcode == Opcode.LogicMT ||
                nextInstruction.Opcode == Opcode.LogicMTEQ ||
                nextInstruction.Opcode == Opcode.LogicNEQ ||
                nextInstruction.Opcode == Opcode.LogicNOT ||
                nextInstruction.Opcode == Opcode.BitsOR ||
                nextInstruction.Opcode == Opcode.BitsXOR ||

                nextInstruction.Opcode == Opcode.MathAdd ||
                nextInstruction.Opcode == Opcode.MathDiv ||
                nextInstruction.Opcode == Opcode.MathMod ||
                nextInstruction.Opcode == Opcode.MathMult ||
                nextInstruction.Opcode == Opcode.MathSub;
        }
        else
        {
            ExternalFunctionOperation = false;
            StackOperation = false;
            HeapOperation = false;
        }
        Update();
    }

    public void StepInto()
    {
        AbsoluteBreakpoint = int.MinValue;
        DoUpdate();
    }

    /// <exception cref="EndlessLoopException"/>
    public void Continue(int endlessSafe = 1024)
    {
        while (true)
        {
            DoUpdate();

            if (BytecodeInterpreter.Registers.CodePointer == AbsoluteBreakpoint) break;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }
    }
}
