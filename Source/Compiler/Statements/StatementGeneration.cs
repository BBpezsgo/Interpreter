﻿using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using LiteralStatement = LanguageCore.Parser.Statement.Literal;

namespace LanguageCore.Compiler;

public record struct ParameterCleanupItem(int Size, bool CanDeallocate, GeneralType Type, Location Location) { }

public partial class StatementCompiler
{
    bool AllowLoopUnrolling => !Settings.DontOptimize;
    bool AllowFunctionInlining => !Settings.DontOptimize;
    bool AllowPrecomputing => !Settings.DontOptimize;
    bool AllowEvaluating => !Settings.DontOptimize;
    bool AllowOtherOptimizations => !Settings.DontOptimize;
    bool AllowInstructionLevelOptimizations => !Settings.DontOptimize;
    bool AllowTrimming => !Settings.DontOptimize;

    bool GenerateAllocator(StatementWithValue size, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(size);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, parameters, size.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? error, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found: {error}", size));
            return false;
        }

        CompiledFunction? allocator = result.Function;

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

        if (TryEvaluate(allocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
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

        if (allocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(allocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(allocator));
                return false;
            }

            return GenerateCodeForFunctionCall_External(parameters, true, allocator, externalFunction, size.Location, out compiledStatement);
        }

        if (!GenerateCodeForArguments(parameters, allocator, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

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

    bool GenerateDeallocator(StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;
        ImmutableArray<StatementWithValue> parameters = ImmutableArray.Create(value);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameters, value.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical(
                $"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found",
                value,
                notFoundError.ToError(value)));
            return false;
        }
        CompiledFunction? deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(value, value.File));

        if (!deallocator.CanUse(value.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", value));
            return false;
        }

        if (TryEvaluate(deallocator, parameters, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice("Function evaluated", value));
            compiledStatement = new CompiledEvaluatedValue()
            {
                Value = returnValue.Value,
                Type = deallocator.Type,
                Location = value.Location,
                SaveValue = false,
            };
            return true;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(deallocator));
                return false;
            }

            if (GenerateCodeForFunctionCall_External(parameters, true, deallocator, externalFunction, value.Location, out CompiledStatementWithValue? compiledStatement2))
            {
                compiledStatement = compiledStatement2;
                return true;
            }
            else
            {
                return false;
            }
        }

