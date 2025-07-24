using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Brainfuck.Generator;

public partial class CodeGeneratorForBrainfuck : CodeGenerator
{
    bool AllowFunctionInlining => !Settings.DontOptimize;
    bool AllowPrecomputing => !Settings.DontOptimize;
    bool AllowOtherOptimizations => !Settings.DontOptimize;

    GeneratorStatistics _statistics;

    void GenerateDestructor(CompiledStatementWithValue value, CompiledCleanup cleanup)
    {
        GeneralType deallocateableType = value.Type;

        ImmutableArray<CompiledStatementWithValue> parameters = ImmutableArray.Create(value);
        ImmutableArray<GeneralType> parameterTypes = parameters.Select(v => v.Type).ToImmutableArray();

        if (!deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            Diagnostics.Add(Diagnostic.Warning($"The \"{StatementKeywords.Delete}\" keyword-function is only working on pointers so I skip this", value));
            return;
        }

        if (cleanup.Destructor is null)
        {
            if (cleanup.Deallocator is not null) GenerateCodeForFunction(cleanup.Deallocator, ImmutableArray.Create(CompiledPassedArgument.Wrap(value)), null, value);

            if (!deallocateablePointerType.To.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Warning(
                    $"Destructor for type \"{deallocateablePointerType}\" not found",
                    value));
            }

            return;
        }

        GenerateCodeForFunction(cleanup.Destructor, ImmutableArray.Create(CompiledPassedArgument.Wrap(value)), null, value);

        if (cleanup.Destructor.ReturnSomething)
        { Stack.Pop(); }

