using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using LiteralStatement = LanguageCore.Parser.Statements.Literal;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
    bool CompileAllocation(StatementWithValue size, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(size, out CompiledStatementWithValue? compiledSize)) return false;

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, ImmutableArray.Create(compiledSize.Type), size.File, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", size));
            return false;
        }

        if (result.DidReplaceArguments) throw new UnreachableException();

        CompiledFunctionDefinition allocator = result.Function;

        if (!allocator.ReturnSomething)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", size));
            return false;
        }

        allocator.References.AddReference(size, size.File);

        if (!allocator.CanUse(size.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", size));
            return false;
        }

        if (TryEvaluate(allocator, ImmutableArray.Create(compiledSize), new EvaluationContext(), out CompiledValue? returnValue, out ImmutableArray<RuntimeStatement2> runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice("Function evaluated", size));
            compiledStatement = new CompiledEvaluatedValue()
            {
                Location = size.Location,
                SaveValue = true,
                Type = allocator.Type,
                Value = returnValue.Value,
            };
            return true;
        }

        ImmutableArray<StatementWithValue> arguments = ImmutableArray.Create(size);

        if (!CompileArguments(arguments, allocator, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

        if (allocator.ExternalFunctionName is not null)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator));
                return false;
            }

            return CompileFunctionCall_External(compiledArguments, true, allocator, externalFunction, size.Location, out compiledStatement);
        }

        compiledStatement = new CompiledFunctionCall()
        {
            Function = allocator,
            Arguments = compiledArguments,
            Location = size.Location,
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

        if (result.DidReplaceArguments) throw new UnreachableException();

        deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(null, location.File));

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
        FunctionQueryResult<CompiledGeneralFunctionDefinition>? result;

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (deallocateablePointerType.To.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning($"Destructor for type \"{deallocateableType}\" not found", location)
                        .WithSuberrors(error.ToWarning(location)));
                }

                if (!CompileDeallocation(deallocateableType, location, out CompiledFunctionDefinition? deallocator)) return false;

                compiledCleanup = new CompiledCleanup()
                {
                    Destructor = null,
                    Deallocator = deallocator,
                    Location = location,
                    TrashType = deallocateableType,
                };
                return true;
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (AllowDeallocate(deallocateableType))
                {
                    if (!CompileDeallocation(deallocateableType, location, out CompiledFunctionDefinition? deallocator)) return false;

                    compiledCleanup = new CompiledCleanup()
                    {
                        Destructor = null,
                        Deallocator = deallocator,
                        Location = location,
                        TrashType = deallocateableType,
                    };
                    return true;
                }
                Diagnostics.Add(Diagnostic.Warning($"Destructor for type \"{deallocateableType}\" not found", location).WithSuberrors(error.ToWarning(location)));
                return false;
            }
        }

        CompiledGeneralFunctionDefinition? destructor = result.Function;

        destructor.References.Add(new Reference<Statement?>(null, location.File));

        if (!destructor.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" function cannot be called due to its protection level", location));
            return false;
        }

        if (AllowDeallocate(deallocateableType))
        {
            if (!CompileDeallocation(deallocateableType, location, out CompiledFunctionDefinition? deallocator)) return false;

            compiledCleanup = new CompiledCleanup()
            {
                Deallocator = deallocator,
                Destructor = destructor,
                Location = location,
                TrashType = deallocateableType,
            };
            return true;
        }
        else
        {
            compiledCleanup = new CompiledCleanup()
            {
                Deallocator = null,
                Destructor = destructor,
                Location = location,
                TrashType = deallocateableType,
            };
            return true;
        }
    }

    #region CompileStatement

    bool CompileStatement(VariableDeclaration newVariable, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;
        if (newVariable.Modifiers.Contains(ModifierKeywords.Const))
        {
            compiledStatement = new EmptyStatement()
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
            if (!CompileType(newVariable.Type, out type, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(newVariable.Type));
            }
            else
            {
                if (type is ArrayType arrayType)
                {
                    if (newVariable.InitialValue is LiteralList literalList &&
                        arrayType.Length is null)
                    {
                        type = new ArrayType(arrayType.Of, new CompiledEvaluatedValue()
                        {
                            Value = literalList.Values.Length,
                            Location = newVariable.Type.Location,
                            Type = ArrayLengthType,
                            SaveValue = true,
                        });
                    }

                    if (newVariable.InitialValue is LiteralStatement literalStatement &&
                        literalStatement.Type == LiteralType.String)
                    {
                        if (arrayType.Of.SameAs(BasicType.U16))
                        {
                            int length = literalStatement.Value.Length + 1;

                            if (arrayType.ComputedLength.HasValue)
                            {
                                length = arrayType.ComputedLength.Value;
                            }
                            else if (arrayType.Length is not null)
                            {
                                if (arrayType.Length is CompiledEvaluatedValue evaluatedLength)
                                {
                                    length = (int)evaluatedLength.Value;
                                }
                            }

                            if (length != literalStatement.Value.Length &&
                                length != literalStatement.Value.Length + 1)
                            {
                                Diagnostics.Add(Diagnostic.Error($"String literal's length ({literalStatement.Value.Length}) doesn't match with the type's length ({length})", literalStatement));
                            }

                            type = new ArrayType(arrayType.Of, new CompiledEvaluatedValue()
                            {
                                Value = length,
                                Location = newVariable.Type.Location,
                                Type = ArrayLengthType,
                                SaveValue = true,
                            });
                        }
                        else if (arrayType.Of.SameAs(BasicType.U8))
                        {
                            int length = literalStatement.Value.Length + 1;

                            if (arrayType.ComputedLength.HasValue)
                            {
                                length = arrayType.ComputedLength.Value;
                            }
                            else if (arrayType.Length is not null)
                            {
                                if (arrayType.Length is CompiledEvaluatedValue evaluatedLength)
                                {
                                    length = (int)evaluatedLength.Value;
                                }
                            }

                            if (length != literalStatement.Value.Length &&
                                length != literalStatement.Value.Length + 1)
                            {
                                Diagnostics.Add(Diagnostic.Error($"String literal's length ({literalStatement.Value.Length}) doesn't match with the type's length ({length})", literalStatement));
                            }

                            type = new ArrayType(arrayType.Of, new CompiledEvaluatedValue()
                            {
                                Value = length,
                                Location = newVariable.Type.Location,
                                Type = ArrayLengthType,
                                SaveValue = true,
                            });
                        }
                    }
                }

                newVariable.Type.SetAnalyzedType(type);
                newVariable.CompiledType = type;
            }
        }

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, newVariable.File, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Critical($"Symbol name \"{newVariable.Identifier}\" conflicts with an another symbol name", newVariable.Identifier));
            return false;
        }

        CompiledStatementWithValue? initialValue = null;

        CompileVariableAttributes(newVariable);

        if (newVariable.ExternalConstantName is not null)
        {
            ExternalConstant? externalConstant = ExternalConstants.FirstOrDefault(v => v.Name == newVariable.ExternalConstantName);
            if (externalConstant is null)
            {
                Diagnostics.Add(Diagnostic.Warning($"External constant \"{newVariable.ExternalConstantName}\" not found", newVariable));
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

                initialValue = new CompiledEvaluatedValue()
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
            if (!CompileStatement(newVariable.InitialValue, out initialValue, type)) return false;
            type ??= initialValue.Type;
            if (!CanCastImplicitly(initialValue, type, out CompiledStatementWithValue? assignedInitialValue, out PossibleDiagnostic? castError))
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

        if (!type.AllGenericsDefined())
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\" (what edge case is this???)", newVariable.Type, newVariable.File));
        }

        newVariable.Type.SetAnalyzedType(type);

        CompiledCleanup? compiledCleanup = null;
        if (newVariable.Modifiers.Contains(ModifierKeywords.Temp))
        {
            CompileCleanup(type, newVariable.Location, out compiledCleanup);
        }

        bool isGlobal = Frames.Last.IsTopLevel && Frames.Last.Scopes.Count <= 1;

        CompiledVariableDeclaration compiledVariable = new()
        {
            Identifier = newVariable.Identifier.Content,
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
                compiledStatement = new EmptyStatement()
                {
                    Location = compiledVariable.Location,
                };
            }
            else
            {
                compiledStatement = new CompiledFieldSetter()
                {
                    Field = field,
                    Object = new CompiledParameterGetter()
                    {
                        Variable = thisParameter,
                        Type = thisParameter.Type,
                        SaveValue = true,
                        Location = compiledVariable.Location,
                    },
                    Value = initialValue,
                    Location = compiledVariable.Location,
                    Type = field.Type,
                    IsCompoundAssignment = false,
                };
            }
        }

        return true;
    }
    bool CompileStatement(InstructionLabel instructionLabel, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GetInstructionLabel(instructionLabel.Identifier.Content, out CompiledInstructionLabelDeclaration? compiledInstructionLabelDeclaration, out _))
        {
            Diagnostics.Add(Diagnostic.Internal($"Instruction label \"{instructionLabel.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", instructionLabel.Identifier));
            return false;
        }

        compiledStatement = compiledInstructionLabelDeclaration;
        return true;
    }
    bool CompileStatement(KeywordCall keywordCall, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            CompiledStatementWithValue? returnValue = null;

            if (keywordCall.Arguments.Length == 1)
            {
                if (!CompileStatement(keywordCall.Arguments[0], out returnValue, Frames.Last.CurrentReturnType)) return false;
                Frames.Last.CurrentReturnType ??= returnValue.Type;

                if (!CanCastImplicitly(returnValue, Frames.Last.CurrentReturnType, out CompiledStatementWithValue? assignedReturnValue, out PossibleDiagnostic? castError))
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
                if (!CompileStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? yieldValue, Frames.Last.CompiledGeneratorContext.ResultType)) return false;

                if (!CanCastImplicitly(yieldValue.Type, Frames.Last.CompiledGeneratorContext.ResultType, keywordCall.Arguments[0], out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                }

                statements.Add(new CompiledIndirectSetter()
                {
                    AddressValue = new CompiledParameterGetter()
                    {
                        Variable = Frames.Last.CompiledGeneratorContext.ResultParameter,
                        Location = keywordCall.Location,
                        SaveValue = true,
                        Type = Frames.Last.CompiledGeneratorContext.ResultParameter.Type,
                    },
                    Value = yieldValue,
                    Location = keywordCall.Location,
                    IsCompoundAssignment = false,
                });
            }

            CompiledInstructionLabelDeclaration l = new()
            {
                Identifier = "fuck",
                Location = keywordCall.Location,
            };

            statements.Add(new CompiledFieldSetter()
            {
                Object = new CompiledParameterGetter()
                {
                    Variable = Frames.Last.CompiledGeneratorContext.ThisParameter,
                    Location = keywordCall.Location,
                    SaveValue = true,
                    Type = Frames.Last.CompiledGeneratorContext.ThisParameter.Type,
                },
                Field = Frames.Last.CompiledGeneratorContext.State.StateField,
                Value = new InstructionLabelAddressGetter()
                {
                    InstructionLabel = l,
                    Location = keywordCall.Location,
                    Type = new FunctionType(BuiltinType.Void, ImmutableArray<GeneralType>.Empty, false),
                    SaveValue = true,
                },
                Type = Frames.Last.CompiledGeneratorContext.State.StateField.Type,
                Location = keywordCall.Location,
                IsCompoundAssignment = false,
            });

            statements.Add(new CompiledReturn()
            {
                Value = new CompiledEvaluatedValue()
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

            if (!CompileStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? throwValue)) return false;

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
            if (!CompileStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? value)) return false;

            if (!CompileCleanup(value.Type, keywordCall.Arguments[0].Location, out CompiledCleanup? compiledCleanup)) return false;

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

            if (!CompileStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? to)) return false;

            if (!CanCastImplicitly(to.Type, CompiledInstructionLabelDeclaration.Type, out PossibleDiagnostic? castError))
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

    bool CompileArguments(IReadOnlyList<StatementWithValue> arguments, ICompiledFunctionDefinition compiledFunction, [NotNullWhen(true)] out ImmutableArray<CompiledPassedArgument> compiledArguments, int alreadyPassed = 0)
    {
        compiledArguments = ImmutableArray<CompiledPassedArgument>.Empty;

        ImmutableArray<CompiledPassedArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledPassedArgument>(arguments.Count);

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
            StatementWithValue argument = arguments[i];

            if (!CompileStatement(argument, out CompiledStatementWithValue? compiledArgument, parameter.Type)) return false;

            if (parameter.Type.Is<PointerType>() &&
                parameter.Modifiers.Any(v => v.Content == ModifierKeywords.This) &&
                !compiledArgument.Type.Is<PointerType>())
            {
                if (!CompileStatement(new AddressGetter(
                    Token.CreateAnonymous("&", TokenType.Operator, argument.Position.Before()),
                    argument,
                    argument.File
                ), out compiledArgument, parameter.Type))
                { return false; }
            }

            if (!CanCastImplicitly(compiledArgument, parameter.Type, out CompiledStatementWithValue? assignedArgument, out PossibleDiagnostic? error))
            { Diagnostics.Add(error.ToError(argument)); }
            compiledArgument = assignedArgument;

            if (compiledArgument.Type.GetSize(this, Diagnostics, argument) != parameter.Type.GetSize(this, Diagnostics, parameter))
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameter.Type}\" passed \"{compiledArgument.Type}\"", argument)); }

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

            result.Add(new CompiledPassedArgument()
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
                StatementWithValue? argument = parameter.DefaultValue;
                if (argument is null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"Can't explain this error", parameter));
                    return false;
                }
                else
                {
                    Diagnostics.Add(Diagnostic.Warning($"WIP", argument));
                }

                if (!CompileStatement(argument, out CompiledStatementWithValue? compiledArgument, parameter.Type)) return false;

                if (!CanCastImplicitly(compiledArgument, parameter.Type, out CompiledStatementWithValue? assignedArgument, out PossibleDiagnostic? error))
                { Diagnostics.Add(error.ToError(argument)); }
                compiledArgument = assignedArgument;

                if (compiledArgument.Type.GetSize(this, Diagnostics, argument) != parameter.Type.GetSize(this, Diagnostics, parameter))
                { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameter.Type}\" passed \"{compiledArgument.Type}\"", argument)); }

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

                result.Add(new CompiledPassedArgument()
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
    bool CompileArguments(IReadOnlyList<StatementWithValue> arguments, FunctionType function, [NotNullWhen(true)] out ImmutableArray<CompiledPassedArgument> compiledArguments)
    {
        compiledArguments = ImmutableArray<CompiledPassedArgument>.Empty;

        ImmutableArray<CompiledPassedArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledPassedArgument>(arguments.Count);

        for (int i = 0; i < arguments.Count; i++)
        {
            StatementWithValue argument = arguments[i];
            GeneralType parameterType = function.Parameters[i];

            if (!CompileStatement(argument, out CompiledStatementWithValue? compiledArgument, parameterType)) return false;

            if (!CanCastImplicitly(compiledArgument, parameterType, out CompiledStatementWithValue? assignedArgument, out PossibleDiagnostic? error))
            { Diagnostics.Add(error.ToError(compiledArgument)); }
            compiledArgument = assignedArgument;

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && AllowDeallocate(compiledArgument.Type);

            if (StatementCanBeDeallocated(argument, out bool explicitDeallocate))
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

            result.Add(new CompiledPassedArgument()
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

    bool CompileFunctionCall_External<TFunction>(ImmutableArray<CompiledPassedArgument> arguments, bool saveValue, TFunction compiledFunction, IExternalFunction externalFunction, Location location, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
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
    bool CompileFunctionCall<TFunction>(StatementWithValue caller, ImmutableArray<StatementWithValue> arguments, FunctionQueryResult<TFunction> _callee, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
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
            if (!CompileAllocation(new AnyCall(
                new Identifier(Token.CreateAnonymous("sizeof"), caller.File),
                ImmutableArray.Create<StatementWithValue>(new CompiledTypeStatement(Token.CreateAnonymous(StatementKeywords.Type), new StructType(GetGeneratorState(callee).Struct, caller.File), caller.File)),
                ImmutableArray<Token>.Empty,
                TokenPair.CreateAnonymous(caller.Position, "(", ")"),
                caller.File
            ), out CompiledStatementWithValue? allocation))
            {
                compiledStatement = null;
                return false;
            }

            CompliableTemplate<CompiledConstructorDefinition> compliableTemplate = AddCompilable(new CompliableTemplate<CompiledConstructorDefinition>(
                GeneratorStructDefinition.Constructor,
                returnStructType.TypeArguments
            ));

            compiledStatement = new CompiledConstructorCall()
            {
                Object = new CompiledStackAllocation()
                {
                    Type = callee.Type,
                    Location = caller.Location,
                    SaveValue = true,
                },
                Function = compliableTemplate.Function,
                Arguments = ImmutableArray.Create(
                    new CompiledPassedArgument()
                    {
                        Value = new FunctionAddressGetter()
                        {
                            Function = callee,
                            Type = new FunctionType(callee),
                            Location = caller.Location,
                            SaveValue = true,
                        },
                        Cleanup = new CompiledCleanup()
                        {
                            Location = callee.Location,
                            TrashType = new FunctionType(callee),
                        },
                        Type = new FunctionType(callee),
                        Location = caller.Location,
                        SaveValue = true,
                    },
                    new CompiledPassedArgument()
                    {
                        Value = new CompiledFakeTypeCast()
                        {
                            Value = allocation,
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
            GeneratorStructDefinition.Constructor.References.Add(new Reference<ConstructorCall>(
                new ConstructorCall(
                    Token.CreateAnonymous(StatementKeywords.New),
                    new TypeInstanceSimple(
                        Token.CreateAnonymous(GeneratorStructDefinition.Struct.Identifier.Content),
                        caller.Location.File
                    ),
                    ImmutableArray<StatementWithValue>.Empty,
                    TokenPair.CreateAnonymous("(", ")"),
                    caller.File
                ),
                caller.File,
                false
            ));

            return true;
        }

        compiledStatement = null;
        OnGotStatementType(caller, callee.Type);

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

        if (!CompileArguments(arguments, callee, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

        if (callee is IInContext<CompiledStruct> calleeInContext &&
            calleeInContext.Context != null &&
            calleeInContext.Context == GeneratorStructDefinition?.Struct)
        {
            CompiledGeneratorState stateStruct = GetGeneratorState(callee);
            List<CompiledPassedArgument> modifiedArguments = new(compiledArguments.Length)
            {
                new()
                {
                    Value = new CompiledFieldGetter()
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
                Function = new CompiledFieldGetter()
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
            caller.PredictedValue = returnValue.Value;
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Function evaluated with result \"{returnValue.Value}\"", caller));
            compiledStatement = new CompiledEvaluatedValue()
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
            if (f?.Body is not null)
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
                        ImmutableArray<CompiledPassedArgument> volatileArguments =
                            compiledArguments
                            .Where(v => GetStatementComplexity(v.Value).HasFlag(StatementComplexity.Volatile))
                            .ToImmutableArray();
                        int i = 0;
                        foreach (CompiledPassedArgument? item in volatileArguments)
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

                    foreach (CompiledPassedArgument argument in compiledArguments)
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
                        compiledStatement = new CompiledStatementWithValueThatActuallyDoesntHaveValue()
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
                             inlined1 is CompiledStatementWithValue statementWithValue)
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
                    InlineFunction(f.Body, new InlineContext()
                    {
                        Arguments = f.Function.Parameters
                            .Select((value, index) => (value.Identifier.Content, compiledArguments[index]))
                            .ToImmutableDictionary(v => v.Content, v => v.Item2),
                    }, out inlined1);
                    Diagnostics.Add(Diagnostic.FailedOptimization($"Failed to inline \"{callee.ToReadable()}\"", caller));
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

    bool CompileStatement(AnyCall anyCall, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (anyCall.PrevStatement is Identifier _identifier &&
            _identifier.Content == "sizeof")
        {
            _identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall));
                return false;
            }

            StatementWithValue param = anyCall.Arguments[0];
            GeneralType? paramType;
            if (param is TypeStatement typeStatement)
            {
                if (!CompileType(typeStatement.Type, out paramType, out PossibleDiagnostic? typeError))
                {
                    Diagnostics.Add(typeError.ToError(typeStatement.Type));
                    return false;
                }
            }
            else if (param is CompiledTypeStatement compiledTypeStatement)
            {
                paramType = compiledTypeStatement.Type;
            }
            else if (param is Identifier identifier)
            {
                if (FindType(identifier.Token, identifier.File, out paramType))
                { paramType = OnGotStatementType(identifier, paramType); }
                else
                {
                    Diagnostics.Add(Diagnostic.Critical($"Type \"{param}\" not found", param));
                    return false;
                }
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical($"Type \"{param}\" not found", param));
                return false;
            }

            GeneralType resultType = SizeofStatementType;
            if (expectedType is not null &&
                CanCastImplicitly(resultType, expectedType, out _))
            {
                resultType = expectedType;
            }

            OnGotStatementType(anyCall, resultType);

            compiledStatement = new CompiledSizeof()
            {
                Of = paramType,
                Location = param.Location,
                Type = resultType,
                SaveValue = anyCall.SaveValue,
            };
            return true;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunctionDefinition compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.AnalyzedType = TokenAnalyzedType.FunctionName; }

            if (anyCall.PrevStatement is Field _field)
            { _field.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

            anyCall.Reference = compiledFunction;
            if (anyCall.PrevStatement is IReferenceableTo _ref1)
            { _ref1.Reference = compiledFunction; }

            compiledFunction.References.Add(new(anyCall, anyCall.File));

            if (functionCall.CompiledType is not null)
            { OnGotStatementType(anyCall, functionCall.CompiledType); }

            return CompileFunctionCall(functionCall, functionCall.MethodArguments, result, out compiledStatement);
        }

        if (!CompileStatement(anyCall.PrevStatement, out CompiledStatementWithValue? functionValue))
        {
            if (notFound is not null)
            {
                Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            }
            return false;
        }

        {
            List<StatementWithValue> arguments = new();
            arguments.Add(anyCall.PrevStatement);
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
                Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.PrevStatement));
            }

            return false;
        }

        OnGotStatementType(anyCall, functionType.ReturnType);

        if (anyCall.Arguments.Length != functionType.Parameters.Length)
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", new Position(anyCall.Arguments.As<IPositioned>().DefaultIfEmpty(anyCall.Brackets)), anyCall.File));
            return false;
        }

        if (!CompileArguments(anyCall.Arguments, functionType, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

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
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
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
    bool CompileStatement(BinaryOperatorCall @operator, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result, out PossibleDiagnostic? notFoundError))
        {
            CompiledOperatorDefinition? operatorDefinition = result.Function;

            if (result.DidReplaceArguments) throw new UnreachableException();

            if (operatorDefinition.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!CompileFunctionCall(@operator, @operator.Arguments, result, out compiledStatement)) return false;

            if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
                TryCompute(compiledStatement, out CompiledValue evaluated) &&
                evaluated.TryCast(compiledStatement.Type, out evaluated))
            {
                compiledStatement = CompiledEvaluatedValue.Create(evaluated, compiledStatement);
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{evaluated}\"", @operator));
                @operator.PredictedValue = evaluated;
                operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File, true));
            }
            else
            {
                operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            }

            return true;
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            if (@operator.Operator.Content is
                "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                expectedType = null;
            }

            StatementWithValue left = @operator.Left;
            StatementWithValue right = @operator.Right;

            if (!CompileStatement(left, out CompiledStatementWithValue? compiledLeft, expectedType)) return false;
            if (!CompileStatement(right, out CompiledStatementWithValue? compiledRight, expectedType)) return false;

            GeneralType leftType = compiledLeft.Type;
            GeneralType rightType = compiledRight.Type;

            if (!leftType.TryGetNumericType(out NumericType leftNType) ||
                !rightType.TryGetNumericType(out NumericType rightNType))
            {
                Diagnostics.Add(notFoundError.ToError(@operator));
                return false;
            }

            BitWidth leftBitWidth = leftType.GetBitWidth(this, Diagnostics, left);
            BitWidth rightBitWidth = rightType.GetBitWidth(this, Diagnostics, right);

            leftType = BuiltinType.CreateNumeric(leftNType, leftBitWidth);
            rightType = BuiltinType.CreateNumeric(rightNType, rightBitWidth);

            if (!CompileStatement(left, out compiledLeft, leftType)) return false;

            if (leftNType != NumericType.Float &&
                rightNType == NumericType.Float)
            {
                compiledLeft = CompiledTypeCast.Wrap(compiledLeft, BuiltinType.F32);
                leftType = BuiltinType.F32;
                leftNType = NumericType.Float;
            }

            if (!CompileStatement(right, out compiledRight, rightType)) return false;

            if (leftType.SameAs(BasicType.F32) &&
                !rightType.SameAs(BasicType.F32))
            {
                compiledRight = CompiledTypeCast.Wrap(compiledRight, BuiltinType.F32);
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

                    leftBitWidth = leftType.GetBitWidth(this, Diagnostics, @operator.Left);
                    rightBitWidth = rightType.GetBitWidth(this, Diagnostics, @operator.Right);
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
                        case BinaryOperatorCall.CompLT:
                        case BinaryOperatorCall.CompGT:
                        case BinaryOperatorCall.CompLEQ:
                        case BinaryOperatorCall.CompGEQ:
                        case BinaryOperatorCall.CompEQ:
                        case BinaryOperatorCall.CompNEQ:
                            resultType = BooleanType;
                            break;

                        case BinaryOperatorCall.LogicalOR:
                        case BinaryOperatorCall.LogicalAND:
                        case BinaryOperatorCall.BitwiseAND:
                        case BinaryOperatorCall.BitwiseOR:
                        case BinaryOperatorCall.BitwiseXOR:
                        case BinaryOperatorCall.BitshiftLeft:
                        case BinaryOperatorCall.BitshiftRight:
                            resultType = numericResultType;
                            break;

                        case BinaryOperatorCall.Addition:
                        case BinaryOperatorCall.Subtraction:
                        case BinaryOperatorCall.Multiplication:
                        case BinaryOperatorCall.Division:
                        case BinaryOperatorCall.Modulo:
                            resultType = isFloat ? BuiltinType.F32 : numericResultType;
                            break;

                        default:
                            return false;
                    }

                    resultType = OnGotStatementType(@operator, resultType);

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

                    if (!leftType.GetBitWidth(this, out BitWidth leftBitwidth, out PossibleDiagnostic? error))
                    {
                        Diagnostics.Add(error.ToError(@operator.Left));
                        ok = false;
                    }

                    if (!rightType.GetBitWidth(this, out BitWidth rightBitwidth, out error))
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

            OnGotStatementType(@operator, resultType);

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
                compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                @operator.PredictedValue = casted;
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
    bool CompileStatement(UnaryOperatorCall @operator, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result, out PossibleDiagnostic? operatorNotFoundError))
        {
            CompiledOperatorDefinition? operatorDefinition = result.Function;

            if (result.DidReplaceArguments) throw new UnreachableException();

            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (operatorDefinition.Attributes.Any(v => v.Identifier.Content == AttributeConstants.MSILIncompatibleIdentifier))
            {
                Frames.LastRef.IsMsilCompatible = false;
            }

            if (!operatorDefinition.CanUse(@operator.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Operator \"{operatorDefinition.ToReadable()}\" cannot be called due to its protection level", @operator.Operator, @operator.File));
                return false;
            }

            if (UnaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to operator \"{operatorDefinition.ToReadable()}\": required {operatorDefinition.ParameterCount} passed {UnaryOperatorCall.ParameterCount}", @operator));
                return false;
            }

            if (!CompileArguments(@operator.Arguments, operatorDefinition, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

            if (operatorDefinition.ExternalFunctionName is not null)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                {
                    Diagnostics.Add(exception.ToError(@operator.Operator, @operator.File));
                    return false;
                }

                operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
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
                compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                @operator.PredictedValue = casted;
                operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File, true));
            }
            else
            {
                operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            }

            return true;
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            switch (@operator.Operator.Content)
            {
                case UnaryOperatorCall.LogicalNOT:
                {
                    if (!CompileStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

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
                        compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        @operator.PredictedValue = casted;
                    }

                    return true;
                }
                case UnaryOperatorCall.BinaryNOT:
                {
                    if (!CompileStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

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
                        compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        @operator.PredictedValue = casted;
                    }

                    return true;
                }
                case UnaryOperatorCall.UnaryMinus:
                {
                    if (!CompileStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

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
                        compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        @operator.PredictedValue = casted;
                    }

                    return true;
                }
                case UnaryOperatorCall.UnaryPlus:
                {
                    if (!CompileStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

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
                        compiledStatement = CompiledEvaluatedValue.Create(casted, compiledStatement);
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{casted}\"", @operator));
                        @operator.PredictedValue = casted;
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
    bool CompileStatement(LambdaStatement lambdaStatement, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        FunctionType? functionType = expectedType as FunctionType;

        ImmutableArray<CompiledParameter>.Builder compiledParameters = ImmutableArray.CreateBuilder<CompiledParameter>();

        for (int i = 0; i < lambdaStatement.Parameters.Count; i++)
        {
            if (!CompileType(lambdaStatement.Parameters[i].Type, out GeneralType? parameterType, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(lambdaStatement.Parameters[i].Type));
                return false;
            }

            compiledParameters.Add(new CompiledParameter(parameterType, lambdaStatement.Parameters[i]));
        }

        ImmutableArray<CompiledInstructionLabelDeclaration>.Builder localInstructionLabels = ImmutableArray.CreateBuilder<CompiledInstructionLabelDeclaration>();

        foreach (Statement item in lambdaStatement.Body.GetStatementsRecursively(StatementWalkFlags.IncludeThis | StatementWalkFlags.FrameOnly))
        {
            if (item is InstructionLabel instructionLabel)
            {
                localInstructionLabels.Add(new CompiledInstructionLabelDeclaration()
                {
                    Identifier = instructionLabel.Identifier.Content,
                    Location = instructionLabel.Location,
                });
            }
        }

        using (StackAuto<CompiledFrame> frame = Frames.PushAuto(new CompiledFrame()
        {
            TypeArguments = ImmutableDictionary<string, GeneralType>.Empty,
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
            else if (_body is CompiledStatementWithValue _compiledStatementWithValue)
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
            foreach (CompiledVariableDeclaration item in frame.Value.CapturedVariables)
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

            CompiledStatementWithValue? allocator = null;
            if (!closure.IsEmpty)
            {
                compiledParameters.Insert(0, new CompiledParameter(PointerType.Any, new ParameterDefinition(
                    ImmutableArray<Token>.Empty,
                    null,
                    Token.CreateAnonymous("closure"),
                    null
                )));

                int closureSize = 0;
                foreach (CapturedLocal? item in closure)
                {
                    closureSize += (item.Variable?.Type ?? item.Parameter?.Type)!.GetSize(this);
                }
                closureSize += PointerSize;
                if (!CompileAllocation(LiteralStatement.CreateAnonymous(closureSize, lambdaStatement.Position, lambdaStatement.File), out allocator)) return false;
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
    bool CompileStatement(Assignment setter, [NotNullWhen(true)] out CompiledStatement? compiledStatement) => CompileSetter(setter.Left, setter.Right, out compiledStatement);
    bool CompileStatement(LiteralStatement literal, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                        OnGotStatementType(literal, expectedType);
                        compiledStatement = new CompiledEvaluatedValue()
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
                        OnGotStatementType(literal, expectedType);
                        compiledStatement = new CompiledEvaluatedValue()
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
                        OnGotStatementType(literal, expectedType);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((float)literal.GetInt()),
                        };
                        return true;
                    }
                }

                if (!GetLiteralType(literal.Type, out GeneralType? literalType))
                { literalType = BuiltinType.I32; }

                OnGotStatementType(literal, literalType);
                compiledStatement = new CompiledEvaluatedValue()
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
                if (!GetLiteralType(literal.Type, out GeneralType? literalType))
                { literalType = BuiltinType.F32; }

                OnGotStatementType(literal, literalType);
                compiledStatement = new CompiledEvaluatedValue()
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
                    OnGotStatementType(literal, expectedType);

                    compiledStatement = null;
                    if (!CompileAllocation(LiteralStatement.CreateAnonymous((1 + literal.Value.Length) * BuiltinType.U8.GetSize(this), literal.Location.Position, literal.Location.File), out CompiledStatementWithValue? allocator)) return false;

                    compiledStatement = new CompiledStringInstance()
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
                    OnGotStatementType(literal, expectedType);

                    compiledStatement = null;
                    if (!CompileAllocation(LiteralStatement.CreateAnonymous((1 + literal.Value.Length) * BuiltinType.Char.GetSize(this), literal.Location.Position, literal.Location.File), out CompiledStatementWithValue? allocator)) return false;

                    compiledStatement = new CompiledStringInstance()
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
                    OnGotStatementType(literal, expectedType);

                    compiledStatement = new CompiledStackStringInstance()
                    {
                        Value = literal.Value,
                        IsASCII = true,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        IsNullTerminated = arrayType3.ComputedLength.HasValue && arrayType3.ComputedLength.Value > literal.Value.Length,
                    };
                    return true;
                }
                else if (expectedType is not null &&
                    expectedType.Is(out ArrayType? arrayType4) &&
                    arrayType4.Of.SameAs(BasicType.U16))
                {
                    OnGotStatementType(literal, expectedType);

                    compiledStatement = new CompiledStackStringInstance()
                    {
                        Value = literal.Value,
                        IsASCII = false,
                        Location = literal.Location,
                        SaveValue = true,
                        Type = expectedType,
                        IsNullTerminated = arrayType4.ComputedLength.HasValue && arrayType4.ComputedLength.Value > literal.Value.Length,
                    };
                    return true;
                }
                else
                {
                    BuiltinType type = BuiltinType.Char;

                    compiledStatement = null;
                    if (!CompileAllocation(LiteralStatement.CreateAnonymous((1 + literal.Value.Length) * type.GetSize(this), literal.Location.Position, literal.Location.File), out CompiledStatementWithValue? allocator)) return false;

                    if (!GetLiteralType(literal.Type, out GeneralType? stringType))
                    {
                        stringType = new PointerType(new ArrayType(BuiltinType.Char, new CompiledEvaluatedValue()
                        {
                            Value = literal.Value.Length + 1,
                            Location = literal.Location,
                            Type = ArrayLengthType,
                            SaveValue = true
                        }));
                    }

                    OnGotStatementType(literal, stringType);

                    compiledStatement = new CompiledStringInstance()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                        OnGotStatementType(literal, expectedType);
                        compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                            OnGotStatementType(literal, expectedType);
                            compiledStatement = new CompiledEvaluatedValue()
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
                        OnGotStatementType(literal, expectedType);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = expectedType,
                            Value = new CompiledValue((float)literal.Value[0]),
                        };
                        return true;
                    }
                }

                if (!GetLiteralType(literal.Type, out GeneralType? literalType))
                { literalType = BuiltinType.Char; }

                OnGotStatementType(literal, literalType);
                compiledStatement = new CompiledEvaluatedValue()
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
    bool CompileStringLiteral(string literal, Location location, bool withBytes, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        BuiltinType type = withBytes ? BuiltinType.U8 : BuiltinType.Char;

        if (!CompileAllocation(LiteralStatement.CreateAnonymous((1 + literal.Length) * type.GetSize(this), location.Position, location.File), out CompiledStatementWithValue? allocator)) return false;

        compiledStatement = new CompiledStringInstance()
        {
            Value = literal,
            IsASCII = withBytes,
            Location = location,
            SaveValue = true,
            Type = new PointerType(new ArrayType(type, new CompiledEvaluatedValue()
            {
                Value = literal.Length + 1,
                Location = location,
                Type = ArrayLengthType,
                SaveValue = true,
            })),
            Allocator = allocator,
        };
        return true;
    }
    bool CompileStatement(Identifier variable, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        compiledStatement = null;

        if (variable.Content.StartsWith('#'))
        {
            compiledStatement = new CompiledEvaluatedValue()
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
            compiledStatement = new CompilerVariableGetter()
            {
                Identifier = variable.Content[1..],
                Type = BooleanType,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (RegisterKeywords.TryGetValue(variable.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            compiledStatement = new RegisterGetter()
            {
                Register = registerKeyword.Register,
                Type = registerKeyword.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetConstant(variable.Content, variable.File, out CompiledVariableConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            OnGotStatementType(variable, constant.Type);
            variable.PredictedValue = constant.Value;
            variable.Reference = constant;
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

            compiledStatement = new CompiledEvaluatedValue()
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
            variable.Reference = param;
            OnGotStatementType(variable, param.Type);

            compiledStatement = new CompiledParameterGetter()
            {
                Variable = param,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
                Type = param.Type,
            };
            return true;
        }

        if (GetVariable(variable.Content, out CompiledVariableDeclaration? val, out PossibleDiagnostic? variableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = val;
            OnGotStatementType(variable, val.Type);

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

                compiledStatement = new CompiledFieldGetter()
                {
                    Field = field,
                    Object = new CompiledParameterGetter()
                    {
                        Variable = thisParameter,
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

            compiledStatement = new CompiledVariableGetter()
            {
                Variable = val,
                Type = val.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetGlobalVariable(variable.Content, variable.File, out CompiledVariableDeclaration? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            variable.AnalyzedType = TokenAnalyzedType.VariableName;
            variable.Reference = globalVariable;
            OnGotStatementType(variable, globalVariable.Type);
            Frames.Last.CapturesGlobalVariables = true;

            if (!globalVariable.IsGlobal)
            { Diagnostics.Add(Diagnostic.Internal($"Trying to get global variable \"{globalVariable.Identifier}\" but it was compiled as a local variable.", variable)); }

            compiledStatement = new CompiledVariableGetter()
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

            compiledFunction.References.AddReference(variable, variable.File);
            variable.AnalyzedType = TokenAnalyzedType.FunctionName;
            variable.Reference = compiledFunction;
            OnGotStatementType(variable, new FunctionType(compiledFunction));

            compiledStatement = new FunctionAddressGetter()
            {
                Function = compiledFunction,
                Type = new FunctionType(compiledFunction),
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetInstructionLabel(variable.Content, out CompiledInstructionLabelDeclaration? instructionLabel, out PossibleDiagnostic? instructionLabelError))
        {
            variable.Reference = instructionLabel;
            variable.AnalyzedType = TokenAnalyzedType.InstructionLabel;
            OnGotStatementType(variable, CompiledInstructionLabelDeclaration.Type);

            compiledStatement = new InstructionLabelAddressGetter()
            {
                InstructionLabel = instructionLabel,
                Type = CompiledInstructionLabelDeclaration.Type,
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
                variable.Reference = outerParameter;
                OnGotStatementType(variable, outerParameter.Type);

                compiledStatement = new CompiledParameterGetter()
                {
                    Variable = outerParameter,
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

            if (GetVariable(variable.Content, Frames[i], out CompiledVariableDeclaration? outerLocal, out _))
            {
                variable.AnalyzedType = TokenAnalyzedType.VariableName;
                variable.Reference = outerLocal;
                OnGotStatementType(variable, outerLocal.Type);

                if (outerLocal.IsGlobal)
                { Diagnostics.Add(Diagnostic.Internal($"Trying to get local variable \"{outerLocal.Identifier}\" but it was compiled as a global variable.", variable)); }

                if (Frames.Last.CompiledGeneratorContext is not null)
                {
                    Diagnostics.Add(Diagnostic.Internal($"aaaaaaa", variable));
                    return false;
                }

                compiledStatement = new CompiledVariableGetter()
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
    bool CompileStatement(AddressGetter addressGetter, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(addressGetter.PrevStatement, out CompiledStatementWithValue? of)) return false;

        compiledStatement = new CompiledAddressGetter()
        {
            Of = of,
            Type = new PointerType(of.Type),
            Location = addressGetter.Location,
            SaveValue = addressGetter.SaveValue,
        };
        return true;
    }
    bool CompileStatement(Pointer pointer, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(pointer.PrevStatement, out CompiledStatementWithValue? to)) return false;

        GeneralType addressType = to.Type;
        if (!addressType.Is(out PointerType? pointerType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a pointer", pointer.PrevStatement));
            return false;
        }

        compiledStatement = new CompiledPointer()
        {
            To = to,
            Type = pointerType.To,
            Location = pointer.Location,
            SaveValue = pointer.SaveValue,
        };
        return true;
    }
    bool CompileStatement(WhileLoop whileLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        Block block = Block.CreateIfNotBlock(whileLoop.Block);

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

        CompiledStatementWithValue? condition;
        CompiledStatement? body;

        using (Frames.Last.Scopes.PushAuto(CompileScope(block.Statements)))
        {
            if (!CompileStatement(whileLoop.Condition, out condition)) return false;
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
    bool CompileStatement(ForLoop forLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        CompiledStatement? variableDeclaration = null;
        CompiledStatementWithValue? condition;
        CompiledStatement? body;
        CompiledStatement? expression;

        using (Frames.Last.Scopes.PushAuto(CompileScope(forLoop.VariableDeclaration is null ? Enumerable.Empty<Statement>() : Enumerable.Repeat(forLoop.VariableDeclaration, 1))))
        {
            if (forLoop.VariableDeclaration is not null &&
                !CompileStatement(forLoop.VariableDeclaration, out variableDeclaration))
            { return false; }

            /*
            if (AllowEvaluating &&
                TryCompute(forLoop.Condition, out CompiledValue condition))
            {
                if (condition)
                {
                    Diagnostics.Add(Diagnostic.OptimizationNotice($"For-loop condition evaluated as true", forLoop.Condition));

                    CompileStatement(forLoop.Block);

                    AddComment("For-loop expression");
                    CompileStatement(forLoop.Expression);

                    AddComment("Jump back");
                    AddInstruction(Opcode.Jump, beginOffset - GeneratedCode.Count);

                    FinishJumpInstructions(BreakInstructions.Pop());

                    OnScopeExit(forLoop.Position.After(), forLoop.File);

                    AddComment("}");
                }
                else
                {
                    Diagnostics.Add(Diagnostic.OptimizationNotice($"For-loop fully trimmed", forLoop));

                    OnScopeExit(forLoop.Position.After(), forLoop.File);

                    AddComment("}");
                }
                return;
            }
            */

            if (!CompileStatement(forLoop.Condition, out condition)) return false;

            if (!CompileStatement(forLoop.Block, out body)) return false;
            if (!CompileStatement(forLoop.Expression, out expression)) return false;
        }

        compiledStatement = new CompiledForLoop()
        {
            VariableDeclaration = variableDeclaration,
            Condition = condition,
            Expression = expression,
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

        if (!CompileStatement(@if.Condition, out CompiledStatementWithValue? condition))
        {
            if (@if.NextLink is not null) CompileStatement(@if.NextLink, out _);
            CompileStatement(@if.Block, out _);
            return false;
        }

        if (condition is CompiledEvaluatedValue evaluatedCondition && Settings.Optimizations.HasFlag(OptimizationSettings.TrimUnreachable))
        {
            if (evaluatedCondition.Value)
            {
                if (@if.NextLink is null || !@if.NextLink.GetStatementsRecursively(StatementWalkFlags.FrameOnly).OfType<InstructionLabel>().Any())
                {
                    return CompileStatement(@if.Block, out compiledStatement);
                }
            }
            else
            {
                if (!@if.GetStatementsRecursively(StatementWalkFlags.FrameOnly).OfType<InstructionLabel>().Any())
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
                        compiledStatement = new EmptyStatement()
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
            CompileStatement(@if.Block, out _);
            return false;
        }
        if (!CompileStatement(@if.Block, out CompiledStatement? body)) return false;

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

        if (!CompileStatement(@if.Block, out CompiledStatement? body)) return false;

        compiledStatement = new CompiledElse()
        {
            Body = body,
            Location = @if.Location,
        };
        return true;
    }
    bool CompileStatement(NewInstance newInstance, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        if (!CompileType(newInstance.Type, out GeneralType? instanceType, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(newInstance.Type));
            return false;
        }

        newInstance.Type.SetAnalyzedType(instanceType);
        OnGotStatementType(newInstance, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                if (!CompileAllocation(new AnyCall(
                    new Identifier(Token.CreateAnonymous("sizeof"), newInstance.File),
                    ImmutableArray.Create<StatementWithValue>(new CompiledTypeStatement(Token.CreateAnonymous(StatementKeywords.Type), pointerType.To, newInstance.File)),
                    ImmutableArray<Token>.Empty,
                    TokenPair.CreateAnonymous(newInstance.Position, "(", ")"),
                    newInstance.File
                ), out compiledStatement))
                { return false; }

                compiledStatement = new CompiledFakeTypeCast()
                {
                    Value = compiledStatement,
                    Type = instanceType,
                    Location = compiledStatement.Location,
                    SaveValue = compiledStatement.SaveValue,
                };
                return true;
            }

            case StructType structType:
            {
                structType.Struct.References.AddReference(newInstance.Type, newInstance.File);
                compiledStatement = new CompiledStackAllocation()
                {
                    Type = structType,
                    Location = newInstance.Location,
                    SaveValue = newInstance.SaveValue,
                };
                return true;
            }

            case ArrayType arrayType:
            {
                compiledStatement = new CompiledStackAllocation()
                {
                    Type = arrayType,
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
    bool CompileStatement(ConstructorCall constructorCall, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        if (!CompileType(constructorCall.Type, out GeneralType? instanceType, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(constructorCall.Type));
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

        CompiledConstructorDefinition? compiledFunction = result.Function;

        compiledFunction.References.AddReference(constructorCall);
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(constructorCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Constructor \"{compiledFunction.ToReadable()}\" could not be called due to its protection level", constructorCall.Type, constructorCall.File));
            return false;
        }

        ImmutableArray<StatementWithValue> arguments = constructorCall.Arguments;
        result.ReplaceArgumentsIfNeeded(ref arguments);

        if (!CompileStatement(constructorCall.ToInstantiation(), out CompiledStatementWithValue? _object)) return false;
        if (!CompileArguments(arguments, compiledFunction, out ImmutableArray<CompiledPassedArgument> compiledArguments, 1)) return false;

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
    bool CompileStatement(Field field, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        if (!CompileStatement(field.PrevStatement, out CompiledStatementWithValue? prev)) return false;

        if (prev.Type.Is(out ArrayType? arrayType) && field.Identifier.Content == "Length")
        {
            if (!arrayType.ComputedLength.HasValue)
            {
                Diagnostics.Add(Diagnostic.Critical("I will eventually implement this", field));
                return false;
            }

            OnGotStatementType(field, ArrayLengthType);
            field.PredictedValue = arrayType.ComputedLength.Value;

            compiledStatement = new CompiledEvaluatedValue()
            {
                Value = arrayType.ComputedLength.Value,
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
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", field.PrevStatement));
                return false;
            }

            if (!structPointerType.GetField(field.Identifier.Content, this, out CompiledField? fieldDefinition, out _, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(field.Identifier, field.File));
                return false;
            }

            field.CompiledType = fieldDefinition.Type;
            field.Reference = fieldDefinition;

            compiledStatement = new CompiledFieldGetter()
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

        if (!structType.GetField(field.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field.Identifier, field.File));
            return false;
        }

        field.CompiledType = compiledField.Type;
        field.Reference = compiledField;

        compiledStatement = new CompiledFieldGetter()
        {
            Field = compiledField,
            Object = prev,
            Location = field.Location,
            SaveValue = field.SaveValue,
            Type = GeneralType.InsertTypeParameters(compiledField.Type, structType.TypeArguments) ?? compiledField.Type,
        };
        return true;
    }
    bool CompileStatement(IndexCall index, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(index.PrevStatement, out CompiledStatementWithValue? baseStatement)) return false;
        if (!CompileStatement(index.Index, out CompiledStatementWithValue? indexStatement)) return false;

        if (GetIndexGetter(baseStatement.Type, indexStatement.Type, index.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(index, index.File));
            return CompileFunctionCall(index, ImmutableArray.Create(index.PrevStatement, index.Index), indexer, out compiledStatement);
        }

        if (baseStatement.Type.Is(out ArrayType? arrayType))
        {
            if (TryCompute(indexStatement, out CompiledValue computedIndexData))
            {
                index.Index.PredictedValue = computedIndexData;

                if (computedIndexData < 0 || (arrayType.ComputedLength.HasValue && computedIndexData >= arrayType.ComputedLength.Value))
                { Diagnostics.Add(Diagnostic.Warning($"Index out of range", index.Index)); }
            }

            compiledStatement = new CompiledIndexGetter()
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
            compiledStatement = new CompiledIndexGetter()
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
    bool CompileStatement(ModifiedStatement modifiedStatement, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Content == ModifierKeywords.Ref)
        {
            throw new NotImplementedException();
        }

        if (modifier.Content == ModifierKeywords.Temp)
        {
            return CompileStatement(statement, out compiledStatement);
        }

        throw new NotImplementedException();
    }
    bool CompileStatement(LiteralList listValue, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;
        GeneralType? itemType = (expectedType as ArrayType)?.Of;

        ImmutableArray<CompiledStatementWithValue>.Builder result = ImmutableArray.CreateBuilder<CompiledStatementWithValue>(listValue.Values.Length);
        for (int i = 0; i < listValue.Values.Length; i++)
        {
            if (!CompileStatement(listValue.Values[i], out CompiledStatementWithValue? item, itemType)) return false;

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

        ArrayType type = new(itemType, new CompiledEvaluatedValue()
        {
            Value = listValue.Values.Length,
            Location = listValue.Location,
            Type = ArrayLengthType,
            SaveValue = true,
        });

        compiledStatement = new CompiledLiteralList()
        {
            Values = result.ToImmutable(),
            Type = type,
            Location = listValue.Location,
            SaveValue = listValue.SaveValue,
        };
        return true;
    }
    bool CompileStatement(BasicTypeCast typeCast, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileType(typeCast.Type, out GeneralType? targetType, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(typeCast.Type));
            return false;
        }

        if (!CompileStatement(typeCast.PrevStatement, out CompiledStatementWithValue? prev)) return false;

        GeneralType statementType = prev.Type;

        if (statementType.Equals(targetType))
        {
            // Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Keyword, typeCast.File));
            compiledStatement = prev;
            return true;
        }

        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        compiledStatement = new CompiledFakeTypeCast()
        {
            Value = prev,
            Type = targetType,
            Location = typeCast.Location,
            SaveValue = typeCast.SaveValue,
        };
        return true;
    }
    bool CompileStatement(ManagedTypeCast typeCast, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        if (!CompileType(typeCast.Type, out GeneralType? targetType, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(typeCast.Type));
            return false;
        }
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (!CompileStatement(typeCast.PrevStatement, out CompiledStatementWithValue? prev, targetType)) return false;

        if (prev.Type.Equals(targetType))
        {
            // Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Type, typeCast.File));
            compiledStatement = prev;
            return true;
        }

        if (Settings.Optimizations.HasFlag(OptimizationSettings.StatementEvaluating) &&
            targetType.Is(out BuiltinType? targetBuiltinType) &&
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            prevValue.TryCast(targetBuiltinType.RuntimeType, out CompiledValue castedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Type cast evaluated, converting {prevValue} ({prevValue.Type}) to {castedValue} ({castedValue.Type})", typeCast));
            compiledStatement = new CompiledEvaluatedValue()
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
            compiledStatement = new CompiledTypeCast()
            {
                Value = prev,
                Type = targetType,
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
            compiledStatement = new CompiledTypeCast()
            {
                Value = prev,
                Type = targetType,
                Allocator = null,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }
        //fixme
        compiledStatement = new CompiledTypeCast()
        {
            Value = prev,
            Type = targetType,
            Allocator = null,
            Location = typeCast.Location,
            SaveValue = typeCast.SaveValue,
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
                if (item is EmptyStatement) continue;
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
            case StatementWithValue v:
                if (CompileStatement(v, out CompiledStatementWithValue? compiledStatementWithValue))
                {
                    compiledStatement = compiledStatementWithValue;
                    return true;
                }
                else
                {
                    compiledStatement = compiledStatementWithValue;
                    return false;
                }
            case VariableDeclaration v: return CompileStatement(v, out compiledStatement);
            case KeywordCall v: return CompileStatement(v, out compiledStatement);
            case AnyAssignment v: return CompileStatement(v.ToAssignment(), out compiledStatement);
            case WhileLoop v: return CompileStatement(v, out compiledStatement);
            case ForLoop v: return CompileStatement(v, out compiledStatement);
            case IfContainer v: return CompileStatement(v, out compiledStatement);
            case LinkedIf v: return CompileStatement(v, out compiledStatement);
            case LinkedElse v: return CompileStatement(v, out compiledStatement);
            case Block v: return CompileStatement(v, out compiledStatement);
            case InstructionLabel v: return CompileStatement(v, out compiledStatement);
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }
    }
    bool CompileStatement(StatementWithValue statement, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true) => statement switch
    {
        LiteralList v => CompileStatement(v, out compiledStatement, expectedType),
        BinaryOperatorCall v => CompileStatement(v, out compiledStatement, expectedType),
        UnaryOperatorCall v => CompileStatement(v, out compiledStatement),
        LiteralStatement v => CompileStatement(v, out compiledStatement, expectedType),
        Identifier v => CompileStatement(v, out compiledStatement, expectedType, resolveReference),
        AddressGetter v => CompileStatement(v, out compiledStatement),
        Pointer v => CompileStatement(v, out compiledStatement),
        NewInstance v => CompileStatement(v, out compiledStatement),
        ConstructorCall v => CompileStatement(v, out compiledStatement),
        IndexCall v => CompileStatement(v, out compiledStatement),
        Field v => CompileStatement(v, out compiledStatement),
        BasicTypeCast v => CompileStatement(v, out compiledStatement),
        ManagedTypeCast v => CompileStatement(v, out compiledStatement),
        ModifiedStatement v => CompileStatement(v, out compiledStatement),
        AnyCall v => CompileStatement(v, out compiledStatement, expectedType),
        LambdaStatement v => CompileStatement(v, out compiledStatement, expectedType),
        _ => throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\""),
    };

    bool CompileSetter(Statement statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        switch (statementToSet)
        {
            case Identifier v: return CompileSetter(v, value, out compiledStatement);
            case Field v: return CompileSetter(v, value, out compiledStatement);
            case IndexCall v: return CompileSetter(v, value, out compiledStatement);
            case Pointer v: return CompileSetter(v, value, out compiledStatement);
            default:
                Diagnostics.Add(Diagnostic.Critical($"The left side of the assignment operator should be a variable, field or memory address. Passed \"{statementToSet.GetType().Name}\"", statementToSet));
                return false;
        }
    }
    bool CompileSetter(Identifier statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (RegisterKeywords.TryGetValue(statementToSet.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            if (!CompileStatement(value, out CompiledStatementWithValue? _value, registerKeyword.Type)) return false;

            GeneralType valueType = _value.Type;

            if (!CanCastImplicitly(valueType, registerKeyword.Type, value, out PossibleDiagnostic? castError))
            { Diagnostics.Add(castError.ToError(value)); }

            compiledStatement = new RegisterSetter()
            {
                Register = registerKeyword.Register,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is RegisterGetter _v2 &&
                    _v2.Register == registerKeyword.Register,
            };
            return true;
        }

        if (GetConstant(statementToSet.Content, statementToSet.File, out CompiledVariableConstant? constant, out _))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.ConstantName;
            statementToSet.Reference = constant;

            Diagnostics.Add(Diagnostic.Critical($"Can not set constant value: it is readonly", statementToSet));
            return false;
        }

        if (GetParameter(statementToSet.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (statementToSet.Content != StatementKeywords.This)
            { statementToSet.AnalyzedType = TokenAnalyzedType.ParameterName; }
            statementToSet.CompiledType = parameter.Type;
            statementToSet.Reference = parameter;

            if (!CompileStatement(value, out CompiledStatementWithValue? _value, parameter.Type)) return false;

            GeneralType valueType = _value.Type;

            if (!CanCastImplicitly(valueType, parameter.Type, value, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(value));
            }

            compiledStatement = new CompiledParameterSetter()
            {
                Variable = parameter,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledParameterGetter _v2 &&
                    _v2.Variable == parameter,
            };
            return true;
        }

        if (GetVariable(statementToSet.Content, out CompiledVariableDeclaration? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = variable.Type;
            statementToSet.Reference = variable;

            if (!CompileStatement(value, out CompiledStatementWithValue? _value, variable.Type)) return false;

            if (variable.IsGlobal)
            { Diagnostics.Add(Diagnostic.Internal($"Trying to set local variable \"{variable.Identifier}\" but it was compiled as a global variable.", statementToSet)); }

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

                compiledStatement = new CompiledFieldSetter()
                {
                    Field = field,
                    Object = new CompiledParameterGetter()
                    {
                        Variable = thisParameter,
                        Type = thisParameter.Type,
                        SaveValue = true,
                        Location = variable.Location,
                    },
                    Value = _value,
                    Location = statementToSet.Location.Union(value.Location),
                    Type = field.Type,
                    IsCompoundAssignment =
                        _value is CompiledBinaryOperatorCall _v3 &&
                        _v3.Left is CompiledVariableGetter _v4 &&
                        _v4.Variable == variable,
                };
                return true;
            }

            compiledStatement = new CompiledVariableSetter()
            {
                Variable = variable,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledVariableGetter _v2 &&
                    _v2.Variable == variable,
            };
            return true;
        }

        if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariableDeclaration? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;
            Frames.Last.CapturesGlobalVariables = true;

            if (!CompileStatement(value, out CompiledStatementWithValue? _value, globalVariable.Type)) return false;

            if (!globalVariable.IsGlobal)
            { Diagnostics.Add(Diagnostic.Internal($"Trying to set global variable \"{globalVariable.Identifier}\" but it was compiled as a local variable.", statementToSet)); }

            compiledStatement = new CompiledVariableSetter()
            {
                Variable = globalVariable,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledVariableGetter _v2 &&
                    _v2.Variable == globalVariable,
            };
            return true;
        }

        Diagnostics.Add(Diagnostic.Critical($"Symbol \"{statementToSet.Content}\" not found", statementToSet)
            .WithSuberrors(
                parameterNotFoundError.ToError(statementToSet),
                variableNotFoundError.ToError(statementToSet),
                globalVariableNotFoundError.ToError(statementToSet)
            ));
        return false;
    }
    bool CompileSetter(Field statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        if (!CompileStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? prev)) return false;
        GeneralType prevType = prev.Type;

        if (prevType.Is<ArrayType>() && statementToSet.Identifier.Content == "Length")
        {
            Diagnostics.Add(Diagnostic.Critical("Array type's length is readonly", statementToSet));
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
                Diagnostics.Add(Diagnostic.Critical($"Could not get the field offsets of type \"{prevType}\"", statementToSet.PrevStatement));
                return false;
            }

            if (!structPointerType.GetField(statementToSet.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(statementToSet.Identifier, statementToSet.File));
                return false;
            }

            GeneralType type = GeneralType.InsertTypeParameters(compiledField.Type, structPointerType.TypeArguments);
            if (!CompileStatement(value, out CompiledStatementWithValue? _value, type)) return false;

            if (!CanCastImplicitly(_value.Type, type, value, out PossibleDiagnostic? castError2))
            {
                Diagnostics.Add(castError2.ToError(value));
            }

            statementToSet.CompiledType = compiledField.Type;
            statementToSet.Reference = compiledField;

            compiledStatement = new CompiledFieldSetter()
            {
                Object = prev,
                Field = compiledField,
                Location = statementToSet.Location,
                Value = _value,
                Type = type,
                IsCompoundAssignment = false,
            };
            return true;
        }

        if (prevType.Is(out StructType? structType))
        {
            if (!structType.GetField(statementToSet.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error2))
            {
                Diagnostics.Add(error2.ToError(statementToSet.Identifier, statementToSet.File));
                return false;
            }

            GeneralType type = GeneralType.InsertTypeParameters(compiledField.Type, structType.TypeArguments) ?? compiledField.Type;
            if (!CompileStatement(value, out CompiledStatementWithValue? _value, type)) return false;

            if (!CanCastImplicitly(_value.Type, type, value, out PossibleDiagnostic? castError2))
            {
                Diagnostics.Add(castError2.ToError(value));
            }

            statementToSet.CompiledType = compiledField.Type;
            statementToSet.Reference = compiledField;

            compiledStatement = new CompiledFieldSetter()
            {
                Field = compiledField,
                Object = prev,
                Location = statementToSet.Location,
                Value = _value,
                Type = type,
                IsCompoundAssignment = false,
            };
            return true;
        }

        throw new NotImplementedException();
    }
    bool CompileSetter(IndexCall statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? _base)) return false;
        if (!CompileStatement(statementToSet.Index, out CompiledStatementWithValue? _index)) return false;

        if (!CompileStatement(value, out CompiledStatementWithValue? _value)) return false;

        if (GetIndexSetter(_base.Type, _value.Type, _index.Type, statementToSet.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? indexerNotFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(statementToSet, statementToSet.File));
            if (CompileFunctionCall(statementToSet, ImmutableArray.Create<StatementWithValue>(statementToSet.PrevStatement, statementToSet.Index, value), indexer, out CompiledStatementWithValue? compiledStatement2))
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
            itemType = OnGotStatementType(statementToSet, arrayType.Of);
        }
        else if (_base.Type.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            itemType = arrayType.Of;
        }
        else
        {
            Diagnostics.Add(indexerNotFoundError.ToError(statementToSet));
            return false;
        }

        if (!CanCastImplicitly(_value.Type, itemType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        compiledStatement = new CompiledIndexSetter()
        {
            Base = _base,
            Index = _index,
            Value = _value,
            Location = statementToSet.Location.Union(value.Location),
            IsCompoundAssignment = false,
        };
        return true;
    }
    bool CompileSetter(Pointer statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!CompileStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? prev)) return false;

        GeneralType targetType;
        if (prev.Type.Is(out PointerType? pointerType))
        { targetType = OnGotStatementType(statementToSet, pointerType.To); }
        else
        { targetType = OnGotStatementType(statementToSet, BuiltinType.Any); }

        if (!CompileStatement(value, out CompiledStatementWithValue? _value, targetType)) return false;

        if (!CanCastImplicitly(_value.Type, targetType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (prev.Type.GetBitWidth(this, Diagnostics, statementToSet.PrevStatement) != PointerBitWidth)
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{prev.Type}\" cant be a pointer", statementToSet.PrevStatement));
            return false;
        }

        compiledStatement = new CompiledIndirectSetter()
        {
            AddressValue = prev,
            Value = _value,
            Location = statementToSet.Location.Union(value.Location),
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
            if (item is VariableDeclaration variableDeclaration)
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

    #region CompileType

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
            case RuntimeType.Null: result = result = null; error = new($"Invalid type"); return false;
            default: throw new UnreachableException();
        }
    }

    public bool CompileType(
        TypeInstance type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
        {
            TypeInstanceSimple simpleType => CompileType(simpleType, out result, out error),
            TypeInstanceFunction functionType => CompileType(functionType, out result, out error),
            TypeInstanceStackArray stackArrayType => CompileType(stackArrayType, out result, out error),
            TypeInstancePointer pointerType => CompileType(pointerType, out result, out error),
            _ => throw new UnreachableException(),
        };

    public bool CompileType(
        TypeInstanceStackArray type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = null;

        if (!CompileType(type.StackArrayOf, out GeneralType? of, out error)) return false;

        if (type.StackArraySize is not null)
        {
            CompileStatement(type.StackArraySize, out CompiledStatementWithValue? stackArraySizeStatement);
            result = new ArrayType(of, stackArraySizeStatement);
            type.SetAnalyzedType(result);
            return true;
        }
        else
        {
            result = new ArrayType(of, null);
            type.SetAnalyzedType(result);
            return true;
        }
    }

    public bool CompileType(
        TypeInstanceFunction type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = null;

        if (!CompileType(type.FunctionReturnType, out GeneralType? returnType, out error)) return false;
        if (!CompileTypes(type.FunctionParameterTypes, out ImmutableArray<GeneralType> parameters, out error)) return false;

        result = new FunctionType(returnType, parameters, type.ClosureModifier is not null);
        type.SetAnalyzedType(result);

        return true;
    }

    public bool CompileType(
        TypeInstancePointer type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = null;

        if (!CompileType(type.To, out GeneralType? to, out error)) return false;

        result = new PointerType(to);
        type.SetAnalyzedType(result);

        return true;
    }

    public bool CompileType(
        TypeInstanceSimple type,
        [NotNullWhen(true)] out GeneralType? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            type.SetAnalyzedType(result);
            return true;
        }

        if (!FindType(type.Identifier, type.File, out result))
        {
            error = new($"Can't parse \"{type}\" to \"{nameof(GeneralType)}\"", type);
            return false;
        }

        if (result.Is(out StructType? resultStructType) &&
            resultStructType.Struct.Template is not null)
        {
            if (type.TypeArguments.HasValue)
            {
                if (!CompileTypes(type.TypeArguments.Value, out ImmutableArray<GeneralType> typeParameters, out error)) return false;
                result = new StructType(resultStructType.Struct, type.File, typeParameters);
            }
            else
            {
                result = new StructType(resultStructType.Struct, type.File);
            }
        }
        else
        {
            if (type.TypeArguments.HasValue)
            {
                error = new($"Asd", type);
                return false;
            }
        }

        type.SetAnalyzedType(result);
        return true;
    }

    public bool CompileTypes(
        ImmutableArray<TypeInstance> types,
        [NotNullWhen(true)] out ImmutableArray<GeneralType> result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;
        result = default;

        ImmutableArray<GeneralType>.Builder _result = ImmutableArray.CreateBuilder<GeneralType>(types.Length);
        foreach (TypeInstance item in types)
        {
            if (!CompileType(item, out GeneralType? _item, out error)) return false;
            _result.Add(_item);
        }

        result = _result.MoveToImmutable();
        return true;
    }

    #endregion

    #region GenerateCodeFor...

    readonly HashSet<FunctionThingDefinition> _generatedFunctions = new();

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m2 = new("LanguageCore.Compiler.Function");
#endif
    bool CompileFunction<TFunction>(TFunction function, ImmutableDictionary<string, GeneralType>? typeArguments)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition
    {
        if (!_generatedFunctions.Add(function)) return false;

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
            goto end;
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
                Condition = new CompiledFieldGetter()
                {
                    Object = new CompiledParameterGetter()
                    {
                        Variable = thisParmater,
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
                    Value = new CompiledFieldGetter()
                    {
                        Object = new CompiledParameterGetter()
                        {
                            Variable = thisParmater,
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
                Value = new CompiledEvaluatedValue()
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

        ImmutableArray<CompiledInstructionLabelDeclaration>.Builder localInstructionLabels = ImmutableArray.CreateBuilder<CompiledInstructionLabelDeclaration>();

        foreach (Statement item in function.Block.GetStatementsRecursively(StatementWalkFlags.IncludeThis | StatementWalkFlags.FrameOnly))
        {
            if (item is InstructionLabel instructionLabel)
            {
                localInstructionLabels.Add(new CompiledInstructionLabelDeclaration()
                {
                    Identifier = instructionLabel.Identifier.Content,
                    Location = instructionLabel.Location,
                });
            }
        }

        using (StackAuto<CompiledFrame> frame = Frames.PushAuto(new CompiledFrame()
        {
            TypeArguments = typeArguments ?? ImmutableDictionary<string, GeneralType>.Empty,
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
            foreach (CompiledVariableDeclaration item in frame.Value.CapturedVariables)
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
            if (function.IsTemplate) continue;
            if (function.InstructionOffset >= 0) continue;
            if (!Settings.CompileEverything && !function.References.Any())
            {
                if (function is IExposeable exposeable &&
                    exposeable.ExposedFunctionName is not null)
                { }
                else
                {
                    continue;
                }
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
            if (compiledStatement is EmptyStatement) continue;

            ImmutableArray<CompiledStatement> reduced = ReduceStatements(compiledStatement, true);
            res.AddRange(reduced);
        }

        compiledStatements = res.ToImmutable();
        return true;
    }

    #endregion

    void GenerateCode(ImmutableArray<ParsedFile> parsedFiles, Uri file)
    {
        ImmutableArray<CompiledInstructionLabelDeclaration>.Builder globalInstructionLabels = ImmutableArray.CreateBuilder<CompiledInstructionLabelDeclaration>();

        foreach (VariableDeclaration variableDeclaration in TopLevelStatements
            .SelectMany(v => v.Statements)
            .OfType<VariableDeclaration>())
        {
            if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
            {
                if (CompileConstant(variableDeclaration, out CompiledVariableConstant? result))
                {
                    CompiledGlobalConstants.Add(result);
                }
            }
        }

        foreach (InstructionLabel instructionLabel in TopLevelStatements
            .SelectMany(v => v.Statements)
            .SelectMany(v => v.GetStatementsRecursively(StatementWalkFlags.IncludeThis))
            .OfType<InstructionLabel>())
        {
            globalInstructionLabels.Add(new CompiledInstructionLabelDeclaration()
            {
                Identifier = instructionLabel.Identifier.Content,
                Location = instructionLabel.Location,
            });
        }

        using (StackAuto<CompiledFrame> frame = Frames.PushAuto(new CompiledFrame()
        {
            TypeArguments = ImmutableDictionary<string, GeneralType>.Empty,
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
                foreach ((ImmutableArray<Statement> Statements, Uri File) item in TopLevelStatements)
                {
                    if (!CompileTopLevelStatements(item.Statements, out ImmutableArray<CompiledStatement> v)) continue;
                    CompiledTopLevelStatements.AddRange(v);
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

    void AnalyseFunction(CompiledFunction function, HashSet<CompiledFunction> stack)
    {
        if (stack.Contains(function)) return;
        stack.Add(function);
        function.Flags = default;
        AnalyseFunction(function.Body, ref function.Flags, stack);
    }

    void AnalyseFunction(IEnumerable<CompiledStatement> statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        foreach (CompiledStatement item in statement)
        {
            AnalyseFunction(item, ref flags, stack);
        }
    }

    void AnalyseFunction(CompiledStatement statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        switch (statement)
        {
            case CompiledStatementWithValue v: AnalyseFunction(v, ref flags, stack); break;
            case EmptyStatement: break;
            case CompiledBlock v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledIf v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledElse v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledVariableDeclaration v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledCrash v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledDelete v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledReturn v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledBreak v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledGoto v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledInstructionLabelDeclaration v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledWhileLoop v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledForLoop v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledFieldSetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledVariableSetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledIndexSetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledIndirectSetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledParameterSetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledCleanup v: AnalyseFunction(v, ref flags, stack); break;
            default: throw new UnreachableException(statement.GetType().Name);
        }
    }

    void AnalyseFunction(CompiledCleanup statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.Deallocator is not null)
        {
            CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Deallocator);
            if (f is not null)
            {
                AnalyseFunction(f, stack);
                flags |= f.Flags;
            }
        }

        if (statement.Destructor is not null)
        {
            CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Destructor);
            if (f is not null)
            {
                AnalyseFunction(f, stack);
                flags |= f.Flags;
            }
        }
    }

    void AnalyseFunction(CompiledStatementWithValue statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        switch (statement)
        {
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v: AnalyseFunction(v.Statement, ref flags, stack); break;
            case CompiledLiteralList v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledRuntimeCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledFunctionCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledExternalFunctionCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledSizeof v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledPassedArgument v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledBinaryOperatorCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledUnaryOperatorCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledEvaluatedValue v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledAddressGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledPointer v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledStackAllocation v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledHeapAllocation v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledConstructorCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledDesctructorCall v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledTypeCast v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledFakeTypeCast v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledIndexGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledVariableGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledParameterGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledFieldGetter v: AnalyseFunction(v, ref flags, stack); break;
            case RegisterGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledStringInstance v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledStackStringInstance v: AnalyseFunction(v, ref flags, stack); break;
            case FunctionAddressGetter v: AnalyseFunction(v, ref flags, stack); break;
            case InstructionLabelAddressGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompilerVariableGetter v: AnalyseFunction(v, ref flags, stack); break;
            case CompiledLambda v: AnalyseFunction(v, ref flags, stack); break;
            default: throw new UnreachableException();
        }
    }

    void AnalyseFunction(CompiledLambda statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.Allocator is not null) AnalyseFunction(statement.Allocator, ref flags, stack);
        FunctionFlags _flags = default;
        AnalyseFunction(statement.Block, ref _flags, stack);
        statement.Flags = _flags;
        flags |= _flags;
    }

    void AnalyseFunction(GeneralType statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        switch (statement)
        {
            case AliasType v:
                AnalyseFunction(v.Value, ref flags, stack);
                break;
            case ArrayType v:
                AnalyseFunction(v.Of, ref flags, stack);
                if (v.Length is not null) AnalyseFunction(v.Length, ref flags, stack);
                break;
            case BuiltinType v:
                break;
            case FunctionType v:
                AnalyseFunction(v.ReturnType, ref flags, stack);
                foreach (GeneralType i in v.Parameters) AnalyseFunction(i, ref flags, stack);
                break;
            case GenericType v:
                break;
            case PointerType v:
                AnalyseFunction(v.To, ref flags, stack);
                break;
            case StructType v:
                foreach (KeyValuePair<string, GeneralType> i in v.TypeArguments) AnalyseFunction(i.Value, ref flags, stack);
                break;
            default: throw new UnreachableException();
        }
    }

    void AnalyseFunction(CompiledBlock statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Statements, ref flags, stack);
    }
    void AnalyseFunction(CompiledFieldSetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Object, ref flags, stack);
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledVariableSetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.Variable.IsGlobal)
        {
            flags |= FunctionFlags.CapturesGlobalVariables;
        }
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledIndexSetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Base, ref flags, stack);
        AnalyseFunction(statement.Index, ref flags, stack);
        AnalyseFunction(statement.Value, ref flags, stack);

    }
    void AnalyseFunction(CompiledIndirectSetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.AddressValue, ref flags, stack);
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledParameterSetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledIf statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Condition, ref flags, stack);
        AnalyseFunction(statement.Body, ref flags, stack);
        if (statement.Next is not null) AnalyseFunction(statement.Next, ref flags, stack);
    }
    void AnalyseFunction(CompiledElse statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Body, ref flags, stack);
    }
    void AnalyseFunction(CompiledVariableDeclaration statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.InitialValue is not null) AnalyseFunction(statement.InitialValue, ref flags, stack);
        if (statement.Cleanup is not null) AnalyseFunction(statement.Cleanup, ref flags, stack);
    }
    void AnalyseFunction(CompiledCrash statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledDelete statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledReturn statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.Value is not null) AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledBreak statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledGoto statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledInstructionLabelDeclaration statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledWhileLoop statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Condition, ref flags, stack);
        AnalyseFunction(statement.Body, ref flags, stack);
    }
    void AnalyseFunction(CompiledForLoop statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.VariableDeclaration is not null) AnalyseFunction(statement.VariableDeclaration, ref flags, stack);
        AnalyseFunction(statement.Condition, ref flags, stack);
        AnalyseFunction(statement.Expression, ref flags, stack);
        AnalyseFunction(statement.Body, ref flags, stack);
    }
    void AnalyseFunction(CompiledLiteralList statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Values, ref flags, stack);
    }
    void AnalyseFunction(CompiledRuntimeCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Function, ref flags, stack);
        AnalyseFunction(statement.Arguments, ref flags, stack);
    }
    void AnalyseFunction(CompiledFunctionCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Arguments, ref flags, stack);
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Function);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
        }
    }
    void AnalyseFunction(CompiledExternalFunctionCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Arguments, ref flags, stack);
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Declaration);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
        }
    }
    void AnalyseFunction(CompiledSizeof statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Of, ref flags, stack);
    }
    void AnalyseFunction(CompiledPassedArgument statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
        AnalyseFunction(statement.Cleanup, ref flags, stack);
    }
    void AnalyseFunction(CompiledBinaryOperatorCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Left, ref flags, stack);
        AnalyseFunction(statement.Right, ref flags, stack);
    }
    void AnalyseFunction(CompiledUnaryOperatorCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Left, ref flags, stack);
    }
    void AnalyseFunction(CompiledEvaluatedValue statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledAddressGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Of, ref flags, stack);
    }
    void AnalyseFunction(CompiledPointer statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.To, ref flags, stack);
    }
    void AnalyseFunction(CompiledStackAllocation statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledHeapAllocation statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Allocator);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
        }
    }
    void AnalyseFunction(CompiledConstructorCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Object, ref flags, stack);
        AnalyseFunction(statement.Arguments, ref flags, stack);
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Function);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
        }
    }
    void AnalyseFunction(CompiledDesctructorCall statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Function);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
        }
    }
    void AnalyseFunction(CompiledTypeCast statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledFakeTypeCast statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Value, ref flags, stack);
    }
    void AnalyseFunction(CompiledIndexGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Base, ref flags, stack);
        AnalyseFunction(statement.Index, ref flags, stack);
    }
    void AnalyseFunction(CompiledVariableGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        if (statement.Variable.IsGlobal)
        {
            flags |= FunctionFlags.CapturesGlobalVariables;
        }
    }
    void AnalyseFunction(CompiledParameterGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledFieldGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Object, ref flags, stack);
    }
    void AnalyseFunction(RegisterGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompiledStringInstance statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        AnalyseFunction(statement.Allocator, ref flags, stack);
    }
    void AnalyseFunction(CompiledStackStringInstance statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(FunctionAddressGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {
        CompiledFunction? f = GeneratedFunctions.FirstOrDefault(v => v.Function == statement.Function);
        if (f is not null)
        {
            AnalyseFunction(f, stack);
            flags |= f.Flags;
            if (f.Flags.HasFlag(FunctionFlags.CapturesGlobalVariables))
            {
                Diagnostics.Add(Diagnostic.Error($"This is not supported for now", statement));
            }
        }
    }
    void AnalyseFunction(InstructionLabelAddressGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
    void AnalyseFunction(CompilerVariableGetter statement, ref FunctionFlags flags, HashSet<CompiledFunction> stack)
    {

    }
}
