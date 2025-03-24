using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Helper Functions

    void CallRuntime(CompiledStatementWithValue address)
    {
        GeneralType addressType = address.Type;

        if (!addressType.Is<FunctionType>())
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a function pointer and not \"{addressType}\"", address));
            return;
        }

        int returnToValueInstruction = GeneratedCode.Count;
        Push(0);

        PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this, Diagnostics, address));
        Push(Register.BasePointer);

        GenerateCodeForStatement(address);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(addressType.GetBitWidth(this, Diagnostics, address)));
            AddInstruction(Opcode.MathSub, reg.Get(addressType.GetBitWidth(this, Diagnostics, address)), GeneratedCode.Count + 2);

            AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.Jump, reg.Get(addressType.GetBitWidth(this, Diagnostics, address)));

            GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;
        }
    }

    int Call(int absoluteAddress, ILocated callerLocation)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this, Diagnostics, callerLocation));
        Push(Register.BasePointer);

        AddInstruction(Opcode.Move, Register.BasePointer, Register.StackPointer);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Operand1 = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return(ILocated location)
    {
        PopTo(Register.BasePointer);
        Pop(AbsGlobalAddressType.GetSize(this, Diagnostics, location)); // Pop AbsoluteGlobalOffset
        AddInstruction(Opcode.Return);
        ScopeSizes.LastRef -= PointerSize;
    }

    #endregion

    #region Memory Helpers

    AddressOffset GetGlobalVariableAddress(CompiledVariableDeclaration variable)
    {
        if (!variable.IsGlobal)
        { Diagnostics.Add(Diagnostic.Internal($"Trying to get the address of global variable \"{variable.Identifier}\" but it was compiled as a local variable.", variable)); }

        if (!GeneratedVariables.TryGetValue(variable, out GeneratedVariable? generatedVariable))
        { throw new NotImplementedException(); }

        return new(
                new AddressPointer(AbsoluteGlobalAddress),
                0
                + generatedVariable.MemoryAddress
            //  + ((
            //      AbsGlobalAddressSize
            //      + BasePointerSize
            //  ) * BytecodeProcessor.StackDirection)
            );
    }

    AddressOffset GetLocalVariableAddress(CompiledVariableDeclaration variable)
    {
        if (variable.IsGlobal)
        { Diagnostics.Add(Diagnostic.Internal($"Trying to get the address of local variable \"{variable.Identifier}\" but it was compiled as a global variable.", variable)); }

        if (!GeneratedVariables.TryGetValue(variable, out GeneratedVariable? generatedVariable))
        { throw new LanguageException($"Variable {variable} was not compiled", variable.Location.Position, variable.Location.File); }

        return new(
                Register.BasePointer,
                generatedVariable.MemoryAddress
            );
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter")]
    public AddressOffset GetReturnValueAddress(GeneralType returnType)
    {
        return new(
                Register.BasePointer,
                0 // We start at the saved base pointer
                - ((
                    ParametersSize // Offset by the parameters
                    + StackFrameTags // Offset by the stack frame stuff
                ) * BytecodeProcessor.StackDirection)
                // - returnType.SizeBytes // We at the end of the return value, but we want to be at the start
                // + 1 // Stack pointer offset (???)
                );
    }

    public AddressOffset GetParameterAddress(CompiledParameter parameter, int offset = 0)
    {
        return new(
                Register.BasePointer,
                0 // We start at the saved base pointer
                - ((
                    ParametersSizeBefore(parameter.Index) // ???
                    + StackFrameTags // Offset by the stack frame stuff
                ) * BytecodeProcessor.StackDirection)
                + offset
                // + 1 // Stack pointer offset (???)
                );
    }

    void Pop(int size)
    {
        if (PointerBitWidth >= BitWidth._64)
        {
            int qwordCount = size / 8;
            size %= 8;
            for (int i = 0; i < qwordCount; i++)
            {
                AddInstruction(Opcode.Pop64);
                ScopeSizes.LastRef -= 8;
            }
        }

        if (PointerBitWidth != BitWidth._64)
        {
            int dwordCount = size / 4;
            size %= 4;
            for (int i = 0; i < dwordCount; i++)
            {
                AddInstruction(Opcode.Pop32);
                ScopeSizes.LastRef -= 4;
            }
        }

        int wordCount = size / 2;
        size %= 2;
        for (int i = 0; i < wordCount; i++)
        {
            AddInstruction(Opcode.Pop16);
            ScopeSizes.LastRef -= 2;
        }

        for (int i = 0; i < size; i++)
        {
            AddInstruction(Opcode.Pop8);
            ScopeSizes.LastRef -= 1;
        }
    }

    void StackAlloc(int size, bool initializeZero)
    {
        if (!initializeZero)
        {
            AddInstruction(Opcode.MathSub, Register.StackPointer, size);
            ScopeSizes.LastRef += size;
            if (ScopeSizes.Last >= Settings.StackSize)
            { Diagnostics.Add(new DiagnosticWithoutContext(DiagnosticsLevel.Warning, "Stack will overflow")); }
            return;
        }

        if (PointerBitWidth != BitWidth._64)
        {
            int dwordCount = size / 4;
            size %= 4;
            for (int i = 0; i < dwordCount; i++)
            { Push(new CompiledValue((int)0)); }
        }

        int wordCount = size / 2;
        size %= 2;
        for (int i = 0; i < wordCount; i++)
        { Push(new CompiledValue((ushort)0)); }

        for (int i = 0; i < size; i++)
        { Push(new CompiledValue((byte)0)); }
    }

    void PopTo(Address address, int size)
    {
        switch (address)
        {
            case AddressOffset v: PopTo(v, size); break;
            case AddressPointer v: PopTo(v, size); break;
            case AddressRegisterPointer v: PopTo(v, size); break;
            case AddressRuntimePointer v: PopTo(v, size); break;
            case AddressRuntimeIndex v: PopTo(v, size); break;
            default: throw new NotImplementedException();
        }
    }

    void PopTo(InstructionOperand destination, BitWidth size)
    {
        if (PointerBitWidth == BitWidth._64 &&
            size is BitWidth._8 or BitWidth._32)
        { throw new NotImplementedException(); }

        AddInstruction(size switch
        {
            0 => default,
            BitWidth._8 => Opcode.PopTo8,
            BitWidth._16 => Opcode.PopTo16,
            BitWidth._32 => Opcode.PopTo32,
            BitWidth._64 => Opcode.PopTo64,
            _ => throw new UnreachableException(),
        }, destination);
        ScopeSizes.LastRef -= (int)size;
    }

    void PopTo(Register destination)
    {
        AddInstruction(destination.BitWidth() switch
        {
            0 => default,
            BitWidth._8 => Opcode.PopTo8,
            BitWidth._16 => Opcode.PopTo16,
            BitWidth._32 => Opcode.PopTo32,
            BitWidth._64 => Opcode.PopTo64,
            _ => throw new UnreachableException(),
        }, destination);
        ScopeSizes.LastRef -= (int)destination.BitWidth();
    }

    void PopTo(AddressOffset address, int size)
    {
        switch (address.Base)
        {
            case AddressPointer addressPointer:
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
            {
                GenerateAddressResolver(address);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }
                break;
            }
        }
    }

    void PopTo(AddressPointer address, int size)
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
                ((IEnumerable<BitWidth>)Enum.GetValues<BitWidth>()).Reverse()
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

    void PopTo(AddressRuntimePointer address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void PopTo(AddressRuntimeIndex address, int size)
    {
        AddComment($"Resolver address {{");
        GenerateAddressResolver(address);
        AddComment($"}}");
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddComment($"Pop address:");
            PopTo(reg.Get(PointerBitWidth));
            AddComment($"Pop value:");
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void PushFrom(Address address, int size)
    {
        switch (address)
        {
            case AddressOffset v: PushFrom(v, size); break;
            case AddressPointer v: PushFrom(v, size); break;
            case AddressRegisterPointer v: PushFrom(v, size); break;
            case AddressRuntimePointer v: PushFrom(v, size); break;
            case AddressRuntimeIndex v: PushFrom(v, size); break;
            default: throw new NotImplementedException();
        }
    }

    void PushFrom(AddressOffset address, int size)
    {
        switch (address.Base)
        {
            case AddressPointer addressPointer:
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
                            ((IEnumerable<BitWidth>)Enum.GetValues<BitWidth>()).Reverse()
#endif
                        )
                        {
                            int checkSize = (int)checkBitWidth;
                            if (currentOffset < checkSize - 1) continue;
                            if (PointerSize < checkSize) continue;

                            Push(reg.Get(PointerBitWidth).ToPtr(currentOffset + address.Offset - (checkSize - 1), checkBitWidth));
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
            {
                GenerateAddressResolver(address);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    PopTo(reg.Get(PointerBitWidth));
                    PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
                }
                break;
            }
        }
    }

    void PushFrom(AddressPointer address, int size)
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
            //     Push(address.Register.ToPtr(currentOffset - (checkSize - 1), checkBitWidth));
            //     currentOffset -= checkSize;
            //     break;
            // }
            if (currentOffset >= 8 && PointerSize >= 8)
            {
                Push(address.Register.ToPtr(currentOffset - 7, BitWidth._64));
                currentOffset -= 8;
            }
            else if (currentOffset >= 4)
            {
                Push(address.Register.ToPtr(currentOffset - 3, BitWidth._32));
                currentOffset -= 4;
            }
            else if (currentOffset >= 2)
            {
                Push(address.Register.ToPtr(currentOffset - 1, BitWidth._16));
                currentOffset -= 2;
            }
            else
            {
                Push(address.Register.ToPtr(currentOffset, BitWidth._8));
                currentOffset -= 1;
            }
        }
    }

    void PushFrom(AddressRuntimePointer address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void PushFrom(AddressRuntimeIndex address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PushFrom(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    void Push(int value) => Push(new InstructionOperand(value));
    void Push(CompiledValue value) => Push(new InstructionOperand(value));
    void Push(Register value) => Push((InstructionOperand)value);
    void Push(InstructionOperand value)
    {
        AddInstruction(Opcode.Push, value);
        ScopeSizes.LastRef += (int)value.BitWidth;
        if (ScopeSizes.Last >= Settings.StackSize)
        { Diagnostics.Add(new DiagnosticWithoutContext(DiagnosticsLevel.Warning, "Stack will overflow")); }
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
        }

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.JumpIfNotEqual, 0);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.Crash, 0);
        }
        GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction;

        AddComment($"}}");
    }

    void PushFromChecked(Address address, int size)
    {
        GenerateAddressResolver(address);
        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            for (int i = size - 1; i >= 0; i--)
            { Push(reg.Get(PointerBitWidth).ToPtr(i, BitWidth._8)); }
        }
    }

    void PopToChecked(Address address, int size)
    {
        GenerateAddressResolver(address);
        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            PopTo(reg.Get(PointerBitWidth));
            PopTo(new AddressRegisterPointer(reg.Get(PointerBitWidth)), size);
        }
    }

    #endregion

    #region Memory Helpers

    bool GetAddress(CompiledStatementWithValue value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error) => value switch
    {
        CompiledVariableGetter v => GetAddress(v, out address, out error),
        CompiledParameterGetter v => GetAddress(v, out address, out error),
        CompiledIndexGetter v => GetAddress(v, out address, out error),
        CompiledFieldGetter v => GetAddress(v, out address, out error),
        _ => throw new NotImplementedException()
    };

    bool GetAddress(CompiledVariableGetter value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (value.Variable.IsGlobal)
        {
            address = GetGlobalVariableAddress(value.Variable);
        }
        else
        {
            address = GetLocalVariableAddress(value.Variable);
        }
        error = null;
        return true;
    }

    bool GetAddress(CompiledParameterGetter value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        address = GetParameterAddress(value.Variable);
        error = null;
        return true;
    }

    bool GetAddress(CompiledIndexGetter indexCall, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = default;
        address = default;

        GeneralType prevType = indexCall.Base.Type;
        ArrayType? array;
        Address? baseAddress;

        if (prevType.Is(out PointerType? prevPointerType))
        {
            if (!prevPointerType.To.Is(out array))
            {
                error = new PossibleDiagnostic($"Multiple dereference is not supported at the moment", indexCall.Base);
                return false;
            }

            baseAddress = new AddressRuntimePointer(indexCall.Base);
        }
        else if (!prevType.Is(out array))
        {
            error = new PossibleDiagnostic($"Can't index a non-array type", indexCall.Base);
            return false;
        }
        else if (!GetAddress(indexCall.Base, out baseAddress, out error))
        {
            return false;
        }

        int elementSize = array.Of.GetSize(this, Diagnostics, indexCall);

        if (indexCall.Index is CompiledEvaluatedValue evaluatedStatement)
        {
            int offset = (int)evaluatedStatement.Value * array.Of.GetSize(this, Diagnostics, indexCall);
            address = new AddressOffset(baseAddress, offset);
            return true;
        }
        else
        {
            address = new AddressRuntimeIndex(baseAddress, indexCall.Index, elementSize);
            return true;
        }
    }

    bool GetAddress(CompiledFieldGetter value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        address = default;

        GeneralType prevType = value.Object.Type;
        Address? baseAddress = null;

        while (prevType.Is(out PointerType? pointerType))
        {
            prevType = pointerType.To;
            baseAddress =
                baseAddress is null
                ? new AddressRuntimePointer(value.Object)
                : new AddressPointer(baseAddress);
        }

        if (!prevType.Is(out StructType? @struct))
        {
            error = new PossibleDiagnostic($"This is not a struct", value.Object);
            return false;
        }

        if (baseAddress is null &&
            !GetAddress(value.Object, out baseAddress, out error))
        {
            return false;
        }

        if (!@struct.GetField(value.Field.Identifier.Content, this, out _, out int fieldOffset, out error))
        {
            return false;
        }

        address = new AddressOffset(baseAddress, fieldOffset);
        return true;
    }

    CompiledStatementWithValue? NeedDerefernce(CompiledStatementWithValue value) => value switch
    {
        CompiledVariableGetter => null,
        CompiledParameterGetter => null,
        CompiledIndexGetter v => NeedDerefernce(v),
        CompiledFieldGetter v => NeedDerefernce(v),
        _ => throw new NotImplementedException()
    };
    CompiledStatementWithValue? NeedDerefernce(CompiledIndexGetter indexCall)
    {
        if (indexCall.Base.Type.Is<PointerType>())
        { return indexCall.Base; }

        return NeedDerefernce(indexCall.Base);
    }
    CompiledStatementWithValue? NeedDerefernce(CompiledFieldGetter field)
    {
        if (field.Object.Type.Is<PointerType>())
        { return field.Object; }

        return NeedDerefernce(field.Object);
    }

    #endregion

    #region Addressing Helpers

    readonly BuiltinType ExitCodeType = BuiltinType.I32;
    readonly PointerType AbsGlobalAddressType = new(BuiltinType.I32);
    // readonly PointerType StackPointerType = new(BuiltinType.Integer);
    readonly PointerType CodePointerType = new(BuiltinType.I32);
    readonly PointerType BasePointerType = new(BuiltinType.I32);

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
    public Address StackTop => new AddressOffset(
        new AddressRegisterPointer(Register.StackPointer),
        0);
    public Address ExitCodeAddress => new AddressOffset(
        new AddressPointer(AbsoluteGlobalAddress),
        GlobalVariablesSize);

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
                sum += parameter.IsRef ? PointerSize : parameter.Type.GetSize(this, Diagnostics, parameter);
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
            sum += parameter.IsRef ? PointerSize : parameter.Type.GetSize(this, Diagnostics, parameter);
        }

        return sum;
    }

    #endregion
}
