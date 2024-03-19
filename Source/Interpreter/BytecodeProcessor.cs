using System.Collections.Frozen;

namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
{
    public ImmutableArray<int> CallTrace { get; init; }
    public int CodePointer { get; init; }
    public ImmutableArray<Instruction> Code { get; init; }
    public IReadOnlyStack<DataItem> Stack { get; init; }
    public int CodeSampleStart { get; init; }
}

public sealed class UserInvoke
{
    public int InstructionOffset { get; }
    public ImmutableArray<DataItem> Arguments { get; }
    public Action<DataItem> Callback { get; }
    public bool NeedReturnValue { get; }
    public bool IsInvoking { get; set; }

    public UserInvoke(int instructionOffset, IEnumerable<DataItem> arguments, Action<DataItem> callback)
    {
        InstructionOffset = instructionOffset;
        Arguments = arguments.ToImmutableArray();
        Callback = callback;
        NeedReturnValue = true;

        IsInvoking = false;
    }

    public UserInvoke(int instructionOffset, IEnumerable<DataItem> arguments, Action callback)
        : this(instructionOffset, arguments, _ => callback.Invoke())
    {
        NeedReturnValue = false;
    }
}

public struct BytecodeInterpreterSettings
{
    public int StackMaxSize;
    public int HeapSize;

    public static BytecodeInterpreterSettings Default => new()
    {
        StackMaxSize = 128,
        HeapSize = 2048,
    };
}

public interface ISleep
{
    /// <summary>
    /// Executes every time <see cref="BytecodeProcessor.Tick"/> is called.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if it is sleeping, <see langword="false"/> otherwise.
    /// </returns>
    public bool Tick();
}

public class TickSleep : ISleep
{
    readonly int _ticks;
    int _elapsed;

    public TickSleep(int ticks)
    {
        _ticks = ticks;
    }

    public bool Tick()
    {
        _elapsed++;
        return _elapsed < _ticks;
    }
}

public class TimeSleep : ISleep
{
    readonly double _timeout;
    readonly double _started;

    public TimeSleep(double timeout, double now)
    {
        _timeout = timeout;
        _started = now;
    }

    public TimeSleep(double timeout)
    {
        _timeout = timeout;
        _started = DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }

    public bool Tick()
    {
        double now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        return now - _started < _timeout;
    }
}

public class BytecodeProcessor
{
    const int CodeSampleRange = 20;

    Instruction CurrentInstruction => Memory.Code[CodePointer];
    public bool IsDone => CodePointer >= Memory.Code.Length;

    readonly BytecodeInterpreterSettings Settings;

    bool IsUserInvoking => UserInvokes.Count > 0 && UserInvokes.Peek().IsInvoking;
    readonly Queue<UserInvoke> UserInvokes;

    ISleep? Sleep;

    public Memory Memory;

    readonly FrozenDictionary<string, ExternalFunctionBase> ExternalFunctions;

    public int CodePointer;
    public int BasePointer;

    int LastInstructionPointer;

    public BytecodeProcessor(ImmutableArray<Instruction> code, FrozenDictionary<string, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings)
    {
        UserInvokes = new Queue<UserInvoke>();
        Settings = settings;
        LastInstructionPointer = -1;

        ExternalFunctions = externalFunctions;

        BasePointer = 0;
        CodePointer = 0;

        Memory = new(settings.HeapSize, code);
    }

    public void SetSleep(ISleep sleep) => Sleep = sleep;

