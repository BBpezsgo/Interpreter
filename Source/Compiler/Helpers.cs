using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using LiteralStatement = LanguageCore.Parser.Statements.Literal;

namespace LanguageCore.Compiler;

public partial class StatementCompiler : IRuntimeInfoProvider
{
    #region Fields

    readonly List<CompiledStruct> CompiledStructs = new();
    readonly List<CompiledOperatorDefinition> CompiledOperators = new();
    readonly List<CompiledConstructorDefinition> CompiledConstructors = new();
    readonly List<CompiledFunctionDefinition> CompiledFunctions = new();
    readonly List<CompiledGeneralFunctionDefinition> CompiledGeneralFunctions = new();
    readonly List<CompiledAlias> CompiledAliases = new();

    readonly Stack<CompiledVariableConstant> CompiledGlobalConstants = new();
    readonly Stack<CompiledInstructionLabelDeclaration> CompiledGlobalInstructionLabels = new();
    readonly Stack<CompiledVariableDeclaration> CompiledGlobalVariables = new();

    readonly DiagnosticsCollection Diagnostics;

    public const int InvalidFunctionAddress = int.MinValue;

    readonly ImmutableArray<IExternalFunction> ExternalFunctions;
    readonly ImmutableArray<ExternalConstant> ExternalConstants;

    public BuiltinType ArrayLengthType => Settings.ArrayLengthType;
    public BuiltinType BooleanType => Settings.BooleanType;
    public int PointerSize => Settings.PointerSize;
    public BuiltinType SizeofStatementType => Settings.SizeofStatementType;
    public BuiltinType ExitCodeType => Settings.ExitCodeType;

    readonly CompilerSettings Settings;
    readonly List<CompiledFunction> GeneratedFunctions = new();

    public BitWidth PointerBitWidth => (BitWidth)PointerSize;

    readonly List<CompliableTemplate<CompiledFunctionDefinition>> CompilableFunctions = new();
    readonly List<CompliableTemplate<CompiledOperatorDefinition>> CompilableOperators = new();
    readonly List<CompliableTemplate<CompiledGeneralFunctionDefinition>> CompilableGeneralFunctions = new();
    readonly List<CompliableTemplate<CompiledConstructorDefinition>> CompilableConstructors = new();

    readonly List<FunctionDefinition> OperatorDefinitions = new();
    readonly List<FunctionDefinition> FunctionDefinitions = new();
    readonly List<StructDefinition> StructDefinitions = new();
    readonly List<AliasDefinition> AliasDefinitions = new();

    readonly List<(ImmutableArray<Statement> Statements, Uri File)> TopLevelStatements = new();

    readonly Stack<ImmutableArray<Token>> GenericParameters = new();
    readonly ImmutableArray<UserDefinedAttribute> UserDefinedAttributes;
    readonly IEnumerable<string> PreprocessorVariables;
    readonly ImmutableArray<CompiledStatement>.Builder CompiledTopLevelStatements = ImmutableArray.CreateBuilder<CompiledStatement>();

    readonly List<(FunctionThingDefinition Function, CompiledGeneratorState State)> GeneratorStates = new();

    readonly Stack<CompiledFrame> Frames;

    CompiledGeneratorStructDefinition? GeneratorStructDefinition;

    #endregion

    enum ConstantPerfectus
    {
        None,
        Name,
        File,
    }

