using System;
using System.Diagnostics;

namespace LanguageCore.BBCode.Generator;

using Compiler;
using Parser.Statement;
using Runtime;

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Helper Functions

    int CallRuntime(CompiledVariable address)
    {
        if (!address.Type.IsFunction)
        { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.PUSH_VALUE, 0);

        AddInstruction(Opcode.GET_BASEPOINTER);

        StackLoad(new ValueAddress(address), address.Type.Size);

        AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

        AddInstruction(Opcode.MATH_SUB);

        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JUMP_BY, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int CallRuntime(CompiledParameter address)
    {
        if (address.Type != Type.Integer && !address.Type.IsFunction)
        { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.PUSH_VALUE, 0);

        AddInstruction(Opcode.GET_BASEPOINTER);

        ValueAddress offset = GetBaseAddress(address);
        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BasePointerRelative, offset.Address);

        AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

        AddInstruction(Opcode.MATH_SUB);

        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JUMP_BY, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int CallRuntime(StatementWithValue address)
    {
        CompiledType addressType = FindStatementType(address);

        if (addressType != Type.Integer && !addressType.IsFunction)
        { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{addressType}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.PUSH_VALUE, 0); // Saved code pointer

        AddInstruction(Opcode.GET_BASEPOINTER); // Saved base pointer

        GenerateCodeForStatement(address);

        AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);
        AddInstruction(Opcode.MATH_SUB);

        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JUMP_BY, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.PUSH_VALUE, 0);

        AddInstruction(Opcode.GET_BASEPOINTER);

        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.StackRelative, 0);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JUMP_BY, AddressingMode.Absolute, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return()
    {
        AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.Runtime, 0);
        AddInstruction(Opcode.SET_CODEPOINTER, AddressingMode.Runtime);
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
    int GenerateInitialValue(CompiledType type, Action<int>? afterValue = null)
    {
        if (type.IsStruct)
        {
            TypeArguments? typeParameters = type.Struct.TemplateInfo?.ToDictionary(type.TypeParameters); ;
            int size = 0;
            foreach (CompiledField field in type.Struct.Fields)
            {
                if (field.Type.IsGeneric &&
                    typeParameters is not null &&
                    typeParameters.TryGetValue(field.Type.Name, out CompiledType? yeah))
                {
                    size += GenerateInitialValue(yeah, afterValue);
                }
                else
                {
                    size += GenerateInitialValue(field.Type, afterValue);
                }
                afterValue?.Invoke(size);
            }
            return size;
        }

        if (type.IsStackArray)
        {
            int stackSize = type.StackArraySize;

            int size = 0;
            for (int i = 0; i < stackSize; i++)
            {
                size += GenerateInitialValue(type.StackArrayOf, afterValue);
                afterValue?.Invoke(size);
            }
            return size;
        }

        AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type));
        afterValue?.Invoke(0);
        return 1;
    }

    #endregion

    #region Memory Helpers

    protected override ValueAddress GetGlobalVariableAddress(CompiledVariable variable)
    {
        return new ValueAddress(variable.MemoryAddress, AddressingMode.Absolute) + (ExternalFunctionsCache.Count + 2);
    }

    protected override void StackLoad(ValueAddress address)
    {
        if (address.IsReference)
        {
            AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.Runtime);
            throw new NotImplementedException();
        }

        if (address.InHeap)
        {
            throw new NotImplementedException();
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
            case AddressingMode.BasePointerRelative:
            case AddressingMode.StackRelative:
                AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                break;

            case AddressingMode.Runtime:
                AddInstruction(Opcode.PUSH_VALUE, address.Address);
                AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode);
                break;
            default: throw new UnreachableException();
        }
    }
    protected override void StackStore(ValueAddress address)
    {
        if (address.IsReference)
        {
            throw new NotImplementedException();
        }

        if (address.InHeap)
        {
            throw new NotImplementedException();
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.Absolute, address.Address);
                break;
            case AddressingMode.BasePointerRelative:
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BasePointerRelative, address.Address);
                break;
            case AddressingMode.StackRelative:
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.StackRelative, address.Address);
                break;
            case AddressingMode.Runtime:
                AddInstruction(Opcode.PUSH_VALUE, address.Address);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.Runtime);
                break;
            default: throw new UnreachableException();
        }
    }

    /*
    void BoxData(int to, int size)
    {
        AddComment($"Box: {{");
        for (int currentOffset = 0; currentOffset < size; currentOffset++)
        {
            int currentReversedOffset = size - currentOffset - 1;

            AddComment($"Element {currentOffset}:");

            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, (-currentReversedOffset) - 1);

            AddInstruction(Opcode.PUSH_VALUE, to + size);
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }
        AddComment("}");
    }
    void UnboxData(int from, int size)
    {
        AddComment($"Unbox: {{");

        for (int currentOffset = 0; currentOffset < size; currentOffset++)
        {
            AddComment($"Element {currentOffset}:");

            AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        AddComment("}");
    }
    */

    void CheckPointerNull(bool preservePointer = true, string exceptionMessage = "null pointer")
    {
        if (!Settings.CheckNullPointers) return;
        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -1); }
        AddInstruction(Opcode.LOGIC_NOT);
        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JUMP_BY_IF_FALSE);
        GenerateCodeForLiteralString(exceptionMessage);
        AddInstruction(Opcode.THROW);
        GeneratedCode[jumpInstruction].Parameter = GeneratedCode.Count - jumpInstruction;
        AddComment($"}}");
    }

    void HeapLoad(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        AddInstruction(Opcode.PUSH_VALUE, offset);
        AddInstruction(Opcode.MATH_ADD);
        AddInstruction(Opcode.HEAP_GET, AddressingMode.Runtime);
    }

    void HeapStore(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        AddInstruction(Opcode.PUSH_VALUE, offset);
        AddInstruction(Opcode.MATH_ADD);
        AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
    }

    #endregion

    #region Addressing Helpers

    public const int TagsBeforeBasePointer = 2;

    /// <summary>Stuff after BasePointer but before any variables</summary>
    readonly Stack<int> TagCount;

    public int ReturnValueOffset => -(ParametersSize + 1 + TagsBeforeBasePointer);
    public const int ReturnFlagOffset = 0;
    public const int SavedBasePointerOffset = -1;
    public const int SavedCodePointerOffset = -2;

    int ParametersSize
    {
        get
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                sum += CompiledParameters[i].Type.Size;
            }

            return sum;
        }
    }
    int ParametersSizeBefore(int beforeThis)
    {
        int sum = 0;

        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            if (CompiledParameters[i].Index < beforeThis) continue;

            sum += CompiledParameters[i].Type.Size;
        }

        return sum;
    }

    protected override ValueAddress GetBaseAddress(CompiledParameter parameter)
    {
        int address = -(ParametersSizeBefore(parameter.Index) + TagsBeforeBasePointer);
        return new ValueAddress(parameter, address);
    }
    protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
    {
        int address = -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer);
        return new ValueAddress(parameter, address);
    }

    #endregion
}