        if (!GenerateCodeForArguments(parameters, deallocator, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

        compiledStatement = new CompiledFunctionCall()
        {
            Function = deallocator,
            Arguments = compiledArguments,
            Location = value.Location,
            SaveValue = false,
            Type = deallocator.Type,
        };
        return true;
    }

    bool GenerateDestructor(StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;
        GeneralType deallocateableType = FindStatementType(value);
        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);
        FunctionQueryResult<CompiledGeneralFunction>? result;

        if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value)) return false;

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, value.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (deallocateablePointerType.To.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning(
                        $"Destructor for type \"{deallocateableType}\" not found",
                        value,
                        error.ToWarning(value)));
                }

                return GenerateDeallocator(value, out compiledStatement);
            }
        }
        else
        {
            if (!GetGeneralFunction(deallocateableType, argumentTypes, BuiltinFunctionIdentifiers.Destructor, value.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                Diagnostics.Add(Diagnostic.Warning(
                    $"Destructor for type \"{deallocateableType}\" not found",
                    value,
                    error.ToWarning(value)));
                return false;
            }
        }

        CompiledGeneralFunction? destructor = result.Function;

        destructor.References.Add(new Reference<Statement?>(null, value.File));

        if (!destructor.CanUse(value.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" function cannot be called due to its protection level", value));
            return false;
        }

        if (deallocateableType.Is<PointerType>())
        {
            GenerateDeallocator(value, out CompiledStatement? _compiledStatement);
        }

        compiledStatement = new CompiledDesctructorCall()
        {
            Function = destructor,
            Value = _value,
            Location = value.Location,
            SaveValue = false,
            Type = destructor.Type,
        };
        return true;
    }

    bool GenerateDeallocator(GeneralType deallocateableType, Location location, [NotNullWhen(true)] out CompiledFunction? deallocator)
    {
        deallocator = null;
        ImmutableArray<GeneralType> parameterTypes = ImmutableArray.Create(deallocateableType);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, location.File, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            Diagnostics.Add(Diagnostic.Critical($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found ({notFoundError.Message})", location));
            return false;
        }

        deallocator = result.Function;

        deallocator.References.Add(new Reference<StatementWithValue?>(null, location.File));

        if (!deallocator.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{deallocator.ToReadable()}\" cannot be called due to its protection level", location));
            return false;
        }

        if (deallocator.IsExternal)
        {
            if (!ExternalFunctions.TryGet(deallocator.ExternalFunctionName, out _, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(deallocator));
                return false;
            }

            throw new NotImplementedException();
        }

        return true;
    }

    bool GenerateDestructor(GeneralType deallocateableType, Location location, [NotNullWhen(true)] out CompiledCleanup? compiledCleanup)
    {
        compiledCleanup = null;

        ImmutableArray<GeneralType> argumentTypes = ImmutableArray.Create<GeneralType>(deallocateableType);
        FunctionQueryResult<CompiledGeneralFunction>? result;

        if (deallocateableType.Is(out PointerType? deallocateablePointerType))
        {
            if (!GetGeneralFunction(deallocateablePointerType.To, argumentTypes, BuiltinFunctionIdentifiers.Destructor, location.File, out result, out PossibleDiagnostic? error, AddCompilable))
            {
                if (deallocateablePointerType.To.Is<StructType>())
                {
                    Diagnostics.Add(Diagnostic.Warning(
                        $"Destructor for type \"{deallocateableType}\" not found",
                        location,
                        error.ToWarning(location)));
                }

                if (!GenerateDeallocator(deallocateableType, location, out CompiledFunction? deallocator)) return false;

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
                Diagnostics.Add(Diagnostic.Warning(
                    $"Destructor for type \"{deallocateableType}\" not found",
                    location,
                    error.ToWarning(location)));
                return false;
            }
        }

        CompiledGeneralFunction? destructor = result.Function;

        destructor.References.Add(new Reference<Statement?>(null, location.File));

        if (!destructor.CanUse(location.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Destructor for type \"{deallocateableType}\" function cannot be called due to its protection level", location));
            return false;
        }

        if (deallocateableType.Is<PointerType>())
        {
            if (!GenerateDeallocator(deallocateableType, location, out CompiledFunction? deallocator)) return false;

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

    #region GenerateCodeForStatement

    bool GenerateCodeForStatement(VariableDeclaration newVariable, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
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

        GeneralType type;
        if (newVariable.Type == StatementKeywords.Var)
        {
            if (newVariable.InitialValue == null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Initial value for variable declaration with implicit type is required", newVariable));
                type = BuiltinType.Void;
            }
            else
            {
                type = FindStatementType(newVariable.InitialValue);
            }
        }
        else
        {
            type = CompileType(newVariable.Type);

            if (type is ArrayType arrayType &&
                newVariable.InitialValue is LiteralList literalList &&
                arrayType.Length is null)
            {
                type = new ArrayType(arrayType.Of, null, literalList.Values.Length);
            }

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        if (!type.AllGenericsDefined())
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\" (what edge case is this???)", newVariable.Type, newVariable.File));
        }

        newVariable.Identifier.AnalyzedType = TokenAnalyzedType.VariableName;

        if (GetConstant(newVariable.Identifier.Content, newVariable.File, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Critical($"Symbol name \"{newVariable.Identifier}\" conflicts with an another symbol name", newVariable.Identifier));
            return false;
        }

        newVariable.Type.SetAnalyzedType(type);

        CompiledStatementWithValue? initialValue = null;

        if (newVariable.InitialValue is not null)
        {
            if (!GenerateCodeForStatement(newVariable.InitialValue, out initialValue, type)) return false;
            if (!CanCastImplicitly(initialValue.Type, type, null, this, out PossibleDiagnostic? castError))
            {
                if (type is ArrayType a1 &&
                    initialValue is CompiledStringInstance stringInstance &&
                    (a1.Of.SameAs(BasicType.U8) || a1.Of.SameAs(BasicType.U16)) &&
                    (!a1.ComputedLength.HasValue ||
                    a1.ComputedLength.Value == stringInstance.Value.Length ||
                    a1.ComputedLength.Value == stringInstance.Value.Length + 1))
                {

                }
                else
                {
                    Diagnostics.Add(castError.ToError(initialValue));
                    return false;
                }
            }
        }

        CompiledCleanup? compiledCleanup = null;
        if (newVariable.Modifiers.Contains(ModifierKeywords.Temp))
        {
            GenerateDestructor(type, newVariable.Location, out compiledCleanup);
        }

        bool isGlobal = Scopes.Count == 0;

        compiledStatement = (isGlobal ? CompiledGlobalVariables : Scopes.Last.Variables).Push(new CompiledVariableDeclaration()
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
        });
        return true;
    }

    bool GenerateCodeForStatement(InstructionLabel instructionLabel, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GetInstructionLabel(instructionLabel.Identifier.Content, out _, out _))
        {
            Diagnostics.Add(Diagnostic.Internal($"Instruction label \"{instructionLabel.Identifier.Content}\" not found. Possibly not compiled or some other internal errors (not your fault)", instructionLabel.Identifier));
            return false;
        }

        compiledStatement = new CompiledInstructionLabelDeclaration()
        {
            Identifier = instructionLabel.Identifier.Content,
            Location = instructionLabel.Location,
        };
        return true;
    }

    bool GenerateCodeForStatement(KeywordCall keywordCall, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            if (keywordCall.Arguments.Length > 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{StatementKeywords.Return}\": required {0} or {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            CompiledStatementWithValue? returnValue = null;

            if (keywordCall.Arguments.Length == 1)
            {
                if (!GenerateCodeForStatement(keywordCall.Arguments[0], out returnValue, CurrentReturnType ?? ExitCodeType)) return false;

                if (!CanCastImplicitly(returnValue.Type, CurrentReturnType ?? ExitCodeType, keywordCall.Arguments[0], out PossibleDiagnostic? castError))
                {
                    Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                }
            }

            compiledStatement = new CompiledReturn()
            {
                Value = returnValue,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Crash)
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{StatementKeywords.Crash}\": required {1} passed {keywordCall.Arguments}", keywordCall));
                return false;
            }

            if (!GenerateCodeForStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? throwValue)) return false;

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
            if (!GenerateCodeForStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? value)) return false;

            GeneralType deletableType = FindStatementType(keywordCall.Arguments[0]);
            if (!GenerateDestructor(deletableType, keywordCall.Arguments[0].Location, out CompiledCleanup? compiledCleanup)) return false;

            compiledStatement = new CompiledDelete()
            {
                Value = value,
                Cleanup = compiledCleanup,
                Location = keywordCall.Location,
            };
            return true;
        }

        if (keywordCall.Identifier.Content == "goto")
        {
            if (keywordCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"{"goto"}\": required {1} passed {keywordCall.Arguments.Length}", keywordCall));
                return false;
            }

            GeneralType argumentType = FindStatementType(keywordCall.Arguments[0]);

            if (!CanCastImplicitly(argumentType, CompiledInstructionLabelDeclaration.Type, null, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(keywordCall.Arguments[0]));
                return false;
            }

            if (!GenerateCodeForStatement(keywordCall.Arguments[0], out CompiledStatementWithValue? to)) return false;

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

    bool GenerateCodeForArguments(IReadOnlyList<StatementWithValue> arguments, ICompiledFunction compiledFunction, [NotNullWhen(true)] out ImmutableArray<CompiledPassedArgument> compiledArguments, int alreadyPassed = 0)
    {
        compiledArguments = ImmutableArray<CompiledPassedArgument>.Empty;

        ImmutableArray<CompiledPassedArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledPassedArgument>(arguments.Count);

        for (int i = 0; i < arguments.Count; i++)
        {
            StatementWithValue argument = arguments[i];
            GeneralType argumentType = FindStatementType(argument);
            ParameterDefinition parameter = compiledFunction.Parameters[i + alreadyPassed];
            GeneralType parameterType = compiledFunction.ParameterTypes[i + alreadyPassed];

            if (argumentType.GetSize(this, Diagnostics, argument) != parameterType.GetSize(this, Diagnostics, parameter))
            { Diagnostics.Add(Diagnostic.Internal($"Bad argument type passed: expected \"{parameterType}\" passed \"{argumentType}\"", argument)); }

            bool typeAllowsTemp = argumentType.Is<PointerType>();

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

            if (!GenerateCodeForStatement(argument, out CompiledStatementWithValue? compiledArgument, parameterType)) return false;

            CompiledCleanup? compiledCleanup = null;
            if (calleeAllowsTemp && callerAllowsTemp && typeAllowsTemp)
            {
                GenerateDestructor(argumentType, argument.Location, out compiledCleanup);
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

    bool GenerateCodeForParameterPassing(IReadOnlyList<StatementWithValue> arguments, FunctionType function, [NotNullWhen(true)] out ImmutableArray<CompiledPassedArgument> compiledArguments)
    {
        compiledArguments = ImmutableArray<CompiledPassedArgument>.Empty;

        ImmutableArray<CompiledPassedArgument>.Builder result = ImmutableArray.CreateBuilder<CompiledPassedArgument>(arguments.Count);

        for (int i = 0; i < arguments.Count; i++)
        {
            StatementWithValue argument = arguments[i];
            GeneralType argumentType = FindStatementType(argument);
            GeneralType parameterType = function.Parameters[i];

            bool canDeallocate = true; // temp type maybe?

            canDeallocate = canDeallocate && argumentType.Is<PointerType>();

            if (StatementCanBeDeallocated(argument, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !canDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", argument)); }
            }
            else
            {
                if (explicitDeallocate)
                { Diagnostics.Add(Diagnostic.Warning($"Can not deallocate this value", argument)); }
                canDeallocate = false;
            }

            if (!GenerateCodeForStatement(argument, out CompiledStatementWithValue? compiledArgument, parameterType)) return false;

            CompiledCleanup? compiledCleanup = null;
            if (canDeallocate)
            {
                GenerateDestructor(argumentType, argument.Location, out compiledCleanup);
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

    bool GenerateCodeForFunctionCall_External<TFunction>(IReadOnlyList<StatementWithValue> parameters, bool saveValue, TFunction compiledFunction, IExternalFunction externalFunction, Location location, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
        where TFunction : FunctionThingDefinition, ICompiledFunction, ISimpleReadable
    {
        compiledStatement = null;
        Compiler.CheckExternalFunctionDeclaration(this, compiledFunction, externalFunction, Diagnostics);

        if (!GenerateCodeForArguments(parameters, compiledFunction, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

        compiledStatement = new CompiledExternalFunctionCall()
        {
            Function = externalFunction,
            Arguments = compiledArguments,
            Type = compiledFunction.Type,
            Location = location,
            SaveValue = saveValue,
            Declaration = compiledFunction,
        };
        return true;
    }

    bool GenerateCodeForFunctionCall<TFunction>(StatementWithValue caller, ImmutableArray<StatementWithValue> arguments, TFunction callee, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
        where TFunction : FunctionThingDefinition, ICompiledFunction, IExportable, ISimpleReadable, ILocated, IExternalFunctionDefinition, IHaveInstructionOffset
    {
        compiledStatement = null;
        OnGotStatementType(caller, callee.Type);

        if (!callee.CanUse(caller.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Function \"{callee.ToReadable()}\" could not be called due to its protection level", caller));
            return false;
        }

        if (arguments.Length != callee.Parameters.Count)
        {
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{callee.ToReadable()}\": required {callee.ParameterCount} passed {arguments.Length}", caller));
            return false;
        }

        if (callee.IsExternal)
        {
            if (!ExternalFunctions.TryGet(callee.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
            {
                Diagnostics.Add(exception.ToError(caller));
                return false;
            }

            return GenerateCodeForFunctionCall_External(arguments, caller.SaveValue, callee, externalFunction, caller.Location, out compiledStatement);
        }

        if (AllowEvaluating &&
            TryEvaluate(callee, arguments, out CompiledValue? returnValue, out RuntimeStatement[]? runtimeStatements) &&
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

        if (AllowFunctionInlining &&
            callee.IsInlineable)
        {
            if (InlineMacro(callee, arguments, out Statement? inlined))
            {
                bool argumentsAreObservable = false;
                foreach (StatementWithValue argument in arguments)
                {
                    if (IsObservable(argument))
                    {
                        argumentsAreObservable = true;
                        Diagnostics.Add(Diagnostic.Warning($"Can't inline \"{callee.ToReadable()}\" because of this argument", argument));
                        break;
                    }
                }

                if (!argumentsAreObservable)
                {
                    ControlFlowUsage controlFlowUsage = inlined is Block _block2 ? FindControlFlowUsage(_block2.Statements) : FindControlFlowUsage(inlined);
                    if (!callee.ReturnSomething &&
                        controlFlowUsage == ControlFlowUsage.None)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", caller));
                        if (GenerateCodeForStatement(inlined, out CompiledStatement? compiledStatement2))
                        {
                            compiledStatement = new CompiledStatementWithValueThatActuallyDoesntHaveValue()
                            {
                                Statement = compiledStatement2,
                                Location = compiledStatement2.Location,
                                SaveValue = false,
                                Type = BuiltinType.Void,
                            };
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (callee.ReturnSomething &&
                             controlFlowUsage == ControlFlowUsage.None &&
                             inlined is StatementWithValue statementWithValue)
                    {
                        Diagnostics.Add(Diagnostic.OptimizationNotice($"Function inlined", caller));
                        GeneralType type = FindStatementType(statementWithValue);
                        if (!CanCastImplicitly(type, callee.Type, statementWithValue, this, out PossibleDiagnostic? castError))
                        { Diagnostics.Add(castError.ToError(statementWithValue)); }
                        if (GenerateCodeForStatement(inlined, out CompiledStatement? compiledStatement2))
                        {
                            compiledStatement = new CompiledStatementWithValueThatActuallyDoesntHaveValue()
                            {
                                Statement = compiledStatement2,
                                Location = compiledStatement2.Location,
                                SaveValue = false,
                                Type = BuiltinType.Void,
                            };
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        Debugger.Break();
                    }
                }
            }
            else
            {
                Diagnostics.Add(Diagnostic.Warning($"Failed to inline \"{callee.ToReadable()}\"", caller));
            }
        }

        if (!GenerateCodeForArguments(arguments, callee, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

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

    bool GenerateCodeForStatement(AnyCall anyCall, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (anyCall.PrevStatement is Identifier _identifier &&
            _identifier.Content == "sizeof")
        {
            _identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (anyCall.Arguments.Length != 1)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to \"sizeof\": required {1} passed {anyCall.Arguments.Length}", anyCall));
                return false;
            }

            StatementWithValue param = anyCall.Arguments[0];
            GeneralType paramType;
            if (param is TypeStatement typeStatement)
            { paramType = CompileType(typeStatement.Type); }
            else if (param is CompiledTypeStatement compiledTypeStatement)
            { paramType = compiledTypeStatement.Type; }
            else
            { paramType = FindStatementType(param); }

            OnGotStatementType(anyCall, BuiltinType.I32);

            compiledStatement = new CompiledSizeof()
            {
                Of = paramType,
                Location = param.Location,
                Type = SizeofStatementType,
                SaveValue = anyCall.SaveValue,
            };
            return true;
        }

        if (GetFunction(anyCall, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? notFound, AddCompilable) &&
            anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            CompiledFunction compiledFunction = result.Function;

            if (anyCall.PrevStatement is Identifier _identifier2)
            { _identifier2.AnalyzedType = TokenAnalyzedType.FunctionName; }
            anyCall.Reference = compiledFunction;
            if (anyCall.PrevStatement is IReferenceableTo<CompiledFunction> _ref1)
            { _ref1.Reference = compiledFunction; }
            compiledFunction.References.Add(new(anyCall, anyCall.File));

            if (functionCall.CompiledType is not null)
            { OnGotStatementType(anyCall, functionCall.CompiledType); }

            return GenerateCodeForFunctionCall(functionCall, functionCall.MethodArguments, compiledFunction, out compiledStatement);
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);
        if (!prevType.Is(out FunctionType? functionType))
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
            Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{functionType}\": required {functionType.Parameters.Length} passed {anyCall.Arguments.Length}", new Position(anyCall.Arguments.As<IPositioned>().Or(anyCall.Brackets)), anyCall.File));
            return false;
        }

        PossibleDiagnostic? argumentError = null;
        if (!Utils.SequenceEquals(anyCall.Arguments, functionType.Parameters, (argument, parameter) =>
        {
            GeneralType argumentType = FindStatementType(argument, parameter);

            if (argument.Equals(parameter))
            { return true; }

            if (StatementCompiler.CanCastImplicitly(argumentType, parameter, null, this, out argumentError))
            { return true; }

            argumentError = argumentError.TrySetLocation(argument);

            return false;
        }))
        {
            if (notFound is not null) Diagnostics.Add(notFound.ToError(anyCall.PrevStatement));
            Diagnostics.Add(Diagnostic.Critical(
                $"Argument types of caller \"{anyCall.ToReadable(FindStatementType)}\" doesn't match with callee \"{functionType}\"",
                anyCall,
                argumentError?.ToError(anyCall)));
            return false;
        }

        if (!GenerateCodeForParameterPassing(anyCall.Arguments, functionType, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;
        if (!GenerateCodeForStatement(anyCall.PrevStatement, out CompiledStatementWithValue? functionValue)) return false;

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
    bool GenerateCodeForStatement(BinaryOperatorCall @operator, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
    {
        compiledStatement = null;

        if (AllowEvaluating &&
            TryCompute(@operator, out CompiledValue predictedValue))
        {
            if (expectedType is not null &&
                predictedValue.TryCast(expectedType, out CompiledValue casted))
            {
                predictedValue = casted;
            }

            Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{predictedValue}\"", @operator));
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            compiledStatement = new CompiledEvaluatedValue()
            {
                Value = predictedValue,
                Type = new BuiltinType(predictedValue.Type),
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
            };
            return true;
        }

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperator>? result, out PossibleDiagnostic? notFoundError))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            return GenerateCodeForFunctionCall(@operator, @operator.Arguments, operatorDefinition, out compiledStatement);
        }
        else if (LanguageOperators.BinaryOperators.Contains(@operator.Operator.Content))
        {
            if (@operator.Operator.Content is
                "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                expectedType = null;
            }

            FindStatementType(@operator);

            StatementWithValue left = @operator.Left;
            StatementWithValue right = @operator.Right;

            GeneralType leftType = FindStatementType(left, expectedType);
            GeneralType rightType = FindStatementType(right, expectedType);

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

            if (!GenerateCodeForStatement(left, out CompiledStatementWithValue? compiledLeft, leftType)) return false;

            if (leftNType != NumericType.Float &&
                rightNType == NumericType.Float)
            {
                compiledLeft = CompiledTypeCast.Wrap(compiledLeft, BuiltinType.F32);
                leftType = BuiltinType.F32;
                leftNType = NumericType.Float;
            }

            if (!GenerateCodeForStatement(right, out CompiledStatementWithValue? compiledRight, rightType)) return false;

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

            compiledStatement = new CompiledBinaryOperatorCall()
            {
                Operator = @operator.Operator.Content,
                Left = compiledLeft,
                Right = compiledRight,
                Type = FindStatementType(@operator),
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
            };
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
    bool GenerateCodeForStatement(UnaryOperatorCall @operator, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (AllowEvaluating &&
            TryCompute(@operator, out CompiledValue predictedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Operator call evaluated with result \"{predictedValue}\"", @operator));
            OnGotStatementType(@operator, new BuiltinType(predictedValue.Type));
            @operator.PredictedValue = predictedValue;

            compiledStatement = new CompiledEvaluatedValue()
            {
                Value = predictedValue,
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
                Type = new BuiltinType(predictedValue.Type),
            };
            return true;
        }

        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperator>? result, out PossibleDiagnostic? operatorNotFoundError))
        {
            CompiledOperator? operatorDefinition = result.Function;

            operatorDefinition.References.Add(new Reference<StatementWithValue>(@operator, @operator.File));
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            @operator.Reference = operatorDefinition;
            OnGotStatementType(@operator, operatorDefinition.Type);

            if (!operatorDefinition.CanUse(@operator.File))
            {
                Diagnostics.Add(Diagnostic.Error($"Operator \"{operatorDefinition.ToReadable()}\" cannot be called due to its protection level", @operator.Operator, @operator.File));
                return false;
            }

            if (UnaryOperatorCall.ParameterCount != operatorDefinition.ParameterCount)
            {
                Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to operator \"{operatorDefinition.ToReadable()}\": required {operatorDefinition.ParameterCount} passed {UnaryOperatorCall.ParameterCount}", @operator));
                return false;
            }

            if (operatorDefinition.IsExternal)
            {
                if (!ExternalFunctions.TryGet(operatorDefinition.ExternalFunctionName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                {
                    Diagnostics.Add(exception.ToError(@operator.Operator, @operator.File));
                    return false;
                }

                return GenerateCodeForFunctionCall_External(@operator.Arguments, @operator.SaveValue, operatorDefinition, externalFunction, @operator.Location, out compiledStatement);
            }

            if (!GenerateCodeForArguments(@operator.Arguments, operatorDefinition, out ImmutableArray<CompiledPassedArgument> compiledArguments)) return false;

            compiledStatement = new CompiledFunctionCall()
            {
                Function = operatorDefinition,
                Arguments = compiledArguments,
                Location = @operator.Location,
                SaveValue = @operator.SaveValue,
                Type = operatorDefinition.Type,
            };
            return true;
        }
        else if (LanguageOperators.UnaryOperators.Contains(@operator.Operator.Content))
        {
            switch (@operator.Operator.Content)
            {
                case UnaryOperatorCall.LogicalNOT:
                {
                    if (!GenerateCodeForStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = BuiltinType.U8,
                    };
                    return true;
                }
                case UnaryOperatorCall.BinaryNOT:
                {
                    if (!GenerateCodeForStatement(@operator.Left, out CompiledStatementWithValue? left)) return false;

                    compiledStatement = new CompiledUnaryOperatorCall()
                    {
                        Left = left,
                        Location = @operator.Location,
                        Operator = @operator.Operator.Content,
                        SaveValue = @operator.SaveValue,
                        Type = left.Type,
                    };
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
            Diagnostics.Add(Diagnostic.Critical(
                $"Unknown operator \"{@operator.Operator.Content}\"",
                @operator.Operator,
                @operator.File,
                operatorNotFoundError.ToError(@operator)));
            return false;
        }
    }
    bool GenerateCodeForStatement(Assignment setter, [NotNullWhen(true)] out CompiledStatement? compiledStatement) => GenerateCodeForValueSetter(setter.Left, setter.Right, out compiledStatement);
    bool GenerateCodeForStatement(LiteralStatement literal, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null)
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
                            OnGotStatementType(literal, BuiltinType.U8);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.U8,
                                Value = new CompiledValue((byte)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.I8,
                                Value = new CompiledValue((sbyte)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.Char);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.Char,
                                Value = new CompiledValue((ushort)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.I16,
                                Value = new CompiledValue((short)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.GetInt() >= (int)uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.U32,
                                Value = new CompiledValue((uint)literal.GetInt()),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        OnGotStatementType(literal, BuiltinType.I32);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = BuiltinType.I32,
                            Value = new CompiledValue((int)literal.GetInt()),
                        };
                        return true;
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = BuiltinType.F32,
                            Value = new CompiledValue((float)literal.GetInt()),
                        };
                        return true;
                    }
                }

                OnGotStatementType(literal, BuiltinType.I32);
                compiledStatement = new CompiledEvaluatedValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = BuiltinType.I32,
                    Value = new CompiledValue((int)literal.GetInt()),
                };
                return true;
            }
            case LiteralType.Float:
            {
                OnGotStatementType(literal, BuiltinType.F32);
                compiledStatement = new CompiledEvaluatedValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = BuiltinType.F32,
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
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.U8, new CompiledEvaluatedValue()
                    {
                        Value = literal.Value.Length + 1,
                        Location = literal.Location,
                        Type = BuiltinType.I32,
                        SaveValue = true
                    }, literal.Value.Length + 1)));
                    return GenerateCodeForLiteralString(literal.Value, literal.Location, true, out compiledStatement);
                }
                else
                {
                    OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.Char, new CompiledEvaluatedValue()
                    {
                        Value = literal.Value.Length + 1,
                        Location = literal.Location,
                        Type = BuiltinType.I32,
                        SaveValue = true
                    }, literal.Value.Length + 1)));
                    return GenerateCodeForLiteralString(literal.Value, literal.Location, false, out compiledStatement);
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
                            OnGotStatementType(literal, BuiltinType.U8);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.U8,
                                Value = new CompiledValue((byte)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I8);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.I8,
                                Value = new CompiledValue((sbyte)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        OnGotStatementType(literal, BuiltinType.Char);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = BuiltinType.Char,
                            Value = new CompiledValue((char)literal.Value[0]),
                        };
                        return true;
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I16);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.I16,
                                Value = new CompiledValue((short)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.Value[0] >= uint.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.U32);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.U32,
                                Value = new CompiledValue((uint)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I32))
                    {
                        if (literal.Value[0] >= int.MinValue)
                        {
                            OnGotStatementType(literal, BuiltinType.I32);
                            compiledStatement = new CompiledEvaluatedValue()
                            {
                                Location = literal.Location,
                                SaveValue = literal.SaveValue,
                                Type = BuiltinType.I32,
                                Value = new CompiledValue((int)literal.Value[0]),
                            };
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        OnGotStatementType(literal, BuiltinType.F32);
                        compiledStatement = new CompiledEvaluatedValue()
                        {
                            Location = literal.Location,
                            SaveValue = literal.SaveValue,
                            Type = BuiltinType.F32,
                            Value = new CompiledValue((float)literal.Value[0]),
                        };
                        return true;
                    }
                }

                OnGotStatementType(literal, BuiltinType.Char);
                compiledStatement = new CompiledEvaluatedValue()
                {
                    Location = literal.Location,
                    SaveValue = literal.SaveValue,
                    Type = BuiltinType.Char,
                    Value = new CompiledValue((char)literal.Value[0]),
                };
                return true;
            }
            default: throw new UnreachableException();
        }

        compiledStatement = null;
        return false;
    }
    bool GenerateCodeForLiteralString(string literal, Location location, bool withBytes, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        BuiltinType type = withBytes ? BuiltinType.U8 : BuiltinType.Char;

        if (!GenerateAllocator(LiteralStatement.CreateAnonymous((1 + literal.Length) * type.GetSize(this), location.Position, location.File), out var allocator)) return false;

        compiledStatement = new CompiledStringInstance()
        {
            Value = literal,
            IsASCII = withBytes,
            Location = location,
            SaveValue = true,
            Type = new PointerType(new ArrayType(type, null, literal.Length + 1)),
            Allocator = allocator,
        };
        return true;
    }
    bool GenerateCodeForStatement(Identifier variable, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        compiledStatement = null;

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

            compiledStatement = new CompiledEvaluatedValue()
            {
                Value = constant.Value,
                Type = constant.Type,
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

            compiledStatement = new CompiledLocalVariableGetter()
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

            compiledStatement = new CompiledGlobalVariableGetter()
            {
                Variable = globalVariable,
                Type = globalVariable.Type,
                Location = variable.Location,
                SaveValue = variable.SaveValue,
            };
            return true;
        }

        if (GetFunction(variable.Content, expectedType, out FunctionQueryResult<CompiledFunction>? result, out PossibleDiagnostic? functionNotFoundError))
        {
            CompiledFunction? compiledFunction = result.Function;

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

        if (GetInstructionLabel(variable.Content, out CompiledInstructionLabelDeclaration? instructionLabel, out _))
        {
            variable.Reference = instructionLabel;
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

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{variable.Content}\" not found",
            variable,
            constantNotFoundError.ToError(variable),
            parameterNotFoundError.ToError(variable),
            variableNotFoundError.ToError(variable),
            globalVariableNotFoundError.ToError(variable),
            functionNotFoundError.ToError(variable)));
        return false;
    }
    bool GenerateCodeForStatement(AddressGetter addressGetter, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!GenerateCodeForStatement(addressGetter.PrevStatement, out CompiledStatementWithValue? of)) return false;

        compiledStatement = new CompiledAddressGetter()
        {
            Of = of,
            Type = new PointerType(of.Type),
            Location = addressGetter.Location,
            SaveValue = addressGetter.SaveValue,
        };
        return true;
    }
    bool GenerateCodeForStatement(Pointer pointer, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        if (!GenerateCodeForStatement(pointer.PrevStatement, out CompiledStatementWithValue? to)) return false;

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
    bool GenerateCodeForStatement(WhileLoop whileLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
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

                GenerateCodeForStatement(block, true);

                OnScopeExit(whileLoop.Block.Position.After(), whileLoop.Block.File);
            }
            else
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"While loop fully trimmed", whileLoop));
            }
            return;
        }
        */

        Scope scope = Scopes.Push(CompileScope(block.Statements));

        if (!GenerateCodeForStatement(whileLoop.Condition, out CompiledStatementWithValue? condition)) return false;

        if (!GenerateCodeForStatement(block, out CompiledStatement? body, true)) return false;

        if (Scopes.Pop() != scope) throw new InternalExceptionWithoutContext();

        compiledStatement = new CompiledWhileLoop()
        {
            Condition = condition,
            Body = body,
            Location = whileLoop.Location,
        };
        return true;
    }
    bool GenerateCodeForStatement(ForLoop forLoop, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        Scope scope = Scopes.Push(CompileScope(Enumerable.Repeat(forLoop.VariableDeclaration, 1)));

        if (!GenerateCodeForStatement(forLoop.VariableDeclaration, out CompiledStatement? variableDeclaration)) return false;

        /*
        if (AllowEvaluating &&
            TryCompute(forLoop.Condition, out CompiledValue condition))
        {
            if (condition)
            {
                Diagnostics.Add(Diagnostic.OptimizationNotice($"For-loop condition evaluated as true", forLoop.Condition));

                GenerateCodeForStatement(forLoop.Block);

                AddComment("For-loop expression");
                GenerateCodeForStatement(forLoop.Expression);

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

        if (!GenerateCodeForStatement(forLoop.Condition, out CompiledStatementWithValue? condition)) return false;

        if (!GenerateCodeForStatement(forLoop.Block, out CompiledStatement? body)) return false;
        if (!GenerateCodeForStatement(forLoop.Expression, out CompiledStatement? expression)) return false;

        if (Scopes.Pop() != scope) throw new InternalExceptionWithoutContext("Bruh");

        compiledStatement = new CompiledForLoop()
        {
            VariableDeclaration = (CompiledVariableDeclaration)variableDeclaration,
            Condition = condition,
            Expression = expression,
            Body = body,
            Location = forLoop.Location,
        };
        return true;
    }
    bool GenerateCodeForStatement(IfContainer @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        LinkedIf links = @if.ToLinks();
        return GenerateCodeForStatement(links, out compiledStatement);
    }
    bool GenerateCodeForStatement(LinkedIf @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GenerateCodeForStatement(@if.Condition, out CompiledStatementWithValue? condition)) return false;

        if (condition is CompiledEvaluatedValue evaluatedCondition)
        {
            if (evaluatedCondition.Value)
            {
                return GenerateCodeForStatement(@if.Block, out compiledStatement);
            }
            else if (@if.NextLink is not null)
            {
                return GenerateCodeForStatement(@if.NextLink, out compiledStatement);
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

        CompiledStatement? next = null;

        if (@if.NextLink is not null && !GenerateCodeForStatement(@if.NextLink, out next)) return false;
        if (!GenerateCodeForStatement(@if.Block, out CompiledStatement? body)) return false;

        compiledStatement = new CompiledIf()
        {
            Condition = condition,
            Body = body,
            Next = (CompiledBranch?)next,
            Location = @if.Location,
        };
        return true;
    }
    bool GenerateCodeForStatement(LinkedElse @if, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (!GenerateCodeForStatement(@if.Block, out CompiledStatement? body)) return false;

        compiledStatement = new CompiledElse()
        {
            Body = body,
            Location = @if.Location,
        };
        return true;
    }
    bool GenerateCodeForStatement(NewInstance newInstance, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        GeneralType instanceType = CompileType(newInstance.Type);

        newInstance.Type.SetAnalyzedType(instanceType);
        OnGotStatementType(newInstance, instanceType);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                if (!GenerateAllocator(new AnyCall(
                    new Identifier(Token.CreateAnonymous("sizeof"), newInstance.File),
                    new CompiledTypeStatement[] { new(Token.CreateAnonymous(StatementKeywords.Type), pointerType.To, newInstance.File) },
                    Array.Empty<Token>(),
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
    bool GenerateCodeForStatement(ConstructorCall constructorCall, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        GeneralType instanceType = CompileType(constructorCall.Type);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Arguments);

        if (!GetConstructor(instanceType, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructor>? result, out PossibleDiagnostic? notFound, AddCompilable))
        {
            Diagnostics.Add(notFound.ToError(constructorCall.Type, constructorCall.File));
            return false;
        }

        CompiledConstructor? compiledFunction = result.Function;

        compiledFunction.References.AddReference(constructorCall);
        OnGotStatementType(constructorCall, compiledFunction.Type);

        if (!compiledFunction.CanUse(constructorCall.File))
        {
            Diagnostics.Add(Diagnostic.Error($"Constructor \"{compiledFunction.ToReadable()}\" could not be called due to its protection level", constructorCall.Type, constructorCall.File));
            return false;
        }

        if (!GenerateCodeForStatement(constructorCall.ToInstantiation(), out var _object)) return false;
        if (!GenerateCodeForArguments(constructorCall.Arguments, compiledFunction, out ImmutableArray<CompiledPassedArgument> compiledArguments, 1)) return false;

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
    bool GenerateCodeForStatement(Field field, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType.Is(out ArrayType? arrayType) && field.Identifier.Content == "Length")
        {
            if (arrayType.Length is null)
            {
                Diagnostics.Add(Diagnostic.Critical("Array type's length isn't defined", field));
                return false;
            }

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

        if (prevType.Is(out PointerType? pointerType2))
        {
            if (!GenerateCodeForStatement(field.PrevStatement, out CompiledStatementWithValue? prev)) return false;
            prevType = pointerType2.To;

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
            fieldDefinition.References.AddReference(field);

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

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        if (!structType.GetField(field.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(field.Identifier, field.File));
            return false;
        }

        field.CompiledType = compiledField.Type;
        field.Reference = compiledField;
        compiledField.References.AddReference(field);

        {
            if (!GenerateCodeForStatement(field.PrevStatement, out CompiledStatementWithValue? prev)) return false;

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
    }
    bool GenerateCodeForStatement(IndexCall index, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        GeneralType prevType = FindStatementType(index.PrevStatement);
        GeneralType indexType = FindStatementType(index.Index);

        if (GetIndexGetter(prevType, indexType, index.File, out FunctionQueryResult<CompiledFunction>? indexer, out PossibleDiagnostic? notFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(index, index.File));
            return GenerateCodeForFunctionCall(index, ImmutableArray.Create(index.PrevStatement, index.Index), indexer.Function, out compiledStatement);
        }

        if (!GenerateCodeForStatement(index.PrevStatement, out CompiledStatementWithValue? baseStatement)) return false;
        if (!GenerateCodeForStatement(index.Index, out CompiledStatementWithValue? indexStatement)) return false;

        if (prevType.Is(out ArrayType? arrayType))
        {
            if (TryCompute(index.Index, out CompiledValue computedIndexData))
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

        if (prevType.Is(out PointerType? pointerType) && pointerType.To.Is(out arrayType))
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

        Diagnostics.Add(Diagnostic.Critical($"Index getter for type \"{prevType}\" not found", index));
        return false;
    }
    bool GenerateAddressResolver(Address address)
    {
        switch (address)
        {
            case AddressPointer:
            {
                return true;
            }
            case AddressRegisterPointer:
            {
                return true;
            }
            case AddressOffset addressOffset:
            {
                if (!GenerateAddressResolver(addressOffset.Base)) return false;

                return true;
            }
            case AddressRuntimePointer2:
            {
                return true;
            }
            case AddressRuntimeIndex2 runtimeIndex:
            {
                GenerateAddressResolver(runtimeIndex.Base);

                GeneralType indexType = runtimeIndex.IndexValue.Type;

                if (!indexType.Is<BuiltinType>())
                {
                    Diagnostics.Add(Diagnostic.Critical($"Index type must be builtin (ie. \"int\") and not \"{indexType}\"", runtimeIndex.IndexValue));
                    return false;
                }

                return true;
            }
            default: throw new NotImplementedException();
        }
    }
    bool GenerateCodeForStatement(ModifiedStatement modifiedStatement, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Content == ModifierKeywords.Ref)
        {
            throw new NotImplementedException();
        }

        if (modifier.Content == ModifierKeywords.Temp)
        {
            return GenerateCodeForStatement(statement, out compiledStatement);
        }

        throw new NotImplementedException();
    }
    bool GenerateCodeForStatement(LiteralList listValue, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        ArrayType type = FindStatementType(listValue);

        ImmutableArray<CompiledStatementWithValue>.Builder result = ImmutableArray.CreateBuilder<CompiledStatementWithValue>(listValue.Values.Length);
        for (int i = 0; i < listValue.Values.Length; i++)
        {
            if (!GenerateCodeForStatement(listValue.Values[i], out CompiledStatementWithValue? item)) return false;
            result.Add(item);
        }

        compiledStatement = new CompiledLiteralList()
        {
            Values = result.ToImmutable(),
            Type = type,
            Location = listValue.Location,
            SaveValue = listValue.SaveValue,
        };
        return true;
    }
    bool GenerateCodeForStatement(BasicTypeCast typeCast, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;

        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = CompileType(typeCast.Type);

        if (!GenerateCodeForStatement(typeCast.PrevStatement, out CompiledStatementWithValue? prev)) return false;

        if (statementType.Equals(targetType))
        {
            Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Keyword, typeCast.File));
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
    bool GenerateCodeForStatement(ManagedTypeCast typeCast, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement)
    {
        compiledStatement = null;
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = CompileType(typeCast.Type);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (!GenerateCodeForStatement(typeCast.PrevStatement, out CompiledStatementWithValue? prev)) return false;

        if (statementType.Equals(targetType))
        {
            Diagnostics.Add(Diagnostic.Hint($"Redundant type conversion", typeCast.Type, typeCast.File));
            compiledStatement = prev;
            return true;
        }

        if (AllowEvaluating &&
            targetType.Is(out BuiltinType? targetBuiltinType) &&
            TryComputeSimple(typeCast.PrevStatement, out CompiledValue prevValue) &&
            prevValue.TryCast(targetBuiltinType.RuntimeType, out CompiledValue castedValue))
        {
            Diagnostics.Add(Diagnostic.OptimizationNotice($"Type cast evaluated, converting {prevValue} to {castedValue}", typeCast));
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
        if (statementType.SameAs(BuiltinType.F32) &&
            targetType.SameAs(BuiltinType.I32))
        {
            compiledStatement = new CompiledTypeCast()
            {
                Value = prev,
                Type = targetType,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }

        // i32 -> f32
        if (statementType.SameAs(BuiltinType.I32) &&
            targetType.SameAs(BuiltinType.F32))
        {
            compiledStatement = new CompiledTypeCast()
            {
                Value = prev,
                Type = targetType,
                Location = typeCast.Location,
                SaveValue = typeCast.SaveValue,
            };
            return true;
        }

        compiledStatement = new CompiledTypeCast()
        {
            Value = prev,
            Type = targetType,
            Location = typeCast.Location,
            SaveValue = typeCast.SaveValue,
        };
        return true;
    }

    bool GenerateCodeForStatement(Block block, [NotNullWhen(true)] out CompiledStatement? compiledStatement, bool ignoreScope = false)
    {
        compiledStatement = null;

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(block.Statements.Length);

        if (ignoreScope)
        {
            for (int i = 0; i < block.Statements.Length; i++)
            {
                if (!GenerateCodeForStatement(block.Statements[i], out CompiledStatement? item)) return false;
                res.Add(item);
            }

            compiledStatement = new CompiledBlock()
            {
                Statements = res.ToImmutable(),
                Location = block.Location,
            };
            return true;
        }

        Scope scope = Scopes.Push(CompileScope(block.Statements));

        for (int i = 0; i < block.Statements.Length; i++)
        {
            if (!GenerateCodeForStatement(block.Statements[i], out CompiledStatement? item)) return false;
            res.Add(item);
        }

        if (Scopes.Pop() != scope) throw new InternalExceptionWithoutContext("Bruh");

        compiledStatement = new CompiledBlock()
        {
            Statements = res.ToImmutable(),
            Location = block.Location,
        };
        return true;
    }

    bool GenerateCodeForStatement(Statement statement, [NotNullWhen(true)] out CompiledStatement? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true)
    {
        switch (statement)
        {
            case StatementWithValue v:
                if (GenerateCodeForStatement(v, out CompiledStatementWithValue? compiledStatementWithValue))
                {
                    compiledStatement = compiledStatementWithValue;
                    return true;
                }
                else
                {
                    compiledStatement = compiledStatementWithValue;
                    return false;
                }
            case VariableDeclaration v: return GenerateCodeForStatement(v, out compiledStatement);
            case KeywordCall v: return GenerateCodeForStatement(v, out compiledStatement);
            case AnyAssignment v: return GenerateCodeForStatement(v.ToAssignment(), out compiledStatement);
            case WhileLoop v: return GenerateCodeForStatement(v, out compiledStatement);
            case ForLoop v: return GenerateCodeForStatement(v, out compiledStatement);
            case IfContainer v: return GenerateCodeForStatement(v, out compiledStatement);
            case LinkedIf v: return GenerateCodeForStatement(v, out compiledStatement);
            case LinkedElse v: return GenerateCodeForStatement(v, out compiledStatement);
            case Block v: return GenerateCodeForStatement(v, out compiledStatement);
            case InstructionLabel v: return GenerateCodeForStatement(v, out compiledStatement);
            default: throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }
    }

    bool GenerateCodeForStatement(StatementWithValue statement, [NotNullWhen(true)] out CompiledStatementWithValue? compiledStatement, GeneralType? expectedType = null, bool resolveReference = true) => statement switch
    {
        LiteralList v => GenerateCodeForStatement(v, out compiledStatement),
        BinaryOperatorCall v => GenerateCodeForStatement(v, out compiledStatement, expectedType),
        UnaryOperatorCall v => GenerateCodeForStatement(v, out compiledStatement),
        LiteralStatement v => GenerateCodeForStatement(v, out compiledStatement, expectedType),
        Identifier v => GenerateCodeForStatement(v, out compiledStatement, expectedType, resolveReference),
        AddressGetter v => GenerateCodeForStatement(v, out compiledStatement),
        Pointer v => GenerateCodeForStatement(v, out compiledStatement),
        NewInstance v => GenerateCodeForStatement(v, out compiledStatement),
        ConstructorCall v => GenerateCodeForStatement(v, out compiledStatement),
        IndexCall v => GenerateCodeForStatement(v, out compiledStatement),
        Field v => GenerateCodeForStatement(v, out compiledStatement),
        BasicTypeCast v => GenerateCodeForStatement(v, out compiledStatement),
        ManagedTypeCast v => GenerateCodeForStatement(v, out compiledStatement),
        ModifiedStatement v => GenerateCodeForStatement(v, out compiledStatement),
        AnyCall v => GenerateCodeForStatement(v, out compiledStatement),
        _ => throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\""),
    };

    /*
    CompiledVariable CompileVariable(VariableDeclaration newVariable)
    {
        if (LanguageConstants.KeywordList.Contains(newVariable.Identifier.Content))
        { Diagnostics.Add(Diagnostic.Critical($"Illegal variable name \"{newVariable.Identifier.Content}\"", newVariable.Identifier, newVariable.File)); }

        GeneralType type;
        if (newVariable.Type == StatementKeywords.Var)
        {
            if (newVariable.InitialValue == null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Initial value for variable declaration with implicit type is required", newVariable));
                type = BuiltinType.Void;
            }
            else
            {
                type = FindStatementType(newVariable.InitialValue);
            }
        }
        else
        {
            type = CompileType(newVariable.Type);

            if (type is ArrayType arrayType &&
                newVariable.InitialValue is LiteralList literalList &&
                arrayType.Length is null)
            {
                type = new ArrayType(arrayType.Of, null, literalList.Values.Length);
            }

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        if (!type.AllGenericsDefined())
        {
            Diagnostics.Add(Diagnostic.Internal($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\" (what edge case is this???)", newVariable.Type, newVariable.File));
        }

        return new CompiledVariable(0, type, newVariable);
    }
    */

    bool GenerateCodeForValueSetter(Statement statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        switch (statementToSet)
        {
            case Identifier v: return GenerateCodeForValueSetter(v, value, out compiledStatement);
            case Field v: return GenerateCodeForValueSetter(v, value, out compiledStatement);
            case IndexCall v: return GenerateCodeForValueSetter(v, value, out compiledStatement);
            case Pointer v: return GenerateCodeForValueSetter(v, value, out compiledStatement);
            default:
                Diagnostics.Add(Diagnostic.Critical($"The left side of the assignment operator should be a variable, field or memory address. Passed \"{statementToSet.GetType().Name}\"", statementToSet));
                return false;
        }
    }
    bool GenerateCodeForValueSetter(Identifier statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        if (RegisterKeywords.TryGetValue(statementToSet.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            GeneralType valueType = FindStatementType(value, registerKeyword.Type);

            if (!CanCastImplicitly(valueType, registerKeyword.Type, value, out PossibleDiagnostic? castError))
            { Diagnostics.Add(castError.ToError(value)); }

            if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, registerKeyword.Type)) return false;
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

        if (GetConstant(statementToSet.Content, statementToSet.File, out _, out _))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.ConstantName;
            Diagnostics.Add(Diagnostic.Critical($"Can not set constant value: it is readonly", statementToSet));
            return false;
        }

        if (GetParameter(statementToSet.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (statementToSet.Content != StatementKeywords.This)
            { statementToSet.AnalyzedType = TokenAnalyzedType.ParameterName; }
            statementToSet.CompiledType = parameter.Type;
            statementToSet.Reference = parameter;

            GeneralType valueType = FindStatementType(value, parameter.Type);

            if (!CanCastImplicitly(valueType, parameter.Type, value, out PossibleDiagnostic? castError))
            {
                Diagnostics.Add(castError.ToError(value));
            }

            if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, parameter.Type)) return false;
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

            if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, variable.Type)) return false;
            compiledStatement = new CompiledLocalVariableSetter()
            {
                Variable = variable,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledLocalVariableGetter _v2 &&
                    _v2.Variable == variable,
            };
            return true;
        }

        if (GetGlobalVariable(statementToSet.Content, statementToSet.File, out CompiledVariableDeclaration? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            statementToSet.AnalyzedType = TokenAnalyzedType.VariableName;
            statementToSet.CompiledType = globalVariable.Type;
            statementToSet.Reference = globalVariable;

            if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, globalVariable.Type)) return false;
            compiledStatement = new CompiledGlobalVariableSetter()
            {
                Variable = globalVariable,
                Value = _value,
                Location = statementToSet.Location.Union(value.Location),
                IsCompoundAssignment =
                    _value is CompiledBinaryOperatorCall _v1 &&
                    _v1.Left is CompiledGlobalVariableGetter _v2 &&
                    _v2.Variable == globalVariable,
            };
            return true;
        }

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{statementToSet.Content}\" not found",
            statementToSet,
            parameterNotFoundError.ToError(statementToSet),
            variableNotFoundError.ToError(statementToSet),
            globalVariableNotFoundError.ToError(statementToSet)));
        return false;
    }
    bool GenerateCodeForValueSetter(Field statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        GeneralType type = FindStatementType(statementToSet);
        if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, type)) return false;

        if (!CanCastImplicitly(_value.Type, type, value, out PossibleDiagnostic? castError2))
        {
            Diagnostics.Add(castError2.ToError(value));
        }

        compiledStatement = null;
        statementToSet.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);

        if (prevType.Is<ArrayType>() && statementToSet.Identifier.Content == "Length")
        {
            Diagnostics.Add(Diagnostic.Critical("Array type's length is readonly", statementToSet));
            return false;
        }

        if (prevType.Is(out PointerType? pointerType2))
        {
            if (!GenerateCodeForStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? prev)) return false;
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

            if (!structPointerType.GetField(statementToSet.Identifier.Content, this, out CompiledField? fieldDefinition, out _, out PossibleDiagnostic? error1))
            {
                Diagnostics.Add(error1.ToError(statementToSet.Identifier, statementToSet.File));
                return false;
            }

            statementToSet.CompiledType = fieldDefinition.Type;
            statementToSet.Reference = fieldDefinition;
            fieldDefinition.References.AddReference(statementToSet);

            compiledStatement = new CompiledFieldSetter()
            {
                Object = prev,
                Field = fieldDefinition,
                Location = statementToSet.Location,
                Value = _value,
                Type = GeneralType.InsertTypeParameters(fieldDefinition.Type, structPointerType.TypeArguments) ?? fieldDefinition.Type,
                IsCompoundAssignment = false,
            };
            return true;
        }

        if (!prevType.Is(out StructType? structType)) throw new NotImplementedException();

        if (!structType.GetField(statementToSet.Identifier.Content, this, out CompiledField? compiledField, out _, out PossibleDiagnostic? error2))
        {
            Diagnostics.Add(error2.ToError(statementToSet.Identifier, statementToSet.File));
            return false;
        }

        statementToSet.CompiledType = compiledField.Type;
        statementToSet.Reference = compiledField;
        compiledField.References.AddReference(statementToSet);

        {
            if (!GenerateCodeForStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? prev)) return false;

            compiledStatement = new CompiledFieldSetter()
            {
                Field = compiledField,
                Object = prev,
                Location = statementToSet.Location,
                Value = _value,
                Type = GeneralType.InsertTypeParameters(compiledField.Type, structType.TypeArguments) ?? compiledField.Type,
                IsCompoundAssignment = false,
            };
            return true;
        }
    }
    bool GenerateCodeForValueSetter(IndexCall statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        GeneralType itemType = FindStatementType(statementToSet);
        GeneralType prevType = FindStatementType(statementToSet.PrevStatement);
        GeneralType indexType = FindStatementType(statementToSet.Index);
        GeneralType valueType = FindStatementType(value, itemType);

        if (GetIndexSetter(prevType, valueType, indexType, statementToSet.File, out FunctionQueryResult<CompiledFunction>? indexer, out PossibleDiagnostic? indexerNotFoundError, AddCompilable))
        {
            indexer.Function.References.Add(new(statementToSet, statementToSet.File));
            if (GenerateCodeForFunctionCall(statementToSet, ImmutableArray.Create<StatementWithValue>(statementToSet.PrevStatement, statementToSet.Index, value), indexer.Function, out CompiledStatementWithValue? compiledStatement2))
            {
                compiledStatement = compiledStatement2;
                return true;
            }
            else
            {
                return false;
            }
        }

        if (!CanCastImplicitly(valueType, itemType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, itemType)) return false;
        if (!GenerateCodeForStatement(statementToSet.Index, out CompiledStatementWithValue? _index)) return false;
        if (!GenerateCodeForStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? _base)) return false;

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
    bool GenerateCodeForValueSetter(Pointer statementToSet, StatementWithValue value, [NotNullWhen(true)] out CompiledStatement? compiledStatement)
    {
        compiledStatement = null;

        GeneralType targetType = FindStatementType(statementToSet);
        GeneralType valueType = FindStatementType(value, targetType);
        GeneralType pointerValueType = FindStatementType(statementToSet.PrevStatement);

        if (!CanCastImplicitly(valueType, targetType, value, out PossibleDiagnostic? castError))
        {
            Diagnostics.Add(castError.ToError(value));
        }

        if (pointerValueType.GetBitWidth(this, Diagnostics, statementToSet.PrevStatement) != PointerBitWidth)
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{pointerValueType}\" cant be a pointer", statementToSet.PrevStatement));
            return false;
        }

        if (!GenerateCodeForStatement(value, out CompiledStatementWithValue? _value, targetType)) return false;
        if (!GenerateCodeForStatement(statementToSet.PrevStatement, out CompiledStatementWithValue? prev)) return false;

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
        var localConstants = ImmutableArray.CreateBuilder<CompiledVariableConstant>();
        var localInstructionLabels = ImmutableArray.CreateBuilder<CompiledInstructionLabelDeclaration>();

        foreach (var item in statements)
        {
            if (item is VariableDeclaration variableDeclaration)
            {
                if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
                {
                    var variable = CompileConstant(variableDeclaration);
                    localConstants.Add(variable);
                }
            }
            else if (item is InstructionLabel instructionLabel)
            {
                localInstructionLabels.Add(new CompiledInstructionLabelDeclaration()
                {
                    Identifier = instructionLabel.Identifier.Content,
                    Location = instructionLabel.Location,
                });
            }
        }

        return new Scope(localConstants.ToImmutable(), localInstructionLabels.ToImmutable());
    }

    #region CompileType

    public GeneralType CompileType(
        TypeInstance type,
        Uri? file = null) => type switch
        {
            TypeInstanceSimple simpleType => CompileType(simpleType, file),
            TypeInstanceFunction functionType => CompileType(functionType, file),
            TypeInstanceStackArray stackArrayType => CompileType(stackArrayType, file),
            TypeInstancePointer pointerType => CompileType(pointerType, file),
            _ => throw new UnreachableException(),
        };

    public ArrayType CompileType(
        TypeInstanceStackArray type,
        Uri? file = null)
    {
        CompiledValue? stackArraySize = default;

        if (type.StackArraySize is not null)
        {
            if (TryCompute(type.StackArraySize, out CompiledValue _stackArraySize))
            { stackArraySize = _stackArraySize; }
        }

        GeneralType? of = CompileType(type.StackArrayOf, file);

        CompiledStatementWithValue? stackArraySizeStatement = null;

        if (type.StackArraySize is not null) GenerateCodeForStatement(type.StackArraySize, out stackArraySizeStatement);

        ArrayType result = new(of, stackArraySizeStatement, (int?)stackArraySize);
        type.SetAnalyzedType(result);

        return result;
    }

    public FunctionType CompileType(
        TypeInstanceFunction type,
        Uri? file = null)
    {
        GeneralType returnType = CompileType(type.FunctionReturnType, file);
        IEnumerable<GeneralType> parameters = CompileTypes(type.FunctionParameterTypes, file);

        FunctionType result = new(returnType, parameters);
        type.SetAnalyzedType(result);

        return result;
    }

    public PointerType CompileType(
        TypeInstancePointer type,
        Uri? file = null)
    {
        GeneralType to = CompileType(type.To, file);

        PointerType result = new(to);
        type.SetAnalyzedType(result);

        return result;
    }

    public GeneralType CompileType(
        TypeInstanceSimple type,
        Uri? file = null)
    {
        GeneralType? result;

        if (TypeKeywords.BasicTypes.TryGetValue(type.Identifier.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            type.SetAnalyzedType(result);
            return result;
        }

        if (!FindType(type.Identifier, type.File, out result))
        {
            Diagnostic.Critical($"Can't parse \"{type}\" to \"{nameof(GeneralType)}\"", type, file).Throw();
            return default;
        }

        if (result.Is(out StructType? resultStructType) &&
            resultStructType.Struct.Template is not null)
        {
            if (type.TypeArguments.HasValue)
            {
                IEnumerable<GeneralType> typeParameters = CompileTypes(type.TypeArguments.Value, file);
                result = new StructType(resultStructType.Struct, type.File, typeParameters.ToImmutableList());
            }
            else
            {
                result = new StructType(resultStructType.Struct, type.File);
            }
        }
        else
        {
            if (type.TypeArguments.HasValue)
            { throw new InternalExceptionWithoutContext($"Asd"); }
        }

        type.SetAnalyzedType(result);
        return result;
    }

    public IEnumerable<GeneralType> CompileTypes(
        IEnumerable<TypeInstance>? types,
        Uri? file = null)
    {
        if (types is null) yield break;

        foreach (TypeInstance item in types)
        { yield return CompileType(item, file); }
    }

    public IEnumerable<GeneralType> CompileTypes(
        IEnumerable<IHaveType>? types,
        Uri? file = null)
        => CompileTypes(types?.Select(v => v.Type), file);

    #endregion

    #region GenerateCodeForInstructionLabel

    void GenerateCodeForInstructionLabel(InstructionLabel instructionLabel)
    {
        foreach (CompiledInstructionLabelDeclaration other in CompiledInstructionLabels)
        {
            if (other.Identifier != instructionLabel.Identifier.Content) continue;
            Diagnostics.Add(Diagnostic.Warning($"Instruction label \"{other.Identifier}\" already defined", instructionLabel.Identifier));
        }

        CompiledInstructionLabels.Push(new CompiledInstructionLabelDeclaration()
        {
            Identifier = instructionLabel.Identifier.Content,
            Location = instructionLabel.Location,
        });
    }
    void GenerateCodeForInstructionLabel(Statement statement)
    {
        if (statement is InstructionLabel instructionLabel)
        { GenerateCodeForInstructionLabel(instructionLabel); }
    }
    void GenerateCodeForInstructionLabel(IEnumerable<Statement> statements)
    {
        foreach (Statement statement in statements)
        {
            GenerateCodeForInstructionLabel(statement);
        }
    }

    #endregion

    #region GenerateCodeFor...

    bool GenerateCodeForFunction(FunctionThingDefinition function)
    {
        if (function.Identifier is not null &&
            LanguageConstants.KeywordList.Contains(function.Identifier.ToString()))
        {
            Diagnostics.Add(Diagnostic.Critical($"The identifier \"{function.Identifier}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.File));
            return default;
        }

        if (function.Identifier is not null)
        { function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName; }

        if (function is FunctionDefinition functionDefinition)
        {
            for (int i = 0; i < functionDefinition.Attributes.Length; i++)
            {
                if (functionDefinition.Attributes[i].Identifier.Content == AttributeConstants.ExternalIdentifier)
                { return false; }
            }
        }

        Print?.Invoke($"Generate \"{function.ToReadable()}\" ...", LogType.Debug);

        InFunction = true;

        CompiledParameters.Clear();

        CompileParameters(function.Parameters);

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (function.Block is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{function.ToReadable()}\" does not have a body", function));
            return default;
        }

        GenerateCodeForInstructionLabel(function.Block.GetStatementsRecursively(false));

        if (!GenerateCodeForStatement(function.Block, out CompiledStatement? body)) return false;

        CurrentReturnType = null;

        CompiledParameters.Clear();

        InFunction = false;
        GeneratedFunctions.Add(new((ICompiledFunction)function, (CompiledBlock)body));
        return true;
    }

    bool GenerateCodeForFunction(ICompiledFunction function, Block body)
    {
        Print?.Invoke($"Generate \"{((FunctionThingDefinition)function).ToReadable()}\" ...", LogType.Debug);

        InFunction = true;

        CompiledParameters.Clear();

        CompiledParameters.AddRange(function.Parameters.Select((v, i) => new CompiledParameter(i, function.ParameterTypes[i], v)));

        if (function is IHaveCompiledType functionWithType)
        { CurrentReturnType = functionWithType.Type; }
        else
        { CurrentReturnType = BuiltinType.Void; }

        if (body is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Function \"{((FunctionThingDefinition)function).ToReadable()}\" does not have a body", (FunctionThingDefinition)function));
            return default;
        }

        Scope scope = Scopes.Push(CompileScope(body.Statements));

        GenerateCodeForStatement(body, out CompiledStatement? compiledBody);

        if (Scopes.Pop() != scope) throw new InternalExceptionWithoutContext("Bruh");

        CurrentReturnType = null;

        CompiledParameters.Clear();

        InFunction = false;
        return true;
    }

    HashSet<FunctionThingDefinition> _generatedFunctions = new();

    bool GenerateCodeForFunctions<TFunction>(IEnumerable<TFunction> functions)
        where TFunction : FunctionThingDefinition, IHaveInstructionOffset, IReferenceable
    {
        bool generatedAnything = false;
        foreach (TFunction function in functions)
        {
            if (function.IsTemplate) continue;
            if (function.InstructionOffset >= 0) continue;
            if (!function.References.Any()) continue;

            if (!_generatedFunctions.Add(function)) continue;

            if (GenerateCodeForFunction(function))
            { generatedAnything = true; }
        }
        return generatedAnything;
    }

    bool GenerateCodeForCompilableFunction<T>(CompliableTemplate<T> function)
        where T : FunctionThingDefinition, ITemplateable<T>
    {
        if (!_generatedFunctions.Add(function.Function)) return false;
        SetTypeArguments(function.TypeArguments);
        bool generatedAnything = GenerateCodeForFunction(function.Function);
        TypeArguments.Clear();
        return generatedAnything;
    }

    bool GenerateCodeForCompilableFunctions<T>(IReadOnlyList<CompliableTemplate<T>> functions)
        where T : FunctionThingDefinition, ITemplateable<T>, IHaveInstructionOffset
    {
        bool generatedAnything = false;
        int i = 0;
        while (i < functions.Count)
        {
            CompliableTemplate<T> function = functions[i];
            i++;

            if (function.Function.InstructionOffset >= 0) continue;

            if (GenerateCodeForCompilableFunction(function))
            { generatedAnything = true; }
        }
        return generatedAnything;
    }

    void CompileParameters(ImmutableArray<ParameterDefinition> parameters)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            GeneralType parameterType = CompileType(parameters[i].Type);
            parameters[i].Type.SetAnalyzedType(parameterType);

            CompiledParameters.Add(new CompiledParameter(i, parameterType, parameters[i]));
        }
    }

    bool GenerateCodeForTopLevelStatements(ImmutableArray<Statement> statements, [NotNullWhen(true)] out ImmutableArray<CompiledStatement> compiledStatements)
    {
        CurrentReturnType = ExitCodeType;

        ImmutableArray<CompiledStatement>.Builder res = ImmutableArray.CreateBuilder<CompiledStatement>(statements.Length);

        foreach (Statement statement in statements)
        {
            if (!GenerateCodeForStatement(statement, out CompiledStatement? compiledStatement))
            {
                CurrentReturnType = null;
                compiledStatements = ImmutableArray<CompiledStatement>.Empty;
                return false;
            }
            res.Add(compiledStatement);
        }

        CurrentReturnType = null;
        compiledStatements = res.ToImmutable();
        return true;
    }

    #endregion

    CompilerResult2 GenerateCode(CompilerResult compilerResult)
    {
        List<string> usedExternalFunctions = new();

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (function.IsExternal)
            { usedExternalFunctions.Add(function.ExternalFunctionName); }
        }

        foreach (CompiledOperator @operator in CompiledOperators)
        {
            if (@operator.IsExternal)
            { usedExternalFunctions.Add(@operator.ExternalFunctionName); }
        }

        foreach (var item in compilerResult.TopLevelStatements.SelectMany(v => v.Statements))
        {
            if (item is VariableDeclaration variableDeclaration)
            {
                if (variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
                {
                    var variable = CompileConstant(variableDeclaration);
                    CompiledGlobalConstants.Add(variable);
                }
            }
            else if (item is InstructionLabel instructionLabel)
            {
                CompiledInstructionLabels.Add(new CompiledInstructionLabelDeclaration()
                {
                    Identifier = instructionLabel.Identifier.Content,
                    Location = instructionLabel.Location,
                });
            }
        }

        ImmutableArray<CompiledStatement>.Builder compiledTopLevelStatements = ImmutableArray.CreateBuilder<CompiledStatement>();
        foreach ((ImmutableArray<Statement> Statements, Uri File) item in compilerResult.TopLevelStatements)
        {
            if (!GenerateCodeForTopLevelStatements(item.Statements, out ImmutableArray<CompiledStatement> v)) continue;
            compiledTopLevelStatements.AddRange(v);
        }

        while (true)
        {
            bool generatedAnything = false;

            generatedAnything = GenerateCodeForFunctions(CompiledFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledOperators) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledGeneralFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForFunctions(CompiledConstructors) || generatedAnything;

            generatedAnything = GenerateCodeForCompilableFunctions(CompilableFunctions) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableConstructors) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableOperators) || generatedAnything;
            generatedAnything = GenerateCodeForCompilableFunctions(CompilableGeneralFunctions) || generatedAnything;

            if (!generatedAnything) break;
        }

        return new CompilerResult2()
        {
            Functions = GeneratedFunctions.ToImmutableArray(),
            Statements = compiledTopLevelStatements.ToImmutable(),
        };
    }
}
