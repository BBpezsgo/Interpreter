using LanguageCore.Compiler;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Helper Functions

    int CallRuntime(StatementWithValue address)
    {
        GeneralType addressType = FindStatementType(address);

        if (!addressType.Is<FunctionType>())
        { throw new CompilerException($"This should be a function pointer and not {addressType}", address, CurrentFile); }

        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this));
        AddInstruction(Opcode.Push, Register.BasePointer);

        GenerateCodeForStatement(address);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(addressType.GetBitWidth(this)));
            AddInstruction(Opcode.MathSub, reg.Get(addressType.GetBitWidth(this)), GeneratedCode.Count + 2);

            AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, reg.Get(addressType.GetBitWidth(this)));

            GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;
            return jumpInstruction;
        }
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this));
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
        Pop(AbsGlobalAddressType.GetSize(this)); // Pop AbsoluteGlobalOffset
        AddInstruction(Opcode.Return);
    }

    #endregion

    #region Memory Helpers

    Address GetDataAddress(StatementWithValue value) => value switch
    {
        IndexCall v => GetDataAddress(v),
        Identifier v => GetDataAddress(v),
        Field v => GetDataAddress(v),
        _ => throw new NotImplementedException()
    };
    Address GetDataAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetParameterAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return GetLocalVariableAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
    }
    Address GetDataAddress(Field field)
    {
        Address @base = GetBaseAddress(field);
        int offset = GetDataOffset(field);
        return new AddressOffset(@base, offset);
    }
    Address GetDataAddress(IndexCall indexCall)
    {
        Address @base = GetBaseAddress(indexCall.PrevStatement);
        int offset = GetDataOffset(indexCall);
        return new AddressOffset(@base, offset);
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

        if (!prevType.Is(out StructType? structType))
        { throw new NotImplementedException(); }

        if (!structType.GetField(field.Identifier.Content, this, out _, out int fieldOffset))
        { throw new CompilerException($"Field \"{field.Identifier}\" not found in struct \"{structType.Struct.Identifier}\"", field.Identifier, CurrentFile); }

        int prevOffset = GetDataOffset(field.PrevStatement, until);
        return prevOffset + fieldOffset;
    }
    int GetDataOffset(IndexCall indexCall, StatementWithValue? until = null)
    {
        if (indexCall.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(indexCall.PrevStatement);

        if (!prevType.Is(out ArrayType? arrayType))
        { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

        if (!TryCompute(indexCall.Index, out CompiledValue index))
        { throw new CompilerException($"Can't compute the index value", indexCall.Index, CurrentFile); }

        int prevOffset = GetDataOffset(indexCall.PrevStatement, until);
        int offset = (int)index * arrayType.Of.GetSize(this);
        return prevOffset + offset;
    }

    AddressOffset GetGlobalVariableAddress(CompiledVariable variable)
        => new(
            new AddressRuntimePointer(AbsoluteGlobalAddress),
            0
            + variable.MemoryAddress
            + ((
                AbsGlobalAddressSize
                + BasePointerSize
            ) * BytecodeProcessor.StackDirection)
        );

    static AddressOffset GetLocalVariableAddress(CompiledVariable variable)
        => new(
            Register.BasePointer,
            variable.MemoryAddress
        );

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public AddressOffset GetReturnValueAddress(GeneralType returnType)
        => new(
            Register.BasePointer,
            0 // We start at the saved base pointer
            - ((
                ParametersSize // Offset by the parameters
                + StackFrameTags // Offset by the stack frame stuff
            ) * BytecodeProcessor.StackDirection)
        // - returnType.SizeBytes // We at the end of the return value, but we want to be at the start
        // + 1 // Stack pointer offset (???)
        );

    public AddressOffset GetParameterAddress(CompiledParameter parameter, int offset = 0)
        => new(
            Register.BasePointer,
            0 // We start at the saved base pointer
            - ((
                ParametersSizeBefore(parameter.Index) // ???
                + StackFrameTags // Offset by the stack frame stuff
            ) * BytecodeProcessor.StackDirection)
            + offset
        // + 1 // Stack pointer offset (???)
        );

    Address GetBaseAddress(StatementWithValue statement) => statement switch
    {
        Identifier v => GetBaseAddress(v),
        Field v => GetBaseAddress(v),
        IndexCall v => GetBaseAddress(v),
        _ => throw new NotImplementedException()
    };
    Address GetBaseAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetParameterAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return GetLocalVariableAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
    }
    Address GetBaseAddress(Field statement)
    {
        Address address = GetBaseAddress(statement.PrevStatement);
        if (FindStatementType(statement.PrevStatement).Is<PointerType>()) throw null!;
        return address;
    }
    Address GetBaseAddress(IndexCall statement)
    {
        Address address = GetBaseAddress(statement.PrevStatement);
        if (FindStatementType(statement.PrevStatement).Is<PointerType>()) throw null!;
        return address;
    }

    StatementWithValue? NeedDerefernce(StatementWithValue value) => value switch
    {
        Identifier => null,
        Field v => NeedDereference(v),
        IndexCall v => NeedDerefernce(v),
        _ => throw new NotImplementedException()
    };
    StatementWithValue? NeedDerefernce(IndexCall indexCall)
    {
        if (FindStatementType(indexCall.PrevStatement).Is<PointerType>())
        { return indexCall.PrevStatement; }

        return NeedDerefernce(indexCall.PrevStatement);
    }
    StatementWithValue? NeedDereference(Field field)
    {
        if (FindStatementType(field.PrevStatement).Is<PointerType>())
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
            else if (GetVariable(identifier.Content, out _))
            { }
            else if (GetGlobalVariable(identifier.Content, identifier.File, out _, out _))
            { }
        }

        return NeedDerefernce(field.PrevStatement);
    }

    void Pop(int size)
    {
        if (PointerBitWidth >= BitWidth._64)
        {
            int qwordCount = size / 8;
            size %= 8;
            for (int i = 0; i < qwordCount; i++)
            { AddInstruction(Opcode.Pop64); }
        }

        if (PointerBitWidth != BitWidth._64)
        {
            int dwordCount = size / 4;
            size %= 4;
            for (int i = 0; i < dwordCount; i++)
            { AddInstruction(Opcode.Pop32); }
        }

        int wordCount = size / 2;
        size %= 2;
        for (int i = 0; i < wordCount; i++)
        { AddInstruction(Opcode.Pop16); }

        for (int i = 0; i < size; i++)
        { AddInstruction(Opcode.Pop8); }
    }

    void StackAlloc(int size)
    {
        if (PointerBitWidth != BitWidth._64)
        {
            int dwordCount = size / 4;
            size %= 4;
            for (int i = 0; i < dwordCount; i++)
            { AddInstruction(Opcode.Push, new CompiledValue((int)0)); }
        }

        int wordCount = size / 2;
        size %= 2;
        for (int i = 0; i < wordCount; i++)
        { AddInstruction(Opcode.Push, new CompiledValue((char)0)); }

        for (int i = 0; i < size; i++)
        { AddInstruction(Opcode.Push, new CompiledValue((byte)0)); }
    }

    void PopTo(InstructionOperand destination, BitWidth size)
    {
        if (PointerBitWidth == BitWidth._64 &&
            size is BitWidth._8 or BitWidth._32)
        { throw new NotImplementedException(); }

        AddInstruction(size switch
        {
            BitWidth._8 => Opcode.PopTo8,
            BitWidth._16 => Opcode.PopTo16,
            BitWidth._32 => Opcode.PopTo32,
            BitWidth._64 => Opcode.PopTo64,
            _ => throw new UnreachableException(),
        }, destination);
    }

    void PopTo(Register destination)
    {
        AddInstruction(destination.BitWidth() switch
        {
            BitWidth._8 => Opcode.PopTo8,
            BitWidth._16 => Opcode.PopTo16,
            BitWidth._32 => Opcode.PopTo32,
            BitWidth._64 => Opcode.PopTo64,
            _ => throw new UnreachableException(),
        }, destination);
    }

    void PopTo(AddressOffset address, int size)
    {
        switch (address.Base)
        {
            case AddressRuntimePointer addressPointer:
            {
                PushFrom(addressPointer.PointerAddress, PointerSize);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), address.Offset);
                    PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }

                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth), registerPointer.Register);
                    AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), address.Offset);
                    PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }

                break;
            }

            default:
                throw new NotImplementedException();
        }
    }

    void PopTo(AddressRuntimePointer address, int size)
    {
        PushFrom(address.PointerAddress, PointerSize);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void PopTo(AddressRegisterPointer address, int size)
    {
        int currentOffset = 0;
        while (currentOffset < size)
        {
            foreach (BitWidth checkBitWidth in
#if NET_STANDARD
                CompatibilityUtils.GetEnumValues<BitWidth>().Reverse()
#else
                Enum.GetValues<BitWidth>().Reverse()
#endif
            )
            {
                if (PointerBitWidth == BitWidth._64 &&
                    checkBitWidth == BitWidth._32)
                { continue; }

                int checkSize = (int)checkBitWidth;
                if (size - currentOffset < checkSize) continue;
                if (PointerSize < checkSize) continue;

                PopTo(address.Register.ToPtr(currentOffset, checkBitWidth), checkBitWidth);
                currentOffset += checkSize;
                break;
            }
        }
    }

    void PopTo(Address address, int size)
    {
        switch (address)
        {
            case AddressOffset addressOffset:
            {
                PopTo(addressOffset, size);
                break;
            }

            case AddressRuntimePointer runtimePointer:
            {
                PopTo(runtimePointer, size);
                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                PopTo(registerPointer, size);
                break;
            }

            default:
                throw new NotImplementedException();
        }
    }

    void PushFrom(AddressOffset address, int size)
    {
        switch (address.Base)
        {
            case AddressRuntimePointer addressPointer:
            {
                PushFrom(addressPointer.PointerAddress, PointerSize);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), address.Offset);
                    PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }

                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Move, reg.Get(PointerBitWidth), registerPointer.Register);

                    int currentOffset = size - 1;

                    while (currentOffset >= 0)
                    {
                        foreach (BitWidth checkBitWidth in
#if NET_STANDARD
                            CompatibilityUtils.GetEnumValues<BitWidth>().Reverse()
#else
                            Enum.GetValues<BitWidth>().Reverse()
#endif
                        )
                        {
                            int checkSize = (int)checkBitWidth;
                            if (currentOffset < checkSize - 1) continue;
                            if (PointerSize < checkSize) continue;

                            AddInstruction(Opcode.Push, reg.Get(PointerBitWidth).ToPtr(currentOffset + address.Offset - (checkSize - 1), checkBitWidth));
                            currentOffset -= checkSize;
                            break;
                        }
                    }

                    // AddInstruction(Opcode.MathAdd, reg.Get(PointerBitWidth), address.Offset);
                    // PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }

                break;
            }

            default:
                throw new NotImplementedException();
        }
    }

    void PushFrom(AddressRuntimePointer address, int size)
    {
        PushFrom(address.PointerAddress, PointerSize);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void PushFrom(AddressRegisterPointer address, int size)
    {
        int currentOffset = size - 1;

        while (currentOffset >= 0)
        {
            // foreach (BitWidth checkBitWidth in Enum.GetValues<BitWidth>().Reverse())
            // {
            //     int checkSize = (int)checkBitWidth;
            //     if (currentOffset < checkSize) continue;
            //     if (PointerSize < checkSize) continue;
            // 
            //     AddInstruction(Opcode.Push, address.Register.ToPtr(currentOffset - (checkSize - 1), checkBitWidth));
            //     currentOffset -= checkSize;
            //     break;
            // }
            if (currentOffset >= 8 && PointerSize >= 8)
            {
                AddInstruction(Opcode.Push, address.Register.ToPtr(currentOffset - 7, BitWidth._64));
                currentOffset -= 8;
            }
            else if (currentOffset >= 4)
            {
                AddInstruction(Opcode.Push, address.Register.ToPtr(currentOffset - 3, BitWidth._32));
                currentOffset -= 4;
            }
            else if (currentOffset >= 2)
            {
                AddInstruction(Opcode.Push, address.Register.ToPtr(currentOffset - 1, BitWidth._16));
                currentOffset -= 2;
            }
            else
            {
                AddInstruction(Opcode.Push, address.Register.ToPtr(currentOffset, BitWidth._8));
                currentOffset -= 1;
            }
        }
    }

    void PushFrom(Address address, int size)
    {
        switch (address)
        {
            case AddressOffset addressOffset:
            {
                PushFrom(addressOffset, size);
                break;
            }
            case AddressRuntimePointer runtimePointer:
            {
                PushFrom(runtimePointer, size);
                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                PushFrom(registerPointer, size);
                break;
            }

            default:
                throw new NotImplementedException();
        }
    }

    void CheckPointerNull(bool preservePointer = true)
    {
        if (!Settings.CheckNullPointers)
        {
            if (!preservePointer)
            { Pop(PointerSize); }
            return;
        }

        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { PushFrom(StackTop, PointerSize); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            AddInstruction(Opcode.Compare, reg.Get(PointerBitWidth), 0);
            AddInstruction(Opcode.JumpIfNotEqual, 0);
        }

        int jumpInstruction = GeneratedCode.Count - 1;

        GenerateCodeForLiteralString("null pointer", false);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            AddInstruction(Opcode.Throw, reg.Get(PointerBitWidth));
        }
        GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction;

        AddComment($"}}");
    }

    void HeapLoad(StatementWithValue pointer, int offset, int size)
    {
        if (!FindStatementType(pointer).Is<PointerType>())
        {
            if (pointer is not Identifier identifier ||
                !GetParameter(identifier.Content, out CompiledParameter? parameter) ||
                !parameter.IsRef)
            { throw new CompilerException($"This isn't a pointer", pointer, CurrentFile); }
        }

        GenerateCodeForStatement(pointer, resolveReference: false);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            for (int i = size - 1; i >= 0; i--)
            { AddInstruction(Opcode.Push, reg.Get(PointerBitWidth).ToPtr(i + offset, BitWidth._8)); }
        }
    }

    void HeapStore(StatementWithValue pointer, int offset, int size)
    {
        if (!FindStatementType(pointer).Is<PointerType>())
        { throw new CompilerException($"This isn't a pointer", pointer, CurrentFile); }

        GenerateCodeForStatement(pointer, resolveReference: false);

        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            for (int i = 0; i < size; i++)
            { PopTo(reg.Get(PointerBitWidth).ToPtr(i + offset, BitWidth._8), BitWidth._8); }
        }
    }

    #endregion

    #region Addressing Helpers

    BuiltinType ReturnFlagType => PointerBitWidth == BitWidth._64 ? BuiltinType.Char : BuiltinType.Byte;
    CompiledValue ReturnFlagTrue => PointerBitWidth == BitWidth._64 ? new((char)1) : new((byte)1);
    CompiledValue ReturnFlagFalse => PointerBitWidth == BitWidth._64 ? new((char)0) : new((byte)0);
    readonly BuiltinType ExitCodeType = BuiltinType.Integer;
    readonly PointerType AbsGlobalAddressType = new(BuiltinType.Integer);
    // readonly PointerType StackPointerType = new(BuiltinType.Integer);
    readonly PointerType CodePointerType = new(BuiltinType.Integer);
    readonly PointerType BasePointerType = new(BuiltinType.Integer);

    int AbsGlobalAddressSize => PointerSize;
    // int StackPointerSize => PointerSize;
    int CodePointerSize => PointerSize;
    int BasePointerSize => PointerSize;

    /// <summary>
    /// <c>Saved BP</c> + <c>Abs global address</c> + <c>Saved CP</c>
    /// </summary>
    int StackFrameTags => BasePointerSize + AbsGlobalAddressSize + CodePointerSize;

    public Address AbsoluteGlobalAddress => new AddressOffset(
        new AddressRegisterPointer(Register.BasePointer),
        AbsoluteGlobalOffset);
    public Address ReturnFlagAddress => new AddressOffset(
        new AddressRegisterPointer(Register.BasePointer),
        ReturnFlagOffset);
    public Address StackTop => new AddressOffset(
        new AddressRegisterPointer(Register.StackPointer),
        0);
    public Address ExitCodeAddress => new AddressOffset(
        new AddressRuntimePointer(AbsoluteGlobalAddress),
        0);

    public int ReturnFlagOffset => ReturnFlagType.GetSize(this) * BytecodeProcessor.StackDirection;
    public int SavedBasePointerOffset => 0 * BytecodeProcessor.StackDirection;
    public int AbsoluteGlobalOffset => ExitCodeType.GetSize(this) * -BytecodeProcessor.StackDirection;
    public int SavedCodePointerOffset => (AbsGlobalAddressSize + CodePointerSize) * -BytecodeProcessor.StackDirection;

    public const int InvalidFunctionAddress = int.MinValue;

    int ParametersSize
    {
        get
        {
            int sum = 0;

            foreach (CompiledParameter parameter in CompiledParameters)
            {
                sum += parameter.IsRef ? PointerSize : parameter.Type.GetSize(this);
            }

            return sum;
        }
    }

    int ParametersSizeBefore(int beforeThis)
    {
        int sum = 0;

        foreach (CompiledParameter parameter in CompiledParameters)
        {
            if (parameter.Index <= beforeThis) continue;
            sum += parameter.IsRef ? PointerSize : parameter.Type.GetSize(this);
        }

        return sum;
    }

    #endregion
}
