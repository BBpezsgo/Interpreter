using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public readonly struct CallStackFrame
    {
        public readonly string Function;
        public readonly string File;
        public readonly string InstructionOffset;
        public readonly string Line;

        public CallStackFrame(string frame)
        {
            string[] parts = frame.Split(';');
            this.Function = parts[0];
            this.File = parts[1];
            this.InstructionOffset = parts[2];
            this.Line = parts[3];
        }

        public override string ToString() => $"at {Function} in {File}:line {Line} instruction {InstructionOffset}";
    }

    public struct Context
    {
        public string[] RawCallStack;
        public CallStackFrame[] CallStack
        {
            get
            {
                CallStackFrame[] result = new CallStackFrame[RawCallStack.Length];
                for (int i = 0; i < result.Length; i++)
                { result[i] = new CallStackFrame(RawCallStack[i]); }
                return result;
            }
        }
        public int CodePointer;
        public int ExecutedInstructionCount;
        public Instruction[] Code;
        internal DataStack Stack;
    }

    public class BytecodeInterpreter
    {
        class UserInvoke
        {
            internal readonly int InstructionOffset;
            internal readonly DataItem[] Arguments;
            internal bool IsInvoking;
            internal Action<DataItem> Callback;

            internal UserInvoke(int instructionOffset, DataItem[] arguments, Action<DataItem> callback)
            {
                this.InstructionOffset = instructionOffset;
                this.Arguments = arguments;
                this.IsInvoking = false;
                this.Callback = callback;
            }
        }

        BytecodeInterpreterSettings Settings;

        BytecodeProcessor BytecodeProcessor;

        // Control
        bool CanExecuteCode => CodePointer < BytecodeProcessor.Memory.Code.Length;
        bool isExecuting;
        bool IsDestroyed;
        bool IsUserInvoking => UserInvokes.Count > 0 && UserInvokes.Peek().IsInvoking;
        int RemainingClockCycles;

        // Safety
        int LastInstructionPointer = -1;
        int EndlessSafe;

        readonly Queue<UserInvoke> UserInvokes = new();

        #region Public Properties

        public bool IsExecuting => isExecuting;
        public int CodePointer => BytecodeProcessor.CodePointer;
        public int BasePointer => BytecodeProcessor.BasePointer;
        public IReadOnlyStack<DataItem> Stack => BytecodeProcessor.Memory.Stack;
        public IReadOnlyHeap Heap => BytecodeProcessor.Memory.Heap;
        public int StackMemorySize => BytecodeProcessor.Memory.Stack.UsedVirtualMemory;
        public string[] CallStack => BytecodeProcessor.Memory.CallStack.ToArray();

        #endregion

        internal BytecodeInterpreter(Instruction[] code, Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreterSettings settings)
        {
            this.Settings = settings;

            this.BytecodeProcessor = new BytecodeProcessor(code, 0, settings.HeapSize, builtinFunctions);

            this.isExecuting = false;
            this.IsDestroyed = false;
            this.RemainingClockCycles = this.Settings.ClockCyclesPerUpdate;

            this.EndlessSafe = 0;
            this.LastInstructionPointer = -1;
        }

        #region Public Methods

        internal void Jump(int instructionOffset)
        {
            if (IsDestroyed) return;
            if (isExecuting) return;

            isExecuting = true;

            BytecodeProcessor.CodePointer = instructionOffset;
            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        internal void Call(int instructionOffset, Action<DataItem> callback, params DataItem[] arguments)
        {
            if (IsDestroyed) return;

            UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));
            TryUserInvoke();
        }

        internal void Destroy()
        {
            if (IsDestroyed) return;
            BytecodeProcessor.Destroy();
            BytecodeProcessor = null;
            IsDestroyed = true;
        }

        /// <exception cref="RuntimeException"></exception>
        internal void Tick()
        {
            if (!CanExecuteCode || IsDestroyed) return;
            RemainingClockCycles = Math.Min(RemainingClockCycles + Settings.ClockCyclesPerUpdate, Settings.ClockCyclesPerUpdate);
            while (ExecuteNext())
            {

            }
        }

        internal void AddValueToStack(DataItem value)
        {
            if (IsDestroyed) return;
            BytecodeProcessor.Memory.Stack.Push(value);
        }

        internal Context GetContext() => new()
        {
            RawCallStack = this.BytecodeProcessor.Memory.CallStack.ToArray(),
            ExecutedInstructionCount = this.EndlessSafe,
            CodePointer = this.BytecodeProcessor.CodePointer,
            Code = this.BytecodeProcessor.Memory.Code[Math.Clamp(this.BytecodeProcessor.CodePointer - 20, 0, this.BytecodeProcessor.Memory.Code.Length - 1)..Math.Clamp(this.BytecodeProcessor.CodePointer + 20, 0, this.BytecodeProcessor.Memory.Code.Length - 1)],
            Stack = this.BytecodeProcessor.Memory.Stack,
        };

        #endregion

        void PrepareUserInvoke(UserInvoke userInvoke)
        {
            BytecodeProcessor.CodePointer = userInvoke.InstructionOffset;
            BytecodeProcessor.Memory.Stack.Push(new DataItem(0, "return value"));
            BytecodeProcessor.Memory.Stack.PushRange(userInvoke.Arguments, "arg");

            BytecodeProcessor.Memory.Stack.Push(new DataItem(0, "saved base pointer"));
            BytecodeProcessor.Memory.ReturnAddressStack.Push(BytecodeProcessor.End());
            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        bool TryUserInvoke()
        {
            if (IsDestroyed) return false;
            if (isExecuting) return false;
            if (UserInvokes.Count == 0) return false;
            if (IsUserInvoking) return false;

            PrepareUserInvoke(UserInvokes.Peek());
            UserInvokes.Peek().IsInvoking = true;

            isExecuting = true;

            return true;
        }

        /// <exception cref="RuntimeException"></exception>
        bool ExecuteNext()
        {
            if (IsDestroyed) return false;

            if (EndlessSafe > Settings.InstructionLimit)
            {
                BytecodeProcessor.CodePointer = BytecodeProcessor.Memory.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (BytecodeProcessor.Memory.Stack.Count > Settings.StackMaxSize)
            {
                throw new RuntimeException("Stack size exceed the StackMaxSize", GetContext());
            }

            if (!CanExecuteCode)
            {
                OnStop();
                return false;
            }

            if (BytecodeProcessor.CurrentInstruction.opcode == Opcode.COMMENT)
            {
                BytecodeProcessor.Step();
                return true;
            }

            EndlessSafe++;

            try
            {
                RemainingClockCycles -= Math.Max(1, BytecodeProcessor.Clock());
            }
            catch (UserException error)
            {
                error.Context = GetContext();
                throw;
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

            if (LastInstructionPointer == BytecodeProcessor.CodePointer)
            {
                Output.Debug.Debug.LogWarning($"Possible endless loop! Instruction: " + BytecodeProcessor.CurrentInstruction.ToString());
            }

            LastInstructionPointer = BytecodeProcessor.CodePointer;

            isExecuting = true;

            if (RemainingClockCycles > 0)
            {
                return true;
            }
            return false;
        }

        void OnStop()
        {
            if (IsDestroyed) return;

            if (IsUserInvoking)
            {
                UserInvoke userInvoke = UserInvokes.Dequeue();
                for (int i = 0; i < userInvoke.Arguments.Length; i++)
                {
                    if (BytecodeProcessor.Memory.Stack.Count == 0)
                    { throw new InternalException($"Tried to pop user-invoked function's parameters but the stack is empty"); }
                    BytecodeProcessor.Memory.Stack.Pop();
                }
                if (BytecodeProcessor.Memory.Stack.Count == 0)
                { throw new InternalException($"Tried to pop user-invoked function's return value but the stack is empty"); }
                DataItem returnValue = BytecodeProcessor.Memory.Stack.Pop();
                userInvoke.Callback?.Invoke(returnValue);
            }

            LastInstructionPointer = -1;
            EndlessSafe = 0;

            isExecuting = false;

            TryUserInvoke();
        }

        internal int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
        {
            AddressingMode.ABSOLUTE => offset,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + offset,
            AddressingMode.RELATIVE => BytecodeProcessor.Memory.Stack.Count + offset,
            AddressingMode.POP => BytecodeProcessor.Memory.Stack.Count - 1,
            AddressingMode.RUNTIME => BytecodeProcessor.Memory.Stack.Last().ValueInt,
            _ => offset,
        };
    }

    public struct BytecodeInterpreterSettings
    {
        internal int ClockCyclesPerUpdate;
        internal int InstructionLimit;
        internal int StackMaxSize;
        internal int HeapSize;

        public static BytecodeInterpreterSettings Default => new()
        {
            ClockCyclesPerUpdate = int.MaxValue,
            InstructionLimit = 8192,
            StackMaxSize = 128,
            HeapSize = 2048,
        };
    }

}
