using System.Collections;

namespace LanguageCore.Runtime;

public readonly struct Record<T>
{
    public readonly T Value;
    public readonly TimeSpan Time;

    public Record(T value, TimeSpan time)
    {
        Value = value;
        Time = time;
    }
}

public class Records<T> : IReadOnlyList<Record<T>>
{
    readonly List<Record<T>> _records;
    readonly int _maxSize = 100;

    public int Count => _records.Count;
    public Record<T> this[int i] => _records[i];

    public Records() => _records = new List<Record<T>>();

    public void Add(T record)
    {
        _records.Add(new Record<T>(record, DateTime.Now.TimeOfDay));
        if (_records.Count > _maxSize)
        { _records.RemoveAt(0); }
    }
    public void Clear() => _records.Clear();

    public IEnumerator<Record<T>> GetEnumerator() => _records.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _records.GetEnumerator();
}

public class InterpreterDebuggabble : Interpreter
{
    public int AbsoluteBreakpoint { get; set; } = int.MinValue;
    public int Breakpoint
    {
        get
        {
            if (DebugInfo is null ||
                !DebugInfo.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return -1; }

            return sourceLocation.SourcePosition.Range.Start.Line;
        }
        set
        {
            if (DebugInfo is null ||
                !DebugInfo.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return; }

            AbsoluteBreakpoint = sourceLocation.SourcePosition.Range.Start.Line;
        }
    }
    public bool StackOperation { get; private set; }
    public bool HeapOperation { get; private set; }
    public bool ExternalFunctionOperation { get; private set; }
    public bool AluOperation { get; private set; }
    public readonly Records<float> HeapUsage = new();

    DebugInformation? DebugInfo => CompilerResult.DebugInfo;

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
        SampleHeap();
    }

    public void StepInto()
    {
        AbsoluteBreakpoint = int.MinValue;
        DoUpdate();
    }

    public void SampleHeap()
    {
        if (this.BytecodeInterpreter == null) return;
        {
            (int, int, int) heapDiagnostics = this.BytecodeInterpreter.Memory.Heap.Diagnostics();
            HeapUsage.Add((float)heapDiagnostics.Item1 / (float)this.BytecodeInterpreter.Memory.Heap.Size);
        }
    }

    /// <exception cref="EndlessLoopException"/>
    public void Continue(int endlessSafe = 1024)
    {
        while (true)
        {
            DoUpdate();

            // TODO: Remove ! operator
            if (BytecodeInterpreter!.CodePointer == AbsoluteBreakpoint) break;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }
    }
}
