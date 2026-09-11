using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
    bool CompileAllocation(CompiledTypeExpression type, [NotNullWhen(true)] out CompiledExpression? compiledStatement)
    {
        if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) || Settings.OptimizationDiagnostics)
        {
            if (FindSize(type, out int typeSize, out PossibleDiagnostic? typeSizeError, Settings.RuntimeInfo))
            {
                Diagnostics.Add(DiagnosticAt.OptimizationNotice($"Allocation size computed as {typeSize}", type));
                if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating))
                {
                    return CompileAllocation(typeSize, type.Location, out compiledStatement);
                }
            }
            else
            {
                Diagnostics.Add(DiagnosticAt.FailedOptimization($"Failed to compute allocation size", type).WithSuberrors(typeSizeError.ToError(type, false)));
            }
        }

        compiledStatement = null;

        ImmutableArray<CompiledExpression> argumentExpressions = ImmutableArray.Create<CompiledExpression>(new CompiledSizeof()
        {
            Of = type,
            Location = type.Location,
            SaveValue = true,
            Type = SizeofStatementType,
        });

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, argumentExpressions, type.Location.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found", type)
                .WithSuberrors(error.ToError(type)));
            return false;
        }

        result.ReplaceArgumentsIfNeeded(ref argumentExpressions);

        if (!CompileArguments(default, argumentExpressions, result.Function, result.TypeArguments, out ImmutableArray<CompiledArgument> compiledArguments))
        {
            return false;
        }

        result.Function.AddReference(type);

        CompiledFunctionDefinition allocator = result.Function;
        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", allocator.Definition.Type));
            return false;
        }

        if (!allocator.Definition.CanUse(type.Location.File))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", type));
            return false;
        }

        if (allocator.ExternalFunctionName is not null)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator.Definition));
                return false;
            }

            return CompileFunctionCall_External(compiledArguments, true, allocator, externalFunction, type.Location, out compiledStatement);
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Function = new(allocator, result.TypeArguments),
            Arguments = compiledArguments,
            Location = type.Location,
            SaveValue = true,
            Type = allocator.Type,
        };
        return true;
    }
    bool CompileAllocation(int size, Location sizeLocation, [NotNullWhen(true)] out CompiledExpression? compiledStatement)
    {
        compiledStatement = null;

        if (!GetLiteralType(LiteralType.Integer, out GeneralType? intType, out PossibleDiagnostic? typeError))
        {
            intType = SizeofStatementType;
            Diagnostics.Add(DiagnosticAt.Warning($"No type defined for integer literals, using the default {intType}", sizeLocation, ignoreOnPartialSource: true).WithSuberrors(typeError.ToError(sizeLocation, false)));
        }

        ImmutableArray<CompiledExpression> argumentExpressions = ImmutableArray.Create<CompiledExpression>(new CompiledConstantValue()
        {
            Value = size,
            Location = sizeLocation,
            SaveValue = true,
            Type = intType,
        });

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, argumentExpressions, sizeLocation.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", sizeLocation));
            return false;
        }

        result.ReplaceArgumentsIfNeeded(ref argumentExpressions);

        result.Function.AddReference(sizeLocation);

        if (!CompileArguments(default, argumentExpressions, result.Function, result.TypeArguments, out ImmutableArray<CompiledArgument> compiledArguments))
        {
            return false;
        }

        CompiledFunctionDefinition allocator = result.Function;
        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", allocator.Definition.Type));
            return false;
        }

        if (!allocator.Definition.CanUse(sizeLocation.File))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", sizeLocation));
            return false;
        }

        if (allocator.ExternalFunctionName is not null)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator.Definition));
                return false;
            }

            return CompileFunctionCall_External(compiledArguments, true, allocator, externalFunction, sizeLocation, out compiledStatement);
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Function = new(allocator, result.TypeArguments),
            Arguments = compiledArguments,
            Location = sizeLocation,
            SaveValue = true,
            Type = allocator.Type,
        };
        return true;
    }
    bool CompileDeallocation(GeneralType deallocateableType, Location location, [NotNullWhen(true)] out CompiledFunctionDefinition? deallocator)
    {
        deallocator = null;
        ImmutableArray<CompiledExpression> argumentExpressions = ImmutableArray.Create<CompiledExpression>(
            new CompiledMeowExpression()
            {
                Location = location,
                SaveValue = true,
                Type = deallocateableType,
            }
        );

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, argumentExpressions, location.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", location).WithSuberrors(notFoundError.ToError(location)));
            return false;
        }

        result.Function.AddReference(new CompiledMeowExpression() { Location = location, SaveValue = true, Type = deallocateableType });

        result.ReplaceArgumentsIfNeeded(ref argumentExpressions);

        deallocator = result.Function;

        if (!deallocator.Definition.CanUse(location.File))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", location));
            return false;
        }

        if (deallocator.ExternalFunctionName is not null)
        {
            throw new NotImplementedException();
        }

        return true;
    }
    bool CompileCleanup(GeneralType deallocateableType, Location location, [NotNullWhen(true)] out CompiledCleanup? compiledCleanup)
    {
        compiledCleanup = null;

        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);

        FunctionQueryResult<CompiledGeneralFunctionDefinition>? destructor = null;
        CompiledFunctionDefinition? deallocator;

        if (AllowDeallocate(deallocateableType))
        {
            if (!CompileDeallocation(deallocateableType, location, out deallocator))
            {
                return false;
            }
        }
        else
        {
            deallocator = null;
        }

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out destructor, out PossibleDiagnostic? error, AddCompilable))
            {
                //if (deallocateablePointerType.To.Is<StructType>())
                //{
                //    Diagnostics.Add(DiagnosticAt.Warning($"Destructor for type \"{deallocateablePointerType.To}\" not found", location).WithSuberrors(error.ToWarning(location)));
                //}
            }
            else
            {
                destructor.Function.AddReference(null, location);
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out destructor, out PossibleDiagnostic? error, AddCompilable))
            {
                //if (deallocateableType.Is<StructType>())
                //{
                //    Diagnostics.Add(DiagnosticAt.Warning($"Destructor for type \"{deallocateableType}\" not found", location).WithSuberrors(error.ToWarning(location)));
                //}
            }
            else
            {
                destructor.Function.AddReference(null, location);
            }
        }

        if (destructor is not null
            && !destructor.Function.Definition.CanUse(location.File))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Destructor for type \"{deallocateableType}\" cannot be called due to its protection level", location));
            return false;
        }

        compiledCleanup = new CompiledCleanup()
        {
            Deallocator = TemplateInstance.New(deallocator, null),
            Destructor = TemplateInstance.New(destructor),
            Location = location,
            TrashType = deallocateableType,
        };
        return true;
    }

    public bool CompileStatement(ImmutableArray<TypeInstance> types, [NotNullWhen(true)] out ImmutableArray<CompiledTypeExpression> result, DiagnosticsCollection diagnostics)
    {
        result = default;

        ImmutableArray<CompiledTypeExpression>.Builder _result = ImmutableArray.CreateBuilder<CompiledTypeExpression>(types.Length);
        foreach (TypeInstance item in types)
        {
            if (!CompileStatement(item, out CompiledTypeExpression? _item, diagnostics)) return false;
            _result.Add(_item);
        }
        result = _result.MoveToImmutable();
        return true;
    }

    bool CompileStatement(TypeInstance typeInstance, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        return typeInstance switch
        {
            TypeInstanceSimple v => CompileStatement(v, out result, diagnostics),
            TypeInstanceFunction v => CompileStatement(v, out result, diagnostics),
            TypeInstanceStackArray v => CompileStatement(v, out result, diagnostics),
            TypeInstancePointer v => CompileStatement(v, out result, diagnostics),
            TypeInstanceReference v => CompileStatement(v, out result, diagnostics),
            MissingTypeInstance v => CompileStatement(v, out result, diagnostics),
            _ => throw new UnreachableException(),
        };
    }
    bool CompileStatement(MissingTypeInstance type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        result = null;
        diagnostics.Add(DiagnosticAt.Error("Incomplete AST", type, false));
        return false;
    }
    bool CompileStatement(TypeInstanceSimple type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        if (!type.TypeArguments.HasValue && TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new CompiledBuiltinTypeExpression(builtinType, type.Location);
            type.Identifier.AnalyzedType = TokenAnalyzedType.BuiltinType;
            return true;
        }

        {
            PossibleDiagnostic? aliasError = null;
            PossibleDiagnostic? enumError = null;

            if (!type.TypeArguments.HasValue)
            {
                if (Frames.Last.TypeArguments.TryGetValue(type.Identifier.Content, out GeneralType? typeArgument))
                {
                    type.Identifier.AnalyzedType = TokenAnalyzedType.TypeParameter;
                    result = CompiledTypeExpression.CreateAnonymous(typeArgument, type.Location);
                    goto ok;
                }

                {
                    int i = Frames.Last.TypeParameters.IndexOf(type.Identifier.Content);
                    if (i != -1)
                    {
                        type.Identifier.AnalyzedType = TokenAnalyzedType.TypeParameter;
                        result = new CompiledGenericTypeExpression(Frames.Last.TypeParameters[i], type.File, type.Location);
                        SetStatementReference(type, Frames.Last.TypeParameters[i]);
                        goto ok;
                    }
                }

                foreach (ImmutableArray<Token> v in GenericParameters)
                {
                    foreach (Token w in v)
                    {
                        if (w.Content != type.Identifier.Content) continue;

                        type.Identifier.AnalyzedType = TokenAnalyzedType.TypeParameter;
                        result = new CompiledGenericTypeExpression(w, type.File, type.Location);
                        SetStatementReference(type, w);
                        goto ok;
                    }
                }

                if (GetAlias(type.Identifier.Content, type.File, out AliasDefinition? aliasDefinition, out aliasError))
                {
                    CompiledAlias compiled = CompileAlias(aliasDefinition);

                    type.Identifier.AnalyzedType = compiled.Value.FinalValue switch
                    {
                        CompiledBuiltinTypeExpression => TokenAnalyzedType.BuiltinType,
                        CompiledStructTypeExpression => TokenAnalyzedType.Struct,
                        CompiledGenericTypeExpression => TokenAnalyzedType.TypeParameter,
                        CompiledEnumTypeExpression => TokenAnalyzedType.Enum,
                        _ => TokenAnalyzedType.Type,
                    };
                    compiled.AddReference(type);
                    SetStatementReference(type, aliasDefinition);

                    result = new CompiledAliasTypeExpression(compiled, type.Location);
                    goto ok;
                }

                if (GetEnum(type.Identifier.Content, type.File, out EnumDefinition? enumDefinition, out enumError))
                {
                    CompiledEnum compiled = CompileEnum(enumDefinition);

                    type.Identifier.AnalyzedType = TokenAnalyzedType.Enum;
                    compiled.AddReference(new IdentifierExpression(type.Identifier, type.File));
                    SetStatementReference(type, enumDefinition);

                    result = new CompiledEnumTypeExpression(compiled, type.Location);
                    goto ok;
                }
            }

            if (GetStruct(type.Identifier.Content, type.TypeArguments?.Length, type.File, out StructDefinition? structDefinition, out PossibleDiagnostic? structError))
            {
                CompiledStruct compiled = CompileStruct(structDefinition);

                type.Identifier.AnalyzedType = TokenAnalyzedType.Struct;
                compiled.AddReference(type);
                SetStatementReference(type, structDefinition);

                result = new CompiledStructTypeExpression(compiled, type.File, type.Location);
                goto ok;
            }

            result = null;
            diagnostics.Add(DiagnosticAt.Error($"Can't find type `{type}`", type, ignoreOnPartialSource: true).WithSuberrors(ImmutableArray.Create(
                aliasError?.ToError(type),
                structError?.ToError(type),
                enumError?.ToError(type)
            )));
            return false;

        ok:;
        }

        if (result.Is(out CompiledStructTypeExpression? resultStructType) &&
            resultStructType.Struct.Definition.Template is not null)
        {
            if (type.TypeArguments.HasValue)
            {
                if (!CompileStatement(type.TypeArguments.Value, out ImmutableArray<CompiledTypeExpression> typeParameters, diagnostics)) return false;
                result = new CompiledStructTypeExpression(resultStructType.Struct, type.File, typeParameters, type.Location);
            }
            else
            {
                result = new CompiledStructTypeExpression(resultStructType.Struct, type.File, type.Location);
            }
        }

        return true;
    }
    bool CompileStatement(TypeInstanceFunction type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        result = null;

        if (!CompileStatement(type.FunctionReturnType, out CompiledTypeExpression? returnType, diagnostics)) return false;
        if (!CompileStatement(type.FunctionParameterTypes, out ImmutableArray<CompiledTypeExpression> parameters, diagnostics)) return false;

        result = new CompiledFunctionTypeExpression(returnType, parameters, type.ClosureModifier is not null, type.Location);
        //type.SetAnalyzedType(result);
        return true;
    }
    bool CompileStatement(TypeInstanceStackArray type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        result = null;

        if (type.StackArraySize is not null)
        {
            CompiledExpression? compiledLength;
            using (Diagnostics.MakeOverride(diagnostics))
            {
                if (!CompileExpression(type.StackArraySize, out compiledLength, Settings.ArrayLengthType)) return false;
            }
            if (!CompileStatement(type.StackArrayOf, out CompiledTypeExpression? of, diagnostics)) return false;

            result = new CompiledArrayTypeExpression(of, compiledLength, type.Location);
            //SetTypeType(type, result);
            return true;
        }
        else
        {
            if (!CompileStatement(type.StackArrayOf, out CompiledTypeExpression? of, diagnostics)) return false;
            result = new CompiledArrayTypeExpression(of, null, type.Location);
            //SetTypeType(type, result);
            return true;
        }
    }
    bool CompileStatement(TypeInstancePointer type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        result = null;

        if (!CompileStatement(type.To, out CompiledTypeExpression? to, diagnostics)) return false;

        result = new CompiledPointerTypeExpression(to, type.Location);
        //type.SetAnalyzedType(result);

        return true;
    }
    bool CompileStatement(TypeInstanceReference type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        result = null;

        if (!CompileStatement(type.To, out CompiledTypeExpression? to, diagnostics)) return false;

        result = new CompiledReferenceTypeExpression(to, type.Location);
        //type.SetAnalyzedType(result);

        return true;
    }

    bool CompileStatement(VariableDefinition newVariable, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const))
        {
            compiledStatement = new CompiledEmptyStatement()
            {
                Location = newVariable.Location,
            };
            return true;
        }

        if (LanguageConstants.KeywordList.Contains(newVariable.Identifier.Content))
        { Diagnostics.Add(DiagnosticAt.Error($"Illegal variable name \"{newVariable.Identifier.Content}\"", newVariable.Identifier, newVariable.File)); }

        GeneralType? type = null;
        if (newVariable.Type != StatementKeywords.Var)
        {
            if (!CompileType(newVariable.Type, out type, Diagnostics))
            {
                type = BuiltinType.Any;
            }
            else
            {
                if (type is ArrayType arrayType)
                {
                    if (newVariable.InitialValue is ListExpression literalList &&
                        arrayType.Length is null)
                    {
                        type = new ArrayType(arrayType.Of, literalList.Values.Length);
                    }

                    if (newVariable.InitialValue is StringLiteralExpression literalStatement)
                    {
                        int realLength;

                        if (arrayType.Of.SameAs(BasicType.U16))
                        {
                            realLength = literalStatement.Value.Length;
                        }
                        else if (arrayType.Of.SameAs(BasicType.U8))
                        {
                            realLength = Encoding.UTF8.GetByteCount(literalStatement.Value);
                        }
                        else
                        {
                            goto skip;
                        }

                        int definedLength = realLength + 1;

                        if (arrayType.Length.HasValue)
                        {
                            definedLength = arrayType.Length.Value;
                        }
                        else if (arrayType.Length.HasValue)
                        {
                            definedLength = arrayType.Length.Value;
                        }

                        if (definedLength != realLength &&
                            definedLength != realLength + 1)
                        {
                            Diagnostics.Add(DiagnosticAt.Error($"String literal's length ({realLength}) doesn't match with the type's length ({definedLength})", literalStatement));
                        }

                        type = new ArrayType(arrayType.Of, definedLength);

                    skip:;
                    }
                }

                if (!Frames.Last.IsTemplateInstance) newVariable.CompiledType = type;
            }
        }

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, newVariable.File, out _, out _))
        {
            Diagnostics.Add(DiagnosticAt.Error($"Constant with name \"{newVariable.Identifier}\" already exists", newVariable.Identifier, newVariable.File));
            return false;
        }

        CompiledExpression? initialValue = null;

        CompileVariableAttributes(newVariable);

        if (newVariable.ExternalConstantName is not null)
        {
            ExternalConstant? externalConstant = ExternalConstants.FirstOrDefault(v => v.Name == newVariable.ExternalConstantName);
            if (externalConstant is null)
            {
                if (type is null)
                {
                    Diagnostics.Add(DiagnosticAt.Warning($"External constant \"{newVariable.ExternalConstantName}\" not found", newVariable));
                }
            }
            else
            {
                if (type is null && !CompileType(externalConstant.Value.Type, out type, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(newVariable));
                    return false;
                }

                if (!externalConstant.Value.TryCast(type, out CompiledValue castedValue))
                {
                    Diagnostics.Add(DiagnosticAt.Error($"Can't cast external constant value {externalConstant.Value} of type \"{externalConstant.Value.Type}\" to {type}", newVariable));
                    return false;
                }

                initialValue = new CompiledConstantValue()
                {
                    Value = castedValue,
                    Location = newVariable.Location,
                    SaveValue = true,
                    Type = type,
                };
            }
        }

        if (newVariable.InternalConstantName is not null)
        {
            if (!InternalConstants.TryGetValue(newVariable.InternalConstantName, out GeneralType? internalConstantType))
            {
                Diagnostics.Add(DiagnosticAt.Warning($"Internal constant \"{newVariable.InternalConstantName}\" not found", newVariable));
            }
            else
            {
                type ??= internalConstantType;

                if (!CanCastImplicitly(internalConstantType, type, out PossibleDiagnostic? castError, out _))
                {
                    Diagnostics.Add(castError.ToError(newVariable.Type));
                    return false;
                }

                initialValue = new CompiledCompilerVariableAccess()
                {
                    Identifier = newVariable.InternalConstantName,
                    Location = newVariable.Location,
                    SaveValue = true,
                    Type = internalConstantType,
                };
            }
        }

        bool success = true;

        if (initialValue is null && newVariable.InitialValue is not null)
        {
            if (!CompileExpression(newVariable.InitialValue, out initialValue, type))
            {
                success = false;
            }
            else
            {
                type = type is null
                    ? initialValue.Type
                    : GeneralType.TryInsertConstants(type, initialValue.Type);

                if (!CanCastImplicitly(initialValue, type, out CompiledExpression? assignedInitialValue, out PossibleDiagnostic? castError, out _))
                {
                    Diagnostics.Add(castError.ToError(initialValue));
                    success = false;
                }
                else
                {
                    initialValue = assignedInitialValue;
                }
            }
        }

        if (type is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Initial value for variable declaration with implicit type is required", newVariable));
            type = BuiltinType.Any;
        }

        //if (!type.AllGenericsDefined())
        //{
        //    Diagnostics.Add(Diagnostic.Internal($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\" (what edge case is this???)", newVariable.Type, newVariable.File));
        //}

        SetStatementType(newVariable.Type, type);

        CompiledCleanup? compiledCleanup = null;
        if (newVariable.Modifiers.Contains(ModifierKeywords.Temp))
        {
            CompileCleanup(type, newVariable.Location, out compiledCleanup);
            if (!Frames.Last.IsTemplateInstance) newVariable.CleanupReference = compiledCleanup;
        }

        bool isGlobal = Frames.Last.IsTopLevel && Frames.Last.Scopes.Count <= 1;

        CompiledVariableDefinition compiledVariable = new()
        {
            Identifier = newVariable.Identifier.Content,
            TypeExpression = CompiledTypeExpression.CreateAnonymous(type, newVariable.Type.Location),
            Type = type,
            InitialValue = initialValue,
            Location = newVariable.Location,
            Cleanup = compiledCleanup ?? new CompiledCleanup()
            {
                Location = newVariable.Location,
                TrashType = type,
            },
            IsGlobal = isGlobal,
            Definition = newVariable,
        };

        SetStatementReference(newVariable, compiledVariable);

        if (isGlobal)
        { compiledStatement = CompiledGlobalVariables.Push(compiledVariable); }
        else
        { compiledStatement = Frames.Last.Scopes.Last.Variables.Push(compiledVariable); }

        return success;
    }
    bool CompileStatement(InstructionLabelDeclaration instructionLabel, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GetInstructionLabel(instructionLabel.Identifier.Content, out CompiledLabelDeclaration? compiledInstructionLabelDeclaration, out _))
        {
            Diagnostics.Add(DiagnosticAt.Internal($"Instruction label \"{instructionLabel.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", instructionLabel.Identifier, instructionLabel.File));
            return false;
        }

        compiledStatement = compiledInstructionLabelDeclaration;
        return true;
    }
    bool CompileStatement(KeywordCallStatement keywordCall, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (keywordCall.Keyword.Content == StatementKeywords.Return)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            CompiledExpression? returnValue = null;

            if (keywordCall.Arguments.Length == 1)
            {
                if (!CompileExpression(keywordCall.Arguments[0], out returnValue, Frames.Last.CurrentReturnType)) return false;
                Frames.Last.CurrentReturnType ??= returnValue.Type;

                if (!CanCastImplicitly(returnValue, Frames.Last.CurrentReturnType, out CompiledExpression? assignedReturnValue, out PossibleDiagnostic? castError, out _))
                {
                    Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                }

                returnValue = assignedReturnValue;
            }

            compiledStatement = new CompiledReturn()
            {
                Value = returnValue,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Keyword.Content == StatementKeywords.Crash)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to \"{StatementKeywords.Crash}\": required {1} passed {keywordCall.Arguments}", keywordCall));
                return false;
            }

            if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? throwValue, new ArrayType(BuiltinType.Char, null))) return false;

            ResolveReference(ref throwValue);

            compiledStatement = new CompiledCrash()
            {
                Value = throwValue,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Keyword.Content == StatementKeywords.Break)
        {
            compiledStatement = new CompiledBreak()
            {
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Keyword.Content == StatementKeywords.Delete)
        {
            if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? value)) return false;

            if (!CompileCleanup(value.Type, keywordCall.Arguments[0].Location, out CompiledCleanup? compiledCleanup)) return false;

            SetStatementReference(keywordCall, compiledCleanup);

            compiledStatement = new CompiledDelete()
            {
                Value = value,
                Cleanup = compiledCleanup,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Keyword.Content == StatementKeywords.Goto)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Wrong number of arguments passed to \"{StatementKeywords.Goto}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? to)) return false;

            if (!CanCastImplicitly(to, CompiledLabelDeclaration.Type, out CompiledExpression? assignedValue, out PossibleDiagnostic? castError, out _))
            {
                Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                return false;
            }

            to = assignedValue;

            compiledStatement = new CompiledGoto()
            {
                Value = to,
                Location = keywordCall.Location,
            };
            return true;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Unknown keyword \"{keywordCall.Keyword}\"", keywordCall.Keyword, keywordCall.File));
        return false;
    }
    bool CompileStatement(SimpleAssignmentStatement setter, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        return CompileSetter(setter.Target, setter.Value, out compiledStatement);
    }
    bool CompileStatement(WhileLoopStatement whileLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        Block block = Block.CreateIfNotBlock(whileLoop.Body);

        /*
        if (AllowEvaluating &&
            TryCompute(whileLoop.Condition, out CompiledValue condition))
        {
            if (condition)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"While loop condition evaluated as true", whileLoop.Condition));

                OnScopeEnter(block, false);

                CompileStatement(block, true);

                OnScopeExit(whileLoop.Block.Position.After(), whileLoop.Block.File);
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"While loop fully trimmed", whileLoop));
            }
            return;
        }
        */

        CompiledExpression? condition;
        CompiledStatement? body;

        using (Frames.Last.Scopes.PushAuto(CompileScope(block.Statements)))
        {
            if (!CompileExpression(whileLoop.Condition, out condition)) return false;
            if (!CompileStatement(block, out body, true)) return false;
        }

        compiledStatement = new CompiledWhileLoop()
        {
            Condition = condition,
            Body = body,
            Location = whileLoop.Location,
        };
        return true;
    }
    bool CompileStatement(ForLoopStatement forLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        CompiledStatement? initialization = null;
        CompiledExpression? condition = null;
        CompiledStatement? body;
        CompiledStatement? step = null;

        using (Frames.Last.Scopes.PushAuto(CompileScope(forLoop.Initialization is null ? Enumerable.Empty<Statement>() : Enumerable.Repeat(forLoop.Initialization, 1))))
        {
            if (forLoop.Initialization is not null && !CompileStatement(forLoop.Initialization, out initialization)) return false;
            if (forLoop.Condition is not null && !CompileExpression(forLoop.Condition, out condition)) return false;
            if (!CompileStatement(forLoop.Body, out body)) return false;
            if (forLoop.Step is not null && !CompileStatement(forLoop.Step, out step)) return false;
        }

        compiledStatement = new CompiledForLoop()
        {
            Initialization = initialization,
            Condition = condition,
            Step = step,
            Body = body,
            Location = forLoop.Location,
        };
        return true;
    }
    bool CompileStatement(IfBranchStatement @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileExpression(@if.Condition, out CompiledExpression? condition))
        {
            if (@if.Else is not null) CompileStatement(@if.Else, out _);
            CompileStatement(@if.Body, out _);
            return false;
        }

        if (condition is CompiledConstantValue evaluatedCondition && (Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable) || Settings.OptimizationDiagnostics))
        {
            if (evaluatedCondition.Value)
            {
                if (!StatementWalker.Visit(@if.Else, StatementWalkerFilter.FrameOnlyFilter).OfType<InstructionLabelDeclaration>().Any())
                {
                    Diagnostics.Add(DiagnosticAt.OptimizationNotice($"If condition trimmed away", @if.Condition));
                    if (Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable))
                    {
                        return CompileStatement(@if.Body, out compiledStatement);
                    }
                }
            }
            else
            {
                if (!StatementWalker.Visit(@if, StatementWalkerFilter.FrameOnlyFilter).OfType<InstructionLabelDeclaration>().Any())
                {
                    Diagnostics.Add(DiagnosticAt.OptimizationNotice($"If branch trimmed away", @if));
                    if (@if.Else is not null)
                    {
                        if (Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable))
                        {
                            if (!CompileStatement(@if.Else, out compiledStatement)) return false;
                            if (compiledStatement is CompiledElse nextElse)
                            {
                                compiledStatement = nextElse.Body;
                            }
                            return true;
                        }
                    }
                    else
                    {
                        if (Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable))
                        {
                            compiledStatement = new CompiledEmptyStatement()
                            {
                                Location = @if.Location,
                            };
                            return true;
                        }
                    }
                }
            }
        }

        if (!CompileStatement(@if.Body, out CompiledStatement? body)) return false;

        CompiledBranch? next = null;
        if (@if.Else is not null)
        {
            BranchStatementBase nextLink = @if.Else;

            if (nextLink.Body is IfBranchStatement nextIfContainer1)
            {
                nextLink = nextIfContainer1;
            }
            else if (nextLink.Body is Block nextBlock
                    && nextBlock.Statements.Length == 1
                    && nextBlock.Statements[0] is IfBranchStatement nextIfContainer2)
            {
                nextLink = nextIfContainer2;
            }

            if (!CompileStatement(nextLink, out CompiledStatement? _next)) return false;

            next = _next is CompiledBranch v ? v : new CompiledElse()
            {
                Body = _next,
                Location = _next.Location,
            };
        }

        compiledStatement = new CompiledIf()
        {
            Condition = condition,
            Body = body,
            Next = next,
            Location = @if.Location,
        };
        return true;
    }
    bool CompileStatement(ElseBranchStatement @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(@if.Body, out CompiledStatement? body)) return false;

        compiledStatement = new CompiledElse()
        {
            Body = body,
            Location = @if.Location,
        };
        return true;
    }
    bool CompileStatement(BranchStatementBase branch, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        return branch switch
        {
            IfBranchStatement v => CompileStatement(v, out compiledStatement),
            ElseBranchStatement v => CompileStatement(v, out compiledStatement),
            _ => throw new NotImplementedException(),
        };
    }
    bool CompileStatement(Block block, [NotNullWhen(true)] out CompiledStatement? compiledStatement, bool ignoreScope = false)
    {
        if (CompileStatement(block, out CompiledBlock? compiledBlock, ignoreScope))
        {
            compiledStatement = compiledBlock;
            return true;
        }
        else
        {
            compiledStatement = null;
            return false;
        }
    }
    bool CompileStatement(Block block, [NotNullWhen(true)] out CompiledBlock? compiledStatement, bool ignoreScope = false)
    {
        compiledStatement = null;

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(block.Statements.Length);

        if (ignoreScope)
        {
            bool success = true;
            foreach (Statement statement in block.Statements)
            {
                if (!CompileStatement(statement, out CompiledStatement? item))
                {
                    success = false;
                    continue;
                }
                res.AddRange(ReduceStatements(item, Diagnostics));
            }
            if (!success) return false;

            compiledStatement = new CompiledBlock()
            {
                Statements = res.ToImmutable(),
                Location = block.Location,
            };
            return true;
        }

        using (Frames.Last.Scopes.PushAuto(CompileScope(block.Statements)))
        {
            bool success = true;
            foreach (Statement statement in block.Statements)
            {
                if (!CompileStatement(statement, out CompiledStatement? item))
                {
                    success = false;
                    continue;
                }
                res.AddRange(ReduceStatements(item, Diagnostics));
            }
            if (!success) return false;
        }

        compiledStatement = new CompiledBlock()
        {
            Statements = res.ToImmutable(),
            Location = block.Location,
        };
        return true;
    }
    bool CompileStatement(EmptyStatement statement, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = new CompiledEmptyStatement()
        {
            Location = new Location(statement.Position, statement.File),
        };
        return true;
    }
    bool CompileStatement(Statement statement, [NotNullWhen(true)] out CompiledStatement? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        Settings.CancellationToken.ThrowIfCancellationRequested();

        if (statement is IMissingNode)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Incomplete AST", statement, false));
            compiledStatement = null;
            return false;
        }

        switch (statement)
        {
            case Expression v:
                bool res = CompileExpression(v, out CompiledExpression? compiledStatementWithValue);
                compiledStatement = compiledStatementWithValue;
                return res;
            case VariableDefinition v: return CompileStatement(v, out compiledStatement);
            case KeywordCallStatement v: return CompileStatement(v, out compiledStatement);
            case AssignmentStatement v: return CompileStatement(v.ToAssignment(), out compiledStatement);
            case WhileLoopStatement v: return CompileStatement(v, out compiledStatement);
            case ForLoopStatement v: return CompileStatement(v, out compiledStatement);
            case IfBranchStatement v: return CompileStatement(v, out compiledStatement);
            case Block v: return CompileStatement(v, out compiledStatement);
            case InstructionLabelDeclaration v: return CompileStatement(v, out compiledStatement);
            case EmptyStatement v: return CompileStatement(v, out compiledStatement);
            default: throw new NotImplementedException($"Statement {statement.GetType().Name} is not implemented");
        }
    }

    bool CompileSetter(Statement target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (target is IMissingNode)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Incomplete AST", target, false));
            compiledStatement = null;
            CompileExpression(value, out _);
            return false;
        }

        switch (target)
        {
            case IdentifierExpression v: return CompileSetter(v, value, out compiledStatement);
            case FieldExpression v: return CompileSetter(v, value, out compiledStatement);
            case IndexCallExpression v: return CompileSetter(v, value, out compiledStatement);
            case DereferenceExpression v: return CompileSetter(v, value, out compiledStatement);
            default:
                Diagnostics.Add(DiagnosticAt.Error($"The left side of the assignment operator should be a variable, field or memory address. Passed \"{target.GetType().Name}\"", target));
                return false;
        }
    }
    bool CompileSetter(IdentifierExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (RegisterKeywords.TryGetValue(target.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            if (!CompileExpression(value, out CompiledExpression? _value, registerKeyword.Type)) return false;

            if (!CanCastImplicitly(_value, registerKeyword.Type, out _value, out PossibleDiagnostic? castError, out _))
            { Diagnostics.Add(castError.ToError(value)); }

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledRegisterAccess()
                {
                    Register = registerKeyword.Register,
                    Location = target.Location.Union(value.Location),
                    SaveValue = true,
                    Type = registerKeyword.Type,
                },
                Value = _value,
                Location = target.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledRegisterAccess _v2 &&
                    _v2.Register == registerKeyword.Register,
            };
            return true;
        }

        if (GetConstant(target.Content, target.File, out CompiledVariableConstant? constant, out _))
        {
            target.AnalyzedType = TokenAnalyzedType.ConstantName;
            SetStatementReference(target, constant);

            Diagnostics.Add(DiagnosticAt.Error($"Can not set constant value: it is readonly", target));
            return false;
        }

        if (GetParameter(target.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (target.Content != StatementKeywords.This)
            { target.AnalyzedType = TokenAnalyzedType.ParameterName; }
            SetStatementType(target, parameter.Type);
            SetStatementReference(target, parameter);

            if (!CompileExpression(value, out CompiledExpression? _value, parameter.Type)) return false;

            if (!CanCastImplicitly(_value, parameter.Type, out _value, out PossibleDiagnostic? castError, out _))
            {
                Diagnostics.Add(castError.ToError(value));
            }

            if (parameter.Definition.Modifiers.Contains(ModifierKeywords.Temp))
            {
                Diagnostics.Add(DiagnosticAt.Warning($"Please don't assign a temp parameter. Thanks", target));
            }

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledParameterAccess()
                {
                    Parameter = parameter,
                    Location = target.Location.Union(value.Location),
                    SaveValue = true,
                    Type = parameter.Type,
                },
                Value = _value,
                Location = target.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledParameterAccess _v2 &&
                    Utils.ReferenceEquals(_v2.Parameter, parameter),
            };
            return true;
        }

        if (GetVariable(target.Content, out CompiledVariableDefinition? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            target.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementType(target, variable.Type);
            SetStatementReference(target, variable);

            if (!CompileExpression(value, out CompiledExpression? _value, variable.Type)) return false;

            if (variable.IsGlobal)
            { Diagnostics.Add(DiagnosticAt.Internal($"Trying to set local variable \"{variable.Identifier}\" but it was compiled as a global variable.", target)); }

            if (variable.Definition.Modifiers.Contains(ModifierKeywords.Temp))
            {
                Diagnostics.Add(DiagnosticAt.Warning($"Please don't assign a temp variable. Thanks", target));
            }

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledVariableAccess()
                {
                    Variable = variable,
                    Type = variable.Type,
                    Location = target.Location.Union(value.Location),
                    SaveValue = true,
                },
                Value = _value,
                Location = target.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledVariableAccess _v2 &&
                    Utils.ReferenceEquals(_v2.Variable, variable),
            };
            return true;
        }

        if (GetGlobalVariable(target.Content, target.File, out CompiledVariableDefinition? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            target.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementType(target, globalVariable.Type);
            SetStatementReference(target, globalVariable);
            Frames.Last.CapturesGlobalVariables = true;

            if (!CompileExpression(value, out CompiledExpression? _value, globalVariable.Type)) return false;

            if (!globalVariable.IsGlobal)
            { Diagnostics.Add(DiagnosticAt.Internal($"Trying to set global variable \"{globalVariable.Identifier}\" but it was compiled as a local variable.", target)); }

            if (globalVariable.Definition.Modifiers.Contains(ModifierKeywords.Temp))
            {
                Diagnostics.Add(DiagnosticAt.Warning($"Please don't assign a temp variable. Thanks", target));
            }

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledVariableAccess()
                {
                    Variable = globalVariable,
                    Type = globalVariable.Type,
                    Location = target.Location.Union(value.Location),
                    SaveValue = true,
                },
                Value = _value,
                Location = target.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledVariableAccess _v2 &&
                    Utils.ReferenceEquals(_v2.Variable, globalVariable),
            };
            return true;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Symbol \"{target.Content}\" not found", target, ignoreOnPartialSource: true)
            .WithSuberrors(
                parameterNotFoundError.ToError(target),
                variableNotFoundError.ToError(target),
                globalVariableNotFoundError.ToError(target)
            ));
        return false;
    }
    bool CompileSetter(FieldExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        target.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        if (!CompileExpression(target.Object, out CompiledExpression? prev))
        {
            CompileExpression(value, out _);
            return false;
        }

        GeneralType prevType = prev.Type;

        if (prevType.Is<ArrayType>() && target.Identifier.Content == "Length")
        {
            Diagnostics.Add(DiagnosticAt.Error("Array type's length is readonly", target));
            return false;
        }

        if (prevType.Is<IReferenceType>())
        {
            while (prevType.Is(out IReferenceType? referenceType))
            {
                prevType = referenceType.To;
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(DiagnosticAt.Error($"Could not get the field offsets of type \"{prevType}\"", target.Object));
                return false;
            }

            if (!structPointerType.GetField(target.Identifier.Content, out CompiledField? compiledField, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(target.Identifier, target.File));
                return false;
            }

            GeneralType type = GeneralType.InsertTypeParameters(compiledField.Type, structPointerType.TypeArguments);
            if (!CompileExpression(value, out CompiledExpression? _value, type)) return false;

            if (!CanCastImplicitly(_value, type, out _value, out PossibleDiagnostic? castError2, out _))
            {
                Diagnostics.Add(castError2.ToError(value));
            }

            SetStatementType(target, compiledField.Type);
            SetStatementReference(target, compiledField);

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledFieldAccess()
                {
                    Object = prev,
                    Field = compiledField,
                    Type = type,
                    Location = target.Location,
                    SaveValue = true,
                },
                Location = target.Location,
                Value = _value,
                IsCompoundAssignment = false,
            };
            return true;
        }

        if (prevType.Is(out StructType? structType))
        {
            if (!structType.GetField(target.Identifier.Content, out CompiledField? compiledField, out PossibleDiagnostic? error2))
            {
                Diagnostics.Add(error2.ToError(target.Identifier, target.File));
                return false;
            }

            GeneralType type = GeneralType.TryInsertTypeParameters(compiledField.Type, structType.TypeArguments);
            if (!CompileExpression(value, out CompiledExpression? _value, type)) return false;

            if (!CanCastImplicitly(_value, type, out _value, out PossibleDiagnostic? castError2, out _))
            {
                Diagnostics.Add(castError2.ToError(value));
            }

            SetStatementType(target, compiledField.Type);
            SetStatementReference(target, compiledField);

            compiledStatement = new CompiledSetter()
            {
                Target = new CompiledFieldAccess()
                {
                    Field = compiledField,
                    Object = prev,
                    Type = type,
                    Location = target.Location,
                    SaveValue = true,
                },
                Location = target.Location,
                Value = _value,
                IsCompoundAssignment = false,
            };
            return true;
        }

        Diagnostics.Add(DiagnosticAt.Error($"Type `{prevType}` doesn't have any fields", target.Identifier, target.File, ignoreOnPartialSource: prev.Type.Equals(BuiltinType.Any)));
        return false;
    }
    bool CompileSetter(IndexCallExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileExpression(target.Object, out CompiledExpression? _base)) return false;
        if (!CompileExpression(target.Index, out CompiledExpression? _index)) return false;

        if (!CompileExpression(value, out CompiledExpression? _value)) return false;

        if (GetIndexSetter(_base, _value, _index, target.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? indexerNotFoundError, AddCompilable))
        {
            indexer.Function.AddReference(target, target.Location);
            if (CompileFunctionCall(target, ImmutableArray.Create(_base, _index, _value), ImmutableArray.Create(ArgumentExpression.Wrap(target.Object), target.Index, ArgumentExpression.Wrap(value)), indexer, out CompiledExpression? compiledStatement2))
            {
                compiledStatement = compiledStatement2;
                return true;
            }
            else
            {
                return false;
            }
        }

        if (GetIndexGetter(_base, _index, target.File, out indexer, out PossibleDiagnostic? indexerNotFoundError2, AddCompilable))
        {
            indexer.Function.AddReference(target, target.Location);
            if (CompileFunctionCall(target, ImmutableArray.Create(_base, _index), ImmutableArray.Create(ArgumentExpression.Wrap(target.Object), target.Index), indexer, out CompiledExpression? compiledStatement2))
            {
                if (compiledStatement2.Type.Is(out IReferenceType? referenceReturnType))
                {
                    if (CanCastImplicitly(_value.Type, referenceReturnType.To, out PossibleDiagnostic? castError2, out _))
                    {
                        compiledStatement = new CompiledSetter()
                        {
                            Target = new CompiledDereference()
                            {
                                Address = compiledStatement2,
                                Type = referenceReturnType.To,
                                Location = target.Location,
                                SaveValue = true,
                            },
                            Value = _value,
                            Location = target.Location.Union(value.Location),
                            IsCompoundAssignment = false,
                        };
                        return true;
                    }
                    else
                    {
                        indexerNotFoundError2 = castError2;
                    }
                }
                else
                {
                    indexerNotFoundError2 = new PossibleDiagnostic($"Indexer get return type must be a reference type to be able to use as a setter");
                }
            }
            else
            {
                return false;
            }
        }

        GeneralType? itemType = null;

        if (_base.Type.Is(out ArrayType? arrayType))
        {
            SetStatementType(target, itemType = arrayType.Of);
        }
        else if (_base.Type.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            itemType = arrayType.Of;
        }
        else
        {
            Diagnostics.Add(indexerNotFoundError.ToError(target));
            Diagnostics.Add(indexerNotFoundError2.ToError(target));
            return false;
        }

        if (!CanCastImplicitly(_value, itemType, out _value, out PossibleDiagnostic? castError, out _))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        compiledStatement = new CompiledSetter()
        {
            Target = new CompiledElementAccess()
            {
                Base = _base,
                Index = _index,
                Type = itemType,
                Location = target.Location,
                SaveValue = true,
            },
            Value = _value,
            Location = target.Location.Union(value.Location),
            IsCompoundAssignment = false,
        };
        return true;
    }
    bool CompileSetter(DereferenceExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileExpression(target.Expression, out CompiledExpression? prev)) return false;

        GeneralType targetType;
        if (prev.Type.Is(out PointerType? pointerType))
        { targetType = SetStatementType(target, pointerType.To); }
        else
        { targetType = SetStatementType(target, BuiltinType.Any); }

        if (!CompileExpression(value, out CompiledExpression? _value, targetType)) return false;

        if (!CanCastImplicitly(_value, targetType, out _value, out PossibleDiagnostic? castError, out _))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (!prev.Type.Is<PointerType>())
        {
            Diagnostics.Add(DiagnosticAt.Error($"Type \"{prev.Type}\" isn't a pointer", target.Expression));
            return false;
        }

        compiledStatement = new CompiledSetter()
        {
            Target = new CompiledDereference()
            {
                Address = prev,
                Type = targetType,
                Location = target.Location,
                SaveValue = true,
            },
            Value = _value,
            Location = target.Location.Union(value.Location),
            IsCompoundAssignment = false,
        };
        return true;
    }

    Scope CompileScope(IEnumerable<Statement> statements)
    {
        ImmutableArray<CompiledVariableConstant>.Builder localConstants = ImmutableArray.CreateBuilder<CompiledVariableConstant>();

        foreach (Statement item in statements)
        {
            if (item is VariableDefinition variableDeclaration)
            {
                if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
                {
                    if (!CompileConstant(variableDeclaration, out CompiledVariableConstant? variable)) continue;
                    localConstants.Add(variable);
                }
            }
        }

        return new Scope(localConstants.ToImmutable());
    }

    public static bool CompileType(
        RuntimeType type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        switch (type)
        {
            case RuntimeType.U8: result = BuiltinType.U8; error = null; return true;
            case RuntimeType.I8: result = BuiltinType.I8; error = null; return true;
            case RuntimeType.U16: result = BuiltinType.Char; error = null; return true;
            case RuntimeType.I16: result = BuiltinType.I16; error = null; return true;
            case RuntimeType.U32: result = BuiltinType.U32; error = null; return true;
            case RuntimeType.I32: result = BuiltinType.I32; error = null; return true;
            case RuntimeType.F32: result = BuiltinType.F32; error = null; return true;
            case RuntimeType.Null: result = null; error = new($"Invalid type"); return false;
            default: throw new UnreachableException();
        }
    }

    readonly List<TemplateInstance<FunctionThingDefinition>> _generatedFunctions = new();

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m2 = new("LanguageCore.Compiler.Function");
#endif
    bool CompileFunction<TFunction>(TFunction function, ImmutableDictionary<string, GeneralType>? typeArguments)
        where TFunction : ICompiledFunctionDefinition, ICompiledDefinition<FunctionThingDefinition>
    {
        foreach (TemplateInstance<FunctionThingDefinition> item in _generatedFunctions)
        {
            if (Utils.ReferenceEquals(item.Template, function.Definition) && TypeArgumentsEquals(item.TypeArguments, typeArguments))
            {
                if (GeneratedFunctions.Any(v => Utils.ReferenceEquals(v.Function, function) && TypeArgumentsEquals(v.TypeArguments, typeArguments)))
                {
                    // Something went wrong bruh
                }
                return false;
            }
        }
        _generatedFunctions.Add(new TemplateInstance<FunctionThingDefinition>(function.Definition, typeArguments));

#if UNITY
        using var _1 = _m2.Auto();
#endif

        if (LanguageConstants.KeywordList.Except(BuiltinFunctionIdentifiers.All).Contains(function.Definition.Identifier.Content))
        {
            Diagnostics.Add(DiagnosticAt.Error($"The identifier \"{function.Definition.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Definition.Identifier, function.File));
            goto end;
        }

        if (function is IExternalFunctionDefinition externalFunctionDefinition &&
            externalFunctionDefinition.ExternalFunctionName is not null &&
            (ExternalFunctions.Any(v => v.Name == externalFunctionDefinition.ExternalFunctionName) || function.Definition.Block is null))
        {
            // fixme: hmmm
            return false;
        }

        if (function.Definition.Block is null)
        {
            Diagnostics.Add(DiagnosticAt.Error($"Function \"{function.ToReadable()}\" does not have a body", function.Definition));
            goto end;
        }

        GeneralType returnType = GeneralType.TryInsertTypeParameters(function.Type, typeArguments);
        ImmutableArray<CompiledParameter>.Builder compiledParameters = ImmutableArray.CreateBuilder<CompiledParameter>();

        for (int i = 0; i < function.Definition.Parameters.Length; i++)
        {
            compiledParameters.Add(function.Parameters[i]);
        }

        ImmutableArray<CompiledLabelDeclaration>.Builder localInstructionLabels = ImmutableArray.CreateBuilder<CompiledLabelDeclaration>();

        foreach (Statement item in StatementWalker.Visit(function.Definition.Block, StatementWalkerFilter.FrameOnlyFilter))
        {
            if (item is InstructionLabelDeclaration instructionLabel)
            {
                localInstructionLabels.Add(new CompiledLabelDeclaration()
                {
                    Identifier = instructionLabel.Identifier.Content,
                    Location = instructionLabel.Location,
                });
            }
        }

        using (StackAuto<CompiledFrame> frame = Frames.PushAuto(new CompiledFrame()
        {
            TypeArguments = typeArguments ?? ImmutableDictionary<string, GeneralType>.Empty,
            IsTemplateInstance = typeArguments is not null,
            IsTemplate = function.Definition.IsTemplate,
            TypeParameters = function.Definition.Template?.Parameters ?? ImmutableArray<Token>.Empty,
            CompiledParameters = compiledParameters.ToImmutable(),
            InstructionLabels = localInstructionLabels.ToImmutable(),
            Scopes = new(),
            CurrentReturnType = returnType,
            IsTopLevel = false,
        }))
        {
            CompiledBlock? body;
            using (frame.Value.Scopes.PushAuto(new Scope(ImmutableArray<CompiledVariableConstant>.Empty)))
            {
                if (!CompileStatement(function.Definition.Block, out body)) return false;
            }

            ImmutableArray<CapturedLocal>.Builder closureBuilder = ImmutableArray.CreateBuilder<CapturedLocal>(frame.Value.CapturedVariables.Count + frame.Value.CapturedParameters.Count);
            foreach (CompiledParameter item in frame.Value.CapturedParameters)
            {
                closureBuilder.Add(new()
                {
                    ByRef = false,
                    Parameter = item,
                    Variable = null,
                });
            }
            foreach (CompiledVariableDefinition item in frame.Value.CapturedVariables)
            {
                closureBuilder.Add(new()
                {
                    ByRef = false,
                    Parameter = null,
                    Variable = item,
                });
            }
            ImmutableArray<CapturedLocal> closure = closureBuilder.MoveToImmutable();

            function.IsMsilCompatible = function.IsMsilCompatible && frame.Value.IsMsilCompatible;

            GeneratedFunctions.Add(new(function, body, closure, typeArguments));

            if (!closure.IsEmpty)
            {
                Diagnostics.Add(DiagnosticAt.Error($"Closures are not supported in this context", function)
                    .WithRelatedInfo(closure.Select(v =>
                        v.Parameter is not null ? new DiagnosticRelatedInformationAt($"Captured parameter \"{v.Parameter.Identifier}\"", v.Parameter.Location) :
                        v.Variable is not null ? new DiagnosticRelatedInformationAt($"Captured variable \"{v.Variable.Identifier}\"", v.Variable.Location) :
                        new DiagnosticRelatedInformation("meow"))));
            }

            return true;
        }

    end:
        return false;
    }

    bool CompileFunctions<TFunction>(IEnumerable<TFunction> functions)
        where TFunction : IHaveInstructionOffset, IReferenceable, ICompiledFunctionDefinition, ICompiledDefinition<FunctionThingDefinition>
    {
        bool compiledAnything = false;
        foreach (TFunction function in functions)
        {
            if (function.Definition.IsTemplate) continue;
            if (!Settings.CompileEverything)
            {
                if (!function.GetReferences().Any() && (function is not IExposeable exposeable || exposeable.ExposedFunctionName is null) && !function.Definition.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out _))
                { continue; }
            }

            if (CompileFunction(function, null))
            { compiledAnything = true; }
        }
        return compiledAnything;
    }
    bool CompileFunctionTemplates<T>(IReadOnlyList<TemplateInstance<T>> functions)
        where T : IHaveInstructionOffset, ICompiledFunctionDefinition, ICompiledDefinition<FunctionThingDefinition>
    {
        bool compiledAnything = false;
        int i = 0;
        while (i < functions.Count)
        {
            TemplateInstance<T> function = functions[i];
            i++;

            if (CompileFunction(function.Template, function.TypeArguments))
            { compiledAnything = true; }
        }
        return compiledAnything;
    }

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m1 = new("LanguageCore.Compiler.TopLevelStatements");
#endif
    bool CompileTopLevelStatements(ImmutableArray<Statement> statements, [NotNullWhen(true)] out ImmutableArray<CompiledStatement> compiledStatements)
    {
#if UNITY
        using var _1 = _m1.Auto();
#endif

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(statements.Length);

        bool success = true;
        foreach (Statement statement in statements)
        {
            if (!CompileStatement(statement, out CompiledStatement? compiledStatement))
            {
                success = false;
                continue;
            }

            if (Settings.IsExpression)
            {
                res.Add(compiledStatement);
            }
            else
            {
                res.AddRange(ReduceStatements(compiledStatement, Diagnostics));
            }
        }
        if (!success)
        {
            compiledStatements = ImmutableArray<CompiledStatement>.Empty;
            return false;
        }

        compiledStatements = res.ToImmutable();
        return true;
    }

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m4 = new("LanguageCore.Compiler.CompileExpression");
#endif
    bool CompileExpression(Statement statement, Dictionary<string, int>? contextualVariables, [NotNullWhen(true)] out ImmutableArray<CompiledStatement> compiledStatements)
    {
#if UNITY
        using var _1 = _m4.Auto();
#endif

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(1);

        if (!CompileStatement(statement, out CompiledStatement? compiledStatement))
        {
            compiledStatements = ImmutableArray<CompiledStatement>.Empty;
            return false;
        }

        res.Add(compiledStatement);

        compiledStatements = res.ToImmutable();
        return true;
    }

    void GenerateCode(Uri entryFile)
    {
        ImmutableArray<CompiledLabelDeclaration>.Builder globalInstructionLabels = ImmutableArray.CreateBuilder<CompiledLabelDeclaration>();

        foreach (InstructionLabelDeclaration instructionLabel in TopLevelStatements
            .SelectMany(v => v.Statements)
            .SelectMany(v => StatementWalker.Visit(v, StatementWalkerFilter.FrameOnlyFilter))
            .OfType<InstructionLabelDeclaration>())
        {
            globalInstructionLabels.Add(new CompiledLabelDeclaration()
            {
                Identifier = instructionLabel.Identifier.Content,
                Location = instructionLabel.Location,
            });
        }

        using (StackAuto<CompiledFrame> frame = Frames.PushAuto(new CompiledFrame()
        {
            TypeArguments = ImmutableDictionary<string, GeneralType>.Empty,
            IsTemplateInstance = false,
            IsTemplate = false,
            TypeParameters = ImmutableArray<Token>.Empty,
            CompiledParameters = ImmutableArray<CompiledParameter>.Empty,
            InstructionLabels = globalInstructionLabels.ToImmutable(),
            Scopes = new(),
            CurrentReturnType = ExitCodeType,
            IsTopLevel = true,
        }))
        {
            using (Frames.Last.Scopes.PushAuto(new Scope(ImmutableArray<CompiledVariableConstant>.Empty)))
            {
                if (Settings.IsExpression)
                {
                    foreach ((ImmutableArray<Statement> statements, Uri file) in TopLevelStatements)
                    {
                        if (file != entryFile) continue;
                        if (!CompileTopLevelStatements(statements, out ImmutableArray<CompiledStatement> v)) continue;
                        CompiledTopLevelStatements.AddRange(v);
                    }
                }
                else if (!Settings.IgnoreTopLevelStatements)
                {
                    foreach ((ImmutableArray<Statement> statements, Uri file) in TopLevelStatements)
                    {
                        if (!CompileTopLevelStatements(statements, out ImmutableArray<CompiledStatement> v)) continue;
                        CompiledTopLevelStatements.AddRange(v);
                    }
                }

                while (true)
                {
                    bool compiledAnything = false;

                    compiledAnything = CompileFunctions(CompiledFunctions) || compiledAnything;
                    compiledAnything = CompileFunctions(CompiledOperators) || compiledAnything;
                    compiledAnything = CompileFunctions(CompiledGeneralFunctions) || compiledAnything;
                    compiledAnything = CompileFunctions(CompiledConstructors) || compiledAnything;

                    compiledAnything = CompileFunctionTemplates(CompilableFunctions) || compiledAnything;
                    compiledAnything = CompileFunctionTemplates(CompilableConstructors) || compiledAnything;
                    compiledAnything = CompileFunctionTemplates(CompilableOperators) || compiledAnything;
                    compiledAnything = CompileFunctionTemplates(CompilableGeneralFunctions) || compiledAnything;

                    if (!compiledAnything) break;
                }
            }
            if (frame.Value.CapturedParameters.Count > 0 || frame.Value.CapturedVariables.Count > 0) throw new UnreachableException();
        }

        foreach (CompiledVariableDefinition item in Visit(CompiledTopLevelStatements).OfType<CompiledVariableDefinition>())
        {
            if (!item.IsGlobal) continue;
            foreach (CompiledVariableAccess possibleRef in GeneratedFunctions.SelectMany(v => Visit(v.Body)).OfType<CompiledVariableAccess>())
            {
                if (Utils.ReferenceEquals(possibleRef.Variable, item))
                {
                    goto skip;
                }
            }
            item.IsOnlyUsedLocally = true;
        skip:;
        }

        foreach (CompiledFunction function in GeneratedFunctions)
        {
            AnalyseFunction(function, new());
        }

        FunctionFlags topLevelFlags = default;
        Location? firstHeapUsageLocation = default;
        StatementWalker.Visit(CompiledTopLevelStatements.Append(GeneratedFunctions.Where(v => v.Function is IExposeable exposeable && exposeable.ExposedFunctionName is not null).Select(v => v.Body)), v =>
        {
            FunctionFlags flags = GetStatementFlags(v);
            if (flags.HasFlag(FunctionFlags.AllocatesMemory) || topLevelFlags.HasFlag(FunctionFlags.DeallocatesMemory))
            {
                firstHeapUsageLocation ??= new Location(new Position(new Range<SinglePosition>(v.Location.Position.Range.Start, v.Location.Position.Range.Start), new Range<int>(v.Location.Position.AbsoluteRange.Start, v.Location.Position.AbsoluteRange.Start)), v.Location.File);
            }
            topLevelFlags |= flags;
            return true;
        });

        if (topLevelFlags.HasFlag(FunctionFlags.AllocatesMemory) || topLevelFlags.HasFlag(FunctionFlags.DeallocatesMemory))
        {
            if (!firstHeapUsageLocation.HasValue) throw new UnreachableException();

            if (!TryGetBuiltinFunction(BuiltinFunctions.InitializeHeap, ImmutableArray<CompiledExpression>.Empty, entryFile, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
            {
                Diagnostics.Add(
                    DiagnosticAt.Error($"Failed to generate heap initialization code", firstHeapUsageLocation.Value)
                    .WithSuberrors(
                        DiagnosticAt.Error($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.InitializeHeap}\")] not found", firstHeapUsageLocation.Value)
                        .WithSuberrors(
                            notFoundError.ToError(firstHeapUsageLocation.Value)
                        )
                    )
                );
            }
            else if (!Settings.IgnoreTopLevelStatements)
            {
                if (CompileFunctionCall(new FunctionCallExpression(
                    null,
                    Token.CreateAnonymous(result.Function.Identifier, TokenType.Identifier, firstHeapUsageLocation.Value.Position),
                    ArgumentListExpression.CreateAnonymous(TokenPair.CreateAnonymous(firstHeapUsageLocation.Value.Position, "(", ")"), entryFile),
                    entryFile
                ), ImmutableArray<CompiledExpression>.Empty, ImmutableArray<ArgumentExpression>.Empty, result, out CompiledExpression? call))
                {
                    CompiledTopLevelStatements.Insert(0, call);
                }
                else
                {
                    Diagnostics.Add(DiagnosticAt.Warning($"Heap initialization code not generated", firstHeapUsageLocation.Value));
                }
            }
            else
            {
                Diagnostics.Add(DiagnosticAt.Warning($"Heap initialization code not generated because top level statements are ignored by the settings. Use a memory with an already initialized heap when you execute the code.", firstHeapUsageLocation.Value));
            }
        }
    }

    FunctionFlags GetStatementFlags(CompiledStatement statement)
    {
        static bool IsAllocator(ICompiledFunctionDefinition function) =>
            function is IHaveAttributes haveAttributes
            && haveAttributes.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? attribute)
            && attribute.TryGetValue(out string? value)
            && value == BuiltinFunctions.Allocate;

        static bool IsDeallocator(ICompiledFunctionDefinition function) =>
            function is IHaveAttributes haveAttributes
            && haveAttributes.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? attribute)
            && attribute.TryGetValue(out string? value)
            && value == BuiltinFunctions.Free;

        FunctionFlags flags = default;
        switch (statement)
        {
            case CompiledLambda v:
            {
                flags |= v.Flags;
                break;
            }
            case CompiledVariableAccess v:
            {
                if (v.Variable.IsGlobal)
                {
                    flags |= FunctionFlags.CapturesGlobalVariables;
                }
                break;
            }
            case CompiledFunctionCall v:
            {
                if (IsAllocator(v.Function.Template)) flags |= FunctionFlags.AllocatesMemory;
                if (IsDeallocator(v.Function.Template)) flags |= FunctionFlags.DeallocatesMemory;
                CompiledFunction? f = GeneratedFunctions.FirstOrDefault(w => Utils.ReferenceEquals(w.Function, v.Function.Template) && TypeArgumentsEquals(w.TypeArguments, v.Function.TypeArguments));
                if (f is not null) flags |= f.Flags;
                break;
            }
            case CompiledExternalFunctionCall v:
            {
                if (IsAllocator(v.Declaration)) flags |= FunctionFlags.AllocatesMemory;
                if (IsDeallocator(v.Declaration)) flags |= FunctionFlags.DeallocatesMemory;
                break;
            }
            case CompiledFunctionReference v:
            {
                if (v.Function is ICompiledFunctionDefinition f1 && IsAllocator(f1)) flags |= FunctionFlags.AllocatesMemory;
                if (v.Function is ICompiledFunctionDefinition f2 && IsDeallocator(f2)) flags |= FunctionFlags.DeallocatesMemory;
                CompiledFunction? f = GeneratedFunctions.FirstOrDefault(w => Utils.ReferenceEquals(w.Function, v.Function.Template) && TypeArgumentsEquals(w.TypeArguments, v.Function.TypeArguments));
                if (f is not null) flags |= f.Flags;
                break;
            }
        }
        return flags;
    }

    FunctionFlags AnalyseFunction(CompiledStatement statement, HashSet<CompiledFunction> visited)
    {
        FunctionFlags flags = default;

        StatementWalker.VisitWithFunctions(GeneratedFunctions, statement, statement =>
        {
            switch (statement)
            {
                case CompiledLambda v:
                    v.Flags = AnalyseFunction(v.Block, visited);
                    break;
                default:
                    flags |= GetStatementFlags(statement);
                    break;
            }
            return true;
        }, function =>
        {
            AnalyseFunction(function, visited);
            flags |= function.Flags;
        });
        return flags;
    }

    void AnalyseFunction(CompiledFunction function, HashSet<CompiledFunction> visited)
    {
        if (!visited.Add(function)) return;
        function.Flags = default;
        function.Flags = AnalyseFunction(function.Body, visited);
    }
}