    public void Call(int instructionOffset, Action<DataItem> callback, params DataItem[] arguments)
        => UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));
    public void Call(int instructionOffset, Action callback, params DataItem[] arguments)
        => UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));

    RuntimeContext GetContext() => new()
    {
        CallTrace = TraceCalls(),
        CodePointer = CodePointer,
        Code = Memory.Code[Math.Max(CodePointer - CodeSampleRange, 0)..Math.Clamp(CodePointer + CodeSampleRange, 0, Memory.Code.Length - 1)],
        Stack = Memory.Stack,
        CodeSampleStart = Math.Max(CodePointer - CodeSampleRange, 0),
    };

    public ImmutableArray<int> TraceCalls()
    {
        bool CanTraceCallsWith(int basePointer) =>
            basePointer >= 2 &&
            basePointer + BBCode.Generator.CodeGeneratorForMain.SavedCodePointerOffset < Memory.Stack.Count &&
            basePointer + BBCode.Generator.CodeGeneratorForMain.SavedBasePointerOffset < Memory.Stack.Count;

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

        if (!CanTraceCallsWith(BasePointer))
        { return ImmutableArray.Create<int>(); }

        List<int> callTrace = new();

        TraceCalls(callTrace, BasePointer);

        int[] callTraceResult = callTrace.ToArray();
        Array.Reverse(callTraceResult);
        return callTraceResult.ToImmutableArray();
    }

    public int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
    {
        AddressingMode.Absolute => offset,
        AddressingMode.BasePointerRelative => BasePointer + offset,
        AddressingMode.StackRelative => Memory.StackLength + offset,
        AddressingMode.Runtime => Memory.Stack.Last.VInt,
        _ => offset,
    };

    void Step() => CodePointer++;
    void Step(int num) => CodePointer += num;

    /// <exception cref="RuntimeException"/>
    /// <exception cref="UserException"/>
    /// <exception cref="InternalException"/>
    public bool Tick()
    {
        if (Memory.Stack.Count > Settings.StackMaxSize)
        { throw new RuntimeException("Stack size exceed the StackMaxSize", GetContext()); }

        if (Sleep is not null)
        {
            if (Sleep.Tick())
            { return true; }
            else
            { Sleep = null; }
        }

        if (IsDone)
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
                return true;
            }

            LastInstructionPointer = -1;

            if (UserInvokes.Count > 0)
            {
                UserInvoke userInvoke = UserInvokes.Peek();

                CodePointer = userInvoke.InstructionOffset;
                Memory.Stack.Push(new DataItem(0));
                Memory.Stack.PushRange(userInvoke.Arguments);

                Memory.Stack.Push(new DataItem(0));
                Memory.Stack.Push(new DataItem(Memory.Code.Length));

                BasePointer = Memory.Stack.Count;

                userInvoke.IsInvoking = true;
                return true;
            }

            return false;
        }

        try
        {
            Process();
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
        { throw new RuntimeException($"Execution stuck at instruction {LastInstructionPointer}", GetContext()); }

        LastInstructionPointer = CodePointer;

        return true;
    }

    /// <exception cref="RuntimeException"/>
    /// <exception cref="UserException"/>
    /// <exception cref="InternalException"/>
    /// <exception cref="Exception"/>
    void Process()
    {
        switch (CurrentInstruction.Opcode)
        {
            case Opcode._: throw new InternalException("Unknown instruction");

            case Opcode.Exit: EXIT(); break;

            case Opcode.Push: PUSH_VALUE(); break;
            case Opcode.Pop: POP_VALUE(); break;

            case Opcode.StackLoad: LOAD_VALUE(); break;
            case Opcode.StackStore: STORE_VALUE(); break;

            case Opcode.JumpIfZero: JUMP_BY_IF_FALSE(); break;
            case Opcode.Jump: JUMP_BY(); break;
            case Opcode.Throw: THROW(); break;

            case Opcode.Call: CALL(); break;
            case Opcode.Return: RETURN(); break;

            case Opcode.CallExternal: CALL_EXTERNAL(); break;

            case Opcode.MathAdd: MATH_ADD(); break;
            case Opcode.MathSub: MATH_SUB(); break;
            case Opcode.MathMult: MATH_MULT(); break;
            case Opcode.MathDiv: MATH_DIV(); break;
            case Opcode.MathMod: MATH_MOD(); break;

            case Opcode.BitsShiftLeft: BITSHIFT_LEFT(); break;
            case Opcode.BitsShiftRight: BITSHIFT_RIGHT(); break;

            case Opcode.BitsAND: BITS_AND(); break;
            case Opcode.BitsOR: BITS_OR(); break;
            case Opcode.BitsXOR: BITS_XOR(); break;
            case Opcode.BitsNOT: BITS_NOT(); break;

            case Opcode.LogicLT: LOGIC_LT(); break;
            case Opcode.LogicMT: LOGIC_MT(); break;
            case Opcode.LogicEQ: LOGIC_EQ(); break;
            case Opcode.LogicNEQ: LOGIC_NEQ(); break;
            case Opcode.LogicLTEQ: LOGIC_LTEQ(); break;
            case Opcode.LogicMTEQ: LOGIC_MTEQ(); break;
            case Opcode.LogicNOT: LOGIC_NOT(); break;
            case Opcode.LogicOR: LOGIC_OR(); break;
            case Opcode.LogicAND: LOGIC_AND(); break;

            case Opcode.HeapGet: HEAP_GET(); break;
            case Opcode.HeapSet: HEAP_SET(); break;

            case Opcode.Allocate: HEAP_ALLOC(); break;
            case Opcode.Free: HEAP_FREE(); break;

            case Opcode.GetBasePointer: GET_BASEPOINTER(); break;
            case Opcode.SetBasePointer: SET_BASEPOINTER(); break;

            case Opcode.SetCodePointer: SET_CODEPOINTER(); break;

            case Opcode.TypeGet: TYPE_GET(); break;
            case Opcode.TypeSet: TYPE_SET(); break;

            default: throw new UnreachableException();
        }
    }

    /// <exception cref="InternalException"/>
    int FetchStackAddress() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
        AddressingMode.Runtime => (int)Memory.Pop(),

        AddressingMode.BasePointerRelative => BasePointer + (int)CurrentInstruction.Parameter,
        AddressingMode.StackRelative => Memory.StackLength + (int)CurrentInstruction.Parameter,

        _ => throw new InternalException($"Invalid stack addressing mode {CurrentInstruction.AddressingMode}"),
    };

    /// <exception cref="InternalException"/>
    int FetchHeapAddress() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
        AddressingMode.Runtime => (int)Memory.Pop(),

        _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
    };

    /// <exception cref="InternalException"/>
    DataItem FetchData() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => CurrentInstruction.Parameter,
        AddressingMode.Runtime => Memory.Pop(),

        _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
    };

    #region Instruction Methods

    #region HEAP Operations

    void HEAP_ALLOC()
    {
        DataItem sizeData = Memory.Pop();
        int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_ALLOC, got {sizeData.Type}");

        int block = Memory.Allocate(size);

        Memory.Push(new DataItem(block));

        Step();
    }

    void HEAP_FREE()
    {
        DataItem pointerData = Memory.Pop();
        int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_DEALLOC, got {pointerData.Type}");

        Memory.Free(pointer);

        Step();
    }

    void HEAP_GET()
    {
        int address = FetchHeapAddress();
        DataItem value = Memory.HeapGet(address);
        Memory.Push(value);
        Step();
    }

    void HEAP_SET()
    {
        int address = FetchHeapAddress();
        DataItem value = Memory.Pop();
        Memory.HeapSet(address, value);
        Step();
    }

    #endregion

    #region Flow Control

    /// <exception cref="UserException"/>
    void THROW()
    {
        int pointer = (int)Memory.Pop();
        string? value = Memory.HeapGetString(pointer);
        Memory.Free(pointer);
        throw new UserException(value ?? "null");
    }

    void CALL()
    {
        int relativeAddress = (int)FetchData();

        Memory.Push(new DataItem(CodePointer));

        Step(relativeAddress);
    }

    void RETURN()
    {
        DataItem codePointer = Memory.Pop();

        CodePointer = (int)codePointer;
    }

    void JUMP_BY()
    {
        int relativeAddress = (int)FetchData();

        Step(relativeAddress);
    }

    void JUMP_BY_IF_FALSE()
    {
        int relativeAddress = (int)FetchData();

        DataItem condition = Memory.Pop();

        if (condition)
        { Step(); }
        else
        { Step(relativeAddress); }
    }

    void EXIT()
    {
        CodePointer = Memory.Code.Length;
    }

    #endregion

    #region Logic Operations

    void BITSHIFT_LEFT()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide << rightSide);

        Step();
    }

    void BITSHIFT_RIGHT()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide >> rightSide);

        Step();
    }

    void LOGIC_LT()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide < rightSide));

        Step();
    }

    void LOGIC_NOT()
    {
        DataItem v = Memory.Pop();
        Memory.Push(!v);
        Step();
    }

    void LOGIC_MT()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide > rightSide));

        Step();
    }

    void LOGIC_AND()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem((bool)leftSide && (bool)rightSide));

        Step();
    }

    void LOGIC_OR()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem((bool)leftSide || (bool)rightSide));

        Step();
    }

    void LOGIC_EQ()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide == rightSide));

        Step();
    }

    void LOGIC_NEQ()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide != rightSide));

        Step();
    }

    void BITS_OR()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide | rightSide);

        Step();
    }

    void BITS_XOR()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide ^ rightSide);

        Step();
    }

    void BITS_NOT()
    {
        DataItem rightSide = Memory.Pop();

        Memory.Push(~rightSide);

        Step();
    }

    void LOGIC_LTEQ()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide <= rightSide));

        Step();
    }

    void LOGIC_MTEQ()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(new DataItem(leftSide >= rightSide));

        Step();
    }

    void BITS_AND()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide & rightSide);

        Step();
    }

    #endregion

    #region Math Operations

    void MATH_ADD()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide + rightSide);

        Step();
    }

    void MATH_DIV()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide / rightSide);

        Step();
    }

    void MATH_SUB()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide - rightSide);

        Step();
    }

    void MATH_MULT()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide * rightSide);

        Step();
    }

    void MATH_MOD()
    {
        DataItem rightSide = Memory.Pop();
        DataItem leftSide = Memory.Pop();

        Memory.Push(leftSide % rightSide);

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        DataItem value = CurrentInstruction.Parameter;
        Memory.Push(value);

        Step();
    }

    void POP_VALUE()
    {
        Memory.Pop();

        Step();
    }

    void STORE_VALUE()
    {
        int address = FetchStackAddress();
        DataItem value = Memory.Pop();
        Memory.StackSet(address, value);

        Step();
    }

    void LOAD_VALUE()
    {
        int address = FetchStackAddress();
        DataItem value = Memory.StackGet(address);
        Memory.Push(value);

        Step();
    }

    #endregion

    #region Utility Operations

    void GET_BASEPOINTER()
    {
        Memory.Push(new DataItem(BasePointer));

        Step();
    }

    void SET_BASEPOINTER()
    {
        BasePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Memory.Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            AddressingMode.StackRelative => Memory.StackLength + (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SetBasePointer}"),
        };

        Step();
    }

    void SET_CODEPOINTER()
    {
        CodePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Memory.Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SetCodePointer}"),
        };
    }

    void TYPE_SET()
    {
        RuntimeType targetType = (RuntimeType)(byte)Memory.Pop();
        DataItem value = Memory.Pop();

        if (!DataItem.TryCast(ref value, targetType))
        { throw new RuntimeException($"Cannot cast {value.Type} to {targetType}"); }

        Memory.Push(value);

        Step();
    }

    void TYPE_GET()
    {
        DataItem value = Memory.Pop();
        byte type = (byte)value.Type;
        Memory.Push(new DataItem(type));

        Step();
    }

    #endregion

    #region External Calls

    void OnExternalReturnValue(DataItem returnValue)
    {
        Memory.Push(returnValue);
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    void CALL_EXTERNAL()
    {
        DataItem functionNameDataItem = Memory.Pop();
        if (functionNameDataItem.Type != RuntimeType.Integer)
        { throw new InternalException($"Instruction CALL_EXTERNAL need a String pointer (int) DataItem parameter from the stack, received {functionNameDataItem.Type} {functionNameDataItem}"); }

        string? functionName = Memory.HeapGetString((int)functionNameDataItem)
            ?? throw new RuntimeException($"Function name is null");

        if (!ExternalFunctions.TryGetValue(functionName, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined function \"{functionName}\""); }

        int parameterCount = (int)CurrentInstruction.Parameter;

        List<DataItem> parameters = new();
        for (int i = 1; i <= (int)CurrentInstruction.Parameter; i++)
        { parameters.Add(Memory.StackGet(^i)); }
        parameters.Reverse();

        if (function is ExternalFunctionManaged managedFunction)
        {
            managedFunction.OnReturn = OnExternalReturnValue;
            managedFunction.Callback(parameters.ToArray());
        }
        else if (function is ExternalFunctionSimple simpleFunction)
        {
            if (function.ReturnSomething)
            {
                DataItem returnValue = simpleFunction.Call(this, parameters.ToArray());
                // returnValue.Tag ??= $"{function.Name}() result";
                Memory.Push(returnValue);
            }
            else
            {
                simpleFunction.Call(this, parameters.ToArray());
            }
        }

        Step();
    }

    #endregion

    #endregion
}

public readonly struct Memory
{
    public readonly Stack<DataItem> Stack;
    public readonly HEAP Heap;
    public readonly ImmutableArray<Instruction> Code;

    public readonly int StackLength => Stack.Count;
    public readonly int HeapSize => Heap.Size;

    public readonly void HeapSet(int index, DataItem data) => Heap[index] = data;
    public readonly DataItem HeapGet(int index) => Heap[index];
    public readonly string? HeapGetString(int pointer) => Heap.GetString(pointer);
    public readonly int Allocate(int size) => Heap.Allocate(size);
    public readonly void Free(int pointer) => Heap.Deallocate(pointer);

    public readonly void StackSet(int index, DataItem data) => Stack[index] = data;
    public readonly DataItem StackGet(int index) => Stack[index];
    public readonly DataItem StackGet(Index index) => Stack[index];
    public readonly void Push(DataItem data) => Stack.Push(data);
    public readonly DataItem Pop() => Stack.Pop();

    public Memory(int heapSize, ImmutableArray<Instruction> code)
    {
        Code = code;

        Stack = new Stack<DataItem>();
        Heap = new HEAP(heapSize);
    }
}
