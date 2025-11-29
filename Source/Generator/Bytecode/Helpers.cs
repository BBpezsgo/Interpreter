using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

enum RegisterIdentifier : byte
{
    AX,
    BX,
    CX,
    DX,
}

enum RegisterSlice : byte
{
    R,
    D,
    W,
    H,
    L,
}

readonly struct GeneralPurposeRegister : IEquatable<GeneralPurposeRegister>
{
    public static readonly GeneralPurposeRegister EAX = new(RegisterIdentifier.AX, RegisterSlice.D);
    public static readonly GeneralPurposeRegister EBX = new(RegisterIdentifier.BX, RegisterSlice.D);
    public static readonly GeneralPurposeRegister ECX = new(RegisterIdentifier.CX, RegisterSlice.D);
    public static readonly GeneralPurposeRegister EDX = new(RegisterIdentifier.DX, RegisterSlice.D);

    public readonly RegisterIdentifier Identifier;
    public readonly RegisterSlice Slice;

    public GeneralPurposeRegister(RegisterIdentifier identifier, RegisterSlice slice)
    {
        Identifier = identifier;
        Slice = slice;
    }

    public override bool Equals(object? obj) => obj is GeneralPurposeRegister other && Equals(other);
    public bool Equals(GeneralPurposeRegister other) => Identifier == other.Identifier && Slice == other.Slice;
    public override int GetHashCode() => HashCode.Combine(Identifier, Slice);

    public static bool operator ==(GeneralPurposeRegister left, GeneralPurposeRegister right) => left.Equals(right);
    public static bool operator !=(GeneralPurposeRegister left, GeneralPurposeRegister right) => !left.Equals(right);

    public static implicit operator Register(GeneralPurposeRegister reg) => reg.Identifier switch
    {
        RegisterIdentifier.AX => reg.Slice switch
        {
            RegisterSlice.R => Register.RAX,
            RegisterSlice.D => Register.EAX,
            RegisterSlice.W => Register.AX,
            RegisterSlice.H => Register.AH,
            RegisterSlice.L => Register.AL,
            _ => throw new UnreachableException(),
        },
        RegisterIdentifier.BX => reg.Slice switch
        {
            RegisterSlice.R => Register.RBX,
            RegisterSlice.D => Register.EBX,
            RegisterSlice.W => Register.BX,
            RegisterSlice.H => Register.BH,
            RegisterSlice.L => Register.BL,
            _ => throw new UnreachableException(),
        },
        RegisterIdentifier.CX => reg.Slice switch
        {
            RegisterSlice.R => Register.RCX,
            RegisterSlice.D => Register.ECX,
            RegisterSlice.W => Register.CX,
            RegisterSlice.H => Register.CH,
            RegisterSlice.L => Register.CL,
            _ => throw new UnreachableException(),
        },
        RegisterIdentifier.DX => reg.Slice switch
        {
            RegisterSlice.R => Register.RDX,
            RegisterSlice.D => Register.EDX,
            RegisterSlice.W => Register.DX,
            RegisterSlice.H => Register.DH,
            RegisterSlice.L => Register.DL,
            _ => throw new UnreachableException(),
        },
        _ => throw new UnreachableException(),
    };

    public override string ToString()
    {
        return Identifier switch
        {
            RegisterIdentifier.AX => Slice switch
            {
                RegisterSlice.R => "RAX",
                RegisterSlice.D => "EAX",
                RegisterSlice.W => "AX",
                RegisterSlice.H => "AH",
                RegisterSlice.L => "AL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.BX => Slice switch
            {
                RegisterSlice.R => "RBX",
                RegisterSlice.D => "EBX",
                RegisterSlice.W => "BX",
                RegisterSlice.H => "BH",
                RegisterSlice.L => "BL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.CX => Slice switch
            {
                RegisterSlice.R => "RCX",
                RegisterSlice.D => "ECX",
                RegisterSlice.W => "CX",
                RegisterSlice.H => "CH",
                RegisterSlice.L => "CL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.DX => Slice switch
            {
                RegisterSlice.R => "RDX",
                RegisterSlice.D => "EDX",
                RegisterSlice.W => "DX",
                RegisterSlice.H => "DH",
                RegisterSlice.L => "DL",
                _ => throw new UnreachableException(),
            },
            _ => throw new UnreachableException(),
        };
    }

    public bool Overlaps(GeneralPurposeRegister other)
    {
        if (Identifier != other.Identifier)
        {
            return false;
        }

        return Slice switch
        {
            RegisterSlice.R => true,
            RegisterSlice.D => true,
            RegisterSlice.W => true,
            RegisterSlice.H => other.Slice is not RegisterSlice.L,
            RegisterSlice.L => other.Slice is not RegisterSlice.H,
            _ => throw new UnreachableException(),
        };
    }
}

public partial class CodeGeneratorForMain : CodeGenerator
{
    void AddComment(string comment)
    {
        if (DebugInfo is null) return;
        if (DebugInfo.CodeComments.TryGetValue(Code.Offset, out List<string>? comments))
        { comments.Add(comment); }
        else
        { DebugInfo.CodeComments.Add(Code.Offset, new List<string>() { comment }); }
    }

    #region Helper Functions

    void CallRuntime(CompiledExpression address)
    {
        if (!address.Type.Is(out FunctionType? addressType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This should be a function pointer and not \"{address.Type}\"", address));
            return;
        }

        InstructionLabel returnLabel = Code.DefineLabel();
        Push(returnLabel.Absolute());

        //PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this, Diagnostics, address));
        Push(Register.BasePointer);

        GenerateCodeForStatement(address);

        if (addressType.HasClosure)
        {
            using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
            {
                PopTo(reg.Register);
                CheckPointerNull(reg.Register);
                PushFrom(reg.Register, 0, PointerSize);
            }
        }

        using (RegisterUsage.Auto reg = Registers.GetFree(addressType.GetBitWidth(this, Diagnostics, address)))
        {
            PopTo(reg.Register);
            InstructionLabel offsetLabel = Code.DefineLabel();
            Code.Emit(Opcode.MathSub, reg.Register, offsetLabel.Absolute());
            Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);
            Code.MarkLabel(offsetLabel);
            Code.Emit(Opcode.Jump, reg.Register);
        }

        Code.MarkLabel(returnLabel);
    }

    void Call(InstructionLabel label, ILocated callerLocation, bool captureGlobalVariables)
    {
        InstructionLabel returnLabel = Code.DefineLabel();
        Code.Emit(Opcode.Push, returnLabel.Absolute());

        if (captureGlobalVariables)
        {
            PushFrom(AbsoluteGlobalAddress, AbsGlobalAddressType.GetSize(this, Diagnostics, callerLocation));
        }
        Push(Register.BasePointer);

        Code.Emit(Opcode.Move, Register.BasePointer, Register.StackPointer);

        Code.Emit(Opcode.Jump, label.Relative());

        Code.MarkLabel(returnLabel);
    }

    void Return(ILocated location)
    {
        PopTo(Register.BasePointer);
        if (HasCapturedGlobalVariables)
        {
            Pop(AbsGlobalAddressType.GetSize(this, Diagnostics, location)); // Pop AbsoluteGlobalOffset
        }
        Code.Emit(Opcode.Return);
        ScopeSizes.LastRef -= PointerSize;
    }

    #endregion

    #region Memory Helpers

    AddressOffset GetVariableAddress(CompiledVariableDefinition variable)
    {
        GeneratedVariable? generatedVariable;

        if (variable.IsGlobal)
        {
            if (!GeneratedVariables.TryGetValue(variable, out generatedVariable))
            { throw new LanguageException($"Variable `{variable}` was not compiled", variable.Location.Position, variable.Location.File); }

            if (!HasCapturedGlobalVariables)
            { throw new LanguageException($"Unexpected global variable `{variable}`", variable.Location.Position, variable.Location.File); }

            return new AddressOffset(
                new AddressPointer(AbsoluteGlobalAddress),
                0
                + generatedVariable.MemoryAddress
            //  + ((
            //      AbsGlobalAddressSize
            //      + BasePointerSize
            //  ) * BytecodeProcessor.StackDirection)
            );
        }

        if (CurrentContext is CompiledLambda compiledLambda)
        {
            int offset = PointerSize;
            foreach (CapturedLocal capturedLocal in compiledLambda.CapturedLocals)
            {
                if (capturedLocal.Variable is not null)
                {
                    if (capturedLocal.Variable == variable)
                    {
                        return new AddressOffset(new AddressPointer(GetParameterAddress(0)), offset);
                    }
                }
                offset += (capturedLocal.Variable?.Type ?? capturedLocal.Parameter?.Type)!.GetSize(this);
            }
        }

        if (!GeneratedVariables.TryGetValue(variable, out generatedVariable))
        { throw new LanguageException($"Variable {variable} was not compiled", variable.Location.Position, variable.Location.File); }

        return new AddressOffset(
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
            ) * ProcessorState.StackDirection)
        //  - returnType.SizeBytes // We at the end of the return value, but we want to be at the start
        //  + 1 // Stack pointer offset (???)
        );
    }

    public AddressOffset GetParameterAddress(CompiledParameter parameter, int offset = 0)
    {
        if (CurrentContext is CompiledLambda compiledLambda)
        {
            int _offset = PointerSize;
            foreach (CapturedLocal capturedLocal in compiledLambda.CapturedLocals)
            {
                if (capturedLocal.Parameter is not null)
                {
                    if (capturedLocal.Parameter == parameter)
                    {
                        return new AddressOffset(new AddressPointer(GetParameterAddress(0)), _offset + offset);
                    }
                }
                _offset += (capturedLocal.Variable?.Type ?? capturedLocal.Parameter?.Type)!.GetSize(this);
            }
        }

        return GetParameterAddress(GetParameterIndex(parameter), offset);
    }

    int ParametersSizeBefore(int beforeThis)
    {
        int sum = 0;

        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            if (i <= beforeThis) continue;
            CompiledParameter parameter = CompiledParameters[i];
            sum += parameter.IsRef ? PointerSize : parameter.Type.GetSize(this, Diagnostics, parameter);
        }

        return sum;
    }

    public AddressOffset GetParameterAddress(int index, int offset = 0)
    {
        return new(
            Register.BasePointer,
            0 // We start at the saved base pointer
            - ((
                ParametersSizeBefore(index) // ???
                + StackFrameTags // Offset by the stack frame stuff
            ) * ProcessorState.StackDirection)
            + offset
        //  + 1 // Stack pointer offset (???)
            );
    }

    void Pop(int size)
    {
        Code.Emit(Opcode.MathAdd, Register.StackPointer, size);
        ScopeSizes.LastRef -= size;
    }

    void StackAlloc(int size, bool initializeZero)
    {
        if (!initializeZero)
        {
            Code.Emit(Opcode.MathSub, Register.StackPointer, size);
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
            case AddressAbsolute v: PopTo(v, size); break;
            default: throw new NotImplementedException();
        }
    }

    void PopTo(InstructionOperand destination, BitWidth size)
    {
        if (PointerBitWidth == BitWidth._64 &&
            size is BitWidth._8 or BitWidth._32)
        { throw new NotImplementedException(); }

        Code.Emit(size switch
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
        Code.Emit(destination.BitWidth() switch
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
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PopTo(new AddressRegisterPointer(reg.Register), size, address.Offset);
                }

                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                PopTo(new AddressRegisterPointer(registerPointer.Register), size, address.Offset);

                break;
            }

            default:
            {
                GenerateAddressResolver(address);
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PopTo(new AddressRegisterPointer(reg.Register), size);
                }
                break;
            }
        }
    }

    void PopTo(AddressPointer address, int size)
    {
        PushFrom(address.PointerAddress, PointerSize);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), size);
        }
    }

    void PopTo(AddressRegisterPointer address, int size, int offset = 0)
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

                PopTo(address.Register.ToPtr(currentOffset + offset, checkBitWidth), checkBitWidth);
                currentOffset += checkSize;
                break;
            }
        }
    }

    void PopTo(AddressRuntimePointer address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), size);
        }
    }

    void PopTo(AddressRuntimeIndex address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), size);
        }
    }

    void PopTo(AddressAbsolute address, int size)
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

                PopTo(new AddressAbsolute(address.Value + currentOffset), checkBitWidth);
                currentOffset += checkSize;
                break;
            }
        }
    }

    void PopTo(AddressAbsolute address, BitWidth size)
    {
        Code.Emit(size switch
        {
            BitWidth._8 => Opcode.PopTo8,
            BitWidth._16 => Opcode.PopTo16,
            BitWidth._32 => Opcode.PopTo32,
            BitWidth._64 => Opcode.PopTo64,
            _ => throw new UnreachableException(),
        }, new InstructionOperand(address.Value, size switch
        {
            BitWidth._8 => InstructionOperandType.Pointer8,
            BitWidth._16 => InstructionOperandType.Pointer16,
            BitWidth._32 => InstructionOperandType.Pointer32,
            BitWidth._64 => throw new NotImplementedException(),
            _ => throw new UnreachableException(),
        }));
        ScopeSizes.LastRef -= (int)size;
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
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PushFrom(reg.Register, address.Offset, size);
                }

                break;
            }

            case AddressRegisterPointer registerPointer:
            {
                PushFrom(registerPointer.Register, address.Offset, size);
                break;
            }

            default:
            {
                GenerateAddressResolver(address);
                using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
                {
                    PopTo(reg.Register);
                    PushFrom(reg.Register, 0, size);
                }
                break;
            }
        }
    }

    void PushFrom(Register register, int offset, int size)
    {
        int currentOffset = size;

        while (currentOffset > 0)
        {
            if (currentOffset >= 8 && PointerSize >= 8)
            {
                Push(register.ToPtr(currentOffset - 8 + offset, BitWidth._64));
                currentOffset -= 8;
            }
            else if (currentOffset >= 4)
            {
                Push(register.ToPtr(currentOffset - 4 + offset, BitWidth._32));
                currentOffset -= 4;
            }
            else if (currentOffset >= 2)
            {
                Push(register.ToPtr(currentOffset - 2 + offset, BitWidth._16));
                currentOffset -= 2;
            }
            else if (currentOffset >= 1)
            {
                Push(register.ToPtr(currentOffset - 1 + offset, BitWidth._8));
                currentOffset -= 1;
            }
            else
            {
                throw new UnreachableException();
            }
        }
    }

    void PushFrom(AddressPointer address, int size)
    {
        PushFrom(address.PointerAddress, PointerSize);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PushFrom(reg.Register, 0, size);
        }
    }

    void PushFrom(AddressRegisterPointer address, int size)
    {
        PushFrom(address.Register, 0, size);
    }

    void PushFrom(AddressRuntimePointer address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PushFrom(reg.Register, 0, size);
        }
    }

    void PushFrom(AddressRuntimeIndex address, int size)
    {
        GenerateAddressResolver(address);
        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PushFrom(reg.Register, 0, size);
        }
    }

    void Push(int value) => Push(new InstructionOperand(value));
    void Push(CompiledValue value) => Push(new InstructionOperand(value));
    void Push(Register value) => Push((InstructionOperand)value);
    void Push(InstructionOperand value)
    {
        Code.Emit(Opcode.Push, value);
        ScopeSizes.LastRef += (int)value.BitWidth;
        if (ScopeSizes.Last >= Settings.StackSize)
        { Diagnostics.Add(new DiagnosticWithoutContext(DiagnosticsLevel.Warning, "Stack will overflow")); }
    }
    void Push(PreparationInstructionOperand value)
    {
        Code.Emit(Opcode.Push, value);

        if (value.IsLabelAddress) ScopeSizes.LastRef += (int)BitWidth._32;
        else ScopeSizes.LastRef += (int)value.Value.BitWidth;

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

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            Code.Emit(Opcode.Compare, reg.Register, 0);
        }

        InstructionLabel skipLabel = Code.DefineLabel();
        Code.Emit(Opcode.JumpIfNotEqual, skipLabel.Relative());

        //using (RegisterUsage.Auto reg = Registers.GetFree())
        //{
        Code.Emit(Opcode.Crash, 0);
        //}

        Code.MarkLabel(skipLabel);

        AddComment($"}}");
    }

    void CheckPointerNull(Register register)
    {
        if (!Settings.CheckNullPointers) return;

        AddComment($"Check for pointer zero (in {register}) {{");

        Code.Emit(Opcode.Compare, register, 0);

        InstructionLabel skipLabel = Code.DefineLabel();
        Code.Emit(Opcode.JumpIfNotEqual, skipLabel.Relative());
        Code.Emit(Opcode.Crash, 0);
        Code.MarkLabel(skipLabel);

        AddComment($"}}");
    }

    void PushFromChecked(Address address, int size)
    {
        GenerateAddressResolver(address);
        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            for (int i = size - 1; i >= 0; i--)
            { Push(reg.Register.ToPtr(i, BitWidth._8)); }
        }
    }

    void PopToChecked(Address address, int size)
    {
        GenerateAddressResolver(address);
        CheckPointerNull();

        using (RegisterUsage.Auto reg = Registers.GetFree(PointerBitWidth))
        {
            PopTo(reg.Register);
            PopTo(new AddressRegisterPointer(reg.Register), size);
        }
    }

    #endregion

    #region Memory Helpers

    bool GetAddress(CompiledExpression value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        switch (value)
        {
            case CompiledVariableAccess v: return GetAddress(v, out address, out error);
            case CompiledParameterAccess v: return GetAddress(v, out address, out error);
            case CompiledElementAccess v: return GetAddress(v, out address, out error);
            case CompiledFieldAccess v: return GetAddress(v, out address, out error);
            default:
                address = null;
                error = new PossibleDiagnostic($"Can't get the address of {value.GetType().Name}", value);
                return false;
        }
    }

    bool GetAddress(CompiledVariableAccess value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        address = GetVariableAddress(value.Variable);
        error = null;
        return true;
    }

    bool GetAddress(CompiledParameterAccess value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        address = GetParameterAddress(value.Parameter);
        error = null;
        return true;
    }

    bool GetAddress(CompiledElementAccess indexCall, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
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

        if (indexCall.Index is CompiledConstantValue evaluatedStatement)
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

    bool GetAddress(CompiledFieldAccess value, [NotNullWhen(true)] out Address? address, [NotNullWhen(false)] out PossibleDiagnostic? error)
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

    CompiledExpression? NeedDerefernce(CompiledExpression value) => value switch
    {
        CompiledVariableAccess => null,
        CompiledParameterAccess => null,
        CompiledElementAccess v => NeedDerefernce(v),
        CompiledFieldAccess v => NeedDerefernce(v),
        CompiledFunctionCall v => NeedDerefernce(v),
        _ => throw new NotImplementedException()
    };
    CompiledExpression? NeedDerefernce(CompiledFunctionCall functionCall)
    {
        if (functionCall.Type.Is<PointerType>())
        { return functionCall; }

        return null;
    }
    CompiledExpression? NeedDerefernce(CompiledElementAccess indexCall)
    {
        if (indexCall.Base.Type.Is<PointerType>())
        { return indexCall.Base; }

        return NeedDerefernce(indexCall.Base);
    }
    CompiledExpression? NeedDerefernce(CompiledFieldAccess field)
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

    bool HasCapturedGlobalVariables { get; set; }
    int AbsGlobalAddressSize => PointerSize;
    // int StackPointerSize => PointerSize;
    int CodePointerSize => PointerSize;
    int BasePointerSize => PointerSize;

    /// <summary>
    /// <c>Saved BP</c> + <c>Abs global address</c> + <c>Saved CP</c>
    /// </summary>
    int StackFrameTags => BasePointerSize + (HasCapturedGlobalVariables ? AbsGlobalAddressSize : 0) + CodePointerSize;

    public Address AbsoluteGlobalAddress => new AddressOffset(
        new AddressRegisterPointer(Register.BasePointer),
        AbsoluteGlobalOffset);
    public Address StackTop => new AddressOffset(
        new AddressRegisterPointer(Register.StackPointer),
        0);
    public Address ExitCodeAddress => new AddressOffset(
        new AddressPointer(AbsoluteGlobalAddress),
        GlobalVariablesSize);

    public int SavedBasePointerOffset => 0 * ProcessorState.StackDirection;
    public int AbsoluteGlobalOffset => ExitCodeType.GetSize(this) * -ProcessorState.StackDirection;
    public int SavedCodePointerOffset => ((HasCapturedGlobalVariables ? AbsGlobalAddressSize : 0) + CodePointerSize) * -ProcessorState.StackDirection;

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

    #endregion
}
