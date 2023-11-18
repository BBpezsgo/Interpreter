using System;
using System.Collections.Generic;

namespace LanguageCore.Runtime
{
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
        public int[] CallTrace;
        public int CodePointer;
        public int ExecutedInstructionCount;
        public Instruction[] Code;
        public DataStack Stack;
        public int CodeSampleStart;
    }

    class UserInvoke
    {
        public readonly int InstructionOffset;
        public readonly DataItem[] Arguments;
        public readonly Action<DataItem> Callback;
        public bool NeedReturnValue;

        public bool IsInvoking;

        public UserInvoke(int instructionOffset, DataItem[] arguments, Action<DataItem> callback)
        {
            this.InstructionOffset = instructionOffset;
            this.Arguments = arguments;
            this.Callback = callback;
            this.NeedReturnValue = true;

            this.IsInvoking = false;
        }

        public UserInvoke(int instructionOffset, DataItem[] arguments, Action callback)
        {
            this.InstructionOffset = instructionOffset;
            this.Arguments = arguments;
            this.Callback = (_) => callback?.Invoke();
            this.NeedReturnValue = false;

            this.IsInvoking = false;
        }
    }

    public class BytecodeInterpreter : BytecodeProcessor
    {
        readonly BytecodeInterpreterSettings Settings;

        // User Invoke
        bool IsUserInvoking => UserInvokes.Count > 0 && UserInvokes.Peek().IsInvoking;
        readonly Queue<UserInvoke> UserInvokes = new();

        // Tick Sleeping
        int SleepTickCounter;
        Action? SleepTickCallback;

        // Time Sleeping
        double SleepTimeCounter;
        Action? SleepTimeCallback;

        // Safety
        int LastInstructionPointer = -1;
        int EndlessSafe;

        public BytecodeInterpreter(Instruction[] code, Dictionary<string, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings) : base(code, settings.HeapSize, externalFunctions)
        {
            this.Settings = settings;

            this.EndlessSafe = 0;
            this.LastInstructionPointer = -1;
        }

        #region Public Methods

        public void SleepTicks(int ticks, Action? callback)
        {
            SleepTickCounter = ticks;
            SleepTickCallback = callback;

            SleepTimeCounter = 0;
            SleepTimeCallback = null;
        }

        public void SleepTime(double seconds, Action? callback)
        {
            SleepTimeCounter = DateTime.UtcNow.TimeOfDay.TotalSeconds + seconds;
            SleepTimeCallback = callback;

            SleepTickCounter = 0;
            SleepTickCallback = null;
        }

        public bool Jump(int instructionOffset)
        {
            if (!IsDone) return false;

            CodePointer = instructionOffset;
            BasePointer = Memory.Stack.Count;
            return true;
        }

        public bool Call(int instructionOffset, Action<DataItem> callback, params DataItem[] arguments)
        {
            UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));
            TryUserInvoke();
            return true;
        }

        public Context GetContext() => new()
        {
            CallTrace = TraceCalls(),
            ExecutedInstructionCount = this.EndlessSafe,
            CodePointer = this.CodePointer,
            Code = this.Memory.Code[Math.Max(this.CodePointer - 20, 0)..Math.Clamp(this.CodePointer + 20, 0, this.Memory.Code.Length - 1)],
            Stack = this.Memory.Stack,
            CodeSampleStart = Math.Max(this.CodePointer - 20, 0),
        };

        bool CanTraceCallsWith(int basePointer) =>
            basePointer >= 2 &&
            basePointer + BBCode.Generator.CodeGeneratorForMain.SavedCodePointerOffset < Memory.Stack.Count &&
            basePointer + BBCode.Generator.CodeGeneratorForMain.SavedBasePointerOffset < Memory.Stack.Count;

        public int[] TraceCalls()
        {
            if (!CanTraceCallsWith(BasePointer))
            { return Array.Empty<int>(); }

            List<int> callTrace = new();

            TraceCalls(callTrace, BasePointer);

            int[] callTraceResult;
            callTraceResult = callTrace.ToArray();
            Array.Reverse(callTraceResult);
            return callTraceResult;
        }

        void TraceCalls(List<int> callTrace, int basePointer)
        {
            if (!CanTraceCallsWith(basePointer)) return;

            DataItem savedCodePointerD = Memory.Stack[basePointer + BBCode.Generator.CodeGeneratorForMain.SavedCodePointerOffset];
            DataItem savedBasePointerD = Memory.Stack[basePointer + BBCode.Generator.CodeGeneratorForMain.SavedBasePointerOffset];

            if (!savedCodePointerD.Integer.HasValue) return;
            if (!savedBasePointerD.Integer.HasValue) return;

            int savedCodePointer = savedCodePointerD.Integer ?? 0;
            int savedBasePointer = savedBasePointerD.Integer ?? 0;

            callTrace.Add(savedCodePointer);

            if (savedBasePointer == BasePointer) return;
            TraceCalls(callTrace, savedBasePointer);
        }

        #endregion

        void DoUserInvoke(UserInvoke userInvoke)
        {
            CodePointer = userInvoke.InstructionOffset;
            Memory.Stack.Push(new DataItem(0));
            Memory.Stack.PushRange(userInvoke.Arguments);

            Memory.Stack.Push(new DataItem(0));
            Memory.Stack.Push(new DataItem(Memory.Code.Length));

            BasePointer = Memory.Stack.Count;
        }

        bool TryUserInvoke()
        {
            if (!IsDone) return false;
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
            { throw new RuntimeException("Instruction limit reached!", GetContext()); }

            if (Memory.Stack.Count > Settings.StackMaxSize)
            { throw new RuntimeException("Stack size exceed the StackMaxSize", GetContext()); }

            if (SleepTickCounter > 0)
            {
                SleepTickCounter--;

                if (SleepTickCounter <= 0)
                {
                    SleepTickCallback?.Invoke();
                    SleepTickCallback = null;
                }

                return true;
            }

            if (SleepTimeCounter != 0)
            {
                if (SleepTimeCounter < DateTime.UtcNow.TimeOfDay.TotalSeconds)
                {
                    SleepTimeCallback?.Invoke();
                    SleepTimeCallback = null;

                    SleepTimeCounter = 0;
                }

                return true;
            }

            if (IsDone)
            {
                OnStop();
                return false;
            }

            EndlessSafe++;

            try
            {
                base.Process();
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
            catch (Exception error)
            {
                throw new RuntimeException(error.Message, error, GetContext());
            }

            if (LastInstructionPointer == CodePointer)
            {
                throw new RuntimeException($"Execution stuck at instruction {LastInstructionPointer}", GetContext());
            }

            LastInstructionPointer = CodePointer;

            return true;
        }

        void OnStop()
        {
            if (IsUserInvoking)
            {
                UserInvoke userInvoke = UserInvokes.Dequeue();

                for (int i = 0; i < userInvoke.Arguments.Length; i++)
                {
                    if (Memory.Stack.Count == 0)
                    { throw new InternalException($"Tried to pop user-invoked function's parameters but the stack is empty"); }
                    Memory.Stack.Pop();
                }

                if (Memory.Stack.Count == 0)
                { throw new InternalException($"Tried to pop user-invoked function's return value but the stack is empty"); }

                DataItem returnValue = userInvoke.NeedReturnValue ? Memory.Stack.Pop() : DataItem.Null;
                userInvoke.Callback?.Invoke(returnValue);
            }

            LastInstructionPointer = -1;
            EndlessSafe = 0;

            TryUserInvoke();
        }

        public int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
        {
            AddressingMode.ABSOLUTE => offset,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + offset,
            AddressingMode.RELATIVE => Memory.Stack.Count + offset,
            AddressingMode.POP => Memory.Stack.Count - 1,
            AddressingMode.RUNTIME => Memory.Stack.Last.ValueSInt32,
            _ => offset,
        };
    }

    public struct BytecodeInterpreterSettings
    {
        public int InstructionLimit;
        public int StackMaxSize;
        public int HeapSize;

        public static BytecodeInterpreterSettings Default => new()
        {
            InstructionLimit = 8192,
            StackMaxSize = 128,
            HeapSize = 2048,
        };
    }
}
