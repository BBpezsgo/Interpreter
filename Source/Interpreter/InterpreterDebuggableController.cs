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

    public void DoUpdate() => Update();

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
