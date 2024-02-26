using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;

namespace LanguageCore.Runtime;

public class BytecodeProcessor
{
    public readonly Memory Memory;

    public readonly FrozenDictionary<string, ExternalFunctionBase> ExternalFunctions;

    public int CodePointer;
    public int BasePointer;

    public Instruction CurrentInstruction => Memory.Code[CodePointer];
    public bool IsDone => CodePointer >= Memory.Code.Length;

    public BytecodeProcessor(Instruction[] code, int heapSize, FrozenDictionary<string, ExternalFunctionBase> externalFunctions)
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
        switch (CurrentInstruction.opcode)
        {
            case Opcode.UNKNOWN: throw new InternalException("Unknown instruction");

            case Opcode.EXIT: EXIT(); break;

            case Opcode.PUSH_VALUE: PUSH_VALUE(); break;
            case Opcode.POP_VALUE: POP_VALUE(); break;

            case Opcode.LOAD_VALUE: LOAD_VALUE(); break;
            case Opcode.STORE_VALUE: STORE_VALUE(); break;

            case Opcode.JUMP_BY_IF_FALSE: JUMP_BY_IF_FALSE(); break;
            case Opcode.JUMP_BY: JUMP_BY(); break;
            case Opcode.THROW: THROW(); break;

            case Opcode.CALL: CALL(); break;
            case Opcode.RETURN: RETURN(); break;

            case Opcode.CALL_EXTERNAL: CALL_EXTERNAL(); break;

            case Opcode.MATH_ADD: MATH_ADD(); break;
            case Opcode.MATH_SUB: MATH_SUB(); break;
            case Opcode.MATH_MULT: MATH_MULT(); break;
            case Opcode.MATH_DIV: MATH_DIV(); break;
            case Opcode.MATH_MOD: MATH_MOD(); break;

            case Opcode.BITS_SHIFT_LEFT: BITSHIFT_LEFT(); break;
            case Opcode.BITS_SHIFT_RIGHT: BITSHIFT_RIGHT(); break;

            case Opcode.BITS_AND: BITS_AND(); break;
            case Opcode.BITS_OR: BITS_OR(); break;
            case Opcode.BITS_XOR: BITS_XOR(); break;
            case Opcode.BITS_NOT: BITS_NOT(); break;

            case Opcode.LOGIC_LT: LOGIC_LT(); break;
            case Opcode.LOGIC_MT: LOGIC_MT(); break;
            case Opcode.LOGIC_EQ: LOGIC_EQ(); break;
            case Opcode.LOGIC_NEQ: LOGIC_NEQ(); break;
            case Opcode.LOGIC_LTEQ: LOGIC_LTEQ(); break;
            case Opcode.LOGIC_MTEQ: LOGIC_MTEQ(); break;
            case Opcode.LOGIC_NOT: LOGIC_NOT(); break;
            case Opcode.LOGIC_OR: LOGIC_OR(); break;
            case Opcode.LOGIC_AND: LOGIC_AND(); break;

            case Opcode.HEAP_GET: HEAP_GET(); break;
            case Opcode.HEAP_SET: HEAP_SET(); break;

            case Opcode.HEAP_ALLOC: HEAP_ALLOC(); break;
            case Opcode.HEAP_FREE: HEAP_FREE(); break;

            case Opcode.GET_BASEPOINTER: GET_BASEPOINTER(); break;
            case Opcode.SET_BASEPOINTER: SET_BASEPOINTER(); break;

            case Opcode.SET_CODEPOINTER: SET_CODEPOINTER(); break;

            case Opcode.TYPE_GET: TYPE_GET(); break;
            case Opcode.TYPE_SET: TYPE_SET(); break;

            default: throw new UnreachableException();
        }
    }

    /// <exception cref="InternalException"/>
    int FetchStackAddress() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
        AddressingMode.Runtime => (int)Memory.Stack.Pop(),

        AddressingMode.BasePointerRelative => BasePointer + (int)CurrentInstruction.Parameter,
        AddressingMode.StackRelative => Memory.Stack.Count + (int)CurrentInstruction.Parameter,

        _ => throw new InternalException($"Invalid stack addressing mode {CurrentInstruction.AddressingMode}"),
    };

    /// <exception cref="InternalException"/>
    int FetchHeapAddress() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
        AddressingMode.Runtime => (int)Memory.Stack.Pop(),

        _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
    };

    /// <exception cref="InternalException"/>
    DataItem FetchData() => CurrentInstruction.AddressingMode switch
    {
        AddressingMode.Absolute => CurrentInstruction.Parameter,
        AddressingMode.Runtime => Memory.Stack.Pop(),

        _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
    };

    #region Instruction Methods

    #region HEAP Operations

    void HEAP_ALLOC()
    {
        DataItem sizeData = Memory.Stack.Pop();
        int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_ALLOC, got {sizeData.Type}");

        int block = Memory.Heap.Allocate(size);

        Memory.Stack.Push(new DataItem(block));

        Step();
    }

    void HEAP_FREE()
    {
        DataItem pointerData = Memory.Stack.Pop();
        int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_DEALLOC, got {pointerData.Type}");

        Memory.Heap.Deallocate(pointer);

        Step();
    }

    void HEAP_GET()
    {
        int address = FetchHeapAddress();
        DataItem value = Memory.Heap[address];
        Memory.Stack.Push(value);
        Step();
    }

    void HEAP_SET()
    {
        int address = FetchHeapAddress();
        DataItem value = Memory.Stack.Pop();
        Memory.Heap[address] = value;
        Step();
    }

    #endregion

    #region Flow Control

    /// <exception cref="UserException"/>
    void THROW()
    {
        int pointer = (int)Memory.Stack.Pop();
        string? value = null;
        try
        {
            value = Memory.Heap.GetString(pointer);
            Memory.Heap.Deallocate(pointer);
        }
        catch (Exception) { }
        throw new UserException(value ?? "null");
    }

    void CALL()
    {
        int relativeAddress = (int)FetchData();

        Memory.Stack.Push(new DataItem(CodePointer));

        Step(relativeAddress);
    }

    void RETURN()
    {
        DataItem codePointer = Memory.Stack.Pop();

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

        DataItem condition = Memory.Stack.Pop();

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
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide << rightSide);

        Step();
    }

    void BITSHIFT_RIGHT()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide >> rightSide);

        Step();
    }

    void LOGIC_LT()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide < rightSide));

        Step();
    }

    void LOGIC_NOT()
    {
        DataItem v = Memory.Stack.Pop();
        Memory.Stack.Push(!v);
        Step();
    }

    void LOGIC_MT()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide > rightSide));

        Step();
    }

    void LOGIC_AND()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem((bool)leftSide && (bool)rightSide));

        Step();
    }

    void LOGIC_OR()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem((bool)leftSide || (bool)rightSide));

        Step();
    }

    void LOGIC_EQ()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide == rightSide));

        Step();
    }

    void LOGIC_NEQ()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide != rightSide));

        Step();
    }

    void BITS_OR()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide | rightSide);

        Step();
    }

    void BITS_XOR()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide ^ rightSide);

        Step();
    }

    void BITS_NOT()
    {
        DataItem rightSide = Memory.Stack.Pop();

        Memory.Stack.Push(~rightSide);

        Step();
    }

    void LOGIC_LTEQ()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide <= rightSide));

        Step();
    }

    void LOGIC_MTEQ()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(new DataItem(leftSide >= rightSide));

        Step();
    }

    void BITS_AND()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide & rightSide);

        Step();
    }

    #endregion

    #region Math Operations

    void MATH_ADD()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide + rightSide);

        Step();
    }

    void MATH_DIV()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide / rightSide);

        Step();
    }

    void MATH_SUB()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide - rightSide);

        Step();
    }

    void MATH_MULT()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide * rightSide);

        Step();
    }

    void MATH_MOD()
    {
        DataItem rightSide = Memory.Stack.Pop();
        DataItem leftSide = Memory.Stack.Pop();

        Memory.Stack.Push(leftSide % rightSide);

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        DataItem value = CurrentInstruction.Parameter;
        Memory.Stack.Push(value);

        Step();
    }

    void POP_VALUE()
    {
        Memory.Stack.Pop();

        Step();
    }

    void STORE_VALUE()
    {
        int address = FetchStackAddress();
        DataItem value = Memory.Stack.Pop();
        Memory.Stack[address] = value;

        Step();
    }

    void LOAD_VALUE()
    {
        int address = FetchStackAddress();
        DataItem value = Memory.Stack[address];
        Memory.Stack.Push(value);

        Step();
    }

    #endregion

    #region Utility Operations

    void GET_BASEPOINTER()
    {
        Memory.Stack.Push(new DataItem(BasePointer));

        Step();
    }

    void SET_BASEPOINTER()
    {
        BasePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Memory.Stack.Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            AddressingMode.StackRelative => Memory.Stack.Count + (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_BASEPOINTER}"),
        };

        Step();
    }

    void SET_CODEPOINTER()
    {
        CodePointer = CurrentInstruction.AddressingMode switch
        {
            AddressingMode.Runtime => (int)Memory.Stack.Pop(),
            AddressingMode.Absolute => (int)CurrentInstruction.Parameter,
            _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_CODEPOINTER}"),
        };
    }

    void TYPE_SET()
    {
        RuntimeType targetType = (RuntimeType)(byte)Memory.Stack.Pop();
        DataItem value = Memory.Stack.Pop();

        DataItem.Cast(ref value, targetType);

        Memory.Stack.Push(value);

        Step();
    }

    void TYPE_GET()
    {
        DataItem value = Memory.Stack.Pop();
        byte type = (byte)value.Type;
        Memory.Stack.Push(new DataItem(type));

        Step();
    }

    #endregion

    #region External Calls

    void OnExternalReturnValue(DataItem returnValue)
    {
        Memory.Stack.Push(returnValue);
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    void CALL_EXTERNAL()
    {
        DataItem functionNameDataItem = Memory.Stack.Pop();
        if (functionNameDataItem.Type != RuntimeType.SInt32)
        { throw new InternalException($"Instruction CALL_EXTERNAL need a String pointer (int) DataItem parameter from the stack, received {functionNameDataItem.Type} {functionNameDataItem}"); }

        string functionName = Memory.Heap.GetString((int)functionNameDataItem);

        if (!ExternalFunctions.TryGetValue(functionName, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined function \"{functionName}\""); }

        int parameterCount = (int)CurrentInstruction.Parameter;

        List<DataItem> parameters = new();
        for (int i = 1; i <= (int)CurrentInstruction.Parameter; i++)
        { parameters.Add(Memory.Stack[^i]); }
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
                Memory.Stack.Push(returnValue);
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
    public readonly Instruction[] Code;

    public Memory(int heapSize, Instruction[] code)
    {
        Code = code;

        Stack = new Stack<DataItem>();
        Heap = new HEAP(heapSize);
    }
}
