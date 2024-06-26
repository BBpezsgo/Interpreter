﻿#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable RCS1213 // Remove unused member declaration
#pragma warning disable CS0414
#pragma warning disable CS0612 // Type or member is obsolete

namespace LanguageCore.ASM.Generator;

using BBLang.Generator;
using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using InstructionOperand = LanguageCore.ASM.InstructionOperand;
using LiteralStatement = Parser.Statement.Literal;
using ParameterCleanupItem = (int Size, bool CanDeallocate, Compiler.GeneralType Type);

public class ImportedAsmFunction
{
    public readonly string Name;
    public readonly int ParameterSizeBytes;

    public ImportedAsmFunction(string name, int parameterSizeBytes)
    {
        Name = name;
        ParameterSizeBytes = parameterSizeBytes;
    }

    public override string ToString() => $"_{Name}@{ParameterSizeBytes}";
}

public struct AsmGeneratorSettings
{
    public bool Is16Bits;
}

public struct AsmGeneratorResult
{
    public string AssemblyCode;
}

public class CodeGeneratorForAsm : CodeGenerator
{
    #region Fields

    readonly AsmGeneratorSettings GeneratorSettings;
    readonly AssemblyCode Builder;

    readonly List<(CompiledFunction Function, string Label)> FunctionLabels;
    readonly List<CompiledFunction> GeneratedFunctions;
    readonly Stack<int> FunctionFrameSize;

    readonly int ByteSize = 1;
    int IntSize => Is16Bits ? 2 : 4;
    bool Is16Bits => GeneratorSettings.Is16Bits;

    #endregion