    bool GetConstant(string identifier, Uri file, [NotNullWhen(true)] out CompiledVariableConstant? constant, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
    {
        constant = null;
        notFoundError = null;
        ConstantPerfectus perfectus = ConstantPerfectus.None;

        foreach (Scope item in Frames.Last.Scopes)
        {
            foreach (CompiledVariableConstant _constant in item.Constants)
            {
                if (_constant.Identifier != identifier)
                {
                    if (perfectus < ConstantPerfectus.Name ||
                        notFoundError is null)
                    { notFoundError = new PossibleDiagnostic($"Constant \"{_constant.Identifier}\" not found"); }
                    continue;
                }
                perfectus = ConstantPerfectus.Name;

                if (!_constant.CanUse(file))
                {
                    if (perfectus < ConstantPerfectus.File ||
                        notFoundError is null)
                    { notFoundError = new PossibleDiagnostic($"Constant \"{_constant.Identifier}\" cannot be used due to its protection level"); }
                    continue;
                }
                perfectus = ConstantPerfectus.File;

                if (constant is not null)
                {
                    if (perfectus <= ConstantPerfectus.File ||
                        notFoundError is null)
                    { notFoundError = new PossibleDiagnostic($"Constant \"{_constant.Identifier}\" not found: multiple constants found"); }
                    return false;
                }

                constant = _constant;
            }
        }

        foreach (CompiledVariableConstant _constant in CompiledGlobalConstants)
        {
            if (_constant.Identifier != identifier)
            {
                if (perfectus < ConstantPerfectus.Name ||
                    notFoundError is null)
                { notFoundError = new PossibleDiagnostic($"Constant \"{identifier}\" not found"); }
                continue;
            }
            perfectus = ConstantPerfectus.Name;

            if (!_constant.CanUse(file))
            {
                if (perfectus < ConstantPerfectus.File ||
                    notFoundError is null)
                { notFoundError = new PossibleDiagnostic($"Constant \"{identifier}\" cannot be used due to its protection level"); }
                continue;
            }
            perfectus = ConstantPerfectus.File;

            if (constant is not null)
            {
                if (perfectus <= ConstantPerfectus.File ||
                    notFoundError is null)
                { notFoundError = new PossibleDiagnostic($"Constant \"{identifier}\" not found: multiple constants found"); }
                return false;
            }

            constant = _constant;
        }

        if (constant is null)
        {
            notFoundError = new PossibleDiagnostic($"Constant \"{identifier}\" not found");
            return false;
        }

        return true;
    }

    bool StatementCanBeDeallocated(StatementWithValue statement, out bool explicitly)
    {
        if (statement is ModifiedStatement modifiedStatement &&
            modifiedStatement.Modifier.Content == ModifierKeywords.Temp)
        {
            if (modifiedStatement.Statement is
                LiteralStatement or
                BinaryOperatorCall or
                UnaryOperatorCall)
            {
                Diagnostics.Add(Diagnostic.Hint($"Unnecessary explicit temp modifier (\"{modifiedStatement.Statement.GetType().Name}\" statements are implicitly deallocated)", modifiedStatement.Modifier, modifiedStatement.File));
            }

            explicitly = true;
            return true;
        }

        if (statement is LiteralStatement)
        {
            explicitly = false;
            return true;
        }

        if (statement is BinaryOperatorCall)
        {
            explicitly = false;
            return true;
        }

        if (statement is UnaryOperatorCall)
        {
            explicitly = false;
            return true;
        }

        explicitly = default;
        return false;
    }

    CompiledGeneratorState GetGeneratorState(FunctionThingDefinition function)
    {
        foreach ((FunctionThingDefinition _function, CompiledGeneratorState state) in GeneratorStates)
        {
            if (_function.Identifier == function.Identifier &&
                _function.Location == function.Location)
            {
                return state;
            }
        }
        CompiledGeneratorState result = new();
        GeneratorStates.Add((function, result));
        return result;
    }

    #region AddCompilable()

    void AddCompilable(CompliableTemplate<CompiledFunctionDefinition> compilable)
    {
        for (int i = 0; i < CompilableFunctions.Count; i++)
        {
            if (CompilableFunctions[i].Function.IsSame(compilable.Function))
            { return; }
        }
        CompilableFunctions.Add(compilable);
    }

    void AddCompilable(CompliableTemplate<CompiledOperatorDefinition> compilable)
    {
        for (int i = 0; i < CompilableOperators.Count; i++)
        {
            if (CompilableOperators[i].Function.IsSame(compilable.Function))
            { return; }
        }
        CompilableOperators.Add(compilable);
    }

    void AddCompilable(CompliableTemplate<CompiledGeneralFunctionDefinition> compilable)
    {
        for (int i = 0; i < CompilableGeneralFunctions.Count; i++)
        {
            if (CompilableGeneralFunctions[i].Function.IsSame(compilable.Function))
            { return; }
        }
        CompilableGeneralFunctions.Add(compilable);
    }

    CompliableTemplate<CompiledConstructorDefinition> AddCompilable(CompliableTemplate<CompiledConstructorDefinition> compilable)
    {
        for (int i = 0; i < CompilableConstructors.Count; i++)
        {
            if (CompilableConstructors[i].Function.IsSame(compilable.Function))
            { return CompilableConstructors[i]; }
        }
        CompilableConstructors.Add(compilable);
        return compilable;
    }

    #endregion

    bool GetLocalSymbolType(Identifier symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (GetVariable(symbolName.Content, out CompiledVariableDeclaration? variable, out _))
        {
            type = variable.Type;
            return true;
        }

        if (GetParameter(symbolName.Content, out CompiledParameter? parameter, out _))
        {
            type = parameter.Type;
            return true;
        }

        type = null;
        return false;
    }

    #region Get Functions ...

    public readonly struct Functions<TFunction> where TFunction : ITemplateable<TFunction>
    {
        public IEnumerable<TFunction> Compiled { get; init; }
        public IEnumerable<CompliableTemplate<TFunction>> Compilable { get; init; }
    }

    Functions<CompiledFunctionDefinition> GetFunctions() => new()
    {
        Compiled = CompiledFunctions,
        Compilable = CompilableFunctions,
    };

    Functions<CompiledOperatorDefinition> GetOperators() => new()
    {
        Compiled = CompiledOperators,
        Compilable = CompilableOperators,
    };

    Functions<CompiledGeneralFunctionDefinition> GetGeneralFunctions() => new()
    {
        Compiled = CompiledGeneralFunctions,
        Compilable = CompilableGeneralFunctions,
    };

    Functions<CompiledConstructorDefinition> GetConstructors() => new()
    {
        Compiled = CompiledConstructors,
        Compilable = CompilableConstructors,
    };

    bool GetConstructor(
        GeneralType type,
        ImmutableArray<GeneralType> arguments,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledConstructorDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledConstructorDefinition>>? addCompilable = null)
    {
        StructType? structType;

        {
            ImmutableArray<GeneralType>.Builder argumentsBuilder = ImmutableArray.CreateBuilder<GeneralType>();

            if (type.Is(out PointerType? pointerType))
            {
                if (!pointerType.To.Is<StructType>(out structType))
                {
                    result = null;
                    error = new PossibleDiagnostic($"Invalid type \"{type}\" used for constructor");
                    return false;
                }
                argumentsBuilder.Add(type);
            }
            else if (type.Is(out structType))
            {
                argumentsBuilder.Add(new PointerType(structType));
            }
            else
            {
                result = null;
                error = new PossibleDiagnostic($"Invalid type \"{type}\" used for constructor");
                return false;
            }

            argumentsBuilder.AddRange(arguments);
            arguments = argumentsBuilder.ToImmutable();
        }

        Functions<CompiledConstructorDefinition> constructors = GetConstructors();

        constructors = new Functions<CompiledConstructorDefinition>()
        {
            Compiled = constructors.Compiled.Where(v => v.Context == structType.Struct),
            Compilable = constructors.Compilable.Where(v => v.OriginalFunction.Context == structType.Struct),
        };

        return GetFunction<CompiledConstructorDefinition, GeneralType, GeneralType, GeneralType>(
            constructors,
            "constructor",
            CompiledConstructorDefinition.ToReadable(type, arguments),

            FunctionQuery.Create(type, arguments, relevantFile, null, addCompilable, (GeneralType passed, GeneralType defined, out int badness) =>
            {
                badness = 0;
                if (passed.Is(out PointerType? passedPointerType))
                {
                    if (defined.Is(out PointerType? definedPointerType))
                    {
                        badness = 0;
                        return passedPointerType.To.Equals(definedPointerType.To);
                    }
                    else if (defined.Is(out StructType? definedStructType))
                    {
                        badness = 1;
                        return passedPointerType.To.Equals(definedStructType);
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (passed.Is(out StructType? passedStructType))
                {
                    if (defined.Is(out PointerType? definedPointerType))
                    {
                        badness = 1;
                        return passedStructType.Equals(definedPointerType.To);
                    }
                    else if (defined.Is(out StructType? definedStructType))
                    {
                        badness = 0;
                        return passedStructType.Equals(definedStructType);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }),

            out result,
            out error
        );
    }

    bool GetIndexGetter(
        GeneralType prevType,
        GeneralType indexType,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = ImmutableArray.Create(prevType, indexType);
        FunctionQuery<CompiledFunctionDefinition, string, Token, GeneralType> query = FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(BuiltinFunctionIdentifiers.IndexerGet, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledFunctionDefinition, string, Token, GeneralType>(
            GetFunctions(),
            "function",
            null,

            query,

            out result,
            out error
        ) && result.Success;
    }

    bool GetIndexSetter(
        GeneralType prevType,
        GeneralType elementType,
        GeneralType indexType,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = ImmutableArray.Create(prevType, indexType, elementType);
        FunctionQuery<CompiledFunctionDefinition, string, Token, GeneralType> query = FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(BuiltinFunctionIdentifiers.IndexerSet, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledFunctionDefinition, string, Token, GeneralType>(
            GetFunctions(),
            "function",
            null,

            query,

            out result,
            out error
        ) && result.Success;
    }

    bool TryGetBuiltinFunction(
        string builtinName,
        ImmutableArray<GeneralType> arguments,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        IEnumerable<CompiledFunctionDefinition> builtinCompiledFunctions =
            CompiledFunctions
            .Where(v => v.BuiltinFunctionName == builtinName);

        IEnumerable<CompliableTemplate<CompiledFunctionDefinition>> builtinCompilableFunctions =
            CompilableFunctions
            .Where(v => v.Function.BuiltinFunctionName == builtinName);

        string readable = $"[Builtin(\"{builtinName}\")] ?({string.Join(", ", arguments)})";
        FunctionQuery<CompiledFunctionDefinition, string, Token, GeneralType> query = FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(null as string, arguments, relevantFile, null, addCompilable);

        return GetFunction<CompiledFunctionDefinition, string, Token, GeneralType>(
            new Functions<CompiledFunctionDefinition>()
            {
                Compiled = builtinCompiledFunctions,
                Compilable = builtinCompilableFunctions,
            },
            "builtin function",
            readable,

            query,

            out result,
            out error
        );
    }

    bool GetOperator(
        BinaryOperatorCall @operator,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperatorDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledOperatorDefinition>>? addCompilable = null)
    {
        FunctionQuery<CompiledOperatorDefinition, string, Token, StatementWithValue> query = FunctionQuery.Create<CompiledOperatorDefinition, string, Token>(
            @operator.Operator.Content,
            @operator.Arguments,
            FunctionArgumentConverter,
            relevantFile,
            null,
            addCompilable);
        return GetFunction<CompiledOperatorDefinition, string, Token, StatementWithValue>(
            GetOperators(),
            "operator",
            null,

            query,

            out result,
            out error
        ) && result.Success;
    }

    bool GetOperator(
        UnaryOperatorCall @operator,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperatorDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledOperatorDefinition>>? addCompilable = null)
    {
        FunctionQuery<CompiledOperatorDefinition, string, Token, StatementWithValue> query = FunctionQuery.Create<CompiledOperatorDefinition, string, Token>(
            @operator.Operator.Content,
            @operator.Arguments,
            FunctionArgumentConverter,
            relevantFile,
            null,
            addCompilable);
        return GetFunction<CompiledOperatorDefinition, string, Token, StatementWithValue>(
            GetOperators(),
            "operator",
            null,

            query,

            out result,
            out error
        ) && result.Success;
    }

    bool GetGeneralFunction(
        GeneralType context,
        ImmutableArray<GeneralType> arguments,
        string identifier,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledGeneralFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledGeneralFunctionDefinition>>? addCompilable = null)
    {
        IEnumerable<CompiledGeneralFunctionDefinition> compiledGeneralFunctionsInContext =
            CompiledGeneralFunctions
            .Where(v => ContextIs(v, context));

        IEnumerable<CompliableTemplate<CompiledGeneralFunctionDefinition>> compilableGeneralFunctionsInContext =
            CompilableGeneralFunctions
            .Where(v => ContextIs(v.Function, context));

        return GetFunction<CompiledGeneralFunctionDefinition, string, Token, GeneralType>(
            new Functions<CompiledGeneralFunctionDefinition>()
            {
                Compiled = compiledGeneralFunctionsInContext,
                Compilable = compilableGeneralFunctionsInContext,
            },
            "general function",
            null,

            FunctionQuery.Create<CompiledGeneralFunctionDefinition, string, Token>(identifier, arguments, relevantFile, null, addCompilable),

            out result,
            out error
        );
    }

    bool GetFunction(
        string identifier,
        GeneralType? type,
        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (type is null || !type.Is(out FunctionType? functionType))
        {
            return GetFunction(
                FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(identifier),
                out result,
                out error
            );
        }

        return GetFunction(
            FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(identifier, functionType.Parameters, null, functionType.ReturnType, null),
            out result,
            out error
        );
    }

    bool GetFunction(
        AnyCall call,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        result = null;
        error = null;

        if (!call.ToFunctionCall(out FunctionCall? functionCall))
        {
            error ??= new PossibleDiagnostic($"Function \"{call.ToReadable(FindStatementType)}\" not found");
            return false;
        }

        return GetFunction(
            functionCall,

            out result,
            out error,
            addCompilable
        );
    }

    bool GetFunction(
        FunctionCall functionCallStatement,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        string identifier = functionCallStatement.Identifier.Content;
        FunctionQuery<CompiledFunctionDefinition, string, Token, StatementWithValue> query = FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(
            identifier,
            functionCallStatement.MethodArguments,
            FunctionArgumentConverter,
            functionCallStatement.File,
            null,
            addCompilable);
        return GetFunction(
            query,
            out result,
            out error
        );
    }

    bool GetFunction<TArgument>(
        FunctionQuery<CompiledFunctionDefinition, string, Token, TArgument> query,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
        where TArgument : notnull
        => GetFunction<CompiledFunctionDefinition, string, Token, TArgument>(
            GetFunctions(),
            "function",
            null,

            query,
            out result,
            out error
        );

    #endregion

    static bool ContextIs(CompiledGeneralFunctionDefinition function, GeneralType type) =>
        type.Is(out StructType? structType) &&
        function.Context is not null &&
        function.Context == structType.Struct;

    #region CompileConstant()

    CompiledVariableConstant CompileConstant(VariableDeclaration variableDeclaration)
    {
        variableDeclaration.Identifier.AnalyzedType = TokenAnalyzedType.ConstantName;

        if (GetConstant(variableDeclaration.Identifier.Content, variableDeclaration.File, out _, out _))
        { Diagnostics.Add(Diagnostic.Critical($"Constant \"{variableDeclaration.Identifier}\" already defined", variableDeclaration.Identifier, variableDeclaration.File)); }

        CompileVariableAttributes(variableDeclaration);

        GeneralType? constantType = null;
        if (variableDeclaration.Type != StatementKeywords.Var)
        {
            constantType = CompileType(variableDeclaration.Type, variableDeclaration.File);
        }

        CompiledValue constantValue;

        if (variableDeclaration.ExternalConstantName is not null)
        {
            ExternalConstant? externalConstant = ExternalConstants.FirstOrDefault(v => v.Name == variableDeclaration.ExternalConstantName);
            if (externalConstant is not null)
            {
                constantValue = externalConstant.Value;
                goto gotExternalValue;
            }
            else if (variableDeclaration.InitialValue is null)
            {
                Diagnostics.Add(Diagnostic.Critical($"External constant \"{variableDeclaration.ExternalConstantName}\" not found", variableDeclaration));
                constantValue = default;
            }
            else
            {
                Diagnostics.Add(Diagnostic.Warning($"External constant \"{variableDeclaration.ExternalConstantName}\" not found", variableDeclaration));
            }
        }

        if (variableDeclaration.InitialValue is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Constant value must have initial value", variableDeclaration));
            constantValue = default;
        }
        else
        {
            CompileStatement(variableDeclaration.InitialValue, out CompiledStatementWithValue? compiledInitialValue, constantType);
            if (!TryCompute(compiledInitialValue, out constantValue))
            {
                Diagnostics.Add(Diagnostic.Critical($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue));
                constantValue = default;
            }
        }

    gotExternalValue:

        if (constantType is not null)
        {
            if (constantType.Is(out BuiltinType? builtinType))
            {
                if (!constantValue.TryCast(builtinType.RuntimeType, out CompiledValue castedConstantValue))
                {
                    Diagnostics.Add(Diagnostic.Error($"Can't cast constant value {constantValue} of type \"{new BuiltinType(constantValue.Type)}\" to {constantType}", variableDeclaration));
                }
                else
                {
                    constantValue = castedConstantValue;
                }
            }
        }
        else
        {
            constantType = new BuiltinType(constantValue.Type);
        }
        variableDeclaration.Type.SetAnalyzedType(constantType);

        return new(constantValue, constantType, variableDeclaration);
    }

    #endregion

    #region GetStruct()

    public enum StructPerfectus
    {
        None,

        /// <summary>
        /// The identifier is good
        /// </summary>
        Identifier,

        /// <summary>
        /// Boundary between good and bad structs
        /// </summary>
        Good,

        // == MATCHED --> Searching for the most relevant struct ==

        /// <summary>
        /// The struct is in the same file
        /// </summary>
        File,
    }

    bool GetStruct(
        string structName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledStruct? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
        => StatementCompiler.GetStruct(
            CompiledStructs,

            structName,
            relevantFile,

            out result,
            out error);

    public static bool GetStruct(
        IEnumerable<CompiledStruct> structs,

        string structName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledStruct? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        CompiledStruct? result_ = default;
        PossibleDiagnostic? error_ = null;

        StructPerfectus perfectus = StructPerfectus.None;

        static StructPerfectus Max(StructPerfectus a, StructPerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledStruct function)
        {
            if (structName is not null &&
                function.Identifier.Content != structName)
            { return false; }

            perfectus = Max(perfectus, StructPerfectus.Identifier);
            return true;
        }

        bool HandleFile(CompiledStruct function)
        {
            if (relevantFile is null ||
                function.File != relevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= StructPerfectus.File)
            {
                error_ = new PossibleDiagnostic($"Struct \"{structName}\" not found: multiple structs matched in the same file");
                // Debugger.Break();
            }

            perfectus = StructPerfectus.File;
            result_ = function;
            return true;
        }

        foreach (CompiledStruct function in structs)
        {
            if (!HandleIdentifier(function))
            { continue; }

            // MATCHED --> Searching for most relevant struct

            if (perfectus < StructPerfectus.Good)
            {
                result_ = function;
                perfectus = StructPerfectus.Good;
            }

            if (!HandleFile(function))
            { continue; }
        }

        if (result_ is not null && perfectus >= StructPerfectus.Good)
        {
            result = result_;
            error = error_;
            return true;
        }

        error = error_ ?? new PossibleDiagnostic($"Struct \"{structName}\" not found");
        result = null;
        return false;
    }

    #endregion

    #region GetAlias()

    public enum AliasPerfectus
    {
        None,

        /// <summary>
        /// The identifier is good
        /// </summary>
        Identifier,

        /// <summary>
        /// Boundary between good and bad structs
        /// </summary>
        Good,

        // == MATCHED --> Searching for the most relevant struct ==

        /// <summary>
        /// The struct is in the same file
        /// </summary>
        File,
    }

    bool GetAlias(
        string aliasName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledAlias? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
        => StatementCompiler.GetAlias(
            CompiledAliases,

            aliasName,
            relevantFile,

            out result,
            out error);

    public static bool GetAlias(
        IEnumerable<CompiledAlias> aliases,

        string aliasName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledAlias? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        CompiledAlias? result_ = default;
        PossibleDiagnostic? error_ = null;

        AliasPerfectus perfectus = AliasPerfectus.None;

        static AliasPerfectus Max(AliasPerfectus a, AliasPerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledAlias _alias)
        {
            if (aliasName is not null &&
                _alias.Identifier.Content != aliasName)
            { return false; }

            perfectus = Max(perfectus, AliasPerfectus.Identifier);
            return true;
        }

        bool HandleFile(CompiledAlias _alias)
        {
            if (relevantFile is null ||
                _alias.File != relevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= AliasPerfectus.File)
            {
                error_ = new PossibleDiagnostic($"Alias \"{aliasName}\" not found: multiple aliases matched in the same file");
                // Debugger.Break();
            }

            perfectus = AliasPerfectus.File;
            result_ = _alias;
            return true;
        }

        foreach (CompiledAlias _alias in aliases)
        {
            if (!HandleIdentifier(_alias))
            { continue; }

            // MATCHED --> Searching for most relevant alias

            if (perfectus < AliasPerfectus.Good)
            {
                result_ = _alias;
                perfectus = AliasPerfectus.Good;
            }

            if (!HandleFile(_alias))
            { continue; }
        }

        if (result_ is not null && perfectus >= AliasPerfectus.Good)
        {
            result = result_;
            error = error_;
            return true;
        }

        error = error_ ?? new PossibleDiagnostic($"Alias \"{aliasName}\" not found");
        result = null;
        return false;
    }

    #endregion

    public enum GlobalVariablePerfectus
    {
        None,

        /// <summary>
        /// The identifier is good
        /// </summary>
        Identifier,

        /// <summary>
        /// Boundary between good and bad global variables
        /// </summary>
        Good,

        // == MATCHED --> Searching for the most relevant global variable ==

        /// <summary>
        /// The global variable is in the same file
        /// </summary>
        File,
    }

    bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledVariableDeclaration? compiledVariable, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (Scope scope in Frames.Last.Scopes)
        {
            foreach (CompiledVariableDeclaration compiledVariable_ in scope.Variables)
            {
                if (compiledVariable_.Identifier == variableName)
                {
                    compiledVariable = compiledVariable_;
                    error = null;
                    return true;
                }
            }
        }

        error = new PossibleDiagnostic($"Variable \"{variableName}\" not found");
        compiledVariable = null;
        return false;
    }

    bool GetGlobalVariable(
        string variableName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledVariableDeclaration? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        CompiledVariableDeclaration? result_ = default;
        PossibleDiagnostic? error_ = null;

        GlobalVariablePerfectus perfectus = GlobalVariablePerfectus.None;

        static GlobalVariablePerfectus Max(GlobalVariablePerfectus a, GlobalVariablePerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledVariableDeclaration variable)
        {
            if (variableName is not null &&
                variable.Identifier != variableName)
            { return false; }

            perfectus = Max(perfectus, GlobalVariablePerfectus.Identifier);
            return true;
        }

        bool HandleFile(CompiledVariableDeclaration variable)
        {
            if (relevantFile is null ||
                variable.Location.File != relevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= GlobalVariablePerfectus.File)
            {
                error_ = new PossibleDiagnostic($"Global variable \"{variableName}\" not found: multiple variables matched in the same file");
                // Debugger.Break();
            }

            perfectus = GlobalVariablePerfectus.File;
            result_ = variable;
            return true;
        }

        foreach (CompiledVariableDeclaration variable in CompiledGlobalVariables)
        {
            if (!HandleIdentifier(variable))
            { continue; }

            // MATCHED --> Searching for most relevant global variable

            if (perfectus < GlobalVariablePerfectus.Good)
            {
                result_ = variable;
                perfectus = GlobalVariablePerfectus.Good;
            }

            if (!HandleFile(variable))
            { continue; }
        }

        if (result_ is not null && perfectus >= GlobalVariablePerfectus.Good)
        {
            result = result_;
            error = error_;
            return true;
        }

        error = error_ ?? new PossibleDiagnostic($"Global variable \"{variableName}\" not found");
        result = null;
        return false;
    }

    bool GetParameter(string parameterName, [NotNullWhen(true)] out CompiledParameter? parameter, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (CompiledParameter compiledParameter in Frames.Last.CompiledParameters)
        {
            if (compiledParameter.Identifier.Content == parameterName)
            {
                parameter = compiledParameter;
                error = null;
                return true;
            }
        }

        error = new PossibleDiagnostic($"Parameter \"{parameterName}\" not found");
        parameter = null;
        return false;
    }

    bool GetInstructionLabel(string identifier, [NotNullWhen(true)] out CompiledInstructionLabelDeclaration? instructionLabel, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (Scope scope in Frames.Last.Scopes)
        {
            foreach (CompiledInstructionLabelDeclaration compiledInstructionLabel in scope.InstructionLabels)
            {
                if (compiledInstructionLabel.Identifier != identifier) continue;
                instructionLabel = compiledInstructionLabel;
                error = null;
                return true;
            }
        }

        foreach (CompiledInstructionLabelDeclaration compiledInstructionLabel in CompiledGlobalInstructionLabels)
        {
            if (compiledInstructionLabel.Identifier != identifier) continue;
            instructionLabel = compiledInstructionLabel;
            error = null;
            return true;
        }

        error = new PossibleDiagnostic($"Instruction label \"{identifier}\" not found");
        instructionLabel = null;
        return false;
    }

    public static bool CanCastImplicitly(GeneralType source, GeneralType destination, StatementWithValue? value, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (destination.SameAs(source))
        { return true; }

        if (destination.SameAs(BasicType.Any))
        { return true; }

        if (value is LiteralStatement literal &&
            literal.Type == LiteralType.String)
        {
            if (destination.Is(out ArrayType? destArrayType) &&
                destArrayType.Of.SameAs(BasicType.U16))
            {
                string literalValue = literal.Value;
                if (destArrayType.Length is null)
                {
                    error = new($"Can't cast literal value \"{literalValue}\" (length of {literalValue.Length}) to stack array \"{destination}\" (without length)");
                    return false;
                }

                if (!destArrayType.ComputedLength.HasValue)
                {
                    error = new($"Can't cast literal value \"{literalValue}\" (length of {literalValue.Length}) to stack array \"{destination}\" (length of <runtime value>)");
                    return false;
                }

                if (literalValue.Length != destArrayType.ComputedLength.Value)
                {
                    error = new($"Can't cast literal value \"{literalValue}\" (length of {literalValue.Length}) to stack array \"{destination}\" (length of \"{destArrayType.Length?.ToString() ?? "null"}\")");
                    return false;
                }

                return true;
            }

            if (destination.Is(out PointerType? pointerType) &&
                pointerType.To.Is(out ArrayType? arrayType) &&
                arrayType.Of.SameAs(BasicType.U16))
            {
                if (arrayType.Length is not null)
                {
                    if (!arrayType.ComputedLength.HasValue)
                    {
                        error = new($"Can't cast literal value \"{literal.Value}\" (length of {literal.Value.Length}) to array \"{destination}\" (length of <runtime value>)");
                        return false;
                    }

                    if (literal.Value.Length != arrayType.ComputedLength.Value)
                    {
                        error = new($"Can't cast literal value \"{literal.Value}\" (length of {literal.Value.Length}) to array \"{destination}\" (length of \"{arrayType.Length?.ToString() ?? "null"}\")");
                        return false;
                    }
                }

                return true;
            }
        }

        {
            if (destination.Is(out PointerType? dstPointer) &&
                source.Is(out PointerType? srcPointer))
            {
                if (dstPointer.To.SameAs(BasicType.Any))
                { return true; }

                if (dstPointer.To.Is(out ArrayType? dstArray) &&
                    srcPointer.To.Is(out ArrayType? srcArray))
                {
                    if (dstArray.ComputedLength.HasValue &&
                        srcArray.ComputedLength.HasValue &&
                        dstArray.ComputedLength.Value != srcArray.ComputedLength.Value)
                    {
                        error = new($"Can't cast an array pointer with length of {dstArray.ComputedLength.Value} to an array pointer with length of {srcArray.ComputedLength.Value}");
                        return false;
                    }

                    if (dstArray.Length is null)
                    { return true; }
                }
            }
        }

        error = new($"Can't cast \"{source}\" to \"{destination}\" implicitly");
        return false;
    }

    public static BitWidth MaxBitWidth(BitWidth a, BitWidth b) => a > b ? a : b;

    #region Initial Value

    static CompiledValue GetInitialValue(BasicType type) => type switch
    {
        BasicType.U8 => new CompiledValue(default(byte)),
        BasicType.I8 => new CompiledValue(default(sbyte)),
        BasicType.U16 => new CompiledValue(default(ushort)),
        BasicType.I16 => new CompiledValue(default(short)),
        BasicType.U32 => new CompiledValue(default(uint)),
        BasicType.I32 => new CompiledValue(default(int)),
        BasicType.F32 => new CompiledValue(default(float)),
        _ => throw new NotImplementedException($"Type \"{type}\" can't have value"),
    };

    static bool GetInitialValue(GeneralType type, out CompiledValue value)
    {
        switch (type.FinalValue)
        {
            case GenericType:
            case StructType:
            case FunctionType:
            case ArrayType:
                value = default;
                return false;
            case BuiltinType builtinType:
                value = GetInitialValue(builtinType.Type);
                return true;
            case PointerType:
                value = new CompiledValue(0);
                return true;
            default: throw new NotImplementedException();
        }
    }

    static CompiledValue GetInitialValue(NumericType type, BitWidth bitWidth) => (type, bitWidth) switch
    {
        (NumericType.Float, BitWidth._32) => new CompiledValue(default(float)),
        (NumericType.SignedInteger, BitWidth._8) => new CompiledValue(default(sbyte)),
        (NumericType.SignedInteger, BitWidth._16) => new CompiledValue(default(short)),
        (NumericType.SignedInteger, BitWidth._32) => new CompiledValue(default(int)),
        (NumericType.SignedInteger, BitWidth._64) => new CompiledValue(default(long)),
        (NumericType.UnsignedInteger, BitWidth._8) => new CompiledValue(default(byte)),
        (NumericType.UnsignedInteger, BitWidth._16) => new CompiledValue(default(ushort)),
        (NumericType.UnsignedInteger, BitWidth._32) => new CompiledValue(default(uint)),
        (NumericType.UnsignedInteger, BitWidth._64) => new CompiledValue(default(ulong)),
        _ => throw new NotImplementedException(),
    };

    #endregion

    #region Find Type

    bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out GeneralType? result)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            return true;
        }

        if (Frames.Last.TypeArguments.TryGetValue(name.Content, out GeneralType? typeArgument))
        {
            result = typeArgument;
            return true;
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            for (int j = 0; j < GenericParameters[i].Length; j++)
            {
                if (GenericParameters[i][j].Content == name.Content)
                {
                    GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                    result = new GenericType(GenericParameters[i][j], relevantFile);
                    return true;
                }
            }
        }

        if (GetAlias(name.Content, relevantFile, out CompiledAlias? alias, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.Type;
            alias.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            // HERE
            result = new AliasType(alias.Value, alias);
            return true;
        }

        if (GetStruct(name.Content, relevantFile, out CompiledStruct? @struct, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.Struct;
            @struct.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            result = new StructType(@struct, relevantFile);
            return true;
        }

        if (GetFunction(FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(name.Content, null, null, relevantFile), out FunctionQueryResult<CompiledFunctionDefinition>? function, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.FunctionName;
            function.Function.References.Add(new Reference<StatementWithValue?>(new Identifier(name, relevantFile), relevantFile));

            result = new FunctionType(function.Function);
            return true;
        }

        if (GetGlobalVariable(name.Content, relevantFile, out CompiledVariableDeclaration? globalVariable, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.VariableName;

            result = globalVariable.Type;
            return true;
        }

        result = null;
        return false;
    }

    bool GetLiteralType(LiteralType literal, [NotNullWhen(true)] out GeneralType? type)
    {
        LiteralType ParseLiteralType(AttributeUsage attribute)
        {
            if (!attribute.TryGetValue(out string? literalTypeName))
            {
                Diagnostics.Add(Diagnostic.Critical($"Attribute \"{attribute.Identifier}\" needs one string argument", attribute));
                return default;
            }
            switch (literalTypeName)
            {
                case "char": return LiteralType.Char;
                case "integer": return LiteralType.Integer;
                case "float": return LiteralType.Float;
                case "string": return LiteralType.String;
                default:
                {
                    Diagnostics.Add(Diagnostic.Critical($"Invalid literal type \"{literalTypeName}\"", attribute.Parameters[0]));
                    return default;
                }
            }
        }

        type = null;

        foreach (CompiledAlias alias in CompiledAliases)
        {
            if (alias.Attributes.TryGetAttribute("UsedByLiteral", out AttributeUsage? attribute))
            {
                LiteralType literalType = ParseLiteralType(attribute);
                if (literalType == literal)
                {
                    if (type is not null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Multiple type definitions defined with attribute [{"UsedByLiteral"}(\"{attribute.Parameters[0].Value}\")]", attribute));
                        return default;
                    }
                    type = new AliasType(alias.Value, alias);
                }
            }
        }

        foreach (CompiledStruct @struct in CompiledStructs)
        {
            if (@struct.Attributes.TryGetAttribute("UsedByLiteral", out AttributeUsage? attribute))
            {
                LiteralType literalType = ParseLiteralType(attribute);
                if (literalType == literal)
                {
                    if (type is not null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Multiple type definitions defined with attribute [{"UsedByLiteral"}(\"{attribute.Parameters[0].Value}\")]", attribute));
                        return default;
                    }
                    type = new StructType(@struct, @struct.File);
                }
            }
        }

        return type is not null;
    }

    static TType OnGotStatementType<TType>(StatementWithValue statement, TType type)
        where TType : GeneralType
    {
        statement.CompiledType = type;
        return type;
    }

    GeneralType FindStatementType(AnyCall anyCall)
    {
        if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            return OnGotStatementType(anyCall, FindStatementType(functionCall));
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);

        if (!prevType.Is(out FunctionType? functionType))
        {
            Diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.PrevStatement));
            return BuiltinType.Void;
        }

        return OnGotStatementType(anyCall, functionType.ReturnType);
    }
    ArrayType FindStatementType(LiteralList list)
    {
        GeneralType? itemType = null;

        for (int i = 0; i < list.Values.Length; i++)
        {
            StatementWithValue item = list.Values[i];
            GeneralType currentItemType = FindStatementType(item, itemType);
            if (itemType is null)
            {
                itemType = currentItemType;
            }
            else if (!currentItemType.SameAs(itemType))
            {
                Diagnostics.Add(Diagnostic.Critical($"List element at index {i} should be a {itemType} and not {currentItemType}", item));
            }
        }

        if (itemType is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Could not infer the list element type", list));
            itemType = BuiltinType.Any;
        }

        return new ArrayType(itemType, new CompiledEvaluatedValue()
        {
            Value = list.Values.Length,
            Location = list.Location,
            Type = ArrayLengthType,
            SaveValue = true,
        });
    }
    GeneralType FindStatementType(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);
        GeneralType indexType = FindStatementType(index.Index);

        // TODO: (index.PrevStatement as IInFile)?.OriginalFile can be null
        if (GetIndexGetter(prevType, indexType, index.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? notFoundError))
        { return OnGotStatementType(index, indexer.Function.Type); }

        if (prevType.Is(out ArrayType? arrayType))
        { return OnGotStatementType(index, arrayType.Of); }

        if (prevType.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        { return arrayType.Of; }

        Diagnostics.Add(notFoundError.ToError(index));
        return BuiltinType.Void;
    }
    GeneralType FindStatementType(FunctionCall functionCall)
    {
        if (functionCall.Identifier.Content == "sizeof")
        {
            if (GetLiteralType(LiteralType.Integer, out GeneralType? integerType))
            { return integerType; }

            Diagnostics.Add(Diagnostic.Warning($"No type defined for integer literals, using the default i32", functionCall));
            return SizeofStatementType;
        }

        if (!GetFunction(functionCall, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(functionCall));
            return BuiltinType.Void;
        }

        // Diagnostics.Add(notFoundError?.SubErrors.FirstOrDefault()?.ToWarning(functionCall));
        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
        return OnGotStatementType(functionCall, result.Function.Type);
    }
    GeneralType FindStatementType(BinaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? _result, out _))
        {
            if (_result.DidReplaceArguments) throw new UnreachableException();
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, _result.Function.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left, expectedType);
        GeneralType rightType = FindStatementType(@operator.Right, expectedType);
        GeneralType result;

        {
            if (leftType.Is(out BuiltinType? leftBType) &&
                rightType.Is(out BuiltinType? rightBType))
            {
                bool isFloat =
                    leftBType.Type == BasicType.F32 ||
                    rightBType.Type == BasicType.F32;

                BitWidth leftBitWidth = leftType.GetBitWidth(this, Diagnostics, @operator.Left);
                BitWidth rightBitWidth = rightType.GetBitWidth(this, Diagnostics, @operator.Right);
                BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

                if (!leftBType.TryGetNumericType(out NumericType leftNType1) ||
                    !rightBType.TryGetNumericType(out NumericType rightNType1))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{leftType}\" \"{@operator.Operator.Content}\" \"{rightType}\"", @operator.Operator, @operator.File));
                    return BuiltinType.Void;
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
                        result = BooleanType;
                        break;

                    case BinaryOperatorCall.LogicalOR:
                    case BinaryOperatorCall.LogicalAND:
                    case BinaryOperatorCall.BitwiseAND:
                    case BinaryOperatorCall.BitwiseOR:
                    case BinaryOperatorCall.BitwiseXOR:
                    case BinaryOperatorCall.BitshiftLeft:
                    case BinaryOperatorCall.BitshiftRight:
                        result = numericResultType;
                        break;

                    case BinaryOperatorCall.Addition:
                    case BinaryOperatorCall.Subtraction:
                    case BinaryOperatorCall.Multiplication:
                    case BinaryOperatorCall.Division:
                    case BinaryOperatorCall.Modulo:
                        result = isFloat ? BuiltinType.F32 : numericResultType;
                        break;

                    default:
                        Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{leftType}\" \"{@operator.Operator.Content}\" \"{rightType}\"", @operator.Operator, @operator.File));
                        result = BuiltinType.Void;
                        break;
                }
                if (expectedType is not null &&
                    CanCastImplicitly(result, expectedType, null, out _))
                { result = expectedType; }
                return OnGotStatementType(@operator, result);
            }
        }

        bool ok = true;

        if (!leftType.TryGetNumericType(out NumericType leftNType))
        {
            Diagnostics.Add(Diagnostic.Critical($"Type \"{leftType}\" aint a numeric type", @operator.Left));
            ok = false;
        }

        if (!rightType.TryGetNumericType(out NumericType rightNType))
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

        if (!ok) { return BuiltinType.Void; }

        CompiledValue leftValue = GetInitialValue(leftNType, leftBitwidth);
        CompiledValue rightValue = GetInitialValue(rightNType, rightBitwidth);

        if (!TryComputeSimple(@operator.Operator.Content, leftValue, rightValue, out CompiledValue predictedValue, out PossibleDiagnostic? evaluateError))
        {
            Diagnostics.Add(evaluateError.ToError(@operator));
            return BuiltinType.Void;
        }

        result = new BuiltinType(predictedValue.Type);

        if (expectedType is not null)
        {
            if (result.SameAs(BasicType.I32) &&
                expectedType.Is<PointerType>())
            { return OnGotStatementType(@operator, expectedType); }
        }

        return OnGotStatementType(@operator, result);
    }
    GeneralType FindStatementType(UnaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result_, out _))
        {
            if (result_.DidReplaceArguments) throw new UnreachableException();
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, result_.Function.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left);

        GeneralType result;
        switch (@operator.Operator.Content)
        {
            case UnaryOperatorCall.LogicalNOT:
            {
                result = BooleanType;
                break;
            }
            case UnaryOperatorCall.BinaryNOT:
            {
                result = leftType;
                break;
            }
            case UnaryOperatorCall.UnaryMinus:
            {
                result = leftType;
                break;
            }
            case UnaryOperatorCall.UnaryPlus:
            {
                result = leftType;
                break;
            }
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
                result = BuiltinType.Void;
                break;
            }
        }

        if (expectedType is not null && expectedType.Is<PointerType>() && result.SameAs(BasicType.I32))
        { return OnGotStatementType(@operator, expectedType); }

        return OnGotStatementType(@operator, result);
    }
    GeneralType FindStatementType(LiteralStatement literal, GeneralType? expectedType)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if (literal.GetInt() is >= byte.MinValue and <= byte.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.GetInt() >= (int)uint.MinValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        return OnGotStatementType(literal, expectedType);
                    }
                }

                if (GetLiteralType(literal.Type, out GeneralType? literalType))
                { return literalType; }

                Diagnostics.Add(Diagnostic.Warning($"No type defined for integer literals, using the default i32", literal));
                return OnGotStatementType(literal, BuiltinType.I32);
            case LiteralType.Float:

                if (GetLiteralType(literal.Type, out literalType))
                { return literalType; }

                Diagnostics.Add(Diagnostic.Warning($"No type defined for float literals, using the default f32", literal));
                return OnGotStatementType(literal, BuiltinType.F32);
            case LiteralType.String:
                if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType) &&
                    pointerType.To.Is(out ArrayType? arrayType) &&
                    arrayType.Of.SameAs(BasicType.U8))
                { return OnGotStatementType(literal, expectedType); }

                if (GetLiteralType(literal.Type, out literalType))
                { return literalType; }

                Diagnostics.Add(Diagnostic.Warning($"No type defined for string literals, using the default u16[]*", literal));
                return OnGotStatementType(literal, new PointerType(new ArrayType(BuiltinType.Char, new CompiledEvaluatedValue()
                {
                    Value = literal.Value.Length + 1,
                    Location = literal.Location,
                    Type = BuiltinType.I32,
                    SaveValue = true
                })));
            case LiteralType.Char:
                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            return OnGotStatementType(literal, expectedType);
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        return OnGotStatementType(literal, expectedType);
                    }
                }

                if (GetLiteralType(literal.Type, out literalType))
                { return literalType; }

                Diagnostics.Add(Diagnostic.Warning($"No type defined for character literals, using the default u16", literal));
                return OnGotStatementType(literal, BuiltinType.Char);
            default:
                throw new UnreachableException($"Unknown literal type \"{literal.Type}\"");
        }
    }
    GeneralType FindStatementType(Identifier identifier, GeneralType? expectedType = null)
    {
        if (identifier.Content.StartsWith('#'))
        {
            identifier.Reference = null;
            identifier.AnalyzedType = TokenAnalyzedType.ConstantName;
            return OnGotStatementType(identifier, BooleanType);
        }

        if (BBLang.Generator.CodeGeneratorForMain.RegisterKeywords.TryGetValue(identifier.Content, out (Register Register, BuiltinType Type) registerKeyword))
        { return registerKeyword.Type; }

        if (GetConstant(identifier.Content, identifier.File, out CompiledVariableConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            identifier.Reference = constant;
            identifier.AnalyzedType = TokenAnalyzedType.ConstantName;
            return OnGotStatementType(identifier, constant.Type);
        }

        if (GetParameter(identifier.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (identifier.Content != StatementKeywords.This)
            { identifier.AnalyzedType = TokenAnalyzedType.ParameterName; }
            identifier.Reference = parameter;
            return OnGotStatementType(identifier, parameter.Type);
        }

        if (GetVariable(identifier.Content, out CompiledVariableDeclaration? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.VariableName;
            identifier.Reference = variable;
            return OnGotStatementType(identifier, variable.Type);
        }

        if (GetGlobalVariable(identifier.Content, identifier.File, out CompiledVariableDeclaration? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.VariableName;
            identifier.Reference = globalVariable;
            return OnGotStatementType(identifier, globalVariable.Type);
        }

        if (GetLocalSymbolType(identifier, out GeneralType? type))
        {
            return OnGotStatementType(identifier, type);
        }

        if (GetFunction(identifier.Content, expectedType, out FunctionQueryResult<CompiledFunctionDefinition>? function, out PossibleDiagnostic? functionNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(identifier, new FunctionType(function.Function));
        }

        if (GetInstructionLabel(identifier.Content, out _, out PossibleDiagnostic? instructionLabelNotFound))
        {
            return OnGotStatementType(identifier, new FunctionType(BuiltinType.Void, Enumerable.Empty<GeneralType>()));
        }

        if (FindType(identifier.Token, identifier.File, out GeneralType? result))
        { return OnGotStatementType(identifier, result); }

        Diagnostics.Add(Diagnostic.Critical(
            $"Symbol \"{identifier.Content}\" not found",
            identifier,
            parameterNotFoundError.ToError(identifier),
            variableNotFoundError.ToError(identifier),
            globalVariableNotFoundError.ToError(identifier),
            constantNotFoundError.ToError(identifier),
            functionNotFoundError.ToError(identifier),
            instructionLabelNotFound.ToError(identifier)));
        return BuiltinType.Void;
    }
    PointerType FindStatementType(AddressGetter addressGetter)
    {
        GeneralType to = FindStatementType(addressGetter.PrevStatement);
        return OnGotStatementType(addressGetter, new PointerType(to));
    }
    GeneralType FindStatementType(Pointer pointer)
    {
        GeneralType to = FindStatementType(pointer.PrevStatement);
        if (!to.Is(out PointerType? pointerType))
        { return OnGotStatementType(pointer, BuiltinType.Any); }
        return OnGotStatementType(pointer, pointerType.To);
    }
    GeneralType FindStatementType(NewInstance newInstance)
    {
        GeneralType type = GeneralType.From(newInstance.Type, FindType);
        newInstance.Type.SetAnalyzedType(type);
        return OnGotStatementType(newInstance, type);
    }
    GeneralType FindStatementType(ConstructorCall constructorCall)
    {
        GeneralType type = GeneralType.From(constructorCall.Type, FindType);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Arguments);

        if (GetConstructor(type, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructorDefinition>? result, out PossibleDiagnostic? notFound))
        {
            constructorCall.Type.SetAnalyzedType(result.Function.Type);
            return OnGotStatementType(constructorCall, result.Function.Type);
        }

        Diagnostics.Add(notFound.ToError(constructorCall));
        return BuiltinType.Void;
    }
    GeneralType FindStatementType(Field field)
    {
        GeneralType prevStatementType = FindStatementType(field.PrevStatement);

        if (prevStatementType.Is<ArrayType>() && field.Identifier.Content == "Length")
        {
            field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            return OnGotStatementType(field, ArrayLengthType);
        }

        while (prevStatementType.Is(out PointerType? pointerType))
        { prevStatementType = pointerType.To; }

        if (prevStatementType.Is(out StructType? structType))
        {
            foreach (CompiledField definedField in structType.Struct.Fields)
            {
                if (definedField.Identifier.Content != field.Identifier.Content) continue;
                field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

                if (structType.Struct.Template is null)
                { return definedField.Type; }

                return GeneralType.InsertTypeParameters(definedField.Type, structType.TypeArguments) ?? definedField.Type;
            }

            Diagnostics.Add(Diagnostic.Critical($"Field definition \"{field.Identifier}\" not found in type \"{prevStatementType}\"", field.Identifier, field.File));
        }

        Diagnostics.Add(Diagnostic.Critical($"Type \"{prevStatementType}\" does not have a field \"{field.Identifier}\"", field));
        return BuiltinType.Void;
    }
    GeneralType FindStatementType(BasicTypeCast @as)
    {
        GeneralType type = GeneralType.From(@as.Type, FindType);
        @as.Type.SetAnalyzedType(type);
        return OnGotStatementType(@as, type);
    }
    GeneralType FindStatementType(ManagedTypeCast @as)
    {
        GeneralType type = GeneralType.From(@as.Type, FindType);
        @as.Type.SetAnalyzedType(type);
        return OnGotStatementType(@as, type);
    }
    GeneralType FindStatementType(ModifiedStatement modifiedStatement, GeneralType? expectedType)
    {
        if (modifiedStatement.Modifier.Equals(ModifierKeywords.Ref))
        {
            return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
        }

        if (modifiedStatement.Modifier.Equals(ModifierKeywords.Temp))
        {
            return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
        }

        Diagnostics.Add(Diagnostic.Critical($"Unimplemented modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, modifiedStatement.File));
        return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
    }
    [return: NotNullIfNotNull(nameof(statement))]
    GeneralType? FindStatementType(StatementWithValue? statement)
        => FindStatementType(statement, null);
    [return: NotNullIfNotNull(nameof(statement))]
    GeneralType? FindStatementType(StatementWithValue? statement, GeneralType? expectedType)
    {
        switch (statement)
        {
            case null: return null;
            case FunctionCall v: return FindStatementType(v);
            case BinaryOperatorCall v: return FindStatementType(v, expectedType);
            case UnaryOperatorCall v: return FindStatementType(v, expectedType);
            case LiteralStatement v: return FindStatementType(v, expectedType);
            case Identifier v: return FindStatementType(v, expectedType);
            case AddressGetter v: return FindStatementType(v);
            case Pointer v: return FindStatementType(v);
            case NewInstance v: return FindStatementType(v);
            case ConstructorCall v: return FindStatementType(v);
            case Field v: return FindStatementType(v);
            case BasicTypeCast v: return FindStatementType(v);
            case ManagedTypeCast v: return FindStatementType(v);
            case IndexCall v: return FindStatementType(v);
            case ModifiedStatement v: return FindStatementType(v, expectedType);
            case AnyCall v: return FindStatementType(v);
            case LiteralList v: return FindStatementType(v);
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Statement \"{statement.GetType().Name}\" does not have a type", statement));
                return BuiltinType.Void;
            }
        }
    }
    ImmutableArray<GeneralType> FindStatementTypes(ImmutableArray<StatementWithValue> statements)
    {
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(statements.Length);
        for (int i = 0; i < statements.Length; i++)
        { result.Add(FindStatementType(statements[i])); }
        return result.ToImmutable();
    }

    #endregion

    #region Inlining

    static IEnumerable<StatementWithValue> InlineMacro(IEnumerable<StatementWithValue> statements, Dictionary<string, StatementWithValue> parameters)
        => statements.Select(statement => InlineMacro(statement, parameters));
    static bool InlineMacro(FunctionThingDefinition function, ImmutableArray<StatementWithValue> parameters, [NotNullWhen(true)] out Statement? inlined)
    {
        Dictionary<string, StatementWithValue> _parameters =
            function.Parameters
            .Select((value, index) => (value.Identifier.Content, parameters[index]))
            .ToDictionary(v => v.Content, v => v.Item2);

        return InlineMacro(function, _parameters, out inlined);
    }
    static bool InlineMacro(FunctionThingDefinition function, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Statement? inlined)
    {
        inlined = default;

        if (function.Block is null ||
            function.Block.Statements.Length == 0)
        { return false; }

        if (function.Block.Statements.Length == 1)
        {
            if (!InlineMacro(function.Block.Statements[0], parameters, out inlined))
            { return false; }
        }
        else
        {
            if (!InlineMacro(function.Block, parameters, out inlined))
            { return false; }
        }

        if (inlined is KeywordCall keywordCall &&
            keywordCall.Identifier.Content == StatementKeywords.Return &&
            keywordCall.Arguments.Length == 1)
        { inlined = keywordCall.Arguments[0]; }

        return true;
    }
    static bool InlineMacro(Block block, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Block? inlined)
    {
        inlined = null;

        List<Statement> statements = new(block.Statements.Length);
        for (int i = 0; i < block.Statements.Length; i++)
        {
            Statement statement = block.Statements[i];
            if (!InlineMacro(statement, parameters, out Statement? inlinedStatement))
            { return false; }

            statements.Add(inlinedStatement);

            if (statement is KeywordCall keywordCall &&
                keywordCall.Identifier.Content == StatementKeywords.Return)
            { break; }
        }
        inlined = new Block(statements.ToArray(), block.Brackets, block.File)
        {
            Semicolon = block.Semicolon,
        };
        return true;
    }
    static BinaryOperatorCall InlineMacro(BinaryOperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            op: operatorCall.Operator,
            left: InlineMacro(operatorCall.Left, parameters),
            right: InlineMacro(operatorCall.Right, parameters),
            file: operatorCall.File)
        {
            SurroundingBracelet = operatorCall.SurroundingBracelet,
            SaveValue = operatorCall.SaveValue,
            Semicolon = operatorCall.Semicolon,
        };
    static UnaryOperatorCall InlineMacro(UnaryOperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            op: operatorCall.Operator,
            left: InlineMacro(operatorCall.Left, parameters),
            file: operatorCall.File)
        {
            SurroundingBracelet = operatorCall.SurroundingBracelet,
            SaveValue = operatorCall.SaveValue,
            Semicolon = operatorCall.Semicolon,
        };
    static KeywordCall InlineMacro(KeywordCall keywordCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            identifier: keywordCall.IdentifierToken,
            arguments: InlineMacro(keywordCall.Arguments, parameters),
            file: keywordCall.File)
        {
            Semicolon = keywordCall.Semicolon,
        };
    static FunctionCall InlineMacro(FunctionCall functionCall, Dictionary<string, StatementWithValue> parameters)
    {
        IEnumerable<StatementWithValue> _parameters = InlineMacro(functionCall.Arguments, parameters);
        StatementWithValue? prevStatement = functionCall.PrevStatement;
        if (prevStatement != null)
        { prevStatement = InlineMacro(prevStatement, parameters); }
        return new FunctionCall(prevStatement, functionCall.Identifier, _parameters, functionCall.Brackets, functionCall.File);
    }
    static AnyCall InlineMacro(AnyCall anyCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(anyCall.PrevStatement, parameters),
            parameters: InlineMacro(anyCall.Arguments, parameters),
            commas: anyCall.Commas,
            brackets: anyCall.Brackets,
            file: anyCall.File)
        {
            SaveValue = anyCall.SaveValue,
            Semicolon = anyCall.Semicolon,
        };
    static ConstructorCall InlineMacro(ConstructorCall constructorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            keyword: constructorCall.Keyword,
            typeName: constructorCall.Type,
            arguments: InlineMacro(constructorCall.Arguments, parameters),
            brackets: constructorCall.Brackets,
            file: constructorCall.File)
        {
            SaveValue = constructorCall.SaveValue,
            Semicolon = constructorCall.Semicolon,
        };
    static NewInstance InlineMacro(NewInstance newInstance, Dictionary<string, StatementWithValue> _)
        => newInstance;
    static LiteralList InlineMacro(LiteralList literalList, Dictionary<string, StatementWithValue> parameters)
        => new(
            values: literalList.Values.Select(v => InlineMacro(v, parameters)),
            brackets: literalList.Brackets,
            file: literalList.File)
        {
            SaveValue = literalList.SaveValue,
            Semicolon = literalList.Semicolon,
            SurroundingBracelet = literalList.SurroundingBracelet,
        };
    static TypeInstance InlineMacro(TypeInstance type, Dictionary<string, StatementWithValue> parameters) => type switch
    {
        TypeInstanceFunction v => new TypeInstanceFunction(
            InlineMacro(v.FunctionReturnType, parameters),
            v.FunctionParameterTypes.Select(p => InlineMacro(p, parameters)),
            v.File
        ),
        TypeInstancePointer v => new TypeInstancePointer(
            InlineMacro(v.To, parameters),
            v.Operator,
            v.File
        ),
        TypeInstanceSimple v => v,
        TypeInstanceStackArray v => new TypeInstanceStackArray(
            InlineMacro(v.StackArrayOf, parameters),
            InlineMacro(v.StackArraySize, parameters),
            v.File
        ),
        _ => throw new NotImplementedException(),
    };
    static bool InlineMacro(Statement statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Statement? inlined)
    {
        inlined = null;

        switch (statement)
        {
            case Block v:
            {
                if (InlineMacro(v, parameters, out Block? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            case KeywordCall v:
            {
                inlined = InlineMacro(v, parameters);
                break;
            }

            case StatementWithValue v:
            {
                inlined = InlineMacro(v, parameters);
                return true;
            }

            case ForLoop v:
            {
                if (InlineMacro(v, parameters, out ForLoop? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            case IfContainer v:
            {
                if (InlineMacro(v, parameters, out IfContainer? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            case AnyAssignment v:
            {
                if (InlineMacro(v, parameters, out AnyAssignment? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            case VariableDeclaration v:
            {
                if (InlineMacro(v, parameters, out VariableDeclaration? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            case WhileLoop v:
            {
                if (InlineMacro(v, parameters, out WhileLoop? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }

            default: throw new NotImplementedException(statement.GetType().ToString());
        }

        return false;
    }
    static bool InlineMacro(IfContainer statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out IfContainer? inlined)
    {
        inlined = null;
        BaseBranch[] branches = new BaseBranch[statement.Branches.Length];
        for (int i = 0; i < branches.Length; i++)
        {
            if (!InlineMacro(statement.Branches[i], parameters, out BaseBranch? inlinedBranch))
            { return false; }
            branches[i] = inlinedBranch;
        }
        inlined = new IfContainer(branches, statement.File)
        { Semicolon = statement.Semicolon, };
        return true;
    }
    static bool InlineMacro(BaseBranch statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out BaseBranch? inlined) => statement switch
    {
        IfBranch v => InlineMacro(v, parameters, out inlined),
        ElseIfBranch v => InlineMacro(v, parameters, out inlined),
        ElseBranch v => InlineMacro(v, parameters, out inlined),
        _ => throw new UnreachableException()
    };
    static bool InlineMacro(IfBranch statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out BaseBranch? inlined)
    {
        inlined = null;

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Block, parameters, out Statement? block))
        { return false; }

        inlined = new IfBranch(
             keyword: statement.Keyword,
             condition: condition,
             block: block,
             file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(ElseIfBranch statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out BaseBranch? inlined)
    {
        inlined = null;

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Block, parameters, out Statement? block))
        { return false; }

        inlined = new ElseIfBranch(
            keyword: statement.Keyword,
            condition: condition,
            block: block,
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(ElseBranch statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out BaseBranch? inlined)
    {
        inlined = null;

        if (!InlineMacro(statement.Block, parameters, out Statement? block))
        { return false; }

        inlined = new ElseBranch(
            keyword: statement.Keyword,
            block: block,
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(WhileLoop statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out WhileLoop? inlined)
    {
        inlined = null;

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Block, parameters, out Statement? block))
        { return false; }

        inlined = new WhileLoop(
            keyword: statement.KeywordToken,
            condition: condition,
            block: block,
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(ForLoop statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out ForLoop? inlined)
    {
        inlined = null;

        VariableDeclaration? variableDeclaration = null;
        if (statement.VariableDeclaration is not null &&
            !InlineMacro(statement.VariableDeclaration, parameters, out variableDeclaration))
        { return false; }

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Expression, parameters, out AnyAssignment? expression))
        { return false; }

        if (!InlineMacro(statement.Block, parameters, out Block? block))
        { return false; }

        inlined = new ForLoop(
            keyword: statement.KeywordToken,
            variableDeclaration: variableDeclaration,
            condition: condition,
            expression: expression,
            block: block,
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(AnyAssignment statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out AnyAssignment? inlined)
    {
        inlined = null;

        switch (statement)
        {
            case Assignment v:
            {
                if (InlineMacro(v, parameters, out Assignment? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }
            case ShortOperatorCall v:
            {
                if (InlineMacro(v, parameters, out ShortOperatorCall? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }
            case CompoundAssignment v:
            {
                if (InlineMacro(v, parameters, out CompoundAssignment? inlined_))
                {
                    inlined = inlined_;
                    return true;
                }
                break;
            }
            default: throw new UnreachableException();
        }

        return false;
    }
    static bool InlineMacro(Assignment statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Assignment? inlined)
    {
        inlined = null;
        if (statement.Left is Identifier identifier &&
            parameters.ContainsKey(identifier.Content))
        { return false; }

        inlined = new Assignment(
            @operator: statement.Operator,
            left: InlineMacro(statement.Left, parameters),
            right: InlineMacro(statement.Right, parameters),
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(ShortOperatorCall statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out ShortOperatorCall? inlined)
    {
        inlined = null;
        if (statement.Left is Identifier identifier &&
            parameters.ContainsKey(identifier.Content))
        { return false; }

        inlined = new ShortOperatorCall(
            op: statement.Operator,
            left: statement.Left,
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(CompoundAssignment statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out CompoundAssignment? inlined)
    {
        inlined = null;
        if (statement.Left is Identifier identifier &&
            parameters.ContainsKey(identifier.Content))
        { return false; }

        inlined = new CompoundAssignment(
            @operator: statement.Operator,
            left: statement.Left,
            right: InlineMacro(statement.Right, parameters),
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static bool InlineMacro(VariableDeclaration statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out VariableDeclaration? inlined)
    {
        inlined = null;

        if (parameters.ContainsKey(statement.Identifier.Content))
        { return false; }

        inlined = new VariableDeclaration(
            attributes: statement.Attributes,
            modifiers: statement.Modifiers,
            type: statement.Type,
            variableName: statement.Identifier,
            initialValue: InlineMacro(statement.InitialValue, parameters),
            file: statement.File)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }
    static Pointer InlineMacro(Pointer statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            operatorToken: statement.Operator,
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };
    static AddressGetter InlineMacro(AddressGetter statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            operatorToken: statement.Operator,
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };
    static StatementWithValue InlineMacro(Identifier statement, Dictionary<string, StatementWithValue> parameters)
    {
        if (parameters.TryGetValue(statement.Content, out StatementWithValue? inlinedStatement))
        { return inlinedStatement; }
        return statement;
    }
    static LiteralStatement InlineMacro(LiteralStatement statement, Dictionary<string, StatementWithValue> _)
        => statement;
    static Field InlineMacro(Field statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            identifier: statement.Identifier,
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };
    static IndexCall InlineMacro(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            indexStatement: InlineMacro(statement.Index, parameters),
            brackets: statement.Brackets,
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };
    static BasicTypeCast InlineMacro(BasicTypeCast statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            keyword: statement.Keyword,
            type: statement.Type,
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,

            CompiledType = statement.CompiledType,
        };
    static ManagedTypeCast InlineMacro(ManagedTypeCast statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            type: statement.Type,
            brackets: statement.Brackets,
            file: statement.File)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,

            CompiledType = statement.CompiledType,
            SurroundingBracelet = statement.SurroundingBracelet,
        };
    static ModifiedStatement InlineMacro(ModifiedStatement modifiedStatement, Dictionary<string, StatementWithValue> parameters)
        => new(
            modifier: modifiedStatement.Modifier,
            statement: InlineMacro(modifiedStatement.Statement, parameters),
            file: modifiedStatement.File)
        {
            SaveValue = modifiedStatement.SaveValue,
            Semicolon = modifiedStatement.Semicolon,

            CompiledType = modifiedStatement.CompiledType,
        };
    [return: NotNullIfNotNull(nameof(statement))]
    static StatementWithValue? InlineMacro(StatementWithValue? statement, Dictionary<string, StatementWithValue> parameters) => statement switch
    {
        null => null,
        Identifier v => InlineMacro(v, parameters),
        BinaryOperatorCall v => InlineMacro(v, parameters),
        UnaryOperatorCall v => InlineMacro(v, parameters),
        FunctionCall v => InlineMacro(v, parameters),
        AnyCall v => InlineMacro(v, parameters),
        Pointer v => InlineMacro(v, parameters),
        AddressGetter v => InlineMacro(v, parameters),
        LiteralStatement v => InlineMacro(v, parameters),
        Field v => InlineMacro(v, parameters),
        IndexCall v => InlineMacro(v, parameters),
        BasicTypeCast v => InlineMacro(v, parameters),
        ManagedTypeCast v => InlineMacro(v, parameters),
        ModifiedStatement v => InlineMacro(v, parameters),
        TypeStatement v => v,
        ConstructorCall v => InlineMacro(v, parameters),
        NewInstance v => InlineMacro(v, parameters),
        LiteralList v => InlineMacro(v, parameters),
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };

    #endregion

    #region Inlining

    class InlineContext
    {
        public required ImmutableDictionary<string, CompiledPassedArgument> Arguments { get; init; }
        public List<CompiledPassedArgument> InlinedArguments { get; } = new();
        public Dictionary<CompiledVariableDeclaration, CompiledVariableDeclaration> VariableReplacements { get; } = new();
    }

    static bool Inline(IEnumerable<CompiledPassedArgument> statements, InlineContext context, [NotNullWhen(true)] out ImmutableArray<CompiledPassedArgument> inlined)
    {
        inlined = ImmutableArray<CompiledPassedArgument>.Empty;
        ImmutableArray<CompiledPassedArgument>.Builder res = ImmutableArray.CreateBuilder<CompiledPassedArgument>();

        foreach (CompiledPassedArgument statement in statements)
        {
            if (!Inline(statement.Value, context, out CompiledStatementWithValue? v)) return false;
            res.Add(new CompiledPassedArgument()
            {
                Value = v,
                Cleanup = new CompiledCleanup()
                {
                    Location = statement.Cleanup.Location,
                    TrashType = statement.Cleanup.TrashType,
                },
                Location = statement.Location,
                SaveValue = statement.SaveValue,
                Type = statement.Type,
            });
        }

        inlined = res.ToImmutable();
        return true;
    }

    static bool InlineFunction(CompiledBlock _block, InlineContext context, [NotNullWhen(true)] out CompiledStatement? inlined)
    {
        if (_block.Statements.Length == 1)
        {
            if (!Inline(_block.Statements[0], context, out inlined))
            { return false; }
        }
        else
        {
            if (!Inline(_block, context, out inlined))
            { return false; }
        }

        if (inlined is CompiledReturn compiledReturn &&
            compiledReturn.Value is not null)
        { inlined = compiledReturn.Value; }

        return true;
    }

    static bool Inline(CompiledSizeof statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledBinaryOperatorCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Left, context, out CompiledStatementWithValue? inlinedLeft)) return false;
        if (!Inline(statement.Right, context, out CompiledStatementWithValue? inlinedRight)) return false;

        inlined = new CompiledBinaryOperatorCall()
        {
            Left = inlinedLeft,
            Right = inlinedRight,
            Operator = statement.Operator,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledUnaryOperatorCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Left, context, out CompiledStatementWithValue? inlinedLeft)) return false;

        inlined = new CompiledUnaryOperatorCall()
        {
            Left = inlinedLeft,
            Operator = statement.Operator,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledEvaluatedValue statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(RegisterGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledVariableGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!context.VariableReplacements.TryGetValue(statement.Variable, out CompiledVariableDeclaration? replacedVariable))
        {
            if (statement.Variable.IsGlobal)
            {
                replacedVariable = statement.Variable;
            }
            else
            {
                return false;
            }
        }

        inlined = new CompiledVariableGetter()
        {
            Variable = replacedVariable,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledParameterGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (context.Arguments.TryGetValue(statement.Variable.Identifier.Content, out CompiledPassedArgument? inlinedArgument))
        {
            if (inlinedArgument.Cleanup.Deallocator is not null ||
                inlinedArgument.Cleanup.Destructor is not null)
            { return false; }

            context.InlinedArguments.Add(inlinedArgument);
            inlined = inlinedArgument.Value;
            return true;
        }

        return false;
    }
    static bool Inline(FunctionAddressGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(InstructionLabelAddressGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledFieldGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Object, context, out CompiledStatementWithValue? inlinedObject)) return false;

        while (inlinedObject is CompiledAddressGetter addressGetter)
        { inlinedObject = addressGetter.Of; }

        inlined = new CompiledFieldGetter()
        {
            Object = inlinedObject,
            Field = statement.Field,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledIndexGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Base, context, out CompiledStatementWithValue? inlinedBase)) return false;
        if (!Inline(statement.Index, context, out CompiledStatementWithValue? inlinedIndex)) return false;

        inlined = new CompiledIndexGetter()
        {
            Base = inlinedBase,
            Index = inlinedIndex,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledAddressGetter statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Of, context, out CompiledStatementWithValue? inlinedOf)) return false;

        inlined = new CompiledAddressGetter()
        {
            Of = inlinedOf,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledPointer statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.To, context, out CompiledStatementWithValue? inlinedTo)) return false;

        inlined = new CompiledPointer()
        {
            To = inlinedTo,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledStackAllocation statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledConstructorCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Object, context, out CompiledStatementWithValue? inlinedObject)) return false;
        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledPassedArgument> inlinedArguments)) return false;

        inlined = new CompiledConstructorCall()
        {
            Object = inlinedObject,
            Arguments = inlinedArguments,
            Function = statement.Function,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledTypeCast statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledFakeTypeCast()
        {
            Value = inlinedValue,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledFakeTypeCast statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledFakeTypeCast()
        {
            Value = inlinedValue,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledRuntimeCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledPassedArgument> inlinedArguments)) return false;
        if (!Inline(statement.Function, context, out CompiledStatementWithValue? inlinedFunction)) return false;

        inlined = new CompiledRuntimeCall()
        {
            Function = inlinedFunction,
            Arguments = inlinedArguments,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledFunctionCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledPassedArgument> inlinedArguments)) return false;

        inlined = new CompiledFunctionCall()
        {
            Function = statement.Function,
            Arguments = inlinedArguments,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledExternalFunctionCall statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledPassedArgument> inlinedArguments)) return false;

        inlined = new CompiledExternalFunctionCall()
        {
            Declaration = statement.Declaration,
            Function = statement.Function,
            Arguments = inlinedArguments,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledStatementWithValueThatActuallyDoesntHaveValue statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        if (!Inline(statement.Statement, context, out CompiledStatement? inlinedStatement)) return false;

        inlined = new CompiledStatementWithValueThatActuallyDoesntHaveValue()
        {
            Statement = inlinedStatement,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledStringInstance statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledStackStringInstance statement, InlineContext context, out CompiledStatementWithValue inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledVariableDeclaration statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        if (!Inline(statement.InitialValue, context, out CompiledStatementWithValue? inlinedValue)) return false;

        CompiledVariableDeclaration _inlined = new()
        {
            InitialValue = inlinedValue,
            Cleanup = statement.Cleanup,
            Identifier = statement.Identifier,
            IsGlobal = statement.IsGlobal,
            Type = statement.Type,
            Location = statement.Location,
        };
        context.VariableReplacements[statement] = _inlined;
        inlined = _inlined;
        return true;
    }
    static bool Inline(CompiledReturn statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledReturn()
        {
            Value = inlinedValue,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledCrash statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledCrash()
        {
            Value = inlinedValue,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledBreak statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledDelete statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledDelete()
        {
            Value = inlinedValue,
            Cleanup = statement.Cleanup,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledGoto statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledGoto()
        {
            Value = inlinedValue,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(RegisterSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new RegisterSetter()
        {
            Value = inlinedValue,
            Register = statement.Register,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledVariableSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!context.VariableReplacements.TryGetValue(statement.Variable, out CompiledVariableDeclaration? replacedVariable))
        {
            if (statement.Variable.IsGlobal)
            {
                replacedVariable = statement.Variable;
            }
            else
            {
                return false;
            }
        }
        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledVariableSetter()
        {
            Value = inlinedValue,
            Variable = replacedVariable,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledParameterSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        return false;
    }
    static bool Inline(CompiledIndirectSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.AddressValue, context, out CompiledStatementWithValue? inlinedAddressValue)) return false;
        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledIndirectSetter()
        {
            AddressValue = inlinedAddressValue,
            Value = inlinedValue,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledFieldSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Object, context, out CompiledStatementWithValue? inlinedObject)) return false;
        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledFieldSetter()
        {
            Object = inlinedObject,
            Value = inlinedValue,
            Field = statement.Field,
            Type = statement.Type,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledIndexSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Base, context, out CompiledStatementWithValue? inlinedBase)) return false;
        if (!Inline(statement.Index, context, out CompiledStatementWithValue? inlinedIndex)) return false;
        if (!Inline(statement.Value, context, out CompiledStatementWithValue? inlinedValue)) return false;

        inlined = new CompiledIndexSetter()
        {
            Base = inlinedBase,
            Index = inlinedIndex,
            Value = inlinedValue,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledWhileLoop statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Condition, context, out CompiledStatementWithValue? inlinedCondition)) return false;
        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;

        inlined = new CompiledWhileLoop()
        {
            Condition = inlinedCondition,
            Body = inlinedBody,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledForLoop statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        CompiledStatement? inlinedVariableDeclaration = null;
        if (statement.VariableDeclaration is not null && !Inline(statement.VariableDeclaration, context, out inlinedVariableDeclaration)) return false;
        if (!Inline(statement.Condition, context, out CompiledStatementWithValue? inlinedCondition)) return false;
        if (!Inline(statement.Expression, context, out CompiledStatement? inlinedExpression)) return false;
        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;

        inlined = new CompiledForLoop()
        {
            VariableDeclaration = inlinedVariableDeclaration,
            Condition = inlinedCondition,
            Expression = inlinedExpression,
            Body = inlinedBody,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledIf statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Condition, context, out CompiledStatementWithValue? inlinedCondition)) return false;
        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;
        if (!Inline(statement.Next, context, out CompiledStatement? inlinedNext)) return false;

        inlined = new CompiledIf()
        {
            Condition = inlinedCondition,
            Body = inlinedBody,
            Next = (CompiledBranch?)inlinedNext,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledElse statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;

        inlined = new CompiledElse()
        {
            Body = inlinedBody,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledBlock statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        List<CompiledStatement> statements = new(statement.Statements.Length);
        foreach (CompiledStatement v in statement.Statements)
        {
            if (!Inline(v, context, out CompiledStatement? inlinedStatement))
            { return false; }

            statements.Add(inlinedStatement);

            if (v is CompiledReturn)
            { break; }
        }

        inlined = new CompiledBlock()
        {
            Statements = statements.ToImmutableArray(),
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledInstructionLabelDeclaration statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(EmptyStatement statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        return true;
    }

    static bool Inline(CompiledStatement? statement, InlineContext context, [NotNullIfNotNull(nameof(statement))] out CompiledStatement? inlined)
    {
        if (statement is null)
        {
            inlined = null;
            return true;
        }

        switch (statement)
        {
            case CompiledVariableDeclaration v: return Inline(v, context, out inlined);
            case CompiledReturn v: return Inline(v, context, out inlined);
            case CompiledCrash v: return Inline(v, context, out inlined);
            case CompiledBreak v: return Inline(v, context, out inlined);
            case CompiledDelete v: return Inline(v, context, out inlined);
            case CompiledGoto v: return Inline(v, context, out inlined);
            case RegisterSetter v: return Inline(v, context, out inlined);
            case CompiledVariableSetter v: return Inline(v, context, out inlined);
            case CompiledParameterSetter v: return Inline(v, context, out inlined);
            case CompiledIndirectSetter v: return Inline(v, context, out inlined);
            case CompiledFieldSetter v: return Inline(v, context, out inlined);
            case CompiledIndexSetter v: return Inline(v, context, out inlined);
            case CompiledWhileLoop v: return Inline(v, context, out inlined);
            case CompiledForLoop v: return Inline(v, context, out inlined);
            case CompiledIf v: return Inline(v, context, out inlined);
            case CompiledElse v: return Inline(v, context, out inlined);
            case CompiledBlock v: return Inline(v, context, out inlined);
            case CompiledInstructionLabelDeclaration v: return Inline(v, context, out inlined);
            case EmptyStatement v: return Inline(v, context, out inlined);
            case CompiledStatementWithValue v:
                if (Inline(v, context, out CompiledStatementWithValue inlinedWithValue))
                {
                    inlined = inlinedWithValue;
                    return true;
                }
                else
                {
                    inlined = inlinedWithValue;
                    return false;
                }
            default: throw new NotImplementedException(statement.GetType().Name);
        }
    }
    static bool Inline(CompiledStatementWithValue? statement, InlineContext context, [NotNullIfNotNull(nameof(statement))] out CompiledStatementWithValue? inlined)
    {
        if (statement is null)
        {
            inlined = null;
            return true;
        }

        return statement switch
        {
            CompiledSizeof v => Inline(v, context, out inlined),
            CompiledBinaryOperatorCall v => Inline(v, context, out inlined),
            CompiledUnaryOperatorCall v => Inline(v, context, out inlined),
            CompiledEvaluatedValue v => Inline(v, context, out inlined),
            RegisterGetter v => Inline(v, context, out inlined),
            CompiledVariableGetter v => Inline(v, context, out inlined),
            CompiledParameterGetter v => Inline(v, context, out inlined),
            FunctionAddressGetter v => Inline(v, context, out inlined),
            InstructionLabelAddressGetter v => Inline(v, context, out inlined),
            CompiledFieldGetter v => Inline(v, context, out inlined),
            CompiledIndexGetter v => Inline(v, context, out inlined),
            CompiledAddressGetter v => Inline(v, context, out inlined),
            CompiledPointer v => Inline(v, context, out inlined),
            CompiledStackAllocation v => Inline(v, context, out inlined),
            CompiledConstructorCall v => Inline(v, context, out inlined),
            CompiledTypeCast v => Inline(v, context, out inlined),
            CompiledFakeTypeCast v => Inline(v, context, out inlined),
            CompiledRuntimeCall v => Inline(v, context, out inlined),
            CompiledFunctionCall v => Inline(v, context, out inlined),
            CompiledExternalFunctionCall v => Inline(v, context, out inlined),
            CompiledStatementWithValueThatActuallyDoesntHaveValue v => Inline(v, context, out inlined),
            CompiledStringInstance v => Inline(v, context, out inlined),
            CompiledStackStringInstance v => Inline(v, context, out inlined),

            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };
    }

    #endregion

    #region Control Flow Usage

    [Flags]
    public enum ControlFlowUsage
    {
        None = 0x0,
        Return = 0x1,
        ConditionalReturn = 0x2,
        Break = 0x4,
    }

    static ControlFlowUsage FindControlFlowUsage(IEnumerable<Statement> statements, bool inDepth = false)
    {
        ControlFlowUsage result = ControlFlowUsage.None;
        foreach (Statement statement in statements)
        { result |= FindControlFlowUsage(statement, inDepth); }
        return result;
    }
    static ControlFlowUsage FindControlFlowUsage(Statement statement, bool inDepth = false) => statement switch
    {
        Block v => FindControlFlowUsage(v.Statements, true),
        KeywordCall v => FindControlFlowUsage(v, inDepth),
        WhileLoop v => FindControlFlowUsage(v.Block, true),
        ForLoop v => FindControlFlowUsage(v.Block.Statements, true),
        IfContainer v => FindControlFlowUsage(v.Branches, true),
        BaseBranch v => FindControlFlowUsage(v.Block, true),

        Assignment => ControlFlowUsage.None,
        VariableDeclaration => ControlFlowUsage.None,
        AnyCall => ControlFlowUsage.None,
        ShortOperatorCall => ControlFlowUsage.None,
        CompoundAssignment => ControlFlowUsage.None,
        BinaryOperatorCall => ControlFlowUsage.None,
        Identifier => ControlFlowUsage.None,
        ConstructorCall => ControlFlowUsage.None,
        Field => ControlFlowUsage.None,

        _ => throw new NotImplementedException(statement.GetType().Name),
    };
    static ControlFlowUsage FindControlFlowUsage(KeywordCall statement, bool inDepth = false)
    {
        switch (statement.Identifier.Content)
        {
            case StatementKeywords.Return:
            {
                if (inDepth) return ControlFlowUsage.ConditionalReturn;
                else return ControlFlowUsage.Return;
            }

            case StatementKeywords.Break:
            {
                return ControlFlowUsage.Break;
            }

            case StatementKeywords.Delete:
            case StatementKeywords.Crash:
                return ControlFlowUsage.None;

            default: throw new NotImplementedException(statement.ToString());
        }
    }

    public static ControlFlowUsage FindControlFlowUsage(IEnumerable<CompiledStatement> statements, bool inDepth = false)
    {
        ControlFlowUsage result = ControlFlowUsage.None;
        foreach (CompiledStatement statement in statements)
        { result |= FindControlFlowUsage(statement, inDepth); }
        return result;
    }
    public static ControlFlowUsage FindControlFlowUsage(CompiledStatement statement, bool inDepth = false) => statement switch
    {
        CompiledBlock v => FindControlFlowUsage(v.Statements, true),
        CompiledReturn => inDepth ? ControlFlowUsage.ConditionalReturn : ControlFlowUsage.Return,
        CompiledBreak => ControlFlowUsage.Break,
        CompiledWhileLoop v => FindControlFlowUsage(v.Body, true),
        CompiledForLoop v => FindControlFlowUsage(v.Body, true),
        CompiledIf v => FindControlFlowUsage(v.Body, true) | (v.Next is null ? ControlFlowUsage.None : FindControlFlowUsage(v.Next, true)),
        CompiledElse v => FindControlFlowUsage(v.Body, true),

        _ => ControlFlowUsage.None,
    };

    #endregion

    #region Compile Time Evaluation

    static bool TryComputeSimple(string @operator, CompiledValue left, [NotNullWhen(true)] out CompiledValue result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        // TODO: wtf
        error = null;
        result = @operator switch
        {
            UnaryOperatorCall.LogicalNOT => !left,
            UnaryOperatorCall.BinaryNOT => ~left,
            UnaryOperatorCall.UnaryPlus => +left,
            UnaryOperatorCall.UnaryMinus => -left,

            _ => throw new NotImplementedException($"Unknown unary operator \"{@operator}\""),
        };
        return true;
    }

    static bool TryComputeSimple(string @operator, CompiledValue left, CompiledValue right, [NotNullWhen(true)] out CompiledValue result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        // TODO: wtf
        try
        {
            error = null;
            result = @operator switch
            {
                BinaryOperatorCall.Addition => left + right,
                BinaryOperatorCall.Subtraction => left - right,
                BinaryOperatorCall.Multiplication => left * right,
                BinaryOperatorCall.Division => left / right,
                BinaryOperatorCall.Modulo => left % right,

                BinaryOperatorCall.LogicalAND => new CompiledValue((bool)left && (bool)right),
                BinaryOperatorCall.LogicalOR => new CompiledValue((bool)left || (bool)right),

                BinaryOperatorCall.BitwiseAND => left & right,
                BinaryOperatorCall.BitwiseOR => left | right,
                BinaryOperatorCall.BitwiseXOR => left ^ right,

                BinaryOperatorCall.BitshiftLeft => left << right,
                BinaryOperatorCall.BitshiftRight => left >> right,

                BinaryOperatorCall.CompLT => new CompiledValue(left < right),
                BinaryOperatorCall.CompGT => new CompiledValue(left > right),
                BinaryOperatorCall.CompEQ => new CompiledValue(left == right),
                BinaryOperatorCall.CompNEQ => new CompiledValue(left != right),
                BinaryOperatorCall.CompLEQ => new CompiledValue(left <= right),
                BinaryOperatorCall.CompGEQ => new CompiledValue(left >= right),

                _ => throw new NotImplementedException($"Unknown binary operator \"{@operator}\""),
            };
            return true;
        }
        catch (Exception)
        {
            if (left.Type != right.Type)
            {
                result = default;
                error = new PossibleDiagnostic($"Can do {@operator} operator to type {left.Type} and {right.Type}");
                return false;
            }
            throw;
        }
    }

    static bool TryComputeSimple(LiteralStatement literal, out CompiledValue value)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
                value = new CompiledValue(literal.GetInt());
                return true;
            case LiteralType.Float:
                value = new CompiledValue(literal.GetFloat());
                return true;
            case LiteralType.Char:
                if (literal.Value.Length != 1)
                { throw new InternalExceptionWithoutContext($"Invalid character literal"); }

                value = new CompiledValue(literal.Value[0]);
                return true;
            case LiteralType.String:
            default:
                value = CompiledValue.Null;
                return false;
        }
    }
    static bool TryComputeSimple(BinaryOperatorCall @operator, out CompiledValue value)
    {
        if (!TryComputeSimple(@operator.Left, out CompiledValue leftValue))
        {
            value = CompiledValue.Null;
            return false;
        }

        string op = @operator.Operator.Content;

        if (TryComputeSimple(@operator.Right, out CompiledValue rightValue) &&
            TryComputeSimple(op, leftValue, rightValue, out value, out _))
        { return true; }

        switch (op)
        {
            case "&&":
            {
                if (!leftValue)
                {
                    value = new CompiledValue(false);
                    return true;
                }
                break;
            }
            case "||":
            {
                if (leftValue)
                {
                    value = new CompiledValue(true);
                    return true;
                }
                break;
            }
            default:
                value = CompiledValue.Null;
                return false;
        }

        value = leftValue;
        return true;
    }
    static bool TryComputeSimple(UnaryOperatorCall @operator, out CompiledValue value)
    {
        if (!TryComputeSimple(@operator.Left, out CompiledValue leftValue))
        {
            value = CompiledValue.Null;
            return false;
        }

        switch (@operator.Operator.Content)
        {
            case UnaryOperatorCall.LogicalNOT:
                value = !leftValue;
                return true;
            case UnaryOperatorCall.BinaryNOT:
                value = ~leftValue;
                return true;
            case UnaryOperatorCall.UnaryPlus:
                value = +leftValue;
                return true;
            case UnaryOperatorCall.UnaryMinus:
                value = -leftValue;
                return true;
            default:
                value = leftValue;
                return true;
        }
    }
    static bool TryComputeSimple(IndexCall indexCall, out CompiledValue value)
    {
        if (indexCall.PrevStatement is LiteralStatement literal &&
            literal.Type == LiteralType.String &&
            TryComputeSimple(indexCall.Index, out CompiledValue index))
        {
            if (index == literal.Value.Length)
            { value = new CompiledValue('\0'); }
            else
            { value = new CompiledValue(literal.Value[(int)index]); }
            return true;
        }

        value = CompiledValue.Null;
        return false;
    }
    public static bool TryComputeSimple(StatementWithValue? statement, out CompiledValue value)
    {
        value = CompiledValue.Null;
        return statement switch
        {
            LiteralStatement v => TryComputeSimple(v, out value),
            BinaryOperatorCall v => TryComputeSimple(v, out value),
            UnaryOperatorCall v => TryComputeSimple(v, out value),
            IndexCall v => TryComputeSimple(v, out value),
            _ => false,
        };
    }

    abstract class RuntimeStatement2 :
        IPositioned,
        IInFile,
        ILocated
    {
        public abstract Position Position { get; }
        public abstract Uri File { get; }

        public Location Location => new(Position, File);
    }

    abstract class RuntimeStatement2<TOriginal> : RuntimeStatement2
        where TOriginal : CompiledStatement
    {
        public TOriginal Original { get; }

        public override Position Position => Original.Location.Position;

        protected RuntimeStatement2(TOriginal original)
        {
            Original = original;
        }
    }

    class RuntimeFunctionCall2 : RuntimeStatement2<CompiledExternalFunctionCall>
    {
        public ImmutableArray<CompiledValue> Parameters { get; }
        public override Uri File => Original.Location.File;

        public RuntimeFunctionCall2(ImmutableArray<CompiledValue> parameters, CompiledExternalFunctionCall original) : base(original)
        {
            Parameters = parameters;
        }
    }

    class EvaluationScope
    {
        public readonly Dictionary<CompiledVariableDeclaration, CompiledValue> Variables = new();
    }

    class EvaluationFrame
    {
        public readonly CompiledFunction Function;
        public CompiledValue? ReturnValue;
        public readonly Dictionary<string, CompiledValue> Parameters = new();
        public readonly Stack<EvaluationScope> Scopes = new();

        public EvaluationFrame(CompiledFunction function) => Function = function;
    }

    class EvaluationContext
    {
        public readonly Stack<EvaluationFrame> Frames;

        public readonly List<RuntimeStatement2> RuntimeStatements;

        public bool IsReturning;
        public bool IsBreaking;

        public static EvaluationContext Empty => new();

        public EvaluationContext()
        {
            Frames = new();
            RuntimeStatements = new();
        }

        public bool TryGetVariable(CompiledVariableDeclaration name, out CompiledValue value)
        {
            value = default;

            if (Frames.LastOrDefault is null)
            { return false; }

            foreach (EvaluationScope scope in Frames.LastOrDefault.Scopes)
            {
                if (!scope.Variables.TryGetValue(name, out value)) continue;
                return true;
            }

            return false;
        }

        public bool TryGetParameter(string name, out CompiledValue value)
        {
            value = default;

            if (Frames.LastOrDefault is null)
            { return false; }

            return Frames.LastOrDefault.Parameters.TryGetValue(name, out value);
        }

        public bool TrySetVariable(CompiledVariableDeclaration name, CompiledValue value)
        {
            if (Frames.LastOrDefault is null)
            { return false; }

            foreach (EvaluationScope scope in Frames.LastOrDefault.Scopes)
            {
                if (!scope.Variables.ContainsKey(name)) continue;
                scope.Variables[name] = value;
                return true;
            }

            return false;
        }

        public bool TrySetParameter(string name, CompiledValue value)
        {
            if (Frames.LastOrDefault is null)
            { return false; }

            if (Frames.LastOrDefault.Parameters.ContainsKey(name))
            { return false; }

            Frames.LastOrDefault.Parameters[name] = value;
            return true;
        }

        public void PushScope()
        {
            if (Frames.LastOrDefault is null) return;
            Frames.LastOrDefault.Scopes.Push(new());
        }

        public void PopScope()
        {
            if (Frames.LastOrDefault is null) return;
            Frames.LastOrDefault.Scopes.Pop();
        }
    }

    bool TryCompute(CompiledPointer pointer, EvaluationContext context, out CompiledValue value)
    {
        if (pointer.To is CompiledAddressGetter addressGetter)
        { return TryCompute(addressGetter.Of, context, out value); }

        value = CompiledValue.Null;
        return false;
    }
    bool TryCompute(CompiledBinaryOperatorCall @operator, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(@operator.Left, context, out CompiledValue leftValue))
        {
            value = CompiledValue.Null;
            return false;
        }

        string op = @operator.Operator;

        if (TryCompute(@operator.Right, context, out CompiledValue rightValue) &&
            TryComputeSimple(op, leftValue, rightValue, out value, out _))
        {
            return true;
        }

        switch (op)
        {
            case "&&":
            {
                if (!(bool)leftValue)
                {
                    value = new CompiledValue(false);
                    return true;
                }
                break;
            }
            case "||":
            {
                if ((bool)leftValue)
                {
                    value = new CompiledValue(true);
                    return true;
                }
                break;
            }
            default:
                value = CompiledValue.Null;
                return false;
        }

        value = leftValue;
        return true;
    }
    bool TryCompute(CompiledUnaryOperatorCall @operator, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(@operator.Left, context, out CompiledValue leftValue))
        {
            value = CompiledValue.Null;
            return false;
        }

        switch (@operator.Operator)
        {
            case UnaryOperatorCall.LogicalNOT:
                value = !leftValue;
                return true;
            case UnaryOperatorCall.BinaryNOT:
                value = ~leftValue;
                return true;
            case UnaryOperatorCall.UnaryPlus:
                value = +leftValue;
                return true;
            case UnaryOperatorCall.UnaryMinus:
                value = -leftValue;
                return true;
            default:
                value = CompiledValue.Null;
                return false;
        }
    }
    bool TryCompute(CompiledEvaluatedValue literal, EvaluationContext context, out CompiledValue value)
    {
        value = literal.Value;
        return true;
    }
    bool TryCompute(CompiledFunctionCall functionCall, EvaluationContext context, out CompiledValue value)
    {
        value = CompiledValue.Null;

        ICompiledFunctionDefinition? function = functionCall.Function;

        if (!TryCompute(functionCall.Arguments, context, out ImmutableArray<CompiledValue> parameters))
        {
            return false;
        }

        if (function is IExternalFunctionDefinition externalFunctionDefinition &&
            externalFunctionDefinition.ExternalFunctionName is not null)
        {
            Debugger.Break();
            return false;
        }

        CompiledFunction? found = GeneratedFunctions.FirstOrDefault(v => v.Function == function);

        if (found is null)
        {
            return false;
        }

        if (TryEvaluate(found, parameters, context, out CompiledValue? returnValue, out RuntimeStatement2[]? runtimeStatements) &&
            returnValue.HasValue &&
            runtimeStatements.Length == 0)
        {
            value = returnValue.Value;
            return true;
        }

        return false;
    }
    bool TryCompute(CompiledSizeof functionCall, EvaluationContext context, out CompiledValue value)
    {
        if (!FindSize(functionCall.Of, out int size, out _))
        {
            value = CompiledValue.Null;
            return false;
        }

        value = new CompiledValue(size);
        return true;
    }
    bool TryCompute(CompiledVariableGetter identifier, EvaluationContext context, out CompiledValue value)
    {
        if (!context.TryGetVariable(identifier.Variable, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        return true;
    }
    bool TryCompute(CompiledParameterGetter identifier, EvaluationContext context, out CompiledValue value)
    {
        if (!context.TryGetParameter(identifier.Variable.Identifier.Content, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        return true;
    }
    bool TryCompute(CompiledFakeTypeCast typeCast, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(typeCast.Value, context, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        if (!typeCast.Type.Is(out BuiltinType? builtinType)) return false;

        value = CompiledValue.CreateUnsafe(value.I32, builtinType.RuntimeType);
        return true;
    }
    bool TryCompute(CompiledTypeCast typeCast, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(typeCast.Value, context, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        if (!typeCast.Type.Is(out BuiltinType? builtinType)) return false;
        if (!value.TryCast(builtinType.RuntimeType, out CompiledValue casted)) return false;

        value = casted;
        return true;
    }
    bool TryCompute(CompiledIndexGetter indexCall, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(indexCall.Index, context, out CompiledValue index))
        {
            value = CompiledValue.Null;
            return false;
        }

        if (indexCall.Base is CompiledStringInstance stringInstance)
        {
            if (index == stringInstance.Value.Length)
            { value = new CompiledValue('\0'); }
            else
            { value = new CompiledValue(stringInstance.Value[(int)index]); }
            return true;
        }

        if (indexCall.Base is CompiledStackStringInstance stackString)
        {
            if (index == stackString.Value.Length)
            { value = new CompiledValue('\0'); }
            else
            { value = new CompiledValue(stackString.Value[(int)index]); }
            return true;
        }

        if (indexCall.Base is CompiledLiteralList listLiteral &&
            TryCompute(listLiteral.Values[(int)index], context, out value))
        {
            return true;
        }

        value = CompiledValue.Null;
        return false;
    }
    bool TryCompute(FunctionAddressGetter functionAddressGetter, EvaluationContext context, out CompiledValue value)
    {
        if (functionAddressGetter.Function.InstructionOffset != InvalidFunctionAddress)
        {
            value = functionAddressGetter.Function.InstructionOffset;
            return true;
        }

        value = CompiledValue.Null;
        return false;
    }

    bool TryCompute([NotNullWhen(true)] CompiledStatementWithValue? statement, out CompiledValue value)
        => TryCompute(statement, EvaluationContext.Empty, out value);
    bool TryCompute(IEnumerable<CompiledStatementWithValue>? statements, EvaluationContext context, [NotNullWhen(true)] out ImmutableArray<CompiledValue> values)
    {
        if (statements is null)
        {
            values = ImmutableArray<CompiledValue>.Empty;
            return false;
        }

        ImmutableArray<CompiledValue>.Builder result = ImmutableArray.CreateBuilder<CompiledValue>();
        foreach (CompiledStatementWithValue statement in statements)
        {
            if (!TryCompute(statement, context, out CompiledValue value))
            {
                values = ImmutableArray<CompiledValue>.Empty;
                return false;
            }

            result.Add(value);
        }

        values = result.ToImmutable();
        return true;
    }
    bool TryCompute([NotNullWhen(true)] CompiledStatementWithValue? statement, EvaluationContext context, out CompiledValue value)
    {
        value = CompiledValue.Null;
        return statement switch
        {
            CompiledEvaluatedValue v => TryCompute(v, context, out value),
            CompiledBinaryOperatorCall v => TryCompute(v, context, out value),
            CompiledUnaryOperatorCall v => TryCompute(v, context, out value),
            CompiledPointer v => TryCompute(v, context, out value),
            CompiledFunctionCall v => TryCompute(v, context, out value),
            CompiledSizeof v => TryCompute(v, context, out value),
            CompiledVariableGetter v => TryCompute(v, context, out value),
            CompiledParameterGetter v => TryCompute(v, context, out value),
            CompiledFakeTypeCast v => TryCompute(v, context, out value),
            CompiledTypeCast v => TryCompute(v, context, out value),
            CompiledIndexGetter v => TryCompute(v, context, out value),
            CompiledPassedArgument v => TryCompute(v.Value, context, out value),
            FunctionAddressGetter v => TryCompute(v, context, out value),

            CompiledStringInstance => false,
            CompiledStackStringInstance => false,
            CompiledExternalFunctionCall => false,
            CompiledRuntimeCall => false,
            CompiledFieldGetter => false,
            CompiledStackAllocation => false,
            CompiledConstructorCall => false,
            CompiledAddressGetter => false,
            CompiledLiteralList => false,
            CompiledStatementWithValueThatActuallyDoesntHaveValue => false,
            RegisterGetter => false,
            InstructionLabelAddressGetter => false,
            null => false,

            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };
    }

    bool TryEvaluate(ICompiledFunctionDefinition function, ImmutableArray<CompiledPassedArgument> parameters, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out RuntimeStatement2[]? runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        CompiledFunction? found = GeneratedFunctions.FirstOrDefault(v => v.Function == function);

        if (found is null)
        { return false; }

        if (TryCompute(parameters, context, out ImmutableArray<CompiledValue> parameterValues) &&
            TryEvaluate(found, parameterValues, context, out value, out runtimeStatements))
        { return true; }

        return false;
    }
    bool TryEvaluate(ICompiledFunctionDefinition function, ImmutableArray<CompiledStatementWithValue> parameters, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out RuntimeStatement2[]? runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        CompiledFunction? found = GeneratedFunctions.FirstOrDefault(v => v.Function == function);

        if (found is null)
        {
            return false;
        }

        if (TryCompute(parameters, context, out ImmutableArray<CompiledValue> parameterValues) &&
            TryEvaluate(found, parameterValues, context, out value, out runtimeStatements))
        { return true; }

        return false;
    }
    bool TryEvaluate(CompiledFunction function, ImmutableArray<CompiledValue> parameterValues, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out RuntimeStatement2[]? runtimeStatements)
    {
        value = null;
        runtimeStatements = null;

        {
            CompiledValue[] castedParameterValues = new CompiledValue[parameterValues.Length];
            for (int i = 0; i < parameterValues.Length; i++)
            {
                if (!parameterValues[i].TryCast(function.Function.ParameterTypes[i], out CompiledValue castedValue))
                {
                    // Debugger.Break();
                    return false;
                }

                if (!function.Function.ParameterTypes[i].SameAs(castedValue.Type))
                {
                    // Debugger.Break();
                    return false;
                }

                castedParameterValues[i] = castedValue;
            }
            parameterValues = castedParameterValues.ToImmutableArray();
        }

        if (function.Function.ReturnSomething)
        {
            if (!function.Function.Type.Is<BuiltinType>())
            { return false; }
        }

        if (context.Frames.Count > 8)
        { return false; }

        using (StackAuto<EvaluationFrame> _ = context.Frames.PushAuto(new EvaluationFrame(function)))
        {
            for (int i = 0; i < parameterValues.Length; i++)
            {
                context.Frames.Last.Parameters.Add(function.Function.Parameters[i].Identifier.Content, parameterValues[i]);
            }

            bool success = TryEvaluate(function.Body, context);

            if (!success)
            { return false; }

            if (function.Function.ReturnSomething)
            {
                if (context.Frames?.LastOrDefault is null)
                { throw new InternalExceptionWithoutContext(); }
                if (!context.Frames.LastOrDefault.ReturnValue.HasValue)
                { return false; }
                value = context.Frames.LastOrDefault.ReturnValue.Value;
            }
        }

        runtimeStatements = context.RuntimeStatements.ToArray();

        return true;
    }
    bool TryEvaluate(CompiledWhileLoop whileLoop, EvaluationContext context)
    {
        int iterations = 64;

        while (true)
        {
            if (!TryCompute(whileLoop.Condition, context, out CompiledValue condition))
            { return false; }

            if (!condition)
            { break; }

            if (iterations-- < 0)
            { return false; }

            if (!TryEvaluate(whileLoop.Body, context))
            {
                context.IsBreaking = false;
                return false;
            }

            if (context.IsBreaking)
            { break; }

            context.IsBreaking = false;
        }

        context.IsBreaking = false;
        return true;
    }
    bool TryEvaluate(CompiledForLoop forLoop, EvaluationContext context)
    {
        int iterations = 5048;

        context.PushScope();

        if (forLoop.VariableDeclaration is not null && !TryEvaluate(forLoop.VariableDeclaration, context))
        { return false; }

        while (true)
        {
            if (!TryCompute(forLoop.Condition, context, out CompiledValue condition))
            { return false; }

            if (!condition)
            { break; }

            if (iterations-- < 0)
            { return false; }

            if (!TryEvaluate(forLoop.Body, context))
            {
                context.IsBreaking = false;
                return false;
            }

            if (context.IsBreaking)
            { break; }

            if (!TryEvaluate(forLoop.Expression, context))
            { return false; }

            context.IsBreaking = false;
        }

        context.IsBreaking = false;
        context.PopScope();
        return true;
    }
    bool TryEvaluate(CompiledIf ifContainer, EvaluationContext context)
    {
        CompiledBranch? current = ifContainer;
        while (true)
        {
            switch (current)
            {
                case CompiledIf _if:
                {
                    if (!TryCompute(_if.Condition, context, out CompiledValue condition))
                    { return false; }

                    if (condition)
                    { return TryEvaluate(_if.Body, context); }

                    current = _if.Next;
                    break;
                }
                case CompiledElse _else:
                {
                    return TryEvaluate(_else.Body, context);
                }
                default:
                    throw new NotImplementedException();
            }
            if (current is null) break;
        }

        return false;
    }
    bool TryEvaluate(CompiledBlock block, EvaluationContext context)
    {
        context.PushScope();
        bool result = TryEvaluate(block.Statements, context);
        context.PopScope();
        return result;
    }
    bool TryEvaluate(CompiledVariableDeclaration variableDeclaration, EvaluationContext context)
    {
        CompiledValue value;

        if (context.Frames.LastOrDefault is null)
        { return false; }

        if (variableDeclaration.InitialValue is null &&
            variableDeclaration.Type.ToString() != StatementKeywords.Var)
        {
            if (!GetInitialValue(variableDeclaration.Type, out value))
            { return false; }
        }
        else
        {
            if (!TryCompute(variableDeclaration.InitialValue, context, out value))
            { return false; }
        }

        if (!(context.Frames.LastOrDefault.Scopes.LastOrDefault?.Variables.TryAdd(variableDeclaration, value) ?? false))
        { return false; }

        return true;
    }
    bool TryEvaluate(CompiledVariableSetter anyAssignment, EvaluationContext context)
    {
        if (!TryCompute(anyAssignment.Value, context, out CompiledValue value))
        { return false; }

        if (!context.TrySetVariable(anyAssignment.Variable, value))
        { return false; }

        return true;
    }
    bool TryEvaluate(CompiledParameterSetter anyAssignment, EvaluationContext context)
    {
        if (!TryCompute(anyAssignment.Value, context, out CompiledValue value))
        { return false; }

        if (!context.TrySetParameter(anyAssignment.Variable.Identifier.Content, value))
        { return false; }

        return true;
    }
    bool TryEvaluate(CompiledFieldSetter anyAssignment, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledIndexSetter anyAssignment, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledIndirectSetter anyAssignment, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledReturn keywordCall, EvaluationContext context)
    {
        context.IsReturning = true;

        if (keywordCall.Value is not null)
        {
            if (!TryCompute(keywordCall.Value, context, out CompiledValue returnValue))
            { return false; }

            context.Frames.Last.ReturnValue = returnValue;
        }

        return true;
    }
    bool TryEvaluate(CompiledBreak keywordCall, EvaluationContext context)
    {
        context.IsBreaking = true;
        return true;
    }
    bool TryEvaluate(CompiledCrash keywordCall, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledGoto keywordCall, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledDelete keywordCall, EvaluationContext context)
    {
        return false;
    }
    bool TryEvaluate(CompiledStatement statement, EvaluationContext context) => statement switch
    {
        CompiledStatementWithValue v => TryCompute(v, context, out _),
        CompiledBlock v => TryEvaluate(v, context),
        CompiledVariableDeclaration v => TryEvaluate(v, context),
        CompiledWhileLoop v => TryEvaluate(v, context),
        CompiledForLoop v => TryEvaluate(v, context),
        CompiledVariableSetter v => TryEvaluate(v, context),
        CompiledParameterSetter v => TryEvaluate(v, context),
        CompiledFieldSetter v => TryEvaluate(v, context),
        CompiledIndexSetter v => TryEvaluate(v, context),
        CompiledIndirectSetter v => TryEvaluate(v, context),
        CompiledReturn v => TryEvaluate(v, context),
        CompiledCrash v => TryEvaluate(v, context),
        CompiledBreak v => TryEvaluate(v, context),
        CompiledGoto v => TryEvaluate(v, context),
        CompiledDelete v => TryEvaluate(v, context),
        CompiledIf v => TryEvaluate(v, context),
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };
    bool TryEvaluate(ImmutableArray<CompiledStatement> statements, EvaluationContext context)
    {
        foreach (CompiledStatement statement in statements)
        {
            if (!TryEvaluate(statement, context))
            { return false; }

            if (context.IsReturning || context.IsBreaking)
            { break; }
        }
        return true;
    }

    #endregion

    #region Find Size

    static bool FindSize(GeneralType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        PointerType v => FindSize(v, out size, out error),
        ArrayType v => FindSize(v, out size, out error),
        FunctionType v => FindSize(v, out size, out error),
        StructType v => FindSize(v, out size, out error),
        GenericType v => FindSize(v, out size, out error),
        BuiltinType v => FindSize(v, out size, out error),
        AliasType v => FindSize(v, out size, out error),
        _ => throw new NotImplementedException(),
    };
    static bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = new PossibleDiagnostic($"Pointer size is runtime dependent");
        return false;
    }
    static bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = new PossibleDiagnostic($"Pointer size is runtime dependent");
        return false;
    }
    static bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error)) return false;

        if (type.Length is not CompiledEvaluatedValue evaluatedStatement)
        {
            error = new PossibleDiagnostic($"Can't compute the array type's length");
            return false;
        }

        size = elementSize * (int)evaluatedStatement.Value;
        error = null;
        return true;
    }
    static bool FindSize(StructType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = 0;

        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!FindSize(fieldType, out int fieldSize, out error)) return false;
            size += fieldSize;
        }

        error = null;
        return true;
    }
    static bool FindSize(GenericType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        throw new InvalidOperationException($"Generic type doesn't have a size");
    }
    static bool FindSize(BuiltinType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = default;
        switch (type.Type)
        {
            case BasicType.Void: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.Any: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.U8: size = 1; return true;
            case BasicType.I8: size = 1; return true;
            case BasicType.U16: size = 2; return true;
            case BasicType.I16: size = 2; return true;
            case BasicType.U32: size = 4; return true;
            case BasicType.I32: size = 4; return true;
            case BasicType.U64: size = 8; return true;
            case BasicType.I64: size = 8; return true;
            case BasicType.F32: size = 4; return true;
            default: throw new UnreachableException();
        }
    }
    static bool FindSize(AliasType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        return FindSize(type.Value, out size, out error);
    }

    #endregion

    #region IsObservable

    [Flags]
    enum StatementComplexity
    {
        None = 0,
        Volatile = 1,
        Complex = 2,
        Bruh = 4,
    }

    StatementComplexity GetStatementComplexity(IEnumerable<CompiledStatementWithValue> statements)
    {
        StatementComplexity res = StatementComplexity.None;
        foreach (CompiledStatementWithValue statement in statements) res |= GetStatementComplexity(statement);
        return res;
    }

    StatementComplexity GetStatementComplexity(GeneralType statement) => statement.FinalValue switch
    {
        ArrayType v => (v.Length is null || v.ComputedLength.HasValue) ? StatementComplexity.None : GetStatementComplexity(v.Length),
        BuiltinType v => StatementComplexity.None,
        FunctionType v => StatementComplexity.None,
        GenericType v => StatementComplexity.Bruh,
        PointerType v => StatementComplexity.None,
        StructType v => StatementComplexity.None,
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };

    StatementComplexity GetStatementComplexity(CompiledStatementWithValue statement) => statement switch
    {
        CompiledSizeof v => GetStatementComplexity(v.Of),
        CompiledPassedArgument v => GetStatementComplexity(v.Value) | ((v.Cleanup.Destructor is not null || v.Cleanup.Deallocator is not null) ? StatementComplexity.Complex | StatementComplexity.Volatile : StatementComplexity.None),
        CompiledBinaryOperatorCall v => GetStatementComplexity(v.Left) | GetStatementComplexity(v.Right) | StatementComplexity.Complex,
        CompiledUnaryOperatorCall v => GetStatementComplexity(v.Left) | StatementComplexity.Complex,
        CompiledEvaluatedValue => StatementComplexity.None,
        RegisterGetter => StatementComplexity.None,
        CompiledVariableGetter => StatementComplexity.None,
        CompiledParameterGetter => StatementComplexity.None,
        FunctionAddressGetter => StatementComplexity.None,
        InstructionLabelAddressGetter => StatementComplexity.None,
        CompiledFieldGetter v => GetStatementComplexity(v.Object),
        CompiledIndexGetter v => GetStatementComplexity(v.Base) | GetStatementComplexity(v.Index),
        CompiledAddressGetter v => GetStatementComplexity(v.Of),
        CompiledPointer v => GetStatementComplexity(v.To),
        CompiledStackAllocation => StatementComplexity.Bruh,
        CompiledConstructorCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledTypeCast v => GetStatementComplexity(v.Value) | StatementComplexity.Complex,
        CompiledFakeTypeCast v => GetStatementComplexity(v.Value),
        CompiledRuntimeCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledFunctionCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledExternalFunctionCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Volatile,
        CompiledStatementWithValueThatActuallyDoesntHaveValue => StatementComplexity.Bruh,
        CompiledStringInstance => StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledStackStringInstance => StatementComplexity.Bruh,
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };

    #endregion

    #region Visit

    static IEnumerable<CompiledStatement> Visit(IEnumerable<GeneralType> type)
    {
        foreach (GeneralType v in type)
        {
            foreach (CompiledStatement v2 in Visit(v)) yield return v2;
        }
    }

    static IEnumerable<CompiledStatement> Visit(GeneralType? type)
    {
        switch (type)
        {
            case BuiltinType:
                break;
            case AliasType v:
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case PointerType v:
                foreach (CompiledStatement v2 in Visit(v.To)) yield return v2;
                break;
            case FunctionType v:
                foreach (CompiledStatement v2 in Visit(v.ReturnType)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Parameters)) yield return v2;
                break;
            case GenericType:
                break;
            case StructType v:
                foreach (CompiledStatement v2 in Visit(v.TypeArguments.Values)) yield return v2;
                break;
            case ArrayType v:
                foreach (CompiledStatement v2 in Visit(v.Of)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Length)) yield return v2;
                break;
        }
    }

    public static IEnumerable<CompiledStatement> Visit(IEnumerable<CompiledStatement> type)
    {
        foreach (CompiledStatement v in type)
        {
            foreach (CompiledStatement v2 in Visit(v)) yield return v2;
        }
    }

    public static IEnumerable<CompiledStatement> Visit(CompiledStatement? statement)
    {
        switch (statement)
        {
            case CompiledVariableDeclaration v:
                yield return v;
                if (v.InitialValue is not null)
                {
                    foreach (CompiledStatement v2 in Visit(v.InitialValue)) yield return v2;
                }
                foreach (CompiledStatement v2 in Visit(v.Type)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Cleanup)) yield return v2;
                break;
            case CompiledSizeof v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Of)) yield return v2;
                break;
            case CompiledReturn v:
                yield return v;
                if (v.Value is not null)
                {
                    foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                }
                break;
            case CompiledCrash v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledBreak v:
                yield return v;
                break;
            case CompiledDelete v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Cleanup)) yield return v2;
                break;
            case CompiledGoto v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledBinaryOperatorCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Left)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Right)) yield return v2;
                break;
            case CompiledUnaryOperatorCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Left)) yield return v2;
                break;
            case CompiledEvaluatedValue v:
                yield return v;
                break;
            case RegisterGetter v:
                yield return v;
                break;
            case CompiledVariableGetter v:
                yield return v;
                break;
            case CompiledParameterGetter v:
                yield return v;
                break;
            case FunctionAddressGetter v:
                yield return v;
                break;
            case InstructionLabelAddressGetter v:
                yield return v;
                break;
            case CompiledFieldGetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Object)) yield return v2;
                break;
            case CompiledIndexGetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Base)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Index)) yield return v2;
                break;
            case CompiledAddressGetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Of)) yield return v2;
                break;
            case RegisterSetter v:
                yield return v;
                break;
            case CompiledVariableSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledParameterSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledIndirectSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.AddressValue)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledFieldSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Object)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledIndexSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Base)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Index)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledPointer v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.To)) yield return v2;
                break;
            case CompiledWhileLoop v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Condition)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Body)) yield return v2;
                break;
            case CompiledForLoop v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.VariableDeclaration)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Condition)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Expression)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Body)) yield return v2;
                break;
            case CompiledIf v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Condition)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Body)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Next)) yield return v2;
                break;
            case CompiledElse v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Body)) yield return v2;
                break;
            case CompiledStackAllocation v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Type)) yield return v2;
                break;
            case CompiledConstructorCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Type)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Arguments)) yield return v2;
                break;
            case CompiledTypeCast v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Type)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledFakeTypeCast v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledRuntimeCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Function)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Arguments)) yield return v2;
                break;
            case CompiledFunctionCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Arguments)) yield return v2;
                break;
            case CompiledExternalFunctionCall v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Arguments)) yield return v2;
                break;
            case CompiledBlock v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Statements)) yield return v2;
                break;
            case CompiledInstructionLabelDeclaration v:
                yield return v;
                break;
            case CompiledStatementWithValueThatActuallyDoesntHaveValue v:
                yield return v;
                break;
            case CompiledStringInstance v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Allocator)) yield return v2;
                break;
            case CompiledStackStringInstance v:
                yield return v;
                break;
            case EmptyStatement v:
                yield return v;
                break;
            case CompiledPassedArgument v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Cleanup)) yield return v2;
                break;
            case CompiledCleanup v:
                yield return v;
                break;
            case null:
                break;
            default:
                throw new NotImplementedException($"Unimplemented statement \"{statement.GetType().Name}\"");
        }
    }

    #endregion

    static ImmutableArray<CompiledStatement> ReduceStatements<TStatement>(ImmutableArray<TStatement> statements, bool forceDiscardValue = false)
        where TStatement : CompiledStatement
    {
        ImmutableArray<CompiledStatement>.Builder result = ImmutableArray.CreateBuilder<CompiledStatement>();

        foreach (TStatement statement in statements)
        {
            result.AddRange(ReduceStatements(statement, forceDiscardValue));
        }

        return result.ToImmutable();
    }

    static ImmutableArray<CompiledStatement> ReduceStatements(CompiledStatement statement, bool forceDiscardValue = false)
    {
        if (statement is CompiledBlock compiledBlock)
        {
            if (!compiledBlock.Statements.Any(v => v is CompiledVariableDeclaration or CompiledInstructionLabelDeclaration))
            {
                return ReduceStatements(compiledBlock.Statements);
            }
        }

        if (statement is not CompiledStatementWithValue statementWithValue)
        {
            return ImmutableArray.Create(statement);
        }

        if (statementWithValue.SaveValue && !forceDiscardValue)
        {
            return ImmutableArray.Create(statement);
        }

        return statementWithValue switch
        {
            CompiledPassedArgument v => ReduceStatements(v.Value, true),
            CompiledBinaryOperatorCall v => ReduceStatements(v.Left, true).AddRange(ReduceStatements(v.Right, true)),
            CompiledUnaryOperatorCall v => ReduceStatements(v.Left, true),
            CompiledIndexGetter v => ReduceStatements(v.Base, true).AddRange(ReduceStatements(v.Index, true)),
            CompiledAddressGetter v => ReduceStatements(v.Of, true),
            CompiledPointer v => ReduceStatements(v.To, true),
            CompiledConstructorCall v => ReduceStatements(v.Arguments, true),
            CompiledTypeCast v => ReduceStatements(v.Value, true),
            CompiledFakeTypeCast v => ReduceStatements(v.Value, true),
            CompiledStatementWithValueThatActuallyDoesntHaveValue v => ReduceStatements(v.Statement),

            CompiledRuntimeCall or
            CompiledFunctionCall or
            CompiledExternalFunctionCall => ImmutableArray.Create(statement),

            CompiledSizeof or
            CompiledEvaluatedValue or
            RegisterGetter or
            CompiledVariableGetter or
            CompiledParameterGetter or
            FunctionAddressGetter or
            InstructionLabelAddressGetter or
            CompiledFieldGetter or
            CompiledStackAllocation or
            CompiledStringInstance or
            CompiledStackStringInstance => ImmutableArray<CompiledStatement>.Empty,
            _ => throw new NotImplementedException(),
        };
    }
}
