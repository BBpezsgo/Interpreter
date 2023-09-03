using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.Bytecode
{
    using Core;
    using Errors;

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
        public readonly CallStackFrame[] CallStack
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
        internal int CodeSampleStart;
    }

    public class BytecodeInterpreter
    {
        class UserInvoke
        {
            internal readonly int InstructionOffset;
            internal readonly DataItem[] Arguments;
            internal readonly Action<DataItem> Callback;
            internal bool NeedReturnValue;

            internal bool IsInvoking;

            internal UserInvoke(int instructionOffset, DataItem[] arguments, Action<DataItem> callback)
            {
                this.InstructionOffset = instructionOffset;
                this.Arguments = arguments;
                this.Callback = callback;
                this.NeedReturnValue = true;

                this.IsInvoking = false;
            }

            internal UserInvoke(int instructionOffset, DataItem[] arguments, Action callback)
            {
                this.InstructionOffset = instructionOffset;
                this.Arguments = arguments;
                this.Callback = (_) => callback?.Invoke();
                this.NeedReturnValue = false;

                this.IsInvoking = false;
            }
        }

        readonly BytecodeInterpreterSettings Settings;
        readonly BytecodeProcessor BytecodeProcessor;

        // User Invoke
        bool IsUserInvoking => UserInvokes.Count > 0 && UserInvokes.Peek().IsInvoking;
        readonly Queue<UserInvoke> UserInvokes = new();

        // Safety
        int LastInstructionPointer = -1;
        int EndlessSafe;

        #region Public Properties

        public bool IsDone => BytecodeProcessor.IsDone;
        public int CodePointer => BytecodeProcessor.CodePointer;
        public int BasePointer => BytecodeProcessor.BasePointer;
        public IReadOnlyStack<DataItem> Stack => BytecodeProcessor.Memory.Stack;
        public IReadOnlyHeap Heap => BytecodeProcessor.Memory.Heap;
        public string[] CallStack => BytecodeProcessor.Memory.CallStack.ToArray();

        #endregion

        public BytecodeInterpreter(Instruction[] code, Dictionary<string, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings)
        {
            this.Settings = settings;

            this.BytecodeProcessor = new BytecodeProcessor(code, settings.HeapSize, externalFunctions);

            this.EndlessSafe = 0;
            this.LastInstructionPointer = -1;
        }

        #region Public Methods

        internal bool Jump(int instructionOffset)
        {
            if (!BytecodeProcessor.IsDone) return false;

            BytecodeProcessor.CodePointer = instructionOffset;
            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
            return true;
        }

        public bool Call(int instructionOffset, Action<DataItem> callback, params DataItem[] arguments)
        {
            UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));
            TryUserInvoke();
            return true;
        }

        internal Context GetContext() => new()
        {
            RawCallStack = this.BytecodeProcessor.Memory.CallStack.ToArray(),
            ExecutedInstructionCount = this.EndlessSafe,
            CodePointer = this.BytecodeProcessor.CodePointer,
            Code = this.BytecodeProcessor.Memory.Code[Math.Max(this.BytecodeProcessor.CodePointer - 20, 0)..Math.Clamp(this.BytecodeProcessor.CodePointer + 20, 0, this.BytecodeProcessor.Memory.Code.Length - 1)],
            Stack = this.BytecodeProcessor.Memory.Stack,
            CodeSampleStart = Math.Max(this.BytecodeProcessor.CodePointer - 20, 0),
        };

        #endregion

        void DoUserInvoke(UserInvoke userInvoke)
        {
            BytecodeProcessor.CodePointer = userInvoke.InstructionOffset;
            BytecodeProcessor.Memory.Stack.Push(new DataItem(0, "return value"));
            BytecodeProcessor.Memory.Stack.PushRange(userInvoke.Arguments, "arg");

            BytecodeProcessor.Memory.Stack.Push(new DataItem(0, "saved base pointer"));
            BytecodeProcessor.Memory.Stack.Push(new DataItem(BytecodeProcessor.Memory.Code.Length, "saved code pointer"));

            BytecodeProcessor.BasePointer = BytecodeProcessor.Memory.Stack.Count;
        }

        bool TryUserInvoke()
        {
            if (!BytecodeProcessor.IsDone) return false;
            if (UserInvokes.Count == 0) return false;
            if (IsUserInvoking) return false;

            DoUserInvoke(UserInvokes.Peek());
            UserInvokes.Peek().IsInvoking = true;

            return true;
        }

        /// <exception cref="RuntimeException"/>
        public bool Tick()
        {
            if (EndlessSafe > Settings.InstructionLimit)
            {
                BytecodeProcessor.CodePointer = BytecodeProcessor.Memory.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (BytecodeProcessor.Memory.Stack.Count > Settings.StackMaxSize)
            {
                throw new RuntimeException("Stack size exceed the StackMaxSize", GetContext());
            }

            if (BytecodeProcessor.IsDone)
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
                BytecodeProcessor.Tick();
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
                throw new RuntimeException($"Execution stuck at instruction {LastInstructionPointer}", GetContext());
            }

            LastInstructionPointer = BytecodeProcessor.CodePointer;

            return true;
        }

        void OnStop()
        {
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

                DataItem returnValue = userInvoke.NeedReturnValue ? BytecodeProcessor.Memory.Stack.Pop() : DataItem.Null;
                userInvoke.Callback?.Invoke(returnValue);
            }

            LastInstructionPointer = -1;
            EndlessSafe = 0;

            TryUserInvoke();
        }

        internal int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
        {
            AddressingMode.ABSOLUTE => offset,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + offset,
            AddressingMode.RELATIVE => BytecodeProcessor.Memory.Stack.Count + offset,
            AddressingMode.POP => BytecodeProcessor.Memory.Stack.Count - 1,
            AddressingMode.RUNTIME => BytecodeProcessor.Memory.Stack.Last.ValueInt,
            _ => offset,
        };
    }

    public struct BytecodeInterpreterSettings
    {
        internal int InstructionLimit;
        internal int StackMaxSize;
        internal int HeapSize;

        public static BytecodeInterpreterSettings Default => new()
        {
            InstructionLimit = 8192,
            StackMaxSize = 128,
            HeapSize = 2048,
        };
    }
}
