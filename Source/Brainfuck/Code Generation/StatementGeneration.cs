using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Tokenizing;

namespace LanguageCore.Brainfuck.Generator;

public partial class CodeGeneratorForBrainfuck : CodeGenerator
{
    bool AllowLoopUnrolling => !Settings.DontOptimize;
    bool AllowFunctionInlining => !Settings.DontOptimize;
    bool AllowPrecomputing => !Settings.DontOptimize;
    bool AllowEvaluating => !Settings.DontOptimize;
    bool AllowOtherOptimizations => !Settings.DontOptimize;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int Optimizations { get; set; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int Precomputations { get; set; }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] int FunctionEvaluations { get; set; }

    void GenerateAllocator(int size, IPositioned position, Uri file)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create<StatementWithValue>(Literal.CreateAnonymous(LiteralType.Integer, size.ToString(CultureInfo.InvariantCulture), position, file));

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, parameters, file, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found", position, file));
            return;
        }
        if (!result.Function.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", position, file));
            return;
        }

        if (!result.Function.CanUse(file))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{result.Function.ToReadable()}\" cannot be called due to its protection level", position, file));
            return;
        }

        GenerateCodeForFunction(result.Function, parameters, null, new Location(position.Position, file));
    }

    void GenerateAllocator(StatementWithValue size)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create<StatementWithValue>(size);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, parameters, size.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found", size));
            return;
        }
        if (!result.Function.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", size));
            return;
        }

        if (!result.Function.CanUse(size.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{result.Function.ToReadable()}\" cannot be called due to its protection level", size));
            return;
        }

        GenerateCodeForFunction(result.Function, parameters, null, size);
    }

    void GenerateDestructor(StatementWithValue value)
    {
        GeneralType deallocateableType = FindStatementType(value);

        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);
        ImmutableArray<GeneralType> parameterTypes = FindStatementTypes(parameters);

        if (!deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            Diagnostics.Add(Diagnostic.Warning($"The \"{StatementKeywords.Delete}\" keyword-function is only working on pointers so I skip this", value));
            return;
        }

        if (!GetGeneralFunction(deallocateablePointerType.To, parameterTypes, BuiltinFunctionIdentifiers.Destructor, value.File, out FunctionQueryResult<CompiledGeneralFunction>? result, out PossibleDiagnostic? error))
        {
            GenerateDeallocator(value);

            if (!deallocateablePointerType.To.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Warning(
                    $"Destructor for type \"{deallocateablePointerType}\" not found",
                    value,
                    error.ToWarning(value, value.File)));
            }

            return;
        }

        (CompiledGeneralFunction? destructor, Dictionary<string, GeneralType>? typeArguments) = result;

        if (!destructor.CanUse(value.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" cannot be called due to its protection level", value));
            return;
        }

        GenerateCodeForFunction(destructor, ImmutableArray.Create(value), typeArguments, value);

        if (destructor.ReturnSomething)
        { Stack.Pop(); }

        GenerateDeallocator(value);
    }

    void GenerateDeallocator(StatementWithValue value)
    {
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameters, value.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(Diagnostic.Critical(
                $"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found",
                value,
                value.File,
                notFoundError.ToError(value)));
            return;
        }

        GenerateCodeForFunction(result.Function, parameters, null, value);
    }

    #region PrecompileVariables
    int PrecompileVariables(Block block, bool ignoreRedefinition)
    { return PrecompileVariables(block.Statements, ignoreRedefinition); }
    int PrecompileVariables(IEnumerable<Statement>? statements, bool ignoreRedefinition)
    {
        if (statements == null) return 0;

        int result = 0;
        foreach (Statement statement in statements)
        { result += PrecompileVariables(statement, ignoreRedefinition); }
        return result;
    }
    int PrecompileVariables(Statement statement, bool ignoreRedefinition)
    {
        if (statement is not VariableDeclaration instruction)
        { return 0; }

        return PrecompileVariable(instruction, ignoreRedefinition);
    }
    int PrecompileVariable(VariableDeclaration variableDeclaration, bool ignoreRedefinition)
        => PrecompileVariable(CompiledVariables, variableDeclaration, variableDeclaration.Modifiers.Contains(ModifierKeywords.Temp), ignoreRedefinition);
    int PrecompileVariable(Stack<BrainfuckVariable> variables, VariableDeclaration variableDeclaration, bool deallocateOnClean, bool ignoreRedefinition, GeneralType? type = null)
    {
        if (variables.Any(other =>
                other.Identifier.Content == variableDeclaration.Identifier.Content &&
                other.File == variableDeclaration.File))
        {
            if (ignoreRedefinition) return 0;
            Diagnostics.Add(Diagnostic.Critical($"Variable \"{variableDeclaration.Identifier.Content}\" already defined", variableDeclaration.Identifier));
        }

        if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
        { return 0; }

        if (type is not null)
        {

        }
        else if (variableDeclaration.Type == StatementKeywords.Var)
        {
            if (variableDeclaration.InitialValue == null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable with implicit type must have an initial value", variableDeclaration));
                return default;
            }

            type = FindStatementType(variableDeclaration.InitialValue);
        }
        else
        {
            type = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);
            variableDeclaration.Type.SetAnalyzedType(type);
        }

        if (variableDeclaration.InitialValue != null)
        {
            GeneralType initialValueType = FindStatementType(variableDeclaration.InitialValue, type);

            if (initialValueType.GetSize(this, Diagnostics, variableDeclaration.InitialValue) != type.GetSize(this, Diagnostics, variableDeclaration))
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable initial value type (\"{initialValueType}\") and variable type (\"{type}\") mismatch", variableDeclaration.InitialValue));
                return default;
            }

            if (type.Is(out ArrayType? arrayType))
            {
                if (arrayType.Of.SameAs(BasicType.Char) &&
                    variableDeclaration.InitialValue is Literal literal)
                {
                    if (literal.Type != LiteralType.String)
                    { throw new NotSupportedException($"Only string literals supported", literal); }
                    if (arrayType.Length is not null)
                    {
                        if (!arrayType.ComputedLength.HasValue)
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Literal length {literal.Value.Length} must be equal to the stack array length <runtime value>", literal, literal.File));
                            return default;
                        }
                        if (literal.Value.Length != arrayType.ComputedLength.Value)
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Literal length {literal.Value.Length} must be equal to the stack array length {arrayType.ComputedLength.Value}", literal, literal.File));
                            return default;
                        }
                    }

                    using (DebugBlock(variableDeclaration.InitialValue))
                    {
                        int arraySize = literal.Value.Length;

                        int size = Snippets.ARRAY_SIZE(arraySize);

                        int address2 = Stack.PushVirtual(size, literal);

                        variables.Push(new BrainfuckVariable(address2, false, true, deallocateOnClean, type, size, variableDeclaration)
                        {
                            IsInitialized = true
                        });

                        for (int i = 0; i < literal.Value.Length; i++)
                        { Code.ARRAY_SET_CONST(address2, i, new CompiledValue(literal.Value[i])); }
                    }
                }
                else
                {
                    if (!arrayType.ComputedLength.HasValue)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"This aint supported", variableDeclaration));
                        return default;
                    }

                    int arraySize = arrayType.ComputedLength.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    int address2 = Stack.PushVirtual(size, variableDeclaration);
                    variables.Push(new BrainfuckVariable(address2, false, true, deallocateOnClean, type, size, variableDeclaration));
                }
                return 1;
            }

            if (variableDeclaration.InitialValue is AddressGetter addressGetter &&
                GetVariable(addressGetter.PrevStatement, out BrainfuckVariable? shadowingVariable, out _) &&
                type.Is(out PointerType? pointerType))
            {
                if (!CanCastImplicitly(pointerType.To, shadowingVariable.Type, null, this, out PossibleDiagnostic? castError))
                { Diagnostics.Add(castError.ToError(variableDeclaration.InitialValue)); }

                variables.Push(new BrainfuckVariable(shadowingVariable.Address, true, false, false, type, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration)
                {
                    IsInitialized = true
                });
                return 0;
            }

            int address = Stack.PushVirtual(type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, deallocateOnClean, type, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration));
            return 1;
        }
        else
        {
            if (type.Is(out ArrayType? arrayType))
            {
                if (!arrayType.ComputedLength.HasValue)
                {
                    Diagnostics.Add(Diagnostic.Critical($"This aint supported", variableDeclaration));
                    return default;
                }

                int arraySize = arrayType.ComputedLength.Value;

                int size = Snippets.ARRAY_SIZE(arraySize);

                int address2 = Stack.PushVirtual(size, variableDeclaration);
                variables.Push(new BrainfuckVariable(address2, false, true, deallocateOnClean, type, size, variableDeclaration));
                return 1;
            }

            int address = Stack.PushVirtual(type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, deallocateOnClean, type, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration));
            return 1;
        }
    }
    #endregion

    #region Find Size

    protected override bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = 1;
        error = null;
        return true;
    }

    protected override bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = 1;
        error = null;
        return true;
    }

    protected override bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!TryCompute(type.Length, out CompiledValue lengthValue))
        {
            error = new PossibleDiagnostic($"Can't compute the array type's length");
            return false;
        }

        error = null;
        size = Snippets.ARRAY_SIZE((int)lengthValue);
        return true;
    }

    protected override bool FindSize(BuiltinType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = default;
        switch (type.Type)
        {
            case BasicType.Void: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.Any: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.U8: size = 1; return true;
            case BasicType.I8: size = 1; return true;
            case BasicType.Char: size = 1; return true;
            case BasicType.I16: size = 1; return true;
            case BasicType.U32: size = 1; return true;
            case BasicType.I32: size = 1; return true;
            case BasicType.F32: size = 1; return true;
            default: throw new UnreachableException();
        }
    }

    #endregion

    #region Generate Size

    bool GenerateSize(GeneralType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        PointerType v => GenerateSize(v, result, out error),
        ArrayType v => GenerateSize(v, result, out error),
        FunctionType v => GenerateSize(v, result, out error),
        StructType v => GenerateSize(v, result, out error),
        GenericType v => GenerateSize(v, result, out error),
        BuiltinType v => GenerateSize(v, result, out error),
        AliasType v => GenerateSize(v, result, out error),
        _ => throw new NotImplementedException(),
    };

    bool GenerateSize(PointerType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.AddValue(result, 1);
        return true;
    }
    bool GenerateSize(ArrayType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        if (FindSize(type, out int size, out error))
        {
            Code.AddValue(result, size);
            return true;
        }

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        int lengthAddress = Stack.NextAddress;
        GenerateCodeForStatement(type.Length);

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        using (StackAddress elementSizeAddress = Stack.Push(elementSize))
        { Code.MULTIPLY(lengthAddress, elementSizeAddress, v => Stack.GetTemporaryAddress(v, null)); }

        Code.MoveAddValue(lengthAddress, result);
        Stack.PopVirtual();
        return true;
    }
    bool GenerateSize(FunctionType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.AddValue(result, 1);
        return true;
    }
    bool GenerateSize(StructType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.AddValue(result, size);
        return true;
    }
    bool GenerateSize(GenericType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(BuiltinType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.AddValue(result, size);
        return true;
    }
    bool GenerateSize(AliasType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Value, result, out error);

    #endregion

    #region GenerateCodeForSetter()

    void GenerateCodeForSetter(Statement statement, StatementWithValue value)
    {
        switch (statement)
        {
            case Identifier v: GenerateCodeForSetter(v, value); break;
            case Pointer v: GenerateCodeForSetter(v, value); break;
            case IndexCall v: GenerateCodeForSetter(v, value); break;
            case Field v: GenerateCodeForSetter(v, value); break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Setter for statement \"{statement.GetType().Name}\" not implemented", statement, statement.File));
                return;
        }
    }

    void GenerateCodeForSetter(Identifier statement, StatementWithValue value)
    {
        if (GetConstant(statement.Content, statement.File, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Critical($"This is a constant so you can not modify it's value", statement, statement.File));
            return;
        }

        if (!GetVariable(statement, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(statement));
            return;
        }

        GenerateCodeForSetter(variable, value);
    }

    void GenerateCodeForSetter(Field statementToSet, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType type = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, type);

        if ((
            GetVariable(statementToSet.PrevStatement, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            prevType.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is<StructType>()
        ) ||
            prevType.Is<StructType>())
        {
            if (!type.SameAs(valueType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Can not set a \"{valueType}\" type value to the \"{type}\" type field.", value, value.File));
                return;
            }

            if (!TryGetAddress(statementToSet, out Address? address, out int size))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get field address", statementToSet));
                return;
            }

            if (size != valueType.GetSize(this, Diagnostics, value))
            {
                Diagnostics.Add(Diagnostic.Critical($"Field and value size mismatch", value));
                return;
            }

            CompileSetter(address, value);
            return;
        }

        if (prevType.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{pointerType}\"", statementToSet.PrevStatement, statementToSet.PrevStatement.File));
                return;
            }

            if (!structPointerType.GetField(statementToSet.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(statementToSet.Identifier, statementToSet.File));
                return;
            }

            statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            statementToSet.Reference = fieldDefinition;
            statementToSet.CompiledType = fieldDefinition.Type;

            if (type.GetSize(this, Diagnostics, statementToSet) != valueType.GetSize(this, Diagnostics, value))
            {
                Diagnostics.Add(Diagnostic.Critical($"Field and value size mismatch", value, value.File));
                return;
            }

            int _pointerAddress = Stack.NextAddress;
            using (DebugInfoBlock debugBlock = DebugBlock(statementToSet))
            {
                GenerateCodeForStatement(statementToSet.PrevStatement);
                Code.AddValue(_pointerAddress, fieldOffset);
            }

            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            Heap.Set(_pointerAddress, valueAddress);

            Stack.Pop(); // valueAddress
            Stack.Pop(); // _pointerAddress

            return;
        }

        throw new NotImplementedException();
    }

    void GenerateCodeForSetter(BrainfuckVariable variable, StatementWithValue value)
    {
        if (AllowOtherOptimizations &&
            GetVariable(value, out BrainfuckVariable? valueVariable, out _))
        {
            if (variable.Address == valueVariable.Address)
            {
                Optimizations++;
                return;
            }

            if (valueVariable.IsDiscarded)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{valueVariable.Name}\" is discarded", value));
                return;
            }

            if (!valueVariable.IsInitialized)
            { Diagnostics.Add(Diagnostic.Warning($"Variable \"{valueVariable.Name}\" is not initialized", value)); }

            if (variable.Size != valueVariable.Size)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable and value size mismatch ({variable.Size} != {valueVariable.Size})", value));
                return;
            }

            UndiscardVariable(CompiledVariables, variable.Name);

            using StackAddress tempAddress = Stack.GetTemporaryAddress(1, value);

            int size = valueVariable.Size;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedSource = valueVariable.Address + offset;
                int offsettedTarget = variable.Address + offset;

                Code.CopyValue(offsettedSource, offsettedTarget, tempAddress);
            }

            Optimizations++;

            variable.IsInitialized = valueVariable.IsInitialized;

            return;
        }

        if (VariableUses(value, variable) == 0)
        { VariableCanBeDiscarded = variable.Name; }

        using (Code.Block(this, $"Set variable \"{variable.Name}\" (at {variable.Address}) to \"{value}\""))
        {
            if (AllowPrecomputing && TryCompute(value, out CompiledValue constantValue))
            {
                if (constantValue.TryCast(variable.Type, out CompiledValue castedValue))
                { constantValue = castedValue; }

                if (variable.IsReference &&
                    variable.Type.Is(out PointerType? pointerType))
                {
                    if (!CanCastImplicitly(constantValue, pointerType.To, value, out PossibleDiagnostic? castError))
                    { Diagnostics.Add(castError.ToError(value)); }
                }
                else
                {
                    if (!CanCastImplicitly(constantValue, variable.Type, value, out PossibleDiagnostic? castError))
                    { Diagnostics.Add(castError.ToError(value)); }
                }

                Code.SetValue(variable.Address, constantValue);

                Precomputations++;

                VariableCanBeDiscarded = null;
                variable.IsInitialized = true;
                return;
            }

            GeneralType valueType = FindStatementType(value);
            int valueSize = valueType.GetSize(this, Diagnostics, value);

            if (variable.Type.Is(out ArrayType? arrayType))
            {
                if (arrayType.Of.SameAs(BasicType.Char))
                {
                    if (value is not Literal literal)
                    { throw new InternalExceptionWithoutContext(); }
                    if (literal.Type != LiteralType.String)
                    { throw new InternalExceptionWithoutContext(); }
                    if (!arrayType.ComputedLength.HasValue)
                    { throw new InternalExceptionWithoutContext(); }
                    if (literal.Value.Length != arrayType.ComputedLength.Value)
                    { throw new InternalExceptionWithoutContext(); }

                    int arraySize = arrayType.ComputedLength.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                    using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                    {
                        for (int i = 0; i < literal.Value.Length; i++)
                        {
                            Code.SetValue(indexAddress, i);
                            Code.SetValue(valueAddress, literal.Value[i]);
                            Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                        }
                    }

                    UndiscardVariable(CompiledVariables, variable.Name);

                    VariableCanBeDiscarded = null;

                    return;
                }
                else if (arrayType.Of.SameAs(BasicType.U8))
                {
                    if (value is not Literal literal)
                    { throw new InternalExceptionWithoutContext(); }
                    if (literal.Type != LiteralType.String)
                    { throw new InternalExceptionWithoutContext(); }
                    if (!arrayType.ComputedLength.HasValue)
                    { throw new InternalExceptionWithoutContext(); }
                    if (literal.Value.Length != arrayType.ComputedLength.Value)
                    { throw new InternalExceptionWithoutContext(); }

                    int arraySize = arrayType.ComputedLength.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                    using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                    {
                        for (int i = 0; i < literal.Value.Length; i++)
                        {
                            Code.SetValue(indexAddress, i);
                            Code.SetValue(valueAddress, (byte)literal.Value[i]);
                            Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                        }
                    }

                    UndiscardVariable(CompiledVariables, variable.Name);

                    VariableCanBeDiscarded = null;

                    return;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                if (TryComputeSimple(value, out CompiledValue compiledValue))
                {
                    if (variable.IsReference &&
                        variable.Type.Is(out PointerType? pointerType))
                    {
                        if (!CanCastImplicitly(compiledValue, pointerType.To, value, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(value)); }
                    }
                    else
                    {
                        if (!CanCastImplicitly(compiledValue, variable.Type, value, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(value)); }
                    }
                }
                else
                {
                    if (variable.IsReference &&
                        variable.Type.Is(out PointerType? pointerType))
                    {
                        if (!CanCastImplicitly(valueType, pointerType.To, value, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(value)); }
                    }
                    else
                    {
                        if (!CanCastImplicitly(valueType, variable.Type, value, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(value)); }
                    }
                }
            }

            using (Code.Block(this, $"Compute value"))
            {
                GenerateCodeForStatement(value);
            }

            using (Code.Block(this, $"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
            { Stack.PopAndStore(variable.Address); }

            UndiscardVariable(CompiledVariables, variable.Name);

            VariableCanBeDiscarded = null;
            variable.IsInitialized = true;
        }
    }

    void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
    {
        if (statement.PrevStatement is Identifier variableIdentifier)
        {
            if (!GetVariable(variableIdentifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableIdentifier));
                return;
            }

            if (variable.IsDiscarded)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", variableIdentifier));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", variableIdentifier));
                return;
            }

            if (!variable.IsInitialized)
            { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", variableIdentifier)); }

            if (variable.IsReference)
            {
                GenerateCodeForSetter(variable, value);
                return;
            }
        }

        CompileDereferencedSetter(statement.PrevStatement, 0, value);
    }

    void CompileSetter(Address address, StatementWithValue value)
    {
        switch (address)
        {
            case AddressAbsolute v: CompileSetter(v, value); break;
            case AddressOffset v: CompileSetter(v, value); break;
            default: throw new NotImplementedException();
        }
    }

    void CompileSetter(AddressAbsolute address, StatementWithValue value)
    {
        using (Code.Block(this, $"Set value \"{value}\" to address {address}"))
        {
            if (AllowPrecomputing && TryCompute(value, out CompiledValue constantValue))
            {
                Code.SetValue(address.Value, constantValue.U8);

                Precomputations++;

                return;
            }

            int stackSize = Stack.Size;

            using (Code.Block(this, $"Compute value"))
            {
                GenerateCodeForStatement(value);
            }

            int variableSize = Stack.Size - stackSize;

            using (Code.Block(this, $"Store computed value (from {Stack.LastAddress}) to {address}"))
            { Stack.PopAndStore(address.Value); }
        }
    }

    void CompileSetter(AddressOffset address, StatementWithValue value)
    {
        if (address.Base is AddressRuntimePointer runtimePointer)
        {
            GeneralType referenceType = FindStatementType(runtimePointer.PointerValue);

            if (!referenceType.Is(out PointerType? _))
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", runtimePointer.PointerValue));
                return;
            }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(runtimePointer.PointerValue);

            /*
            {
                using StackAddress checkResultAddress = Stack.PushVirtual(1);
                {
                    using StackAddress maxSizeAddress = Stack.Push(GeneratorSettings.HeapSize);
                    using StackAddress pointerAddressCopy = Stack.PushVirtual(1);
                    {
                        Code.CopyValue(pointerAddress, pointerAddressCopy);

                        Code.LOGIC_MT(pointerAddressCopy, maxSizeAddress, checkResultAddress, checkResultAddress + 1, checkResultAddress + 2);
                    }

                    using (Code.ConditionalBlock(this, checkResultAddress))
                    { Code.OUT_STRING(checkResultAddress, "\nOut of memory range\n"); }
                }
            }
            */

            Code.AddValue(pointerAddress, address.Offset);

            GeneralType valueType = FindStatementType(value);

            // TODO: this
            // AssignTypeCheck(pointerType.To, valueType, value);

            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            if (valueType.GetSize(this, Diagnostics, value) == 1 && AllowOtherOptimizations)
            {
                Heap.Set(pointerAddress, valueAddress);
            }
            else
            {
                using StackAddress tempPointerAddress = Stack.PushVirtual(1, value);
                for (int i = 0; i < valueType.GetSize(this, Diagnostics, value); i++)
                {
                    Code.CopyValue(pointerAddress, tempPointerAddress);
                    Heap.Set(tempPointerAddress, valueAddress + i);
                    Code.AddValue(pointerAddress, 1);
                }
            }

            Stack.PopVirtual();
            Stack.PopVirtual();
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    void CompileDereferencedSetter(StatementWithValue dereference, int offset, StatementWithValue value)
    {
        GeneralType referenceType = FindStatementType(dereference);

        if (!referenceType.Is(out PointerType? _))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", dereference, dereference.File));
            return;
        }

        int pointerAddress = Stack.NextAddress;
        GenerateCodeForStatement(dereference);

        /*
        {
            using StackAddress checkResultAddress = Stack.PushVirtual(1);
            {
                using StackAddress maxSizeAddress = Stack.Push(GeneratorSettings.HeapSize);
                using StackAddress pointerAddressCopy = Stack.PushVirtual(1);
                {
                    Code.CopyValue(pointerAddress, pointerAddressCopy);

                    Code.LOGIC_MT(pointerAddressCopy, maxSizeAddress, checkResultAddress, checkResultAddress + 1, checkResultAddress + 2);
                }

                using (Code.ConditionalBlock(this, checkResultAddress))
                { Code.OUT_STRING(checkResultAddress, "\nOut of memory range\n"); }
            }
        }
        */

        Code.AddValue(pointerAddress, offset);

        GeneralType valueType = FindStatementType(value);

        // TODO: this
        // AssignTypeCheck(pointerType.To, valueType, value);

        int valueAddress = Stack.NextAddress;
        GenerateCodeForStatement(value);

        if (valueType.GetSize(this, Diagnostics, value) == 1 && AllowOtherOptimizations)
        {
            Heap.Set(pointerAddress, valueAddress);
        }
        else
        {
            using StackAddress tempPointerAddress = Stack.PushVirtual(1, value);
            for (int i = 0; i < valueType.GetSize(this, Diagnostics, value); i++)
            {
                Code.CopyValue(pointerAddress, tempPointerAddress);
                Heap.Set(tempPointerAddress, valueAddress + i);
                Code.AddValue(pointerAddress, 1);
            }
        }

        Stack.PopVirtual();
        Stack.PopVirtual();
    }

    void GenerateCodeForSetter(IndexCall statement, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statement.PrevStatement);
        GeneralType indexType = FindStatementType(statement.Index);
        GeneralType valueType = FindStatementType(value);

        if (GetIndexSetter(prevType, valueType, indexType, statement.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? indexerNotFoundError))
        {
            (CompiledFunction? indexer, Dictionary<string, GeneralType>? typeArguments) = result;

            if (!indexer.CanUse(statement.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Function \"{indexer.ToReadable()}\" cannot be called due to its protection level", statement));
                return;
            }

            GenerateCodeForFunction(indexer, ImmutableArray.Create(
                statement.PrevStatement,
                statement.Index,
                value
            ), typeArguments, statement);

            if (!statement.SaveValue && indexer.ReturnSomething)
            { Stack.Pop(); }

            return;
        }

        PossibleDiagnostic? variableNotFoundError = null;

        if ((
            GetVariable(statement.PrevStatement, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            prevType.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is(out ArrayType? arrayType)
        ) ||
            prevType.Is(out arrayType)
        )
        {
            if (!TryGetAddress(statement.PrevStatement, out Address? arrayAddress, out _))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get array address", statement.PrevStatement));
                return;
            }

            if (variable is not null)
            {
                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement.PrevStatement));
                    return;
                }

                if (!variable.IsInitialized)
                { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement.PrevStatement)); }
            }

            using (Code.Block(this, $"Set array (\"{statement.PrevStatement}\") index (\"{statement.Index}\") (at {arrayAddress}) to \"{value}\""))
            {
                GeneralType elementType = arrayType.Of;

                if (!elementType.SameAs(valueType))
                {
                    Diagnostics.Add(Diagnostic.Critical("Bruh", value));
                    return;
                }

                int elementSize = elementType.GetSize(this, Diagnostics, statement.PrevStatement);

                if (elementSize != 1)
                { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", value); }

                int indexAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute index"))
                { GenerateCodeForStatement(statement.Index); }

                int valueAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute value"))
                { GenerateCodeForStatement(value); }

                if (arrayAddress is not AddressAbsolute arrayAddressAbs)
                { throw new NotImplementedException(); }

                Code.ARRAY_SET(arrayAddressAbs.Value, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, statement));

                Stack.Pop();
                Stack.Pop();
            }

            return;
        }

        if (prevType.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            if (!arrayType.Of.SameAs(valueType))
            {
                Diagnostics.Add(Diagnostic.Critical("Bruh", value));
                return;
            }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(statement.PrevStatement);

            if (!indexType.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Critical($"Index type must be built-in (ie. \"i32\") and not \"{indexType}\"", statement.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(statement.Index);

            if (arrayType.Of.GetSize(this, Diagnostics, statement.PrevStatement) != 1)
            {
                using StackAddress multiplierAddress = Stack.Push(arrayType.Of.GetSize(this, Diagnostics, statement.PrevStatement));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, value));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Set(pointerAddress, valueAddress);

            Stack.Pop(); // pointerAddress
            Stack.Pop(); // valueAddress

            return;
        }

        if (variableNotFoundError is not null) Diagnostics.Add(variableNotFoundError.ToError(statement.PrevStatement));
        Diagnostics.Add(indexerNotFoundError.ToError(statement));
    }

    #endregion

    #region GenerateCodeForStatement()
    void GenerateCodeForStatement(RuntimeStatement statement)
    {
        switch (statement)
        {
            case RuntimeFunctionCall v: GenerateCodeForStatement(v); break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown runtime statement \"{statement.GetType().Name}\"", statement, statement.File));
                return;
        }
    }
    void GenerateCodeForStatement(RuntimeFunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (!functionCall.Function.CanUse(functionCall.Original.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{functionCall.Function.ToReadable()}\" cannot be called due to its protection level", functionCall.Original));
            return;
        }

        GenerateCodeForFunction(functionCall.Function, Literal.CreateAnonymous(functionCall.Parameters, functionCall.Original.MethodArguments).ToImmutableArray<StatementWithValue>(), null, functionCall.Original);

        if (!functionCall.Original.SaveValue && functionCall.Function.ReturnSomething)
        { Stack.Pop(); }
    }

    void GenerateCodeForStatement(Statement statement, GeneralType? expectedType = null)
    {
        switch (statement)
        {
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case FunctionCall v: GenerateCodeForStatement(v); break;
            case IfContainer v: GenerateCodeForStatement(v.ToLinks()); break;
            case WhileLoop v: GenerateCodeForStatement(v); break;
            case ForLoop v: GenerateCodeForStatement(v); break;
            case Literal v: GenerateCodeForStatement(v, expectedType); break;
            case Identifier v: GenerateCodeForStatement(v); break;
            case BinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case UnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case AddressGetter v: GenerateCodeForStatement(v); break;
            case Pointer v: GenerateCodeForStatement(v); break;
            case Assignment v: GenerateCodeForStatement(v); break;
            case ShortOperatorCall v: GenerateCodeForStatement(v); break;
            case CompoundAssignment v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case BasicTypeCast v: GenerateCodeForStatement(v); break;
            case ManagedTypeCast v: GenerateCodeForStatement(v); break;
            case NewInstance v: GenerateCodeForStatement(v); break;
            case ConstructorCall v: GenerateCodeForStatement(v); break;
            case Field v: GenerateCodeForStatement(v); break;
            case IndexCall v: GenerateCodeForStatement(v); break;
            case AnyCall v: GenerateCodeForStatement(v); break;
            case ModifiedStatement v: GenerateCodeForStatement(v); break;
            case Block v: GenerateCodeForStatement(v); break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown statement \"{statement.GetType().Name}\"", statement));
                return;
        }
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Content == ModifierKeywords.Ref)
        {
            throw new NotImplementedException();
        }

        if (modifier.Content == ModifierKeywords.Temp)
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

        throw new NotSupportedException($"Function pointers not supported by brainfuck", anyCall.PrevStatement);
    }
    void GenerateCodeForStatement(IndexCall indexCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(indexCall);

        GeneralType prevType = FindStatementType(indexCall.PrevStatement);
        GeneralType indexType = FindStatementType(indexCall.Index);

        if (GetIndexGetter(prevType, indexType, indexCall.File, out FunctionQueryResult<CompiledFunction>? indexer, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            if (!indexer.Function.CanUse(indexCall.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Function \"{indexer.Function.ToReadable()}\" cannot be called due to its protection level", indexCall));
                return;
            }

            GenerateCodeForFunction(indexer.Function, ImmutableArray.Create(indexCall.PrevStatement, indexCall.Index), indexer.TypeArguments, indexCall);

            if (!indexCall.SaveValue && indexer.Function.ReturnSomething)
            { Stack.Pop(); }
            return;
        }

        if ((
            GetVariable(indexCall.PrevStatement, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            prevType.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is(out ArrayType? arrayType)
        ) ||
            prevType.Is(out arrayType)
        )
        {
            if (!TryGetAddress(indexCall.PrevStatement, out Address? arrayAddress, out _))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get array address", indexCall.PrevStatement));
                return;
            }

            GeneralType elementType = arrayType.Of;

            int elementSize = elementType.GetSize(this, Diagnostics, indexCall.PrevStatement);

            if (elementSize != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Array element size must be 1 :(", indexCall));
                return;
            }

            int resultAddress = Stack.PushVirtual(elementSize, indexCall);

            int indexAddress = Stack.NextAddress;
            using (Code.Block(this, $"Compute index"))
            { GenerateCodeForStatement(indexCall.Index); }

            if (arrayAddress is not AddressAbsolute arrayAddressAbs)
            { throw new NotImplementedException(); }

            Code.ARRAY_GET(arrayAddressAbs.Value, indexAddress, resultAddress);

            Stack.Pop();

            return;
        }

        if (prevType.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            int resultAddress = Stack.Push(0);

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.PrevStatement);

            if (!indexType.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Critical($"Index type must be built-in (ie. \"int\") and not \"{indexType}\"", indexCall.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.Index);

            {
                using StackAddress multiplierAddress = Stack.Push(arrayType.Of.GetSize(this, Diagnostics, indexCall.PrevStatement));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, indexCall.Index));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Get(pointerAddress, resultAddress);

            Stack.Pop(); // pointerAddress
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Index getter for type \"{prevType}\" not found", indexCall));
        return;
    }
    void GenerateCodeForStatement(LinkedIf @if, bool linked = false)
    {
        if (TryCompute(@if.Condition, out CompiledValue computedCondition))
        {
            if (computedCondition)
            { GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block)); }
            else if (@if.NextLink is not null)
            { GenerateCodeForStatement(@if.NextLink); }
            return;
        }

        {
            if (@if.Condition is BasicTypeCast _basicTypeCast &&
                GeneralType.From(_basicTypeCast.Type, FindType, TryCompute).Is<BuiltinType>() &&
                _basicTypeCast.PrevStatement is Literal _literal &&
                _literal.Type == LiteralType.String)
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
                return;
            }
        }

        {
            if (@if.Condition is ManagedTypeCast _typeCast &&
                GeneralType.From(_typeCast.Type, FindType, TryCompute).Is<BuiltinType>() &&
                _typeCast.PrevStatement is Literal _literal &&
                _literal.Type == LiteralType.String)
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
                return;
            }
        }

        {
            if (@if.Condition is Literal _literal &&
                _literal.Type == LiteralType.String)
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
                return;
            }
        }

        using (Code.Block(this, $"If (\"{@if.Condition}\")"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@if.Condition); }

            using (DebugBlock(@if.Condition))
            { Code.NORMALIZE_BOOL(conditionAddress, v => Stack.GetTemporaryAddress(v, @if.Condition)); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            Code.CommentLine($"Pointer: {Code.Pointer}");

            using (DebugBlock(@if.Keyword))
            {
                Code.JumpStart(conditionAddress);
            }

            using (Code.Block(this, "The if statements"))
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");

            if (@if.NextLink == null)
            {
                // using (this.DebugBlock(@if.Block.BracketEnd))
                // {
                using (Code.Block(this, "Cleanup condition"))
                {
                    Code.ClearValue(conditionAddress);
                    Code.JumpEnd(conditionAddress);
                    Stack.PopVirtual();
                }
                // }
            }
            else
            {
                using (Code.Block(this, "Else"))
                {
                    // using (this.DebugBlock(@if.Block.BracketEnd))
                    // {
                    using (Code.Block(this, "Finish if statement"))
                    {
                        Code.MoveValue(conditionAddress, conditionAddress + 1);
                        Code.JumpEnd(conditionAddress);
                    }
                    Code.MoveValue(conditionAddress + 1, conditionAddress);
                    // }

                    using (DebugBlock(@if.NextLink.Keyword))
                    {
                        // using (Code.CommentBlock(this, $"Invert condition (at {conditionAddress}) result (to {conditionAddress + 1})"))
                        // { Code.LOGIC_NOT(conditionAddress, conditionAddress + 1); }

                        Code.CommentLine($"Pointer: {Code.Pointer}");

                        int elseFlagAddress = conditionAddress + 1;

                        Code.CommentLine($"ELSE flag is at {elseFlagAddress}");

                        using (Code.Block(this, "Set ELSE flag"))
                        { Code.SetValue(elseFlagAddress, 1); }

                        using (Code.Block(this, "If previous \"if\" condition is true"))
                        using (Code.ConditionalBlock(this, conditionAddress))
                        {
                            using (Code.Block(this, "Reset ELSE flag"))
                            { Code.ClearValue(elseFlagAddress); }
                        }

                        Code.MoveValue(elseFlagAddress, conditionAddress);

                        Code.CommentLine($"Pointer: {Code.Pointer}");
                    }

                    using (Code.Block(this, $"If ELSE flag set (previous \"if\" condition is false)"))
                    {
                        using (Code.LoopBlock(this, conditionAddress))
                        {
                            if (@if.NextLink is LinkedElse elseBlock)
                            {
                                using (Code.Block(this, "Block (else)"))
                                { GenerateCodeForStatement(Block.CreateIfNotBlock(elseBlock.Block)); }
                            }
                            else if (@if.NextLink is LinkedIf elseIf)
                            {
                                using (Code.Block(this, "Block (else if)"))
                                { GenerateCodeForStatement(elseIf, true); }
                            }
                            else
                            { throw new UnreachableException(); }

                            using (Code.Block(this, $"Reset ELSE flag"))
                            { Code.ClearValue(conditionAddress); }
                        }
                        Stack.PopVirtual();
                    }

                    Code.CommentLine($"Pointer: {Code.Pointer}");
                }
            }

            if (!linked)
            {
                using (DebugBlock(@if.Semicolon?.Position ?? @if.Block.Semicolon?.Position ?? @if.Block.Position.After(), @if.File))
                {
                    ContinueControlFlowStatements(Returns, "return");
                    ContinueControlFlowStatements(Breaks, "break");
                }
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");
        }
    }
    void GenerateCodeForStatement(WhileLoop @while)
    {
        using (Code.Block(this, $"While (\"{@while.Condition}\")"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@while.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(new Location(@while.Block.Brackets.Start.Position, @while.Block.File), FindControlFlowUsage(@while.Block.Statements));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(Block.CreateIfNotBlock(@while.Block));
                }

                using (Code.Block(this, "Compute condition again"))
                {
                    GenerateCodeForStatement(@while.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                using StackAddress tempAddress = Stack.GetTemporaryAddress(1, @while);
                {
                    if (Returns.Count > 0)
                    {
                        if (Returns.Last.FlagAddress.HasValue)
                        {
                            Code.CopyValue(Returns.Last.FlagAddress.Value, tempAddress);
                            Code.LOGIC_NOT(tempAddress, v => Stack.GetTemporaryAddress(v, @while));
                            using (Code.ConditionalBlock(this, tempAddress))
                            { Code.SetValue(conditionAddress, 0); }
                        }
                    }

                    if (Breaks.Count > 0)
                    {
                        if (!Breaks.Last.FlagAddress.HasValue)
                        { Diagnostics.Add(Diagnostic.Internal($"Unexpected conditional jump in the depths (this is the compiler's fault)", @while)); }
                        else
                        {
                            Code.CopyValue(Breaks.Last.FlagAddress.Value, tempAddress);
                            Code.LOGIC_NOT(tempAddress, v => Stack.GetTemporaryAddress(v, @while));
                            using (Code.ConditionalBlock(this, tempAddress))
                            { Code.SetValue(conditionAddress, 0); }
                        }
                    }
                }
            }

            if (breakBlock is not null)
            { FinishControlFlowStatements(Breaks.Pop(), true, "break"); }

            Stack.Pop(); // Pop Condition

            ContinueControlFlowStatements(Returns, "return");
        }
    }
    void GenerateCodeForStatement(ForLoop @for)
    {
        if (AllowLoopUnrolling)
        {
            CodeSnapshot codeSnapshot = SnapshotCode();
            GeneratorSnapshot genSnapshot = Snapshot();
            int initialCodeLength = codeSnapshot.Code.Length;

            if (IsUnrollable(@for))
            {
                if (GenerateCodeForStatement(@for, true))
                {
                    CodeSnapshot unrolledCode = SnapshotCode();

                    int unrolledLength = unrolledCode.Code.Length - initialCodeLength;
                    GeneratorSnapshot unrolledSnapshot = Snapshot();

                    Restore(genSnapshot);
                    RestoreCode(codeSnapshot);

                    try
                    {
                        GenerateCodeForStatement(@for, false);

                        CodeSnapshot notUnrolledCode = SnapshotCode();
                        int notUnrolledLength = notUnrolledCode.Code.Length - initialCodeLength;
                        GeneratorSnapshot notUnrolledSnapshot = Snapshot();

                        if (unrolledLength <= notUnrolledLength)
                        {
                            Restore(unrolledSnapshot);
                            RestoreCode(unrolledCode);
                        }
                        else
                        {
                            Restore(notUnrolledSnapshot);
                            RestoreCode(notUnrolledCode);
                        }
                        return;
                    }
                    catch (Exception)
                    { }

                    Restore(unrolledSnapshot);
                    RestoreCode(unrolledCode);

                    return;
                }
            }

            Restore(genSnapshot);
            RestoreCode(codeSnapshot);
        }

        GenerateCodeForStatement(@for, false);
    }
    bool GenerateCodeForStatement(ForLoop @for, bool shouldUnroll)
    {
        if (shouldUnroll)
        {
            GeneratorSnapshot generatorSnapshot = Snapshot();
            CodeSnapshot codeSnapshot = SnapshotCode();

            try
            {
                ImmutableArray<Block> unrolled = Unroll(@for, new Dictionary<StatementWithValue, CompiledValue>());

                for (int i = 0; i < unrolled.Length; i++)
                { GenerateCodeForStatement(unrolled[i]); }

                return true;
            }
            catch (Exception)
            {
                Restore(generatorSnapshot);
                RestoreCode(codeSnapshot);
                Debugger.Break();
            }
        }

        using (Code.Block(this, $"For"))
        {
            VariableCleanupStack.Push(PrecompileVariable(@for.VariableDeclaration, false));

            using (Code.Block(this, "Variable Declaration"))
            { GenerateCodeForStatement(@for.VariableDeclaration); }

            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@for.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(new Location(@for.Block.Brackets.Start.Position, @for.Block.File), FindControlFlowUsage(@for.Block.Statements));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(Block.CreateIfNotBlock(@for.Block));
                }

                using (Code.Block(this, "Compute expression"))
                {
                    GenerateCodeForStatement(@for.Expression);
                }

                using (Code.Block(this, "Compute condition again"))
                {
                    GenerateCodeForStatement(@for.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                using StackAddress tempAddress = Stack.GetTemporaryAddress(1, @for);
                {
                    if (Returns.Count > 0)
                    {
                        if (Returns.Last.FlagAddress.HasValue)
                        {
                            Code.CopyValue(Returns.Last.FlagAddress.Value, tempAddress);
                            Code.LOGIC_NOT(tempAddress, v => Stack.GetTemporaryAddress(v, @for));
                            using (Code.ConditionalBlock(this, tempAddress))
                            { Code.SetValue(conditionAddress, 0); }
                        }
                    }

                    if (Breaks.Count > 0)
                    {
                        if (!Breaks.Last.FlagAddress.HasValue)
                        { Diagnostics.Add(Diagnostic.Internal($"Unexpected conditional jump in the depths (this is the compiler's fault)", @for)); }
                        else
                        {
                            Code.CopyValue(Breaks.Last.FlagAddress.Value, tempAddress);
                            Code.LOGIC_NOT(tempAddress, v => Stack.GetTemporaryAddress(v, @for));
                            using (Code.ConditionalBlock(this, tempAddress))
                            { Code.SetValue(conditionAddress, 0); }
                        }
                    }
                }
            }

            if (breakBlock is not null)
            { FinishControlFlowStatements(Breaks.Pop(), true, "break"); }

            Stack.Pop();

            CleanupVariables(VariableCleanupStack.Pop());

            // ContinueReturnStatements();
            // ContinueBreakStatements();
        }

        return false;
    }
    void GenerateCodeForStatement(KeywordCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);
        switch (statement.Identifier.Content)
        {
            case StatementKeywords.Return:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

                if (statement.Arguments.Length is not 0 and not 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0 or 1, passed {statement.Arguments.Length})", statement));
                    return;
                }

                if (statement.Arguments.Length is 1)
                {
                    if (!GetVariable(ReturnVariableName, statement.File, out BrainfuckVariable? returnVariable, out PossibleDiagnostic? notFoundError))
                    {
                        Diagnostics.Add(Diagnostic.Critical(
                            $"Can't return value for some reason :(",
                            statement,
                            notFoundError.ToError(statement)));
                        return;
                    }

                    GenerateCodeForSetter(returnVariable, statement.Arguments[0]);
                }

                if (Returns.Count == 0)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Can't return for some reason :(", statement.Identifier, statement.File));
                    return;
                }

                if (Returns.Last.FlagAddress.HasValue)
                { Code.SetValue(Returns.Last.FlagAddress.Value, 0); }

                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.JumpStart(Stack.NextAddress);

                Returns.Last.PendingJumps.Last++;
                Returns.Last.Doings.Last = true;

                break;
            }

            case StatementKeywords.Break:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

                if (statement.Arguments.Length != 0)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0, passed {statement.Arguments.Length})", statement));
                    return;
                }

                if (Breaks.Count == 0)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Looks like this \"{statement.Identifier}\" statement is not inside a loop", statement.Identifier, statement.File));
                    return;
                }

                if (!Breaks.Last.FlagAddress.HasValue)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Looks like this \"{statement.Identifier}\" statement is not inside a loop", statement.Identifier, statement.File));
                    return;
                }

                Code.SetValue(Breaks.Last.FlagAddress.Value, 0);

                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.JumpStart(Stack.NextAddress);

                Breaks.Last.PendingJumps.Last++;
                Breaks.Last.Doings.Last = true;

                break;
            }

            case StatementKeywords.Delete:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                if (statement.Arguments.Length != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Arguments.Length})", statement));
                    return;
                }

                GenerateDestructor(statement.Arguments[0]);

                break;
            }

            case StatementKeywords.Crash:
            {
                if (statement.Arguments.Length != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Arguments.Length})", statement));
                    return;
                }
                GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed), statement.Arguments[0]);
                GenerateCodeForPrinter(statement.Arguments[0]);
                GenerateCodeForPrinter(Ansi.Reset, statement.Arguments[0]);
                Code.SetPointer(Stack.Push(1));
                Code += "[]";
                Stack.PopVirtual();
                break;
            }

            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown keyword-call \"{statement.Identifier}\"", statement.Identifier));
                return;
        }
    }
    void GenerateCodeForStatement(Assignment statement)
    {
        if (statement.Operator.Content != "=")
        {
            Diagnostics.Add(Diagnostic.Critical($"Unknown assignment operator \"{statement.Operator}\"", statement.Operator, statement.File));
            return;
        }

        GenerateCodeForSetter(statement.Left, statement.Right);
    }
    void GenerateCodeForStatement(CompoundAssignment statement)
    {
        {
            BinaryOperatorCall @operator = statement.GetOperatorCall();
            if (GetOperator(@operator, @operator.File, out _, out _))
            {
                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
        }

        using DebugInfoBlock debugBlock = DebugBlock(statement);

        switch (statement.Operator.Content)
        {
            case "+=":
            {
                if (!GetVariable(statement.Left, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
                {
                    GenerateCodeForStatement(statement.ToAssignment());
                    return;
                }

                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement.Left));
                    return;
                }

                if (variable.Size != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Bruh", statement.Left));
                    return;
                }

                if (statement.Right == null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Value is required for \"{statement.Operator}\" assignment", statement));
                    return;
                }

                if (!variable.IsInitialized)
                { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement.Left)); }

                if (AllowPrecomputing && TryCompute(statement.Right, out CompiledValue constantValue))
                {
                    if (constantValue.TryCast(variable.Type, out CompiledValue castedValue))
                    { constantValue = castedValue; }

                    if (!variable.Type.SameAs(constantValue.Type))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Variable and value type mismatch (\"{variable.Type}\" != \"{constantValue.Type}\")", statement.Right));
                        return;
                    }

                    Code.AddValue(variable.Address, constantValue);

                    Precomputations++;
                    return;
                }

                using (Code.Block(this, $"Add \"{statement.Right}\" to variable \"{variable.Name}\" (at {variable.Address})"))
                {
                    using (Code.Block(this, $"Compute value"))
                    {
                        GenerateCodeForStatement(statement.Right);
                    }

                    using (Code.Block(this, $"Set computed value to {variable.Address}"))
                    {
                        Stack.Pop(address => Code.MoveAddValue(address, variable.Address));
                    }
                }

                return;
            }
            case "-=":
            {
                if (!GetVariable(statement.Left, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
                {
                    GenerateCodeForStatement(statement.ToAssignment());
                    return;
                }

                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement.Left));
                    return;
                }

                if (variable.Size != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Bruh", statement.Left));
                    return;
                }

                if (statement.Right == null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Value is required for \"{statement.Operator}\" assignment", statement));
                    return;
                }

                if (!variable.IsInitialized)
                { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement.Left)); }

                if (AllowPrecomputing && TryCompute(statement.Right, out CompiledValue constantValue))
                {
                    if (constantValue.TryCast(variable.Type, out CompiledValue castedValue))
                    { constantValue = castedValue; }

                    if (!variable.Type.SameAs(constantValue.Type))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Variable and value type mismatch (\"{variable.Type}\" != \"{constantValue.Type}\")", statement.Right));
                        return;
                    }

                    Code.AddValue(variable.Address, -constantValue);

                    Precomputations++;
                    return;
                }

                using (Code.Block(this, $"Add \"{statement.Right}\" to variable \"{variable.Name}\" (at {variable.Address})"))
                {
                    using (Code.Block(this, $"Compute value"))
                    {
                        GenerateCodeForStatement(statement.Right);
                    }

                    using (Code.Block(this, $"Set computed value to {variable.Address}"))
                    {
                        Stack.Pop(address => Code.MoveSubValue(address, variable.Address));
                    }
                }

                return;
            }
            default:
                GenerateCodeForStatement(statement.ToAssignment());
                break;
        }
    }
    void GenerateCodeForStatement(ShortOperatorCall statement)
    {
        {
            BinaryOperatorCall @operator = statement.GetOperatorCall();
            if (GetOperator(@operator, @operator.File, out _, out _))
            {
                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
        }

        if (!AllowOtherOptimizations)
        {
            GenerateCodeForStatement(statement.ToAssignment());
            return;
        }

        using DebugInfoBlock debugBlock = DebugBlock(statement);
        switch (statement.Operator.Content)
        {
            case "++":
            {
                if (!GetVariable(statement.Left, out BrainfuckVariable? variable, out _))
                {
                    GenerateCodeForStatement(statement.ToAssignment());
                    return;
                }

                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement.Left));
                    return;
                }

                if (variable.Size != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Bruh", statement.Left));
                    return;
                }

                if (!variable.IsInitialized)
                { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement.Left)); }

                using (Code.Block(this, $"Increment variable \"{variable.Name}\" (at {variable.Address})"))
                {
                    Code.AddValue(variable.Address, 1);
                }

                Optimizations++;
                return;
            }
            case "--":
            {
                if (!GetVariable(statement.Left, out BrainfuckVariable? variable, out _))
                {
                    GenerateCodeForStatement(statement.ToAssignment());
                    return;
                }

                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement.Left));
                    return;
                }

                if (variable.Size != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Bruh", statement.Left));
                    return;
                }

                if (!variable.IsInitialized)
                { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement.Left)); }

                using (Code.Block(this, $"Decrement variable {variable.Name} (at {variable.Address})"))
                {
                    Code.AddValue(variable.Address, -1);
                }

                Optimizations++;
                return;
            }
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown assignment operator \"{statement.Operator}\"", statement.Operator, statement.File));
                return;
        }
    }
    void GenerateCodeForStatement(VariableDeclaration statement)
    {
        if (statement.InitialValue == null) return;

        if (statement.Modifiers.Contains(ModifierKeywords.Const))
        { return; }

        if (!GetVariable(statement.Identifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(statement.Identifier, statement.File));
            return;
        }

        if (variable.IsInitialized)
        { return; }

        GenerateCodeForSetter(variable, statement.InitialValue);
    }
    void GenerateCodeForStatement(FunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (functionCall.Identifier.Content == "sizeof")
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (functionCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"sizeof\": required {1} passed {functionCall.Arguments.Length}", functionCall));
                return;
            }

            StatementWithValue param = functionCall.Arguments[0];
            GeneralType paramType;
            if (param is TypeStatement typeStatement)
            { paramType = GeneralType.From(typeStatement.Type, FindType, TryCompute); }
            else if (param is CompiledTypeStatement compiledTypeStatement)
            { paramType = compiledTypeStatement.Type; }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(functionCall, SizeofStatementType);

            if (FindSize(paramType, out int size, out PossibleDiagnostic? findSizeError))
            {
                if (functionCall.SaveValue)
                { Stack.Push(size); }
            }
            else
            {
                StackAddress sizeAddress = Stack.Push(0);

                if (!GenerateSize(paramType, sizeAddress, out PossibleDiagnostic? generateSizeError))
                { Diagnostics.Add(generateSizeError.ToError(param)); }

                if (!functionCall.SaveValue)
                { Stack.Pop(); }
            }

            return;
        }

        if (!GetFunction(functionCall, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFound))
        {
            Diagnostics.Add(notFound.ToError(functionCall.Identifier, functionCall.File));
            return;
        }

        (CompiledFunction? compiledFunction, Dictionary<string, GeneralType>? typeArguments) = result;

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

        if (!compiledFunction.CanUse(functionCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{compiledFunction.ToReadable()}\" cannot be called due to its protection level", functionCall.Identifier));
            return;
        }

        GenerateCodeForFunction(compiledFunction, functionCall.MethodArguments, typeArguments, functionCall);

        if (!functionCall.SaveValue && compiledFunction.ReturnSomething)
        { Stack.Pop(); }
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(constructorCall);

        GeneralType instanceType = GeneralType.From(constructorCall.Type, FindType, TryCompute, constructorCall.File);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Arguments);

        if (instanceType.Is(out StructType? structType))
        { structType.Struct?.References.AddReference(constructorCall.Type, constructorCall.File); }

        if (!GetConstructor(instanceType, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructor>? result, out PossibleDiagnostic? notFound))
        {
            Diagnostics.Add(notFound.ToError(constructorCall.Keyword, constructorCall.File));
            return;
        }

        (CompiledConstructor? constructor, Dictionary<string, GeneralType>? typeArguments) = result;

        typeArguments ??= new Dictionary<string, GeneralType>();

        constructor.References.AddReference(constructorCall);
        OnGotStatementType(constructorCall, constructor.Type);

        if (!constructor.CanUse(constructorCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Constructor \"{constructor.ToReadable()}\" could not be called due to its protection level", constructorCall));
            return;
        }

        GenerateCodeForFunction(constructor, constructorCall.Arguments, typeArguments, constructorCall);
    }
    void GenerateCodeForStatement(Literal statement, GeneralType? expectedType = null)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Set \"{statement}\" to address {Stack.NextAddress}"))
        {
            switch (statement.Type)
            {
                case LiteralType.Integer:
                {
                    int value = statement.GetInt();
                    Stack.Push(value);
                    break;
                }
                case LiteralType.Char:
                {
                    Stack.Push(statement.Value[0]);
                    break;
                }

                case LiteralType.Float:
                    throw new NotSupportedException($"Floats not supported by the brainfuck compiler", statement);
                case LiteralType.String:
                {
                    if (expectedType is not null &&
                        expectedType.Is(out PointerType? pointerType) &&
                        pointerType.To.SameAs(BasicType.Char))
                    {
                        // TODO: not true but false
                        GenerateCodeForLiteralString(statement, true);
                    }
                    else
                    {
                        GenerateCodeForLiteralString(statement, true);
                    }
                    break;
                }

                default:
                    Diagnostics.Add(Diagnostic.Critical($"Unknown literal type \"{statement.Type}\"", statement));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(Identifier statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (GetVariable(statement, out BrainfuckVariable? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            if (variable.IsReference)
            { Diagnostics.Add(Diagnostic.Critical($"Can't get the value of variable \"{variable.Name}\" directly because its contains a stack pointer", statement)); }

            GenerateCodeForStatement(variable, statement);
            return;
        }

        if (GetConstant(statement.Content, statement.File, out IConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            using (Code.Block(this, $"Load constant \"{statement.Content}\" (with value {constant.Value})"))
            {
                Stack.Push(constant.Value);
            }

            return;
        }

        if (GetFunction(FunctionQuery.Create<CompiledFunction>(statement.Content), out _, out PossibleDiagnostic? functionNotFoundError))
        { throw new NotSupportedException($"Function pointers not supported by brainfuck", statement); }

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{statement}\" not found",
            statement,
            variableNotFoundError.ToError(statement),
            constantNotFoundError.ToError(statement),
            functionNotFoundError.ToError(statement)));
        return;
    }
    void GenerateCodeForStatement(BrainfuckVariable variable, ILocated statement)
    {
        if (variable.IsDiscarded)
        {
            Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", statement));
            return;
        }

        if (!variable.IsInitialized)
        { Diagnostics.Add(Diagnostic.Warning($"Variable \"{variable.Name}\" is not initialized", statement)); }

        int variableSize = variable.Size;

        if (variableSize <= 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't load variable \"{variable.Name}\" because it's size is {variableSize} (bruh)", statement));
            return;
        }

        int loadTarget = Stack.PushVirtual(variableSize, statement);

        using (Code.Block(this, $"Load variable \"{variable.Name}\" (from {variable.Address}) to {loadTarget}"))
        {
            for (int offset = 0; offset < variableSize; offset++)
            {
                int offsettedSource = variable.Address + offset;
                int offsettedTarget = loadTarget + offset;

                if (AllowOtherOptimizations &&
                    VariableCanBeDiscarded != null &&
                    VariableCanBeDiscarded == variable.Name)
                {
                    Code.MoveValue(offsettedSource, offsettedTarget);
                    DiscardVariable(CompiledVariables, variable.Name);
                    Optimizations++;
                }
                else
                {
                    Code.CopyValue(offsettedSource, offsettedTarget);
                }
            }
        }
    }
    void GenerateCodeForStatement(BinaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (GetOperator(statement, statement.File, out FunctionQueryResult<CompiledOperator>? operatorQueryResult, out PossibleDiagnostic? operatorNotFoundError))
        {
            (CompiledOperator? compiledOperator, Dictionary<string, GeneralType>? typeArguments) = operatorQueryResult;

            statement.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

            if (!compiledOperator.CanUse(statement.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Function \"{compiledOperator.ToReadable()}\" cannot be called due to its protection level", statement.Operator, statement.File));
                return;
            }

            GenerateCodeForFunction(compiledOperator, statement.Arguments, typeArguments, statement);

            if (!statement.SaveValue)
            { Stack.Pop(); }
            return;
        }

        if (AllowPrecomputing && TryCompute(statement, out CompiledValue computed))
        {
            Stack.Push(computed);
            Precomputations++;
            return;
        }

        using (Code.Block(this, $"Expression \"{statement.Left}\" \"{statement.Operator}\" \"{statement.Right}\""))
        {
            switch (statement.Operator.Content)
            {
                case BinaryOperatorCall.CompEQ:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, "Compute equality"))
                    { Code.LOGIC_EQ(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.Addition:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Move & add right-side (from {rightAddress}) to left-side (to {leftAddress})"))
                    { Code.MoveAddValue(rightAddress, leftAddress); }

                    Stack.PopVirtual();

                    break;
                }
                case BinaryOperatorCall.Subtraction:
                {
                    {
                        if (AllowOtherOptimizations &&
                            GetVariable(statement.Left, out BrainfuckVariable? left, out _) &&
                            !left.IsDiscarded &&
                            TryCompute(statement.Right, out CompiledValue right) &&
                            right.Type == RuntimeType.U8)
                        {
                            if (!left.IsInitialized)
                            { Diagnostics.Add(Diagnostic.Warning($"Variable \"{left.Name}\" is not initialized", statement.Left)); }

                            int resultAddress = Stack.PushVirtual(1, statement);

                            Code.CopyValue(left.Address, resultAddress, Stack.NextAddress);

                            Code.AddValue(resultAddress, -right.U8);

                            Optimizations++;

                            return;
                        }
                    }

                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Move & sub right-side (from {rightAddress}) from left-side (to {leftAddress})"))
                    { Code.MoveSubValue(rightAddress, leftAddress); }

                    Stack.PopVirtual();

                    return;
                }
                case BinaryOperatorCall.Multiplication:
                {
                    {
                        if (AllowOtherOptimizations &&
                            statement.Left is Identifier identifier1 &&
                            statement.Right is Identifier identifier2 &&
                            string.Equals(identifier1.Content, identifier2.Content))
                        {
                            int leftAddress_ = Stack.NextAddress;
                            using (Code.Block(this, "Compute left-side value (right-side is the same)"))
                            { GenerateCodeForStatement(statement.Left); }

                            using (Code.Block(this, $"Snippet MATH_MUL_SELF({leftAddress_})"))
                            {
                                Code.MATH_MUL_SELF(leftAddress_, v => Stack.GetTemporaryAddress(v, statement));
                                Optimizations++;
                                break;
                            }
                        }
                    }

                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet MULTIPLY({leftAddress} {rightAddress})"))
                    { Code.MULTIPLY(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.Division:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet DIVIDE({leftAddress} {rightAddress})"))
                    { Code.MATH_DIV(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.Modulo:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet MOD({leftAddress} {rightAddress})"))
                    { Code.MATH_MOD(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.CompLT:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet LT({leftAddress} {rightAddress})"))
                    { Code.LOGIC_LT(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.CompGT:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (StackAddress resultAddress = Stack.PushVirtual(1, statement))
                    {
                        using (Code.Block(this, $"Snippet MT({leftAddress} {rightAddress})"))
                        { Code.LOGIC_MT(leftAddress, rightAddress, resultAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                        Code.MoveValue(resultAddress, leftAddress);
                    }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.CompGEQ:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet: *{leftAddress} <= *{rightAddress}"))
                    {
                        Code.LOGIC_LT(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement));
                        Stack.Pop();
                        Code.LOGIC_NOT(leftAddress, v => Stack.GetTemporaryAddress(v, statement));
                    }

                    break;
                }
                case BinaryOperatorCall.CompLEQ:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet: *{leftAddress} <= *{rightAddress}"))
                    { Code.LOGIC_LTEQ(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.CompNEQ:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet NEQ({leftAddress} {rightAddress})"))
                    { Code.LOGIC_NEQ(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                    Stack.Pop();

                    break;
                }
                case BinaryOperatorCall.LogicalAND:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int tempLeftAddress = Stack.PushVirtual(1, statement);
                    Code.CopyValue(leftAddress, tempLeftAddress);

                    using (Code.ConditionalBlock(this, tempLeftAddress))
                    {
                        int rightAddress = Stack.NextAddress;
                        using (Code.Block(this, "Compute right-side value"))
                        { GenerateCodeForStatement(statement.Right); }

                        using (Code.Block(this, $"Snippet AND({leftAddress} {rightAddress})"))
                        { Code.LOGIC_AND(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                        Stack.Pop(); // Pop rightAddress
                    }

                    Stack.PopVirtual(); // Pop tempLeftAddress

                    break;
                }
                case BinaryOperatorCall.LogicalOR:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int invertedLeftAddress = Stack.PushVirtual(1, statement);
                    Code.CopyValue(leftAddress, invertedLeftAddress);
                    Code.LOGIC_NOT(invertedLeftAddress, v => Stack.GetTemporaryAddress(v, statement));

                    using (Code.ConditionalBlock(this, invertedLeftAddress))
                    {
                        int rightAddress = Stack.NextAddress;
                        using (Code.Block(this, "Compute right-side value"))
                        { GenerateCodeForStatement(statement.Right); }

                        using (Code.Block(this, $"Snippet AND({leftAddress} {rightAddress})"))
                        { Code.LOGIC_OR(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                        Stack.Pop(); // Pop rightAddress
                    }

                    Stack.PopVirtual(); // Pop tempLeftAddress

                    break;
                }
                case BinaryOperatorCall.BitshiftLeft:
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (!TryCompute(statement.Right, out CompiledValue offsetConst))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, statement.File));
                        return;
                    }

                    if (offsetConst != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.I32))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, statement.File));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.I32));

                        using (Code.Block(this, $"Snippet MULTIPLY({valueAddress} {offsetAddress})"))
                        { Code.MULTIPLY(valueAddress, offsetAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                    }

                    break;
                }
                case BinaryOperatorCall.BitshiftRight:
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (!TryCompute(statement.Right, out CompiledValue offsetConst))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, statement.File));
                        return;
                    }

                    if (offsetConst != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.I32))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, statement.File));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.I32));

                        using (Code.Block(this, $"Snippet MATH_DIV({valueAddress} {offsetAddress})"))
                        { Code.MATH_DIV(valueAddress, offsetAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                    }

                    break;
                }
                case BinaryOperatorCall.BitwiseAND:
                {
                    GeneralType leftType = FindStatementType(statement.Left);

                    if ((leftType.SameAs(BasicType.U8) ||
                        leftType.SameAs(BasicType.I8) ||
                        leftType.SameAs(BasicType.Char) ||
                        leftType.SameAs(BasicType.I16) ||
                        leftType.SameAs(BasicType.U32) ||
                        leftType.SameAs(BasicType.I32)) &&
                        TryCompute(statement.Right, out CompiledValue right))
                    {
                        if (right == 1)
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block(this, "Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            using StackAddress rightAddress = Stack.Push(2);

                            using (Code.Block(this, $"Snippet MOD({leftAddress} {rightAddress})"))
                            { Code.MATH_MOD(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                            break;
                        }

                        if (right == 0b_1_0000000)
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block(this, "Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            using (StackAddress rightAddress = Stack.Push(0b_0_1111111))
                            {
                                using (Code.Block(this, $"Snippet MT({leftAddress} {rightAddress})"))
                                { Code.LOGIC_MT(leftAddress, rightAddress, leftAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                            }

                            using (Code.ConditionalBlock(this, leftAddress))
                            {
                                Code.SetValue(leftAddress, 0b_1_0000000);
                            }

                            break;
                        }
                    }

                    Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, statement.File));
                    return;
                }
                default:
                    Diagnostics.Add(Diagnostic.Critical(
                        $"I can't make \"{statement.Operator}\" operators to work in brainfuck",
                        statement.Operator,
                        statement.File,
                        operatorNotFoundError.ToError(statement)));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(UnaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (GetOperator(statement, statement.File, out FunctionQueryResult<CompiledOperator>? operatorQueryResult, out PossibleDiagnostic? operatorNotFoundError))
        {
            (CompiledOperator? compiledOperator, Dictionary<string, GeneralType>? typeArguments) = operatorQueryResult;

            statement.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

            if (!compiledOperator.CanUse(statement.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Function \"{compiledOperator.ToReadable()}\" cannot be called due to its protection level", statement.Operator, statement.File));
                return;
            }

            GenerateCodeForFunction(compiledOperator, statement.Arguments, typeArguments, statement);

            if (!statement.SaveValue)
            { Stack.Pop(); }
            return;
        }

        if (AllowPrecomputing && TryCompute(statement, out CompiledValue computed))
        {
            Stack.Push(computed);
            Precomputations++;
            return;
        }

        using (Code.Block(this, $"Expression \"{statement.Left}\" \"{statement.Operator}\""))
        {
            switch (statement.Operator.Content)
            {
                case UnaryOperatorCall.LogicalNOT:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    Code.LOGIC_NOT(leftAddress, v => Stack.GetTemporaryAddress(v, statement));

                    break;
                }
                default:
                    Diagnostics.Add(Diagnostic.Critical(
                        $"I can't make \"{statement.Operator}\" operators to work in brainfuck",
                        statement.Operator,
                        statement.File,
                        operatorNotFoundError.ToError(statement)));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(Block block)
    {
        using ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress);

        using (DebugBlock(block.Brackets.Start, block.File))
        {
            VariableCleanupStack.Push(PrecompileVariables(block, false));

            if (Returns.Count > 0)
            {
                Returns.Last.PendingJumps.Push(0);
                Returns.Last.Doings.Push(false);
            }

            if (Breaks.Count > 0)
            {
                Breaks.Last.PendingJumps.Push(0);
                Breaks.Last.Doings.Push(false);
            }
        }

        int branchDepth = Code.BranchDepth;
        for (int i = 0; i < block.Statements.Length; i++)
        {
            if (Returns.Count > 0 && Returns.Last.Doings.Last)
            { break; }

            if (Breaks.Count > 0 && Breaks.Last.Doings.Last)
            { break; }

            progressBar.Print(i, block.Statements.Length);
            VariableCanBeDiscarded = null;
            GenerateCodeForStatement(block.Statements[i]);
            VariableCanBeDiscarded = null;
        }

        using (DebugBlock(block.Brackets.End, block.File))
        {
            if (Returns.Count > 0)
            { FinishControlFlowStatements(Returns.Last, false, "return"); }

            if (Breaks.Count > 0)
            { FinishControlFlowStatements(Breaks.Last, false, "break"); }

            CleanupVariables(VariableCleanupStack.Pop());
        }
        if (branchDepth != Code.BranchDepth)
        { Diagnostics.Add(Diagnostic.Internal($"Unbalanced branches", block)); }
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        Diagnostics.Add(Diagnostic.Critical($"This is when pointers to the stack isn't work in brainfuck", addressGetter));
        return;
    }
    void GenerateCodeForStatement(Pointer pointer)
    {
        using DebugInfoBlock debugBlock = DebugBlock(pointer);

        if (pointer.PrevStatement is Identifier variableIdentifier)
        {
            if (!GetVariable(variableIdentifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableIdentifier));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", variableIdentifier));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForStatement(variable, pointer.PrevStatement);
                return;
            }
        }

        if (TryCompute(pointer, out CompiledValue computed))
        {
            Stack.Push(computed);
            return;
        }

        int pointerAddress = Stack.NextAddress;
        GenerateCodeForStatement(pointer.PrevStatement);

        Heap.Get(pointerAddress, pointerAddress);
    }
    void GenerateCodeForStatement(NewInstance newInstance)
    {
        using DebugInfoBlock debugBlock = DebugBlock(newInstance);

        GeneralType instanceType = GeneralType.From(newInstance.Type, FindType, TryCompute);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                int pointerAddress = Stack.NextAddress;

                GenerateAllocator(new AnyCall(
                    new Identifier(Token.CreateAnonymous("sizeof"), newInstance.File),
                    new CompiledTypeStatement[] { new(Token.CreateAnonymous(StatementKeywords.Type), pointerType.To, newInstance.File) },
                    Array.Empty<Token>(),
                    TokenPair.CreateAnonymous(Position.UnknownPosition, "(", ")"),
                    newInstance.File));

                // GenerateAllocator(pointerType.To.Size, newInstance);

                // int temp = Stack.PushVirtual(1);
                // Code.CopyValue(pointerAddress, temp, temp + 1);
                // 
                // for (int i = 0; i < pointerType.To.Size; i++)
                // {
                //     Heap.Set(temp, 0);
                //     Code.AddValue(temp, 1);
                // }
                // 
                // Stack.Pop();

                if (!newInstance.SaveValue)
                { Stack.Pop(); }
                break;
            }

            case StructType structType:
            {
                structType.Struct.References.AddReference(newInstance.Type, newInstance.File);

                int address = Stack.PushVirtual(structType.GetSize(this, Diagnostics, newInstance), newInstance);

                int structSize = structType.GetSize(this);

                for (int offset = 0; offset < structSize; offset++)
                {
                    int offsettedAddress = address + offset;
                    Code.SetValue(offsettedAddress, 0);
                }

                break;
            }

            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown type \"{instanceType}\"", newInstance.Type, newInstance.File));
                return;
        }
    }
    void GenerateCodeForStatement(Field field)
    {
        using DebugInfoBlock debugBlock = DebugBlock(field);

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType.Is(out ArrayType? arrayType) && field.Identifier.Content == "Length")
        {
            if (!arrayType.ComputedLength.HasValue)
            {
                Diagnostics.Add(Diagnostic.Critical($"I will eventually implement this", field.Identifier, field.File));
                return;
            }

            Stack.Push(arrayType.ComputedLength.Value);
            return;
        }

        if (TryCompute(field, out CompiledValue computed))
        {
            Stack.Push(computed);
            return;
        }

        if (GetVariable(field.PrevStatement, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference &&
            TryGetAddress(field, out Address? address, out int size))
        {
            if (size <= 0)
            {
                Diagnostics.Add(Diagnostic.Critical($"Can't load field \"{field}\" because it's size is {size} (bruh)", field));
                return;
            }

            using (Code.Block(this, $"Load field \"{field}\" (from {address})"))
            {
                int loadTarget = Stack.PushVirtual(size, field);

                for (int offset = 0; offset < size; offset++)
                {
                    Address offsettedSource = address + offset;
                    Address offsettedTarget = new AddressAbsolute(loadTarget) + offset;

                    if (offsettedSource is not AddressAbsolute offsettedSourceAbs)
                    { throw new NotImplementedException(); }

                    if (offsettedTarget is not AddressAbsolute offsettedTargetAbs)
                    { throw new NotImplementedException(); }

                    Code.CopyValue(offsettedSourceAbs.Value, offsettedTargetAbs.Value);
                }
            }

            return;
        }

        if (prevType.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", field.PrevStatement));
                return;
            }

            if (!structPointerType.GetField(field.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field.Identifier, field.File));
                return;
            }

            field.Reference = fieldDefinition;
            field.CompiledType = fieldDefinition.Type;

            int resultAddress = Stack.Push(fieldDefinition.Type.GetSize(this, Diagnostics, fieldDefinition));

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(field.PrevStatement);

            Code.AddValue(pointerAddress, fieldOffset);

            Heap.Get(pointerAddress, resultAddress, fieldDefinition.Type.GetSize(this, Diagnostics, fieldDefinition));

            Stack.Pop();

            return;
        }

        if (TryGetAddress(field, out address, out size))
        {
            PushFrom(address, size);
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Failed to get field memory address", field));
    }
    void GenerateCodeForStatement(BasicTypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.SameAs(targetType))
        { Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Keyword, typeCast.File)); }

        if (statementType.GetSize(this, Diagnostics, typeCast.PrevStatement) != targetType.GetSize(this, Diagnostics, typeCast))
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't convert \"{statementType}\" ({statementType.GetSize(this, Diagnostics, typeCast.PrevStatement)} bytes) to \"{targetType}\" ({targetType.GetSize(this, Diagnostics, typeCast.PrevStatement)} bytes)", typeCast.Keyword, typeCast.File));
            return;
        }

        GenerateCodeForStatement(typeCast.PrevStatement);
    }
    void GenerateCodeForStatement(ManagedTypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.GetSize(this, Diagnostics, typeCast.PrevStatement) != targetType.GetSize(this, Diagnostics, typeCast))
        {
            Diagnostics.Add(Diagnostic.Critical($"Type-cast is not supported at the moment", typeCast));
            return;
        }

        GenerateCodeForStatement(typeCast.PrevStatement);
    }
    #endregion

    void PushFrom(Address address, int size)
    {
        switch (address)
        {
            case AddressAbsolute v: PushFrom(v, size); break;
            case AddressOffset v: PushFrom(v, size); break;
            default: throw new NotImplementedException();
        }
    }

    void PushFrom(AddressAbsolute address, int size)
    {
        using (Code.Block(this, $"Load daata (from {address})"))
        {
            int loadTarget = Stack.PushVirtual(size);

            for (int offset = 0; offset < size; offset++)
            {
                Address offsettedSource = address + offset;
                Address offsettedTarget = new AddressAbsolute(loadTarget) + offset;

                if (offsettedSource is not AddressAbsolute offsettedSourceAbs)
                { throw new NotImplementedException(); }

                if (offsettedTarget is not AddressAbsolute offsettedTargetAbs)
                { throw new NotImplementedException(); }

                Code.CopyValue(offsettedSourceAbs.Value, offsettedTargetAbs.Value);
            }
        }
    }

    void PushFrom(AddressOffset address, int size)
    {
        if (address.Base is AddressRuntimePointer runtimePointer)
        {
            using (Code.Block(this, $"Load data (dereferenced from \"{runtimePointer.PointerValue}\" + {address.Offset})"))
            {
                int pointerAddress = Stack.NextAddress;
                GenerateCodeForStatement(runtimePointer.PointerValue);
                Code.AddValue(pointerAddress, address.Offset);
                Heap.Get(pointerAddress, pointerAddress);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    #region GenerateCodeForPrinter()

    void GenerateCodeForPrinter(StatementWithValue value)
    {
        if (TryCompute(value, out CompiledValue constantToPrint))
        {
            GenerateCodeForPrinter(constantToPrint);
            return;
        }

        if (value is Literal literal &&
            literal.Type == LiteralType.String)
        {
            GenerateCodeForPrinter(literal.Value, literal);
            return;
        }

        GeneralType valueType = FindStatementType(value);
        GenerateCodeForValuePrinter(value, valueType);
    }
    void GenerateCodeForPrinter(CompiledValue value)
    {
        int tempAddress = Stack.NextAddress;
        using (Code.Block(this, $"Print value {value} (on address {tempAddress})"))
        {
            Code.SetValue(tempAddress, value);
            Code.SetPointer(tempAddress);
            Code += '.';
            Code.ClearValue(tempAddress);
            Code.SetPointer(0);
        }
    }
    void GenerateCodeForPrinter(string value, ILocated location)
    {
        using (Code.Block(this, $"Print string value \"{value}\""))
        {
            int address = Stack.NextAddress;

            Code.ClearValue(address);

            byte prevValue = 0;
            for (int i = 0; i < value.Length; i++)
            {
                using DebugInfoBlock debugBlock = DebugBlock(location);

                Code.SetPointer(address);
                byte charToPrint = CharCode.GetByte(value[i]);

                while (prevValue > charToPrint)
                {
                    Code += '-';
                    prevValue--;
                }

                while (prevValue < charToPrint)
                {
                    Code += '+';
                    prevValue++;
                }

                prevValue = charToPrint;

                Code += '.';
            }

            Code.ClearValue(address);
            Code.SetPointer(0);
        }
    }
    void GenerateCodeForValuePrinter(StatementWithValue value, GeneralType valueType)
    {
        if (value is Literal literalValue &&
            literalValue.Type == LiteralType.String)
        {
            GenerateCodeForPrinter(literalValue.Value, literalValue);
            return;
        }

        if (valueType.GetSize(this, Diagnostics, value) != 1)
        { throw new NotSupportedException($"Only value of size 1 (not {valueType.GetSize(this, Diagnostics, value)}) supported by the output printer in brainfuck", value); }

        if (!valueType.Is<BuiltinType>())
        { throw new NotSupportedException($"Only built-in types or string literals (not \"{valueType}\") supported by the output printer in brainfuck", value); }

        using (Code.Block(this, $"Print value \"{value}\" as text"))
        {
            int address = Stack.NextAddress;

            using (Code.Block(this, $"Compute value"))
            { GenerateCodeForStatement(value); }

            Code.CommentLine($"Computed value is on {address}");

            Code.SetPointer(address);

            Code += '.';

            using (Code.Block(this, $"Clear address {address}"))
            { Code.ClearValue(address); }

            Stack.PopVirtual();

            Code.SetPointer(0);
        }
    }

    bool CanGenerateCodeForPrinter(StatementWithValue value)
    {
        if (TryCompute(value, out _)) return true;

        if (value is Literal literal && literal.Type == LiteralType.String) return true;

        GeneralType valueType = FindStatementType(value);
        return CanGenerateCodeForValuePrinter(valueType);
    }
    bool CanGenerateCodeForValuePrinter(GeneralType valueType) =>
        valueType.GetSize(this) == 1 &&
        valueType.Is<BuiltinType>();

    /*
    void CompileRawPrinter(StatementWithValue value)
    {
        if (TryCompute(value, null, out DataItem constantValue))
        {
            CompileRawPrinter(constantValue);
            return;
        }

        if (value is Identifier identifier && Variables.TryFind(identifier.Content, out Variable variable))
        {
            CompileRawPrinter(variable, identifier);
            return;
        }

        CompileRawValuePrinter(value);
    }
    void CompileRawPrinter(DataItem value)
    {
        if (value.Type == RuntimeType.BYTE)
        {
            using (Code.CommentBlock(this, $"Print value {value.ValueByte}"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.AddValue(value.ValueByte);

                Code += '.';

                Code.ClearCurrent();
                Code.SetPointer(0);
            }
            return;
        }

        if (value.Type == RuntimeType.INT)
        {
            using (Code.CommentBlock(this, $"Print value {value.ValueInt}"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.AddValue(value.ValueInt);

                Code += '.';

                Code.ClearCurrent();
                Code.SetPointer(0);
            }
            return;
        }

        if (value.Type == RuntimeType.CHAR)
        {
            using (Code.CommentBlock(this, $"Print value '{value.ValueChar}'"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.AddValue(value.ValueChar);

                Code += '.';

                Code.ClearCurrent();
                Code.SetPointer(0);
            }
            return;
        }

        throw new NotImplementedException($"Unimplemented constant value type \"{value.Type}\"");
    }
    void CompileRawPrinter(Variable variable, IThingWithPosition symbolPosition)
    {
        if (variable.IsDiscarded)
        { Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Name}\" is discarded", symbolPosition, CurrentFile); }

        using (Code.CommentBlock(this, $"Print variable (\"{variable.Name}\") (from {variable.Address}) value"))
        {
            int size = variable.Size;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedAddress = variable.Address + offset;
                Code.SetPointer(offsettedAddress);
                Code += '.';
            }
            Code.SetPointer(0);
        }
    }
    void CompileRawValuePrinter(StatementWithValue value)
    {
        using (Code.CommentBlock(this, $"Print {value} as raw"))
        {
            using (Code.CommentBlock(this, $"Compute value"))
            { Compile(value); }

            using (Code.CommentBlock(this, $"Print computed value"))
            {
                Stack.Pop(address =>
                {
                    Code.SetPointer(address);
                    Code += '.';
                    Code.ClearCurrent();
                });
                Code.SetPointer(0);
            }
        }
    }
    */

    #endregion

    int GenerateCodeForLiteralString(Literal literal, bool withBytes)
        => GenerateCodeForLiteralString(literal.Value, literal, literal.File, withBytes);
    int GenerateCodeForLiteralString(string literal, IPositioned position, Uri file, bool withBytes)
    {
        if (!withBytes)
        { throw new NotImplementedException(); }

        using DebugInfoBlock debugBlock = DebugBlock(position, file);

        using (Code.Block(this, $"Create String \"{literal}\""))
        {
            int pointerAddress = Stack.NextAddress;
            using (Code.Block(this, "Allocate String object {"))
            { GenerateAllocator(1 + literal.Length, position, file); }

            using (Code.Block(this, "Set string data {"))
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    // Prepare value
                    int valueAddress = Stack.Push((byte)literal[i]);
                    int pointerAddressCopy = valueAddress + 1;

                    // Calculate pointer
                    Code.CopyValue(pointerAddress, pointerAddressCopy);
                    Code.AddValue(pointerAddressCopy, i);

                    // Set value
                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }

                {
                    // Prepare value
                    int valueAddress = Stack.Push((byte)'\0');
                    int pointerAddressCopy = valueAddress + 1;

                    // Calculate pointer
                    Code.CopyValue(pointerAddress, pointerAddressCopy);
                    Code.AddValue(pointerAddressCopy, literal.Length);

                    // Set value
                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }
            }
            return pointerAddress;
        }
    }

    bool IsFunctionInlineable(FunctionThingDefinition function, IEnumerable<StatementWithValue> parameters)
    {
        if (function.Block is null ||
            !function.IsInlineable)
        { return false; }

        foreach (StatementWithValue parameter in parameters)
        {
            if (TryCompute(parameter, out _))
            { continue; }
            if (parameter is Literal)
            { continue; }
            return false;
        }

        return true;
    }

    void GenerateCodeForFunction(CompiledFunction function, ImmutableArray<StatementWithValue> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated caller)
    {
        if (AllowEvaluating &&
            TryEvaluate(function, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements))
        {
            foreach (RuntimeStatement runtimeStatement in runtimeStatements)
            {
                GenerateCodeForStatement(runtimeStatement);
            }

            if (returnValue.HasValue &&
                caller is StatementWithValue callerWithValue &&
                callerWithValue.SaveValue)
            { Stack.Push(returnValue.Value); }

            FunctionEvaluations++;
            return;
        }

        if (!AllowFunctionInlining ||
            !IsFunctionInlineable(function, parameters) ||
            !InlineMacro(function, parameters, out Statement? inlined))
        {
            GenerateCodeForFunction_(function, parameters, typeArguments, caller);
            return;
        }

        CodeSnapshot originalCode = SnapshotCode();
        GeneratorSnapshot originalSnapshot = Snapshot();
        int originalCodeLength = originalCode.Code.Length;

        try
        {
            GenerateCodeForStatement(inlined);
        }
        catch
        {
            Restore(originalSnapshot);
            RestoreCode(originalCode);

            GenerateCodeForFunction_(function, parameters, typeArguments, caller);
            return;
        }

        CodeSnapshot inlinedCode = SnapshotCode();
        int inlinedLength = inlinedCode.Code.Length - originalCodeLength;
        GeneratorSnapshot inlinedSnapshot = Snapshot();

        Restore(originalSnapshot);
        RestoreCode(originalCode);

        GenerateCodeForFunction_(function, parameters, typeArguments, caller);

        CodeSnapshot notInlinedCode = SnapshotCode();
        int notInlinedLength = notInlinedCode.Code.Length - originalCodeLength;
        GeneratorSnapshot notInlinedSnapshot = Snapshot();

        if (inlinedLength <= notInlinedLength)
        {
            Restore(inlinedSnapshot);
            RestoreCode(inlinedCode);
        }
        else
        {
            Restore(notInlinedSnapshot);
            RestoreCode(notInlinedCode);
        }
    }

    void GenerateCodeForParameterPassing<TFunction>(TFunction function, ImmutableArray<StatementWithValue> parameters, Stack<BrainfuckVariable> compiledParameters, List<IConstant> constantParameters, Dictionary<string, GeneralType>? typeArguments)
        where TFunction : ICompiledFunction, ISimpleReadable
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            StatementWithValue passed = parameters[i];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = FindStatementType(passed, definedType);

            if (passedType.GetSize(this, Diagnostics, passed) != definedType.GetSize(this, Diagnostics, defined))
            { Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed)); }

            foreach (BrainfuckVariable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Name == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, defined.File));
                    break;
                }
            }

            foreach (IConstant constantParameter in constantParameters)
            {
                if (constantParameter.Identifier == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as constant", defined.Identifier, defined.File));
                    break;
                }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            { Diagnostics.Add(Diagnostic.Critical($"Bruh", defined.Identifier, defined.File)); }

            bool canDeallocate = defined.Modifiers.Contains(ModifierKeywords.Temp);

            canDeallocate = canDeallocate && passedType.Is<PointerType>();

            if (StatementCanBeDeallocated(passed, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", passed)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", passed)); }
                canDeallocate = false;
            }

            if (passed is ModifiedStatement modifiedStatement)
            {
                if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, modifiedStatement.File));
                    return;
                }

                switch (modifiedStatement.Modifier.Content)
                {
                    case ModifierKeywords.Ref:
                    {
                        Identifier modifiedVariable = (Identifier)modifiedStatement.Statement;

                        if (!GetVariable(modifiedVariable, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                        {
                            Diagnostics.Add(notFoundError.ToError(modifiedVariable));
                            return;
                        }

                        if (!v.Type.SameAs(definedType))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{v.Type}\"", passed));
                            return;
                        }

                        VariableDeclaration variableDeclaration = defined.ToVariable(passed);
                        compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, false, v.Type, v.Size, variableDeclaration));
                        continue;
                    }
                    case ModifierKeywords.Const:
                    {
                        StatementWithValue valueStatement = modifiedStatement.Statement;
                        if (!TryCompute(valueStatement, out CompiledValue constValue))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Constant parameter must have a constant value", valueStatement));
                            return;
                        }

                        GeneralType constantType;
                        if (defined.Type != StatementKeywords.Var)
                        {
                            constantType = GeneralType.From(defined.Type, FindType, TryCompute);
                            defined.Type.SetAnalyzedType(constantType);

                            if (constantType.Is(out BuiltinType? builtinType))
                            { constValue.TryCast(builtinType.RuntimeType, out constValue); }
                        }
                        else
                        {
                            constantType = new BuiltinType(constValue.Type);
                        }

                        constantParameters.Add(new CompiledParameterConstant(constValue, constantType, defined));
                        continue;
                    }
                    case ModifierKeywords.Temp:
                    {
                        passed = modifiedStatement.Statement;
                        break;
                    }
                    default:
                        Diagnostics.Add(Diagnostic.Critical($"Unknown identifier modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, modifiedStatement.File));
                        return;
                }
            }

            if (passed is AddressGetter addressGetter)
            {
                Identifier modifiedVariable = (Identifier)addressGetter.PrevStatement;

                if (!GetVariable(modifiedVariable, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(modifiedVariable));
                    return;
                }

                if (!definedType.Is(out PointerType? pointerType) ||
                    !v.Type.SameAs(pointerType.To))
                {
                    if (pointerType is not null &&
                        v.Type.Is(out ArrayType? v1) &&
                        pointerType.To.Is(out ArrayType? v2) &&
                        v2.Length is null &&
                        v1.Length is not null)
                    { }
                    else
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{new PointerType(v.Type)}\"", passed));
                        return;
                    }
                }

                VariableDeclaration variableDeclaration = defined.ToVariable(passed);
                PointerType parameterType = new(v.Type);
                compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, false, parameterType, parameterType.GetSize(this, Diagnostics, modifiedVariable), variableDeclaration));
                continue;
            }

            if (passed is Identifier identifier &&
                GetVariable(identifier, out BrainfuckVariable? variable, out _) &&
                variable.IsReference)
            {
                if (!CanCastImplicitly(variable.Type, definedType, null, this, out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(Diagnostic.Critical(
                        $"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{variable.Type}\"",
                        passed,
                        castError.ToError(passed)));
                }

                VariableDeclaration variableDeclaration = defined.ToVariable(passed);
                compiledParameters.Push(new BrainfuckVariable(variable.Address, true, false, false, variable.Type, variable.Type.GetSize(this, Diagnostics, identifier), variableDeclaration));
                continue;
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref))
            {
                Diagnostics.Add(Diagnostic.Critical($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Ref}\" modifier", passed));
                return;
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Const))
            {
                Diagnostics.Add(Diagnostic.Critical($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed));
                return;
            }

            {
                VariableDeclaration variableDeclaration = defined.ToVariable(passed);
                using (TypeArgumentsScope g = SetTypeArgumentsScope(typeArguments))
                { PrecompileVariable(compiledParameters, variableDeclaration, canDeallocate, false, definedType); }

                BrainfuckVariable? compiledParameter = null;
                foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Name == defined.Identifier.Content)
                    {
                        compiledParameter = compiledParameter_;
                    }
                }

                if (compiledParameter is null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" not found", defined.Identifier, defined.File));
                    return;
                }

                if (!compiledParameter.Type.SameAs(definedType))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{compiledParameter.Type}\"", passed));
                    return;
                }

                using (Code.Block(this, $"SET \"{defined.Identifier.Content}\" TO _something_"))
                {
                    GenerateCodeForStatement(passed, definedType);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
            }
        }
    }

    void GenerateCodeForFunction_(CompiledFunction function, ImmutableArray<StatementWithValue> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledFunction> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (StatementWithValue parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (StatementWithValue parameter in parameters)
                { GenerateCodeForPrinter(parameter); }
                return;
            }
        }

        if (function.ExternalFunctionName == ExternalFunctionNames.StdIn)
        {
            int address = Stack.PushVirtual(1, callerPosition);
            Code.SetPointer(address);
            if (function.Type.SameAs(BasicType.Void))
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (function.Type.GetSize(this, Diagnostics, function) != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Function with attribute \"[{AttributeConstants.ExternalIdentifier}(\"{ExternalFunctionNames.StdIn}\")]\" must have a return type with size of 1", ((FunctionDefinition)function).Type, function.File));
                    return;
                }
                Code += ',';
            }

            return;
        }

        if (function.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition));
            return;
        }

        using ConsoleProgressLabel progressLabel = new($"Generating function \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        BrainfuckVariable? returnVariable = null;

        if (function.ReturnSomething)
        {
            VariableDeclaration variableDeclaration = new(
                Enumerable.Empty<Token>(),
                function.TypeToken,
                new Identifier(Token.CreateAnonymous(ReturnVariableName), function.File),
                null,
                function.File
            );
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(function.Type.GetSize(this, Diagnostics, function), callerPosition), false, false, false, function.Type, function.Type.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CompiledLocalConstants.PushRange(constantParameters);
        CompiledLocalConstants.AddRangeIf(frame.SavedConstants, v => !GetConstant(v.Identifier, v.File, out _, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        using (DebugBlock(function.Block.Brackets.End, function.Block.File))
        {
            if (returnBlock is not null)
            {
                returnBlock = Returns.Pop();
                if (returnBlock.Value.FlagAddress.HasValue)
                {
                    using (Code.Block(this, $"Finish \"return\" block"))
                    {
                        if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                        { Diagnostics.Add(Diagnostic.Internal("I don't know what happened here", function.Block)); }
                        Stack.Pop();
                    }
                }
            }

            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(new ManagedTypeCast(
                            new Identifier(Token.CreateAnonymous(variable.Name), variable.File),
                            new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Any, variable.File), Token.CreateAnonymous("*", TokenType.Operator), variable.File),
                            TokenPair.CreateAnonymous("(", ")"),
                            variable.File
                        ));
                    }
                }
            }

            using (Code.Block(this, $"Clean up function variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    BrainfuckVariable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledOperator function, ImmutableArray<StatementWithValue> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (StatementWithValue parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (StatementWithValue parameter in parameters)
                { GenerateCodeForPrinter(parameter); }
                return;
            }
        }

        if (function.ExternalFunctionName == ExternalFunctionNames.StdIn)
        {
            int address = Stack.PushVirtual(1, callerPosition);
            Code.SetPointer(address);
            if (function.Type.SameAs(BasicType.Void))
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (function.Type.GetSize(this, Diagnostics, function) != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Function with attribute \"StandardInput\" must have a return type with size of 1", ((FunctionDefinition)function).Type, function.File));
                    return;
                }
                Code += ',';
            }

            return;
        }

        if (function.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition));
            return;
        }

        using ConsoleProgressLabel progressLabel = new($"Generating function \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        BrainfuckVariable? returnVariable = null;

        if (true) // always returns something
        {
            VariableDeclaration variableDeclaration = new(
                Enumerable.Empty<Token>(),
                ((FunctionDefinition)function).Type,
                new Identifier(Token.CreateAnonymous(ReturnVariableName), function.File),
                null,
                function.File
            );
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(function.Type.GetSize(this, Diagnostics, function), callerPosition), false, false, false, function.Type, function.Type.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CompiledLocalConstants.PushRange(constantParameters);
        CompiledLocalConstants.AddRangeIf(frame.SavedConstants, v => !GetConstant(v.Identifier, v.File, out _, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        if (returnBlock is not null)
        {
            returnBlock = Returns.Pop();
            if (returnBlock.Value.FlagAddress.HasValue)
            {
                using (DebugBlock(function.Block.Brackets.End, function.Block.File))
                {
                    if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                    { Diagnostics.Add(Diagnostic.Internal("I don't know what happened here", function.Block)); }
                    Stack.Pop();
                }
            }
        }

        using (DebugBlock(function.Block.Brackets.End, function.Block.File))
        {
            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(new ManagedTypeCast(
                            new Identifier(Token.CreateAnonymous(variable.Name), variable.File),
                            new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Any, variable.File), Token.CreateAnonymous("*", TokenType.Operator), variable.File),
                            TokenPair.CreateAnonymous("(", ")"),
                            variable.File
                        ));
                    }
                }
            }

            using (Code.Block(this, $"Clean up function variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    BrainfuckVariable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type.Is<PointerType>())
                    { }
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledGeneralFunction function, ImmutableArray<StatementWithValue> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        BrainfuckVariable? returnVariable = null;

        if (function.ReturnSomething)
        {
            VariableDeclaration variableDeclaration = new(
                Enumerable.Empty<Token>(),
                new TypeInstanceSimple(function.Context.Identifier, function.File),
                new Identifier(Token.CreateAnonymous(ReturnVariableName), function.File),
                null,
                function.File
            );
            GeneralType returnType = GeneralType.InsertTypeParameters(function.Type, typeArguments) ?? function.Type;
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(returnType.GetSize(this, Diagnostics, function), callerPosition), false, false, false, returnType, returnType.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CompiledLocalConstants.PushRange(constantParameters);
        CompiledLocalConstants.AddRangeIf(frame.SavedConstants, v => !GetConstant(v.Identifier, v.File, out _, out _));

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", function));
            return;
        }

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        if (returnBlock is not null)
        {
            returnBlock = Returns.Pop();
            if (returnBlock.Value.FlagAddress.HasValue)
            {
                if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                { throw new InternalExceptionWithoutContext(); }
                Stack.Pop();
            }
        }

        using (Code.Block(this, $"Clean up function variables ({CompiledVariables.Count})"))
        {
            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(new Identifier(Token.CreateAnonymous(variable.Name), variable.File));
                    }
                }
            }

            int n = CompiledVariables.Count;
            for (int i = 0; i < n; i++)
            {
                BrainfuckVariable variable = CompiledVariables.Pop();
                if (!variable.HaveToClean) continue;
                Stack.Pop();
            }
        }

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledConstructor function, ImmutableArray<StatementWithValue> parameters, Dictionary<string, GeneralType>? typeArguments, ConstructorCall callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount - 1 != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to constructor \"{function.ToReadable()}\" (required {function.ParameterCount - 1} passed {parameters.Length})", callerPosition));
            return;
        }

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Constructor \"{function.ToReadable()}\" does not have any body definition", callerPosition));
            return;
        }

        using ConsoleProgressLabel progressLabel = new($"Generating function \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);

        NewInstance newInstance = callerPosition.ToInstantiation();

        int newInstanceAddress = Stack.NextAddress;
        GeneralType newInstanceType = FindStatementType(newInstance);
        GenerateCodeForStatement(newInstance);

        if (!newInstanceType.SameAs(function.ParameterTypes[0]))
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {0}: Expected \"{function.ParameterTypes[0]}\", passed \"{newInstanceType}\"", newInstance));
            return;
        }

        VariableDeclaration variableDeclaration = function.Parameters[0].ToVariable();
        compiledParameters.Add(new BrainfuckVariable(newInstanceAddress, false, false, false, newInstanceType, newInstanceType.GetSize(this, Diagnostics, newInstance), variableDeclaration));

        for (int i = 1; i < function.Parameters.Count; i++)
        {
            StatementWithValue passed = parameters[i - 1];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = FindStatementType(passed, definedType);

            if (!passedType.SameAs(definedType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed));
                return;
            }

            foreach (BrainfuckVariable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Name == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, defined.File));
                    return;
                }
            }

            foreach (IConstant constantParameter in constantParameters)
            {
                if (constantParameter.Identifier == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as constant", defined.Identifier, defined.File));
                    return;
                }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", defined.Identifier, defined.File));
                return;
            }

            bool deallocateOnClean = false;

            if (passed is ModifiedStatement modifiedStatement)
            {
                if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, modifiedStatement.File));
                    return;
                }

                switch (modifiedStatement.Modifier.Content)
                {
                    case ModifierKeywords.Ref:
                    {
                        Identifier modifiedVariable = (Identifier)modifiedStatement.Statement;

                        if (!GetVariable(modifiedVariable, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                        {
                            Diagnostics.Add(notFoundError.ToError(modifiedVariable));
                            return;
                        }

                        if (!v.Type.SameAs(definedType))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{v.Type}\"", passed));
                            return;
                        }

                        VariableDeclaration variableDeclaration2 = defined.ToVariable(passed);
                        compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, false, v.Type, v.Size, variableDeclaration2));
                        continue;
                    }
                    case ModifierKeywords.Const:
                    {
                        StatementWithValue valueStatement = modifiedStatement.Statement;

                        if (!TryCompute(valueStatement, out CompiledValue constValue))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Constant parameter must have a constant value", valueStatement));
                            return;
                        }

                        GeneralType constantType;
                        if (defined.Type != StatementKeywords.Var)
                        {
                            constantType = GeneralType.From(defined.Type, FindType, TryCompute);
                            defined.Type.SetAnalyzedType(constantType);

                            if (constantType.Is(out BuiltinType? builtinType))
                            { constValue.TryCast(builtinType.RuntimeType, out constValue); }
                        }
                        else
                        {
                            constantType = new BuiltinType(constValue.Type);
                        }

                        constantParameters.Add(new CompiledParameterConstant(constValue, constantType, defined));
                        continue;
                    }
                    case ModifierKeywords.Temp:
                    {
                        deallocateOnClean = true;
                        passed = modifiedStatement.Statement;
                        break;
                    }
                    default:
                        Diagnostics.Add(Diagnostic.Critical($"Unknown identifier modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, modifiedStatement.File));
                        return;
                }
            }

            if (passed is StatementWithValue value)
            {
                if (defined.Modifiers.Contains(ModifierKeywords.Ref))
                {
                    Diagnostics.Add(Diagnostic.Critical($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Ref}\" modifier", passed));
                    return;
                }

                if (defined.Modifiers.Contains(ModifierKeywords.Const))
                {
                    Diagnostics.Add(Diagnostic.Critical($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed));
                    return;
                }

                VariableDeclaration variableDeclaration2 = defined.ToVariable(passed);
                PrecompileVariable(compiledParameters, variableDeclaration2, defined.Modifiers.Contains(ModifierKeywords.Temp) && deallocateOnClean, false);

                BrainfuckVariable? compiledParameter = null;
                foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Name == defined.Identifier.Content)
                    {
                        compiledParameter = compiledParameter_;
                    }
                }

                if (compiledParameter is null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" not found", defined.Identifier, defined.File));
                    return;
                }

                if (!compiledParameter.Type.SameAs(definedType))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{compiledParameter.Type}\"", passed));
                    return;
                }

                using (Code.Block(this, $"SET \"{defined.Identifier.Content}\" TO _something_"))
                {
                    GenerateCodeForStatement(value);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
                continue;
            }

            throw new NotImplementedException($"Unimplemented invocation parameter \"{passed.GetType().Name}\"");
        }

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushRange(compiledParameters);
        CompiledLocalConstants.PushRange(constantParameters);
        CompiledLocalConstants.AddRangeIf(frame.SavedConstants, v => !GetConstant(v.Identifier, v.File, out _, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        if (returnBlock is not null)
        {
            returnBlock = Returns.Pop();
            if (returnBlock.Value.FlagAddress.HasValue)
            {
                using (DebugBlock(function.Block.Brackets.End, function.Block.File))
                using (Code.Block(this, $"Finish \"return\" block"))
                {
                    if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                    { Diagnostics.Add(Diagnostic.Internal("I don't know what happened here", function.Block)); }
                    Stack.Pop();
                }
            }
        }

        using (DebugBlock(function.Block.Brackets.End, function.Block.File))
        {
            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(new ManagedTypeCast(
                            new Identifier(Token.CreateAnonymous(variable.Name), variable.File),
                            new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Any, variable.File), Token.CreateAnonymous("*", TokenType.Operator), variable.File),
                            TokenPair.CreateAnonymous("(", ")"),
                            variable.File
                        ));
                    }
                }
            }

            using (Code.Block(this, $"Clean up function variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    BrainfuckVariable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    bool IxMaxResursiveDepthReached(FunctionThingDefinition function, ILocated callerPosition)
    {
        int depth = 0;
        for (int i = 0; i < CurrentMacro.Count; i++)
        {
            if (!object.ReferenceEquals(CurrentMacro[i], function))
            { continue; }
            depth++;

            if (MaxRecursiveDepth > 0)
            {
                if (MaxRecursiveDepth >= depth)
                {
                    return true;
                }
                else
                {
                    GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed), callerPosition);
                    GenerateCodeForPrinter($"Max recursivity depth ({MaxRecursiveDepth}) exceeded (\"{function.ToReadable()}\")", callerPosition);
                    GenerateCodeForPrinter(Ansi.Reset, callerPosition);
                    return false;
                }
            }

            throw new NotSupportedException($"Recursive functions are not supported (The function \"{function.ToReadable()}\" used recursively)", callerPosition.Location);
        }

        return true;
    }

    void FinishControlFlowStatements(ControlFlowBlock block, bool popFlag, string kind)
    {
        int pendingJumps = block.PendingJumps.Pop();
        block.Doings.Pop();
        using (Code.Block(this, $"Finish {pendingJumps} \"{kind}\" statements"))
        {
            Code.ClearValue(Stack.NextAddress);
            Code.CommentLine($"Pointer: {Code.Pointer}");
            for (int i = 0; i < pendingJumps; i++)
            {
                Code.JumpEnd();
                Code.LineBreak();
            }
            Code.CommentLine($"Pointer: {Code.Pointer}");
        }

        if (!popFlag || !block.FlagAddress.HasValue) return;

        using (Code.Block(this, $"Finish \"{kind}\" block"))
        {
            if (block.FlagAddress.Value != Stack.LastAddress)
            { throw new InternalExceptionWithoutContext(); }
            Stack.Pop();
        }
    }

    void ContinueControlFlowStatements(Stack<ControlFlowBlock> controlFlowBlocks, string kind)
    {
        if (controlFlowBlocks.Count == 0) return;
        // TODO: think about it
        if (!controlFlowBlocks.Last.FlagAddress.HasValue) return;

        using (Code.Block(this, $"Continue \"{kind}\" statements"))
        {
            if (!controlFlowBlocks.Last.FlagAddress.HasValue)
            { Diagnostics.Add(Diagnostic.Internal($"Unexpected conditional jump continuation in the depths (this is the compiler's fault)", controlFlowBlocks.Last.Location)); }

            Code.CopyValue(controlFlowBlocks.Last.FlagAddress.Value, Stack.NextAddress);
            Code.JumpStart(Stack.NextAddress);
            controlFlowBlocks.Last.PendingJumps.Last++;
        }
    }

    ControlFlowBlock? BeginReturnBlock(ILocated location, ControlFlowUsage usage)
    {
        if (usage.HasFlag(ControlFlowUsage.ConditionalReturn) || !AllowOtherOptimizations)
        {
            using (DebugBlock(location))
            using (Code.Block(this, $"Begin conditional return block (depth: {Returns.Count} (now its one more))"))
            {
                int flagAddress = Stack.Push(1);
                Code.CommentLine($"Return flag is at {flagAddress}");
                ControlFlowBlock block = new(flagAddress, location);
                Returns.Push(block);
                return block;
            }
        }
        else if (usage.HasFlag(ControlFlowUsage.Return))
        {
            Code.CommentLine($"Begin simple return block (depth: {Returns.Count} (now its one more))");
            ControlFlowBlock block = new(null, location);
            Returns.Push(block);
            return block;
        }
        else
        {
            Code.CommentLine($"Doesn't begin return block (usage: {usage})");
            return null;
        }
    }

    ControlFlowBlock? BeginBreakBlock(ILocated location, ControlFlowUsage usage)
    {
        if ((usage & ControlFlowUsage.Break) == ControlFlowUsage.None)
        {
            Code.CommentLine("Doesn't begin \"break\" block");
            return null;
        }

        using (DebugBlock(location))
        using (Code.Block(this, $"Begin \"break\" block (depth: {Breaks.Count} (now its one more))"))
        {
            int flagAddress = Stack.Push(1);
            Code.CommentLine($"Break flag is at {flagAddress}");
            ControlFlowBlock block = new(flagAddress, location);
            Breaks.Push(block);
            return block;
        }
    }
}
