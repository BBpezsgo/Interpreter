using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Brainfuck.Generator;

public partial class CodeGeneratorForBrainfuck : CodeGenerator
{
    bool AllowFunctionInlining => !Settings.DontOptimize;
    bool AllowPrecomputing => !Settings.DontOptimize;
    bool AllowOtherOptimizations => !Settings.DontOptimize;

    GeneratorStatistics _statistics;

    void GenerateDestructor(CompiledExpression value, CompiledCleanup cleanup)
    {
        if (cleanup.Destructor is not null)
        {
            GenerateCodeForFunction(cleanup.Destructor.Template, ImmutableArray.Create(CompiledArgument.Wrap(value)), cleanup.Destructor.TypeArguments, value);

            if (cleanup.Destructor.Template.ReturnSomething)
            { Stack.Pop(); }
        }

        if (StatementCompiler.AllowDeallocate(cleanup.TrashType))
        {
            if (cleanup.Deallocator is not null)
            {
                GenerateCodeForFunction(cleanup.Deallocator.Template, ImmutableArray.Create(CompiledArgument.Wrap(value)), cleanup.Deallocator.TypeArguments, value);
            }
        }
    }

    void GenerateDestructor(BrainfuckVariable variable)
    {
        if (variable.Cleanup is null) return;
        GenerateDestructor(
            new CompiledVariableAccess()
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
    int PrecompileVariables(CompiledBlock block, bool ignoreRedefinition, List<Runtime.StackElementInformation>? debugInfo = null)
    { return PrecompileVariables(block.Statements, ignoreRedefinition, debugInfo); }
    int PrecompileVariables(IEnumerable<CompiledStatement>? statements, bool ignoreRedefinition, List<Runtime.StackElementInformation>? debugInfo = null)
    {
        if (statements is null) return 0;

        int result = 0;
        foreach (CompiledStatement statement in statements)
        { result += PrecompileVariables(statement, ignoreRedefinition, debugInfo); }
        return result;
    }
    int PrecompileVariables(CompiledStatement statement, bool ignoreRedefinition, List<Runtime.StackElementInformation>? debugInfo = null)
    {
        if (statement is not CompiledVariableDefinition instruction)
        { return 0; }

        return PrecompileVariable(instruction, ignoreRedefinition, debugInfo);
    }
    int PrecompileVariable(CompiledVariableDefinition variableDeclaration, bool ignoreRedefinition, List<Runtime.StackElementInformation>? debugInfo = null)
        => PrecompileVariable(CompiledVariables, variableDeclaration, ignoreRedefinition, null, debugInfo);
    int PrecompileVariable(Stack<BrainfuckVariable> variables, CompiledVariableDefinition variableDeclaration, bool ignoreRedefinition, GeneralType? type = null, List<Runtime.StackElementInformation>? debugInfo = null)
    {
        //if (variables.Any(other =>
        //        other.Identifier == variableDeclaration.Identifier &&
        //        other.File == variableDeclaration.Location.File))
        //{
        //    if (ignoreRedefinition) return 0;
        //    Diagnostics.Add(Diagnostic.Error($"Variable \"{variableDeclaration.Identifier}\" already defined", variableDeclaration));
        //}

        if (type is null)
        {
            type = variableDeclaration.Type;
        }

        if (variableDeclaration.InitialValue is not null)
        {
            GeneralType initialValueType = variableDeclaration.InitialValue.Type;

            if (FindSize(initialValueType, variableDeclaration.InitialValue) != FindSize(type, variableDeclaration))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Variable initial value type (\"{initialValueType}\") and variable type (\"{type}\") mismatch", variableDeclaration.InitialValue));
                return default;
            }

            if (type.Is(out ArrayType? arrayType))
            {
                if (arrayType.Of.SameAs(BasicType.U16) &&
                    variableDeclaration.InitialValue is CompiledString literal)
                {
                    if (arrayType.Length is not null)
                    {
                        if (!arrayType.Length.HasValue)
                        {
                            Diagnostics.Add(DiagnosticAt.Error($"Literal length {literal.Value.Length} must be equal to the stack array length <runtime value>", literal));
                            return default;
                        }
                        if (literal.Value.Length != arrayType.Length.Value)
                        {
                            Diagnostics.Add(DiagnosticAt.Error($"Literal length {literal.Value.Length} must be equal to the stack array length {arrayType.Length.Value}", literal));
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
                        debugInfo?.Add(new()
                        {
                            Identifier = variableDeclaration.Identifier,
                            Address = address2,
                            Size = size,
                            Kind = Runtime.StackElementKind.Variable,
                            Type = variableDeclaration.Type,
                        });

                        for (int i = 0; i < literal.Value.Length; i++)
                        { Code.ARRAY_SET_CONST(address2, i, new CompiledValue(literal.Value[i])); }
                    }
                }
                else
                {
                    if (!arrayType.Length.HasValue)
                    {
                        Diagnostics.Add(DiagnosticAt.Error($"This aint supported", variableDeclaration));
                        return default;
                    }

                    int arraySize = arrayType.Length.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    int address2 = Stack.PushVirtual(size, variableDeclaration);
                    variables.Push(new BrainfuckVariable(address2, false, true, variableDeclaration.Cleanup, size, variableDeclaration));
                    debugInfo?.Add(new()
                    {
                        Identifier = variableDeclaration.Identifier,
                        Address = address2,
                        Size = size,
                        Kind = Runtime.StackElementKind.Variable,
                        Type = variableDeclaration.Type,
                    });
                }
                return 1;
            }

            if (variableDeclaration.InitialValue is CompiledGetReference addressGetter &&
                GetVariable(addressGetter.Of, out BrainfuckVariable? shadowingVariable, out _) &&
                type.Is(out PointerType? pointerType))
            {
                if (!StatementCompiler.CanCastImplicitly(pointerType.To, shadowingVariable.Type, out PossibleDiagnostic? castError, out _))
                { Diagnostics.Add(castError.ToError(variableDeclaration.InitialValue)); }

                variables.Push(new BrainfuckVariable(shadowingVariable.Address, true, false, null, FindSize(type, variableDeclaration), variableDeclaration)
                {
                    IsInitialized = true
                });
                return 0;
            }

            int address = Stack.PushVirtual(FindSize(type, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, variableDeclaration.Cleanup, FindSize(type, variableDeclaration), variableDeclaration));
            debugInfo?.Add(new()
            {
                Identifier = variableDeclaration.Identifier,
                Address = address,
                Size = FindSize(type, variableDeclaration),
                Kind = Runtime.StackElementKind.Variable,
                Type = variableDeclaration.Type,
            });
            return 1;
        }
        else
        {
            if (type.Is(out ArrayType? arrayType))
            {
                if (!arrayType.Length.HasValue)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"This aint supported", variableDeclaration));
                    return default;
                }

                int arraySize = arrayType.Length.Value;

                int size = Snippets.ARRAY_SIZE(arraySize);

                int address2 = Stack.PushVirtual(size, variableDeclaration);
                variables.Push(new BrainfuckVariable(address2, false, true, variableDeclaration.Cleanup, size, variableDeclaration));
                debugInfo?.Add(new()
                {
                    Identifier = variableDeclaration.Identifier,
                    Address = address2,
                    Size = size,
                    Kind = Runtime.StackElementKind.Variable,
                    Type = variableDeclaration.Type,
                });
                return 1;
            }

