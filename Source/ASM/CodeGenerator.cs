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
    readonly Stack<bool> InMacro;

    #endregion

    public CodeGeneratorForAsm(CompilerResult compilerResult, AsmGeneratorSettings settings, AnalysisCollection? analysisCollection) : base(compilerResult, LanguageCore.Compiler.GeneratorSettings.Default, analysisCollection)
    {
        this.GeneratorSettings = settings;
        this.Builder = new AssemblyCode();

        this.FunctionLabels = new List<(CompiledFunction Function, string Label)>();
        this.GeneratedFunctions = new List<CompiledFunction>();
        this.FunctionFrameSize = new Stack<int>();
        this.InMacro = new Stack<bool>();
    }

    #region Memory Helpers

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
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{(address.Address + 1) * 4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    return;
                case AddressingMode.Runtime:
                    throw new NotImplementedException();
                case AddressingMode.BasePointerRelative:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.EBP}{(address.Address > 0 ? "-" : "+")}{(Math.Abs(address.Address) + 1) * 4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    return;
                case AddressingMode.StackRelative:
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.EBP}-{(address.Address + 1) * 4}]");
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                break;

            case AddressingMode.StackRelative:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.ESP}+{(address.Address + 1) * 4}]");
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
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
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, $"DWORD[{(address.Address + 1) * 4}]", Registers.EAX);
                    return;
                case AddressingMode.Runtime:
                    throw new NotImplementedException();
                case AddressingMode.BasePointerRelative:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, $"DWORD[{Registers.EBP}{(address.Address > 0 ? "-" : "+")}{(Math.Abs(address.Address) + 1) * 4}]", Registers.EAX);
                    return;
                case AddressingMode.StackRelative:
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, $"DWORD[{Registers.EBP}-{(address.Address + 1) * 4}]".Replace("--", "+", StringComparison.Ordinal), Registers.EAX);
                break;
            case AddressingMode.StackRelative:
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
    int GenerateInitialValue(TypeInstance type)
    {
        if (type is TypeInstanceFunction)
        {
            throw new NotImplementedException();
        }

        if (type is TypeInstanceStackArray)
        {
            throw new NotImplementedException();
        }

        if (type is TypeInstanceSimple simpleType)
        {
            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(simpleType.Identifier.Content, out BasicType builtinType))
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)GetInitialValue(builtinType));
                return 1;
            }

            GeneralType instanceType = FindType(simpleType);

            if (instanceType is StructType structType)
            {
                int size = 0;
                foreach (FieldDefinition field in structType.Struct.Fields)
                {
                    size++;
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)GetInitialValue(field.Type));
                }
                throw new NotImplementedException();
            }

            if (instanceType is EnumType enumType)
            {
                if (enumType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{enumType.Enum.Identifier.Content}\" initial value: enum has no members", enumType.Enum.Identifier, enumType.Enum.FilePath); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)enumType.Enum.Members[0].ComputedValue);
                return 1;
            }

            if (instanceType is FunctionType)
            {
                throw new NotImplementedException();
            }

            throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", simpleType, CurrentFile);
        }

        throw new UnreachableException();
    }
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

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)GetInitialValue(type));
        return 1;
    }
    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="CompilerException"></exception>
    /// <exception cref="InternalException"></exception>
    int GenerateInitialValue(GeneralType type, Action<int> afterValue)
    {
        if (type is StructType structType)
        {
            int size = 0;
            foreach (CompiledField field in structType.Struct.Fields)
            {
                size++;
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)GetInitialValue(field.Type));
                afterValue?.Invoke(size);
            }
            throw new NotImplementedException();
        }

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)GetInitialValue(type));
        afterValue?.Invoke(0);
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
        if (newVariable.Modifiers.Contains("const")) return CleanupItem.Null;

        newVariable.VariableName.AnalyzedType = TokenAnalyzedType.VariableName;

        for (int i = 0; i < CompiledVariables.Count; i++)
        {
            if (CompiledVariables[i].VariableName.Content == newVariable.VariableName.Content)
            {
                AnalysisCollection?.Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].VariableName}\" already defined", CompiledVariables[i].VariableName, CurrentFile));
                return CleanupItem.Null;
            }
        }

        int offset = VariablesSize;

        CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

        CompiledVariables.Add(compiledVariable);

        newVariable.Type.SetAnalyzedType(compiledVariable.Type);

        Builder.CodeBuilder.AppendCommentLine($"{compiledVariable.Type} {compiledVariable.VariableName.Content}");

        int size;

        if (TryCompute(newVariable.InitialValue, out DataItem computedInitialValue))
        {
            size = 1;

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)computedInitialValue);
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
        { FunctionFrameSize.Last += size; }

        return new CleanupItem(size, newVariable.Modifiers.Contains("temp"), compiledVariable.Type);
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

            yield return (item);
        }
    }

    #endregion

    #region GenerateCodeForSetter

    void GenerateCodeForSetter(Statement statement, StatementWithValue value)
    {
        if (statement is Identifier variableIdentifier)
        { GenerateCodeForSetter(variableIdentifier, value); }
        else if (statement is Pointer pointerToSet)
        { GenerateCodeForSetter(pointerToSet, value); }
        else if (statement is IndexCall index)
        { GenerateCodeForSetter(index, value); }
        else if (statement is Field field)
        { GenerateCodeForSetter(field, value); }
        else
        { throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile); }
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
        }
        else if (GetVariable(statement.Token.Content, out CompiledVariable? variable))
        {
            statement.Token.AnalyzedType = TokenAnalyzedType.VariableName;

            GenerateCodeForStatement(value);

            StackStore(new ValueAddress(variable), variable.Type.Size);
        }
        else
        {
            throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
        }
    }

    void GenerateCodeForSetter(Field field, StatementWithValue value)
    {
        throw new NotImplementedException();
    }

    void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
    {
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
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, label);
    }

    void Return()
    {
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.RET);
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
            ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
            // GeneralType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

            bool canDeallocate = definedParameter.Modifiers.Contains("temp");

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
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
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
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

    Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(OperatorCall functionCall, CompiledOperator compiledFunction)
    {
        Stack<ParameterCleanupItem> parameterCleanup = new();

        for (int i = 0; i < functionCall.Parameters.Length; i++)
        {
            StatementWithValue passedParameter = functionCall.Parameters[i];
            GeneralType passedParameterType = FindStatementType(passedParameter);
            ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
            // GeneralType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

            bool canDeallocate = definedParameter.Modifiers.Contains("temp");

            canDeallocate = canDeallocate && passedParameterType is PointerType;

            if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
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

        if (functionCall.IsMethodCall != compiledFunction.IsMethod)
        { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

        if (compiledFunction.Attributes.HasAttribute("External", "stdout"))
        {
            StatementWithValue valueToPrint = functionCall.Parameters[0];
            GeneralType valueToPrintType = FindStatementType(valueToPrint);

            if (valueToPrintType == BasicType.Char &&
                valueToPrint is LiteralStatement charLiteral)
            {
                string dataLabel = Builder.DataBuilder.NewString(charLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LEA, Registers.EBX, (InstructionOperand)new ValueAddress(0, AddressingMode.BasePointerRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.EAX,
                    dataLabel,
                    charLiteral.Value.Length,
                    Registers.EBX,
                    0);
                return;
            }

            if (valueToPrintType == BasicType.Char)
            {
                GenerateCodeForStatement(valueToPrint);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LEA, Registers.EBX, (InstructionOperand)new ValueAddress(-1, AddressingMode.StackRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.EAX,
                    (InstructionOperand)new ValueAddress(-2, AddressingMode.StackRelative),
                    1,
                    Registers.EBX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP);

                return;
            }

            if (valueToPrint is LiteralStatement stringLiteral &&
                stringLiteral.Type == LiteralType.String)
            {
                string dataLabel = Builder.DataBuilder.NewString(stringLiteral.Value);

                Builder.CodeBuilder.Call_stdcall("_GetStdHandle@4", 4, -11);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LEA, Registers.EBX, (InstructionOperand)new ValueAddress(-1, AddressingMode.StackRelative));

                Builder.CodeBuilder.Call_stdcall("_WriteFile@20", 20,
                    Registers.EAX,
                    dataLabel,
                    stringLiteral.Value.Length,
                    Registers.EBX,
                    0);

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP);

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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.RAX);
            }
        }
    }

    void GenerateCodeForStatement(Statement statement)
    {
        if (statement is KeywordCall instructionStatement)
        { GenerateCodeForStatement(instructionStatement); }
        else if (statement is FunctionCall functionCall)
        { GenerateCodeForStatement(functionCall); }
        else if (statement is IfContainer @if)
        { GenerateCodeForStatement(@if); }
        else if (statement is WhileLoop @while)
        { GenerateCodeForStatement(@while); }
        else if (statement is LiteralStatement literal)
        { GenerateCodeForStatement(literal); }
        else if (statement is Identifier variable)
        { GenerateCodeForStatement(variable); }
        else if (statement is OperatorCall expression)
        { GenerateCodeForStatement(expression); }
        else if (statement is AddressGetter addressGetter)
        { GenerateCodeForStatement(addressGetter); }
        else if (statement is Pointer pointer)
        { GenerateCodeForStatement(pointer); }
        else if (statement is Assignment assignment)
        { GenerateCodeForStatement(assignment); }
        else if (statement is ShortOperatorCall shortOperatorCall)
        { GenerateCodeForStatement(shortOperatorCall); }
        else if (statement is CompoundAssignment compoundAssignment)
        { GenerateCodeForStatement(compoundAssignment); }
        else if (statement is VariableDeclaration variableDeclaration)
        { GenerateCodeForStatement(variableDeclaration); }
        else if (statement is TypeCast typeCast)
        { GenerateCodeForStatement(typeCast); }
        else if (statement is NewInstance newInstance)
        { GenerateCodeForStatement(newInstance); }
        else if (statement is ConstructorCall constructorCall)
        { GenerateCodeForStatement(constructorCall); }
        else if (statement is Field field)
        { GenerateCodeForStatement(field); }
        else if (statement is IndexCall indexCall)
        { GenerateCodeForStatement(indexCall); }
        else if (statement is AnyCall anyCall)
        { GenerateCodeForStatement(anyCall); }
        else if (statement is Block block)
        { GenerateCodeForStatement(block); }
        else if (statement is ModifiedStatement modifiedStatement)
        { GenerateCodeForStatement(modifiedStatement); }
        else
        { throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile); }

        if (statement is FunctionCall statementWithValue &&
            !statementWithValue.SaveValue &&
            GetFunction(statementWithValue, out CompiledFunction? _f) &&
            _f.ReturnSomething)
        {
            throw new NotImplementedException();
        }
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals("ref"))
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, $"[rbp+{address.Address}]".Replace("+-", "-", StringComparison.Ordinal));
            }
            return;
        }

        if (modifier.Equals("temp"))
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
            indexCall.BracketLeft,
            new StatementWithValue[]
            {
                indexCall.Index,
            },
            indexCall.BracketRight));
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, nextLabel);
                GenerateCodeForStatement(ifBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, endLabel);
                Builder.CodeBuilder.AppendLabel(nextLabel);
            }
            else if (part is ElseIfBranch elseIfBranch)
            {
                string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                GenerateCodeForStatement(elseIfBranch.Condition);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, nextLabel);
                GenerateCodeForStatement(elseIfBranch.Block);
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, endLabel);
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

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, endLabel);

        Builder.CodeBuilder.AppendCommentLine($"{{");
        using (Builder.CodeBuilder.Block())
        { GenerateCodeForStatement(@while.Block); }
        Builder.CodeBuilder.AppendCommentLine($"}}");

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, startLabel);

        Builder.CodeBuilder.AppendLabel(endLabel);

        Builder.CodeBuilder.Indent -= SectionBuilder.IndentIncrement;
        Builder.CodeBuilder.AppendCommentLine($"}}");
    }
    void GenerateCodeForStatement(KeywordCall statement)
    {
        switch (statement.Identifier.Content)
        {
            case "return":
            {
                if (statement.Parameters.Length > 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"return\": required {0} or {1} passed {statement.Parameters.Length}", statement, CurrentFile); }

                if (statement.Parameters.Length == 1)
                {
                    StatementWithValue returnValue = statement.Parameters[0];
                    GeneralType returnValueType = FindStatementType(returnValue);

                    GenerateCodeForStatement(returnValue);

                    int offset = ReturnValueOffset;
                    StackStore(new ValueAddress(offset, AddressingMode.BasePointerRelative), returnValueType.Size);
                }

                if (InFunction)
                { Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.ESP, FunctionFrameSize.Last * 4); }

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBP);
                Return();
                break;
            }

            case "break":
            {
                if (statement.Parameters.Length != 0)
                { throw new CompilerException($"Wrong number of parameters passed to \"break\": required {0}, passed {statement.Parameters.Length}", statement, CurrentFile); }

                throw new NotImplementedException();
            }

            case "delete":
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

        if (statement.Modifiers.Contains("const"))
        { return; }

        if (!GetVariable(statement.VariableName.Content, out CompiledVariable? variable))
        { throw new InternalException($"Variable \"{statement.VariableName.Content}\" not found", CurrentFile); }

        if (variable.IsInitialized) return;

        GenerateCodeForSetter(new Identifier(statement.VariableName), statement.InitialValue);

        variable.IsInitialized = true;
    }
    void GenerateCodeForStatement(FunctionCall functionCall)
    {
        if (functionCall.FunctionName == "sizeof")
        {
            throw new NotImplementedException();
        }

        if (GetVariable(functionCall.Identifier.Content, out CompiledVariable? compiledVariable))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

            if (compiledVariable.Type is not FunctionType function)
            { throw new CompilerException($"Variable \"{compiledVariable.VariableName.Content}\" is not a function", functionCall.Identifier, CurrentFile); }

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (function.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(function.ReturnType);
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, function);

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, (InstructionOperand)new ValueAddress(compiledVariable));

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (function.ReturnSomething && !functionCall.SaveValue)
            {
                for (int i = 0; i < returnValueSize; i++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.RAX);
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

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
        {
            if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            { throw new CompilerException($"Function {functionCall.ToReadable(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, statement.Value);
                break;
            case LiteralType.Float:
                throw new NotImplementedException();
            case LiteralType.String:
                throw new NotImplementedException();
            case LiteralType.Char:
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, statement.Value[0]);
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
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (InstructionOperand)constant.Value);
            return;
        }

        if (GetParameter(statement.Content, out CompiledParameter? compiledParameter))
        {
            if (statement.Content != "this")
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

        if (GetFunction(statement.Token, expectedType, out CompiledFunction? compiledFunction))
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

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, label);
            return;
        }

        throw new CompilerException($"Variable \"{statement.Content}\" not found", statement, CurrentFile);
    }
    void GenerateCodeForStatement(OperatorCall statement)
    {
        if (GetOperator(statement, out _))
        {
            throw new NotImplementedException();
        }
        else if (LanguageOperators.OpCodes.TryGetValue(statement.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[statement.Operator.Content] != statement.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator \"{statement.Operator.Content}\": required {LanguageOperators.ParameterCounts[statement.Operator.Content]} passed {statement.ParameterCount}", statement.Operator, CurrentFile); }

            GenerateCodeForStatement(statement.Left);

            if (statement.Right != null)
            {
                GenerateCodeForStatement(statement.Right);
            }

            switch (opcode)
            {
                case Opcode.LOGIC_LT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JGE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_MT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JLE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_LTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JG, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_MTEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JL, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_OR:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JNE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EBX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JNE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_AND:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EBX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);

                    break;
                }
                case Opcode.LOGIC_EQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JNE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_NEQ:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }
                case Opcode.LOGIC_NOT:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CMP, Registers.EAX, 0);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JNE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    break;
                }

                case Opcode.BITS_AND:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PAND, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.BITS_OR:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POR, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.BITS_XOR:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.XOR, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;

                case Opcode.BITS_SHIFT_LEFT:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SHL, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.BITS_SHIFT_RIGHT:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SHR, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;

                case Opcode.MATH_ADD:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.MATH_SUB:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SUB, Registers.EBX, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBX);
                    break;
                case Opcode.MATH_MULT:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MUL, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.MATH_DIV:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CDQ);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CDQ);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.IDIV, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;
                case Opcode.MATH_MOD:
                {
                    string label1 = Builder.CodeBuilder.NewLabel();
                    string label2 = Builder.CodeBuilder.NewLabel();

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CDQ);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.IDIV, Registers.EAX, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, Registers.EDX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JE, label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, 1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, label2);
                    Builder.CodeBuilder.AppendLabel(label1);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, 0);
                    Builder.CodeBuilder.AppendLabel(label2);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
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
            keywordCall.Identifier.Equals("return") &&
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
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.ESP, 4);
            }
        }
    }

    void GenerateCodeForTopLevelStatements(Statement[] statements)
    {
        CompileConstants(statements);

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBP, Registers.ESP);

        Builder.CodeBuilder.AppendCommentLine("Variables:");
        CleanupItem[] cleanup = GenerateCodeForVariable(statements).ToArray();
        bool hasExited = false;

        Builder.CodeBuilder.AppendCommentLine("Code:");

        for (int i = 0; i < statements.Length; i++)
        {
            if (statements[i] is KeywordCall keywordCall &&
                keywordCall.Identifier.Equals("return"))
            {
                if (keywordCall.Parameters.Length != 0 &&
                    keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (required 0 or 1, passed {keywordCall.Parameters.Length})", keywordCall, CurrentFile); }

                if (keywordCall.Parameters.Length == 1)
                {
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

        if (!hasExited)
        {
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0);
            Builder.CodeBuilder.Call_stdcall("_ExitProcess@4", 4);
        }

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.HALT);

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
        if (LanguageConstants.Keywords.Contains(function.Identifier.Content))
        { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

        function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

        if (function is CompiledFunction compiledFunction1) GeneratedFunctions.Add(compiledFunction1);

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Equals("External"))
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

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBP);
        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBP, Registers.ESP);

        Builder.CodeBuilder.AppendCommentLine("Block");

        GenerateCodeForStatement(function.Block);

        Builder.CodeBuilder.AppendCommentLine("End frame");

        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBP);

        CurrentFile = null;

        Return();

        CompiledParameters.Clear();

        InFunction = false;
        FunctionFrameSize.Pop(0);
        Builder.CodeBuilder.Indent = 0;
    }

    AsmGeneratorResult GenerateCode(CompilerResult compilerResult, PrintCallback? printCallback = null)
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
            AssemblyCode = Builder.Make(),
        };
    }

    public static AsmGeneratorResult Generate(CompilerResult compilerResult, AsmGeneratorSettings generatorSettings, PrintCallback? printCallback = null, AnalysisCollection? analysisCollection = null)
        => new CodeGeneratorForAsm(compilerResult, generatorSettings, analysisCollection).GenerateCode(compilerResult, printCallback);
}
