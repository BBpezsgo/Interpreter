namespace LanguageCore.BBLang.Generator;

using Compiler;
using Parser.Statement;
using Runtime;

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Helper Functions

    int CallRuntime(StatementWithValue address)
    {
        GeneralType addressType = FindStatementType(address);

        if (addressType is not FunctionType)
        { throw new CompilerException($"This should be a function pointer and not {addressType}", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        StackLoad(AbsoluteGlobalAddress, AbsGlobalAddressType.BitWidth);
        AddInstruction(Opcode.Push, Register.BasePointer);

        GenerateCodeForStatement(address);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(addressType.BitWidth));
            AddInstruction(Opcode.MathSub, reg.Get(addressType.BitWidth), GeneratedCode.Count + 2);

            AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, reg.Get(addressType.BitWidth));

            GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;
            return jumpInstruction;
        }
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        StackLoad(AbsoluteGlobalAddress, AbsGlobalAddressType.BitWidth);
        AddInstruction(Opcode.Push, Register.BasePointer);

        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return()
    {
        PopTo(Register.BasePointer);
        Pop(AbsGlobalAddressType.BitWidth); // Pop AbsoluteGlobalOffset
        AddInstruction(Opcode.Return);
    }

    void GenerateInitialValue(GeneralType type)
    {
        if (type is StructType structType)
        {
            foreach (CompiledField field in structType.Struct.Fields)
            {
                if (field.Type is GenericType genericType &&
                    structType.TypeArguments.TryGetValue(genericType.Identifier, out GeneralType? typeArgument))
                { GenerateInitialValue(typeArgument); }
                else
                { GenerateInitialValue(field.Type); }
            }
            return;
        }

        if (type is ArrayType arrayType)
        {
            for (int i = 0; i < arrayType.Length; i++)
            { GenerateInitialValue(arrayType.Of); }
            return;
        }

        AddInstruction(Opcode.Push, GetInitialValue(type));
    }

    #endregion

    #region Memory Helpers

    ValueAddress GetDataAddress(StatementWithValue value) => value switch
    {
        IndexCall v => GetDataAddress(v),
        Identifier v => GetDataAddress(v),
        Field v => GetDataAddress(v),
        _ => throw new NotImplementedException()
    };
    ValueAddress GetDataAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetParameterAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return new ValueAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
    }
    ValueAddress GetDataAddress(Field field)
    {
        ValueAddress address = GetBaseAddress(field);
        if (address.IsReference)
        { throw new NotImplementedException(); }
        int offset = GetDataOffset(field);
        return address + offset;
    }
    ValueAddress GetDataAddress(IndexCall indexCall)
    {
        ValueAddress address = GetBaseAddress(indexCall.PrevStatement);
        if (address.IsReference)
        { throw new NotImplementedException(); }
        int currentOffset = GetDataOffset(indexCall);
        return address + currentOffset;
    }

    int GetDataOffset(StatementWithValue value, StatementWithValue? until = null) => value switch
    {
        IndexCall v => GetDataOffset(v, until),
        Field v => GetDataOffset(v, until),
        Identifier => 0,
        _ => throw new NotImplementedException()
    };
    int GetDataOffset(Field field, StatementWithValue? until = null)
    {
        if (field.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is not StructType structType)
        { throw new NotImplementedException(); }

        if (!structType.GetField(field.Identifier.Content, true, out _, out int fieldOffset))
        { throw new CompilerException($"Field \"{field.Identifier}\" not found in struct \"{structType.Struct.Identifier}\"", field.Identifier, CurrentFile); }

        int prevOffset = GetDataOffset(field.PrevStatement, until);
        return prevOffset + fieldOffset;
    }
    int GetDataOffset(IndexCall indexCall, StatementWithValue? until = null)
    {
        if (indexCall.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(indexCall.PrevStatement);

        if (prevType is not ArrayType arrayType)
        { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

        if (!TryCompute(indexCall.Index, out CompiledValue index))
        { throw new CompilerException($"Can't compute the index value", indexCall.Index, CurrentFile); }

        int prevOffset = GetDataOffset(indexCall.PrevStatement, until);
        int offset = (int)index * arrayType.Of.SizeBytes;
        return prevOffset + offset;
    }

    static ValueAddress GetGlobalVariableAddress(CompiledVariable variable)
        =>
        new ValueAddress(variable.MemoryAddress, AddressingMode.Pointer) // Return flag + SP offset + Variable offset
        + AbsGlobalAddressSize // Abs global address
        + BasePointerSize; // Saved BP
                           // The abs global address will be added to this and then dereferenced

    public ValueAddress GetReturnValueAddress(GeneralType returnType)
        => new(
            0 // We start at the saved base pointer
            - ParametersSize // Offset by the parameters
            - StackFrameTags // Offset by the stack frame stuff
            - returnType.SizeBytes // We at the end of the return value, but we want to be at the start
            + 1 // Stack pointer offset (???)

            , AddressingMode.PointerBP);

    ValueAddress GetParameterAddress(CompiledParameter parameter, int offset = 0)
    {
        return new ValueAddress(
            0 // We start at the saved base pointer
            - ParametersSizeBefore(parameter.Index) // ???
            + offset
            - StackFrameTags // Offset by the stack frame stuff
            + 1 // Stack pointer offset (???)

            , AddressingMode.PointerBP, parameter.IsRef);
    }

    ValueAddress GetBaseAddress(StatementWithValue statement) => statement switch
    {
        Identifier v => GetBaseAddress(v),
        Field v => GetBaseAddress(v),
        IndexCall v => GetBaseAddress(v),
        _ => throw new NotImplementedException()
    };
    ValueAddress GetBaseAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetParameterAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return new ValueAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
    }
    ValueAddress GetBaseAddress(Field statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        if (FindStatementType(statement.PrevStatement) is PointerType) throw null!;
        return address;
    }
    ValueAddress GetBaseAddress(IndexCall statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        if (FindStatementType(statement.PrevStatement) is PointerType) throw null!;
        return address;
    }

    StatementWithValue? NeedDerefernce(StatementWithValue value) => value switch
    {
        Identifier => null,
        Field v => NeedDerefernce(v),
        IndexCall v => NeedDerefernce(v),
        _ => throw new NotImplementedException()
    };
    StatementWithValue? NeedDerefernce(IndexCall indexCall)
    {
        if (FindStatementType(indexCall.PrevStatement) is PointerType)
        { return indexCall.PrevStatement; }

        return NeedDerefernce(indexCall.PrevStatement);
    }
    StatementWithValue? NeedDerefernce(Field field)
    {
        if (FindStatementType(field.PrevStatement) is PointerType)
        { return field.PrevStatement; }

        if (field.PrevStatement is Identifier identifier)
        {
            if (GetParameter(identifier.Content, out CompiledParameter? prevParameter))
            {
                if (prevParameter.IsRef)
                {
                    return field.PrevStatement;
                }
            }
            else if (GetVariable(identifier.Content, out CompiledVariable? prevVariable))
            {

            }
            else if (GetGlobalVariable(identifier.Content, identifier.File, out CompiledVariable? prevGlobalVariable, out _))
            {

            }
        }

        return NeedDerefernce(field.PrevStatement);
    }

    void PopTo(InstructionOperand destination, BitWidth size) => AddInstruction(size switch
    {
        BitWidth._8 => Opcode.PopTo8,
        BitWidth._16 => Opcode.PopTo16,
        BitWidth._32 => Opcode.PopTo32,
        _ => throw new UnreachableException(),
    }, destination);

    void PopTo(Register destination) => PopTo(destination, destination.BitWidth());

    void Pop(BitWidth size) => AddInstruction(size switch
    {
        BitWidth._8 => Opcode.Pop8,
        BitWidth._16 => Opcode.Pop16,
        BitWidth._32 => Opcode.Pop32,
        _ => throw new UnreachableException(),
    });

    void StackStore(ValueAddress address, int size)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced() + (BytecodeProcessor.PointerSize - 1), BitWidth._32);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(BitWidth._32));
                for (int i = size - 1; i >= 0; i--)
                { PopTo(reg.Get(BitWidth._32).ToPtr(i * BytecodeProcessor.StackDirection, BitWidth._8), BitWidth._8); }
            }
            return;
        }

        for (int i = size - 1; i >= 0; i--)
        { StackStore(address + i, BitWidth._8); }
    }

    void StackLoad(ValueAddress address, int size)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced() + (BytecodeProcessor.PointerSize - 1), BitWidth._32);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(BitWidth._32));
                for (int i = 0; i < size; i++)
                { AddInstruction(Opcode.Push, reg.Get(BitWidth._32).ToPtr(i * BytecodeProcessor.StackDirection, BitWidth._8)); }
            }
            return;
        }

        for (int i = 0; i < size; i++)
        { StackLoad(address + i, BitWidth._8); }
    }

    void StackLoad(ValueAddress address, BitWidth size)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced(), BitWidth._32);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(BitWidth._32));
                AddInstruction(Opcode.Push, reg.Get(BitWidth._32).ToPtr(0, size));
            }
            return;
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Pointer:
                StackLoad(AbsoluteGlobalAddress, AbsGlobalAddressType.BitWidth);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(AbsGlobalAddressType.BitWidth));

                    if (BytecodeProcessor.StackDirection > 0)
                    { AddInstruction(Opcode.MathAdd, reg.Get(AbsGlobalAddressType.BitWidth), address.Address * BytecodeProcessor.StackDirection); }
                    else
                    { AddInstruction(Opcode.MathSub, reg.Get(AbsGlobalAddressType.BitWidth), address.Address * -BytecodeProcessor.StackDirection); }

                    AddInstruction(Opcode.Push, reg.Get(AbsGlobalAddressType.BitWidth).ToPtr(0, size));
                }
                break;
            case AddressingMode.PointerBP:
                AddInstruction(Opcode.Push, Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection, size));
                break;

            case AddressingMode.PointerSP:
                AddInstruction(Opcode.Push, Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection, size));
                break;

            default: throw new UnreachableException();
        }
    }

    void StackStore(ValueAddress address, BitWidth size)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced(), BitWidth._32);
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                PopTo(reg.Get(BitWidth._32));
                PopTo(reg.Get(BitWidth._32).ToPtr(0, size), size);
            }
            return;
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Pointer:
                StackLoad(AbsoluteGlobalAddress, AbsGlobalAddressType.BitWidth);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(AbsGlobalAddressType.BitWidth));

                    if (BytecodeProcessor.StackDirection > 0)
                    { AddInstruction(Opcode.MathAdd, reg.Get(AbsGlobalAddressType.BitWidth), address.Address * BytecodeProcessor.StackDirection); }
                    else
                    { AddInstruction(Opcode.MathSub, reg.Get(AbsGlobalAddressType.BitWidth), address.Address * -BytecodeProcessor.StackDirection); }

                    PopTo(reg.Get(AbsGlobalAddressType.BitWidth).ToPtr(0, size), size);
                }
                break;
            case AddressingMode.PointerBP:
                PopTo(Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection, size), size);
                break;
            case AddressingMode.PointerSP:
                PopTo(Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection, size), size);
                break;
            default: throw new UnreachableException();
        }
    }

    void CheckPointerNull(bool preservePointer = true)
    {
        if (!Settings.CheckNullPointers)
        {
            if (!preservePointer)
            { AddInstruction(Opcode.Pop32); }
            return;
        }

        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { AddInstruction(Opcode.Push, (InstructionOperand)StackTop); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            AddInstruction(Opcode.Compare, reg.Get(BitWidth._32), 0);
            AddInstruction(Opcode.JumpIfNotEqual, 0);
        }

        int jumpInstruction = GeneratedCode.Count - 1;

        GenerateCodeForLiteralString("null pointer");
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            AddInstruction(Opcode.Throw, reg.Get(BitWidth._32));
        }
        GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction;

        AddComment($"}}");
    }

    void HeapLoad(StatementWithValue pointer, int offset, int size)
    {
        if (FindStatementType(pointer) is not PointerType pointerType)
        { throw new CompilerException($"This isn't a pointer", pointer, CurrentFile); }

        GenerateCodeForStatement(pointer, resolveReference: false);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            for (int i = 0; i < size; i++)
            { AddInstruction(Opcode.Push, reg.Get(BitWidth._32).ToPtr(i + offset, BitWidth._8)); }
        }
    }

    void HeapStore(ValueAddress pointerAddress, int offset, BitWidth size)
    {
        StackLoad(pointerAddress, BitWidth._32);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            PopTo(reg.Get(BitWidth._32).ToPtr(offset, size), size);
        }
    }

    void HeapStore(StatementWithValue pointer, int offset, int size)
    {
        // if (FindStatementType(pointer) is not PointerType pointerType)
        // { throw new CompilerException($"This isn't a pointer", pointer, CurrentFile); }

        GenerateCodeForStatement(pointer, resolveReference: false);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(BitWidth._32));
            for (int i = size - 1; i >= 0; i--)
            { PopTo(reg.Get(BitWidth._32).ToPtr(i + offset, BitWidth._8), BitWidth._8); }
        }
    }

    #endregion

    #region Addressing Helpers

    public static readonly BuiltinType ReturnFlagType = new(BasicType.Byte);
    public static readonly BuiltinType ExitCodeType = new(BasicType.Integer);
    public static readonly GeneralType AbsGlobalAddressType = new PointerType(new BuiltinType(BasicType.Integer));
    public static readonly GeneralType StackPointerType = new PointerType(new BuiltinType(BasicType.Integer));
    public static readonly GeneralType CodePointerType = new PointerType(new BuiltinType(BasicType.Integer));
    public static readonly GeneralType BasePointerType = new PointerType(new BuiltinType(BasicType.Integer));

    public const int AbsGlobalAddressSize = BytecodeProcessor.PointerSize;
    public const int StackPointerSize = BytecodeProcessor.PointerSize;
    public const int CodePointerSize = BytecodeProcessor.PointerSize;
    public const int BasePointerSize = BytecodeProcessor.PointerSize;

    /// <summary>
    /// (<c>Saved BP</c> +) <c>Abs global address</c> + <c>Saved CP</c>
    /// </summary>
    public static int StackFrameTags => BasePointerSize + AbsGlobalAddressSize + CodePointerSize;

    public static ValueAddress AbsoluteGlobalAddress => new(AbsoluteGlobalOffset, AddressingMode.PointerBP);
    public static ValueAddress ReturnFlagAddress => new(ReturnFlagOffset, AddressingMode.PointerBP);
    public static ValueAddress StackTop => new(0, AddressingMode.PointerSP);

    public static int ReturnFlagOffset => 1;
    public static int AbsoluteGlobalOffset => -ExitCodeType.SizeBytes;
    public static int SavedBasePointerOffset => 0;
    public static int SavedCodePointerOffset => -AbsGlobalAddressSize + -CodePointerSize;

    public static int ScaledReturnFlagOffset => ReturnFlagOffset * BytecodeProcessor.StackDirection;
    public static int ScaledSavedBasePointerOffset => SavedBasePointerOffset * BytecodeProcessor.StackDirection;
    public static int ScaledAbsoluteGlobalOffset => AbsoluteGlobalOffset * BytecodeProcessor.StackDirection;
    public static int ScaledSavedCodePointerOffset => SavedCodePointerOffset * BytecodeProcessor.StackDirection;

    public const int InvalidFunctionAddress = int.MinValue;

    int ParametersSize
    {
        get
        {
            int sum = 0;

            foreach (CompiledParameter parameter in CompiledParameters)
            { sum += parameter.Type.SizeBytes; }

            return sum;
        }
    }

    int ParametersSizeBefore(int beforeThis)
    {
        int sum = 0;

        foreach (CompiledParameter parameter in CompiledParameters)
        {
            if (parameter.Index < beforeThis) continue;
            sum += parameter.Type.SizeBytes;
        }

        return sum;
    }

    #endregion
}
