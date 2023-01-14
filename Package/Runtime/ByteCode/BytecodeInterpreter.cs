using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public class BytecodeInterpreter
    {
        internal class InterpreterDetails
        {
            readonly BytecodeInterpreter bytecodeInterpreter;
            public int CodePointer => bytecodeInterpreter.CPU.CodePointer;
            public int BasePointer => bytecodeInterpreter.CPU.MU.BasePointer;
            public int[] ReturnAddressStack => bytecodeInterpreter.CPU.MU.ReturnAddressStack.ToArray();
            public DataItem[] Stack => bytecodeInterpreter.CPU.MU.Stack.ToArray();
            public int StackMemorySize => bytecodeInterpreter.CPU.MU.Stack.UsedVirtualMemory;

            public InterpreterDetails(BytecodeInterpreter bytecodeInterpreter)
            {
                this.bytecodeInterpreter = bytecodeInterpreter;
            }
        }

        BytecodeInterpreterSettings settings;

        CentralProcessingUnit CPU;
        int[] arguments;

        // Running control
        bool enable;
        bool currentlyRunning;
        public bool IsRunning => currentlyRunning;
        bool destroyed;
        bool IsCall;
        int remainingClockCycles;

        // Safely
        int lastInstrPointer = -1;
        int endlessSafe;

        readonly InterpreterDetails details;
        internal InterpreterDetails Details => details;

        public BytecodeInterpreter(Instruction[] code, Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreterSettings settings)
        {
            this.settings = settings;

            CPU = new CentralProcessingUnit(code, 0, builtinFunctions);

            arguments = Array.Empty<int>();

            enable = false;
            currentlyRunning = false;
            destroyed = false;
            IsCall = false;
            remainingClockCycles = this.settings.ClockCyclesPerUpdate;

            endlessSafe = 0;
            lastInstrPointer = -1;

            details = new InterpreterDetails(this);
        }

        public void Jump(int instructionOffset)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            currentlyRunning = true;
            enable = true;
            IsCall = false;

            CPU.MU.CodePointer = instructionOffset;
            CPU.MU.BasePointer = CPU.MU.Stack.Count;
        }

        public void Call(int instructionOffset, params int[] arguments)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            this.arguments = arguments;
            currentlyRunning = true;
            enable = true;
            IsCall = true;

            CPU.MU.CodePointer = instructionOffset;
            CPU.MU.Stack.Push(0, "return value");
            CPU.MU.Stack.PushRange(this.arguments, "arg");

            CPU.MU.Stack.Push(0, "saved base pointer");
            CPU.MU.ReturnAddressStack.Add(CPU.MU.End());
            CPU.MU.BasePointer = CPU.MU.Stack.Count;
        }

        void ExecuteNext()
        {
            if (destroyed) return;

            if (endlessSafe > settings.InstructionLimit)
            {
                CPU.MU.CodePointer = CPU.MU.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (CPU.MU.Stack.Count > settings.StackMaxSize)
            {
                throw new RuntimeException("Stack overflow!", GetContext());
            }

            if (CPU.CodePointer < CPU.MU.End())
            {
                if (CPU.MU.CurrentInstruction.opcode == Opcode.COMMENT)
                {
                    CPU.MU.Step();
                    ExecuteNext();
                    return;
                }

                endlessSafe++;

                try
                {
                    remainingClockCycles -= Math.Max(1, CPU.Clock());
                }
                catch (RuntimeException error)
                {
                    error.Context = GetContext();
                    throw;
                }

                if (lastInstrPointer == CPU.CodePointer)
                {
                    Output.Debug.Debug.LogWarning($"Possible endless loop! Instruction: " + CPU.MU.CurrentInstruction.ToString());
                }

                lastInstrPointer = CPU.CodePointer;

                currentlyRunning = true;

                if (remainingClockCycles > 0)
                {
                    ExecuteNext();
                }
            }
            else
            {
                Shutdown(out _);
            }
        }

        public void Destroy()
        {
            if (destroyed) return;
            CPU.Destroy();
            CPU = null;
            destroyed = true;
        }

        void Shutdown(out int result)
        {
            result = -1;
            if (destroyed) return;

            if (IsCall)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (CPU.MU.Stack.Count - 1 > i)
                        CPU.MU.Stack.RemoveAt(CPU.MU.Stack.Count - 1);
                }
                result = CPU.MU.Stack.Pop().ValueInt;
            }

            lastInstrPointer = -1;
            endlessSafe = 0;
            currentlyRunning = false;
            enable = false;
        }

        public void Tick()
        {
            if (!enable || destroyed) return;
            remainingClockCycles = Math.Min(remainingClockCycles + settings.ClockCyclesPerUpdate, settings.ClockCyclesPerUpdate);
            ExecuteNext();
        }

        public void AddValueToStack(DataItem value)
        {
            if (destroyed) return;
            CPU.MU.Stack.Push(value);
        }

        public void CallStackPush(string data) => this.CPU.MU.CallStack.Push(data);
        public void CallStackPop() => this.CPU.MU.CallStack.Pop();

        internal Context GetContext() => new()
        {
            CallStack = this.CPU.MU.CallStack.ToArray(),
            ExecutedInstructionCount = this.endlessSafe,
            CodePointer = this.CPU.CodePointer,
        };
        public struct Context
        {
            public string[] CallStack;
            public int CodePointer;
            public int ExecutedInstructionCount;
        }
    }

    public struct BytecodeInterpreterSettings
    {
        internal int ClockCyclesPerUpdate;
        internal int InstructionLimit;
        internal int StackMaxSize;

        public static BytecodeInterpreterSettings Default => new()
        {
            ClockCyclesPerUpdate = 2,
            InstructionLimit = 1024,
            StackMaxSize = 128,
        };
    }

}
