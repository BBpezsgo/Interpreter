using System.Collections.Frozen;

namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
{
    public ImmutableArray<int> CallTrace { get; init; }
    public int CodePointer { get; init; }
    public ImmutableArray<Instruction> Code { get; init; }
    public IReadOnlyList<DataItem> Memory { get; init; }
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
    public static readonly int StackDirection = -1;
    public const int StackSize = 256;

    Instruction CurrentInstruction => Code[Registers.CodePointer];
    public bool IsDone => Registers.CodePointer >= Code.Length;

    readonly BytecodeInterpreterSettings Settings;

    bool IsUserInvoking => UserInvokes.Count > 0 && UserInvokes.Peek().IsInvoking;
    readonly Queue<UserInvoke> UserInvokes;

    ISleep? CurrentSleep;

    public Registers Registers;
    public readonly DataItem[] Memory;

    public ImmutableArray<Instruction> Code;

    public int StackStart => Settings.HeapSize;

    public IEnumerable<DataItem> EnumerateStack()
    {
        ArraySegment<DataItem> stack = GetStack(out bool shouldReverse);
        if (shouldReverse)
        { return stack.Reverse(); }
        else
        { return stack; }
    }

    public ArraySegment<DataItem> GetStack(out bool shouldReverse)
    {
        if (StackDirection > 0)
        {
            shouldReverse = false;
            return new ArraySegment<DataItem>(Memory)[StackStart..Registers.StackPointer];
        }
        else
        {
            shouldReverse = true;
            return new ArraySegment<DataItem>(Memory)[(Registers.StackPointer + 1)..];
        }
    }

    public Range<int> GetStackInterval(out bool isReversed)
    {
        if (StackDirection > 0)
        {
            isReversed = false;
            return new Range<int>(StackStart, Registers.StackPointer);
        }
        else
        {
            isReversed = true;
            return new Range<int>(Memory.Length - 1, Registers.StackPointer);
        }
    }

    readonly FrozenDictionary<int, ExternalFunctionBase> ExternalFunctions;

    public BytecodeProcessor(ImmutableArray<Instruction> code, FrozenDictionary<int, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings)
    {
        UserInvokes = new Queue<UserInvoke>();
        Settings = settings;
        ExternalFunctions = externalFunctions;

        Code = code;

        Memory = new DataItem[settings.HeapSize + StackSize];
        // HeapUtils.Init(Memory);

        if (StackDirection > 0)
        { Registers.StackPointer = Settings.HeapSize; }
        else
        { Registers.StackPointer = Memory.Length - 1; }

        externalFunctions.SetInterpreter(this);
    }

    public void Sleep(ISleep sleep) => CurrentSleep = sleep;

    public void Call(int instructionOffset, Action<DataItem> callback, params DataItem[] arguments)
        => UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));
    public void Call(int instructionOffset, Action callback, params DataItem[] arguments)
        => UserInvokes.Enqueue(new UserInvoke(instructionOffset, arguments, callback));

    RuntimeContext GetContext() => new()
    {
        CallTrace = TraceCalls(Memory, Registers.BasePointer),
        CodePointer = Registers.CodePointer,
        Code = Code[Math.Max(Registers.CodePointer - CodeSampleRange, 0)..Math.Clamp(Registers.CodePointer + CodeSampleRange, 0, Code.Length - 1)],
        Memory = Memory,
        CodeSampleStart = Math.Max(Registers.CodePointer - CodeSampleRange, 0),
    };

    public static ImmutableArray<int> TraceCalls(IReadOnlyList<DataItem> stack, int basePointer)
    {
        static bool CanTraceCallsWith(IReadOnlyList<DataItem> stack, int basePointer)
        {
            int savedCodePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * StackDirection);
            int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection);

            if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Count) return false;
            if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Count) return false;

            return true;
        }

        static void TraceCalls(IReadOnlyList<DataItem> stack, List<int> callTrace, int basePointer)
        {
            if (!CanTraceCallsWith(stack, basePointer)) return;

            DataItem savedCodePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * StackDirection)];
            DataItem savedBasePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection)];

            if (!savedCodePointerD.Integer.HasValue) return;
            if (!savedBasePointerD.Integer.HasValue) return;

            int savedCodePointer = savedCodePointerD.Integer.Value;
            int savedBasePointer = savedBasePointerD.Integer.Value;

            callTrace.Add(savedCodePointer);

            if (savedBasePointer == basePointer) return;
            TraceCalls(stack, callTrace, savedBasePointer);
        }

        if (!CanTraceCallsWith(stack, basePointer))
        { return ImmutableArray.Create<int>(); }

        List<int> callTrace = new();

        TraceCalls(stack, callTrace, basePointer);

        int[] callTraceResult = callTrace.ToArray();
        Array.Reverse(callTraceResult);
        return callTraceResult.ToImmutableArray();
    }

    public static ImmutableArray<int> TraceBasePointers(IReadOnlyList<DataItem> stack, int basePointer)
    {
        static bool CanTraceBPsWith(IReadOnlyList<DataItem> stack, int basePointer)
        {
            int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection);

            if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Count) return false;

            return true;
        }

        static void TraceBasePointers(List<int> result, IReadOnlyList<DataItem> stack, int basePointer)
        {
            if (!CanTraceBPsWith(stack, basePointer)) return;

            DataItem savedBasePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection)];
            if (savedBasePointerD.Type != RuntimeType.Integer) return;
            int newBasePointer = savedBasePointerD.VInt;
            result.Add(newBasePointer);
            if (newBasePointer == basePointer) return;
            TraceBasePointers(result, stack, newBasePointer);
        }

        if (!CanTraceBPsWith(stack, basePointer))
        { return ImmutableArray.Create<int>(); }

        List<int> result = new();
        TraceBasePointers(result, stack, basePointer);
        return result.ToImmutableArray();
    }

    void Step() => Registers.CodePointer++;
    void Step(int num) => Registers.CodePointer += num;

    /// <exception cref="RuntimeException"/>
    /// <exception cref="UserException"/>
    /// <exception cref="InternalException"/>
    public bool Tick()
    {
        // if (Registers.StackPointer > Settings.StackMaxSize)
        // { throw new RuntimeException("Stack size exceed the StackMaxSize", GetContext()); }

        if (CurrentSleep is not null)
        {
            if (CurrentSleep.Tick())
            { return true; }
            else
            { CurrentSleep = null; }
        }

        if (IsDone)
        {
            if (IsUserInvoking)
            {
                UserInvoke userInvoke = UserInvokes.Dequeue();

                for (int i = 0; i < userInvoke.Arguments.Length; i++)
                { Pop(); }

                DataItem returnValue = userInvoke.NeedReturnValue ? Pop() : DataItem.Null;
                userInvoke.Callback?.Invoke(returnValue);
                return true;
            }

            if (UserInvokes.Count > 0)
            {
                UserInvoke userInvoke = UserInvokes.Peek();

                Registers.CodePointer = userInvoke.InstructionOffset;
                Push(new DataItem(0));
                for (int i = 0; i < userInvoke.Arguments.Length; i++)
                { Push(userInvoke.Arguments[i]); }

                Push(new DataItem(0));
                Push(new DataItem(Code.Length));

                Registers.BasePointer = Registers.StackPointer;

                userInvoke.IsInvoking = true;
                return true;
            }

            return false;
        }

        int lastCodePointer = Registers.CodePointer;

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

        if (lastCodePointer == Registers.CodePointer)
        { throw new RuntimeException($"Execution stuck at instruction {lastCodePointer}", GetContext()); }

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

            case Opcode.GetRegister: GET_REGISTER(); break;

            default: throw new UnreachableException();
        }
    }

    #region Memory Manipulation

    public int GetAddress(Instruction instruction)
        => GetAddress(instruction.Parameter.Integer ?? 0, instruction.AddressingMode);

    public int GetAddress(int offset, AddressingMode addressingMode) => addressingMode switch
    {
        AddressingMode.Absolute => offset,
        AddressingMode.Runtime => Memory[Registers.StackPointer - StackDirection].VInt,
        AddressingMode.BasePointerRelative => Registers.BasePointer + offset,
        AddressingMode.StackPointerRelative => Registers.StackPointer + offset,

        _ => throw new UnreachableException(),
    };

    /// <exception cref="InternalException"/>
    int FetchAddress() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
        AddressingMode.Runtime => (int)Pop(),
        AddressingMode.BasePointerRelative => Registers.BasePointer + (int)CurrentInstruction.Parameter,
        AddressingMode.StackPointerRelative => Registers.StackPointer + (int)CurrentInstruction.Parameter,

        _ => throw new UnreachableException(),
    };

    /// <exception cref="InternalException"/>
    DataItem FetchData() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => CurrentInstruction.Parameter,
        AddressingMode.Runtime => Pop(),

        _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
    };

    void Push(DataItem data)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Memory[Registers.StackPointer] = data;
        Registers.StackPointer += StackDirection;
    }

    DataItem Pop()
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Registers.StackPointer -= StackDirection;
        return Memory[Registers.StackPointer];
    }

    #endregion

    #region Instruction Methods

    #region HEAP Operations

    void HEAP_ALLOC()
    {
        DataItem sizeData = Pop();
        int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode {nameof(Opcode.Allocate)}, got {sizeData.Type}");

        int block = HeapUtils.Allocate(Memory, size);

        Push(new DataItem(block));

        Step();
    }

    void HEAP_FREE()
    {
        DataItem pointerData = Pop();
        int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode {nameof(Opcode.Free)}, got {pointerData.Type}");

        HeapUtils.Deallocate(Memory, pointer);

        Step();
    }

    void HEAP_GET()
    {
        int address = FetchAddress();
        DataItem value = Memory[address];
        Push(value);
        Step();
    }

    void HEAP_SET()
    {
        int address = FetchAddress();
        DataItem value = Pop();
        Memory[address] = value;
        Step();
    }

    #endregion

    #region Flow Control

    /// <exception cref="UserException"/>
    void THROW()
    {
        int pointer = (int)Pop();
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? "null");
    }

    void CALL()
    {
        int relativeAddress = (int)FetchData();

        Push(new DataItem(Registers.CodePointer));

        Step(relativeAddress);
    }

    void RETURN()
    {
        DataItem codePointer = Pop();

        Registers.CodePointer = (int)codePointer;
    }

    void JUMP_BY()
    {
        int relativeAddress = (int)FetchData();

        Step(relativeAddress);
    }

    void JUMP_BY_IF_FALSE()
    {
        int relativeAddress = (int)FetchData();

        DataItem condition = Pop();

        if (condition)
        { Step(); }
        else
        { Step(relativeAddress); }
    }

    void EXIT()
    {
        Registers.CodePointer = Code.Length;
    }

    #endregion

    #region Logic Operations

    void BITSHIFT_LEFT()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide << rightSide);

        Step();
    }

    void BITSHIFT_RIGHT()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide >> rightSide);

        Step();
    }

    void LOGIC_LT()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide < rightSide));

        Step();
    }

    void LOGIC_NOT()
    {
        DataItem v = Pop();
        Push(!v);
        Step();
    }

    void LOGIC_MT()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide > rightSide));

        Step();
    }

    void LOGIC_AND()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem((bool)leftSide && (bool)rightSide));

        Step();
    }

    void LOGIC_OR()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem((bool)leftSide || (bool)rightSide));

        Step();
    }

    void LOGIC_EQ()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide == rightSide));

        Step();
    }

    void LOGIC_NEQ()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide != rightSide));

        Step();
    }

    void BITS_OR()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide | rightSide);

        Step();
    }

    void BITS_XOR()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide ^ rightSide);

        Step();
    }

    void BITS_NOT()
    {
        DataItem rightSide = Pop();

        Push(~rightSide);

        Step();
    }

    void LOGIC_LTEQ()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide <= rightSide));

        Step();
    }

    void LOGIC_MTEQ()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(new DataItem(leftSide >= rightSide));

        Step();
    }

    void BITS_AND()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide & rightSide);

        Step();
    }

    #endregion

    #region Math Operations

    void MATH_ADD()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide + rightSide);

        Step();
    }

    void MATH_DIV()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide / rightSide);

        Step();
    }

    void MATH_SUB()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide - rightSide);

        Step();
    }

    void MATH_MULT()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide * rightSide);

        Step();
    }

    void MATH_MOD()
    {
        DataItem rightSide = Pop();
        DataItem leftSide = Pop();

        Push(leftSide % rightSide);

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        DataItem value = CurrentInstruction.Parameter;
        Push(value);

        Step();
    }

    void POP_VALUE()
    {
        Pop();

        Step();
    }

    void STORE_VALUE()
    {
        int address = FetchAddress();
        DataItem value = Pop();
        Memory[address] = value;

        Step();
    }

    void LOAD_VALUE()
    {
        int address = FetchAddress();
        DataItem value = Memory[address];
        Push(value);

        Step();
    }

    #endregion

    #region Utility Operations

    void GET_BASEPOINTER()
    {
        Push(new DataItem(Registers.BasePointer));

        Step();
    }

    void SET_BASEPOINTER()
    {
        Registers.BasePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            AddressingMode.StackPointerRelative => Registers.StackPointer + (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SetBasePointer}"),
        };

        Step();
    }

    void SET_CODEPOINTER()
    {
        Registers.CodePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SetCodePointer}"),
        };
    }

    void TYPE_SET()
    {
        RuntimeType targetType = (RuntimeType)(byte)Pop();
        DataItem value = Pop();

        if (!DataItem.TryCast(ref value, targetType))
        { throw new RuntimeException($"Cannot cast {value.Type} to {targetType}"); }

        Push(value);

        Step();
    }

    void TYPE_GET()
    {
        DataItem value = Pop();
        byte type = (byte)value.Type;
        Push(new DataItem(type));

        Step();
    }

    #endregion

    #region External Calls

    void OnExternalReturnValue(DataItem returnValue)
    {
        Push(returnValue);
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    void CALL_EXTERNAL()
    {
        DataItem functionId_ = Pop();
        if (functionId_.Type != RuntimeType.Integer)
        { throw new RuntimeException($"Invalid operand {functionId_} for instruction {nameof(Opcode.CallExternal)}"); }

        int functionId = functionId_.VInt;

        if (!ExternalFunctions.TryGetValue(functionId, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined external function {functionId}"); }

        int parameterCount = (int)CurrentInstruction.Parameter;

        List<DataItem> parameters = new();
        for (int i = 0; i < (int)CurrentInstruction.Parameter; i++)
        { parameters.Add(Memory[Registers.StackPointer - ((1 + i) * StackDirection)]); }
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
                Push(returnValue);
            }
            else
            {
                simpleFunction.Call(this, parameters.ToArray());
            }
        }

        Step();
    }

    #endregion

    void GET_REGISTER()
    {
        int? register = CurrentInstruction.Parameter.Integer;
        switch (register)
        {
            case 1: Push(Registers.CodePointer); break;
            case 2: Push(Registers.StackPointer); break;
            case 3: Push(Registers.BasePointer); break;
            default: throw new RuntimeException($"Invalid register {register}");
        }

        Step();
    }

    #endregion
}

public struct Registers
{
    public int CodePointer;
    public int BasePointer;
    public int StackPointer;
}
