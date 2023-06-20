using IngameCoding.Bytecode;

using System;
using System.Collections;
using System.Collections.Generic;

namespace IngameCoding.Core
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

        int MaxSize = 100;
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
        int _breakpoint = int.MinValue;

        public int Breakpoint
        {
            get => _breakpoint;
            set
            {
                if (value == int.MinValue)
                {
                    _breakpoint = int.MinValue;
                    return;
                }

                int endlessSafe = 8;

                _breakpoint = value;
                while (this.details.CompilerResult.compiledCode[_breakpoint].opcode == Bytecode.Opcode.COMMENT)
                {
                    _breakpoint++;

                    if (endlessSafe-- < 0) throw new Errors.EndlessLoopException();
                }
            }
        }

        public readonly Records<float> HeapUsage = new();

        public void Step()
        {
            Breakpoint = BytecodeInterpreter.CodePointer + 1;
            Continue();
            Breakpoint = int.MinValue;
        }

        public void StepInto()
        {
            Breakpoint = int.MinValue;
            Update();
            SampleHeap();
            Breakpoint = int.MinValue;
        }

        internal void SampleHeap()
        {
            if (this.Details.Interpreter.Heap == null) return;
            if (this.Details.Interpreter.Heap is HEAP heap)
            {
                var heapDiagnostics = heap.Diagnostics();
                HeapUsage.Add((float)heapDiagnostics.Item1 / (float)heap.Size);
            }
        }

        /// <exception cref="Errors.EndlessLoopException"></exception>
        public void Continue()
        {
            int endlessSafe = 1024;
            while (true)
            {
                Update();
                SampleHeap();

                if (BytecodeInterpreter.CodePointer == Breakpoint) break;

                if (endlessSafe-- < 0) throw new Errors.EndlessLoopException();
            }
        }

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        /// <param name="compiledCode"></param>
        public override void ExecuteProgram(Instruction[] compiledCode, BytecodeInterpreterSettings bytecodeInterpreterSettings)
        {
            BytecodeInterpreterSettings settigns = bytecodeInterpreterSettings;
            settigns.ClockCyclesPerUpdate = 1;
            base.ExecuteProgram(compiledCode, settigns);
        }
    }
}
