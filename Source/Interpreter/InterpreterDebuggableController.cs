using System;
using System.Collections;
using System.Collections.Generic;

namespace LanguageCore.Runtime
{
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
        readonly List<Record<T>> records;

        readonly int MaxSize = 100;
        public int Count => records.Count;

        public Record<T> this[int i] => records[i];

        public Records() => records = new List<Record<T>>();

        public void Add(T record)
        {
            records.Add(new Record<T>(record, DateTime.Now.TimeOfDay));
            if (records.Count > MaxSize)
            { records.RemoveAt(0); }
        }
        public void Clear() => records.Clear();

        public IEnumerator<Record<T>> GetEnumerator() => records.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => records.GetEnumerator();
    }

    public class InterpreterDebuggabble : Interpreter
    {
        int _absoluteBreakpoint = int.MinValue;

        bool _stackOperation = false;
        bool _heapOperation = false;
        bool _externalFunctionOperation = false;
        bool _aluOperation = false;

        public int AbsoluteBreakpoint
        {
            get => _absoluteBreakpoint;
            set => _absoluteBreakpoint = value;
        }
        public int Breakpoint
        {
            get
            {
                if (!DebugInfo.TryGetSourceLocation(_absoluteBreakpoint, out SourceCodeLocation sourceLocation))
                { return -1; }

                return sourceLocation.SourcePosition.Start.Line;
            }
            set
            {
                if (!DebugInfo.TryGetSourceLocation(_absoluteBreakpoint, out SourceCodeLocation sourceLocation))
                { return; }

                AbsoluteBreakpoint = sourceLocation.SourcePosition.Start.Line;
            }
        }
        public bool StackOperation => _stackOperation;
        public bool HeapOperation => _heapOperation;
        public bool ExternalFunctionOperation => _externalFunctionOperation;
        public bool AluOperation => _aluOperation;
        public readonly Records<float> HeapUsage = new();

        DebugInformation DebugInfo => CompilerResult.DebugInfo;
        Instruction[] Code => CompilerResult.Code;

        public void DoUpdate()
        {
            Instruction nextInstruction = NextInstruction;
            if (nextInstruction != null)
            {
                _externalFunctionOperation =
                    nextInstruction.opcode == Opcode.CALL_EXTERNAL;

                _stackOperation =
                    nextInstruction.opcode == Opcode.STORE_VALUE ||
                    nextInstruction.opcode == Opcode.PUSH_VALUE ||
                    nextInstruction.opcode == Opcode.POP_VALUE ||
                    nextInstruction.opcode == Opcode.LOAD_VALUE;

                _heapOperation =
                    nextInstruction.opcode == Opcode.HEAP_ALLOC ||
                    nextInstruction.opcode == Opcode.HEAP_DEALLOC ||
                    nextInstruction.opcode == Opcode.HEAP_GET ||
                    nextInstruction.opcode == Opcode.HEAP_SET;

                _aluOperation =
                    nextInstruction.opcode == Opcode.BITSHIFT_LEFT ||
                    nextInstruction.opcode == Opcode.BITSHIFT_RIGHT ||

                    nextInstruction.opcode == Opcode.BITS_AND ||
                    nextInstruction.opcode == Opcode.LOGIC_EQ ||
                    nextInstruction.opcode == Opcode.LOGIC_LT ||
                    nextInstruction.opcode == Opcode.LOGIC_LTEQ ||
                    nextInstruction.opcode == Opcode.LOGIC_MT ||
                    nextInstruction.opcode == Opcode.LOGIC_MTEQ ||
                    nextInstruction.opcode == Opcode.LOGIC_NEQ ||
                    nextInstruction.opcode == Opcode.LOGIC_NOT ||
                    nextInstruction.opcode == Opcode.BITS_OR ||
                    nextInstruction.opcode == Opcode.BITS_XOR ||

                    nextInstruction.opcode == Opcode.MATH_ADD ||
                    nextInstruction.opcode == Opcode.MATH_DIV ||
                    nextInstruction.opcode == Opcode.MATH_MOD ||
                    nextInstruction.opcode == Opcode.MATH_MULT ||
                    nextInstruction.opcode == Opcode.MATH_SUB;
            }
            else
            {
                _externalFunctionOperation = false;
                _stackOperation = false;
                _heapOperation = false;
            }
            Update();
            SampleHeap();
        }

        public void StepInto()
        {
            AbsoluteBreakpoint = int.MinValue;
            DoUpdate();
        }

        internal void SampleHeap()
        {
            if (this.BytecodeInterpreter == null) return;
            if (this.BytecodeInterpreter.Memory.Heap == null) return;
            if (this.BytecodeInterpreter.Memory.Heap is HEAP heap)
            {
                (int, int, int) heapDiagnostics = heap.Diagnostics();
                HeapUsage.Add((float)heapDiagnostics.Item1 / (float)heap.Size);
            }
        }

        /// <exception cref="Errors.EndlessLoopException"></exception>
        public void Continue(int endlessSafe = 1024)
        {
            while (true)
            {
                DoUpdate();

                if (BytecodeInterpreter.CodePointer == AbsoluteBreakpoint) break;

                if (endlessSafe-- < 0) throw new EndlessLoopException();
            }
        }

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        /// <param name="compiledCode"></param>
        public override void ExecuteProgram(Instruction[] compiledCode, BytecodeInterpreterSettings bytecodeInterpreterSettings)
            => base.ExecuteProgram(compiledCode, bytecodeInterpreterSettings);
    }
}
