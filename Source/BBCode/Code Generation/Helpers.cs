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

        StackLoad(AbsoluteGlobalAddress);
        AddInstruction(Opcode.Push, Register.BasePointer);

        GenerateCodeForStatement(address);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.MathSub, reg.Register, GeneratedCode.Count + 2);

            AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, reg.Register);

            GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;
            return jumpInstruction;
        }
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        StackLoad(AbsoluteGlobalAddress);
        AddInstruction(Opcode.Push, Register.BasePointer);

        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return()
    {
        AddInstruction(Opcode.PopTo, Register.BasePointer);
        AddInstruction(Opcode.Pop32); // Pop AbsoluteGlobalOffset
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

        if (!structType.GetField(field.Identifier.Content, out _, out int fieldOffset))
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
        => new ValueAddress(variable.MemoryAddress, AddressingMode.Pointer) + 3;
    public ValueAddress GetReturnValueAddress(GeneralType returnType)
        => new(-(ParametersSize + TagsBeforeBasePointer + returnType.SizeBytes) + BytecodeProcessor.StackPointerOffset, AddressingMode.PointerBP);
    ValueAddress GetParameterAddress(CompiledParameter parameter)
    {
        int address = -(ParametersSizeBefore(parameter.Index) + TagsBeforeBasePointer) + BytecodeProcessor.StackPointerOffset;
        return new ValueAddress(parameter, address);
    }
    ValueAddress GetParameterAddress(CompiledParameter parameter, int offset)
    {
        int address = -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer) + BytecodeProcessor.StackPointerOffset;
        return new ValueAddress(parameter, address);
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

        return NeedDerefernce(field.PrevStatement);
    }

    void StackStore(ValueAddress address, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        { StackStore(address + i); }
    }

    void StackLoad(ValueAddress address, int size)
    {
        for (int i = 0; i < size; i++)
        { StackLoad(address + i); }
    }

    void StackLoad(ValueAddress address)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced());
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg.Register);
                AddInstruction(Opcode.Push, reg.Register.ToPtr());
            }
            return;
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Pointer:
                StackLoad(AbsoluteGlobalAddress);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Register);

                    if (BytecodeProcessor.StackDirection > 0)
                    { AddInstruction(Opcode.MathAdd, reg.Register, address.Address * BytecodeProcessor.StackDirection); }
                    else
                    { AddInstruction(Opcode.MathSub, reg.Register, address.Address * -BytecodeProcessor.StackDirection); }

                    AddInstruction(Opcode.Push, reg.Register.ToPtr());
                }
                break;
            case AddressingMode.PointerBP:
                AddInstruction(Opcode.Push, Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;

            case AddressingMode.PointerSP:
                AddInstruction(Opcode.Push, Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;

            default: throw new UnreachableException();
        }
    }

    void StackStore(ValueAddress address)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced());
            using (RegisterUsage.Auto reg = Registers.GetFree())
            {
                AddInstruction(Opcode.PopTo, reg.Register);
                AddInstruction(Opcode.PopTo, reg.Register.ToPtr());
            }
            return;
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Pointer:
                StackLoad(AbsoluteGlobalAddress);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.PopTo, reg.Register);

                    if (BytecodeProcessor.StackDirection > 0)
                    { AddInstruction(Opcode.MathAdd, reg.Register, address.Address * BytecodeProcessor.StackDirection); }
                    else
                    { AddInstruction(Opcode.MathSub, reg.Register, address.Address * -BytecodeProcessor.StackDirection); }

                    AddInstruction(Opcode.PopTo, reg.Register.ToPtr());
                }
                break;
            case AddressingMode.PointerBP:
                AddInstruction(Opcode.PopTo, Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;
            case AddressingMode.PointerSP:
                AddInstruction(Opcode.PopTo, Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;
            default: throw new UnreachableException();
        }
    }

    void CheckPointerNull(bool preservePointer = true)
    {
        if (!Settings.CheckNullPointers) return;
        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { AddInstruction(Opcode.Push, (InstructionOperand)StackTop); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Compare, reg.Register, 0);
            AddInstruction(Opcode.JumpIfNotEqual, 0);
        }

        int jumpInstruction = GeneratedCode.Count - 1;

        GenerateCodeForLiteralString("null pointer");
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Throw, reg.Register);
        }
        GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction;

        AddComment($"}}");
    }

    void HeapLoad(ValueAddress pointerAddress, int offset)
    {
        StackLoad(pointerAddress);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.Push, reg.Register.ToPtr(offset));
        }
    }

    void HeapStore(ValueAddress pointerAddress, int offset)
    {
        StackLoad(pointerAddress);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.PopTo, reg.Register.ToPtr(offset));
        }
    }

    void HeapStore(StatementWithValue pointer, int offset)
    {
        if (FindStatementType(pointer) is not PointerType)
        { throw new CompilerException($"This isn't a pointer", pointer, CurrentFile); }

        GenerateCodeForStatement(pointer);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.PopTo, reg.Register);
            AddInstruction(Opcode.PopTo, reg.Register.ToPtr(offset));
        }
    }

    #endregion

    #region Addressing Helpers

    public const int TagsBeforeBasePointer = 3;

    /// <summary>Stuff after BasePointer but before any variables</summary>
    readonly Stack<int> TagCount;

    public static ValueAddress AbsoluteGlobalAddress => new(AbsoluteGlobalOffset, AddressingMode.PointerBP);
    public static ValueAddress ReturnFlagAddress => new(ReturnFlagOffset, AddressingMode.PointerBP);
    public static ValueAddress StackTop => new(-1 + BytecodeProcessor.StackPointerOffset, AddressingMode.PointerSP);

    public static int ReturnFlagOffset => 0 + BytecodeProcessor.StackPointerOffset;
    public static int SavedBasePointerOffset => -1 + BytecodeProcessor.StackPointerOffset;
    public static int AbsoluteGlobalOffset => -2 + BytecodeProcessor.StackPointerOffset;
    public static int SavedCodePointerOffset => -3 + BytecodeProcessor.StackPointerOffset;

    public static int ScaledReturnFlagOffset => ReturnFlagOffset * BytecodeProcessor.StackDirection;
    public static int ScaledSavedBasePointerOffset => SavedBasePointerOffset * BytecodeProcessor.StackDirection;
    public static int ScaledAbsoluteGlobalOffset => AbsoluteGlobalOffset * BytecodeProcessor.StackDirection;
    public static int ScaledSavedCodePointerOffset => SavedCodePointerOffset * BytecodeProcessor.StackDirection;

    public static int InvalidFunctionAddress => int.MinValue;

    int ParametersSize
    {
        get
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                sum += CompiledParameters[i].Type.SizeBytes;
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

            sum += CompiledParameters[i].Type.SizeBytes;
        }

        return sum;
    }

    #endregion
}
