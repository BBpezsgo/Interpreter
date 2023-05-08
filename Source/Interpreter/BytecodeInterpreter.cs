using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    using System.Linq;

    public class BytecodeInterpreter
    {
        BytecodeInterpreterSettings settings;

        BytecodeProcessor BytecodeProcessor;
        int argumentCount;

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

        #region Public Properties

        public int CodePointer => BytecodeProcessor.CodePointer;
        public int BasePointer => BytecodeProcessor.BasePointer;
        public int[] ReturnAddressStack => BytecodeProcessor.Memory.ReturnAddressStack.ToArray();
        public DataItem[] Stack => BytecodeProcessor.Memory.Stack.ToArray();
        public DataItem[] Heap => BytecodeProcessor.Memory.Heap.ToArray();
        public int StackMemorySize => BytecodeProcessor.Memory.Stack.UsedVirtualMemory;
        public string[] CallStack => BytecodeProcessor.Memory.CallStack.ToArray();

        #endregion

        internal BytecodeInterpreter(Instruction[] code, Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreterSettings settings)
        {
            this.settings = settings;

            this.BytecodeProcessor = new BytecodeProcessor(code, 0, builtinFunctions);

            this.argumentCount = 0;

            this.enable = false;
            this.currentlyRunning = false;
            this.destroyed = false;
            this.IsCall = false;
            this.remainingClockCycles = this.settings.ClockCyclesPerUpdate;

            this.endlessSafe = 0;
            this.lastInstrPointer = -1;
        }

        #region Public Methods

        internal void Jump(int instructionOffset)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            currentlyRunning = true;
            enable = true;
            IsCall = false;

            BytecodeProcessor.CodePointer = instructionOffset;
            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        internal void Call(int instructionOffset, params DataItem[] arguments)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            this.argumentCount = arguments.Length;
            currentlyRunning = true;
            enable = true;
            IsCall = true;

            BytecodeProcessor.CodePointer = instructionOffset;
            BytecodeProcessor.Memory.Stack.Push(0, "return value");
            BytecodeProcessor.Memory.Stack.PushRange(arguments, "arg");

            BytecodeProcessor.Memory.Stack.Push(0, "saved base pointer");
            BytecodeProcessor.Memory.ReturnAddressStack.Add(BytecodeProcessor.End());
            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        internal void Destroy()
        {
            if (destroyed) return;
            BytecodeProcessor.Destroy();
            BytecodeProcessor = null;
            destroyed = true;
        }

        internal void Tick()
        {
            if (!enable || destroyed) return;
            remainingClockCycles = Math.Min(remainingClockCycles + settings.ClockCyclesPerUpdate, settings.ClockCyclesPerUpdate);
            ExecuteNext();
        }

        internal void AddValueToStack(DataItem value)
        {
            if (destroyed) return;
            BytecodeProcessor.Memory.Stack.Push(value);
        }

        internal Context GetContext() => new()
        {
            CallStack = this.BytecodeProcessor.Memory.CallStack.ToArray(),
            ExecutedInstructionCount = this.endlessSafe,
            CodePointer = this.BytecodeProcessor.CodePointer,
        };
        internal struct Context
        {
            public string[] CallStack;
            public int CodePointer;
            public int ExecutedInstructionCount;
        }

        #endregion

        void ExecuteNext()
        {
            if (destroyed) return;

            if (endlessSafe > settings.InstructionLimit)
            {
                BytecodeProcessor.CodePointer = BytecodeProcessor.Memory.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (BytecodeProcessor.Memory.Stack.Count > settings.StackMaxSize)
            {
                throw new RuntimeException("Stack overflow!", GetContext());
            }

            if (BytecodeProcessor.CodePointer < BytecodeProcessor.End())
            {
                if (BytecodeProcessor.CurrentInstruction.opcode == Opcode.COMMENT)
                {
                    BytecodeProcessor.Step();
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
                    Output.Debug.Debug.LogWarning($"Possible endless loop! Instruction: " + BytecodeProcessor.CurrentInstruction.ToString());
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

        void Shutdown(out int result)
        {
            result = -1;
            if (destroyed) return;

            if (IsCall)
            {
                for (int i = 0; i < argumentCount; i++)
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

        internal int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
        {
            AddressingMode.ABSOLUTE => offset,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + offset,
            AddressingMode.RELATIVE => BytecodeProcessor.Memory.Stack.Count + offset,
            AddressingMode.POP => BytecodeProcessor.Memory.Stack.Count - 1,
            _ => offset,
        };
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