    public CodeGeneratorForAsm(CompilerResult compilerResult, AsmGeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, analysisCollection, print)
    {
        GeneratorSettings = settings;
        Builder = new AssemblyCode();
        FunctionLabels = new List<(CompiledFunction Function, string Label)>();
        GeneratedFunctions = new List<CompiledFunction>();
        FunctionFrameSize = new Stack<int>();
    }

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
        int offset = (int)index * arrayType.Of.Size;
        return prevOffset + offset;
    }

    static ValueAddress GetGlobalVariableAddress(CompiledVariable variable)
        => new ValueAddress(variable.MemoryAddress, AddressingMode.Pointer) + 3;
    public ValueAddress GetReturnValueAddress(GeneralType returnType)
        => new(-(ParametersSize + 0 + returnType.Size) + BytecodeProcessor.StackPointerOffset, AddressingMode.PointerBP);
    ValueAddress GetParameterAddress(CompiledParameter parameter)
    {
        int address = -(ParametersSizeBefore(parameter.Index) + 0) + BytecodeProcessor.StackPointerOffset;
        return new ValueAddress(parameter, address);
    }
    ValueAddress GetParameterAddress(CompiledParameter parameter, int offset)
    {
        int address = -(ParametersSizeBefore(parameter.Index) - offset + 0) + BytecodeProcessor.StackPointerOffset;
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

    #endregion

    #region Memory Helpers

    protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (base.GetLocalSymbolType(symbolName, out type)) return true;

        if (TryGetRegister(symbolName, out _, out BuiltinType? registerType))
        {
            type = registerType;
            return true;
        }

        return false;
    }

    public static bool TryGetRegister(string identifier, [NotNullWhen(true)] out Intel.Register register, [NotNullWhen(true)] out BuiltinType? type)
    {
        register = default;
        type = null;

        if (!identifier.StartsWith('@')) return false;
        identifier = identifier[1..];

        if (identifier is
            "AX" or "BX" or "CX" or "DX" or
            "DS")
        {
            register = Enum.Parse<Intel.Register>(identifier);
            type = new BuiltinType(BasicType.Char);
            return true;
        }

        if (identifier is
            "AH" or "BH" or "CH" or "DH" or
            "AL" or "BL" or "CL" or "DL")
        {
            register = Enum.Parse<Intel.Register>(identifier);
            type = new BuiltinType(BasicType.Byte);
            return true;
        }

        return false;
    }

    bool TryGetFunctionLabel(CompiledFunction function, [NotNullWhen(true)] out string? label)
    {
        for (int i = 0; i < FunctionLabels.Count; i++)
        {
            if (ReferenceEquals(FunctionLabels[i].Function, function))
            {
                label = FunctionLabels[i].Label;
                return true;
            }
        }
        label = null;
        return false;
    }

    void StackStore(ValueAddress address, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        { StackStore(address + i); }
    }

    void StackLoad(ValueAddress address, int size)
    {
        for (int currentOffset = 0; currentOffset < size; currentOffset++)
        { StackLoad(address + currentOffset); }
    }

    void StackLoad(ValueAddress address)
    {
        if (address.IsReference)
        {
            switch (address.AddressingMode)
            {
                case AddressingMode.Pointer:
                throw new NotImplementedException();
                case AddressingMode.PointerBP:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, InstructionOperand.Pointer(Intel.Register.BP, (Math.Abs(address.Address) + 1) * IntSize * Math.Sign(address.Address)));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    return;
                case AddressingMode.PointerSP:
                    throw new NotImplementedException();
                default: throw new UnreachableException();
            }
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.PointerBP:
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, InstructionOperand.Pointer(Intel.Register.BP, (address.Address + 1) * IntSize * -1));
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                break;

            case AddressingMode.PointerSP:
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, InstructionOperand.Pointer(Intel.Register.SP, (address.Address + 1) * IntSize));
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                throw new NotImplementedException();

            case AddressingMode.Pointer:
                throw new NotImplementedException();
            default: throw new UnreachableException();
        }
    }
    void StackStore(ValueAddress address)
    {
        if (address.IsReference)
        {
            switch (address.AddressingMode)
            {
                case AddressingMode.Pointer:
                    throw new NotImplementedException();
                case AddressingMode.PointerBP:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, InstructionOperand.Pointer(Intel.Register.BP, (Math.Abs(address.Address) + 1) * IntSize * Math.Sign(address.Address)), Intel.Register.AX);
                    return;
                case AddressingMode.PointerSP:
                    throw new NotImplementedException();
                default: throw new UnreachableException();
            }
        }

        switch (address.AddressingMode)
        {
            case AddressingMode.PointerBP:
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, InstructionOperand.Pointer(Intel.Register.BP, (address.Address + 1) * IntSize * -1), Intel.Register.AX);
                break;
            case AddressingMode.PointerSP:
            case AddressingMode.Pointer:
                throw new NotImplementedException();
            default: throw new UnreachableException();
        }
    }

    #endregion

    #region Addressing Helpers

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

    public int ReturnValueOffset => -(ParametersSize + 3);

    #endregion

    #region GenerateInitialValue
    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
    int GenerateInitialValue(GeneralType type)
    {
        if (type is StructType structType)
        {
            int size = 0;
            foreach (CompiledField field in structType.Struct.Fields)
            {
                size += GenerateInitialValue(field.Type);
            }
            return size;
        }

        if (type is ArrayType arrayType)
        {
            int size = 0;
            for (int i = 0; i < arrayType.Length; i++)
            {
                size += GenerateInitialValue(arrayType.Of);
            }
            return size;
        }

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, (InstructionOperand)GetInitialValue(type));
        return 1;
    }

    #endregion

    #region GenerateCodeForVariable
    int VariablesSize
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < CompiledVariables.Count; i++)
            { sum += CompiledVariables[i].Type.Size; }
            return sum;
        }
    }

    CleanupItem GenerateCodeForVariable(VariableDeclaration newVariable)
    {
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const)) return CleanupItem.Null;

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        for (int i = 0; i < CompiledVariables.Count; i++)
        {
            if (CompiledVariables[i].Identifier.Content == newVariable.Identifier.Content)
            {
                AnalysisCollection?.Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].Identifier}\" already defined", CompiledVariables[i].Identifier, CurrentFile));
                return CleanupItem.Null;
            }
        }

        int offset = VariablesSize;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        CompiledVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        Builder.CodeBuilder.AppendCommentLine($"{compiledVariable.Type} {compiledVariable.Identifier.Content}");

        int size;

        if (TryCompute(newVariable.InitialValue, out CompiledValue computedInitialValue))
        {
            size = 1;

            Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, (InstructionOperand)computedInitialValue);
            compiledVariable.IsInitialized = true;

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }
        }
        else
        {
            size = GenerateInitialValue(compiledVariable.Type);

            if (size <= 0)
            { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }
        }

        if (size != compiledVariable.Type.Size)
        { throw new InternalException($"Variable size ({compiledVariable.Type.Size}) and initial value size ({size}) mismatch"); }

        if (FunctionFrameSize.Count > 0)
        { FunctionFrameSize[^1] += size; }

        return new CleanupItem(size, newVariable.Modifiers.Contains(ModifierKeywords.Temp), compiledVariable.Type);
    }
    CleanupItem GenerateCodeForVariable(Statement statement)
    {
        if (statement is VariableDeclaration newVariable)
        { return GenerateCodeForVariable(newVariable); }
        return CleanupItem.Null;
    }
    IEnumerable<CleanupItem> GenerateCodeForVariable(IEnumerable<Statement> statements)
    {
        foreach (Statement statement in statements)
        {
            CleanupItem item = GenerateCodeForVariable(statement);
            if (item.SizeOnStack == 0) continue;

            yield return item;
        }
    }

    #endregion

    #region GenerateCodeForSetter

    void GenerateCodeForSetter(Statement statement, StatementWithValue value)
    {
        switch (statement)
        {
            case Identifier v: GenerateCodeForSetter(v, value); break;
            case Pointer v: GenerateCodeForSetter(v, value); break;
            case IndexCall v: GenerateCodeForSetter(v, value); break;
            case Field v: GenerateCodeForSetter(v, value); break;
            default: throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile);
        }
    }

    void GenerateCodeForSetter(Identifier statement, StatementWithValue value)
    {
        if (GetConstant(statement.Content, out _))
        { throw new CompilerException($"Can not set constant value: it is readonly", statement, CurrentFile); }

        if (GetParameter(statement.Content, out CompiledParameter? parameter))
        {
            GeneralType valueType = FindStatementType(value, parameter.Type);

            if (parameter.Type != valueType)
            { throw new CompilerException($"Can not set a \"{valueType}\" type value to the \"{parameter.Type}\" type parameter.", value, CurrentFile); }

            GenerateCodeForStatement(value);

            ValueAddress offset = GetParameterAddress(parameter);
            StackStore(offset);
            return;
        }

        if (GetVariable(statement.Token.Content, out CompiledVariable? variable))
        {
            statement.Token.AnalyzedType = TokenAnalyzedType.VariableName;

            GenerateCodeForStatement(value);

            StackStore(new ValueAddress(variable), variable.Type.Size);
            return;
        }

        if (TryGetRegister(statement.Token.Content, out Intel.Register dstRegister, out BuiltinType? dstRegisterType))
        {
            if (TryCompute(value, out CompiledValue _value))
            {
                if (dstRegisterType == BasicType.Byte &&
                    !CompiledValue.TryShrinkTo8bit(ref _value))
                { throw new CompilerException($"Can't set constant value {_value} to an 8bit register", value, CurrentFile); }

                if (dstRegisterType == BasicType.Char &&
                    !CompiledValue.TryShrinkTo16bit(ref _value))
                { throw new CompilerException($"Can't set constant value {_value} to an 16bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, dstRegister, (InstructionOperand)_value);
            }
            else if (value is Identifier _identifier2 &&
                     TryGetRegister(_identifier2.Content, out Intel.Register srcRegister, out BuiltinType? srcRegisterType))
            {
                if (dstRegisterType == BasicType.Byte &&
                    srcRegisterType == BasicType.Char)
                { throw new CompilerException($"Can't transfer 16bit data from register {srcRegister} to an 8bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, dstRegister, srcRegister);
            }
            else
            {
                GenerateCodeForStatement(value);

                if (dstRegisterType == BasicType.Byte)
                { throw new CompilerException($"Can't pop to an 8bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, dstRegister);
            }
            return;
        }

        throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
    }

    void GenerateCodeForSetter(Field field, StatementWithValue value) => throw new NotImplementedException();

    void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
    {
        if (statement.PrevStatement is Identifier _identifier &&
            _identifier.Content.StartsWith('@'))
        {
            string destinationRegisterName = _identifier.Content[1..];
            if (Enum.TryParse<Intel.Register>(destinationRegisterName, out Intel.Register destinationRegister))
            {
                if (TryCompute(value, out CompiledValue _value))
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, InstructionOperand.Pointer(destinationRegister, 0), (InstructionOperand)_value);
                    return;
                }

                if (value is Identifier _identifier2 &&
                    _identifier2.Content.StartsWith('@'))
                {
                    string sourceRegisterName = _identifier2.Content[1..];
                    if (Enum.TryParse(sourceRegisterName, out Intel.Register sourceRegister))
                    {
                        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, InstructionOperand.Pointer(destinationRegister, 0), sourceRegister);
                        return;
                    }
                }

                throw new NotImplementedException();
            }
        }

        throw new NotImplementedException();
    }

    void GenerateCodeForSetter(IndexCall statement, StatementWithValue value) => throw new NotImplementedException();

    #endregion

    #region GenerateCodeForStatement()

    void Call(string label)
    {
        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Call, InstructionOperand.Label(label));
    }

    void Return()
    {
        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Return);
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        if (functionCall.PrevStatement != null)
        {
            StatementWithValue passedParameter = functionCall.PrevStatement;
            GeneralType passedParameterType = FindStatementType(passedParameter);
            GenerateCodeForStatement(functionCall.PrevStatement);
            parameterCleanup.Push((passedParameterType.Size, false, passedParameterType));
        }

        for (int i = 0; i < functionCall.Arguments.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Arguments[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsExtension ? (i + 1) : i];
            // GeneralType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

            bool canDeallocate = definedParameter.Modifiers.Contains(ModifierKeywords.Temp);

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                canDeallocate = false;
            }

            GenerateCodeForStatement(passedParameter); // TODO: expectedType = definedParameterType

            parameterCleanup.Push((passedParameterType.Size, canDeallocate, passedParameterType));
        }

        return parameterCleanup;
    }

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, FunctionType compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        if (functionCall.PrevStatement != null)
        {
            StatementWithValue passedParameter = functionCall.PrevStatement;
            GeneralType passedParameterType = FindStatementType(passedParameter);
            GenerateCodeForStatement(functionCall.PrevStatement);
            parameterCleanup.Push((passedParameterType.Size, false, passedParameterType));
        }

        for (int i = 0; i < functionCall.Arguments.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Arguments[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"temp\" modifier", passedParameter, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
            }

            GenerateCodeForStatement(passedParameter); // TODO: expectedType = definedParameterType

            parameterCleanup.Push((passedParameterType.Size, false, passedParameterType));
        }

        return parameterCleanup;
    }

    void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
    {
        while (parameterCleanup.Count > 0)
        {
            ParameterCleanupItem passedParameter = parameterCleanup.Pop();

            if (passedParameter.CanDeallocate && passedParameter.Size == 1)
            { throw new NotImplementedException($"HEAP stuff generator isn't implemented for assembly"); }

            for (int i = 0; i < passedParameter.Size; i++)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
            }
        }
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new LanguageError($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodArguments.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodArguments.Length}", functionCall, CurrentFile); }

        if (functionCall.IsMethodCall != compiledFunction.IsExtension)
        { throw new CompilerException($"You called the {(compiledFunction.IsExtension ? "method" : "function")} \"{functionCall.Identifier}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

        if (compiledFunction.Attributes.HasAttribute(AttributeConstants.ExternalIdentifier, ExternalFunctionNames.StdOut))
        {
            StatementWithValue valueToPrint = functionCall.Arguments[0];
            GeneralType valueToPrintType = FindStatementType(valueToPrint);

            if (valueToPrintType == BasicType.Char &&
                valueToPrint is LiteralStatement charLiteral)
            {
                if (Is16Bits)
                { throw new NotSupportedException("Not", functionCall, CurrentFile); }

                string dataLabel = Builder.DataBuilder.NewString(charLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.LoadEA, Intel.Register.BX, (InstructionOperand)new ValueAddress(0, AddressingMode.PointerBP));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Intel.Register.AX,
                    InstructionOperand.Label(dataLabel),
                    charLiteral.Value.Length,
                    Intel.Register.BX,
                    0);

                return;
            }

            if (valueToPrintType == BasicType.Char)
            {
                if (Is16Bits)
                { throw new NotSupportedException("Not", functionCall, CurrentFile); }

                GenerateCodeForStatement(valueToPrint);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.LoadEA, Intel.Register.BX, (InstructionOperand)new ValueAddress(-1, AddressingMode.PointerSP));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Intel.Register.AX,
                    (InstructionOperand)new ValueAddress(-2, AddressingMode.PointerSP),
                    1,
                    Intel.Register.BX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop);

                return;
            }

            if (valueToPrint is LiteralStatement stringLiteral &&
                stringLiteral.Type == LiteralType.String &&
                !Is16Bits)
            {
                string dataLabel = Builder.DataBuilder.NewString(stringLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.LoadEA, Intel.Register.BX, (InstructionOperand)new ValueAddress(-1, AddressingMode.PointerSP));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Intel.Register.AX,
                    InstructionOperand.Label(dataLabel),
                    stringLiteral.Value.Length,
                    Intel.Register.BX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop);

                return;
            }
        }

        Stack<ParameterCleanupItem> parameterCleanup;

        int returnValueSize = 0;
        if (compiledFunction.ReturnSomething)
        {
            returnValueSize = GenerateInitialValue(compiledFunction.Type);
        }

        if (compiledFunction.IsExternal)
        {
            throw new NotImplementedException();
        }

        parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);

        if (!TryGetFunctionLabel(compiledFunction, out string? label))
        {
            StringBuilder functionLabel = new();
            functionLabel.Append(compiledFunction.Identifier);
            for (int i = 0; i < compiledFunction.ParameterTypes.Length; i++)
            {
                functionLabel.Append('_');
                functionLabel.Append(compiledFunction.ParameterTypes[i].ToString());
            }
            label = Builder.CodeBuilder.NewLabel($"f_{functionLabel}");
            FunctionLabels.Add((compiledFunction, label));
        }

        Call(label);

        GenerateCodeForParameterCleanup(parameterCleanup);

        if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
        {
            for (int i = 0; i < returnValueSize; i++)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
            }
        }
    }

    void GenerateCodeForStatement(Statement statement)
    {
        switch (statement)
        {
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case FunctionCall v: GenerateCodeForStatement(v); break;
            case IfContainer v: GenerateCodeForStatement(v); break;
            case WhileLoop v: GenerateCodeForStatement(v); break;
            case LiteralStatement v: GenerateCodeForStatement(v); break;
            case Identifier v: GenerateCodeForStatement(v); break;
            case BinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case UnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case AddressGetter v: GenerateCodeForStatement(v); break;
            case Pointer v: GenerateCodeForStatement(v); break;
            case Assignment v: GenerateCodeForStatement(v); break;
            case ShortOperatorCall v: GenerateCodeForStatement(v); break;
            case CompoundAssignment v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case TypeCast v: GenerateCodeForStatement(v); break;
            case NewInstance v: GenerateCodeForStatement(v); break;
            case ConstructorCall v: GenerateCodeForStatement(v); break;
            case Field v: GenerateCodeForStatement(v); break;
            case IndexCall v: GenerateCodeForStatement(v); break;
            case AnyCall v: GenerateCodeForStatement(v); break;
            case Block v: GenerateCodeForStatement(v); break;
            case ModifiedStatement v: GenerateCodeForStatement(v); break;
            default: throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile);
        }
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            ValueAddress address = GetDataAddress(statement);

            if (address.IsReference)
            {
                StackLoad(address);
            }
            else
            {
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, InstructionOperand.Pointer(Intel.Register.BP, address.Address));
            }
            return;
        }

        if (modifier.Equals(ModifierKeywords.Temp))
        {
            GenerateCodeForStatement(statement);
            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(AnyCall anyCall)
    {
        if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            GenerateCodeForStatement(functionCall);
            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(IndexCall indexCall)
    {
        GenerateCodeForStatement(new FunctionCall(
            indexCall.PrevStatement,
            Token.CreateAnonymous(BuiltinFunctionIdentifiers.IndexerGet),
            new StatementWithValue[]
            {
                indexCall.Index,
            },
            indexCall.Brackets,
            indexCall.File));
    }
    void GenerateCodeForStatement(IfContainer @if)
    {
        string endLabel = Builder.CodeBuilder.NewLabel("if_end");
        for (int i = 0; i < @if.Branches.Length; i++)
        {
            BaseBranch part = @if.Branches[i];

            if (part is IfBranch ifBranch)
            {
                string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                GenerateCodeForStatement(ifBranch.Condition);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Test, Intel.Register.AX, Intel.Register.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfZero, InstructionOperand.Label(nextLabel));
                GenerateCodeForStatement(ifBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(endLabel));
                Builder.CodeBuilder.AppendLabel(nextLabel);
            }
            else if (part is ElseIfBranch elseIfBranch)
            {
                string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                GenerateCodeForStatement(elseIfBranch.Condition);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Test, Intel.Register.AX, Intel.Register.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfZero, InstructionOperand.Label(nextLabel));
                GenerateCodeForStatement(elseIfBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(endLabel));
                Builder.CodeBuilder.AppendLabel(nextLabel);
            }
            else if (part is ElseBranch elseBranch)
            {
                GenerateCodeForStatement(elseBranch.Block);
            }
            else
            {
                throw new UnreachableException();
            }
        }
        Builder.CodeBuilder.AppendLabel(endLabel);
    }
    void GenerateCodeForStatement(WhileLoop @while)
    {
        Builder.CodeBuilder.AppendCommentLine($"while ({@while.Condition}) {{");
        Builder.CodeBuilder.Indent += SectionBuilder.IndentIncrement;

        string startLabel = Builder.CodeBuilder.NewLabel("while_start");
        string endLabel = Builder.CodeBuilder.NewLabel("while_end");

        Builder.CodeBuilder.AppendLabel(startLabel);

        Builder.CodeBuilder.AppendCommentLine($"Condition {{");
        using (Builder.CodeBuilder.Block())
        { GenerateCodeForStatement(@while.Condition); }
        Builder.CodeBuilder.AppendCommentLine($"}}");

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Test, Intel.Register.AX, Intel.Register.AX);
        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfZero, InstructionOperand.Label(endLabel));

        Builder.CodeBuilder.AppendCommentLine($"{{");
        using (Builder.CodeBuilder.Block())
        { GenerateCodeForStatement(@while.Block); }
        Builder.CodeBuilder.AppendCommentLine($"}}");

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(startLabel));

        Builder.CodeBuilder.AppendLabel(endLabel);

        Builder.CodeBuilder.Indent -= SectionBuilder.IndentIncrement;
        Builder.CodeBuilder.AppendCommentLine($"}}");
    }
    void GenerateCodeForStatement(KeywordCall statement)
    {
        switch (statement.Identifier.Content)
        {
            case StatementKeywords.Return:
            {
                if (statement.Arguments.Length > 1)
                { throw new CompilerException($"Wrong number of arguments passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {statement.Arguments.Length}", statement, CurrentFile); }

                if (statement.Arguments.Length == 1)
                {
                    StatementWithValue returnValue = statement.Arguments[0];
                    GeneralType returnValueType = FindStatementType(returnValue);

                    GenerateCodeForStatement(returnValue);

                    int offset = ReturnValueOffset;
                    StackStore(new ValueAddress(offset, AddressingMode.PointerBP), returnValueType.Size);
                }

                if (InFunction)
                { Builder.CodeBuilder.AppendInstruction(ASM.OpCode.MathAdd, Intel.Register.SP, FunctionFrameSize.Last * IntSize); }

                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BP);
                Return();
                break;
            }

            case StatementKeywords.Break:
            {
                if (statement.Arguments.Length != 0)
                { throw new CompilerException($"Wrong number of arguments passed to \"{StatementKeywords.Break}\": required {0}, passed {statement.Arguments.Length}", statement, CurrentFile); }

                throw new NotImplementedException();
            }

            case StatementKeywords.Delete:
            {
                throw new NotImplementedException($"HEAP stuff generator isn't implemented for assembly");
            }

            default: throw new CompilerException($"Unknown instruction command \"{statement.Identifier}\"", statement.Identifier, CurrentFile);
        }
    }
    void GenerateCodeForStatement(Assignment statement)
    {
        if (statement.Operator.Content != "=")
        { throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile); }

        GenerateCodeForSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is required for \'{statement.Operator}\' assignment", statement, CurrentFile));
    }
    void GenerateCodeForStatement(CompoundAssignment statement)
    {
        GenerateCodeForStatement(statement.ToAssignment());
        /*
        switch (statement.Operator.Content)
        {
            case "+=":
                {
                    throw new NotImplementedException();
                }
            case "-=":
                {
                    throw new NotImplementedException();
                }
            default:
                GenerateCodeForStatement(statement.ToAssignment());
                break;
        }
        */
    }
    void GenerateCodeForStatement(ShortOperatorCall statement)
    {
        GenerateCodeForStatement(statement.ToAssignment());
        /*
        switch (statement.Operator.Content)
        {
            case "++":
                {
                    throw new NotImplementedException();
                }
            case "--":
                {
                    throw new NotImplementedException();
                }
            default:
                GenerateCodeForStatement(statement.ToAssignment());
                break;
        }
        */
    }
    void GenerateCodeForStatement(VariableDeclaration statement)
    {
        if (statement.InitialValue == null) return;

        if (statement.Modifiers.Contains(ModifierKeywords.Const))
        { return; }

        if (!GetVariable(statement.Identifier.Content, out CompiledVariable? variable))
        { throw new InternalException($"Variable \"{statement.Identifier.Content}\" not found", statement.Identifier, CurrentFile); }

        if (variable.IsInitialized) return;

        GenerateCodeForSetter(new Identifier(statement.Identifier, statement.File), statement.InitialValue);

        variable.IsInitialized = true;
    }
    void GenerateCodeForStatement(FunctionCall functionCall)
    {
        if (functionCall.Identifier.Content == "sizeof")
        {
            throw new NotImplementedException();
        }

        if (GetVariable(functionCall.Identifier.Content, out CompiledVariable? compiledVariable))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

            if (compiledVariable.Type is not FunctionType function)
            { throw new CompilerException($"Variable \"{compiledVariable.Identifier.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (function.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(function.ReturnType);
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, function);

            Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Call, (InstructionOperand)new ValueAddress(compiledVariable));

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (function.ReturnSomething && !functionCall.SaveValue)
            {
                for (int i = 0; i < returnValueSize; i++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                }
            }
            return;
        }

        if (GetParameter(functionCall.Identifier.Content, out _))
        {
            throw new NotImplementedException();
        }

        if (!GetFunction(functionCall, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFound))
        { throw notFound.Instantiate(functionCall.Identifier, CurrentFile); }

        GenerateCodeForFunctionCall_Function(functionCall, result.Function);
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(LiteralStatement statement)
    {
        switch (statement.Type)
        {
            case LiteralType.Integer:
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, int.Parse(statement.Value));
                break;
            case LiteralType.Float:
                throw new NotImplementedException();
            case LiteralType.String:
                throw new NotImplementedException();
            case LiteralType.Char:
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, statement.Value[0]);
                break;
            default:
                throw new UnreachableException();
        }
    }
    void GenerateCodeForStatement(Identifier statement, GeneralType? expectedType = null)
    {
        if (GetConstant(statement.Content, out IConstant? constant))
        {
            statement.Token.AnalyzedType = TokenAnalyzedType.ConstantName;
            statement.Reference = constant;
            Builder.CodeBuilder.AppendInstruction(OpCode.Push, (InstructionOperand)constant.Value);
            return;
        }

        if (GetParameter(statement.Content, out CompiledParameter? compiledParameter))
        {
            if (statement.Content != StatementKeywords.This)
            { statement.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
            ValueAddress address = GetParameterAddress(compiledParameter);
            StackLoad(address, compiledParameter.Type.Size);
            return;
        }

        if (GetVariable(statement.Content, out CompiledVariable? val))
        {
            statement.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            StackLoad(new ValueAddress(val), val.Type.Size);
            return;
        }

        if (GetFunction(statement.Token.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out _))
        {
            CompiledFunction compiledFunction = result.Function;

            if (!TryGetFunctionLabel(compiledFunction, out string? label))
            {
                StringBuilder functionLabel = new();
                functionLabel.Append(compiledFunction.Identifier);
                for (int i = 0; i < compiledFunction.ParameterTypes.Length; i++)
                {
                    functionLabel.Append('_');
                    functionLabel.Append(compiledFunction.ParameterTypes[i].ToString());
                }
                label = Builder.CodeBuilder.NewLabel($"f_{functionLabel}");
                FunctionLabels.Add((compiledFunction, label));
            }

            Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, InstructionOperand.Label(label));
            return;
        }

        if (statement.Token.Content.StartsWith('@'))
        {
            string potentialRegister = statement.Token.Content[1..];
            switch (potentialRegister)
            {
                case "AX":
                case "BX":
                case "CX":
                case "DX":
                case "DS":
                // case "AH":
                // case "BH":
                // case "CH":
                // case "DH":
                // case "AL":
                // case "BL":
                // case "CL":
                // case "DL":
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, InstructionOperand.Label(potentialRegister));
                    return;
                }
            }
        }

        throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
    }
    void GenerateCodeForStatement(BinaryOperatorCall statement)
    {
        if (GetOperator(statement, CurrentFile, out _, out _))
        {
            throw new NotImplementedException();
        }
        else if (LanguageOperators.BinaryOperators.TryGetValue(statement.Operator.Content, out string? opcode))
        {
            GenerateCodeForStatement(statement.Left);
            GenerateCodeForStatement(statement.Right);

            throw new NotImplementedException();
            /*
            switch (opcode)
            {
                case Opcode.CompLT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfGEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.CompMT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfLEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.CompLTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfG, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.CompMTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfL, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicOR:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfNotEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.BX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfNotEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicAND:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.BX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);

                    break;
                }
                case Opcode.CompEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfNotEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.CompNEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }

                case Opcode.BitsAND:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.BitsAND, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.BitsOR:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.BitsOR, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.BitsXOR:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.BitsXOR, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;

                case Opcode.BitsShiftLeft:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.BitsShiftLeft, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.BitsShiftRight:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.BitsShiftRight, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;

                case Opcode.MathAdd:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.MathAdd, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.MathSub:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.MathSub, Intel.Register.BX, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.BX);
                    break;
                case Opcode.MathMult:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.MathMult, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.MathDiv:
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.IMathDiv, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                case Opcode.MathMod:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.IMathDiv, Intel.Register.AX, Intel.Register.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, Intel.Register.DX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Test, Intel.Register.AX, Intel.Register.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.AX, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.AX);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
            */
        }
        else
        { throw new CompilerException($"Unknown operator \"{statement.Operator.Content}\"", statement.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(UnaryOperatorCall statement)
    {
        if (GetOperator(statement, CurrentFile, out _, out _))
        {
            throw new NotImplementedException();
        }
        else if (LanguageOperators.UnaryOperators.TryGetValue(statement.Operator.Content, out _))
        {
            GenerateCodeForStatement(statement.Left);

            /*
            switch (opcode)
            {
                case Opcode.LogicNOT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Compare, Intel.Register.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.JumpIfNotEQ, InstructionOperand.Label(label1));
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Jump, InstructionOperand.Label(label2));
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }

                default:
                    throw new NotImplementedException();
            }
            */
        }
        else
        { throw new CompilerException($"Unknown operator \"{statement.Operator.Content}\"", statement.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(Block block)
    {
        CleanupItem[] cleanup = GenerateCodeForVariable(block.Statements).ToArray();
        foreach (Statement statement in block.Statements)
        {
            Builder.CodeBuilder.AppendCommentLine(statement.ToString());
            GenerateCodeForStatement(statement);
        }
        CleanupVariables(cleanup);
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(Pointer pointer)
    {
        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(NewInstance newInstance)
    {
        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(Field field)
    {
        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(TypeCast typeCast)
    {
        AnalysisCollection?.Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

        GenerateCodeForStatement(typeCast.PrevStatement);
    }

    #endregion

    void CleanupVariables(CleanupItem[] cleanup)
    {
        for (int i = 0; i < cleanup.Length; i++)
        {
            CleanupItem item = cleanup[i];
            for (int j = 0; j < item.SizeOnStack; j++)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.OpCode.MathAdd, Intel.Register.SP, IntSize);
            }
        }
    }

    void GenerateCodeForTopLevelStatements(IEnumerable<Statement> statements)
    {
        CompileLocalConstants(statements);

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.BP, Intel.Register.SP);

        Builder.CodeBuilder.AppendCommentLine("Variables:");
        CleanupItem[] cleanup = GenerateCodeForVariable(statements).ToArray();
        bool hasExited = false;

        Builder.CodeBuilder.AppendCommentLine("Code:");

        foreach (Statement statement in statements)
        {
            if (statement is KeywordCall keywordCall &&
                keywordCall.Identifier.Equals(StatementKeywords.Return))
            {
                if (keywordCall.Arguments.Length is not 0 and not 1)
                { throw new CompilerException($"Wrong number of arguments passed to instruction \"return\" (required 0 or 1, passed {keywordCall.Arguments.Length})", keywordCall, CurrentFile); }

                if (keywordCall.Arguments.Length == 1)
                {
                    if (Is16Bits)
                    { throw new NotSupportedException("Not", keywordCall, CurrentFile); }

                    GenerateCodeForStatement(keywordCall.Arguments[0]);
                    hasExited = true;
                    Builder.CodeBuilder.Call_stdcall("_ExitProcess@4");
                    break;
                }
            }

            Builder.CodeBuilder.AppendCommentLine(statement.ToString());

            GenerateCodeForStatement(statement);
        }

        Builder.CodeBuilder.AppendCommentLine("Cleanup");

        CleanupVariables(cleanup);

        if (!hasExited && !Is16Bits)
        {
            Builder.CodeBuilder.Call_stdcall("_ExitProcess@4", 0);
        }

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Halt);

        CleanupLocalConstants();
    }

    void CompileParameters(ParameterDefinition[] parameters)
    {
        int paramsSize = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            GeneralType parameterType = GeneralType.From(parameters[i].Type, FindType);
            parameters[i].Type.SetAnalyzedType(parameterType);

            this.CompiledParameters.Add(new CompiledParameter(i, -(paramsSize + 1 + CodeGeneratorForMain.TagsBeforeBasePointer), parameterType, parameters[i]));

            paramsSize += parameterType.Size;
        }
    }

    void GenerateCodeForFunction(FunctionThingDefinition function)
    {
        if (LanguageConstants.KeywordList.Contains(function.Identifier.Content))
        { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.File); }

        function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

        if (function is CompiledFunction compiledFunction1) GeneratedFunctions.Add(compiledFunction1);

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Equals(AttributeConstants.ExternalIdentifier))
                { return; }
            }
        }

        if (function.Block == null)
        { return; }

        string? label;

        Builder.CodeBuilder.Indent = 0;
        Builder.CodeBuilder.AppendTextLine();

        if (function is CompiledFunction compiledFunction2)
        {
            if (!TryGetFunctionLabel(compiledFunction2, out label))
            {
                label = Builder.CodeBuilder.NewLabel($"f_{compiledFunction2.Identifier}");
                FunctionLabels.Add((compiledFunction2, label));
            }
        }
        else
        {
            throw new NotImplementedException();
        }

        Builder.CodeBuilder.AppendLabel(label);

        Builder.CodeBuilder.Indent += SectionBuilder.IndentIncrement;

        FunctionFrameSize.Push(0);
        InFunction = true;

        CompiledParameters.Clear();

        CompileParameters(function.Parameters.ToArray());

        CurrentFile = function.File;

        Builder.CodeBuilder.AppendCommentLine("Begin frame");

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Push, Intel.Register.BP);
        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Move, Intel.Register.BP, Intel.Register.SP);

        Builder.CodeBuilder.AppendCommentLine("Block");

        GenerateCodeForStatement(function.Block);

        Builder.CodeBuilder.AppendCommentLine("End frame");

        Builder.CodeBuilder.AppendInstruction(ASM.OpCode.Pop, Intel.Register.BP);

        Return();

        CurrentFile = null;
        CompiledParameters.Clear();
        InFunction = false;
        FunctionFrameSize.Pop(0);
        Builder.CodeBuilder.Indent = 0;
    }

    AsmGeneratorResult GenerateCode(CompilerResult compilerResult)
    {
        GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements[^1].Statements);

        while (true)
        {
            bool shouldExit = true;
            (CompiledFunction Function, string Label)[] functionLabels = FunctionLabels.ToArray();

            for (int i = functionLabels.Length - 1; i >= 0; i--)
            {
                CompiledFunction function = functionLabels[i].Function;
                if (!GeneratedFunctions.Any(other => ReferenceEquals(function, other)))
                {
                    shouldExit = false;
                    GenerateCodeForFunction(function);
                }
            }

            if (shouldExit) break;
        }

        return new AsmGeneratorResult()
        {
            AssemblyCode = Builder.Make(Is16Bits),
        };
    }

    public static AsmGeneratorResult Generate(CompilerResult compilerResult, AsmGeneratorSettings generatorSettings, PrintCallback? printCallback = null, AnalysisCollection? analysisCollection = null)
        => new CodeGeneratorForAsm(compilerResult, generatorSettings, analysisCollection, printCallback).GenerateCode(compilerResult);
}
