using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
    bool CompileAllocation(CompiledTypeExpression type, [NotNullWhen(true)] out CompiledExpression? compiledStatement)
    {
        if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating))
        {
            if (FindSize(type, out int typeSize, out PossibleDiagnostic? typeSizeError, this))
            {
                return CompileAllocation(typeSize, type.Location, out compiledStatement);
            }
            else
            {
                Diagnostics.Add(Diagnostic.FailedOptimization($"Failed to compute allocation size", type).WithSuberrors(typeSizeError.ToError(type, false)));
            }
        }

        compiledStatement = null;

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, ImmutableArray.Create<GeneralType>(SizeofStatementType), type.Location.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", type));
            return false;
        }

        if (result.DidReplaceArguments) throw new UnreachableException();

        //result.Function.References.AddReference(type, type.Location.File);
        //result.OriginalFunction.References.AddReference(type, type.Location.File);

        CompiledFunctionDefinition allocator = result.Function;
        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", allocator.TypeToken));
            return false;
        }

        if (!allocator.CanUse(type.Location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", type));
            return false;
        }

        ImmutableArray<CompiledArgument> compiledArguments = ImmutableArray.Create(CompiledArgument.Wrap(new CompiledSizeof()
        {
            Of = type,
            Location = type.Location,
            SaveValue = true,
            Type = SizeofStatementType,
        }));

        if (allocator.ExternalFunctionName is not null)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator));
                return false;
            }

            return CompileFunctionCall_External(compiledArguments, true, allocator, externalFunction, type.Location, out compiledStatement);
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Function = allocator,
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
            Diagnostics.Add(Diagnostic.Warning($"No type defined for integer literals, using the default {intType}", sizeLocation).WithSuberrors(typeError.ToError(sizeLocation, false)));
        }

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, ImmutableArray.Create<GeneralType>(intType), sizeLocation.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", sizeLocation));
            return false;
        }

        if (result.DidReplaceArguments) throw new UnreachableException();

        //result.Function.References.AddReference(type, type.Location.File);
        //result.OriginalFunction.References.AddReference(type, type.Location.File);

        CompiledFunctionDefinition allocator = result.Function;
        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", allocator.TypeToken));
            return false;
        }

        if (!allocator.CanUse(sizeLocation.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", sizeLocation));
            return false;
        }

        ImmutableArray<CompiledArgument> compiledArguments = ImmutableArray.Create(CompiledArgument.Wrap(new CompiledConstantValue()
        {
            Value = size,
            Location = sizeLocation,
            SaveValue = true,
            Type = intType,
        }));

        if (allocator.ExternalFunctionName is not null)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator));
                return false;
            }

            return CompileFunctionCall_External(compiledArguments, true, allocator, externalFunction, sizeLocation, out compiledStatement);
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Function = allocator,
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
        ImmutableArray<GeneralType> parameterTypes = ImmutableArray.Create(deallocateableType);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, location.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", location).WithSuberrors(notFoundError.ToError(location)));
            return false;
        }

        result.Function.References.Add(new Reference<Expression?>(null, location.File));
        result.OriginalFunction.References.Add(new Reference<Expression?>(null, location.File));

        if (result.DidReplaceArguments) throw new UnreachableException();

        deallocator = result.Function;

        if (!deallocator.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", location));
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

        CompiledGeneralFunctionDefinition? destructor = null;
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
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out FunctionQueryResult<CompiledGeneralFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (deallocateablePointerType.To.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning($"Destructor for type \"{deallocateablePointerType.To}\" not found", location).WithSuberrors(error.ToWarning(location)));
                }
            }
            else
            {
                destructor = result.Function;
                result.Function.References.Add(new Reference<Expression?>(null, location.File));
                result.OriginalFunction.References.Add(new Reference<Expression?>(null, location.File));
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out FunctionQueryResult<CompiledGeneralFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (deallocateableType.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning($"Destructor for type \"{deallocateableType}\" not found", location).WithSuberrors(error.ToWarning(location)));
                }
            }
            else
            {
                destructor = result.Function;
                result.Function.References.Add(new Reference<Expression?>(null, location.File));
                result.OriginalFunction.References.Add(new Reference<Expression?>(null, location.File));
            }
        }

        if (destructor is not null
            && !destructor.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" cannot be called due to its protection level", location));
            return false;
        }

        compiledCleanup = new CompiledCleanup()
        {
            Deallocator = deallocator,
            Destructor = destructor,
            Location = location,
            TrashType = deallocateableType,
        };
        return true;
    }

    #region CompileStatement

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
            TypeInstanceSimple simpleType => CompileStatement(simpleType, out result, diagnostics),
            TypeInstanceFunction functionType => CompileStatement(functionType, out result, diagnostics),
            TypeInstanceStackArray stackArrayType => CompileStatement(stackArrayType, out result, diagnostics),
            TypeInstancePointer pointerType => CompileStatement(pointerType, out result, diagnostics),
            _ => throw new UnreachableException(),
        };
    }
    bool CompileStatement(TypeInstanceSimple type, [NotNullWhen(true)] out CompiledTypeExpression? result, DiagnosticsCollection diagnostics)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new CompiledBuiltinTypeExpression(builtinType, type.Location);
            type.Identifier.AnalyzedType = TokenAnalyzedType.BuiltinType;
            return true;
        }

        if (!FindType(type.Identifier, type.File, out result, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(type));
            return false;
        }

        type.Identifier.AnalyzedType = result.FinalValue switch
        {
            CompiledGenericTypeExpression => TokenAnalyzedType.TypeParameter,
            CompiledStructTypeExpression => TokenAnalyzedType.Struct,
            CompiledBuiltinTypeExpression => TokenAnalyzedType.BuiltinType,
            CompiledAliasTypeExpression => TokenAnalyzedType.TypeAlias,
            _ => TokenAnalyzedType.Type,
        };

        if (result.Is(out CompiledStructTypeExpression? resultStructType) &&
            resultStructType.Struct.Template is not null)
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
        else
        {
            if (type.TypeArguments.HasValue)
            {
                Diagnostics.Add(Diagnostic.Internal($"Asd", type));
                return false;
            }
        }

        //type.SetAnalyzedType(result);
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

    bool CompileArguments(IReadOnlyList<ArgumentExpression> arguments, ICompiledFunctionDefinition compiledFunction, [NotNullWhen(true)] out ImmutableArray<CompiledArgument> compiledArguments, int alreadyPassed = 0)
    {
        compiledArguments = ImmutableArray<CompiledArgument>.Empty;

        ImmutableArray<CompiledArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledArgument>(arguments.Count);

        // int partial = 0;
        // for (int i = 0; i < compiledFunction.Parameters.Count; i++)
        // {
        //     if (compiledFunction.Parameters[i].DefaultValue is null) partial = i + 1;
        //     else break;
        // }

        // TODO:
        // if (arguments.Count < partial)
        // {
        //     Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{callee.ToReadable()}\": required {compiledFunction.ParameterCount} passed {arguments.Count}", caller));
        //     return false;
        // }

        // TODO: A hint if the passed value is the same as the default value

        for (int i = 0; i < arguments.Count; i++)
        {
            CompiledParameter parameter = compiledFunction.Parameters[i + alreadyPassed];
            ArgumentExpression argument = arguments[i];

            if (!CompileExpression(argument, out CompiledExpression? compiledArgument, parameter.Type)) return false;

            if (parameter.Type.Is<PointerType>() &&
                parameter.Modifiers.Any(v => v.Content == ModifierKeywords.This) &&
                !compiledArgument.Type.Is<PointerType>())
            {
                if (!CompileExpression(new GetReferenceExpression(
                    Token.CreateAnonymous("&", TokenType.Operator, argument.Position.Before()),
                    argument,
                    argument.File
                ), out compiledArgument, parameter.Type))
                { return false; }
            }

            if (!CanCastImplicitly(compiledArgument, parameter.Type, out CompiledExpression? assignedArgument, out PossibleDiagnostic? error))
            { Diagnostics.Add(error.ToError(argument)); }
            compiledArgument = assignedArgument;

            if (!FindSize(compiledArgument.Type, out int argumentSize, out PossibleDiagnostic? argumentSizeError, this))
            { Diagnostics.Add(argumentSizeError.ToError(compiledArgument)); }
            else if (!FindSize(parameter.Type, out int parameterSize, out PossibleDiagnostic? parameterSizeError, this))
            { Diagnostics.Add(parameterSizeError.ToError(parameter)); }
            else if (argumentSize != parameterSize)
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameter.Type}\" ({parameterSize} bytes) passed \"{compiledArgument.Type}\" ({argumentSize} bytes)", argument)); }

            bool typeAllowsTemp = AllowDeallocate(compiledArgument.Type);

            bool calleeAllowsTemp = parameter.Modifiers.Contains(ModifierKeywords.Temp);

            bool callerAllowsTemp = StatementCanBeDeallocated(argument, out bool explicitDeallocate);

            if (callerAllowsTemp)
            {
                if (explicitDeallocate && !calleeAllowsTemp)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
                if (explicitDeallocate && !typeAllowsTemp)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this type", argument)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", argument)); }
            }

            CompiledCleanup? compiledCleanup = null;
            if (calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp)
            {
                CompileCleanup(compiledArgument.Type, argument.Location, out compiledCleanup);
            }

            result.Add(new CompiledArgument()
            {
                Value = compiledArgument,
                Type = compiledArgument.Type,
                Cleanup = compiledCleanup ?? new CompiledCleanup()
                {
                    Location = compiledArgument.Location,
                    TrashType = compiledArgument.Type,
                },
                Location = compiledArgument.Location,
                SaveValue = compiledArgument.SaveValue,
            });
        }

        int remaining = compiledFunction.Parameters.Length - arguments.Count - alreadyPassed;

        ImmutableArray<Scope> savedScopes = Frames.Last.Scopes.ToImmutableArray();
        Frames.Last.Scopes.Clear();
        try
        {
            for (int i = 0; i < remaining; i++)
            {
                CompiledParameter parameter = compiledFunction.Parameters[arguments.Count + i + alreadyPassed];
                Expression? argument = parameter.DefaultValue;
                if (argument is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Can't explain this error", parameter));
                    return false;
                }
                else
                {
                    Diagnostics.Add(Diagnostic.Warning($"WIP", argument));
                }

                if (!CompileExpression(argument, out CompiledExpression? compiledArgument, parameter.Type)) return false;

                if (!CanCastImplicitly(compiledArgument, parameter.Type, out CompiledExpression? assignedArgument, out PossibleDiagnostic? error))
                { Diagnostics.Add(error.ToError(argument)); }
                compiledArgument = assignedArgument;

                if (!FindSize(compiledArgument.Type, out int argumentSize, out PossibleDiagnostic? argumentSizeError, this))
                { Diagnostics.Add(argumentSizeError.ToError(compiledArgument)); }
                else if (!FindSize(parameter.Type, out int parameterSize, out PossibleDiagnostic? parameterSizeError, this))
                { Diagnostics.Add(parameterSizeError.ToError(parameter)); }
                else if (argumentSize != parameterSize)
                { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameter.Type}\" ({parameterSize} bytes) passed \"{compiledArgument.Type}\" ({argumentSize} bytes)", argument)); }

                bool typeAllowsTemp = AllowDeallocate(compiledArgument.Type);

                bool calleeAllowsTemp = parameter.Modifiers.Contains(ModifierKeywords.Temp);

                bool callerAllowsTemp = StatementCanBeDeallocated(ArgumentExpression.Wrap(argument), out bool explicitDeallocate);

                if (callerAllowsTemp)
                {
                    if (explicitDeallocate && !calleeAllowsTemp)
                    { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
                    if (explicitDeallocate && !typeAllowsTemp)
                    { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this type", argument)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", argument)); }
                }

                CompiledCleanup? compiledCleanup = null;
                if (calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp)
                {
                    CompileCleanup(compiledArgument.Type, argument.Location, out compiledCleanup);
                }

                result.Add(new CompiledArgument()
                {
                    Value = compiledArgument,
                    Type = compiledArgument.Type,
                    Cleanup = compiledCleanup ?? new CompiledCleanup()
                    {
                        Location = compiledArgument.Location,
                        TrashType = compiledArgument.Type,
                    },
                    Location = compiledArgument.Location,
                    SaveValue = compiledArgument.SaveValue,
                });
            }
        }
        finally
        {
            Frames.Last.Scopes.AddRange(savedScopes);
        }

        compiledArguments = result.ToImmutable();
        return true;
    }
    bool CompileArguments(IReadOnlyList<Expression> arguments, FunctionType function, [NotNullWhen(true)] out ImmutableArray<CompiledArgument> compiledArguments)
    {
        compiledArguments = ImmutableArray<CompiledArgument>.Empty;

        ImmutableArray<CompiledArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledArgument>(arguments.Count);

        for (int i = 0; i < arguments.Count; i++)
        {
            Expression argument = arguments[i];
            GeneralType parameterType = function.Parameters[i];

            if (!CompileExpression(argument, out CompiledExpression? compiledArgument, parameterType)) return false;

            if (!CanCastImplicitly(compiledArgument, parameterType, out CompiledExpression? assignedArgument, out PossibleDiagnostic? error))
            { Diagnostics.Add(error.ToError(compiledArgument)); }
            compiledArgument = assignedArgument;

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && AllowDeallocate(compiledArgument.Type);

            if (StatementCanBeDeallocated(ArgumentExpression.Wrap(argument), out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", compiledArgument)); }
                canDeallocate = false;
            }

            CompiledCleanup? compiledCleanup = null;
            if (canDeallocate)
            {
                CompileCleanup(compiledArgument.Type, compiledArgument.Location, out compiledCleanup);
            }

            result.Add(new CompiledArgument()
            {
                Value = compiledArgument,
                Type = compiledArgument.Type,
                Cleanup = compiledCleanup ?? new CompiledCleanup()
                {
                    Location = compiledArgument.Location,
                    TrashType = compiledArgument.Type,
                },
                Location = compiledArgument.Location,
                SaveValue = compiledArgument.SaveValue,
            });
        }

        compiledArguments = result.ToImmutable();
        return true;
    }

    bool CompileFunctionCall_External<TFunction>(ImmutableArray<CompiledArgument> arguments, bool saveValue, TFunction compiledFunction, IExternalFunction externalFunction, Location location, [NotNullWhen(true)] out CompiledExpression? compiledStatement)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition, ISimpleReadable
    {
        CheckExternalFunctionDeclaration(this, compiledFunction, externalFunction, Diagnostics);

        compiledStatement = new CompiledExternalFunctionCall()
        {
            Function = externalFunction,
            Arguments = arguments,
            Type = compiledFunction.Type,
            Location = location,
            SaveValue = saveValue,
            Declaration = compiledFunction,
        };
        return true;
    }
    bool CompileFunctionCall<TFunction>(Expression caller, ImmutableArray<ArgumentExpression> arguments, FunctionQueryResult<TFunction> _callee, [NotNullWhen(true)] out CompiledExpression? compiledStatement)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition, IExportable, ISimpleReadable, ILocated, IExternalFunctionDefinition, IHaveInstructionOffset
    {
        (TFunction callee, ImmutableDictionary<string, GeneralType>? typeArguments) = _callee;
        _callee.ReplaceArgumentsIfNeeded(ref arguments);

        if (_callee.Function.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
        {
            Frames.LastRef.IsMsilCompatible = false;
        }
        Frames.Last.CapturesGlobalVariables = null;

        if (callee.Type.Is(out StructType? returnStructType) &&
            returnStructType.Struct == GeneratorStructDefinition?.Struct)
        {
            if (!CompileAllocation(CompiledTypeExpression.CreateAnonymous(new StructType(GetGeneratorState(callee).Struct, caller.File), caller), out CompiledExpression? allocation))
            {
                compiledStatement = null;
                return false;
            }

            CompliableTemplate<CompiledConstructorDefinition> compliableTemplate = AddCompilable(new CompliableTemplate<CompiledConstructorDefinition>(
                GeneratorStructDefinition.Constructor,
                returnStructType.TypeArguments
            ));

            FunctionType calleeFunctionType = new(callee.Type, ((ICompiledFunctionDefinition)callee).Parameters.ToImmutableArray(v => v.Type), false);

            compiledStatement = new CompiledConstructorCall()
            {
                Object = new CompiledStackAllocation()
                {
                    TypeExpression = CompiledTypeExpression.CreateAnonymous(callee.Type, caller.Location),
                    Type = callee.Type,
                    Location = caller.Location,
                    SaveValue = true,
                },
                Function = compliableTemplate.Function,
                Arguments = ImmutableArray.Create(
                    new CompiledArgument()
                    {
                        Value = new CompiledFunctionReference()
                        {
                            Function = callee,
                            Type = calleeFunctionType,
                            Location = caller.Location,
                            SaveValue = true,
                        },
                        Cleanup = new CompiledCleanup()
                        {
                            Location = callee.Location,
                            TrashType = calleeFunctionType,
                        },
                        Type = calleeFunctionType,
                        Location = caller.Location,
                        SaveValue = true,
                    },
                    new CompiledArgument()
                    {
                        Value = new CompiledReinterpretation()
                        {
                            Value = allocation,
                            TypeExpression = CompiledTypeExpression.CreateAnonymous(PointerType.Any, caller.Location),
                            Type = PointerType.Any,
                            Location = caller.Location,
                            SaveValue = true,
                        },
                        Cleanup = new CompiledCleanup()
                        {
                            Location = callee.Location,
                            TrashType = PointerType.Any,
                        },
                        Type = PointerType.Any,
                        Location = caller.Location,
                        SaveValue = true,
                    }
                ),
                Type = callee.Type,
                Location = caller.Location,
                SaveValue = caller.SaveValue,
            };
            GeneratorStructDefinition.Constructor.References.Add(new Reference<ConstructorCallExpression>(
                new ConstructorCallExpression(
                    Token.CreateAnonymous(StatementKeywords.New),
                    new TypeInstanceSimple(
                        Token.CreateAnonymous(GeneratorStructDefinition.Struct.Identifier.Content),
                        caller.Location.File
                    ),
                    ImmutableArray<ArgumentExpression>.Empty,
                    TokenPair.CreateAnonymous("(", ")"),
                    caller.File
                ),
                caller.File,
                false
            ));

            return true;
        }

        compiledStatement = null;
        SetStatementType(caller, callee.Type);

        if (!callee.CanUse(caller.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{callee.ToReadable()}\" could not be called due to its protection level", caller));
            return false;
        }

        int partial = 0;
        for (int i = 0; i < callee.Parameters.Count; i++)
        {
            if (callee.Parameters[i].DefaultValue is null) partial = i + 1;
            else break;
        }

        if (arguments.Length < partial)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{callee.ToReadable()}\": required {callee.ParameterCount} passed {arguments.Length}", caller));
            return false;
        }

        if (!CompileArguments(arguments, callee, out ImmutableArray<CompiledArgument> compiledArguments)) return false;

        if (callee is IInContext<CompiledStruct> calleeInContext &&
            calleeInContext.Context != null &&
            calleeInContext.Context == GeneratorStructDefinition?.Struct)
        {
            CompiledGeneratorState stateStruct = GetGeneratorState(callee);
            List<CompiledArgument> modifiedArguments = new(compiledArguments.Length)
            {
                new()
                {
                    Value = new CompiledFieldAccess()
                    {
                        Field = GeneratorStructDefinition.StateField,
                        Object = compiledArguments[0].Value,
                        Type = GeneratorStructDefinition.StateField.Type,
                        Location = caller.Location,
                        SaveValue = true,
                    },
                    Cleanup = new CompiledCleanup()
                    {
                        Location = caller.Location,
                        TrashType = GeneratorStructDefinition.StateField.Type,
                    },
                    Location = caller.Location,
                    SaveValue = true,
                    Type = GeneratorStructDefinition.StateField.Type,
                }
            };
            modifiedArguments.AddRange(compiledArguments[1..]);
            compiledStatement = new CompiledRuntimeCall()
            {
                Function = new CompiledFieldAccess()
                {
                    Field = GeneratorStructDefinition.FunctionField,
                    Object = compiledArguments[0].Value,
                    // FIXME: dirty ahh
                    Type = GeneralType.InsertTypeParameters(GeneratorStructDefinition.FunctionField.Type, ((StructType)((PointerType)((ICompiledFunctionDefinition)callee).Parameters[0].Type).To).TypeArguments) ?? GeneratorStructDefinition.FunctionField.Type,
                    Location = caller.Location,
                    SaveValue = true,
                },
                Arguments = modifiedArguments.ToImmutableArray(),
                Location = callee.Location,
                SaveValue = caller.SaveValue,
                Type = callee.Type,
            };
            //Debugger.Break();
            return true;
        }

        if (callee.ExternalFunctionName is not null)
        {
            if (ExternalFunctions.TryGet(callee.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                return CompileFunctionCall_External(compiledArguments, caller.SaveValue, callee, externalFunction, caller.Location, out compiledStatement);
            }

            if (callee.Block is null)
            {
                Diagnostics.Add(exception.ToError(caller));
                return false;
            }
        }

        CompileFunction(callee, typeArguments);

        if (Settings.Optimizations.HasFlag(OptimizationSettings.FunctionEvaluating) &&
            TryEvaluate(callee, compiledArguments, new EvaluationContext(), out CompiledValue? returnValue, out ImmutableArray<RuntimeStatement2> runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            SetPredictedValue(caller, returnValue.Value);
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Function evaluated with result \"{returnValue.Value}\"", caller));
            compiledStatement = new CompiledConstantValue()
            {
                Value = returnValue.Value,
                Location = caller.Location,
                SaveValue = caller.SaveValue,
                Type = callee.Type,
            };
            return true;
        }

        if (Settings.Optimizations.HasFlag(OptimizationSettings.FunctionInlining) &&
            callee.IsInlineable)
        {
            CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == callee);
            if (f is not null)
            {
                InlineContext inlineContext = new()
                {
                    Arguments = f.Function.Parameters
                        .Select((value, index) => (value.Identifier.Content, compiledArguments[index]))
                        .ToImmutableDictionary(v => v.Content, v => v.Item2),
                };

                if (InlineFunction(f.Body, inlineContext, out CompiledStatement? inlined1))
                {
                    {
                        ImmutableArray<CompiledArgument> volatileArguments =
                            compiledArguments
                            .Where(v => GetStatementComplexity(v.Value).HasFlag(StatementComplexity.Volatile))
                            .ToImmutableArray();
                        int i = 0;
                        foreach (CompiledArgument? item in volatileArguments)
                        {
                            for (int j = i; j < inlineContext.InlinedArguments.Count; j++)
                            {
                                if (inlineContext.InlinedArguments[j] == item)
                                {
                                    i = j;
                                    goto ok;
                                }
                            }
                            Debugger.Break();
                            Diagnostics.Add(Diagnostic.FailedOptimization($"Can't inline \"{callee.ToReadable()}\" because the behavior might change", item));
                            goto bad;
                        ok:;
                        }
                    }

                    foreach (CompiledArgument argument in compiledArguments)
                    {
                        StatementComplexity complexity = StatementComplexity.None;
                        complexity |= GetStatementComplexity(argument.Value);

                        if (complexity.HasFlag(StatementComplexity.Bruh))
                        {
                            Debugger.Break();
                            Diagnostics.Add(Diagnostic.FailedOptimization($"Can't inline \"{callee.ToReadable()}\" because of this argument", argument));
                            goto bad;
                        }

                        if (complexity.HasFlag(StatementComplexity.Complex))
                        {
                            if (inlineContext.InlinedArguments.Count(v => v == argument) > 1)
                            {
                                //Debugger.Break();
                                Diagnostics.Add(Diagnostic.FailedOptimization($"Can't inline \"{callee.ToReadable()}\" because this expression might be complex", argument));
                                goto bad;
                            }
                        }
                    }

                    ControlFlowUsage controlFlowUsage = inlined1 is CompiledBlock _block2 ? FindControlFlowUsage(_block2.Statements) : FindControlFlowUsage(inlined1);
                    if (!callee.ReturnSomething &&
                        controlFlowUsage == ControlFlowUsage.None)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", caller));
                        CompiledStatement? compiledStatement2 = inlined1;
                        compiledStatement = new CompiledDummyExpression()
                        {
                            Statement = compiledStatement2,
                            Location = compiledStatement2.Location,
                            SaveValue = false,
                            Type = BuiltinType.Void,
                        };
                        return true;
                    }
                    else if (callee.ReturnSomething &&
                             controlFlowUsage == ControlFlowUsage.None &&
                             inlined1 is CompiledExpression statementWithValue)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", caller));

                        if (!CanCastImplicitly(statementWithValue.Type, callee.Type, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(statementWithValue)); }
                        statementWithValue.SaveValue = caller.SaveValue;
                        compiledStatement = statementWithValue;
                        return true;
                    }
                    else
                    {
                        Debugger.Break();
                    }

                bad:;
                    //Debugger.Break();
                }
                else
                {
                    // Debugger.Break();
                    //InlineFunction(f.Body, new InlineContext()
                    //{
                    //    Arguments = f.Function.Parameters
                    //        .Select((value, index) => (value.Identifier.Content, compiledArguments[index]))
                    //        .ToImmutableDictionary(v => v.Content, v => v.Item2),
                    //}, out inlined1);
                    Diagnostics.Add(Diagnostic.Warning($"Failed to inline \"{callee.ToReadable()}\"", caller));
                }
            }
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Arguments = compiledArguments,
            Function = callee,
            Location = caller.Location,
            Type = callee.Type,
            SaveValue = caller.SaveValue,
        };
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
        { Diagnostics.Add(Diagnostic.Critical($"Illegal variable name \"{newVariable.Identifier.Content}\"", newVariable.Identifier, newVariable.File)); }

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

                    if (newVariable.InitialValue is LiteralExpression literalStatement &&
                        literalStatement.Type == LiteralType.String)
                    {
                        if (arrayType.Of.SameAs(BasicType.U16))
                        {
                            int length = literalStatement.Value.Length + 1;

                            if (arrayType.Length.HasValue)
                            {
                                length = arrayType.Length.Value;
                            }
                            else if (arrayType.Length.HasValue)
                            {
                                length = arrayType.Length.Value;
                            }

                            if (length != literalStatement.Value.Length &&
                                length != literalStatement.Value.Length + 1)
                            {
                                Diagnostics.Add(Diagnostic.Error($"String literal's length ({literalStatement.Value.Length}) doesn't match with the type's length ({length})", literalStatement));
                            }

                            type = new ArrayType(arrayType.Of, length);
                        }
                        else if (arrayType.Of.SameAs(BasicType.U8))
                        {
                            int length = literalStatement.Value.Length + 1;

                            if (arrayType.Length.HasValue)
                            {
                                length = arrayType.Length.Value;
                            }
                            else if (arrayType.Length.HasValue)
                            {
                                length = arrayType.Length.Value;
                            }

                            if (length != literalStatement.Value.Length &&
                                length != literalStatement.Value.Length + 1)
                            {
                                Diagnostics.Add(Diagnostic.Error($"String literal's length ({literalStatement.Value.Length}) doesn't match with the type's length ({length})", literalStatement));
                            }

                            type = new ArrayType(arrayType.Of, length);
                        }
                    }
                }

                if (!Frames.Last.IsTemplateInstance) newVariable.CompiledType = type;
            }
        }

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, newVariable.File, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Critical($"Symbol name \"{newVariable.Identifier}\" conflicts with an another symbol name", newVariable.Identifier));
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
                    Diagnostics.Add(Diagnostic.Warning($"External constant \"{newVariable.ExternalConstantName}\" not found", newVariable));
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
                    Diagnostics.Add(Diagnostic.Critical($"Can't cast external constant value {externalConstant.Value} of type \"{externalConstant.Value.Type}\" to {type}", newVariable));
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

        if (initialValue is null && newVariable.InitialValue is not null)
        {
            if (!CompileExpression(newVariable.InitialValue, out initialValue, type)) return false;
            type ??= initialValue.Type;
            if (!CanCastImplicitly(initialValue, type, out CompiledExpression? assignedInitialValue, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(initialValue));
                return false;
            }
            initialValue = assignedInitialValue;
        }

        if (type is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Initial value for variable declaration with implicit type is required", newVariable));
            type = BuiltinType.Any;
        }

        //if (!type.AllGenericsDefined())
        //{
        //    Diagnostics.Add(Diagnostic.Internal($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\" (what edge case is this???)", newVariable.Type, newVariable.File));
        //}

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
        };

        if (isGlobal)
        { compiledStatement = CompiledGlobalVariables.Push(compiledVariable); }
        else
        { compiledStatement = Frames.Last.Scopes.Last.Variables.Push(compiledVariable); }

        if (Frames.Last.CompiledGeneratorContext is not null)
        {
            if (isGlobal)
            {
                throw new UnreachableException();
            }

            if (GeneratorStructDefinition is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"No struct found with an [Builtin(\"generator\")] attribute.", compiledVariable));
                return false;
            }

            CompiledField field = Frames.Last.CompiledGeneratorContext.State.AddVariable(compiledVariable.Identifier, compiledVariable.Type);
            if (!GetParameter("this", out CompiledParameter? thisParameter, out PossibleDiagnostic? parameterNotFoundError))
            {
                Diagnostics.Add(parameterNotFoundError.ToError(compiledVariable));
                return false;
            }

            if (initialValue is null)
            {
                compiledStatement = new CompiledEmptyStatement()
                {
                    Location = compiledVariable.Location,
                };
            }
            else
            {
                compiledStatement = new CompiledSetter()
                {
                    Target = new CompiledFieldAccess()
                    {
                        Field = field,
                        Object = new CompiledParameterAccess()
                        {
                            Parameter = thisParameter,
                            Type = thisParameter.Type,
                            SaveValue = true,
                            Location = compiledVariable.Location,
                        },
                        Type = field.Type,
                        Location = compiledVariable.Location,
                        SaveValue = true,
                    },
                    Value = initialValue,
                    Location = compiledVariable.Location,
                    IsCompoundAssignment = false,
                };
            }
        }

        return true;
    }
    bool CompileStatement(InstructionLabelDeclaration instructionLabel, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GetInstructionLabel(instructionLabel.Identifier.Content, out CompiledLabelDeclaration? compiledInstructionLabelDeclaration, out _))
        {
            Diagnostics.Add(Diagnostic.Internal($"Instruction label \"{instructionLabel.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", instructionLabel.Identifier));
            return false;
        }

        compiledStatement = compiledInstructionLabelDeclaration;
        return true;
    }
    bool CompileStatement(KeywordCallStatement keywordCall, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            CompiledExpression? returnValue = null;

            if (keywordCall.Arguments.Length == 1)
            {
                if (!CompileExpression(keywordCall.Arguments[0], out returnValue, Frames.Last.CurrentReturnType)) return false;
                Frames.Last.CurrentReturnType ??= returnValue.Type;

                if (!CanCastImplicitly(returnValue, Frames.Last.CurrentReturnType, out CompiledExpression? assignedReturnValue, out PossibleDiagnostic? castError))
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

        if (keywordCall.Identifier.Content == StatementKeywords.Yield)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to \"{StatementKeywords.Yield}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            if (Frames.Last.CompiledGeneratorContext is null)
            {
                Diagnostics.Add(Diagnostic.Error($"Yield statements are not allowed in this context", keywordCall));
                return false;
            }

            List<CompiledStatement> statements = new();

            if (keywordCall.Arguments.Length == 1)
            {
                if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? yieldValue, Frames.Last.CompiledGeneratorContext.ResultType)) return false;

                if (!CanCastImplicitly(yieldValue.Type, Frames.Last.CompiledGeneratorContext.ResultType, keywordCall.Arguments[0], out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                }

                statements.Add(new CompiledSetter()
                {
                    Target = new CompiledDereference()
                    {
                        Address = new CompiledParameterAccess()
                        {
                            Parameter = Frames.Last.CompiledGeneratorContext.ResultParameter,
                            Location = keywordCall.Location,
                            SaveValue = true,
                            Type = Frames.Last.CompiledGeneratorContext.ResultParameter.Type,
                        },
                        Location = keywordCall.Location,
                        SaveValue = true,
                        Type = BuiltinType.Any,
                    },
                    Value = yieldValue,
                    Location = keywordCall.Location,
                    IsCompoundAssignment = false,
                });
            }

            CompiledLabelDeclaration l = new()
            {
                Identifier = "fuck",
                Location = keywordCall.Location,
            };

            statements.Add(new CompiledSetter()
            {
                Target = new CompiledFieldAccess()
                {
                    Object = new CompiledParameterAccess()
                    {
                        Parameter = Frames.Last.CompiledGeneratorContext.ThisParameter,
                        Location = keywordCall.Location,
                        SaveValue = true,
                        Type = Frames.Last.CompiledGeneratorContext.ThisParameter.Type,
                    },
                    Field = Frames.Last.CompiledGeneratorContext.State.StateField,
                    Type = Frames.Last.CompiledGeneratorContext.State.StateField.Type,
                    Location = keywordCall.Location,
                    SaveValue = true,
                },
                Value = new CompiledLabelReference()
                {
                    InstructionLabel = l,
                    Location = keywordCall.Location,
                    Type = new FunctionType(BuiltinType.Void, ImmutableArray<GeneralType>.Empty, false),
                    SaveValue = true,
                },
                Location = keywordCall.Location,
                IsCompoundAssignment = false,
            });

            statements.Add(new CompiledReturn()
            {
                Value = new CompiledConstantValue()
                {
                    Value = new CompiledValue((byte)1),
                    Location = keywordCall.Location,
                    Type = BuiltinType.U8,
                    SaveValue = true,
                },
                Location = keywordCall.Location,
            });

            statements.Add(l);

            compiledStatement = new CompiledBlock()
            {
                Statements = statements.ToImmutableArray(),
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Crash)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"{StatementKeywords.Crash}\": required {1} passed {keywordCall.Arguments}", keywordCall));
                return false;
            }

            if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? throwValue)) return false;

            compiledStatement = new CompiledCrash()
            {
                Value = throwValue,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Break)
        {
            compiledStatement = new CompiledBreak()
            {
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Delete)
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

        if (keywordCall.Identifier.Content == StatementKeywords.Goto)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"{StatementKeywords.Goto}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            if (!CompileExpression(keywordCall.Arguments[0], out CompiledExpression? to)) return false;

            if (!CanCastImplicitly(to.Type, CompiledLabelDeclaration.Type, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                return false;
            }

            compiledStatement = new CompiledGoto()
            {
                Value = to,
                Location = keywordCall.Location,
            };
            return true;
        }

        Diagnostics.Add(Diagnostic.Critical($"Unknown keyword \"{keywordCall.Identifier}\"", keywordCall.Identifier));
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
            if (!CompileStatement(forLoop.Block, out body)) return false;
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
    bool CompileStatement(IfContainer @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        LinkedIf links = @if.ToLinks();
        return CompileStatement(links, out compiledStatement);
    }
    bool CompileStatement(LinkedIf @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileExpression(@if.Condition, out CompiledExpression? condition))
        {
            if (@if.NextLink is not null) CompileStatement(@if.NextLink, out _);
            CompileStatement(@if.Body, out _);
            return false;
        }

        if (condition is CompiledConstantValue evaluatedCondition && Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable))
        {
            if (evaluatedCondition.Value)
            {
                if (!StatementWalker.Visit(@if.NextLink, StatementWalkerFilter.FrameOnlyFilter).OfType<InstructionLabelDeclaration>().Any())
                {
                    return CompileStatement(@if.Body, out compiledStatement);
                }
            }
            else
            {
                if (!StatementWalker.Visit(@if, StatementWalkerFilter.FrameOnlyFilter).OfType<InstructionLabelDeclaration>().Any())
                {
                    if (@if.NextLink is not null)
                    {
                        if (!CompileStatement(@if.NextLink, out compiledStatement)) return false;
                        if (compiledStatement is CompiledElse nextElse)
                        {
                            compiledStatement = nextElse.Body;
                        }
                        return true;
                    }
                    else
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

        CompiledStatement? next = null;

        if (@if.NextLink is not null && !CompileStatement(@if.NextLink, out next))
        {
            CompileStatement(@if.Body, out _);
            return false;
        }
        if (!CompileStatement(@if.Body, out CompiledStatement? body)) return false;

        compiledStatement = new CompiledIf()
        {
            Condition = condition,
            Body = body,
            Next = (CompiledBranch?)next,
            Location = @if.Location,
        };
        return true;
    }
    bool CompileStatement(LinkedElse @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
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
    bool CompileStatement(Block block, [NotNullWhen(true)] out CompiledStatement? compiledStatement, bool ignoreScope = false)
    {
        compiledStatement = null;

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(block.Statements.Length);

        if (ignoreScope)
        {
            for (int i = 0; i < block.Statements.Length; i++)
            {
                if (!CompileStatement(block.Statements[i], out CompiledStatement? item)) return false;
                if (item is CompiledEmptyStatement) continue;
                ImmutableArray<CompiledStatement> reduced = ReduceStatements(item, true);
                res.AddRange(reduced);
            }

            compiledStatement = new CompiledBlock()
            {
                Statements = res.ToImmutable(),
                Location = block.Location,
            };
            return true;
        }

        using (Frames.Last.Scopes.PushAuto(CompileScope(block.Statements)))
        {
            for (int i = 0; i < block.Statements.Length; i++)
            {
                if (!CompileStatement(block.Statements[i], out CompiledStatement? item)) return false;
                res.Add(item);
            }
        }

        compiledStatement = new CompiledBlock()
        {
            Statements = res.ToImmutable(),
            Location = block.Location,
        };
        return true;
    }
    bool CompileStatement(Statement statement, [NotNullWhen(true)] out CompiledStatement? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
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
            case IfContainer v: return CompileStatement(v, out compiledStatement);
            case LinkedIf v: return CompileStatement(v, out compiledStatement);
            case LinkedElse v: return CompileStatement(v, out compiledStatement);
            case Block v: return CompileStatement(v, out compiledStatement);
            case InstructionLabelDeclaration v: return CompileStatement(v, out compiledStatement);
            default: throw new NotImplementedException($"Statement {statement.GetType().Name} is not implemented");
        }
    }

    bool CompileExpression(AnyCallExpression anyCall, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (anyCall.Expression is IdentifierExpression _identifier &&
            _identifier.Content == StatementKeywords.Sizeof)
        {
            _identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall));
                return false;
            }

            Expression argument = anyCall.Arguments[0];
            CompiledTypeExpression? paramType;
            if (argument is ArgumentExpression argumentExpression
                && argumentExpression.Modifier is null)
            {
                argument = argumentExpression.Value;
            }
            if (argument is IdentifierExpression identifier)
            {
                if (FindType(identifier.Identifier, identifier.File, out paramType, out PossibleDiagnostic? typeError))
                {
                    //SetStatementType(identifier, paramType);
                    //paramType = _paramType;
                }
                else
                {
                    Diagnostics.Add(typeError.ToError(identifier));
                    return false;
                }
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical($"Type \"{argument}\" not found", argument));
                return false;
            }

            GeneralType resultType = SizeofStatementType;
            if (expectedType is not null &&
                CanCastImplicitly(resultType, expectedType, out _))
            {
                resultType = expectedType;
            }

            SetStatementType(anyCall, resultType);

            compiledStatement = new CompiledSizeof()
            {
                Of = paramType,
                Location = argument.Location,
                Type = resultType,
                SaveValue = anyCall.SaveValue,
            };
            return true;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCallExpression? functionCall))
        {
            if (anyCall.Expression is IdentifierExpression _identifier2)
            { _identifier2.AnalyzedType = TokenAnalyzedType.FunctionName; }

            if (anyCall.Expression is FieldExpression _field)
            { _field.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

            SetStatementReference(anyCall, result.OriginalFunction);
            TrySetStatementReference(anyCall.Expression, result.OriginalFunction);

            result.Function.References.Add(new(anyCall, anyCall.File));
            result.OriginalFunction.References.Add(new(anyCall, anyCall.File));

            if (functionCall.CompiledType is not null)
            { SetStatementType(anyCall, functionCall.CompiledType); }

            return CompileFunctionCall(functionCall, functionCall.MethodArguments, result, out compiledStatement);
        }

        if (!CompileExpression(anyCall.Expression, out CompiledExpression? functionValue))
        {
            if (notFound is not null)
            {
                Diagnostics.Add(notFound.ToError(anyCall.Expression));
            }
            return false;
        }

        {
            List<ArgumentExpression> arguments = new();
            arguments.Add(ArgumentExpression.Wrap(anyCall.Expression));
            arguments.AddRange(anyCall.Arguments);
            if (GetFunction(
                    GetOperators(),
                    "operator",
                    null,

                    FunctionQuery.Create<CompiledOperatorDefinition, string, Token>(
                        "()",
                        arguments.ToImmutableArray(),
                        FunctionArgumentConverter,
                        anyCall.File,
                        null,
                        AddCompilable),

                    out FunctionQueryResult<CompiledOperatorDefinition>? res1,
                    out PossibleDiagnostic? err1
                ) && res1.Success)
            {
                CompiledOperatorDefinition compiledFunction = res1.Function;
                compiledFunction.References.Add(new(anyCall, anyCall.File));
                return CompileFunctionCall(anyCall, arguments.ToImmutableArray(), res1, out compiledStatement);
            }
        }

        if (!functionValue.Type.Is(out FunctionType? functionType))
        {
            if (notFound is not null)
            {
                Diagnostics.Add(notFound.ToError(anyCall.Expression));
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.Expression));
            }

            return false;
        }

        SetStatementType(anyCall, functionType.ReturnType);

        if (anyCall.Arguments.Length != functionType.Parameters.Length)
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.Expression));
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", new Position(anyCall.Arguments.As<IPositioned>().DefaultIfEmpty(anyCall.Brackets)), anyCall.File));
            return false;
        }

        if (!CompileArguments(anyCall.Arguments, functionType, out ImmutableArray<CompiledArgument> compiledArguments)) return false;

        PossibleDiagnostic? argumentError = null;
        if (!Utils.SequenceEquals(compiledArguments, functionType.Parameters, (argument, parameter) =>
        {
            GeneralType argumentType = argument.Type;

            if (argument.Equals(parameter))
            { return true; }

            if (CanCastImplicitly(argumentType, parameter, out argumentError))
            { return true; }

            argumentError = argumentError.TrySetLocation(argument);

            return false;
        }))
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.Expression));
            Diagnostics.Add(Diagnostic.Critical($"Argument types of caller \"...({string.Join(", ", compiledArguments.Select(v => v.Type))})\" doesn't match with callee \"{functionType}\"", anyCall).WithSuberrors(argumentError?.ToError(anyCall)));
            return false;
        }

        Frames.Last.CapturesGlobalVariables = null;
        compiledStatement = new CompiledRuntimeCall()
        {
            Function = functionValue,
            Arguments = compiledArguments,
            Location = anyCall.Location,
            SaveValue = anyCall.SaveValue,
            Type = functionType.ReturnType,
        };
        return true;
    }
    bool CompileExpression(BinaryOperatorCallExpression @operator, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result, out PossibleDiagnostic? notFoundError))
        {
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            SetStatementReference(@operator, result.OriginalFunction);

            if (result.DidReplaceArguments) throw new UnreachableException();

            CompiledOperatorDefinition? operatorDefinition = result.Function;

            if (operatorDefinition.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            SetStatementType(@operator, operatorDefinition.Type);

            if (!CompileFunctionCall(@operator, @operator.Arguments.ToImmutableArray(ArgumentExpression.Wrap), result, out compiledStatement)) return false;

            return true;
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            if (@operator.Operator.Content is
                "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                expectedType = null;
            }

            Expression left = @operator.Left;
            Expression right = @operator.Right;

            if (!CompileExpression(left, out CompiledExpression? compiledLeft, expectedType)) return false;
            if (!CompileExpression(right, out CompiledExpression? compiledRight, expectedType)) return false;

            GeneralType leftType = compiledLeft.Type;
            GeneralType rightType = compiledRight.Type;

            if (!leftType.TryGetNumericType(out NumericType leftNType) ||
                !rightType.TryGetNumericType(out NumericType rightNType))
            {
                Diagnostics.Add(notFoundError.ToError(@operator));
                return false;
            }

            if (!FindBitWidth(leftType, out BitWidth leftBitWidth, out PossibleDiagnostic? e1, this))
            {
                Diagnostics.Add(e1.ToError(left));
                return false;
            }

            if (!FindBitWidth(rightType, out BitWidth rightBitWidth, out PossibleDiagnostic? e2, this))
            {
                Diagnostics.Add(e2.ToError(right));
                return false;
            }

            leftType = BuiltinType.CreateNumeric(leftNType, leftBitWidth);
            rightType = BuiltinType.CreateNumeric(rightNType, rightBitWidth);

            if (!CompileExpression(left, out compiledLeft, leftType)) return false;

            if (leftNType != NumericType.Float &&
                rightNType == NumericType.Float)
            {
                compiledLeft = CompiledCast.Wrap(compiledLeft, BuiltinType.F32);
                leftType = BuiltinType.F32;
                leftNType = NumericType.Float;
            }

            if (!CompileExpression(right, out compiledRight, rightType)) return false;

            if (leftType.SameAs(BasicType.F32) &&
                !rightType.SameAs(BasicType.F32))
            {
                compiledRight = CompiledCast.Wrap(compiledRight, BuiltinType.F32);
                // rightType = BuiltinType.F32;
                rightNType = NumericType.Float;
            }

            if ((leftNType is NumericType.Float) != (rightNType is NumericType.Float))
            {
                Diagnostics.Add(notFoundError.ToError(@operator));
                return false;
            }

            GeneralType resultType;

            {
                if (leftType.Is(out BuiltinType? leftBType) &&
                    rightType.Is(out BuiltinType? rightBType))
                {
                    bool isFloat =
                        leftBType.Type == BasicType.F32 ||
                        rightBType.Type == BasicType.F32;

                    if (!FindBitWidth(leftType, out leftBitWidth, out e1, this))
                    {
                        Diagnostics.Add(e1.ToError(@operator.Left));
                        return false;
                    }

                    if (!FindBitWidth(rightType, out rightBitWidth, out e2, this))
                    {
                        Diagnostics.Add(e2.ToError(@operator.Right));
                        return false;
                    }

                    BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

                    if (!leftBType.TryGetNumericType(out NumericType leftNType1) ||
                        !rightBType.TryGetNumericType(out NumericType rightNType1))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{leftType}\" \"{@operator.Operator.Content}\" \"{rightType}\"", @operator.Operator, @operator.File));
                        return false;
                    }
                    NumericType numericType = leftNType1 > rightNType1 ? leftNType1 : rightNType1;

                    BuiltinType numericResultType = BuiltinType.CreateNumeric(numericType, bitWidth);

                    switch (@operator.Operator.Content)
                    {
                        case BinaryOperatorCallExpression.CompLT:
                        case BinaryOperatorCallExpression.CompGT:
                        case BinaryOperatorCallExpression.CompLEQ:
                        case BinaryOperatorCallExpression.CompGEQ:
                        case BinaryOperatorCallExpression.CompEQ:
                        case BinaryOperatorCallExpression.CompNEQ:
                            if (!GetUsedBy("boolean", out GeneralType? booleanType, out PossibleDiagnostic? internalTypeError))
                            {
                                resultType = BooleanType;
                            }
                            else
                            {
                                resultType = booleanType;
                            }
                            break;

                        case BinaryOperatorCallExpression.LogicalOR:
                        case BinaryOperatorCallExpression.LogicalAND:
                        case BinaryOperatorCallExpression.BitwiseAND:
                        case BinaryOperatorCallExpression.BitwiseOR:
                        case BinaryOperatorCallExpression.BitwiseXOR:
                        case BinaryOperatorCallExpression.BitshiftLeft:
                        case BinaryOperatorCallExpression.BitshiftRight:
                            resultType = numericResultType;
                            break;

                        case BinaryOperatorCallExpression.Addition:
                        case BinaryOperatorCallExpression.Subtraction:
                        case BinaryOperatorCallExpression.Multiplication:
                        case BinaryOperatorCallExpression.Division:
                        case BinaryOperatorCallExpression.Modulo:
                            resultType = isFloat ? BuiltinType.F32 : numericResultType;
                            break;

                        default:
                            return false;
                    }

                    SetStatementType(@operator, resultType);

                    goto OK;
                }
                else
                {
                    bool ok = true;

                    if (!leftType.TryGetNumericType(out leftNType))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Type \"{leftType}\" aint a numeric type", @operator.Left));
                        ok = false;
                    }

                    if (!rightType.TryGetNumericType(out rightNType))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Type \"{rightType}\" aint a numeric type", @operator.Right));
                        ok = false;
                    }

                    if (!FindBitWidth(leftType, out BitWidth leftBitwidth, out PossibleDiagnostic? error, this))
                    {
                        Diagnostics.Add(error.ToError(@operator.Left));
                        ok = false;
                    }

                    if (!FindBitWidth(rightType, out BitWidth rightBitwidth, out error, this))
                    {
                        Diagnostics.Add(error.ToError(@operator.Right));
                        ok = false;
                    }

                    if (!ok) { return false; }

                    CompiledValue leftValue = GetInitialValue(leftNType, leftBitwidth);
                    CompiledValue rightValue = GetInitialValue(rightNType, rightBitwidth);

                    if (!TryComputeSimple(@operator.Operator.Content, leftValue, rightValue, out CompiledValue predictedValue, out PossibleDiagnostic? evaluateError))
                    {
                        Diagnostics.Add(evaluateError.ToError(@operator));
                        return false;
                    }

                    if (!CompileType(predictedValue.Type, out resultType!, out PossibleDiagnostic? typeError))
                    {
                        Diagnostics.Add(typeError.ToError(@operator));
                        return false;
                    }
                }
            }

        OK:

            if (expectedType is not null &&
                CanCastImplicitly(resultType, expectedType, out _))
            {
                resultType = expectedType;
            }

            SetStatementType(@operator, resultType);

            compiledStatement = new CompiledBinaryOperatorCall()
            {
                Operator = @operator.Operator.Content,
                Left = compiledLeft,
                Right = compiledRight,
                Type = resultType,
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
            };

            if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                TryCompute(compiledStatement, out CompiledValue evaluated) &&
                evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
            {
                compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                SetPredictedValue(@operator, casted);
            }

            return true;
        }
        else if (@operator.Operator.Content == "=")
        {
            throw new NotImplementedException();
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
            return false;
        }
    }
    bool CompileExpression(UnaryOperatorCallExpression @operator, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result, out PossibleDiagnostic? operatorNotFoundError))
        {
            SetStatementReference(@operator, result.OriginalFunction);
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

            if (result.DidReplaceArguments) throw new UnreachableException();

            CompiledOperatorDefinition? operatorDefinition = result.Function;

            SetStatementType(@operator, operatorDefinition.Type);

            if (operatorDefinition.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            if (!operatorDefinition.CanUse(@operator.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Operator \"{operatorDefinition.ToReadable()}\" cannot be called due to its protection level", @operator.Operator, @operator.File));
                return false;
            }

            if (UnaryOperatorCallExpression.ParameterCount != operatorDefinition.ParameterCount)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to operator \"{operatorDefinition.ToReadable()}\": required {operatorDefinition.ParameterCount} passed {UnaryOperatorCallExpression.ParameterCount}", @operator));
                return false;
            }

            if (!CompileArguments(@operator.Arguments.ToImmutableArray(ArgumentExpression.Wrap), operatorDefinition, out ImmutableArray<CompiledArgument> compiledArguments)) return false;

            if (operatorDefinition.ExternalFunctionName is not null)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                {
                    Diagnostics.Add(exception.ToError(@operator.Operator, @operator.File));
                    return false;
                }

                result.Function.References.Add(new Reference<Expression>(@operator, @operator.File));
                result.OriginalFunction.References.Add(new Reference<Expression>(@operator, @operator.File));
                return CompileFunctionCall_External(compiledArguments, @operator.SaveValue, operatorDefinition, externalFunction, @operator.Location, out compiledStatement);
            }

            compiledStatement = new CompiledFunctionCall()
            {
                Function = operatorDefinition,
                Arguments = compiledArguments,
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
                Type = operatorDefinition.Type,
            };
            Frames.Last.CapturesGlobalVariables = null;

            if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                TryCompute(compiledStatement, out CompiledValue evaluated) &&
                evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
            {
                compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                SetPredictedValue(@operator, casted);
                operatorDefinition.References.Add(new Reference<Expression>(@operator, @operator.File, true));
            }
            else
            {
                operatorDefinition.References.Add(new Reference<Expression>(@operator, @operator.File));
            }

            return true;
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            switch (@operator.Operator.Content)
            {
                case UnaryOperatorCallExpression.LogicalNOT:
                {
                    if (!CompileExpression(@operator.Expression, out CompiledExpression? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = BuiltinType.U8,
                    };

                    if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                        TryCompute(compiledStatement, out CompiledValue evaluated) &&
                        evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
                    {
                        compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        SetPredictedValue(@operator, casted);
                    }

                    return true;
                }
                case UnaryOperatorCallExpression.BinaryNOT:
                {
                    if (!CompileExpression(@operator.Expression, out CompiledExpression? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = left.Type,
                    };

                    if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                        TryCompute(compiledStatement, out CompiledValue evaluated) &&
                        evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
                    {
                        compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        SetPredictedValue(@operator, casted);
                    }

                    return true;
                }
                case UnaryOperatorCallExpression.UnaryMinus:
                {
                    if (!CompileExpression(@operator.Expression, out CompiledExpression? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = left.Type,
                    };

                    if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                        TryCompute(compiledStatement, out CompiledValue evaluated) &&
                        evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
                    {
                        compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        SetPredictedValue(@operator, casted);
                    }

                    return true;
                }
                case UnaryOperatorCallExpression.UnaryPlus:
                {
                    if (!CompileExpression(@operator.Expression, out CompiledExpression? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = left.Type,
                    };

                    if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                        TryCompute(compiledStatement, out CompiledValue evaluated) &&
                        evaluated.TryCast(compiledStatement.Type, out CompiledValue casted))
                    {
                        compiledStatement = CompiledConstantValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        SetPredictedValue(@operator, casted);
                    }

                    return true;
                }
                default:
                {
                    Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
                    return false;
                }
            }
        }
        else
        {
            Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File).WithSuberrors(operatorNotFoundError.ToError(@operator)));
            return false;
        }
    }
    bool CompileExpression(LambdaExpression lambdaStatement, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        FunctionType? functionType = expectedType as FunctionType;

        ImmutableArray<CompiledParameter>.Builder compiledParameters = ImmutableArray.CreateBuilder<CompiledParameter>();

        for (int i = 0; i < lambdaStatement.Parameters.Count; i++)
        {
            if (!CompileType(lambdaStatement.Parameters[i].Type, out GeneralType? parameterType, Diagnostics))
            {
                return false;
            }

            compiledParameters.Add(new CompiledParameter(parameterType, lambdaStatement.Parameters[i]));
        }

        ImmutableArray<CompiledLabelDeclaration>.Builder localInstructionLabels = ImmutableArray.CreateBuilder<CompiledLabelDeclaration>();

        foreach (Statement item in StatementWalker.Visit(lambdaStatement.Body, StatementWalkerFilter.FrameOnlyFilter))
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
            TypeArguments = Frames.Last.TypeArguments,
            IsTemplateInstance = Frames.Last.IsTemplateInstance,
            IsTemplate = Frames.Last.IsTemplate,
            TypeParameters = Frames.Last.TypeParameters,
            CompiledParameters = compiledParameters.ToImmutable(),
            InstructionLabels = localInstructionLabels.ToImmutable(),
            Scopes = new(),
            CurrentReturnType = functionType?.ReturnType,
            CompiledGeneratorContext = null,
            IsTopLevel = false,
        }))
        {
            CompiledStatement? _body;
            using (Frames.Last.Scopes.PushAuto(new Scope(ImmutableArray<CompiledVariableConstant>.Empty)))
            {
                if (!CompileStatement(lambdaStatement.Body, out _body)) return false;
            }

            CompiledBlock block;

            if (_body is CompiledBlock _block)
            {
                block = _block;
            }
            else if (_body is CompiledExpression _compiledStatementWithValue)
            {
                if (frame.Value.CurrentReturnType is null || frame.Value.CurrentReturnType.SameAs(BuiltinType.Void))
                {
                    _compiledStatementWithValue.SaveValue = false;
                    block = new CompiledBlock()
                    {
                        Location = _body.Location,
                        Statements = ImmutableArray.Create<CompiledStatement>(_compiledStatementWithValue),
                    };
                }
                else
                {
                    _compiledStatementWithValue.SaveValue = true;
                    block = new CompiledBlock()
                    {
                        Location = _body.Location,
                        Statements = ImmutableArray.Create<CompiledStatement>(new CompiledReturn()
                        {
                            Location = _body.Location,
                            Value = _compiledStatementWithValue,
                        }),
                    };
                    if (!frame.Value.CurrentReturnType.SameAs(_compiledStatementWithValue.Type))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Lambda expression value's type ({_compiledStatementWithValue.Type}) doesn't match the return type {frame.Value.CurrentReturnType}", _compiledStatementWithValue));
                    }
                }
            }
            else
            {
                block = new CompiledBlock()
                {
                    Location = _body.Location,
                    Statements = ImmutableArray.Create(_body),
                };
            }

            if (!frame.Value.IsMsilCompatible)
            {
                Frames.LastRef.IsMsilCompatible = false;
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

            functionType = new FunctionType(frame.Value.CurrentReturnType ?? BuiltinType.Void, functionType?.Parameters ?? frame.Value.CompiledParameters.ToImmutableArray(v => v.Type), !closure.IsEmpty);

            CompiledExpression? allocator = null;
            if (!closure.IsEmpty)
            {
                compiledParameters.Insert(0, new CompiledParameter(PointerType.Any, new ParameterDefinition(
                    ImmutableArray<Token>.Empty,
                    null!,
                    Token.CreateAnonymous("closure"),
                    null
                )));

                int closureSize = 0;
                foreach (CapturedLocal? item in closure)
                {
                    if (!FindSize((item.Variable?.Type ?? item.Parameter?.Type)!, out int itemSize, out PossibleDiagnostic? sizeError, this))
                    {
                        Diagnostics.Add(sizeError.ToError(((ILocated?)item.Variable ?? (ILocated?)item.Parameter)!));
                    }
                    closureSize += itemSize;
                }
                closureSize += PointerSize;
                if (!CompileAllocation(closureSize, lambdaStatement.Location, out allocator)) return false;
                if (!Frames.Last.IsTemplateInstance) lambdaStatement.AllocatorReference = allocator is CompiledFunctionCall cfc ? cfc.Function : null;
            }

            if (!frame.Value.CapturesGlobalVariables.HasValue) Frames.Last.CapturesGlobalVariables = null;
            else if (frame.Value.CapturesGlobalVariables.Value) Frames.Last.CapturesGlobalVariables = true;

            compiledStatement = new CompiledLambda(
                functionType.ReturnType,
                compiledParameters.ToImmutable(),
                block,
                lambdaStatement.Parameters,
                closure,
                lambdaStatement.File
            )
            {
                InstructionOffset = InvalidFunctionAddress,
                Location = lambdaStatement.Location,
                SaveValue = lambdaStatement.SaveValue,
                Type = functionType,
                Allocator = allocator,
            };

            return true;
        }
    }
    bool CompileExpression(LiteralExpression literal, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
            {
                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if (literal.GetInt() is >= byte.MinValue and <= byte.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((byte)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((sbyte)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((ushort)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((short)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        SetStatementType(literal, expectedType);
                        compiledStatement = new CompiledConstantValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue(literal.GetInt().U32()),
                        };
                        return true;
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        SetStatementType(literal, expectedType);
                        compiledStatement = new CompiledConstantValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((int)literal.GetInt()),
                        };
                        return true;
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        SetStatementType(literal, expectedType);
                        compiledStatement = new CompiledConstantValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((float)literal.GetInt()),
                        };
                        return true;
                    }
                }

                if (!GetLiteralType(literal.Type, out GeneralType? literalType, out _))
                { literalType = BuiltinType.I32; }

                SetStatementType(literal, literalType);
                compiledStatement = new CompiledConstantValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = literalType,
                    Value = new CompiledValue((int)literal.GetInt()),
                };
                return true;
            }
            case LiteralType.Float:
            {
                if (!GetLiteralType(literal.Type, out GeneralType? literalType, out _))
                { literalType = BuiltinType.F32; }

                SetStatementType(literal, literalType);
                compiledStatement = new CompiledConstantValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = literalType,
                    Value = new CompiledValue((float)literal.GetFloat()),
                };
                return true;
            }
            case LiteralType.String:
            {
                if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType) &&
                    pointerType.To.Is(out ArrayType? arrayType) &&
                    arrayType.Of.SameAs(BasicType.U8))
                {
                    SetStatementType(literal, expectedType);

                    compiledStatement = null;
                    if (!FindSize(BuiltinType.U8, out int charSize, out PossibleDiagnostic? sizeError, this))
                    {
                        Diagnostics.Add(sizeError.ToError(literal));
                    }
                    if (!CompileAllocation((1 + literal.Value.Length) * charSize, literal.Location, out CompiledExpression? allocator)) return false;

                    compiledStatement = new CompiledString()
                    {
                        Value = literal.Value,
                        IsASCII = true,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        Allocator = allocator,
                    };
                    return true;
                }
                else if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType2) &&
                    pointerType2.To.Is(out ArrayType? arrayType2) &&
                    arrayType2.Of.SameAs(BasicType.U16))
                {
                    SetStatementType(literal, expectedType);

                    compiledStatement = null;
                    if (!FindSize(BuiltinType.Char, out int charSize, out PossibleDiagnostic? sizeError, this))
                    {
                        Diagnostics.Add(sizeError.ToError(literal));
                    }
                    if (!CompileAllocation((1 + literal.Value.Length) * charSize, literal.Location, out CompiledExpression? allocator)) return false;

                    compiledStatement = new CompiledString()
                    {
                        Value = literal.Value,
                        IsASCII = false,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        Allocator = allocator,
                    };
                    return true;
                }
                else if (expectedType is not null &&
                    expectedType.Is(out ArrayType? arrayType3) &&
                    arrayType3.Of.SameAs(BasicType.U8))
                {
                    SetStatementType(literal, expectedType);

                    compiledStatement = new CompiledStackString()
                    {
                        Value = literal.Value,
                        IsASCII = true,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        IsNullTerminated = arrayType3.Length.HasValue && arrayType3.Length.Value > literal.Value.Length,
                    };
                    return true;
                }
                else if (expectedType is not null &&
                    expectedType.Is(out ArrayType? arrayType4) &&
                    arrayType4.Of.SameAs(BasicType.U16))
                {
                    SetStatementType(literal, expectedType);

                    compiledStatement = new CompiledStackString()
                    {
                        Value = literal.Value,
                        IsASCII = false,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        IsNullTerminated = arrayType4.Length.HasValue && arrayType4.Length.Value > literal.Value.Length,
                    };
                    return true;
                }
                else
                {
                    compiledStatement = null;
                    if (!FindSize(BuiltinType.Char, out int charSize, out PossibleDiagnostic? sizeError, this))
                    {
                        Diagnostics.Add(sizeError.ToError(literal));
                    }
                    if (!CompileAllocation((1 + literal.Value.Length) * charSize, literal.Location, out CompiledExpression? allocator)) return false;

                    if (!GetLiteralType(literal.Type, out GeneralType? stringType, out _))
                    {
                        if (!GetLiteralType(literal.Type, out GeneralType? charType, out _))
                        { charType = BuiltinType.Char; }

                        stringType = new PointerType(new ArrayType(charType, literal.Value.Length + 1));
                    }

                    SetStatementType(literal, stringType);

                    compiledStatement = new CompiledString()
                    {
                        Value = literal.Value,
                        IsASCII = false,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = stringType,
                        Allocator = allocator,
                    };
                    return true;
                }
            }
            case LiteralType.Char:
            {
                if (literal.Value.Length != 1)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Literal char contains {literal.Value.Length} characters but only 1 allowed", literal, literal.File));
                    if (literal.Value.Length == 0) break;
                }

                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((byte)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((sbyte)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        SetStatementType(literal, expectedType);
                        compiledStatement = new CompiledConstantValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((char)literal.Value[0]),
                        };
                        return true;
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((short)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.Value[0] >= uint.MinValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((uint)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        if (literal.Value[0] >= int.MinValue)
                        {
                            SetStatementType(literal, expectedType);
                            compiledStatement = new CompiledConstantValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = expectedType,
                                Value = new CompiledValue((int)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        SetStatementType(literal, expectedType);
                        compiledStatement = new CompiledConstantValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((float)literal.Value[0]),
                        };
                        return true;
                    }
                }

                if (!GetLiteralType(literal.Type, out GeneralType? literalType, out _))
                { literalType = BuiltinType.Char; }

                SetStatementType(literal, literalType);
                compiledStatement = new CompiledConstantValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = literalType,
                    Value = new CompiledValue((char)literal.Value[0]),
                };
                return true;
            }
            default: throw new UnreachableException();
        }

        compiledStatement = null;
        return false;
    }
    bool CompileExpression(IdentifierExpression variable, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        compiledStatement = null;

        if (variable.Content.StartsWith('#'))
        {
            compiledStatement = new CompiledConstantValue()
            {
                Value = new CompiledValue(PreprocessorVariables.Contains(variable.Content[1..])),
                Type = BooleanType,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (variable.Content.StartsWith('@'))
        {
            compiledStatement = new CompiledConstantValue()
            {
                Value = Settings.PreprocessorVariables.Contains(variable.Content[1..]),
                Type = BooleanType,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (RegisterKeywords.TryGetValue(variable.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            compiledStatement = new CompiledRegisterAccess()
            {
                Register = registerKeyword.Register,
                Type = registerKeyword.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (!Settings.ExpressionVariables.IsDefault)
        {
            foreach (ExpressionVariable item in Settings.ExpressionVariables)
            {
                if (item.Name != variable.Content) continue;
                variable.AnalyzedType = TokenAnalyzedType.VariableName;
                SetStatementReference(variable, item);
                SetStatementType(variable, item.Type);

                compiledStatement = new CompiledExpressionVariableAccess()
                {
                    Variable = item,
                    Location = variable.Location,
                    SaveValue = variable.SaveValue,
                    Type = item.Type,
                };
                return true;
            }
        }

        if (GetConstant(variable.Content, variable.File, out CompiledVariableConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            SetStatementType(variable, constant.Type);
            SetPredictedValue(variable, constant.Value);
            SetStatementReference(variable, constant);
            variable.AnalyzedType = TokenAnalyzedType.ConstantName;

            CompiledValue value = constant.Value;
            GeneralType type = constant.Type;

            if (expectedType is not null &&
                constant.Value.TryCast(expectedType, out CompiledValue castedValue))
            {
                value = castedValue;
                type = expectedType;
            }

            if (constant.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            compiledStatement = new CompiledConstantValue()
            {
                Value = value,
                Type = type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetParameter(variable.Content, out CompiledParameter? param, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (variable.Content != StatementKeywords.This)
            { variable.AnalyzedType = TokenAnalyzedType.ParameterName; }
            SetStatementReference(variable, param);
            SetStatementType(variable, param.Type);

            compiledStatement = new CompiledParameterAccess()
            {
                Parameter = param,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
                Type = param.Type,
            };
            return true;
        }

        if (GetVariable(variable.Content, out CompiledVariableDefinition? val, out PossibleDiagnostic? variableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementReference(variable, val);
            SetStatementType(variable, val.Type);

            if (val.IsGlobal)
            { Diagnostics.Add(Diagnostic.Internal($"Trying to get local variable \"{val.Identifier}\" but it was compiled as a global variable.", variable)); }

            if (Frames.Last.CompiledGeneratorContext is not null)
            {
                //Debugger.Break();
                if (GeneratorStructDefinition is null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"No struct found with an [Builtin(\"generator\")] attribute.", variable));
                    return false;
                }

                CompiledField field = Frames.Last.CompiledGeneratorContext.State.AddVariable(val.Identifier, val.Type);
                if (!GetParameter("this", out CompiledParameter? thisParameter, out parameterNotFoundError))
                {
                    Diagnostics.Add(parameterNotFoundError.ToError(variable));
                    return false;
                }

                compiledStatement = new CompiledFieldAccess()
                {
                    Field = field,
                    Object = new CompiledParameterAccess()
                    {
                        Parameter = thisParameter,
                        Type = thisParameter.Type,
                        SaveValue = true,
                        Location = variable.Location,
                    },
                    Type = field.Type,
                    SaveValue = variable.SaveValue,
                    Location = variable.Location,
                };
                return true;
            }

            compiledStatement = new CompiledVariableAccess()
            {
                Variable = val,
                Type = val.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariableDefinition? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementReference(variable, globalVariable);
            SetStatementType(variable, globalVariable.Type);
            Frames.Last.CapturesGlobalVariables = true;

            if (!globalVariable.IsGlobal)
            { Diagnostics.Add(Diagnostic.Internal($"Trying to get global variable \"{globalVariable.Identifier}\" but it was compiled as a local variable.", variable)); }

            compiledStatement = new CompiledVariableAccess()
            {
                Variable = globalVariable,
                Type = globalVariable.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetFunction(variable.Content, expectedType, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? functionNotFoundError, AddCompilable))
        {
            CompiledFunctionDefinition? compiledFunction = result.Function;

            if (result.Function.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            FunctionType functionType = new(compiledFunction.Type, compiledFunction.Parameters.ToImmutableArray(v => v.Type), false);

            compiledFunction.References.AddReference(variable, variable.File);
            variable.AnalyzedType = TokenAnalyzedType.FunctionName;
            SetStatementReference(variable, compiledFunction);
            SetStatementType(variable, functionType);

            compiledStatement = new CompiledFunctionReference()
            {
                Function = compiledFunction,
                Type = functionType,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetInstructionLabel(variable.Content, out CompiledLabelDeclaration? instructionLabel, out PossibleDiagnostic? instructionLabelError))
        {
            SetStatementReference(variable, instructionLabel);
            variable.AnalyzedType = TokenAnalyzedType.InstructionLabel;
            SetStatementType(variable, CompiledLabelDeclaration.Type);

            compiledStatement = new CompiledLabelReference()
            {
                InstructionLabel = instructionLabel,
                Type = CompiledLabelDeclaration.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        for (int i = Frames.Count - 2; i >= 0; i--)
        {
            if (GetParameter(variable.Content, Frames[i], out CompiledParameter? outerParameter, out _))
            {
                variable.AnalyzedType = TokenAnalyzedType.VariableName;
                SetStatementReference(variable, outerParameter);
                SetStatementType(variable, outerParameter.Type);

                compiledStatement = new CompiledParameterAccess()
                {
                    Parameter = outerParameter,
                    Type = outerParameter.Type,
                    Location = variable.Location,
                    SaveValue = variable.SaveValue,
                };
                for (int j = i + 1; j < Frames.Count; j++)
                {
                    Frames[j].CapturedParameters.Add(outerParameter);
                }
                return true;
            }

            if (GetVariable(variable.Content, Frames[i], out CompiledVariableDefinition? outerLocal, out _))
            {
                variable.AnalyzedType = TokenAnalyzedType.VariableName;
                SetStatementReference(variable, outerLocal);
                SetStatementType(variable, outerLocal.Type);

                if (outerLocal.IsGlobal)
                { Diagnostics.Add(Diagnostic.Internal($"Trying to get local variable \"{outerLocal.Identifier}\" but it was compiled as a global variable.", variable)); }

                if (Frames.Last.CompiledGeneratorContext is not null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"aaaaaaa", variable));
                    return false;
                }

                compiledStatement = new CompiledVariableAccess()
                {
                    Variable = outerLocal,
                    Type = outerLocal.Type,
                    Location = variable.Location,
                    SaveValue = variable.SaveValue,
                };
                for (int j = i + 1; j < Frames.Count; j++)
                {
                    Frames[j].CapturedVariables.Add(outerLocal);
                }
                return true;
            }
        }

        Diagnostics.Add(Diagnostic.Critical($"Symbol \"{variable.Content}\" not found", variable)
            .WithSuberrors(
                constantNotFoundError.ToError(variable),
                parameterNotFoundError.ToError(variable),
                variableNotFoundError.ToError(variable),
                globalVariableNotFoundError.ToError(variable),
                functionNotFoundError.ToError(variable),
                instructionLabelError.ToError(variable)
            ));
        return false;
    }
    bool CompileExpression(GetReferenceExpression addressGetter, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (!CompileExpression(addressGetter.Expression, out CompiledExpression? of)) return false;

        compiledStatement = new CompiledGetReference()
        {
            Of = of,
            Type = new PointerType(of.Type),
            Location = addressGetter.Location,
            SaveValue = addressGetter.SaveValue,
        };
        return true;
    }
    bool CompileExpression(DereferenceExpression pointer, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (!CompileExpression(pointer.Expression, out CompiledExpression? to)) return false;

        GeneralType addressType = to.Type;
        if (!addressType.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", pointer.Expression));
            return false;
        }

        compiledStatement = new CompiledDereference()
        {
            Address = to,
            Type = pointerType.To,
            Location = pointer.Location,
            SaveValue = pointer.SaveValue,
        };
        return true;
    }
    bool CompileExpression(NewInstanceExpression newInstance, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        if (!CompileStatement(newInstance.Type, out CompiledTypeExpression? instanceType, Diagnostics))
        {
            return false;
        }

        //SetTypeType(newInstance.Type, instanceType);
        //SetStatementType(newInstance, instanceType);

        switch (instanceType)
        {
            case CompiledPointerTypeExpression pointerType:
            {
                if (!CompileAllocation(pointerType.To, out compiledStatement))
                { return false; }

                SetStatementReference(newInstance, compiledStatement is CompiledFunctionCall cfc ? cfc.Function : null);

                if (!CompileType(instanceType, out GeneralType? compiledType, out PossibleDiagnostic? typeError, true))
                {
                    Diagnostics.Add(typeError.ToError(instanceType));
                    return false;
                }

                compiledStatement = new CompiledReinterpretation()
                {
                    Value = compiledStatement,
                    TypeExpression = instanceType,
                    Type = compiledType,
                    Location = compiledStatement.Location,
                    SaveValue = compiledStatement.SaveValue,
                };
                return true;
            }

            case CompiledStructTypeExpression structType:
            {
                if (!CompileType(structType, out GeneralType? compiledType, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(structType));
                    return false;
                }
                structType.Struct.References.AddReference(newInstance.Type, newInstance.File);
                compiledStatement = new CompiledStackAllocation()
                {
                    Type = compiledType,
                    TypeExpression = structType,
                    Location = newInstance.Location,
                    SaveValue = newInstance.SaveValue,
                };
                return true;
            }

            case CompiledArrayTypeExpression arrayType:
            {
                if (!CompileType(arrayType, out GeneralType? compiledType, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(arrayType));
                    return false;
                }
                compiledStatement = new CompiledStackAllocation()
                {
                    Type = compiledType,
                    TypeExpression = arrayType,
                    Location = newInstance.Location,
                    SaveValue = newInstance.SaveValue,
                };
                return true;
            }

            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown type \"{instanceType}\"", newInstance.Type, newInstance.File));
                return false;
            }
        }
    }
    bool CompileExpression(ConstructorCallExpression constructorCall, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        if (!CompileType(constructorCall.Type, out GeneralType? instanceType, Diagnostics))
        {
            return false;
        }

        if (!FindStatementTypes(constructorCall.Arguments, out ImmutableArray<GeneralType> parameters, Diagnostics))
        {
            return false;
        }

        if (!GetConstructor(instanceType, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructorDefinition>? result, out PossibleDiagnostic? notFound, v => AddCompilable(v)))
        {
            Diagnostics.Add(notFound.ToError(constructorCall.Type, constructorCall.File));
            return false;
        }

        if (result.Function.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
        {
            Frames.LastRef.IsMsilCompatible = false;
        }

        result.Function.References.AddReference(constructorCall);
        result.OriginalFunction.References.AddReference(constructorCall);
        SetStatementReference(constructorCall, result.OriginalFunction);

        CompiledConstructorDefinition? compiledFunction = result.Function;
        SetStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(constructorCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Constructor \"{compiledFunction.ToReadable()}\" could not be called due to its protection level", constructorCall.Type, constructorCall.File));
            return false;
        }

        ImmutableArray<ArgumentExpression> arguments = constructorCall.Arguments;
        result.ReplaceArgumentsIfNeeded(ref arguments);

        if (!CompileExpression(constructorCall.ToInstantiation(), out CompiledExpression? _object)) return false;
        if (!CompileArguments(arguments, compiledFunction, out ImmutableArray<CompiledArgument> compiledArguments, 1)) return false;

        Frames.Last.CapturesGlobalVariables = null;
        compiledStatement = new CompiledConstructorCall()
        {
            Arguments = compiledArguments,
            Function = compiledFunction,
            Object = _object,
            Location = constructorCall.Location,
            SaveValue = constructorCall.SaveValue,
            Type = instanceType,
        };
        return true;
    }
    bool CompileExpression(FieldExpression field, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        if (!CompileExpression(field.Object, out CompiledExpression? prev)) return false;

        if (prev.Type.Is(out ArrayType? arrayType) && field.Identifier.Content == "Length")
        {
            if (!arrayType.Length.HasValue)
            {
                Diagnostics.Add(Diagnostic.Critical("I will eventually implement this", field));
                return false;
            }

            SetStatementType(field, ArrayLengthType);
            SetPredictedValue(field, arrayType.Length.Value);

            compiledStatement = new CompiledConstantValue()
            {
                Value = arrayType.Length.Value,
                Type = ArrayLengthType,
                Location = field.Location,
                SaveValue = field.SaveValue,
            };
            return true;
        }

        if (prev.Type.Is(out PointerType? pointerType2))
        {
            GeneralType prevType = pointerType2.To;

            while (prevType.Is(out pointerType2))
            {
                prevType = pointerType2.To;
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", field.Object));
                return false;
            }

            if (!structPointerType.GetField(field.Identifier.Content, out CompiledField? fieldDefinition, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(field.Identifier, field.File));
                return false;
            }

            SetStatementType(field, fieldDefinition.Type);
            SetStatementReference(field, fieldDefinition);

            compiledStatement = new CompiledFieldAccess()
            {
                Object = prev,
                Field = fieldDefinition,
                Location = field.Location,
                SaveValue = field.SaveValue,
                Type = GeneralType.InsertTypeParameters(fieldDefinition.Type, structPointerType.TypeArguments) ?? fieldDefinition.Type,
            };
            return true;
        }

        if (!prev.Type.Is(out StructType? structType)) throw new NotImplementedException();

        if (!structType.GetField(field.Identifier.Content, out CompiledField? compiledField, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field.Identifier, field.File));
            return false;
        }

        SetStatementType(field, compiledField.Type);
        SetStatementReference(field, compiledField);

        compiledStatement = new CompiledFieldAccess()
        {
            Field = compiledField,
            Object = prev,
            Location = field.Location,
            SaveValue = field.SaveValue,
            Type = GeneralType.InsertTypeParameters(compiledField.Type, structType.TypeArguments) ?? compiledField.Type,
        };
        return true;
    }
    bool CompileExpression(IndexCallExpression index, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (!CompileExpression(index.Object, out CompiledExpression? baseStatement)) return false;
        if (!CompileExpression(index.Index, out CompiledExpression? indexStatement)) return false;

        if (GetIndexGetter(baseStatement.Type, indexStatement.Type, index.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(index, index.File));
            indexer.OriginalFunction.References.Add(new(index, index.File));
            SetStatementReference(index, indexer.OriginalFunction);

            return CompileFunctionCall(index, ImmutableArray.Create(ArgumentExpression.Wrap(index.Object), index.Index), indexer, out compiledStatement);
        }

        if (baseStatement.Type.Is(out ArrayType? arrayType))
        {
            if (TryCompute(indexStatement, out CompiledValue computedIndexData))
            {
                SetPredictedValue(index.Index, computedIndexData);

                if (computedIndexData < 0 || (arrayType.Length.HasValue && computedIndexData >= arrayType.Length.Value))
                { Diagnostics.Add(Diagnostic.Warning($"Index out of range", index.Index)); }
            }

            compiledStatement = new CompiledElementAccess()
            {
                Base = baseStatement,
                Index = indexStatement,
                Type = arrayType.Of,
                Location = index.Location,
                SaveValue = index.SaveValue,
            };
            return true;
        }

        if (baseStatement.Type.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
        {
            compiledStatement = new CompiledElementAccess()
            {
                Base = baseStatement,
                Index = indexStatement,
                Type = arrayType.Of,
                Location = index.Location,
                SaveValue = index.SaveValue,
            };
            return true;
        }

        Diagnostics.Add(Diagnostic.Critical($"Index getter for type \"{baseStatement.Type}\" not found", index));
        return false;
    }
    bool CompileExpression(ArgumentExpression modifiedStatement, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        if (modifiedStatement.Modifier is null) return CompileExpression(modifiedStatement.Value, out compiledStatement, expectedType);

        return modifiedStatement.Modifier.Content switch
        {
            ModifierKeywords.Ref => throw new NotImplementedException(),
            ModifierKeywords.Temp => CompileExpression(modifiedStatement.Value, out compiledStatement, expectedType),
            _ => throw new NotImplementedException(),
        };
    }
    bool CompileExpression(ListExpression listValue, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        GeneralType? itemType = (expectedType as ArrayType)?.Of;

        ImmutableArray<CompiledExpression>.Builder result = ImmutableArray.CreateBuilder<CompiledExpression>(listValue.Values.Length);
        for (int i = 0; i < listValue.Values.Length; i++)
        {
            if (!CompileExpression(listValue.Values[i], out CompiledExpression? item, itemType)) return false;

            if (itemType is null)
            {
                itemType = item.Type;
            }
            else if (!item.Type.SameAs(itemType))
            {
                Diagnostics.Add(Diagnostic.Critical($"List element at index {i} should be a {itemType} and not {item.Type}", item));
            }

            result.Add(item);
        }

        if (itemType is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Could not infer the list element type", listValue));
            itemType = BuiltinType.Any;
        }

        ArrayType type = new(itemType, listValue.Values.Length);

        compiledStatement = new CompiledList()
        {
            Values = result.ToImmutable(),
            Type = type,
            Location = listValue.Location,
            SaveValue = listValue.SaveValue,
        };
        return true;
    }
    bool CompileExpression(ReinterpretExpression typeCast, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (!CompileType(typeCast.Type, out GeneralType? targetType, Diagnostics))
        {
            return false;
        }

        if (!CompileExpression(typeCast.PrevStatement, out CompiledExpression? prev)) return false;

        GeneralType statementType = prev.Type;

        if (statementType.Equals(targetType))
        {
            // Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Keyword, typeCast.File));
            compiledStatement = prev;
            return true;
        }

        SetStatementType(typeCast, targetType);

        compiledStatement = new CompiledReinterpretation()
        {
            Value = prev,
            TypeExpression = CompiledTypeExpression.CreateAnonymous(targetType, typeCast.Type),
            Type = targetType,
            Location = typeCast.Location,
            SaveValue = typeCast.SaveValue,
        };
        return true;
    }
    bool CompileExpression(ManagedTypeCastExpression typeCast, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        if (!CompileType(typeCast.Type, out GeneralType? targetType, Diagnostics))
        {
            return false;
        }
        SetStatementType(typeCast, targetType);

        if (!CompileExpression(typeCast.Expression, out CompiledExpression? prev, targetType)) return false;

        if (prev.Type.Equals(targetType))
        {
            // Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Type, typeCast.File));
            compiledStatement = prev;
            return true;
        }

        if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
            targetType.Is(out BuiltinType? targetBuiltinType) &&
            TryComputeSimple(typeCast.Expression, out CompiledValue prevValue) &&
            prevValue.TryCast(targetBuiltinType.RuntimeType, out CompiledValue castedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Type cast evaluated, converting {prevValue} ({prevValue.Type}) to {castedValue} ({castedValue.Type})", typeCast));
            compiledStatement = new CompiledConstantValue()
            {
                Value = castedValue,
                Type = targetBuiltinType,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }

        // f32 -> i32
        if (prev.Type.SameAs(BuiltinType.F32) &&
            targetType.SameAs(BuiltinType.I32))
        {
            compiledStatement = new CompiledCast()
            {
                Value = prev,
                Type = targetType,
                TypeExpression = CompiledTypeExpression.CreateAnonymous(targetType, typeCast.Type),
                Allocator = null,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }

        // i32 -> f32
        if (prev.Type.SameAs(BuiltinType.I32) &&
            targetType.SameAs(BuiltinType.F32))
        {
            compiledStatement = new CompiledCast()
            {
                Value = prev,
                Type = targetType,
                TypeExpression = CompiledTypeExpression.CreateAnonymous(targetType, typeCast.Type.Location),
                Allocator = null,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }
        //fixme
        compiledStatement = new CompiledCast()
        {
            Value = prev,
            Type = targetType,
            Allocator = null,
            Location = typeCast.Location,
            SaveValue = typeCast.SaveValue,
            TypeExpression = CompiledTypeExpression.CreateAnonymous(targetType, typeCast.Location),
        };
        return true;
    }
    bool CompileExpression(Expression statement, [NotNullWhen(true)] out CompiledExpression? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true) => statement switch
    {
        ListExpression v => CompileExpression(v, out compiledStatement, expectedType),
        BinaryOperatorCallExpression v => CompileExpression(v, out compiledStatement, expectedType),
        UnaryOperatorCallExpression v => CompileExpression(v, out compiledStatement),
        LiteralExpression v => CompileExpression(v, out compiledStatement, expectedType),
        IdentifierExpression v => CompileExpression(v, out compiledStatement, expectedType, resolveReference),
        GetReferenceExpression v => CompileExpression(v, out compiledStatement, expectedType),
        DereferenceExpression v => CompileExpression(v, out compiledStatement, expectedType),
        NewInstanceExpression v => CompileExpression(v, out compiledStatement, expectedType),
        ConstructorCallExpression v => CompileExpression(v, out compiledStatement, expectedType),
        IndexCallExpression v => CompileExpression(v, out compiledStatement, expectedType),
        FieldExpression v => CompileExpression(v, out compiledStatement, expectedType),
        ReinterpretExpression v => CompileExpression(v, out compiledStatement, expectedType),
        ManagedTypeCastExpression v => CompileExpression(v, out compiledStatement, expectedType),
        ArgumentExpression v => CompileExpression(v, out compiledStatement, expectedType),
        AnyCallExpression v => CompileExpression(v, out compiledStatement, expectedType),
        LambdaExpression v => CompileExpression(v, out compiledStatement, expectedType),
        _ => throw new NotImplementedException($"Expression {statement.GetType().Name} is not implemented"),
    };

    bool CompileSetter(Statement target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        switch (target)
        {
            case IdentifierExpression v: return CompileSetter(v, value, out compiledStatement);
            case FieldExpression v: return CompileSetter(v, value, out compiledStatement);
            case IndexCallExpression v: return CompileSetter(v, value, out compiledStatement);
            case DereferenceExpression v: return CompileSetter(v, value, out compiledStatement);
            default:
                Diagnostics.Add(Diagnostic.Critical($"The left side of the assignment operator should be a variable, field or memory address. Passed \"{target.GetType().Name}\"", target));
                return false;
        }
    }
    bool CompileSetter(IdentifierExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (RegisterKeywords.TryGetValue(target.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            if (!CompileExpression(value, out CompiledExpression? _value, registerKeyword.Type)) return false;

            GeneralType valueType = _value.Type;

            if (!CanCastImplicitly(valueType, registerKeyword.Type, value, out PossibleDiagnostic? castError))
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

            Diagnostics.Add(Diagnostic.Critical($"Can not set constant value: it is readonly", target));
            return false;
        }

        if (GetParameter(target.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (target.Content != StatementKeywords.This)
            { target.AnalyzedType = TokenAnalyzedType.ParameterName; }
            SetStatementType(target, parameter.Type);
            SetStatementReference(target, parameter);

            if (!CompileExpression(value, out CompiledExpression? _value, parameter.Type)) return false;

            GeneralType valueType = _value.Type;

            if (!CanCastImplicitly(valueType, parameter.Type, value, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(value));
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
                    _v2.Parameter == parameter,
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
            { Diagnostics.Add(Diagnostic.Internal($"Trying to set local variable \"{variable.Identifier}\" but it was compiled as a global variable.", target)); }

            if (Frames.Last.CompiledGeneratorContext is not null)
            {
                //Debugger.Break();
                if (GeneratorStructDefinition is null)
                {
                    Diagnostics.Add(Diagnostic.Critical($"No struct found with an [Builtin(\"generator\")] attribute.", variable));
                    return false;
                }

                CompiledField field = Frames.Last.CompiledGeneratorContext.State.AddVariable(variable.Identifier, variable.Type);
                if (!GetParameter("this", out CompiledParameter? thisParameter, out parameterNotFoundError))
                {
                    Diagnostics.Add(parameterNotFoundError.ToError(variable));
                    return false;
                }

                compiledStatement = new CompiledSetter()
                {
                    Target = new CompiledFieldAccess()
                    {
                        Field = field,
                        Object = new CompiledParameterAccess()
                        {
                            Parameter = thisParameter,
                            Type = thisParameter.Type,
                            SaveValue = true,
                            Location = variable.Location,
                        },
                        Type = field.Type,
                        Location = target.Location.Union(value.Location),
                        SaveValue = true,
                    },
                    Value = _value,
                    Location = target.Location.Union(value.Location),
                    IsCompoundAssignment =
                        _value is CompiledBinaryOperatorCall _v3 &&
                        _v3.Left is CompiledVariableAccess _v4 &&
                        _v4.Variable == variable,
                };
                return true;
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
                    _v2.Variable == variable,
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
            { Diagnostics.Add(Diagnostic.Internal($"Trying to set global variable \"{globalVariable.Identifier}\" but it was compiled as a local variable.", target)); }

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
                    _v2.Variable == globalVariable,
            };
            return true;
        }

        Diagnostics.Add(Diagnostic.Critical($"Symbol \"{target.Content}\" not found", target)
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

        if (!CompileExpression(target.Object, out CompiledExpression? prev)) return false;
        GeneralType prevType = prev.Type;

        if (prevType.Is<ArrayType>() && target.Identifier.Content == "Length")
        {
            Diagnostics.Add(Diagnostic.Critical("Array type's length is readonly", target));
            return false;
        }

        if (prevType.Is(out PointerType? pointerType2))
        {
            prevType = pointerType2.To;

            while (prevType.Is(out pointerType2))
            {
                prevType = pointerType2.To;
            }

            if (!prevType.Is(out StructType? structPointerType))
            {
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", target.Object));
                return false;
            }

            if (!structPointerType.GetField(target.Identifier.Content, out CompiledField? compiledField, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(target.Identifier, target.File));
                return false;
            }

            GeneralType type = GeneralType.InsertTypeParameters(compiledField.Type, structPointerType.TypeArguments);
            if (!CompileExpression(value, out CompiledExpression? _value, type)) return false;

            if (!CanCastImplicitly(_value.Type, type, value, out PossibleDiagnostic? castError2))
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

            GeneralType type = GeneralType.InsertTypeParameters(compiledField.Type, structType.TypeArguments) ?? compiledField.Type;
            if (!CompileExpression(value, out CompiledExpression? _value, type)) return false;

            if (!CanCastImplicitly(_value.Type, type, value, out PossibleDiagnostic? castError2))
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

        throw new NotImplementedException();
    }
    bool CompileSetter(IndexCallExpression target, Expression value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileExpression(target.Object, out CompiledExpression? _base)) return false;
        if (!CompileExpression(target.Index, out CompiledExpression? _index)) return false;

        if (!CompileExpression(value, out CompiledExpression? _value)) return false;

        if (GetIndexSetter(_base.Type, _value.Type, _index.Type, target.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? indexerNotFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(target, target.File));
            if (CompileFunctionCall(target, ImmutableArray.Create(ArgumentExpression.Wrap(target.Object), target.Index, ArgumentExpression.Wrap(value)), indexer, out CompiledExpression? compiledStatement2))
            {
                compiledStatement = compiledStatement2;
                return true;
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
            return false;
        }

        if (!CanCastImplicitly(_value.Type, itemType, value, out PossibleDiagnostic? castError))
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

        if (!CanCastImplicitly(_value.Type, targetType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (!prev.Type.Is<PointerType>())
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{prev.Type}\" isn't a pointer", target.Expression));
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

    #endregion

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

    #region GenerateCodeFor...

    readonly HashSet<FunctionThingDefinition> _generatedFunctions = new();

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m2 = new("LanguageCore.Compiler.Function");
#endif
    bool CompileFunction<TFunction>(TFunction function, ImmutableDictionary<string, GeneralType>? typeArguments)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition
    {
        if (!_generatedFunctions.Add(function))
        {
            if (GeneratedFunctions.Any(v => v.Function == function))
            {
                // Something went wrong bruh
            }
            return false;
        }

#if UNITY
        using var _1 = _m2.Auto();
#endif

        if (function.Identifier is not null &&
            LanguageConstants.KeywordList.Contains(function.Identifier.ToString()))
        {
            Diagnostics.Add(Diagnostic.Error($"The identifier \"{function.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.File));
            goto end;
        }

        if (function.Identifier is not null)
        { function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

        if (function is IExternalFunctionDefinition externalFunctionDefinition &&
            externalFunctionDefinition.ExternalFunctionName is not null &&
            ExternalFunctions.Any(v => v.Name == externalFunctionDefinition.ExternalFunctionName))
        {
            // FIXME: hmmm
            return false;
        }

        if (function.Block is null)
        {
            //Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", function));
            goto end;
        }

        GeneralType returnType = function.Type;
        ImmutableArray<CompiledParameter>.Builder compiledParameters = ImmutableArray.CreateBuilder<CompiledParameter>();
        CompiledGeneratorContext? compiledGeneratorContext = null;

        int paramIndex = 0;

        CompiledStatement? prefixStatement = null;
        CompiledStatement? suffixStatement = null;

        if (function is CompiledFunctionDefinition &&
            GeneratorStructDefinition is not null &&
            returnType.FinalValue.Is(out StructType? _v) &&
            _v.Struct == GeneratorStructDefinition.Struct)
        {
            GeneralType resultType = _v.TypeArguments.First().Value;

            CompiledGeneratorState generatorState = GetGeneratorState(function);

            CompiledParameter thisParmater;
            CompiledParameter resultParameter;

            compiledParameters.Add(thisParmater = new CompiledParameter(
                //paramIndex,
                new PointerType(new StructType(generatorState.Struct, function.File)),
                new ParameterDefinition(
                    ImmutableArray.Create<Token>(Token.CreateAnonymous(ModifierKeywords.This)),
                    new TypeInstancePointer(new TypeInstanceSimple(Token.CreateAnonymous(GetGeneratorState(function).Struct.Identifier.Content), function.File), Token.CreateAnonymous("*"), function.File),
                    Token.CreateAnonymous("this"),
                    null
                )
            ));
            paramIndex++;

            compiledParameters.Add(resultParameter = new CompiledParameter(
                //paramIndex,
                GeneralType.InsertTypeParameters(GeneratorStructDefinition.NextFunction.Parameters[1].Type, _v.TypeArguments),
                GeneratorStructDefinition.NextFunction.Parameters[1]
            ));
            paramIndex++;

            returnType = BuiltinType.U8;

            Location l = function.Block.Location.Before();
            prefixStatement = new CompiledIf()
            {
                Location = l,
                Condition = new CompiledFieldAccess()
                {
                    Object = new CompiledParameterAccess()
                    {
                        Parameter = thisParmater,
                        Type = thisParmater.Type,
                        Location = l,
                        SaveValue = true,
                    },
                    Field = generatorState.Struct.Fields[0],
                    Type = generatorState.Struct.Fields[0].Type,
                    Location = l,
                    SaveValue = true,
                },
                Body = new CompiledGoto()
                {
                    Value = new CompiledFieldAccess()
                    {
                        Object = new CompiledParameterAccess()
                        {
                            Parameter = thisParmater,
                            Type = thisParmater.Type,
                            Location = l,
                            SaveValue = true,
                        },
                        Field = generatorState.Struct.Fields[0],
                        Type = generatorState.Struct.Fields[0].Type,
                        Location = l,
                        SaveValue = true,
                    },
                    Location = l,
                },
                Next = null,
            };

            l = function.Block.Location.After();
            suffixStatement = new CompiledReturn()
            {
                Location = l,
                Value = new CompiledConstantValue()
                {
                    Value = new CompiledValue((byte)0),
                    SaveValue = true,
                    Location = l,
                    Type = returnType,
                },
            };

            compiledGeneratorContext = new CompiledGeneratorContext()
            {
                ThisParameter = thisParmater,
                ResultParameter = resultParameter,
                ResultType = resultType,
                State = generatorState,
            };
        }
        else
        {
            for (int i = 0; i < function.Parameters.Count; i++)
            {
                compiledParameters.Add(((ICompiledFunctionDefinition)function).Parameters[i]);
            }
        }

        ImmutableArray<CompiledLabelDeclaration>.Builder localInstructionLabels = ImmutableArray.CreateBuilder<CompiledLabelDeclaration>();

        foreach (Statement item in StatementWalker.Visit(function.Block, StatementWalkerFilter.FrameOnlyFilter))
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
            IsTemplate = function.IsTemplate,
            TypeParameters = function.Template?.Parameters ?? ImmutableArray<Token>.Empty,
            CompiledParameters = compiledParameters.ToImmutable(),
            InstructionLabels = localInstructionLabels.ToImmutable(),
            Scopes = new(),
            CurrentReturnType = returnType,
            CompiledGeneratorContext = compiledGeneratorContext,
            IsTopLevel = false,
        }))
        {
            CompiledStatement? body;
            using (frame.Value.Scopes.PushAuto(new Scope(ImmutableArray<CompiledVariableConstant>.Empty)))
            {
                if (!CompileStatement(function.Block, out body)) return false;

                if (prefixStatement is not null || suffixStatement is not null)
                {
                    ImmutableArray<CompiledStatement> bodyStatements = ((CompiledBlock)body).Statements;

                    ImmutableArray<CompiledStatement>.Builder v = ImmutableArray.CreateBuilder<CompiledStatement>(bodyStatements.Length + (prefixStatement is null ? 0 : 1) + (suffixStatement is null ? 0 : 1));
                    if (prefixStatement is not null) v.Add(prefixStatement);
                    v.AddRange(bodyStatements);
                    if (suffixStatement is not null) v.Add(suffixStatement);
                    bodyStatements = v.MoveToImmutable();

                    body = new CompiledBlock()
                    {
                        Location = body.Location,
                        Statements = bodyStatements,
                    };
                }
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

            GeneratedFunctions.Add(new(function, (CompiledBlock)body, closure));

            if (!closure.IsEmpty) throw new NotImplementedException();

            return true;
        }

    end:
        return false;
    }

    bool CompileFunctions<TFunction>(IEnumerable<TFunction> functions)
        where TFunction : FunctionThingDefinition, IHaveInstructionOffset, IReferenceable, ICompiledFunctionDefinition
    {
        bool compiledAnything = false;
        foreach (TFunction function in functions)
        {
            if (function.InstructionOffset >= 0) continue;
            if (function.IsTemplate) continue;
            if (!Settings.CompileEverything)
            {
                if (!function.References.Any() && (function is not IExposeable exposeable || exposeable.ExposedFunctionName is null))
                { continue; }
            }

            if (CompileFunction(function, null))
            { compiledAnything = true; }
        }
        return compiledAnything;
    }
    bool CompileFunctionTemplates<T>(IReadOnlyList<CompliableTemplate<T>> functions)
        where T : FunctionThingDefinition, ITemplateable<T>, IHaveInstructionOffset, ICompiledFunctionDefinition
    {
        bool compiledAnything = false;
        int i = 0;
        while (i < functions.Count)
        {
            CompliableTemplate<T> function = functions[i];
            i++;

            if (function.Function.InstructionOffset >= 0) continue;

            if (CompileFunction(function.Function, function.TypeArguments))
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

        foreach (Statement statement in statements)
        {
            if (!CompileStatement(statement, out CompiledStatement? compiledStatement))
            {
                compiledStatements = ImmutableArray<CompiledStatement>.Empty;
                return false;
            }
            if (Settings.IsExpression)
            {
                res.Add(compiledStatement);
            }
            else
            {
                if (compiledStatement is CompiledEmptyStatement) continue;

                ImmutableArray<CompiledStatement> reduced = ReduceStatements(compiledStatement, true);
                res.AddRange(reduced);
            }
        }

        compiledStatements = res.ToImmutable();
        return true;
    }

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m1 = new("LanguageCore.Compiler.TopLevelStatements");
#endif
    bool CompileExpression(Statement statement, Dictionary<string, int>? contextualVariables, [NotNullWhen(true)] out ImmutableArray<CompiledStatement> compiledStatements)
    {
#if UNITY
        using var _1 = _m1.Auto();
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

    #endregion

    void GenerateCode(ImmutableArray<ParsedFile> parsedFiles, Uri entryFile)
    {
        ImmutableArray<CompiledLabelDeclaration>.Builder globalInstructionLabels = ImmutableArray.CreateBuilder<CompiledLabelDeclaration>();

        foreach (VariableDefinition variableDeclaration in TopLevelStatements
            .SelectMany(v => v.Statements)
            .OfType<VariableDefinition>())
        {
            if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
            {
                if (CompileConstant(variableDeclaration, out CompiledVariableConstant? result))
                {
                    CompiledGlobalConstants.Add(result);
                }
            }
        }

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
            CompiledGeneratorContext = null,
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
                else
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

        /*
        var allStatements =
            Visit(compiledTopLevelStatements)
            .Concat(GeneratedFunctions.SelectMany(v => Visit(v.Body)));

        foreach (var item in allStatements.OfType<CompiledVariableDeclaration>())
        {
            item.Getters.Clear();
            item.Setters.Clear();
        }
        foreach (var item in allStatements.OfType<CompiledInstructionLabelDeclaration>())
        {
            item.Getters.Clear();
        }

        foreach (var item in allStatements.OfType<CompiledVariableGetter>())
        {
            item.Variable.Getters.Add(item);
        }

        foreach (var item in allStatements.OfType<CompiledVariableSetter>())
        {
            item.Variable.Setters.Add(item);
        }

        foreach (var item in allStatements.OfType<InstructionLabelAddressGetter>())
        {
            item.InstructionLabel.Getters.Add(item);
        }
        */

        foreach (CompiledFunction function in GeneratedFunctions)
        {
            AnalyseFunction(function, new());
        }
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
                case CompiledVariableAccess v:
                    if (v.Variable.IsGlobal)
                    {
                        flags |= FunctionFlags.CapturesGlobalVariables;
                    }
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