        if (cleanup.Deallocator is not null) GenerateCodeForFunction(cleanup.Deallocator, ImmutableArray.Create(CompiledPassedArgument.Wrap(value)), null, value);
    }

    void GenerateDestructor(BrainfuckVariable variable)
    {
        if (variable.Cleanup is null) return;
        GenerateDestructor(
            new CompiledVariableGetter()
            {
                Variable = variable.Declaration,
                Location = variable.Declaration.Location,
                SaveValue = true,
                Type = variable.Type,
            },
            variable.Cleanup
        );
    }

    #region PrecompileVariables2
    int PrecompileVariables(CompiledBlock block, bool ignoreRedefinition)
    { return PrecompileVariables(block.Statements, ignoreRedefinition); }
    int PrecompileVariables(IEnumerable<CompiledStatement>? statements, bool ignoreRedefinition)
    {
        if (statements == null) return 0;

        int result = 0;
        foreach (CompiledStatement statement in statements)
        { result += PrecompileVariables(statement, ignoreRedefinition); }
        return result;
    }
    int PrecompileVariables(CompiledStatement statement, bool ignoreRedefinition)
    {
        if (statement is not CompiledVariableDeclaration instruction)
        { return 0; }

        return PrecompileVariable(instruction, ignoreRedefinition);
    }
    int PrecompileVariable(CompiledVariableDeclaration variableDeclaration, bool ignoreRedefinition)
        => PrecompileVariable(CompiledVariables, variableDeclaration, ignoreRedefinition);
    int PrecompileVariable(Stack<BrainfuckVariable> variables, CompiledVariableDeclaration variableDeclaration, bool ignoreRedefinition, GeneralType? type = null)
    {
        //if (variables.Any(other =>
        //        other.Identifier == variableDeclaration.Identifier &&
        //        other.File == variableDeclaration.Location.File))
        //{
        //    if (ignoreRedefinition) return 0;
        //    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variableDeclaration.Identifier}\" already defined", variableDeclaration));
        //}

        if (type is not null)
        {

        }
        else
        {
            type = variableDeclaration.Type;

            if (type is ArrayType arrayType &&
                variableDeclaration.InitialValue is CompiledLiteralList literalList &&
                arrayType.Length is null)
            {
                arrayType = new ArrayType(arrayType.Of, new CompiledEvaluatedValue()
                {
                    Value = literalList.Values.Length,
                    Type = ArrayLengthType,
                    Location = variableDeclaration.Location,
                    SaveValue = true,
                });
            }
        }

        if (variableDeclaration.InitialValue != null)
        {
            GeneralType initialValueType = variableDeclaration.InitialValue.Type;

            if (initialValueType.GetSize(this, Diagnostics, variableDeclaration.InitialValue) != type.GetSize(this, Diagnostics, variableDeclaration))
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable initial value type (\"{initialValueType}\") and variable type (\"{type}\") mismatch", variableDeclaration.InitialValue));
                return default;
            }

            if (type.Is(out ArrayType? arrayType))
            {
                if (arrayType.Of.SameAs(BasicType.U16) &&
                    variableDeclaration.InitialValue is CompiledStringInstance literal)
                {
                    if (arrayType.Length is not null)
                    {
                        if (!arrayType.ComputedLength.HasValue)
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Literal length {literal.Value.Length} must be equal to the stack array length <runtime value>", literal));
                            return default;
                        }
                        if (literal.Value.Length != arrayType.ComputedLength.Value)
                        {
                            Diagnostics.Add(Diagnostic.Critical($"Literal length {literal.Value.Length} must be equal to the stack array length {arrayType.ComputedLength.Value}", literal));
                            return default;
                        }
                    }

                    using (DebugBlock(variableDeclaration.InitialValue))
                    {
                        int arraySize = literal.Value.Length;

                        int size = Snippets.ARRAY_SIZE(arraySize);

                        int address2 = Stack.PushVirtual(size, literal);

                        variables.Push(new BrainfuckVariable(address2, false, true, variableDeclaration.Cleanup, size, variableDeclaration)
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
                    variables.Push(new BrainfuckVariable(address2, false, true, variableDeclaration.Cleanup, size, variableDeclaration));
                }
                return 1;
            }

            if (variableDeclaration.InitialValue is CompiledAddressGetter addressGetter &&
                GetVariable(addressGetter.Of, out BrainfuckVariable? shadowingVariable, out _) &&
                type.Is(out PointerType? pointerType))
            {
                if (!StatementCompiler.CanCastImplicitly(pointerType.To, shadowingVariable.Type, null, out PossibleDiagnostic? castError))
                { Diagnostics.Add(castError.ToError(variableDeclaration.InitialValue)); }

                variables.Push(new BrainfuckVariable(shadowingVariable.Address, true, false, null, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration)
                {
                    IsInitialized = true
                });
                return 0;
            }

            int address = Stack.PushVirtual(type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, variableDeclaration.Cleanup, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration));
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
                variables.Push(new BrainfuckVariable(address2, false, true, variableDeclaration.Cleanup, size, variableDeclaration));
                return 1;
            }

            int address = Stack.PushVirtual(type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, variableDeclaration.Cleanup, type.GetSize(this, Diagnostics, variableDeclaration), variableDeclaration));
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

        if (type.Length is not CompiledEvaluatedValue evaluatedStatement)
        {
            error = new PossibleDiagnostic($"Can't compute the array type's length");
            return false;
        }

        error = null;
        size = Snippets.ARRAY_SIZE((int)evaluatedStatement.Value);
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
            case BasicType.U16: size = 1; return true;
            case BasicType.I16: size = 1; return true;
            case BasicType.U32: size = 1; return true;
            case BasicType.I32: size = 1; return true;
            case BasicType.U64: size = 1; return true;
            case BasicType.I64: size = 1; return true;
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

        if (type.Length.Type.GetSize(this, Diagnostics, type.Length) != 1)
        {
            error = new PossibleDiagnostic($"Array length must be 1 byte");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        if (elementSize != 1)
        {
            error = new PossibleDiagnostic($"Array element size must be 1 byte");
            return false;
        }

        // (2 * elementCount) + 3

        int lengthAddress = Stack.NextAddress;
        GenerateCodeForStatement(type.Length);
        int tempAddress = Stack.PushVirtual(1);
        Code.CopyValue(lengthAddress, tempAddress);
        Stack.PopAndAdd(lengthAddress);
        Code.AddValue(lengthAddress, 3);
        Stack.PopAndAdd(result);

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

    void GenerateCodeForSetter(CompiledVariableSetter _statement)
    {
        if (!GetVariable(_statement.Variable.Identifier, _statement.Variable.Location.File, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(_statement.Variable));
            return;
        }

        GenerateCodeForSetter(variable, _statement.Value);
    }
    void GenerateCodeForSetter(CompiledParameterSetter _statement)
    {
        if (!GetVariable(_statement.Variable.Identifier.Content, _statement.Variable.File, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(_statement.Variable));
            return;
        }

        GenerateCodeForSetter(variable, _statement.Value);
    }
    void GenerateCodeForSetter(CompiledFieldSetter fieldSetter)
    {
        if ((
            GetVariable(fieldSetter.Object, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            fieldSetter.Object.Type.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is<StructType>()
        ) ||
            fieldSetter.Object.Type.Is<StructType>())
        {
            // if (!fieldSetter.Type.SameAs(fieldSetter.Value.Type))
            // {
            //     Diagnostics.Add(Diagnostic.Critical($"Can not set a \"{fieldSetter.Value.Type}\" type value to the \"{fieldSetter.Type}\" type field.", fieldSetter.Value));
            //     return;
            // }

            if (!TryGetAddress(fieldSetter.ToGetter(), out Address? address, out int size))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get field address", fieldSetter.ToGetter()));
                return;
            }

            if (size != fieldSetter.Value.Type.GetSize(this, Diagnostics, fieldSetter.Value))
            {
                Diagnostics.Add(Diagnostic.Critical($"Field and value size mismatch", fieldSetter.Value));
                return;
            }

            CompileSetter(address, fieldSetter.Value);
            return;
        }

        if (fieldSetter.Object.Type.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{pointerType}\"", fieldSetter.Object));
                return;
            }

            if (!structPointerType.GetField(fieldSetter.Field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(fieldSetter));
                return;
            }

            if (fieldSetter.Type.GetSize(this, Diagnostics, fieldSetter.ToGetter()) != fieldSetter.Value.Type.GetSize(this, Diagnostics, fieldSetter.Value))
            {
                Diagnostics.Add(Diagnostic.Critical($"Field and value size mismatch", fieldSetter.Value));
                return;
            }

            int _pointerAddress = Stack.NextAddress;
            using (DebugInfoBlock debugBlock = DebugBlock(fieldSetter.ToGetter()))
            {
                GenerateCodeForStatement(fieldSetter.Object);
                Code.AddValue(_pointerAddress, fieldOffset);
            }

            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(fieldSetter.Value);

            Heap.Set(_pointerAddress, valueAddress);

            Stack.Pop(); // valueAddress
            Stack.Pop(); // _pointerAddress

            return;
        }

        throw new NotImplementedException();
    }
    void GenerateCodeForSetter(BrainfuckVariable variable, CompiledStatementWithValue value)
    {
        if (AllowOtherOptimizations &&
            GetVariable(value, out BrainfuckVariable? valueVariable, out _))
        {
            if (variable.Address == valueVariable.Address)
            {
                _statistics.Optimizations++;
                return;
            }

            if (valueVariable.IsDiscarded)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{valueVariable.Identifier}\" is discarded", value));
                return;
            }

            if (variable.Size != valueVariable.Size)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable and value size mismatch ({variable.Size} != {valueVariable.Size})", value));
                return;
            }

            variable.IsDiscarded = false;

            using StackAddress tempAddress = Stack.GetTemporaryAddress(1, value);

            int size = valueVariable.Size;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedSource = valueVariable.Address + offset;
                int offsettedTarget = variable.Address + offset;

                Code.CopyValue(offsettedSource, offsettedTarget, tempAddress);
            }

            _statistics.Optimizations++;

            variable.IsInitialized = true;

            return;
        }

        if (AllowOtherOptimizations &&
            value is CompiledBinaryOperatorCall valueBinaryOperator &&
            GetVariable(valueBinaryOperator.Left, out BrainfuckVariable? variableInBinaryOperator, out _) &&
            variable.Address == variableInBinaryOperator.Address)
        {
            switch (valueBinaryOperator.Operator)
            {
                case "+":
                {
                    if (variableInBinaryOperator.IsDiscarded) break;
                    if (variable.Size != 1) break;

                    if (AllowPrecomputing && valueBinaryOperator.Right is CompiledEvaluatedValue constantValue)
                    {
                        CompiledValue _value = constantValue.Value;
                        if (constantValue.Value.TryCast(variable.Type, out CompiledValue castedValue))
                        { _value = castedValue; }

                        if (!variable.Type.SameAs(_value.Type)) break;

                        Code.AddValue(variable.Address, _value);

                        _statistics.Precomputations++;
                        return;
                    }

                    using (Code.Block(this, $"Add \"{valueBinaryOperator.Right}\" to variable \"{variable.Identifier}\" (at {variable.Address})"))
                    {
                        using (Code.Block(this, $"Compute value"))
                        {
                            GenerateCodeForStatement(valueBinaryOperator.Right);
                        }

                        using (Code.Block(this, $"Set computed value to {variable.Address}"))
                        {
                            Stack.Pop(address => Code.MoveAddValue(address, variable.Address));
                        }
                    }
                    _statistics.Optimizations++;

                    return;
                }
                case "-":
                {
                    if (variableInBinaryOperator.IsDiscarded) break;
                    if (variable.Size != 1) break;

                    if (AllowPrecomputing && valueBinaryOperator.Right is CompiledEvaluatedValue constantValue)
                    {
                        CompiledValue _value = constantValue.Value;
                        if (constantValue.Value.TryCast(variable.Type, out CompiledValue castedValue))
                        { _value = castedValue; }

                        if (!variable.Type.SameAs(_value.Type)) break;

                        Code.AddValue(variable.Address, -_value);

                        _statistics.Precomputations++;
                        return;
                    }

                    using (Code.Block(this, $"Subtract \"{valueBinaryOperator.Right}\" from variable \"{variable.Identifier}\" (at {variable.Address})"))
                    {
                        using (Code.Block(this, $"Compute value"))
                        {
                            GenerateCodeForStatement(valueBinaryOperator.Right);
                        }

                        using (Code.Block(this, $"Set computed value to {variable.Address}"))
                        {
                            Stack.Pop(address => Code.MoveSubValue(address, variable.Address));
                        }
                    }
                    _statistics.Optimizations++;

                    return;
                }
            }
        }

        if (VariableUses(value, variable) == 0)
        { VariableCanBeDiscarded = variable.Identifier; }

        using (Code.Block(this, $"Set variable \"{variable.Identifier}\" (at {variable.Address}) to \"{value}\""))
        {
            int valueSize = value.Type.GetSize(this, Diagnostics, value);

            if (variable.Type.Is(out ArrayType? arrayType))
            {
                if (value is CompiledStackStringInstance literal)
                {
                    if (!literal.IsASCII)
                    {
                        int size = Snippets.ARRAY_SIZE(literal.Length);

                        using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                        using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                        {
                            int i;
                            for (i = 0; i < literal.Value.Length; i++)
                            {
                                Code.SetValue(indexAddress, i);
                                Code.SetValue(valueAddress, literal.Value[i]);
                                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                            }

                            if (literal.IsNullTerminated)
                            {
                                Code.SetValue(indexAddress, i);
                                Code.SetValue(valueAddress, '\0');
                                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                            }
                        }

                        variable.IsDiscarded = false;

                        VariableCanBeDiscarded = null;

                        return;
                    }
                    else if (arrayType.Of.SameAs(BasicType.U8))
                    {
                        int size = Snippets.ARRAY_SIZE(literal.Length);

                        using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                        using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                        {
                            int i;
                            for (i = 0; i < literal.Value.Length; i++)
                            {
                                Code.SetValue(indexAddress, i);
                                Code.SetValue(valueAddress, (byte)literal.Value[i]);
                                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                            }

                            if (literal.IsNullTerminated)
                            {
                                Code.SetValue(indexAddress, i);
                                Code.SetValue(valueAddress, (byte)'\0');
                                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                            }
                        }

                        variable.IsDiscarded = false;

                        VariableCanBeDiscarded = null;

                        return;
                    }
                }
                else if (value is CompiledLiteralList literalList &&
                         arrayType.ComputedLength.HasValue &&
                         arrayType.ComputedLength.Value == literalList.Values.Length &&
                         arrayType.Of.GetSize(this, Diagnostics, value) == 1)
                {
                    int arraySize = arrayType.ComputedLength.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                    using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                    {
                        for (int i = 0; i < literalList.Values.Length; i++)
                        {
                            if (literalList.Values[i] is CompiledEvaluatedValue elementValue)
                            {
                                Code.ARRAY_SET_CONST(variable.Address, i, elementValue.Value);
                                // Code.SetValue(indexAddress, i);
                                // Code.SetValue(valueAddress, elementValue);
                                // Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, value));
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                    }

                    variable.IsDiscarded = false;

                    VariableCanBeDiscarded = null;

                    return;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            using (Code.Block(this, $"Compute value"))
            {
                GenerateCodeForStatement(value);
            }

            using (Code.Block(this, $"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
            { Stack.PopAndStore(variable.Address); }

            variable.IsDiscarded = false;

            VariableCanBeDiscarded = null;
            variable.IsInitialized = true;
        }
    }
    void GenerateCodeForSetter(CompiledIndirectSetter indirectSetter)
    {
        if (indirectSetter.AddressValue is CompiledVariableGetter variableGetter)
        {
            if (!GetVariable(variableGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableGetter));
                return;
            }

            if (variable.IsDiscarded)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Identifier}\" is discarded", variableGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", variableGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForSetter(variable, indirectSetter.Value);
                return;
            }
        }

        if (indirectSetter.AddressValue is CompiledParameterGetter parameterGetter)
        {
            if (!GetVariable(parameterGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(parameterGetter));
                return;
            }

            if (variable.IsDiscarded)
            {
                Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Identifier}\" is discarded", parameterGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", parameterGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForSetter(variable, indirectSetter.Value);
                return;
            }
        }

        CompileDereferencedSetter(indirectSetter.AddressValue, 0, indirectSetter.Value);
    }
    void CompileSetter(Address address, CompiledStatementWithValue value)
    {
        switch (address)
        {
            case AddressAbsolute v: CompileSetter(v, value); break;
            case AddressOffset v: CompileSetter(v, value); break;
            default: throw new NotImplementedException();
        }
    }
    void CompileSetter(AddressAbsolute address, CompiledStatementWithValue value)
    {
        using (Code.Block(this, $"Set value \"{value}\" to address {address}"))
        {
            if (AllowPrecomputing && value is CompiledEvaluatedValue constantValue)
            {
                Code.SetValue(address.Value, constantValue.Value.U8);

                _statistics.Precomputations++;

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
    void CompileSetter(AddressOffset address, CompiledStatementWithValue value)
    {
        if (address.Base is AddressRuntimePointer runtimePointer2)
        {
            GeneralType referenceType = runtimePointer2.PointerValue.Type;

            if (!referenceType.Is(out PointerType? _))
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", runtimePointer2.PointerValue));
                return;
            }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(runtimePointer2.PointerValue);

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

            GeneralType valueType = value.Type;

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
    void CompileDereferencedSetter(CompiledStatementWithValue dereference, int offset, CompiledStatementWithValue value)
    {
        if (!dereference.Type.Is(out PointerType? _))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", dereference));
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

        int valueAddress = Stack.NextAddress;
        GenerateCodeForStatement(value);

        int size = value.Type.GetSize(this, Diagnostics, value);

        if (size == 1 && AllowOtherOptimizations)
        {
            Heap.Set(pointerAddress, valueAddress);
        }
        else
        {
            using StackAddress tempPointerAddress = Stack.PushVirtual(1, value);
            for (int i = 0; i < size; i++)
            {
                Code.CopyValue(pointerAddress, tempPointerAddress);
                Heap.Set(tempPointerAddress, valueAddress + i);
                if (i + 1 < size) Code.AddValue(pointerAddress, 1);
            }
        }

        Stack.PopVirtual();
        Stack.PopVirtual();
    }
    void GenerateCodeForSetter(CompiledIndexSetter indexSetter)
    {
        if ((
            GetVariable(indexSetter.Base, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            indexSetter.Base.Type.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is(out ArrayType? arrayType)
        ) ||
            indexSetter.Base.Type.Is(out arrayType)
        )
        {
            if (!TryGetAddress(indexSetter.Base, out Address? arrayAddress, out _))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get array address", indexSetter.Base));
                return;
            }

            if (variable is not null)
            {
                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Identifier}\" is discarded", indexSetter.Base));
                    return;
                }
            }

            using (Code.Block(this, $"Set array (\"{indexSetter.Base}\") index (\"{indexSetter.Index}\") (at {arrayAddress}) to \"{indexSetter.Value}\""))
            {
                GeneralType elementType = arrayType.Of;

                if (!elementType.SameAs(indexSetter.Value.Type))
                {
                    Diagnostics.Add(Diagnostic.Critical("Bruh", indexSetter.Value));
                    return;
                }

                int elementSize = elementType.GetSize(this, Diagnostics, indexSetter.Base);

                if (elementSize != 1)
                { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", indexSetter); }

                int indexAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute index"))
                { GenerateCodeForStatement(indexSetter.Index); }

                int valueAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute value"))
                { GenerateCodeForStatement(indexSetter.Value); }

                if (arrayAddress is not AddressAbsolute arrayAddressAbs)
                { throw new NotImplementedException(); }

                Code.ARRAY_SET(arrayAddressAbs.Value, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, indexSetter));

                Stack.Pop();
                Stack.Pop();
            }

            return;
        }

        if (indexSetter.Base.Type.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexSetter.Value);

            if (!arrayType.Of.SameAs(indexSetter.Value.Type))
            {
                Diagnostics.Add(Diagnostic.Critical("Bruh", indexSetter.Value));
                return;
            }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexSetter.Base);

            if (!indexSetter.Index.Type.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Critical($"Index type must be built-in (ie. \"i32\") and not \"{indexSetter.Index.Type}\"", indexSetter.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexSetter.Index);

            if (arrayType.Of.GetSize(this, Diagnostics, indexSetter.Base) != 1)
            {
                using StackAddress multiplierAddress = Stack.Push(arrayType.Of.GetSize(this, Diagnostics, indexSetter.Base));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, indexSetter.Value));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Set(pointerAddress, valueAddress);

            Stack.Pop(); // pointerAddress
            Stack.Pop(); // valueAddress

            return;
        }

        Diagnostics.Add(Diagnostic.Critical("WHAT", indexSetter));
    }

    #endregion

    #region GenerateCodeForStatement()

    void GenerateCodeForStatement(CompiledStatement statement, GeneralType? expectedType = null)
    {
        switch (statement)
        {
            case CompiledReturn v: GenerateCodeForStatement(v); break;
            case CompiledBreak v: GenerateCodeForStatement(v); break;
            case CompiledCrash v: GenerateCodeForStatement(v); break;
            case CompiledDelete v: GenerateCodeForStatement(v); break;
            case CompiledSizeof v: GenerateCodeForStatement(v); break;
            case CompiledFunctionCall v: GenerateCodeForStatement(v); break;
            case CompiledExternalFunctionCall v: GenerateCodeForStatement(v); break;
            case CompiledIf v: GenerateCodeForStatement(v); break;
            case CompiledWhileLoop v: GenerateCodeForStatement(v); break;
            case CompiledForLoop v: GenerateCodeForStatement(v); break;
            case CompiledEvaluatedValue v: GenerateCodeForStatement(v); break;
            case CompiledVariableGetter v: GenerateCodeForStatement(v); break;
            case CompiledParameterGetter v: GenerateCodeForStatement(v); break;
            case CompiledBinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledUnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledAddressGetter v: GenerateCodeForStatement(v); break;
            case CompiledPointer v: GenerateCodeForStatement(v); break;
            case CompiledVariableDeclaration v: GenerateCodeForStatement(v); break;
            case CompiledFakeTypeCast v: GenerateCodeForStatement(v); break;
            case CompiledTypeCast v: GenerateCodeForStatement(v); break;
            case CompiledStackAllocation v: GenerateCodeForStatement(v); break;
            case CompiledConstructorCall v: GenerateCodeForStatement(v); break;
            case CompiledFieldGetter v: GenerateCodeForStatement(v); break;
            case CompiledIndexGetter v: GenerateCodeForStatement(v); break;
            case CompiledRuntimeCall v: GenerateCodeForStatement(v); break;
            case CompiledBlock v: GenerateCodeForStatement(v); break;
            case CompiledVariableSetter v: GenerateCodeForSetter(v); break;
            case CompiledParameterSetter v: GenerateCodeForSetter(v); break;
            case CompiledFieldSetter v: GenerateCodeForSetter(v); break;
            case CompiledIndexSetter v: GenerateCodeForSetter(v); break;
            case CompiledIndirectSetter v: GenerateCodeForSetter(v); break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: GenerateCodeForStatement(v.Statement); break;
            case CompiledStringInstance v: GenerateCodeForStatement(v, expectedType); break;
            case CompiledStackStringInstance v: GenerateCodeForStatement(v, expectedType); break;
            case EmptyStatement: break;
            default:
                Diagnostics.Add(Diagnostic.Critical($"Unknown statement \"{statement.GetType().Name}\"", statement));
                return;
        }
    }
    void GenerateCodeForStatement(CompiledRuntimeCall anyCall)
    {
        throw new NotSupportedException($"Function pointers not supported by brainfuck", anyCall);
    }
    void GenerateCodeForStatement(CompiledIndexGetter indexCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(indexCall);

        if ((
            GetVariable(indexCall.Base, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            indexCall.Base.Type.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is(out ArrayType? arrayType)
        ) ||
            indexCall.Base.Type.Is(out arrayType)
        )
        {
            if (!TryGetAddress(indexCall.Base, out Address? arrayAddress, out _))
            {
                Diagnostics.Add(Diagnostic.Critical($"Failed to get array address", indexCall.Base));
                return;
            }

            GeneralType elementType = arrayType.Of;

            int elementSize = elementType.GetSize(this, Diagnostics, indexCall.Base);

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

        if (indexCall.Base.Type.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            int resultAddress = Stack.Push(0);

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.Base);

            if (!indexCall.Index.Type.Is<BuiltinType>())
            {
                Diagnostics.Add(Diagnostic.Critical($"Index type must be built-in (ie. \"int\") and not \"{indexCall.Index.Type}\"", indexCall.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.Index);

            {
                using StackAddress multiplierAddress = Stack.Push(arrayType.Of.GetSize(this, Diagnostics, indexCall.Base));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, indexCall.Index));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Get(pointerAddress, resultAddress);

            Stack.Pop(); // pointerAddress
            return;
        }

        Diagnostics.Add(Diagnostic.Critical($"Index getter for type \"{indexCall.Base.Type}\" not found", indexCall));
        return;
    }
    void GenerateCodeForStatement(CompiledIf @if, bool linked = false)
    {
        using (Code.Block(this, $"If (\"{@if.Condition}\")"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@if.Condition); }

            using (DebugBlock(@if.Condition))
            { Code.NORMALIZE_BOOL(conditionAddress, v => Stack.GetTemporaryAddress(v, @if.Condition)); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            Code.CommentLine($"Pointer: {Code.Pointer}");

            Code.JumpStart(conditionAddress);

            using (Code.Block(this, "The if statements"))
            {
                GenerateCodeForStatement(CompiledBlock.CreateIfNot(@if.Body));
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");

            if (@if.Next == null)
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
                            if (@if.Next is CompiledElse elseBlock)
                            {
                                using (Code.Block(this, "Block (else)"))
                                { GenerateCodeForStatement(CompiledBlock.CreateIfNot(elseBlock.Body)); }
                            }
                            else if (@if.Next is CompiledIf elseIf)
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
                {
                    ContinueControlFlowStatements(Returns, "return");
                    ContinueControlFlowStatements(Breaks, "break");
                }
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");
        }
    }
    void GenerateCodeForStatement(CompiledWhileLoop @while)
    {
        using (Code.Block(this, $"While (\"{@while.Condition}\")"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@while.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(@while.Body.Location.Before(), StatementCompiler.FindControlFlowUsage(@while.Body));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(CompiledBlock.CreateIfNot(@while.Body));
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
    void GenerateCodeForStatement(CompiledForLoop @for)
    {
        GenerateCodeForStatement(@for, false);
    }
    bool GenerateCodeForStatement(CompiledForLoop @for, bool shouldUnroll)
    {
        using (Code.Block(this, $"For"))
        {
            VariableCleanupStack.Push(@for.VariableDeclaration is null ? 0 : PrecompileVariables(@for.VariableDeclaration, false));

            if (@for.VariableDeclaration is not null)
            {
                using (Code.Block(this, "Variable Declaration"))
                { GenerateCodeForStatement(@for.VariableDeclaration); }
            }

            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@for.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(@for.Body.Location.Before(), StatementCompiler.FindControlFlowUsage(@for.Body));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(CompiledBlock.CreateIfNot(@for.Body));
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
    void GenerateCodeForStatement(CompiledReturn statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (statement.Value is not null)
        {
            if (!GetVariable(ReturnVariableName, statement.Location.File, out BrainfuckVariable? returnVariable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(Diagnostic.Critical(
                    $"Can't return value for some reason :(",
                    statement,
                    notFoundError.ToError(statement)));
                return;
            }

            GenerateCodeForSetter(returnVariable, statement.Value);
        }

        if (Returns.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't return for some reason :(", statement));
            return;
        }

        if (Returns.Last.FlagAddress.HasValue)
        { Code.SetValue(Returns.Last.FlagAddress.Value, 0); }

        Code.SetPointer(Stack.NextAddress);
        Code.ClearCurrent();
        Code.JumpStart(Stack.NextAddress);

        Returns.Last.PendingJumps.Last++;
        Returns.Last.Doings.Last = true;
    }
    void GenerateCodeForStatement(CompiledBreak statement)
    {
        if (Breaks.Count == 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"Looks like this is not inside a loop", statement));
            return;
        }

        if (!Breaks.Last.FlagAddress.HasValue)
        {
            Diagnostics.Add(Diagnostic.Critical($"Looks like this is not inside a loop", statement));
            return;
        }

        Code.SetValue(Breaks.Last.FlagAddress.Value, 0);

        Code.SetPointer(Stack.NextAddress);
        Code.ClearCurrent();
        Code.JumpStart(Stack.NextAddress);

        Breaks.Last.PendingJumps.Last++;
        Breaks.Last.Doings.Last = true;
    }
    void GenerateCodeForStatement(CompiledDelete statement)
    {
        GenerateDestructor(statement.Value, statement.Cleanup);
    }
    void GenerateCodeForStatement(CompiledCrash statement)
    {
        GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed), statement.Value);
        GenerateCodeForPrinter(statement.Value);
        GenerateCodeForPrinter(Ansi.Reset, statement.Value);
        Code.SetPointer(Stack.Push(1));
        Code += "[]";
        Stack.PopVirtual();
    }
    void GenerateCodeForStatement(CompiledVariableDeclaration statement)
    {
        if (statement.InitialValue == null) return;

        if (!GetVariable(statement.Identifier, statement.Location.File, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(statement));
            return;
        }

        if (variable.IsInitialized)
        { return; }

        GenerateCodeForSetter(variable, statement.InitialValue);
    }
    void GenerateCodeForStatement(CompiledSizeof functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (FindSize(functionCall.Of, out int size, out PossibleDiagnostic? findSizeError))
        {
            if (functionCall.SaveValue)
            { Stack.Push(size); }
        }
        else
        {
            StackAddress sizeAddress = Stack.Push(0);

            if (!GenerateSize(functionCall.Of, sizeAddress, out PossibleDiagnostic? generateSizeError))
            { Diagnostics.Add(generateSizeError.ToError(functionCall)); }

            if (!functionCall.SaveValue)
            { Stack.Pop(); }
        }
    }
    void GenerateCodeForStatement(CompiledFunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        GenerateCodeForFunction(functionCall.Function, functionCall.Arguments, null, functionCall);

        if (!functionCall.SaveValue && functionCall.Function.ReturnSomething)
        { Stack.Pop(); }
    }
    void GenerateCodeForStatement(CompiledExternalFunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (functionCall.Function.Name == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledPassedArgument parameter in functionCall.Arguments)
            {
                if (!CanGenerateCodeForPrinter(parameter.Value))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledPassedArgument parameter in functionCall.Arguments)
                { GenerateCodeForPrinter(parameter.Value); }
                return;
            }
        }

        if (functionCall.Function.Name == ExternalFunctionNames.StdIn)
        {
            int address = Stack.PushVirtual(1, functionCall);
            Code.SetPointer(address);
            if (functionCall.Declaration.Type.SameAs(BasicType.Void))
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (functionCall.Declaration.Type.GetSize(this, Diagnostics, functionCall) != 1)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Function with attribute \"[{AttributeConstants.ExternalIdentifier}(\"{ExternalFunctionNames.StdIn}\")]\" must have a return type with size of 1", ((FunctionDefinition)functionCall.Declaration).Type, functionCall.Declaration.File));
                    return;
                }
                Code += ',';
            }

            return;
        }

        if (!functionCall.SaveValue && functionCall.Function.ReturnValueSize > 0)
        { Stack.Pop(); }
    }
    void GenerateCodeForStatement(CompiledConstructorCall constructorCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(constructorCall);
        GenerateCodeForFunction(constructorCall.Function, constructorCall.Arguments, null, constructorCall);
    }
    void GenerateCodeForStatement(CompiledStringInstance statement, GeneralType? expectedType = null)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Set \"{statement}\" to address {Stack.NextAddress}"))
        {
            if (expectedType is not null &&
                expectedType.Is(out PointerType? pointerType) &&
                pointerType.To.SameAs(BasicType.U16))
            {
                // TODO: not true but false
                GenerateCodeForLiteralString(statement);
            }
            else
            {
                GenerateCodeForLiteralString(statement);
            }
        }
    }
    void GenerateCodeForStatement(CompiledStackStringInstance statement, GeneralType? expectedType = null)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(CompiledEvaluatedValue evaluatedValue)
    {
        using DebugInfoBlock debugBlock = DebugBlock(evaluatedValue);

        using (Code.Block(this, $"Set \"{evaluatedValue}\" to address {Stack.NextAddress}"))
        {
            Stack.Push(evaluatedValue.Value);
        }
    }
    void GenerateCodeForStatement(CompiledVariableGetter statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (!GetVariable(statement, out BrainfuckVariable? variable, out PossibleDiagnostic? variableNotFoundError))
        { throw new NotImplementedException(); }

        GenerateCodeForStatement(variable, statement);
    }
    void GenerateCodeForStatement(CompiledParameterGetter statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (!GetVariable(statement, out BrainfuckVariable? variable, out PossibleDiagnostic? variableNotFoundError))
        { throw new NotImplementedException(); }

        GenerateCodeForStatement(variable, statement);
    }
    void GenerateCodeForStatement(BrainfuckVariable variable, ILocated statement)
    {
        if (variable.IsDiscarded)
        {
            Diagnostics.Add(Diagnostic.Critical($"Variable \"{variable.Identifier}\" is discarded", statement));
            return;
        }

        if (variable.Size <= 0)
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't load variable \"{variable.Identifier}\" because it's size is {variable.Size} (bruh)", statement));
            return;
        }

        int loadTarget = Stack.PushVirtual(variable.Size, statement);

        using (Code.Block(this, $"Load variable \"{variable.Identifier}\" (from {variable.Address}) to {loadTarget}"))
        {
            for (int offset = 0; offset < variable.Size; offset++)
            {
                int offsettedSource = variable.Address + offset;
                int offsettedTarget = loadTarget + offset;

                if (AllowOtherOptimizations &&
                    VariableCanBeDiscarded != null &&
                    VariableCanBeDiscarded == variable.Identifier)
                {
                    Code.MoveValue(offsettedSource, offsettedTarget);
                    variable.IsDiscarded = true;
                    _statistics.Optimizations++;
                }
                else
                {
                    Code.CopyValue(offsettedSource, offsettedTarget);
                }
            }
        }
    }
    void GenerateCodeForStatement(CompiledBinaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Expression \"{statement.Left}\" \"{statement.Operator}\" \"{statement.Right}\""))
        {
            switch (statement.Operator)
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

                    if (statement.Right is not CompiledEvaluatedValue offsetConst)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                        return;
                    }

                    if (offsetConst.Value != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.Value.I32))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.Value.I32));

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

                    if (statement.Right is not CompiledEvaluatedValue offsetConst)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                        return;
                    }

                    if (offsetConst.Value != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.Value.I32))
                        {
                            Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.Value.I32));

                        using (Code.Block(this, $"Snippet MATH_DIV({valueAddress} {offsetAddress})"))
                        { Code.MATH_DIV(valueAddress, offsetAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                    }

                    break;
                }
                case BinaryOperatorCall.BitwiseAND:
                {
                    GeneralType leftType = statement.Left.Type;

                    if ((leftType.SameAs(BasicType.U8) ||
                        leftType.SameAs(BasicType.I8) ||
                        leftType.SameAs(BasicType.U16) ||
                        leftType.SameAs(BasicType.I16) ||
                        leftType.SameAs(BasicType.U32) ||
                        leftType.SameAs(BasicType.I32)) &&
                        statement.Right is CompiledEvaluatedValue right)
                    {
                        if (right.Value == 1)
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block(this, "Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            using StackAddress rightAddress = Stack.Push(2);

                            using (Code.Block(this, $"Snippet MOD({leftAddress} {rightAddress})"))
                            { Code.MATH_MOD(leftAddress, rightAddress, v => Stack.GetTemporaryAddress(v, statement)); }

                            break;
                        }

                        if (right.Value == 0b_1_0000000)
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

                    Diagnostics.Add(Diagnostic.Critical($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                    return;
                }
                default:
                    Diagnostics.Add(Diagnostic.Critical(
                        $"I can't make \"{statement.Operator}\" operators to work in brainfuck",
                        statement));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledUnaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Expression \"{statement.Left}\" \"{statement.Operator}\""))
        {
            switch (statement.Operator)
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
                        statement));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledBlock block)
    {
        using ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress);

        using (DebugBlock(block.Location.Before()))
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

        using (DebugBlock(block.Location.After()))
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
    void GenerateCodeForStatement(CompiledAddressGetter addressGetter)
    {
        Diagnostics.Add(Diagnostic.Critical($"This is when pointers to the stack isn't work in brainfuck", addressGetter));
        return;
    }
    void GenerateCodeForStatement(CompiledPointer pointer)
    {
        using DebugInfoBlock debugBlock = DebugBlock(pointer);

        if (pointer.To is CompiledVariableGetter variableGetter)
        {
            if (!GetVariable(variableGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", variableGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForStatement(variable, pointer.To);
                return;
            }
        }

        if (pointer.To is CompiledParameterGetter compiledParameterGetter)
        {
            if (!GetVariable(compiledParameterGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(compiledParameterGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", compiledParameterGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForStatement(variable, pointer.To);
                return;
            }
        }

        int pointerAddress = Stack.NextAddress;
        GenerateCodeForStatement(pointer.To);

        Heap.Get(pointerAddress, pointerAddress);
    }
    void GenerateCodeForStatement(CompiledStackAllocation newInstance)
    {
        using DebugInfoBlock debugBlock = DebugBlock(newInstance);

        int address = Stack.PushVirtual(newInstance.Type.GetSize(this, Diagnostics, newInstance), newInstance);
        int size = newInstance.Type.GetSize(this);

        for (int offset = 0; offset < size; offset++)
        {
            int offsettedAddress = address + offset;
            Code.SetValue(offsettedAddress, 0);
        }
    }
    void GenerateCodeForStatement(CompiledFieldGetter field)
    {
        using DebugInfoBlock debugBlock = DebugBlock(field);

        if (GetVariable(field.Object, out BrainfuckVariable? prevVariable, out _) &&
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

        if (field.Object.Type.Is(out PointerType? pointerType))
        {
            if (!pointerType.To.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{field.Object.Type}\"", field.Object));
                return;
            }

            if (!structPointerType.GetField(field.Field.Identifier.Content, this, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field));
                return;
            }

            int resultAddress = Stack.Push(fieldDefinition.Type.GetSize(this, Diagnostics, fieldDefinition));

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(field.Object);

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
    void GenerateCodeForStatement(CompiledFakeTypeCast typeCast)
    {
        GenerateCodeForStatement(typeCast.Value);
    }
    void GenerateCodeForStatement(CompiledTypeCast typeCast)
    {
        if (typeCast.Value is CompiledEvaluatedValue evaluatedValue &&
            evaluatedValue.Value.TryCast(typeCast.Type, out CompiledValue casted))
        {
            Stack.Push(casted);
            return;
        }

        if (typeCast.Value.Type.GetSize(this, Diagnostics, typeCast.Value) != typeCast.Type.GetSize(this, Diagnostics, typeCast))
        {
            Diagnostics.Add(Diagnostic.Critical($"Type-cast is not supported at the moment", typeCast));
            return;
        }

        GenerateCodeForStatement(typeCast.Value);
        Diagnostics.Add(Diagnostic.Warning($"Type-cast is not supported at the moment", typeCast));
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
        if (address.Base is AddressRuntimePointer runtimePointer2)
        {
            using (Code.Block(this, $"Load data (dereferenced from \"{runtimePointer2.PointerValue}\" + {address.Offset})"))
            {
                int pointerAddress = Stack.NextAddress;
                GenerateCodeForStatement(runtimePointer2.PointerValue);
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

    void GenerateCodeForPrinter(CompiledStatementWithValue value)
    {
        if (value is CompiledStringInstance literal1)
        {
            GenerateCodeForPrinter(literal1.Value, literal1);
            return;
        }

        if (value is CompiledStackStringInstance literal2)
        {
            GenerateCodeForPrinter(literal2.Value, literal2);
            return;
        }

        GenerateCodeForValuePrinter(value, value.Type);
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
    void GenerateCodeForValuePrinter(CompiledStatementWithValue value, GeneralType valueType)
    {
        if (value is CompiledStringInstance literalValue1)
        {
            GenerateCodeForPrinter(literalValue1.Value, literalValue1);
            return;
        }

        if (value is CompiledStackStringInstance literalValue2)
        {
            GenerateCodeForPrinter(literalValue2.Value, literalValue2);
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

    bool CanGenerateCodeForPrinter(CompiledStatementWithValue value)
    {
        if (value is CompiledStringInstance) return true;
        if (value is CompiledStackStringInstance) return true;

        return CanGenerateCodeForValuePrinter(value.Type);
    }
    bool CanGenerateCodeForValuePrinter(GeneralType valueType) =>
        valueType.GetSize(this) == 1 &&
        valueType.Is<BuiltinType>();

    #endregion

    int GenerateCodeForLiteralString(CompiledStringInstance stringInstance)
    {
        if (!stringInstance.IsASCII)
        { throw new NotImplementedException(); }

        using DebugInfoBlock debugBlock = DebugBlock(stringInstance.Location);

        using (Code.Block(this, $"Create String \"{stringInstance.Value}\""))
        {
            int pointerAddress = Stack.NextAddress;
            using (Code.Block(this, "Allocate String object {"))
            { GenerateCodeForStatement(stringInstance.Allocator); }

            using (Code.Block(this, "Set string data {"))
            {
                for (int i = 0; i < stringInstance.Value.Length; i++)
                {
                    // Prepare value
                    int valueAddress = Stack.Push((byte)stringInstance.Value[i]);
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
                    Code.AddValue(pointerAddressCopy, stringInstance.Value.Length);

                    // Set value
                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }
            }
            return pointerAddress;
        }
    }

    bool IsFunctionInlineable(FunctionThingDefinition function, IEnumerable<CompiledPassedArgument> parameters)
    {
        if (function.Block is null ||
            !function.IsInlineable)
        { return false; }

        foreach (CompiledPassedArgument parameter in parameters)
        {
            if (parameter.Value is CompiledEvaluatedValue)
            { continue; }
            return false;
        }

        return true;
    }

    void GenerateCodeForFunction(CompiledFunctionDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated caller)
    {
        if (!AllowFunctionInlining ||
            !IsFunctionInlineable(function, parameters))
        {
            GenerateCodeForFunction_(function, parameters, typeArguments, caller);
            return;
        }

        GenerateCodeForFunction_(function, parameters, typeArguments, caller);
    }

    void GenerateCodeForFunction(ICompiledFunctionDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated caller)
    {
        switch (function)
        {
            case CompiledFunctionDefinition v: GenerateCodeForFunction(v, parameters, typeArguments, caller); break;
            case CompiledOperatorDefinition v: GenerateCodeForFunction(v, parameters, typeArguments, caller); break;
            case CompiledGeneralFunctionDefinition v: GenerateCodeForFunction(v, parameters, typeArguments, caller); break;
            case CompiledConstructorDefinition v: GenerateCodeForFunction(v, parameters, typeArguments, (CompiledConstructorCall)caller); break;
        }
    }

    void GenerateCodeForParameterPassing<TFunction>(TFunction function, ImmutableArray<CompiledPassedArgument> parameters, Stack<BrainfuckVariable> compiledParameters, Dictionary<string, GeneralType>? typeArguments)
        where TFunction : ICompiledFunctionDefinition, ISimpleReadable
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            CompiledPassedArgument passed = parameters[i];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = passed.Type;

            if (passedType.GetSize(this, Diagnostics, passed) != definedType.GetSize(this, Diagnostics, defined))
            { Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed)); }

            foreach (BrainfuckVariable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Identifier == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, defined.File));
                    break;
                }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            { Diagnostics.Add(Diagnostic.Critical($"Bruh", defined.Identifier, defined.File)); }

            if (passed.Value is CompiledAddressGetter addressGetter)
            {
                if (!GetVariable(addressGetter.Of, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(addressGetter.Of));
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

                CompiledVariableDeclaration variableDeclaration = defined.ToVariable(definedType, passed);
                PointerType parameterType = new(v.Type);
                compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, parameterType.GetSize(this, Diagnostics, passed), variableDeclaration)
                {
                    IsInitialized = true,
                });
                continue;
            }

            if (definedType.Is<PointerType>() && passed.Value is CompiledParameterGetter _parameterGetter)
            {
                if (!GetVariable(_parameterGetter, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(_parameterGetter));
                    return;
                }

                if (v.IsReference)
                {
                    CompiledVariableDeclaration variableDeclaration = defined.ToVariable(definedType, passed);
                    PointerType parameterType = new(v.Type);
                    compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, parameterType.GetSize(this, Diagnostics, passed), variableDeclaration)
                    {
                        IsInitialized = true,
                    });
                    continue;
                }
            }

            if (definedType.Is<PointerType>() && passed.Value is CompiledVariableGetter _variableGetter)
            {
                if (!GetVariable(_variableGetter, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(_variableGetter));
                    return;
                }

                if (v.IsReference)
                {
                    CompiledVariableDeclaration variableDeclaration = defined.ToVariable(definedType, passed);
                    PointerType parameterType = new(v.Type);
                    compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, parameterType.GetSize(this, Diagnostics, passed), variableDeclaration)
                    {
                        IsInitialized = true,
                    });
                    continue;
                }
            }

            // if (GetVariable(passed.Value, out BrainfuckVariable? variable, out _) &&
            //     variable.IsReference)
            // {
            //     if (!CanCastImplicitly(variable.Type, definedType, null, this, out PossibleDiagnostic? castError))
            //     {
            //         Diagnostics.Add(Diagnostic.Critical(
            //             $"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{variable.Type}\"",
            //             passed,
            //             castError.ToError(passed)));
            //     }
            // 
            //     var variableDeclaration = defined.ToVariable2(passed);
            //     compiledParameters.Push(new BrainfuckVariable(variable.Address, true, false, null, variable.Type, variable.Type.GetSize(this, Diagnostics, passed), variableDeclaration.Variable)
            //     {
            //         IsInitialized = variable.IsInitialized,
            //     });
            //     continue;
            // }

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
                CompiledVariableDeclaration variableDeclaration = defined.ToVariable(definedType, passed);
                using (TypeArgumentsScope g = SetTypeArgumentsScope(typeArguments))
                { PrecompileVariable(compiledParameters, variableDeclaration, false, definedType); }

                BrainfuckVariable? compiledParameter = null;
                foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Identifier == defined.Identifier.Content)
                    {
                        compiledParameter = compiledParameter_;
                    }
                }

                if (compiledParameter is null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" not found", defined.Identifier, defined.File));
                    return;
                }

                // if (!compiledParameter.Type.SameAs(definedType))
                // {
                //     Diagnostics.Add(Diagnostic.Warning($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{compiledParameter.Type}\"", passed));
                //     return;
                // }

                using (Code.Block(this, $"SET \"{defined.Identifier.Content}\" TO _something_"))
                {
                    GenerateCodeForStatement(passed.Value, definedType);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
            }
        }
    }

    void GenerateCodeForFunction_(CompiledFunctionDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledFunctionDefinition> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledPassedArgument parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledPassedArgument parameter in parameters)
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
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
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
            CompiledVariableDeclaration variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = function.Type,
                Cleanup = new CompiledCleanup()
                {
                    TrashType = function.Type,
                    Location = function.Location,
                },
            };
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(function.Type.GetSize(this, Diagnostics, function), callerPosition), false, false, null, function.Type.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), StatementCompiler.FindControlFlowUsage(FunctionBodies[function]));

        GenerateCodeForStatement(FunctionBodies[function]);

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
                    if (variable.Cleanup is not null &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(variable);
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

    void GenerateCodeForFunction(CompiledOperatorDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledOperatorDefinition> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledPassedArgument parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledPassedArgument parameter in parameters)
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
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
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
            CompiledVariableDeclaration variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = function.Type,
                Cleanup = new CompiledCleanup()
                {
                    TrashType = function.Type,
                    Location = function.Location,
                },
            };
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(function.Type.GetSize(this, Diagnostics, function), callerPosition), false, false, null, function.Type.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), StatementCompiler.FindControlFlowUsage(FunctionBodies[function]));

        GenerateCodeForStatement(FunctionBodies[function]);

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
                    if (variable.Cleanup is not null &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(variable);
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
                    if (variable.Cleanup is not null &&
                        variable.Type.Is<PointerType>())
                    { }
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledGeneralFunctionDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        using DebugFunctionBlock<CompiledOperatorDefinition> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        BrainfuckVariable? returnVariable = null;

        if (function.ReturnSomething)
        {
            CompiledVariableDeclaration variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = function.Type,
                Cleanup = new CompiledCleanup()
                {
                    TrashType = function.Type,
                    Location = function.Location,
                },
            };
            GeneralType returnType = GeneralType.InsertTypeParameters(function.Type, typeArguments) ?? function.Type;
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(returnType.GetSize(this, Diagnostics, function), callerPosition), false, false, null, returnType.GetSize(this, Diagnostics, function), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", function));
            return;
        }

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), StatementCompiler.FindControlFlowUsage(FunctionBodies[function]));

        GenerateCodeForStatement(FunctionBodies[function]);

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
                    if (variable.Cleanup is not null &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(variable);
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

    void GenerateCodeForFunction(CompiledConstructorDefinition function, ImmutableArray<CompiledPassedArgument> parameters, Dictionary<string, GeneralType>? typeArguments, CompiledConstructorCall callerPosition)
    {
        using DebugFunctionBlock<CompiledOperatorDefinition> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount - 1 != parameters.Length)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to constructor \"{function.ToReadable()}\" (required {function.ParameterCount - 1} passed {parameters.Length})", callerPosition));
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

        CurrentMacro.Push(function);

        int newInstanceAddress = Stack.NextAddress;
        GeneralType newInstanceType = callerPosition.Type;
        GenerateCodeForStatement(callerPosition.Object);

        if (newInstanceType.Is<PointerType>(out PointerType? newInstancePointerType))
        {
            if (!newInstancePointerType.To.Is<StructType>())
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong type \"{newInstanceType}\" used for constructor", callerPosition));
                return;
            }

            compiledParameters.Add(new BrainfuckVariable(newInstanceAddress, false, false, null, PointerSize, new CompiledVariableDeclaration()
            {
                Identifier = function.Parameters[0].Identifier.Content,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = newInstanceType,
                Cleanup = new CompiledCleanup()
                {
                    TrashType = newInstanceType,
                    Location = function.Location,
                },
            }));
        }
        else if (newInstanceType.Is<StructType>())
        {
            compiledParameters.Add(new BrainfuckVariable(newInstanceAddress, true, false, null, PointerSize, new CompiledVariableDeclaration()
            {
                Identifier = function.Parameters[0].Identifier.Content,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = new PointerType(newInstanceType),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = new PointerType(newInstanceType),
                    Location = function.Location,
                },
            }));
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong type \"{newInstanceType}\" used for constructor", callerPosition));
            return;
        }

        for (int i = 1; i < function.Parameters.Count; i++)
        {
            CompiledPassedArgument passed = parameters[i - 1];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = passed.Type;

            if (!passedType.SameAs(definedType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed));
                return;
            }

            foreach (BrainfuckVariable compiledParameter2 in compiledParameters)
            {
                if (compiledParameter2.Identifier == defined.Identifier.Content)
                {
                    Diagnostics.Add(Diagnostic.Critical($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, defined.File));
                    return;
                }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            {
                Diagnostics.Add(Diagnostic.Critical($"Bruh", defined.Identifier, defined.File));
                return;
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

            CompiledVariableDeclaration variableDeclaration2 = defined.ToVariable(definedType, passed);
            PrecompileVariable(compiledParameters, variableDeclaration2, false);

            BrainfuckVariable? compiledParameter = null;
            foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
            {
                if (compiledParameter_.Identifier == defined.Identifier.Content)
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
                GenerateCodeForStatement(passed.Value);

                using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                { Stack.PopAndStore(compiledParameter.Address); }
            }
        }

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushRange(compiledParameters);

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Block.Brackets.Start.Position, function.Block.File), StatementCompiler.FindControlFlowUsage(FunctionBodies[function]));

        GenerateCodeForStatement(FunctionBodies[function]);

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
                    if (variable.Cleanup is not null &&
                        variable.Type.Is<PointerType>())
                    {
                        GenerateDestructor(variable);
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
                if (MaxRecursiveDepth >= depth) continue;

                GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed), callerPosition);
                GenerateCodeForPrinter($"Max recursivity depth ({MaxRecursiveDepth}) exceeded (\"{function.ToReadable()}\")", callerPosition);
                GenerateCodeForPrinter(Ansi.Reset, callerPosition);
                return false;
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

    ControlFlowBlock? BeginReturnBlock(ILocated location, StatementCompiler.ControlFlowUsage usage)
    {
        if (usage.HasFlag(StatementCompiler.ControlFlowUsage.ConditionalReturn) || !AllowOtherOptimizations)
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
        else if (usage.HasFlag(StatementCompiler.ControlFlowUsage.Return))
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

    ControlFlowBlock? BeginBreakBlock(ILocated location, StatementCompiler.ControlFlowUsage usage)
    {
        if ((usage & StatementCompiler.ControlFlowUsage.Break) == StatementCompiler.ControlFlowUsage.None)
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
