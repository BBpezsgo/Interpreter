namespace LanguageCore.BBCode.Generator;

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
        AddInstruction(Opcode.GetBasePointer);

        GenerateCodeForStatement(address);

        AddInstruction(Opcode.Push, GeneratedCode.Count + 3);
        AddInstruction(Opcode.MathSub);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackPointerRelative, -1 * BytecodeProcessor.StackDirection);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Runtime);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    int Call(int absoluteAddress)
    {
        int returnToValueInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Push, 0);

        StackLoad(AbsoluteGlobalAddress);
        AddInstruction(Opcode.GetBasePointer);

        AddInstruction(Opcode.SetBasePointer, AddressingMode.StackPointerRelative, 0 * BytecodeProcessor.StackDirection);

        int jumpInstruction = GeneratedCode.Count;
        AddInstruction(Opcode.Jump, AddressingMode.Absolute, absoluteAddress - GeneratedCode.Count);

        GeneratedCode[returnToValueInstruction].Parameter = GeneratedCode.Count;

        return jumpInstruction;
    }

    void Return()
    {
        AddInstruction(Opcode.SetBasePointer, AddressingMode.Runtime, 0 * BytecodeProcessor.StackDirection);
        AddInstruction(Opcode.Pop); // Pop AbsoluteGlobalOffset
        AddInstruction(Opcode.SetCodePointer, AddressingMode.Runtime);
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
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
    {
        return new ValueAddress(variable.MemoryAddress, AddressingMode.Absolute) + 3;
    }

    protected override void StackLoad(ValueAddress address)
    {
        if (address.IsReference)
        {
            StackLoad(address.ToUnreferenced());
            AddInstruction(Opcode.StackLoad, AddressingMode.Runtime);
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
                AddInstruction(Opcode.Push, address.Address);

                if (BytecodeProcessor.StackDirection > 0) AddInstruction(Opcode.MathAdd);
                else AddInstruction(Opcode.MathSub);

                AddInstruction(Opcode.StackLoad, AddressingMode.Runtime);
                break;
            case AddressingMode.BasePointerRelative:
            case AddressingMode.StackPointerRelative:
                AddInstruction(Opcode.StackLoad, address.AddressingMode, address.Address * BytecodeProcessor.StackDirection);
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
            StackLoad(address.ToUnreferenced());
            AddInstruction(Opcode.StackStore, AddressingMode.Runtime);
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
                AddInstruction(Opcode.Push, address.Address);

                if (BytecodeProcessor.StackDirection > 0) AddInstruction(Opcode.MathAdd);
                else AddInstruction(Opcode.MathSub);

                AddInstruction(Opcode.StackStore, AddressingMode.Runtime);
                break;
            case AddressingMode.BasePointerRelative:
                AddInstruction(Opcode.StackStore, AddressingMode.BasePointerRelative, address.Address * BytecodeProcessor.StackDirection);
                break;
            case AddressingMode.StackPointerRelative:
                AddInstruction(Opcode.StackStore, AddressingMode.StackPointerRelative, address.Address * BytecodeProcessor.StackDirection);
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
        { StackLoad(new ValueAddress(-1, AddressingMode.StackPointerRelative)); }
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

    public const int TagsBeforeBasePointer = 3;

    /// <summary>Stuff after BasePointer but before any variables</summary>
    readonly Stack<int> TagCount;

    public ValueAddress GetReturnValueAddress(GeneralType returnType)
    {
        return new ValueAddress(-(ParametersSize + TagsBeforeBasePointer + returnType.Size), AddressingMode.BasePointerRelative);
    }

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