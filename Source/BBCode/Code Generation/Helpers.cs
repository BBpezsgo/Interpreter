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
            AddInstruction(Opcode.Pop, reg.Register);
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
        AddInstruction(Opcode.Pop, Register.BasePointer);
        AddInstruction(Opcode.Pop); // Pop AbsoluteGlobalOffset
        AddInstruction(Opcode.Return);
    }

    int GenerateInitialValue(GeneralType type)
    {
        if (type is StructType structType)
        {
            ImmutableDictionary<string, GeneralType>? typeParameters = structType.TypeArguments;
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
        => new ValueAddress(variable.MemoryAddress, AddressingMode.Absolute) + 3;

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
                AddInstruction(Opcode.Pop, reg.Register);
                AddInstruction(Opcode.Push, reg.Register.ToPtr());
            }
            return;
        }

        if (address.InHeap)
        {
            throw new NotImplementedException();
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
                StackLoad(AbsoluteGlobalAddress);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Pop, reg.Register);

                    if (BytecodeProcessor.StackDirection > 0)
                    {
                        AddInstruction(Opcode.MathAdd, reg.Register, address.Address);
                    }
                    else
                    {
                        AddInstruction(Opcode.MathSub, reg.Register, address.Address);
                    }

                    AddInstruction(Opcode.Push, reg.Register.ToPtr());
                }
                break;
            case AddressingMode.BasePointerRelative:
                AddInstruction(Opcode.Push, Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;

            case AddressingMode.StackPointerRelative:
                AddInstruction(Opcode.Push, Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;

            case AddressingMode.Runtime:
                AddInstruction(Opcode.Push, new InstructionOperand(address.Address, InstructionOperandType.Pointer));
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
                AddInstruction(Opcode.Pop, reg.Register);
                AddInstruction(Opcode.Pop, reg.Register.ToPtr());
            }
            return;
        }

        if (address.InHeap)
        {
            throw new NotImplementedException();
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
                StackLoad(AbsoluteGlobalAddress);
                using (RegisterUsage.Auto reg = Registers.GetFree())
                {
                    AddInstruction(Opcode.Pop, reg.Register);

                    if (BytecodeProcessor.StackDirection > 0)
                    {
                        AddInstruction(Opcode.MathAdd, reg.Register, address.Address);
                    }
                    else
                    {
                        AddInstruction(Opcode.MathSub, reg.Register, address.Address);
                    }

                    AddInstruction(Opcode.Pop, reg.Register.ToPtr());
                }
                break;
            case AddressingMode.BasePointerRelative:
                AddInstruction(Opcode.Pop, Register.BasePointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;
            case AddressingMode.StackPointerRelative:
                AddInstruction(Opcode.Pop, Register.StackPointer.ToPtr(address.Address * BytecodeProcessor.StackDirection));
                break;
            case AddressingMode.Runtime:
                AddInstruction(Opcode.Pop, new InstructionOperand(address.Address, InstructionOperandType.Pointer));
                break;
            default: throw new UnreachableException();
        }
    }

    void CheckPointerNull(bool preservePointer = true, string exceptionMessage = "null pointer")
    {
        if (!Settings.CheckNullPointers) return;
        AddComment($"Check for pointer zero {{");
        if (preservePointer)
        { StackLoad(new ValueAddress(-1, AddressingMode.StackPointerRelative)); }

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.Pop, reg.Register);
            AddInstruction(Opcode.Compare, reg.Register, 0);
            AddInstruction(Opcode.JumpIfNotEqual, 0);
        }

        int jumpInstruction = GeneratedCode.Count - 1;

        GenerateCodeForLiteralString(exceptionMessage);
        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.Pop, reg.Register);
            AddInstruction(Opcode.Throw, reg.Register);
        }
        GeneratedCode[jumpInstruction].Operand1 = GeneratedCode.Count - jumpInstruction;

        AddComment($"}}");
    }

    void HeapLoad(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.Pop, reg.Register);
            AddInstruction(Opcode.Push, reg.Register.ToPtr(offset));
        }
    }

    void HeapStore(ValueAddress pointerAddress, int offset, string nullExceptionMessage = "null pointer")
    {
        StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.AddressingMode, pointerAddress.IsReference));

        CheckPointerNull(exceptionMessage: nullExceptionMessage);

        using (RegisterUsage.Auto reg = Registers.GetFree())
        {
            AddInstruction(Opcode.Pop, reg.Register);
            AddInstruction(Opcode.Pop, reg.Register.ToPtr(offset));
        }
    }

    #endregion

    #region Addressing Helpers

    public const int TagsBeforeBasePointer = 3;

    /// <summary>Stuff after BasePointer but before any variables</summary>
    readonly Stack<int> TagCount;

    public ValueAddress GetReturnValueAddress(GeneralType returnType)
        => new(-(ParametersSize + TagsBeforeBasePointer + returnType.Size), AddressingMode.BasePointerRelative);

    public static ValueAddress SavedBasePointerAddress => new(SavedBasePointerOffset, AddressingMode.BasePointerRelative);
    public static ValueAddress SavedCodePointerAddress => new(SavedCodePointerOffset, AddressingMode.BasePointerRelative);
    public static ValueAddress AbsoluteGlobalAddress => new(AbsoluteGlobalOffset, AddressingMode.BasePointerRelative);
    public static ValueAddress ReturnFlagAddress => new(ReturnFlagOffset, AddressingMode.BasePointerRelative);

    public const int SavedBasePointerOffset = -1;
    public const int SavedCodePointerOffset = -3;
    public const int AbsoluteGlobalOffset = -2;

    public const int ReturnFlagOffset = 0;

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
