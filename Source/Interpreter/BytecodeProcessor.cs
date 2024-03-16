using System.Collections.Frozen;

namespace LanguageCore.Runtime;

public class BytecodeProcessor
{
    public Memory Memory;

    public readonly FrozenDictionary<string, ExternalFunctionBase> ExternalFunctions;

    public int CodePointer;
    public int BasePointer;

    public Instruction CurrentInstruction => Memory.Code[CodePointer];
    public bool IsDone => CodePointer >= Memory.Code.Length;

    public BytecodeProcessor(ImmutableArray<Instruction> code, int heapSize, FrozenDictionary<string, ExternalFunctionBase> externalFunctions)
    {
        ExternalFunctions = externalFunctions;

        BasePointer = 0;
        CodePointer = 0;

        Memory = new(heapSize, code);
    }

    public void Step() => CodePointer++;
    public void Step(int num) => CodePointer += num;

    /// <exception cref="RuntimeException"/>
    /// <exception cref="UserException"/>
    /// <exception cref="InternalException"/>
    /// <exception cref="Exception"/>
    public void Process()
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
