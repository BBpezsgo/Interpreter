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
            if (!DebugInfo.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return -1; }

            return sourceLocation.SourcePosition.Range.Start.Line;
        }
        set
        {
            if (!DebugInfo.TryGetSourceLocation(AbsoluteBreakpoint, out SourceCodeLocation sourceLocation))
            { return; }

            AbsoluteBreakpoint = sourceLocation.SourcePosition.Range.Start.Line;
        }
    }
    public bool StackOperation { get; private set; }
    public bool HeapOperation { get; private set; }
    public bool ExternalFunctionOperation { get; private set; }
    public bool AluOperation { get; private set; }
    public readonly Records<float> HeapUsage = new();

    DebugInformation DebugInfo => CompilerResult.DebugInfo;

    public void DoUpdate()
    {
        Instruction? nextInstruction_ = NextInstruction;
        if (nextInstruction_.HasValue)
        {
            Instruction nextInstruction = nextInstruction_.Value;

            ExternalFunctionOperation =
                nextInstruction.Opcode == Opcode.CALL_EXTERNAL;

            StackOperation =
                nextInstruction.Opcode == Opcode.STORE_VALUE ||
                nextInstruction.Opcode == Opcode.PUSH_VALUE ||
                nextInstruction.Opcode == Opcode.POP_VALUE ||
                nextInstruction.Opcode == Opcode.LOAD_VALUE;

            HeapOperation =
                nextInstruction.Opcode == Opcode.HEAP_ALLOC ||
                nextInstruction.Opcode == Opcode.HEAP_FREE ||
                nextInstruction.Opcode == Opcode.HEAP_GET ||
                nextInstruction.Opcode == Opcode.HEAP_SET;

            AluOperation =
                nextInstruction.Opcode == Opcode.BITS_SHIFT_LEFT ||
                nextInstruction.Opcode == Opcode.BITS_SHIFT_RIGHT ||

                nextInstruction.Opcode == Opcode.BITS_AND ||
                nextInstruction.Opcode == Opcode.LOGIC_EQ ||
                nextInstruction.Opcode == Opcode.LOGIC_LT ||
                nextInstruction.Opcode == Opcode.LOGIC_LTEQ ||
                nextInstruction.Opcode == Opcode.LOGIC_MT ||
                nextInstruction.Opcode == Opcode.LOGIC_MTEQ ||
                nextInstruction.Opcode == Opcode.LOGIC_NEQ ||
                nextInstruction.Opcode == Opcode.LOGIC_NOT ||
                nextInstruction.Opcode == Opcode.BITS_OR ||
                nextInstruction.Opcode == Opcode.BITS_XOR ||

                nextInstruction.Opcode == Opcode.MATH_ADD ||
                nextInstruction.Opcode == Opcode.MATH_DIV ||
                nextInstruction.Opcode == Opcode.MATH_MOD ||
                nextInstruction.Opcode == Opcode.MATH_MULT ||
                nextInstruction.Opcode == Opcode.MATH_SUB;
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

            if (BytecodeInterpreter!.CodePointer == AbsoluteBreakpoint) break;

            if (endlessSafe-- < 0) throw new EndlessLoopException();
        }
    }
}