            int address = Stack.PushVirtual(FindSize(type, variableDeclaration), variableDeclaration);
            variables.Push(new BrainfuckVariable(address, false, true, variableDeclaration.Cleanup, FindSize(type, variableDeclaration), variableDeclaration));
            debugInfo?.Add(new()
            {
                Identifier = variableDeclaration.Identifier,
                Address = address,
                Size = FindSize(type, variableDeclaration),
                Kind = Runtime.StackElementKind.Variable,
                Type = variableDeclaration.Type,
            });
            return 1;
        }
    }
    #endregion

    #region Find Size

    protected override bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        error = null;
        size = Snippets.ARRAY_SIZE(type.Length.Value);
        return true;
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
        EnumType v => GenerateSize(v, result, out error),
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

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        if (elementSize != 1)
        {
            error = new PossibleDiagnostic($"Array element size must be 1 byte");
            return false;
        }

        Code.AddValue(result, (2 * type.Length.Value) + 3);

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
    bool GenerateSize(EnumType type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Definition.Type, result, out error);

    bool GenerateSize(CompiledTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        CompiledPointerTypeExpression v => GenerateSize(v, result, out error),
        CompiledReferenceTypeExpression v => GenerateSize(v, result, out error),
        CompiledArrayTypeExpression v => GenerateSize(v, result, out error),
        CompiledFunctionTypeExpression v => GenerateSize(v, result, out error),
        CompiledStructTypeExpression v => GenerateSize(v, result, out error),
        CompiledGenericTypeExpression v => GenerateSize(v, result, out error),
        CompiledBuiltinTypeExpression v => GenerateSize(v, result, out error),
        CompiledAliasTypeExpression v => GenerateSize(v, result, out error),
        CompiledEnumTypeExpression v => GenerateSize(v, result, out error),
        _ => throw new NotImplementedException(),
    };
    bool GenerateSize(CompiledPointerTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.AddValue(result, 1);
        return true;
    }
    bool GenerateSize(CompiledReferenceTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.AddValue(result, 1);
        return true;
    }
    bool GenerateSize(CompiledArrayTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (FindSize(type, out int size, out error))
        {
            Code.AddValue(result, size);
            return true;
        }

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size", type);
            return false;
        }

        if (FindSize(type.Length.Type, type.Length) != 1)
        {
            error = new PossibleDiagnostic($"Array length must be 1 byte", type.Length);
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error))
        {
            return false;
        }

        if (elementSize != 1)
        {
            error = new PossibleDiagnostic($"Array element size must be 1 byte", type.Of);
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
    bool GenerateSize(CompiledFunctionTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        Code.AddValue(result, 1);
        return true;
    }
    bool GenerateSize(CompiledStructTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(type, out int size, out error))
        { return false; }
        Code.AddValue(result, size);
        return true;
    }
    bool GenerateSize(CompiledGenericTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");
    bool GenerateSize(CompiledBuiltinTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!FindSize(new BuiltinType(type.Type), out int size, out error))
        { return false; }
        Code.AddValue(result, size);
        return true;
    }
    bool GenerateSize(CompiledAliasTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Value, result, out error);
    bool GenerateSize(CompiledEnumTypeExpression type, int result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GenerateSize(type.Definition.Type, result, out error);

    #endregion

    #region GenerateCodeForSetter()

    void GenerateCodeForSetter(CompiledSetter _statement)
    {
        if (_statement.Target is CompiledVariableAccess targetVariable)
        {
            if (!GetVariable(targetVariable.Variable.Identifier, targetVariable.Variable.Location.File, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(targetVariable.Variable));
                return;
            }

            GenerateCodeForSetter(variable, _statement.Value);
        }
        else if (_statement.Target is CompiledParameterAccess targetParameter)
        {
            if (!GetVariable(targetParameter.Parameter.Identifier, targetParameter.Parameter.Definition.File, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(targetParameter.Parameter.Definition));
                return;
            }

            GenerateCodeForSetter(variable, _statement.Value);
        }
        else if (_statement.Target is CompiledFieldAccess targetField)
        {
            GenerateCodeForSetter(targetField, _statement.Value);
        }
        else if (_statement.Target is CompiledDereference targetDereference)
        {
            GenerateCodeForSetter(targetDereference, _statement.Value);
        }
        else if (_statement.Target is CompiledElementAccess targetElement)
        {
            GenerateCodeForSetter(targetElement, _statement.Value);
        }
        else
        {
            throw new NotImplementedException(_statement.Target.GetType().Name);
        }
    }
    void GenerateCodeForSetter(CompiledFieldAccess field, CompiledExpression value)
    {
        if ((
            GetVariable(field.Object, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            field.Object.Type.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is<StructType>()
        ) ||
            field.Object.Type.Is<StructType>())
        {
            // if (!fieldSetter.Type.SameAs(value.Type))
            // {
            //     Diagnostics.Add(Diagnostic.Error($"Can not set a \"{value.Type}\" type value to the \"{fieldSetter.Type}\" type field.", value));
            //     return;
            // }

            if (!TryGetAddress(field, out Address? address, out int size))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Failed to get field address", field));
                return;
            }

            if (size != FindSize(value.Type, value))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Field and value size mismatch", value));
                return;
            }

            CompileSetter(address, value);
            return;
        }

        if (field.Object.Type.Is(out IReferenceType? referenceType))
        {
            if (!referenceType.To.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Could not get the field offsets of type \"{referenceType}\"", field.Object));
                return;
            }

            if (!GetFieldOffset(structPointerType, field.Field.Identifier, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field));
                return;
            }

            if (FindSize(field.Type, field) != FindSize(value.Type, value))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Field and value size mismatch", value));
                return;
            }

            int _pointerAddress = Stack.NextAddress;
            using (DebugInfoBlock debugBlock = DebugBlock(field))
            {
                GenerateCodeForStatement(field.Object);
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
    void GenerateCodeForSetter(BrainfuckVariable variable, CompiledExpression value)
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
                Diagnostics.Add(DiagnosticAt.Error($"Variable \"{valueVariable.Identifier}\" is discarded", value));
                return;
            }

            if (variable.Size != valueVariable.Size)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Variable and value size mismatch ({variable.Size} != {valueVariable.Size})", value));
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

                    if (AllowPrecomputing && valueBinaryOperator.Right is CompiledConstantValue constantValue)
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

                    if (AllowPrecomputing && valueBinaryOperator.Right is CompiledConstantValue constantValue)
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
            int valueSize = FindSize(value.Type, value);

            if (variable.Type.Is(out ArrayType? arrayType))
            {
                if (value is CompiledStackString literal)
                {
                    if (!literal.IsUTF8)
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
                else if (value is CompiledList literalList &&
                         arrayType.Length.HasValue &&
                         arrayType.Length.Value == literalList.Values.Length)
                {
                    if (FindSize(arrayType.Of, value) != 1)
                    { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", value); }

                    int arraySize = arrayType.Length.Value;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    using StackAddress indexAddress = Stack.GetTemporaryAddress(1, value);
                    using StackAddress valueAddress = Stack.GetTemporaryAddress(1, value);
                    {
                        for (int i = 0; i < literalList.Values.Length; i++)
                        {
                            if (literalList.Values[i] is CompiledConstantValue elementValue)
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
    void GenerateCodeForSetter(CompiledDereference dereference, CompiledExpression value)
    {
        if (dereference.Address is CompiledVariableAccess variableGetter)
        {
            if (!GetVariable(variableGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableGetter));
                return;
            }

            if (variable.IsDiscarded)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Variable \"{variable.Identifier}\" is discarded", variableGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Bruh", variableGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForSetter(variable, value);
                return;
            }
        }

        if (dereference.Address is CompiledParameterAccess parameterGetter)
        {
            if (!GetVariable(parameterGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(parameterGetter));
                return;
            }

            if (variable.IsDiscarded)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Variable \"{variable.Identifier}\" is discarded", parameterGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Bruh", parameterGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForSetter(variable, value);
                return;
            }
        }

        CompileDereferencedSetter(dereference.Address, 0, value);
    }
    void CompileSetter(Address address, CompiledExpression value)
    {
        switch (address.Simplify())
        {
            case AddressAbsolute v: CompileSetter(v, value); break;
            case AddressOffset v: CompileSetter(v, value); break;
            default: throw new NotImplementedException();
        }
    }
    void CompileSetter(AddressAbsolute address, CompiledExpression value)
    {
        using (Code.Block(this, $"Set value \"{value}\" to address {address}"))
        {
            if (AllowPrecomputing && value is CompiledConstantValue constantValue)
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
    void CompileSetter(AddressOffset address, CompiledExpression value)
    {
        if (address.Base is AddressRuntimePointer runtimePointer2)
        {
            GeneralType referenceType = runtimePointer2.PointerValue.Type;

            if (!referenceType.Is(out PointerType? _))
            {
                Diagnostics.Add(DiagnosticAt.Error($"This isn't a pointer", runtimePointer2.PointerValue));
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

            // todo: this
            // AssignTypeCheck(pointerType.To, valueType, value);

            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            if (FindSize(valueType, value) == 1 && AllowOtherOptimizations)
            {
                Heap.Set(pointerAddress, valueAddress);
            }
            else
            {
                using StackAddress tempPointerAddress = Stack.PushVirtual(1, value);
                for (int i = 0; i < FindSize(valueType, value); i++)
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
    void CompileDereferencedSetter(CompiledExpression dereference, int offset, CompiledExpression value)
    {
        if (!dereference.Type.Is(out PointerType? _))
        {
            Diagnostics.Add(DiagnosticAt.Error($"This isn't a pointer", dereference));
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

        int size = FindSize(value.Type, value);

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
    void GenerateCodeForSetter(CompiledElementAccess elementAccess, CompiledExpression value)
    {
        if ((
            GetVariable(elementAccess.Base, out BrainfuckVariable? variable, out _) &&
            variable.IsReference &&
            elementAccess.Base.Type.Is(out PointerType? stackPointerType) &&
            stackPointerType.To.Is(out ArrayType? arrayType)
        ) ||
            elementAccess.Base.Type.Is(out arrayType)
        )
        {
            if (!TryGetAddress(elementAccess.Base, out Address? arrayAddress, out _))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Failed to get array address", elementAccess.Base));
                return;
            }

            if (variable is not null)
            {
                if (variable.IsDiscarded)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Variable \"{variable.Identifier}\" is discarded", elementAccess.Base));
                    return;
                }
            }

            using (Code.Block(this, $"Set array (\"{elementAccess.Base}\") index (\"{elementAccess.Index}\") (at {arrayAddress}) to \"{value}\""))
            {
                GeneralType elementType = arrayType.Of;

                if (!elementType.SameAs(value.Type))
                {
                    Diagnostics.Add(DiagnosticAt.Error("Bruh", value));
                    return;
                }

                int elementSize = FindSize(elementType, elementAccess.Base);

                if (elementSize != 1)
                { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", elementAccess); }

                int indexAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute index"))
                { GenerateCodeForStatement(elementAccess.Index); }

                int valueAddress = Stack.NextAddress;
                using (Code.Block(this, $"Compute value"))
                { GenerateCodeForStatement(value); }

                if (arrayAddress is not AddressAbsolute arrayAddressAbs)
                { throw new NotImplementedException(); }

                Code.ARRAY_SET(arrayAddressAbs.Value, indexAddress, valueAddress, v => Stack.GetTemporaryAddress(v, elementAccess));

                Stack.Pop();
                Stack.Pop();
            }

            return;
        }

        if (elementAccess.Base.Type.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            if (!arrayType.Of.SameAs(value.Type))
            {
                Diagnostics.Add(DiagnosticAt.Error("Bruh", value));
                return;
            }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(elementAccess.Base);

            if (!elementAccess.Index.Type.Is<BuiltinType>())
            {
                Diagnostics.Add(DiagnosticAt.Error($"Index type must be built-in (ie. \"i32\") and not \"{elementAccess.Index.Type}\"", elementAccess.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(elementAccess.Index);

            if (FindSize(arrayType.Of, elementAccess.Base) != 1)
            {
                using StackAddress multiplierAddress = Stack.Push(FindSize(arrayType.Of, elementAccess.Base));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, value));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Set(pointerAddress, valueAddress);

            Stack.Pop(); // pointerAddress
            Stack.Pop(); // valueAddress

            return;
        }

        Diagnostics.Add(DiagnosticAt.Error("WHAT", elementAccess));
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
            case CompiledConstantValue v: GenerateCodeForStatement(v); break;
            case CompiledVariableAccess v: GenerateCodeForStatement(v); break;
            case CompiledParameterAccess v: GenerateCodeForStatement(v); break;
            case CompiledBinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledUnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case CompiledGetReference v: GenerateCodeForStatement(v); break;
            case CompiledDereference v: GenerateCodeForStatement(v); break;
            case CompiledVariableDefinition v: GenerateCodeForStatement(v); break;
            case CompiledReinterpretation v: GenerateCodeForStatement(v); break;
            case CompiledCast v: GenerateCodeForStatement(v); break;
            case CompiledStackAllocation v: GenerateCodeForStatement(v); break;
            case CompiledConstructorCall v: GenerateCodeForStatement(v); break;
            case CompiledFieldAccess v: GenerateCodeForStatement(v); break;
            case CompiledElementAccess v: GenerateCodeForStatement(v); break;
            case CompiledRuntimeCall v: GenerateCodeForStatement(v); break;
            case CompiledBlock v: GenerateCodeForStatement(v); break;
            case CompiledSetter v: GenerateCodeForSetter(v); break;
            case CompiledDummyExpression v: GenerateCodeForStatement(v.Statement); break;
            case CompiledString v: GenerateCodeForStatement(v, expectedType); break;
            case CompiledStackString v: GenerateCodeForStatement(v, expectedType); break;
            case CompiledEmptyStatement: break;
            case CompiledGoto v: GenerateCodeForStatement(v); break;
            case CompiledCompilerVariableAccess v: GenerateCodeForStatement(v); break;
            case CompiledMeowExpression: break;
            default: throw new UnreachableException(statement.GetType().Name);
        }
    }
    void GenerateCodeForStatement(CompiledRuntimeCall anyCall)
    {
        throw new NotSupportedException($"Function pointers not supported by brainfuck", anyCall);
    }
    void GenerateCodeForStatement(CompiledGoto statement)
    {
        throw new NotSupportedException($"Goto statements not supported by brainfuck", statement);
    }
    void GenerateCodeForStatement(CompiledElementAccess indexCall)
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
                Diagnostics.Add(DiagnosticAt.Error($"Failed to get array address", indexCall.Base));
                return;
            }

            GeneralType elementType = arrayType.Of;

            int elementSize = FindSize(elementType, indexCall.Base);

            if (elementSize != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Array element size must be 1 :(", indexCall));
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
                Diagnostics.Add(DiagnosticAt.Error($"Index type must be built-in (ie. \"int\") and not \"{indexCall.Index.Type}\"", indexCall.Index));
                return;
            }

            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.Index);

            {
                using StackAddress multiplierAddress = Stack.Push(FindSize(arrayType.Of, indexCall.Base));
                Code.MULTIPLY(indexAddress, multiplierAddress, v => Stack.GetTemporaryAddress(v, indexCall.Index));
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Get(pointerAddress, resultAddress);

            Stack.Pop(); // pointerAddress
            return;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Index getter for type \"{indexCall.Base.Type}\" not found", indexCall));
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

            if (@if.Next is null)
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
                        { Diagnostics.Add(DiagnosticAt.Internal($"Unexpected conditional jump in the depths (this is the compiler's fault)", @while)); }
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
            VariableCleanupStack.Push(@for.Initialization is null ? 0 : PrecompileVariables(@for.Initialization, false));

            if (@for.Initialization is not null)
            {
                using (Code.Block(this, "Variable Declaration"))
                { GenerateCodeForStatement(@for.Initialization); }
            }

            int conditionAddress = Stack.NextAddress;
            if (@for.Condition is not null)
            {
                using (Code.Block(this, "Compute condition"))
                { GenerateCodeForStatement(@for.Condition); }
            }
            else
            {
                Stack.Push(true, @for);
            }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(@for.Body.Location.Before(), StatementCompiler.FindControlFlowUsage(@for.Body));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(CompiledBlock.CreateIfNot(@for.Body));
                }

                if (@for.Step is not null)
                {
                    using (Code.Block(this, "Compute expression"))
                    {
                        GenerateCodeForStatement(@for.Step);
                    }
                }

                if (@for.Condition is not null)
                {
                    using (Code.Block(this, "Compute condition again"))
                    {
                        GenerateCodeForStatement(@for.Condition);
                        Stack.PopAndStore(conditionAddress);
                    }
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
                        { Diagnostics.Add(DiagnosticAt.Internal($"Unexpected conditional jump in the depths (this is the compiler's fault)", @for)); }
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
                Diagnostics.Add(DiagnosticAt.Internal($"Can't return value for some reason :(", statement).WithSuberrors(notFoundError.ToError(statement)));
                return;
            }

            GenerateCodeForSetter(returnVariable, statement.Value);
        }

        if (Returns.Count == 0)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Can't return for some reason :(", statement));
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
            Diagnostics.Add(DiagnosticAt.Error($"Looks like this is not inside a loop", statement));
            return;
        }

        if (!Breaks.Last.FlagAddress.HasValue)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Looks like this is not inside a loop", statement));
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
    void GenerateCodeForStatement(CompiledVariableDefinition statement)
    {
        if (statement.InitialValue is null) return;

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

        switch (functionCall.Function.Template)
        {
            case CompiledFunctionDefinition v: GenerateCodeForFunction(v, functionCall.Arguments, functionCall.Function.TypeArguments, functionCall); break;
            case CompiledOperatorDefinition v: GenerateCodeForFunction(v, functionCall.Arguments, functionCall.Function.TypeArguments, functionCall); break;
            case CompiledGeneralFunctionDefinition v: GenerateCodeForFunction(v, functionCall.Arguments, functionCall.Function.TypeArguments, functionCall); break;
            default: throw new UnreachableException(functionCall.Function.Template.GetType().Name);
        }

        if (!functionCall.SaveValue && functionCall.Function.Template.ReturnSomething)
        { Stack.Pop(); }
    }
    void GenerateCodeForStatement(CompiledExternalFunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (functionCall.Function.Name == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledArgument parameter in functionCall.Arguments)
            {
                if (!CanGenerateCodeForPrinter(parameter.Value))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledArgument parameter in functionCall.Arguments)
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
                if (FindSize(functionCall.Declaration.Type, functionCall) != 1)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Function with attribute \"[{AttributeConstants.ExternalIdentifier}(\"{ExternalFunctionNames.StdIn}\")]\" must have a return type with size of 1", (functionCall.Declaration.Definition as FunctionDefinition)?.Type.Position ?? functionCall.Declaration.Definition.Position, functionCall.Declaration.File));
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
        GenerateCodeForFunction(constructorCall.Function.Template, constructorCall.Arguments, constructorCall.Function.TypeArguments, constructorCall);
    }
    void GenerateCodeForStatement(CompiledString statement, GeneralType? expectedType = null)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Set \"{statement}\" to address {Stack.NextAddress}"))
        {
            if (expectedType is not null &&
                expectedType.Is(out PointerType? pointerType) &&
                pointerType.To.SameAs(BasicType.U16))
            {
                // todo: not true but false
                GenerateCodeForLiteralString(statement);
            }
            else
            {
                GenerateCodeForLiteralString(statement);
            }
        }
    }
    void GenerateCodeForStatement(CompiledStackString statement, GeneralType? expectedType = null)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        throw new NotImplementedException();
    }
    void GenerateCodeForStatement(CompiledConstantValue evaluatedValue)
    {
        using DebugInfoBlock debugBlock = DebugBlock(evaluatedValue);

        using (Code.Block(this, $"Set \"{evaluatedValue}\" to address {Stack.NextAddress}"))
        {
            Stack.Push(evaluatedValue.Value);
        }
    }
    void GenerateCodeForStatement(CompiledCompilerVariableAccess statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        switch (statement.Identifier)
        {
            case "heap_start":
                Diagnostics.Add(DiagnosticAt.Warning($"This is not suppored by Brainfuck, defaulting to 0", statement));
                Stack.Push(0);
                break;
            default:
                Diagnostics.Add(DiagnosticAt.Error($"Unknown compiler variable `{statement.Identifier}`", statement));
                break;
        }
    }
    void GenerateCodeForStatement(CompiledVariableAccess statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (!GetVariable(statement, out BrainfuckVariable? variable, out PossibleDiagnostic? variableNotFoundError))
        { throw new NotImplementedException(); }

        GenerateCodeForStatement(variable, statement);
    }
    void GenerateCodeForStatement(CompiledParameterAccess statement)
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
            Diagnostics.Add(DiagnosticAt.Error($"Variable \"{variable.Identifier}\" is discarded", statement));
            return;
        }

        if (variable.Size <= 0)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Can't load variable \"{variable.Identifier}\" because it's size is {variable.Size} (bruh)", statement));
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
                case BinaryOperatorCallExpression.CompEQ:
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
                case BinaryOperatorCallExpression.Addition:
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
                case BinaryOperatorCallExpression.Subtraction:
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
                case BinaryOperatorCallExpression.Multiplication:
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
                case BinaryOperatorCallExpression.Division:
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
                case BinaryOperatorCallExpression.Modulo:
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
                case BinaryOperatorCallExpression.CompLT:
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
                case BinaryOperatorCallExpression.CompGT:
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
                case BinaryOperatorCallExpression.CompGEQ:
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
                case BinaryOperatorCallExpression.CompLEQ:
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
                case BinaryOperatorCallExpression.CompNEQ:
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
                case BinaryOperatorCallExpression.LogicalAND:
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
                case BinaryOperatorCallExpression.LogicalOR:
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
                case BinaryOperatorCallExpression.BitshiftLeft:
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (statement.Right is not CompiledConstantValue offsetConst)
                    {
                        Diagnostics.Add(DiagnosticAt.Error($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                        return;
                    }

                    if (offsetConst.Value != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.Value.I32))
                        {
                            Diagnostics.Add(DiagnosticAt.Error($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.Value.I32));

                        using (Code.Block(this, $"Snippet MULTIPLY({valueAddress} {offsetAddress})"))
                        { Code.MULTIPLY(valueAddress, offsetAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                    }

                    break;
                }
                case BinaryOperatorCallExpression.BitshiftRight:
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (statement.Right is not CompiledConstantValue offsetConst)
                    {
                        Diagnostics.Add(DiagnosticAt.Error($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                        return;
                    }

                    if (offsetConst.Value != 0)
                    {
                        if (!Utils.PowerOf2(offsetConst.Value.I32))
                        {
                            Diagnostics.Add(DiagnosticAt.Error($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                            return;
                        }

                        using StackAddress offsetAddress = Stack.Push((int)Math.Pow(2, offsetConst.Value.I32));

                        using (Code.Block(this, $"Snippet MATH_DIV({valueAddress} {offsetAddress})"))
                        { Code.MATH_DIV(valueAddress, offsetAddress, v => Stack.GetTemporaryAddress(v, statement)); }
                    }

                    break;
                }
                case BinaryOperatorCallExpression.BitwiseAND:
                {
                    GeneralType leftType = statement.Left.Type;

                    if ((leftType.SameAs(BasicType.U8) ||
                        leftType.SameAs(BasicType.I8) ||
                        leftType.SameAs(BasicType.U16) ||
                        leftType.SameAs(BasicType.I16) ||
                        leftType.SameAs(BasicType.U32) ||
                        leftType.SameAs(BasicType.I32)) &&
                        statement.Right is CompiledConstantValue right)
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

                    Diagnostics.Add(DiagnosticAt.Error($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement));
                    return;
                }
                default:
                    Diagnostics.Add(DiagnosticAt.Error(
                        $"I can't make \"{statement.Operator}\" operators to work in brainfuck",
                        statement));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledUnaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Expression \"{statement.Expression}\" \"{statement.Operator}\""))
        {
            switch (statement.Operator)
            {
                case UnaryOperatorCallExpression.LogicalNOT:
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Expression); }

                    Code.LOGIC_NOT(leftAddress, v => Stack.GetTemporaryAddress(v, statement));

                    break;
                }
                default:
                    Diagnostics.Add(DiagnosticAt.Error(
                        $"I can't make \"{statement.Operator}\" operators to work in brainfuck",
                        statement));
                    return;
            }
        }
    }
    void GenerateCodeForStatement(CompiledBlock block)
    {
        using IDisposableProgress<float>? progressBar = Logger?.Progress(LogType.Debug);

        Runtime.ScopeInformation scopeInformation = new()
        {
            Location = new Runtime.SourceCodeLocation()
            {
                Instructions = (Code.Length, Code.Length + 1),
                Location = block.Location,
            },
            Stack = new List<Runtime.StackElementInformation>(),
        };

        using (DebugBlock(block.Location.Before()))
        {
            VariableCleanupStack.Push(PrecompileVariables(block, false, scopeInformation.Stack));

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

            progressBar?.Report(i, block.Statements.Length);
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

        scopeInformation.Location.Instructions.End = Code.Length;
        DebugInfo?.ScopeInformation.Add(scopeInformation);

        if (branchDepth != Code.BranchDepth)
        { Diagnostics.Add(DiagnosticAt.Internal($"Unbalanced branches", block)); }
    }
    StackAddress GenerateAddressResolver(Address address, ILocated location)
    {
        switch (address.Simplify())
        {
            case AddressPointer runtimePointer:
            {
                return PushFrom(runtimePointer.PointerAddress, 1);
            }
            case AddressRegisterPointer: throw new UnreachableException();
            case AddressOffset addressOffset:
            {
                StackAddress res = GenerateAddressResolver(addressOffset.Base, location);
                CheckPointerNull(res);
                Code.AddValue(res, addressOffset.Offset);
                return res;
            }
            case AddressRuntimePointer runtimePointer:
            {
                int res = Stack.NextAddress;
                GenerateCodeForStatement(runtimePointer.PointerValue);
                return new StackAddress(Stack, res);
            }
            case AddressRuntimeIndex runtimeIndex:
            {
                StackAddress res = GenerateAddressResolver(runtimeIndex.Base, location);
                CheckPointerNull(res);

                GeneralType indexType = runtimeIndex.IndexValue.Type;

                if (!indexType.Is<BuiltinType>())
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Index type must be builtin (ie. \"int\") and not \"{indexType}\"", runtimeIndex.IndexValue));
                    return default;
                }

                if (!Settings.DontOptimize
                    && runtimeIndex.IndexValue is CompiledConstantValue evaluatedIndex)
                {
                    int indexValue = (int)evaluatedIndex.Value;
                    Code.AddValue(res, indexValue * runtimeIndex.ElementSize);
                }
                else
                {
                    int indexValue = Stack.NextAddress;
                    GenerateCodeForStatement(runtimeIndex.IndexValue);

                    Code.MULTIPLY(indexValue, runtimeIndex.ElementSize, v => Stack.GetTemporaryAddress(v, location));
                    Stack.PopAndAdd(res);
                }

                return res;
            }
            case AddressAbsolute absolute:
            {
                return Stack.Push(absolute.Value);
            }
            default: throw new NotImplementedException(address.GetType().Name);
        }
    }
    void GenerateCodeForStatement(CompiledGetReference addressGetter)
    {
        if (TryGetAddress(addressGetter.Of, out Address? address, out _))
        {
            GenerateAddressResolver(address, addressGetter);
        }
        else
        {
            Diagnostics.Add(DiagnosticAt.Error($"Failed to get address of {addressGetter.Of}", addressGetter));
        }
        //Diagnostics.Add(DiagnosticAt.Error($"This is when pointers to the stack isn't work in brainfuck", addressGetter));
        //return;
    }
    void GenerateCodeForStatement(CompiledDereference pointer)
    {
        using DebugInfoBlock debugBlock = DebugBlock(pointer);

        if (pointer.Address is CompiledVariableAccess variableGetter)
        {
            if (!GetVariable(variableGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(variableGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Bruh", variableGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForStatement(variable, pointer.Address);
                return;
            }
        }

        if (pointer.Address is CompiledParameterAccess compiledParameterGetter)
        {
            if (!GetVariable(compiledParameterGetter, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
            {
                Diagnostics.Add(notFoundError.ToError(compiledParameterGetter));
                return;
            }

            if (variable.Size != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Bruh", compiledParameterGetter));
                return;
            }

            if (variable.IsReference)
            {
                GenerateCodeForStatement(variable, pointer.Address);
                return;
            }
        }

        int pointerAddress = Stack.NextAddress;
        GenerateCodeForStatement(pointer.Address);

        Heap.Get(pointerAddress, pointerAddress);
    }
    void GenerateCodeForStatement(CompiledStackAllocation newInstance)
    {
        using DebugInfoBlock debugBlock = DebugBlock(newInstance);

        int address = Stack.PushVirtual(FindSize(newInstance.Type, newInstance), newInstance);
        int size = FindSize(newInstance.Type, newInstance.TypeExpression);

        for (int offset = 0; offset < size; offset++)
        {
            int offsettedAddress = address + offset;
            Code.SetValue(offsettedAddress, 0);
        }
    }
    void GenerateCodeForStatement(CompiledFieldAccess field)
    {
        using DebugInfoBlock debugBlock = DebugBlock(field);

        if (GetVariable(field.Object, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference &&
            TryGetAddress(field, out Address? address, out int size))
        {
            if (size <= 0)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Can't load field \"{field}\" because it's size is {size} (bruh)", field));
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
                Diagnostics.Add(DiagnosticAt.Error($"Could not get the field offsets of type \"{field.Object.Type}\"", field.Object));
                return;
            }

            if (!GetFieldOffset(structPointerType, field.Field.Identifier, out CompiledField? fieldDefinition, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field));
                return;
            }
            GeneralType fieldType = structPointerType.ReplaceType(fieldDefinition.Type, out PossibleDiagnostic? replaceError);
            if (replaceError is not null)
            {
                Diagnostics.Add(replaceError.ToError(field));
            }

            int resultAddress = Stack.Push(FindSize(fieldType, fieldDefinition.Definition));

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(field.Object);

            Code.AddValue(pointerAddress, fieldOffset);

            Heap.Get(pointerAddress, resultAddress, FindSize(fieldType, fieldDefinition.Definition));

            Stack.Pop();

            return;
        }

        if (TryGetAddress(field, out address, out size))
        {
            PushFrom(address, size);
            return;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Failed to get field memory address", field));
    }
    void GenerateCodeForStatement(CompiledReinterpretation typeCast)
    {
        GenerateCodeForStatement(typeCast.Value);
    }
    void GenerateCodeForStatement(CompiledCast typeCast)
    {
        if (typeCast.Value is CompiledConstantValue evaluatedValue &&
            evaluatedValue.Value.TryCast(typeCast.Type, out CompiledValue casted))
        {
            Stack.Push(casted);
            return;
        }

        if (FindSize(typeCast.Value.Type, typeCast.Value) != FindSize(typeCast.Type, typeCast))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Type-cast is not supported at the moment", typeCast));
            return;
        }

        GenerateCodeForStatement(typeCast.Value);
        Diagnostics.Add(DiagnosticAt.Warning($"Type-cast is not supported at the moment", typeCast));
    }
    #endregion

    void CheckPointerNull(int address)
    {
        if (!Settings.CheckNullPointers) return;

        Code.JumpStart(address);
        Code.CRASH($"Null pointer at {address}");
        Code.JumpEnd(address);
    }

    StackAddress PushFrom(Address address, int size) => address.Simplify() switch
    {
        AddressAbsolute v => PushFrom(v, size),
        AddressOffset v => PushFrom(v, size),
        _ => throw new NotImplementedException(),
    };
    StackAddress PushFrom(AddressAbsolute address, int size)
    {
        using (Code.Block(this, $"Load data (from {address})"))
        {
            StackAddress loadTarget = Stack.PushVirtual(size);

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

            return loadTarget;
        }
    }
    StackAddress PushFrom(AddressOffset address, int size)
    {
        if (address.Base is AddressRuntimePointer runtimePointer2)
        {
            using (Code.Block(this, $"Load data (dereferenced from \"{runtimePointer2.PointerValue}\" + {address.Offset})"))
            {
                int pointerAddress = Stack.NextAddress;
                GenerateCodeForStatement(runtimePointer2.PointerValue);
                Code.AddValue(pointerAddress, address.Offset);
                Heap.Get(pointerAddress, pointerAddress);
                return new StackAddress(Stack, pointerAddress);
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    #region GenerateCodeForPrinter()

    void GenerateCodeForPrinter(CompiledExpression value)
    {
        if (value is CompiledString literal1)
        {
            GenerateCodeForPrinter(literal1.Value, literal1);
            return;
        }

        if (value is CompiledStackString literal2)
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
    void GenerateCodeForValuePrinter(CompiledExpression value, GeneralType valueType)
    {
        if (value is CompiledString literalValue1)
        {
            GenerateCodeForPrinter(literalValue1.Value, literalValue1);
            return;
        }

        if (value is CompiledStackString literalValue2)
        {
            GenerateCodeForPrinter(literalValue2.Value, literalValue2);
            return;
        }

        if (FindSize(valueType, value) != 1)
        { throw new NotSupportedException($"Only value of size 1 (not {FindSize(valueType, value)}) supported by the output printer in brainfuck", value); }

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

    bool CanGenerateCodeForPrinter(CompiledExpression value)
    {
        if (value is CompiledString) return true;
        if (value is CompiledStackString) return true;

        return CanGenerateCodeForValuePrinter(value.Type);
    }
    bool CanGenerateCodeForValuePrinter(GeneralType valueType) =>
        FindSize(valueType, out int size, out _)
        && size == 1
        && valueType.Is<BuiltinType>();

    #endregion

    int GenerateCodeForLiteralString(CompiledString stringInstance)
    {
        if (!stringInstance.IsUTF8)
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

    bool IsFunctionInlineable(FunctionThingDefinition function, IEnumerable<CompiledArgument> parameters)
    {
        if (function.Block is null ||
            !function.IsInline)
        { return false; }

        foreach (CompiledArgument parameter in parameters)
        {
            if (parameter.Value is CompiledConstantValue)
            { continue; }
            return false;
        }

        return true;
    }

    void GenerateCodeForFunction(CompiledFunctionDefinition function, ImmutableArray<CompiledArgument> parameters, ImmutableDictionary<string, GeneralType>? typeArguments, ILocated caller)
    {
        if (!AllowFunctionInlining ||
            !IsFunctionInlineable(function.Definition, parameters))
        {
            GenerateCodeForFunction_(function, parameters, typeArguments, caller);
            return;
        }

        GenerateCodeForFunction_(function, parameters, typeArguments, caller);
    }

    void GenerateCodeForParameterPassing(ICompiledFunctionDefinition function, ImmutableArray<CompiledArgument> parameters, Stack<BrainfuckVariable> compiledParameters, ImmutableDictionary<string, GeneralType>? typeArguments)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            CompiledArgument passed = parameters[i];
            CompiledParameter defined = function.Parameters[i];

            GeneralType definedType = GeneralType.TryInsertTypeParameters(defined.Type, typeArguments);
            GeneralType passedType = passed.Type;

            if (FindSize(passedType, passed) != FindSize(definedType, defined.Definition))
            { Diagnostics.Add(DiagnosticAt.Error($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed)); }

            foreach (BrainfuckVariable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Identifier == defined.Identifier)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Parameter \"{defined}\" already defined as parameter", defined.Definition.Identifier, defined.Definition.File));
                    break;
                }
            }

            if (passed.Value is CompiledGetReference addressGetter)
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
                        Diagnostics.Add(DiagnosticAt.Error($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{new PointerType(v.Type)}\"", passed));
                        return;
                    }
                }

                CompiledVariableDefinition variableDeclaration = defined.Definition.ToVariable(definedType, passed);
                PointerType parameterType = new(v.Type);
                compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, FindSize(parameterType, passed), variableDeclaration)
                {
                    IsInitialized = true,
                });
                continue;
            }

            if (definedType.Is<PointerType>() && passed.Value is CompiledParameterAccess _parameterGetter)
            {
                if (!GetVariable(_parameterGetter, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(_parameterGetter));
                    return;
                }

                if (v.IsReference)
                {
                    CompiledVariableDefinition variableDeclaration = defined.Definition.ToVariable(definedType, passed);
                    PointerType parameterType = new(v.Type);
                    compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, FindSize(parameterType, passed), variableDeclaration)
                    {
                        IsInitialized = true,
                    });
                    continue;
                }
            }

            if (definedType.Is<PointerType>() && passed.Value is CompiledVariableAccess _variableGetter)
            {
                if (!GetVariable(_variableGetter, out BrainfuckVariable? v, out PossibleDiagnostic? notFoundError))
                {
                    Diagnostics.Add(notFoundError.ToError(_variableGetter));
                    return;
                }

                if (v.IsReference)
                {
                    CompiledVariableDefinition variableDeclaration = defined.Definition.ToVariable(definedType, passed);
                    PointerType parameterType = new(v.Type);
                    compiledParameters.Push(new BrainfuckVariable(v.Address, true, false, null, FindSize(parameterType, passed), variableDeclaration)
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
            //         Diagnostics.Add(Diagnostic.Error(
            //             $"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{variable.Type}\"",
            //             passed,
            //             castError.ToError(passed)));
            //     }
            // 
            //     var variableDeclaration = defined.ToVariable2(passed);
            //     compiledParameters.Push(new BrainfuckVariable(variable.Address, true, false, null, variable.Type, FindSize(variable.Type, passed), variableDeclaration.Variable)
            //     {
            //         IsInitialized = variable.IsInitialized,
            //     });
            //     continue;
            // }

            if (defined.Definition.Modifiers.Contains(ModifierKeywords.Const))
            {
                Diagnostics.Add(DiagnosticAt.Error($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed));
                return;
            }

            {
                CompiledVariableDefinition variableDeclaration = defined.Definition.ToVariable(definedType, passed);
                using (TypeArgumentsScope g = SetTypeArgumentsScope(typeArguments))
                { PrecompileVariable(compiledParameters, variableDeclaration, false, definedType); }

                BrainfuckVariable? compiledParameter = null;
                foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Identifier == defined.Identifier)
                    {
                        compiledParameter = compiledParameter_;
                    }
                }

                if (compiledParameter is null)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Parameter \"{defined}\" not found", defined.Definition.Identifier, defined.Definition.File));
                    return;
                }

                // if (!compiledParameter.Type.SameAs(definedType))
                // {
                //     Diagnostics.Add(Diagnostic.Warning($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{compiledParameter.Type}\"", passed));
                //     return;
                // }

                using (Code.Block(this, $"SET \"{defined.Identifier}\" TO _something_"))
                {
                    GenerateCodeForStatement(passed.Value, definedType);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
            }
        }
    }

    void GenerateCodeForFunction_(CompiledFunctionDefinition function, ImmutableArray<CompiledArgument> parameters, ImmutableDictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        CompiledFunction? f = FunctionBodies.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function body for function \"{function.ToReadable(typeArguments)}\" not found", function.Definition));
            return;
        }

        using DebugFunctionBlock debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledArgument parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledArgument parameter in parameters)
                { GenerateCodeForPrinter(parameter); }
                return;
            }
        }

        if (function.ExternalFunctionName == ExternalFunctionNames.StdIn)
        {
            int address = Stack.PushVirtual(1, callerPosition);
            Code.SetPointer(address);
            if (GeneralType.TryInsertTypeParameters(function.Type, typeArguments).SameAs(BasicType.Void))
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (FindSize(GeneralType.TryInsertTypeParameters(function.Type, typeArguments), function.Definition) != 1)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Function with attribute \"[{AttributeConstants.ExternalIdentifier}(\"{ExternalFunctionNames.StdIn}\")]\" must have a return type with size of 1", function.Definition.Type, function.File));
                    return;
                }
                Code += ',';
            }

            return;
        }

        if (function.Definition.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.Definition.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        if (function.Definition.Block is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition));
            return;
        }

        using IDisposableProgress<string>? progressLabel = Logger?.Label(LogType.Debug, $"Generating function \"{function.ToReadable(typeArguments)}\"");

        BrainfuckVariable? returnVariable = null;

        if (function.ReturnSomething)
        {
            CompiledVariableDefinition variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Definition.Location,
                Type = GeneralType.TryInsertTypeParameters(function.Type, typeArguments),
                TypeExpression = CompiledTypeExpression.CreateAnonymous(GeneralType.TryInsertTypeParameters(function.Type, typeArguments), function.Definition),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = GeneralType.TryInsertTypeParameters(function.Type, typeArguments),
                    Location = function.Definition.Location,
                },
                Definition = new VariableDefinition(ImmutableArray<AttributeUsage>.Empty, ImmutableArray<Tokenizing.Token>.Empty, new MissingTypeInstance(function.Definition), new MissingToken(TokenType.Identifier, function.Definition.Position), null, function.File),
            };
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(FindSize(GeneralType.TryInsertTypeParameters(function.Type, typeArguments), function.Definition), callerPosition), false, false, null, FindSize(GeneralType.TryInsertTypeParameters(function.Type, typeArguments), function.Definition), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        Runtime.ScopeInformation scopeInformation = new()
        {
            Location = new Runtime.SourceCodeLocation()
            {
                Instructions = (Code.Length, Code.Length + 1),
                Location = f.Body.Location,
            },
            Stack = new List<Runtime.StackElementInformation>(),
        };

        if (returnVariable is not null)
        {
            scopeInformation.Stack.Add(new Runtime.StackElementInformation()
            {
                Address = returnVariable.Address,
                Identifier = returnVariable.Identifier,
                Kind = Runtime.StackElementKind.Internal,
                Size = returnVariable.Size,
                Type = returnVariable.Type,
            });
        }
        scopeInformation.Stack.AddRange(compiledParameters.Select(v => new Runtime.StackElementInformation()
        {
            Address = v.Address,
            Identifier = v.Identifier,
            Kind = Runtime.StackElementKind.Parameter,
            Size = v.Size,
            Type = v.Type,
        }));

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Definition.Block.Brackets.Start.Position, function.Definition.Block.File), StatementCompiler.FindControlFlowUsage(f.Body));

        GenerateCodeForStatement(f.Body);

        using (DebugBlock(function.Definition.Block.Brackets.End, function.Definition.Block.File))
        {
            if (returnBlock is not null)
            {
                returnBlock = Returns.Pop();
                if (returnBlock.Value.FlagAddress.HasValue)
                {
                    using (Code.Block(this, $"Finish \"return\" block"))
                    {
                        if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                        { Diagnostics.Add(DiagnosticAt.Internal("I don't know what happened here", function.Definition.Block)); }
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
                        StatementCompiler.AllowDeallocate(variable.Type))
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

        scopeInformation.Location.Instructions.End = Code.Length;
        DebugInfo?.ScopeInformation.Add(scopeInformation);

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledOperatorDefinition function, ImmutableArray<CompiledArgument> parameters, ImmutableDictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        CompiledFunction? f = FunctionBodies.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function body for function \"{function.ToReadable(typeArguments)}\" not found", function.Definition));
            return;
        }

        using DebugFunctionBlock debugFunction = FunctionBlock(function, typeArguments);

        if (function.ExternalFunctionName == ExternalFunctionNames.StdOut)
        {
            bool canPrint = true;

            foreach (CompiledArgument parameter in parameters)
            {
                if (!CanGenerateCodeForPrinter(parameter))
                {
                    canPrint = false;
                    break;
                }
            }

            if (canPrint)
            {
                foreach (CompiledArgument parameter in parameters)
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
                if (FindSize(function.Type, function.Definition.Type) != 1)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Function with attribute \"StandardInput\" must have a return type with size of 1", function.Definition.Type, function.File));
                    return;
                }
                Code += ',';
            }

            return;
        }

        if (function.Definition.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.Definition.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        if (function.Definition.Block is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition));
            return;
        }

        using IDisposableProgress<string>? progressLabel = Logger?.Label(LogType.Debug, $"Generating function \"{function.ToReadable(typeArguments)}\"");

        BrainfuckVariable? returnVariable = null;

        if (true) // always returns something
        {
            CompiledVariableDefinition variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Definition.Location,
                Type = function.Type,
                TypeExpression = CompiledTypeExpression.CreateAnonymous(function.Type, function.Definition.Location),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = function.Type,
                    Location = function.Definition.Location,
                },
                Definition = new VariableDefinition(ImmutableArray<AttributeUsage>.Empty, ImmutableArray<Tokenizing.Token>.Empty, new MissingTypeInstance(function.Definition), new MissingToken(TokenType.Identifier, function.Definition.Position), null, function.File),
            };
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(FindSize(function.Type, function.Definition.Type), callerPosition), false, false, null, FindSize(function.Type, function.Definition.Type), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        Runtime.ScopeInformation scopeInformation = new()
        {
            Location = new Runtime.SourceCodeLocation()
            {
                Instructions = (Code.Length, Code.Length + 1),
                Location = f.Body.Location,
            },
            Stack = new List<Runtime.StackElementInformation>(),
        };

        scopeInformation.Stack.Add(new Runtime.StackElementInformation()
        {
            Address = returnVariable.Address,
            Identifier = returnVariable.Identifier,
            Kind = Runtime.StackElementKind.Internal,
            Size = returnVariable.Size,
            Type = returnVariable.Type,
        });
        scopeInformation.Stack.AddRange(compiledParameters.Select(v => new Runtime.StackElementInformation()
        {
            Address = v.Address,
            Identifier = v.Identifier,
            Kind = Runtime.StackElementKind.Parameter,
            Size = v.Size,
            Type = v.Type,
        }));

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Definition.Block.Brackets.Start.Position, function.Definition.Block.File), StatementCompiler.FindControlFlowUsage(f.Body));

        GenerateCodeForStatement(f.Body);

        if (returnBlock is not null)
        {
            returnBlock = Returns.Pop();
            if (returnBlock.Value.FlagAddress.HasValue)
            {
                using (DebugBlock(function.Definition.Block.Brackets.End, function.Definition.Block.File))
                {
                    if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                    { Diagnostics.Add(DiagnosticAt.Internal("I don't know what happened here", function.Definition.Block)); }
                    Stack.Pop();
                }
            }
        }

        using (DebugBlock(function.Definition.Block.Brackets.End, function.Definition.Block.File))
        {
            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.Cleanup is not null &&
                        StatementCompiler.AllowDeallocate(variable.Type))
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
                        StatementCompiler.AllowDeallocate(variable.Type))
                    { }
                    Stack.Pop();
                }
            }
        }

        scopeInformation.Location.Instructions.End = Code.Length;
        DebugInfo?.ScopeInformation.Add(scopeInformation);

        PopStackFrame(frame);

        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledGeneralFunctionDefinition function, ImmutableArray<CompiledArgument> parameters, ImmutableDictionary<string, GeneralType>? typeArguments, ILocated callerPosition)
    {
        CompiledFunction? f = FunctionBodies.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function body for function \"{function.ToReadable(typeArguments)}\" not found", function.Definition));
            return;
        }

        using DebugFunctionBlock debugFunction = FunctionBlock(function, typeArguments);

        if (function.Definition.ParameterCount != parameters.Length)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to function \"{function.ToReadable()}\" (required {function.Definition.ParameterCount} passed {parameters.Length})", callerPosition));
            return;
        }

        BrainfuckVariable? returnVariable = null;

        if (function.ReturnSomething)
        {
            CompiledVariableDefinition variableDeclaration = new()
            {
                Identifier = ReturnVariableName,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Definition.Location,
                Type = function.Type,
                TypeExpression = CompiledTypeExpression.CreateAnonymous(function.Type, function.Definition),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = function.Type,
                    Location = function.Definition.Location,
                },
                Definition = new VariableDefinition(ImmutableArray<AttributeUsage>.Empty, ImmutableArray<Tokenizing.Token>.Empty, new MissingTypeInstance(function.Definition), new MissingToken(TokenType.Identifier, function.Definition.Position), null, function.File),
            };
            GeneralType returnType = GeneralType.TryInsertTypeParameters(function.Type, typeArguments);
            returnVariable = new BrainfuckVariable(Stack.PushVirtual(FindSize(returnType, function.Definition), callerPosition), false, false, null, FindSize(returnType, function.Definition), variableDeclaration);
        }

        if (!IxMaxResursiveDepthReached(function, callerPosition))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, typeArguments);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);

        if (function.Definition.Block is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{function.ToReadable()}\" does not have a body", function.Definition));
            return;
        }

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Definition.Block.Brackets.Start.Position, function.Definition.Block.File), StatementCompiler.FindControlFlowUsage(f.Body));

        GenerateCodeForStatement(f.Body);

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
                        StatementCompiler.AllowDeallocate(variable.Type))
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

    void GenerateCodeForFunction(CompiledConstructorDefinition function, ImmutableArray<CompiledArgument> parameters, ImmutableDictionary<string, GeneralType>? typeArguments, CompiledConstructorCall caller)
    {
        CompiledFunction? f = FunctionBodies.FirstOrDefault(v => Utils.ReferenceEquals(v.Function, function) && StatementCompiler.TypeArgumentsEquals(v.TypeArguments, typeArguments));
        if (f is null)
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Function body for function \"{function.ToReadable(typeArguments)}\" not found", function.Definition));
            return;
        }

        using DebugFunctionBlock debugFunction = FunctionBlock(function, typeArguments);

        if (function.Definition.ParameterCount - 1 != parameters.Length)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to constructor \"{function.ToReadable()}\" (required {function.Definition.ParameterCount - 1} passed {parameters.Length})", caller));
            return;
        }

        if (function.Definition.Block is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Constructor \"{function.ToReadable()}\" does not have any body definition", caller));
            return;
        }

        using IDisposableProgress<string>? progressLabel = Logger?.Label(LogType.Debug, $"Generating function \"{function.ToReadable(typeArguments)}\"");

        if (!IxMaxResursiveDepthReached(function, caller))
        { return; }

        Stack<BrainfuckVariable> compiledParameters = new();

        CurrentMacro.Push(function);

        int newInstanceAddress = Stack.NextAddress;
        GeneralType newInstanceType = caller.Type;
        GenerateCodeForStatement(caller.Object);

        if (newInstanceType.Is<PointerType>(out PointerType? newInstancePointerType))
        {
            if (!newInstancePointerType.To.Is<StructType>())
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong type \"{newInstanceType}\" used for constructor", caller));
                return;
            }

            compiledParameters.Add(new BrainfuckVariable(newInstanceAddress, false, false, null, 1, new CompiledVariableDefinition()
            {
                Identifier = function.Parameters[0].Identifier,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = newInstanceType,
                TypeExpression = CompiledTypeExpression.CreateAnonymous(newInstanceType, function.Location),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = newInstanceType,
                    Location = function.Location,
                },
                Definition = new VariableDefinition(ImmutableArray<AttributeUsage>.Empty, ImmutableArray<Tokenizing.Token>.Empty, new MissingTypeInstance(function.Definition), new MissingToken(TokenType.Identifier, function.Definition.Position), null, function.File),
            }));
        }
        else if (newInstanceType.Is<StructType>())
        {
            compiledParameters.Add(new BrainfuckVariable(newInstanceAddress, true, false, null, 1, new CompiledVariableDefinition()
            {
                Identifier = function.Parameters[0].Identifier,
                InitialValue = null,
                IsGlobal = false,
                Location = function.Location,
                Type = new PointerType(newInstanceType),
                TypeExpression = CompiledTypeExpression.CreateAnonymous(new PointerType(newInstanceType), function.Definition),
                Cleanup = new CompiledCleanup()
                {
                    TrashType = new PointerType(newInstanceType),
                    Location = function.Location,
                },
                Definition = new VariableDefinition(ImmutableArray<AttributeUsage>.Empty, ImmutableArray<Tokenizing.Token>.Empty, new MissingTypeInstance(function.Definition), new MissingToken(TokenType.Identifier, function.Definition.Position), null, function.File),
            }));
        }
        else
        {
            Diagnostics.Add(DiagnosticAt.Error($"Wrong type \"{newInstanceType}\" used for constructor", caller));
            return;
        }

        for (int i = 1; i < function.Parameters.Length; i++)
        {
            CompiledArgument passed = parameters[i - 1];
            CompiledParameter defined = function.Parameters[i];

            GeneralType definedType = GeneralType.TryInsertTypeParameters(defined.Type, typeArguments);
            GeneralType passedType = passed.Type;

            if (!passedType.SameAs(definedType))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{passedType}\"", passed));
                return;
            }

            foreach (BrainfuckVariable compiledParameter2 in compiledParameters)
            {
                if (compiledParameter2.Identifier == defined.Identifier)
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Parameter \"{defined}\" already defined as parameter", defined.Definition.Identifier, defined.Definition.File));
                    return;
                }
            }

            if (defined.Definition.Modifiers.Contains(ModifierKeywords.Const))
            {
                Diagnostics.Add(DiagnosticAt.Error($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed));
                return;
            }

            CompiledVariableDefinition variableDeclaration2 = defined.Definition.ToVariable(definedType, passed);
            PrecompileVariable(compiledParameters, variableDeclaration2, false);

            BrainfuckVariable? compiledParameter = null;
            foreach (BrainfuckVariable compiledParameter_ in compiledParameters)
            {
                if (compiledParameter_.Identifier == defined.Identifier)
                {
                    compiledParameter = compiledParameter_;
                }
            }

            if (compiledParameter is null)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Parameter \"{defined}\" not found", defined.Definition.Identifier, defined.Definition.File));
                return;
            }

            if (!compiledParameter.Type.SameAs(definedType))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected \"{definedType}\", passed \"{compiledParameter.Type}\"", passed));
                return;
            }

            using (Code.Block(this, $"SET \"{defined.Identifier}\" TO _something_"))
            {
                GenerateCodeForStatement(passed.Value);

                using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                { Stack.PopAndStore(compiledParameter.Address); }
            }
        }

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushRange(compiledParameters);

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(function.Definition.Block.Brackets.Start.Position, function.Definition.Block.File), StatementCompiler.FindControlFlowUsage(f.Body));

        GenerateCodeForStatement(f.Body);

        if (returnBlock is not null)
        {
            returnBlock = Returns.Pop();
            if (returnBlock.Value.FlagAddress.HasValue)
            {
                using (DebugBlock(function.Definition.Block.Brackets.End, function.Definition.Block.File))
                using (Code.Block(this, $"Finish \"return\" block"))
                {
                    if (returnBlock.Value.FlagAddress.Value != Stack.LastAddress)
                    { Diagnostics.Add(DiagnosticAt.Internal("I don't know what happened here", function.Definition.Block)); }
                    Stack.Pop();
                }
            }
        }

        using (DebugBlock(function.Definition.Block.Brackets.End, function.Definition.Block.File))
        {
            using (Code.Block(this, $"Deallocate function variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    BrainfuckVariable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.Cleanup is not null &&
                        StatementCompiler.AllowDeallocate(variable.Type))
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

    bool IxMaxResursiveDepthReached(ICompiledFunctionDefinition function, ILocated callerPosition)
    {
        int depth = 0;
        for (int i = 0; i < CurrentMacro.Count; i++)
        {
            if (!Utils.ReferenceEquals(CurrentMacro[i], function))
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
        // todo: think about it
        if (!controlFlowBlocks.Last.FlagAddress.HasValue) return;

        using (Code.Block(this, $"Continue \"{kind}\" statements"))
        {
            if (!controlFlowBlocks.Last.FlagAddress.HasValue)
            { Diagnostics.Add(DiagnosticAt.Internal($"Unexpected conditional jump continuation in the depths (this is the compiler's fault)", controlFlowBlocks.Last.Location)); }

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
