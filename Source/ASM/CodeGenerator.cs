#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter

namespace LanguageCore.ASM.Generator;

using BBCode.Generator;
using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using LiteralStatement = Parser.Statement.Literal;
using Registers = LanguageCore.ASM.Registers;
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

    public CodeGeneratorForAsm(CompilerResult compilerResult, AsmGeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, LanguageCore.Compiler.GeneratorSettings.Default, analysisCollection, print)
    {
        GeneratorSettings = settings;
        Builder = new AssemblyCode();
        FunctionLabels = new List<(CompiledFunction Function, string Label)>();
        GeneratedFunctions = new List<CompiledFunction>();
        FunctionFrameSize = new Stack<int>();
    }

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

    public static bool TryGetRegister(string identifier, [NotNullWhen(true)] out string? register, [NotNullWhen(true)] out BuiltinType? type)
    {
        register = null;
        type = null;

        if (!identifier.StartsWith('@')) return false;
        identifier = identifier[1..];

        if (identifier is
            "AX" or "BX" or "CX" or "DX" or
            "DS")
        {
            register = identifier;
            type = new BuiltinType(BasicType.Char);
            return true;
        }

        if (identifier is
            "AH" or "BH" or "CH" or "DH" or
            "AL" or "BL" or "CL" or "DL")
        {
            register = identifier;
            type = new BuiltinType(BasicType.Byte);
            return true;
        }

        return false;
    }

    protected override ValueAddress GetGlobalVariableAddress(CompiledVariable variable)
    {
        throw new NotImplementedException();
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

    protected override void StackLoad(ValueAddress address)
    {
        if (address.IsReference)
        {
            switch (address.AddressingMode)
            {
                case AddressingMode.Absolute:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, InstructionOperand.Pointer((address.Address + 1) * IntSize, "DWORD"));
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    return;
                case AddressingMode.Runtime:
                    throw new NotImplementedException();
                case AddressingMode.BasePointerRelative:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, InstructionOperand.Pointer(Registers.BP, (Math.Abs(address.Address) + 1) * IntSize * Math.Sign(address.Address), "DWORD"));
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    return;
                case AddressingMode.StackPointerRelative:
                    throw new NotImplementedException();
                default: throw new UnreachableException();
            }
        }

        if (address.InHeap)
        { throw new NotImplementedException($"HEAP stuff generator isn't implemented for assembly"); }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
            case AddressingMode.BasePointerRelative:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, InstructionOperand.Pointer(Registers.BP, (address.Address + 1) * IntSize * -1, "DWORD"));
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                break;

            case AddressingMode.StackPointerRelative:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, InstructionOperand.Pointer(Registers.SP, (address.Address + 1) * IntSize, "DWORD"));
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                throw new NotImplementedException();

            case AddressingMode.Runtime:
                throw new NotImplementedException();
            default: throw new UnreachableException();
        }
    }
    protected override void StackStore(ValueAddress address)
    {
        if (address.IsReference)
        {
            switch (address.AddressingMode)
            {
                case AddressingMode.Absolute:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, InstructionOperand.Pointer((address.Address + 1) * IntSize, "DWORD"), Registers.AX);
                    return;
                case AddressingMode.Runtime:
                    throw new NotImplementedException();
                case AddressingMode.BasePointerRelative:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, InstructionOperand.Pointer(Registers.BP, (Math.Abs(address.Address) + 1) * IntSize * Math.Sign(address.Address), "DWORD"), Registers.AX);
                    return;
                case AddressingMode.StackPointerRelative:
                    throw new NotImplementedException();
                default: throw new UnreachableException();
            }
        }

        if (address.InHeap)
        { throw new NotImplementedException($"HEAP stuff generator isn't implemented for assembly"); }

        switch (address.AddressingMode)
        {
            case AddressingMode.Absolute:
            case AddressingMode.BasePointerRelative:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, InstructionOperand.Pointer(Registers.BP, (address.Address + 1) * IntSize * -1, "DWORD"), Registers.AX);
                break;
            case AddressingMode.StackPointerRelative:
            case AddressingMode.Runtime:
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

    protected override ValueAddress GetBaseAddress(CompiledParameter parameter)
    {
        int offset = -(2 + ParametersSizeBefore(parameter.Index));
        return new ValueAddress(parameter, offset);
    }
    protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
    {
        int _offset = -(2 + ParametersSizeBefore(parameter.Index) - offset);
        return new ValueAddress(parameter, _offset);
    }

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

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, (InstructionOperand)GetInitialValue(type));
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

        if (TryCompute(newVariable.InitialValue, out DataItem computedInitialValue))
        {
            size = 1;

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, (InstructionOperand)computedInitialValue);
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

            ValueAddress offset = GetBaseAddress(parameter);
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

        if (TryGetRegister(statement.Token.Content, out string? dstRegister, out BuiltinType? dstRegisterType))
        {
            if (TryCompute(value, out DataItem _value))
            {
                if (dstRegisterType == BasicType.Byte &&
                    !DataItem.TryShrinkTo8bit(ref _value))
                { throw new CompilerException($"Can't set constant value {_value} to an 8bit register", value, CurrentFile); }

                if (dstRegisterType == BasicType.Char &&
                    !DataItem.TryShrinkTo16bit(ref _value))
                { throw new CompilerException($"Can't set constant value {_value} to an 16bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, dstRegister, (InstructionOperand)_value);
            }
            else if (value is Identifier _identifier2 &&
                     TryGetRegister(_identifier2.Content, out string? srcRegister, out BuiltinType? srcRegisterType))
            {
                if (dstRegisterType == BasicType.Byte &&
                    srcRegisterType == BasicType.Char)
                { throw new CompilerException($"Can't transfer 16bit data from register {srcRegister} to an 8bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, dstRegister, srcRegister);
            }
            else
            {
                GenerateCodeForStatement(value);

                if (dstRegisterType == BasicType.Byte)
                { throw new CompilerException($"Can't pop to an 8bit register", value, CurrentFile); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, dstRegister);
            }
            return;
        }

        throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
    }

    void GenerateCodeForSetter(Field field, StatementWithValue value)
    {
        throw new NotImplementedException();
    }

    void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
    {
        if (statement.PrevStatement is Identifier _identifier &&
            _identifier.Content.StartsWith('@'))
        {
            string destinationRegister = _identifier.Content[1..];
            if (destinationRegister is
                "AX" or "BX" or "CX" or "DX" or
                "AH" or "BH" or "CH" or "DH" or
                "AL" or "BL" or "CL" or "DL" or
                "DS")
            {
                if (TryCompute(value, out DataItem _value))
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, InstructionOperand.Pointer(destinationRegister, 0), (InstructionOperand)_value);
                    return;
                }

                if (value is Identifier _identifier2 &&
                         _identifier2.Content.StartsWith('@'))
                {
                    string sourceRegister = _identifier2.Content[1..];
                    if (sourceRegister is
                        "AX" or "BX" or "CX" or "DX" or
                        "AH" or "BH" or "CH" or "DH" or
                        "AL" or "BL" or "CL" or "DL" or
                        "DS")
                    {
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, InstructionOperand.Pointer(destinationRegister, 0), sourceRegister);
                        return;
                    }
                }

                throw new NotImplementedException();
            }
        }

        throw new NotImplementedException();
    }

    void GenerateCodeForSetter(IndexCall statement, StatementWithValue value)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region GenerateCodeForStatement()

    void Call(string label)
    {
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Call, label);
    }

    void Return()
    {
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Return);
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

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
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

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
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

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(BinaryOperatorCall functionCall, CompiledOperator compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
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

    void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
    {
        while (parameterCleanup.Count > 0)
        {
            ParameterCleanupItem passedParameter = parameterCleanup.Pop();

            if (passedParameter.CanDeallocate && passedParameter.Size == 1)
            { throw new NotImplementedException($"HEAP stuff generator isn't implemented for assembly"); }

            for (int i = 0; i < passedParameter.Size; i++)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
            }
        }
    }

    void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
    {
        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"The {compiledFunction.ToReadable()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
        { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ToReadable()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

        if (functionCall.IsMethodCall != compiledFunction.IsExtension)
        { throw new CompilerException($"You called the {(compiledFunction.IsExtension ? "method" : "function")} \"{functionCall.Identifier}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

        if (compiledFunction.Attributes.HasAttribute(AttributeConstants.ExternalIdentifier, ExternalFunctionNames.StdOut))
        {
            StatementWithValue valueToPrint = functionCall.Parameters[0];
            GeneralType valueToPrintType = FindStatementType(valueToPrint);

            if (valueToPrintType == BasicType.Char &&
                valueToPrint is LiteralStatement charLiteral)
            {
                if (Is16Bits)
                { throw new NotSupportedException("Not", functionCall, CurrentFile); }

                string dataLabel = Builder.DataBuilder.NewString(charLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LoadEA, Registers.BX, (InstructionOperand)new ValueAddress(0, AddressingMode.BasePointerRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.AX,
                    dataLabel,
                    charLiteral.Value.Length,
                    Registers.BX,
                    0);

                return;
            }

            if (valueToPrintType == BasicType.Char)
            {
                if (Is16Bits)
                { throw new NotSupportedException("Not", functionCall, CurrentFile); }

                GenerateCodeForStatement(valueToPrint);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LoadEA, Registers.BX, (InstructionOperand)new ValueAddress(-1, AddressingMode.StackPointerRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.AX,
                    (InstructionOperand)new ValueAddress(-2, AddressingMode.StackPointerRelative),
                    1,
                    Registers.BX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop);

                return;
            }

            if (valueToPrint is LiteralStatement stringLiteral &&
                stringLiteral.Type == LiteralType.String &&
                !Is16Bits)
            {
                string dataLabel = Builder.DataBuilder.NewString(stringLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LoadEA, Registers.BX, (InstructionOperand)new ValueAddress(-1, AddressingMode.StackPointerRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.AX,
                    dataLabel,
                    stringLiteral.Value.Length,
                    Registers.BX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop);

                return;
            }
        }

        if (compiledFunction.IsMacro)
        { AnalysisCollection?.Warnings.Add(new Warning($"I can not inline macros because of lack of intelligence so I will treat this macro as a normal function.", functionCall, CurrentFile)); }

        Stack<ParameterCleanupItem> parameterCleanup;

        int returnValueSize = 0;
        if (compiledFunction.ReturnSomething)
        {
            returnValueSize = GenerateInitialValue(compiledFunction.Type);
        }

        if (compiledFunction.IsExternal)
        {
            throw new NotImplementedException();
            // switch (compiledFunction.ExternalFunctionName)
            // {
            //     case "stdout":
            //     default:
            //         throw new NotImplementedException();
            // }
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
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

        if (statement is FunctionCall statementWithValue &&
            !statementWithValue.SaveValue &&
            GetFunction(statementWithValue, out CompiledFunction? _f, out _) &&
            _f.ReturnSomething)
        {
            throw new NotImplementedException();
        }
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            ValueAddress address = GetDataAddress(statement);

            if (address.InHeap)
            { throw new CompilerException($"This value is stored in the heap and not in the stack", statement, CurrentFile); }

            if (address.IsReference)
            {
                StackLoad(address);
            }
            else
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, $"[rbp+{address.Address}]".Replace("+-", "-", StringComparison.Ordinal));
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
            Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
            new StatementWithValue[]
            {
                indexCall.Index,
            },
            indexCall.Brackets));
    }
    void GenerateCodeForStatement(IfContainer @if)
    {
        string endLabel = Builder.CodeBuilder.NewLabel("if_end");
        for (int i = 0; i < @if.Parts.Length; i++)
        {
            BaseBranch part = @if.Parts[i];

            if (part is IfBranch ifBranch)
            {
                string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                GenerateCodeForStatement(ifBranch.Condition);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Test, Registers.AX, Registers.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfZero, nextLabel);
                GenerateCodeForStatement(ifBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, endLabel);
                Builder.CodeBuilder.AppendLabel(nextLabel);
            }
            else if (part is ElseIfBranch elseIfBranch)
            {
                string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                GenerateCodeForStatement(elseIfBranch.Condition);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Test, Registers.AX, Registers.AX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfZero, nextLabel);
                GenerateCodeForStatement(elseIfBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, endLabel);
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

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Test, Registers.AX, Registers.AX);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfZero, endLabel);

        Builder.CodeBuilder.AppendCommentLine($"{{");
        using (Builder.CodeBuilder.Block())
        { GenerateCodeForStatement(@while.Block); }
        Builder.CodeBuilder.AppendCommentLine($"}}");

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, startLabel);

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
                if (statement.Parameters.Length > 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {statement.Parameters.Length}", statement, CurrentFile); }

                if (statement.Parameters.Length == 1)
                {
                    StatementWithValue returnValue = statement.Parameters[0];
                    GeneralType returnValueType = FindStatementType(returnValue);

                    GenerateCodeForStatement(returnValue);

                    int offset = ReturnValueOffset;
                    StackStore(new ValueAddress(offset, AddressingMode.BasePointerRelative), returnValueType.Size);
                }

                if (InFunction)
                { Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MathAdd, Registers.SP, FunctionFrameSize.Last * IntSize); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BP);
                Return();
                break;
            }

            case StatementKeywords.Break:
            {
                if (statement.Parameters.Length != 0)
                { throw new CompilerException($"Wrong number of parameters passed to \"{StatementKeywords.Break}\": required {0}, passed {statement.Parameters.Length}", statement, CurrentFile); }

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
        { throw new InternalException($"Variable \"{statement.Identifier.Content}\" not found", CurrentFile); }

        if (variable.IsInitialized) return;

        GenerateCodeForSetter(new Identifier(statement.Identifier), statement.InitialValue);

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

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Call, (InstructionOperand)new ValueAddress(compiledVariable));

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (function.ReturnSomething && !functionCall.SaveValue)
            {
                for (int i = 0; i < returnValueSize; i++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                }
            }
            return;
        }

        if (GetParameter(functionCall.Identifier.Content, out CompiledParameter? compiledParameter))
        {
            throw new NotImplementedException();
        }

        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

            Uri? prevFile = CurrentFile;

            CurrentFile = macro.FilePath;

            if (!InlineMacro(macro, out Statement? inlinedMacro, functionCall.Parameters))
            { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

            GenerateCodeForInlinedMacro(inlinedMacro);

            CurrentFile = prevFile;

            return;
        }

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction, out WillBeCompilerException? notFound))
        {
            if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            { throw notFound.Instantiate(functionCall.Identifier, CurrentFile); }

            compiledFunction = compilableFunction.Function;
        }

        GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, statement.Value);
                break;
            case LiteralType.Float:
                throw new NotImplementedException();
            case LiteralType.String:
                throw new NotImplementedException();
            case LiteralType.Char:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, statement.Value[0]);
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
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, (InstructionOperand)constant.Value);
            return;
        }

        if (GetParameter(statement.Content, out CompiledParameter? compiledParameter))
        {
            if (statement.Content != StatementKeywords.This)
            { statement.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
            ValueAddress address = GetBaseAddress(compiledParameter);
            StackLoad(address, compiledParameter.Type.Size);
            return;
        }

        if (GetVariable(statement.Content, out CompiledVariable? val))
        {
            statement.Token.AnalyzedType = TokenAnalyzedType.VariableName;
            StackLoad(new ValueAddress(val), val.Type.Size);
            return;
        }

        if (GetFunction(statement.Token.Content, expectedType, out CompiledFunction? compiledFunction, out _))
        {
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

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, label);
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
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, potentialRegister);
                    return;
                }
            }
        }

        throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
    }
    void GenerateCodeForStatement(BinaryOperatorCall statement)
    {
        if (GetOperator(statement, out _))
        {
            throw new NotImplementedException();
        }
        else if (LanguageOperators.OpCodes.TryGetValue(statement.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[statement.Operator.Content] != BinaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator \"{statement.Operator.Content}\": required {LanguageOperators.ParameterCounts[statement.Operator.Content]} passed {BinaryOperatorCall.ParameterCount}", statement.Operator, CurrentFile); }

            GenerateCodeForStatement(statement.Left);
            GenerateCodeForStatement(statement.Right);

            switch (opcode)
            {
                case Opcode.LogicLT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfGEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicMT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfLEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicLTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfG, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicMTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfL, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicOR:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfNotEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.BX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfNotEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicAND:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.BX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);

                    break;
                }
                case Opcode.LogicEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfNotEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LogicNEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }

                case Opcode.BitsAND:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.BitsAND, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.BitsOR:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.BitsOR, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.BitsXOR:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.BitsXOR, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;

                case Opcode.BitsShiftLeft:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.BitsShiftLeft, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.BitsShiftRight:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.BitsShiftRight, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;

                case Opcode.MathAdd:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MathAdd, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.MathSub:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MathSub, Registers.BX, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.BX);
                    break;
                case Opcode.MathMult:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MathMult, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.MathDiv:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.IMathDiv, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                case Opcode.MathMod:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.IMathDiv, Registers.AX, Registers.BX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, Registers.DX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Test, Registers.AX, Registers.AX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.AX, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.AX);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }
        else
        { throw new CompilerException($"Unknown operator \"{statement.Operator.Content}\"", statement.Operator, CurrentFile); }
    }
    void GenerateCodeForStatement(UnaryOperatorCall statement)
    {
        if (GetOperator(statement, out _))
        {
            throw new NotImplementedException();
        }
        else if (LanguageOperators.OpCodes.TryGetValue(statement.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[statement.Operator.Content] != UnaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator \"{statement.Operator.Content}\": required {LanguageOperators.ParameterCounts[statement.Operator.Content]} passed {UnaryOperatorCall.ParameterCount}", statement.Operator, CurrentFile); }

            GenerateCodeForStatement(statement.Left);

            switch (opcode)
            {
                case Opcode.LogicNOT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.AX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Compare, Registers.AX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JumpIfNotEQ, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Jump, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }

                default:
                    throw new NotImplementedException();
            }
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

    void GenerateCodeForInlinedMacro(Statement inlinedMacro)
    {
        Builder.CodeBuilder.AppendCommentLine("Macro {");
        Builder.CodeBuilder.Indent += SectionBuilder.IndentIncrement;

        InMacro.Push(true);
        if (inlinedMacro is Block block)
        {
            GenerateCodeForStatement(block);
        }
        else if (inlinedMacro is KeywordCall keywordCall &&
            keywordCall.Identifier.Equals(StatementKeywords.Return) &&
            keywordCall.Parameters.Length == 1)
        {
            Builder.CodeBuilder.AppendCommentLine($"{keywordCall.Parameters[0]}  (returned)");
            GenerateCodeForStatement(keywordCall.Parameters[0]);
        }
        else
        {
            Builder.CodeBuilder.AppendCommentLine(inlinedMacro.ToString());
            GenerateCodeForStatement(inlinedMacro);
        }
        InMacro.Pop();

        Builder.CodeBuilder.Indent -= SectionBuilder.IndentIncrement;
        Builder.CodeBuilder.AppendCommentLine("}");
    }

    void CleanupVariables(CleanupItem[] cleanup)
    {
        for (int i = 0; i < cleanup.Length; i++)
        {
            CleanupItem item = cleanup[i];
            for (int j = 0; j < item.SizeOnStack; j++)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MathAdd, Registers.SP, IntSize);
            }
        }
    }

    void GenerateCodeForTopLevelStatements(Statement[] statements)
    {
        CompileConstants(statements);

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.BP, Registers.SP);

        Builder.CodeBuilder.AppendCommentLine("Variables:");
        CleanupItem[] cleanup = GenerateCodeForVariable(statements).ToArray();
        bool hasExited = false;

        Builder.CodeBuilder.AppendCommentLine("Code:");

        for (int i = 0; i < statements.Length; i++)
        {
            if (statements[i] is KeywordCall keywordCall &&
                keywordCall.Identifier.Equals(StatementKeywords.Return))
            {
                if (keywordCall.Parameters.Length != 0 &&
                    keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (required 0 or 1, passed {keywordCall.Parameters.Length})", keywordCall, CurrentFile); }

                if (keywordCall.Parameters.Length == 1)
                {
                    if (Is16Bits)
                    { throw new NotSupportedException("Not", keywordCall, CurrentFile); }

                    GenerateCodeForStatement(keywordCall.Parameters[0]);
                    hasExited = true;
                    Builder.CodeBuilder.Call_stdcall("_ExitProcess@4", 4);
                    break;
                }
            }

            Builder.CodeBuilder.AppendCommentLine(statements[i].ToString());

            GenerateCodeForStatement(statements[i]);
        }

        Builder.CodeBuilder.AppendCommentLine("Cleanup");

        CleanupVariables(cleanup);

        if (!hasExited && !Is16Bits)
        {
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, 0);
            Builder.CodeBuilder.Call_stdcall("_ExitProcess@4", 4);
        }

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Halt);

        CleanupConstants();
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
        { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

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

        CurrentFile = function.FilePath;

        Builder.CodeBuilder.AppendCommentLine("Begin frame");

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Push, Registers.BP);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Move, Registers.BP, Registers.SP);

        Builder.CodeBuilder.AppendCommentLine("Block");

        GenerateCodeForStatement(function.Block);

        Builder.CodeBuilder.AppendCommentLine("End frame");

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.Pop, Registers.BP);

        CurrentFile = null;

        Return();

        CompiledParameters.Clear();

        InFunction = false;
        FunctionFrameSize.Pop(0);
        Builder.CodeBuilder.Indent = 0;
    }

    AsmGeneratorResult GenerateCode(CompilerResult compilerResult)
    {
        GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

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
