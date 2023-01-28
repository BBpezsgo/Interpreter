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
            public int CodePointer => bytecodeInterpreter.BytecodeProcessor.CodePointer;
            public int BasePointer => bytecodeInterpreter.BytecodeProcessor.Memory.BasePointer;
            public int[] ReturnAddressStack => bytecodeInterpreter.BytecodeProcessor.Memory.ReturnAddressStack.ToArray();
            public DataItem[] Stack => bytecodeInterpreter.BytecodeProcessor.Memory.Stack.ToArray();
            public DataItem[] Heap => bytecodeInterpreter.BytecodeProcessor.Memory.Heap.ToArray();
            public int StackMemorySize => bytecodeInterpreter.BytecodeProcessor.Memory.Stack.UsedVirtualMemory;

            public InterpreterDetails(BytecodeInterpreter bytecodeInterpreter)
            {
                this.bytecodeInterpreter = bytecodeInterpreter;
            }
        }

        BytecodeInterpreterSettings settings;

        BytecodeProcessor BytecodeProcessor;
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

            BytecodeProcessor = new BytecodeProcessor(code, 0, builtinFunctions);

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

            BytecodeProcessor.Memory.CodePointer = instructionOffset;
            BytecodeProcessor.Memory.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        public void Call(int instructionOffset, params int[] arguments)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            this.arguments = arguments;
            currentlyRunning = true;
            enable = true;
            IsCall = true;

            BytecodeProcessor.Memory.CodePointer = instructionOffset;
            BytecodeProcessor.Memory.Stack.Push(0, "return value");
            BytecodeProcessor.Memory.Stack.PushRange(this.arguments, "arg");

            BytecodeProcessor.Memory.Stack.Push(0, "saved base pointer");
            BytecodeProcessor.Memory.ReturnAddressStack.Add(BytecodeProcessor.Memory.End());
            BytecodeProcessor.Memory.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        void ExecuteNext()
        {
            if (destroyed) return;

            if (endlessSafe > settings.InstructionLimit)
            {
                BytecodeProcessor.Memory.CodePointer = BytecodeProcessor.Memory.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (BytecodeProcessor.Memory.Stack.Count > settings.StackMaxSize)
            {
                throw new RuntimeException("Stack overflow!", GetContext());
            }

            if (BytecodeProcessor.CodePointer < BytecodeProcessor.Memory.End())
            {
                if (BytecodeProcessor.Memory.CurrentInstruction.opcode == Opcode.COMMENT)
                {
                    BytecodeProcessor.Memory.Step();
                    ExecuteNext();
                    return;
                }

                endlessSafe++;

                try
                {
                    remainingClockCycles -= Math.Max(1, BytecodeProcessor.Clock());
                }
                catch (RuntimeException error)
                {
                    error.Context = GetContext();
                    throw;
                }
                catch (System.Exception error)
                {
                    throw new RuntimeException(error.Message, error, GetContext());
                }

                if (lastInstrPointer == BytecodeProcessor.CodePointer)
                {
                    Output.Debug.Debug.LogWarning($"Possible endless loop! Instruction: " + BytecodeProcessor.Memory.CurrentInstruction.ToString());
                }

                lastInstrPointer = BytecodeProcessor.CodePointer;

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
            BytecodeProcessor.Destroy();
            BytecodeProcessor = null;
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
                    if (BytecodeProcessor.Memory.Stack.Count - 1 > i)
                        BytecodeProcessor.Memory.Stack.RemoveAt(BytecodeProcessor.Memory.Stack.Count - 1);
                }
                result = BytecodeProcessor.Memory.Stack.Pop().ValueInt;
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
            BytecodeProcessor.Memory.Stack.Push(value);
        }

        public void CallStackPush(string data) => this.BytecodeProcessor.Memory.CallStack.Push(data);
        public void CallStackPop() => this.BytecodeProcessor.Memory.CallStack.Pop();

        internal Context GetContext() => new()
        {
            CallStack = this.BytecodeProcessor.Memory.CallStack.ToArray(),
            ExecutedInstructionCount = this.endlessSafe,
            CodePointer = this.BytecodeProcessor.CodePointer,
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
            InstructionLimit = 8192,
            StackMaxSize = 128,
        };
    }

}
