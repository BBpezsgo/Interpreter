namespace LanguageCore.BBCode.Generator;

using Compiler;
using Parser.Statement;
using Runtime;

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Helper Functions

    int CallRuntime(CompiledVariable address)
    {
        if (address.Type != BasicType.Integer && address.Type is not FunctionType)
        { throw new CompilerException($"This should be an \"{new BuiltinType(BasicType.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        AddInstruction(Opcode.GetBasePointer);

        StackLoad(new ValueAddress(address), address.Type.Size);

        AddInstruction(Opcode.Push, GeneratedCode.Count + 3);

        AddInstruction(Opcode.MathSub);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int CallRuntime(CompiledParameter address)
    {
        if (address.Type != BasicType.Integer && address.Type is not FunctionType)
        { throw new CompilerException($"This should be an \"{new BuiltinType(BasicType.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        AddInstruction(Opcode.GetBasePointer);

        ValueAddress offset = GetBaseAddress(address);
        AddInstruction(Opcode.StackLoad, AddressingMode.BasePointerRelative, offset.Address);

        AddInstruction(Opcode.Push, GeneratedCode.Count + 3);

        AddInstruction(Opcode.MathSub);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int CallRuntime(StatementWithValue address)
    {
        GeneralType addressType = FindStatementType(address);

        if (addressType != BasicType.Integer && addressType is not FunctionType)
        { throw new CompilerException($"This should be an \"{new BuiltinType(BasicType.Integer)}\" or function pointer and not \"{addressType}\"", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0); // Saved code pointer

        AddInstruction(Opcode.GetBasePointer); // Saved base pointer

        GenerateCodeForStatement(address);

        AddInstruction(Opcode.Push, GeneratedCode.Count + 3);
        AddInstruction(Opcode.MathSub);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackRelative, -1);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        AddInstruction(Opcode.GetBasePointer);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackRelative, 0);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Absolute, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return()
    {
        AddInstruction(Opcode.SetBasePointer, AddressingMode.Runtime, 0);
        AddInstruction(Opcode.SetCodePointer, AddressingMode.Runtime);
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
    int GenerateInitialValue(GeneralType type)
    {
        if (type is StructType structType)
        {
            IReadOnlyDictionary<string, GeneralType>? typeParameters = structType.TypeParametersMap;
            int size = 0;
            foreach (CompiledField field in structType.Struct.Fields)
            {
                if (field.Type is GenericType genericType &&
                    typeParameters is not null &&
                    typeParameters.TryGetValue(genericType.Identifier, out GeneralType? yeah))
                { size += GenerateInitialValue(yeah); }
                else
                { size += GenerateInitialValue(field.Type); }
            }
            return size;
        }

        if (type is ArrayType arrayType)
        {
            int size = 0;
            for (int i = 0; i < arrayType.Length; i++)
            { size += GenerateInitialValue(arrayType.Of); }
            return size;
        }

        AddInstruction(Opcode.Push, GetInitialValue(type));
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
            AddInstruction(Opcode.StackLoad, address.AddressingMode, address.Address);
            AddInstruction(Opcode.StackLoad, AddressingMode.Runtime);
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
                AddInstruction(Opcode.StackLoad, address.AddressingMode, address.Address);
                break;

            case AddressingMode.Runtime:
                AddInstruction(Opcode.Push, address.Address);
                AddInstruction(Opcode.StackLoad, address.AddressingMode);
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
                AddInstruction(Opcode.StackStore, AddressingMode.Absolute, address.Address);
                break;
            case AddressingMode.BasePointerRelative:
                AddInstruction(Opcode.StackStore, AddressingMode.BasePointerRelative, address.Address);
                break;
            case AddressingMode.StackRelative:
                AddInstruction(Opcode.StackStore, AddressingMode.StackRelative, address.Address);
                break;
            case AddressingMode.Runtime:
                AddInstruction(Opcode.Push, address.Address);
                AddInstruction(Opcode.StackStore, AddressingMode.Runtime);
                break;
            default: throw new UnreachableException();
        }
    }

    void CheckPointerNull(bool preservePointer = true, string exceptionMessage = "null pointer")
    {
        if (!Settings.CheckNullPointers) return;
        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { AddInstruction(Opcode.StackLoad, AddressingMode.StackRelative, -1); }
        AddInstruction(Opcode.LogicNOT);
        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JumpIfZero);
        GenerateCodeForLiteralString(exceptionMessage);
        AddInstruction(Opcode.Throw);
        GeneratedCode[jumpInstruction].Parameter = GeneratedCode.Count - jumpInstruction;
        AddComment($"}}");
    }

    void HeapLoad(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        AddInstruction(Opcode.Push, offset);
        AddInstruction(Opcode.MathAdd);
        AddInstruction(Opcode.HeapGet, AddressingMode.Runtime);
    }

    void HeapStore(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        AddInstruction(Opcode.Push, offset);
        AddInstruction(Opcode.MathAdd);
        AddInstruction(Opcode.HeapSet, AddressingMode.Runtime);
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