using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

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
    readonly Stack<CompiledVariableDefinition> CompiledGlobalVariables = new();

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
    readonly ImmutableHashSet<string> PreprocessorVariables;
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

    bool StatementCanBeDeallocated(ArgumentExpression statement, out bool explicitly)
    {
        if (statement.Modifier?.Content == ModifierKeywords.Temp)
        {
            if (statement.Value is
                LiteralExpression or
                BinaryOperatorCallExpression or
                UnaryOperatorCallExpression)
            {
                Diagnostics.Add(Diagnostic.Hint($"Unnecessary explicit temp modifier (\"{statement.Value.GetType().Name}\" statements are implicitly deallocated)", statement.Modifier, statement.File));
            }

            explicitly = true;
            return true;
        }

        if (statement.Value is LiteralExpression)
        {
            explicitly = false;
            return true;
        }

        if (statement.Value is BinaryOperatorCallExpression)
        {
            explicitly = false;
            return true;
        }

        if (statement.Value is UnaryOperatorCallExpression)
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

    public static bool AllowDeallocate(GeneralType type) => type.Is<PointerType>() || (type.Is(out FunctionType? functionType) && functionType.HasClosure);

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

    bool GetLocalSymbolType(IdentifierExpression symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (GetVariable(symbolName.Content, out CompiledVariableDefinition? variable, out _))
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

    [DebuggerStepThrough]
    Functions<CompiledFunctionDefinition> GetFunctions() => new()
    {
        Compiled = CompiledFunctions,
        Compilable = CompilableFunctions,
    };

    [DebuggerStepThrough]
    Functions<CompiledOperatorDefinition> GetOperators() => new()
    {
        Compiled = CompiledOperators,
        Compilable = CompilableOperators,
    };

    [DebuggerStepThrough]
    Functions<CompiledGeneralFunctionDefinition> GetGeneralFunctions() => new()
    {
        Compiled = CompiledGeneralFunctions,
        Compilable = CompilableGeneralFunctions,
    };

    [DebuggerStepThrough]
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
        BinaryOperatorCallExpression @operator,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperatorDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledOperatorDefinition>>? addCompilable = null)
    {
        FunctionQuery<CompiledOperatorDefinition, string, Token, ArgumentExpression> query = FunctionQuery.Create<CompiledOperatorDefinition, string, Token>(
            @operator.Operator.Content,
            @operator.Arguments.ToImmutableArray(ArgumentExpression.Wrap),
            FunctionArgumentConverter,
            relevantFile,
            null,
            addCompilable);
        return GetFunction<CompiledOperatorDefinition, string, Token, ArgumentExpression>(
            GetOperators(),
            "operator",
            null,

            query,

            out result,
            out error
        ) && result.Success;
    }

    bool GetOperator(
        UnaryOperatorCallExpression @operator,
        Uri relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperatorDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledOperatorDefinition>>? addCompilable = null)
    {
        FunctionQuery<CompiledOperatorDefinition, string, Token, ArgumentExpression> query = FunctionQuery.Create<CompiledOperatorDefinition, string, Token>(
            @operator.Operator.Content,
            @operator.Arguments.ToImmutableArray(ArgumentExpression.Wrap),
            FunctionArgumentConverter,
            relevantFile,
            null,
            addCompilable);
        return GetFunction<CompiledOperatorDefinition, string, Token, ArgumentExpression>(
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
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
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
            FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(identifier, functionType.Parameters, null, functionType.ReturnType, addCompilable),
            out result,
            out error
        );
    }

    bool GetFunction(
        AnyCallExpression call,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        result = null;
        error = null;

        if (!call.ToFunctionCall(out FunctionCallExpression? functionCall))
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
        FunctionCallExpression functionCallStatement,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunctionDefinition>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error,
        Action<CompliableTemplate<CompiledFunctionDefinition>>? addCompilable = null)
    {
        string identifier = functionCallStatement.Identifier.Content;
        FunctionQuery<CompiledFunctionDefinition, string, Token, ArgumentExpression> query = FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(
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

    bool CompileConstant(VariableDefinition variableDeclaration, [NotNullWhen(true)] out CompiledVariableConstant? result)
    {
        result = null;
        variableDeclaration.Identifier.AnalyzedType = TokenAnalyzedType.ConstantName;

        if (GetConstant(variableDeclaration.Identifier.Content, variableDeclaration.File, out _, out _))
        { Diagnostics.Add(Diagnostic.Critical($"Constant \"{variableDeclaration.Identifier}\" already defined", variableDeclaration.Identifier, variableDeclaration.File)); }

        CompileVariableAttributes(variableDeclaration);

        GeneralType? constantType = null;
        if (variableDeclaration.Type != StatementKeywords.Var)
        {
            CompileType(variableDeclaration.Type, out constantType, Diagnostics);
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
        }

        if (variableDeclaration.InitialValue is null)
        {
            Diagnostics.Add(Diagnostic.Critical($"Constant value must have initial value", variableDeclaration));
            constantValue = default;
        }
        else
        {
            CompileExpression(variableDeclaration.InitialValue, out CompiledExpression? compiledInitialValue, constantType);
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
                    Diagnostics.Add(Diagnostic.Error($"Can't cast constant value {constantValue} of type \"{constantValue.Type}\" to {constantType}", variableDeclaration));
                }
                else
                {
                    constantValue = castedConstantValue;
                }
            }
        }
        else
        {
            if (!CompileType(constantValue.Type, out constantType, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError((ILocated?)variableDeclaration.InitialValue ?? (ILocated)variableDeclaration));
                return false;
            }
        }

        result = new CompiledVariableConstant(constantValue, constantType, variableDeclaration);
        return true;
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
        => GetStruct(
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
        => GetAlias(
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

    bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledVariableDefinition? compiledVariable, [NotNullWhen(false)] out PossibleDiagnostic? error) => GetVariable(variableName, Frames.Last, out compiledVariable, out error);

    static bool GetVariable(string variableName, CompiledFrame frame, [NotNullWhen(true)] out CompiledVariableDefinition? compiledVariable, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (Scope scope in frame.Scopes)
        {
            foreach (CompiledVariableDefinition compiledVariable_ in scope.Variables)
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

        [NotNullWhen(true)] out CompiledVariableDefinition? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        CompiledVariableDefinition? result_ = default;
        PossibleDiagnostic? error_ = null;

        GlobalVariablePerfectus perfectus = GlobalVariablePerfectus.None;

        static GlobalVariablePerfectus Max(GlobalVariablePerfectus a, GlobalVariablePerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledVariableDefinition variable)
        {
            if (variableName is not null &&
                variable.Identifier != variableName)
            { return false; }

            perfectus = Max(perfectus, GlobalVariablePerfectus.Identifier);
            return true;
        }

        bool HandleFile(CompiledVariableDefinition variable)
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

        foreach (CompiledVariableDefinition variable in CompiledGlobalVariables)
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

    bool GetParameter(string parameterName, [NotNullWhen(true)] out CompiledParameter? parameter, [NotNullWhen(false)] out PossibleDiagnostic? error) => GetParameter(parameterName, Frames.Last, out parameter, out error);

    static bool GetParameter(string parameterName, CompiledFrame frame, [NotNullWhen(true)] out CompiledParameter? parameter, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (CompiledParameter compiledParameter in frame.CompiledParameters)
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

    bool GetInstructionLabel(string identifier, [NotNullWhen(true)] out CompiledLabelDeclaration? instructionLabel, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (CompiledLabelDeclaration compiledInstructionLabel in Frames[^1].InstructionLabels)
        {
            if (compiledInstructionLabel.Identifier != identifier) continue;
            instructionLabel = compiledInstructionLabel;
            error = null;
            return true;
        }

        foreach (CompiledLabelDeclaration compiledInstructionLabel in Frames[0].InstructionLabels)
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

    public static bool CanCastImplicitly(GeneralType source, GeneralType destination, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = null;

        if (destination.SameAs(source))
        { return true; }

        if (destination.SameAs(BasicType.Any))
        { return true; }

        {
            if (destination.Is(out PointerType? dstPointer) &&
                source.Is(out PointerType? srcPointer))
            {
                if (dstPointer.To.SameAs(BasicType.Any))
                { return true; }

                if (dstPointer.To.Is(out ArrayType? dstArray) &&
                    srcPointer.To.Is(out ArrayType? srcArray))
                {
                    if (dstArray.Length.HasValue &&
                        srcArray.Length.HasValue &&
                        dstArray.Length.Value != srcArray.Length.Value)
                    {
                        error = new($"Can't cast an array pointer with length of {dstArray.Length.Value} to an array pointer with length of {srcArray.Length.Value}");
                        return false;
                    }

                    if (dstArray.Length is null)
                    { return true; }
                }
            }
        }

        {
            if (destination.Is(out PointerType? dstPointer) &&
                source.Is(out FunctionType? srcFunction)
                && dstPointer.To.SameAs(BasicType.Any)
                && srcFunction.HasClosure)
            { return true; }
        }

        error = new($"Can't cast \"{source}\" to \"{destination}\" implicitly");
        return false;
    }

    public static bool CanCastImplicitly(GeneralType source, GeneralType destination, Expression? value, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (CanCastImplicitly(source, destination, out error)) return true;

        if (value is LiteralExpression literal &&
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

                if (!destArrayType.Length.HasValue)
                {
                    error = new($"Can't cast literal value \"{literalValue}\" (length of {literalValue.Length}) to stack array \"{destination}\" (length of <runtime value>)");
                    return false;
                }

                if (literalValue.Length != destArrayType.Length.Value)
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
                    if (!arrayType.Length.HasValue)
                    {
                        error = new($"Can't cast literal value \"{literal.Value}\" (length of {literal.Value.Length}) to array \"{destination}\" (length of <runtime value>)");
                        return false;
                    }

                    if (literal.Value.Length != arrayType.Length.Value)
                    {
                        error = new($"Can't cast literal value \"{literal.Value}\" (length of {literal.Value.Length}) to array \"{destination}\" (length of \"{arrayType.Length?.ToString() ?? "null"}\")");
                        return false;
                    }
                }

                return true;
            }
        }

        error = new($"Can't cast \"{source}\" to \"{destination}\" implicitly");
        return false;
    }

    public bool CanCastImplicitly(CompiledExpression value, GeneralType destination, out CompiledExpression assignedValue, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        GeneralType source = value.Type;
        assignedValue = value;

        if (CanCastImplicitly(source, destination, out error)) return true;

        if (value is CompiledString stringInstance)
        {
            if (destination.Is(out PointerType? pointerType) &&
                pointerType.To.Is(out ArrayType? arrayType) &&
                arrayType.Of.SameAs(BasicType.U16))
            {
                if (arrayType.Length is not null)
                {
                    if (!arrayType.Length.HasValue)
                    {
                        error = new($"Can't cast literal value \"{stringInstance.Value}\" (length of {stringInstance.Value.Length}) to array \"{destination}\" (length of <runtime value>)");
                        return false;
                    }

                    if (stringInstance.Value.Length != arrayType.Length.Value)
                    {
                        error = new($"Can't cast literal value \"{stringInstance.Value}\" (length of {stringInstance.Value.Length}) to array \"{destination}\" (length of \"{arrayType.Length?.ToString() ?? "null"}\")");
                        return false;
                    }
                }

                return true;
            }
        }

        if (value is CompiledStackString stackStringInstance)
        {
            if (destination.Is(out ArrayType? destArrayType) &&
                destArrayType.Of.SameAs(BasicType.U16))
            {
                if (destArrayType.Length is null)
                {
                    error = new($"Can't cast literal value \"{stackStringInstance.Value}\" (length of {stackStringInstance.Value.Length}) to stack array \"{destination}\" (without length)");
                    return false;
                }

                if (!destArrayType.Length.HasValue)
                {
                    error = new($"Can't cast literal value \"{stackStringInstance.Value}\" (length of {stackStringInstance.Value.Length}) to stack array \"{destination}\" (length of <runtime value>)");
                    return false;
                }

                if (stackStringInstance.Value.Length != destArrayType.Length.Value)
                {
                    error = new($"Can't cast literal value \"{stackStringInstance.Value}\" (length of {stackStringInstance.Value.Length}) to stack array \"{destination}\" (length of \"{destArrayType.Length?.ToString() ?? "null"}\")");
                    return false;
                }

                return true;
            }
        }

        {
            if (source.Is(out FunctionType? sourceFunctionType)
                && destination.Is(out FunctionType? targetFunctionType))
            {
                if (sourceFunctionType.HasClosure && targetFunctionType.HasClosure) return true;
                if (!sourceFunctionType.HasClosure && !targetFunctionType.HasClosure) return true;
                if (sourceFunctionType.HasClosure && !targetFunctionType.HasClosure)
                {
                    error = new($"Can't convert `{sourceFunctionType}` to `{targetFunctionType}` because it would lose the closure");
                    return false;
                }
                if (!sourceFunctionType.HasClosure && targetFunctionType.HasClosure)
                {
                    if (!CompileAllocation(PointerSize, value.Location, out CompiledExpression? allocator))
                    {
                        return false;
                    }
                    assignedValue = new CompiledCast()
                    {
                        TypeExpression = CompiledTypeExpression.CreateAnonymous(targetFunctionType, value.Location),
                        Type = targetFunctionType,
                        Value = value,
                        Allocator = allocator,
                        Location = value.Location,
                        SaveValue = value.SaveValue,
                    };
                    return true;
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

    bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out GeneralType? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            error = null;
            return true;
        }

        if (Frames.Last.TypeArguments.TryGetValue(name.Content, out GeneralType? typeArgument))
        {
            result = typeArgument;
            error = null;
            return true;
        }

        {
            int i = Frames.Last.TypeParameters.IndexOf(name.Content);
            if (i != -1)
            {
                result = new GenericType(Frames.Last.TypeParameters[i], relevantFile);
                error = null;
                return true;
            }
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            for (int j = 0; j < GenericParameters[i].Length; j++)
            {
                if (GenericParameters[i][j].Content == name.Content)
                {
                    GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                    result = new GenericType(GenericParameters[i][j], relevantFile);
                    error = null;
                    return true;
                }
            }
        }

        if (GetAlias(name.Content, relevantFile, out CompiledAlias? alias, out PossibleDiagnostic? aliasError))
        {
            name.AnalyzedType = alias.Value.FinalValue switch
            {
                BuiltinType => TokenAnalyzedType.BuiltinType,
                StructType => TokenAnalyzedType.Struct,
                GenericType => TokenAnalyzedType.TypeParameter,
                AliasType => TokenAnalyzedType.TypeAlias,
                _ => TokenAnalyzedType.Type,
            };
            alias.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            // HERE
            result = new AliasType(alias.Value, alias);
            error = null;
            return true;
        }

        if (GetStruct(name.Content, relevantFile, out CompiledStruct? @struct, out PossibleDiagnostic? structError))
        {
            name.AnalyzedType = TokenAnalyzedType.Struct;
            @struct.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            result = new StructType(@struct, relevantFile);
            error = null;
            return true;
        }

        /*
        if (GetFunction(FunctionQuery.Create<CompiledFunctionDefinition, string, Token>(name.Content, null, null, relevantFile), out FunctionQueryResult<CompiledFunctionDefinition>? function, out var functionError))
        {
            name.AnalyzedType = TokenAnalyzedType.FunctionName;
            function.Function.References.Add(new Reference<StatementWithValue?>(new Identifier(name, relevantFile), relevantFile));

            result = new FunctionType(function.Function);
            error = null;
            return true;
        }

        if (GetGlobalVariable(name.Content, relevantFile, out CompiledVariableDeclaration? globalVariable, out var globalVariableError))
        {
            name.AnalyzedType = TokenAnalyzedType.VariableName;

            result = globalVariable.Type;
            error = null;
            return true;
        }
        */

        result = null;
        error = new PossibleDiagnostic($"Can't find type `{name.Content}`", ImmutableArray.Create(
            aliasError,
            structError
        ));
        return false;
    }
    bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out CompiledTypeExpression? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new CompiledBuiltinTypeExpression(builtinType, new Location(name.Position, relevantFile));
            error = null;
            return true;
        }

        if (Frames.Last.TypeArguments.TryGetValue(name.Content, out GeneralType? typeArgument))
        {
            result = CompiledTypeExpression.CreateAnonymous(typeArgument, new Location(name.Position, relevantFile));
            error = null;
            return true;
        }

        {
            int i = Frames.Last.TypeParameters.IndexOf(name.Content);
            if (i != -1)
            {
                result = new CompiledGenericTypeExpression(Frames.Last.TypeParameters[i], relevantFile, new Location(name.Position, relevantFile));
                error = null;
                return true;
            }
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            for (int j = 0; j < GenericParameters[i].Length; j++)
            {
                if (GenericParameters[i][j].Content == name.Content)
                {
                    GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                    result = new CompiledGenericTypeExpression(GenericParameters[i][j], relevantFile, new Location(name.Position, relevantFile));
                    error = null;
                    return true;
                }
            }
        }

        if (GetAlias(name.Content, relevantFile, out CompiledAlias? alias, out PossibleDiagnostic? aliasError))
        {
            name.AnalyzedType = alias.Value.FinalValue switch
            {
                BuiltinType => TokenAnalyzedType.BuiltinType,
                StructType => TokenAnalyzedType.Struct,
                GenericType => TokenAnalyzedType.TypeParameter,
                AliasType => TokenAnalyzedType.TypeAlias,
                _ => TokenAnalyzedType.Type,
            };
            alias.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            // HERE
            result = new CompiledAliasTypeExpression(CompiledTypeExpression.CreateAnonymous(alias.Value, ((AliasDefinition)alias).Value), alias, new Location(name.Position, relevantFile));
            error = null;
            return true;
        }

        if (GetStruct(name.Content, relevantFile, out CompiledStruct? @struct, out PossibleDiagnostic? structError))
        {
            name.AnalyzedType = TokenAnalyzedType.Struct;
            @struct.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, relevantFile), relevantFile));

            result = new CompiledStructTypeExpression(@struct, relevantFile, new Location(name.Position, relevantFile));
            error = null;
            return true;
        }

        result = null;
        error = new PossibleDiagnostic($"Can't find type `{name.Content}`", ImmutableArray.Create(
            aliasError,
            structError
        ));
        return false;
    }

    bool GetLiteralType(LiteralType literal, [NotNullWhen(true)] out GeneralType? type, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        return GetUsedBy(literal switch
        {
            LiteralType.Integer => "integer",
            LiteralType.Float => "float",
            LiteralType.String => "string",
            LiteralType.Char => "char",
            _ => throw new UnreachableException(),
        }, out type, out error);
    }

    bool GetUsedBy(string by, [NotNullWhen(true)] out GeneralType? type, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        string? ParseAttribute(AttributeUsage attribute)
        {
            if (!attribute.TryGetValue(out string? literalTypeName))
            {
                Diagnostics.Add(Diagnostic.Critical($"Attribute \"{attribute.Identifier}\" needs one string argument", attribute));
                return default;
            }
            return literalTypeName;
        }

        type = null;

        foreach (CompiledAlias alias in CompiledAliases)
        {
            if (alias.Attributes.TryGetAttribute(AttributeConstants.InternalType, out AttributeUsage? attribute))
            {
                if (ParseAttribute(attribute) == by)
                {
                    if (type is not null)
                    {
                        error = new PossibleDiagnostic($"Multiple type definitions marked as an internal type `{by}`", attribute);
                        return false;
                    }
                    type = new AliasType(alias.Value, alias);
                }
            }
        }

        foreach (CompiledStruct @struct in CompiledStructs)
        {
            if (@struct.Attributes.TryGetAttribute(AttributeConstants.InternalType, out AttributeUsage? attribute))
            {
                if (ParseAttribute(attribute) == by)
                {
                    if (type is not null)
                    {
                        error = new PossibleDiagnostic($"Multiple type definitions marked as an internal type `{by}`", attribute);
                        return false;
                    }
                    type = new StructType(@struct, @struct.File);
                }
            }
        }

        if (type is null)
        {
            error = new PossibleDiagnostic($"No type definition found with attribute `{AttributeConstants.InternalType}`", false);
            return false;
        }
        else
        {
            error = null;
            return true;
        }
    }

    void SetPredictedValue(Expression expression, CompiledValue value)
    {
        if (!Frames.Last.IsTemplateInstance) expression.PredictedValue = value;
    }
    TType SetStatementType<TType>(Expression expression, TType type)
        where TType : GeneralType
    {
        if (!Frames.Last.IsTemplateInstance) expression.CompiledType = type;
        return type;
    }
    void TrySetStatementReference<TRef>(Statement statement, TRef? reference)
    {
        if (statement is IReferenceableTo<TRef> v1) SetStatementReference(v1, reference);
        else if (statement is IReferenceableTo v2) SetStatementReference(v2, reference);
    }
    void SetStatementReference<TRef>(IReferenceableTo<TRef> statement, TRef? reference)
    {
        if (!Frames.Last.IsTemplateInstance) statement.Reference = reference;
    }
    void SetStatementReference(IReferenceableTo statement, object? reference)
    {
        if (!Frames.Last.IsTemplateInstance) statement.Reference = reference;
    }

    bool FindStatementType(AnyCallExpression anyCall, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        DiagnosticsCollection subdiagnostics = new();
        if (anyCall.ToFunctionCall(out FunctionCallExpression? functionCall) && FindStatementType(functionCall, out type, subdiagnostics))
        {
            SetStatementType(anyCall, type);
            return true;
        }

        if (!FindStatementType(anyCall.Expression, out GeneralType? prevType, diagnostics))
        {
            type = null;
            diagnostics.AddRange(subdiagnostics);
            return false;
        }

        if (!prevType.Is(out FunctionType? functionType))
        {
            type = null;
            diagnostics.Add(Diagnostic.Critical($"This isn't a function", anyCall.Expression));
            diagnostics.AddRange(subdiagnostics);
            return false;
        }

        type = functionType.ReturnType;
        SetStatementType(anyCall, functionType.ReturnType);
        return true;
    }
    bool FindStatementType(LambdaExpression lambdaExpression, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        type = null;

        if (expectedType is null || !expectedType.Is(out FunctionType? functionType))
        {
            diagnostics.Add(Diagnostic.Internal($"No bro please no", lambdaExpression));
            return false;
        }

        if (functionType.Parameters.Length != lambdaExpression.Parameters.Parameters.Length)
        {
            diagnostics.Add(Diagnostic.Error($"Idk how to explain this", lambdaExpression));
            return false;
        }

        GeneralType[] parameterTypes = new GeneralType[lambdaExpression.Parameters.Parameters.Length];
        for (int i = 0; i < lambdaExpression.Parameters.Parameters.Length; i++)
        {
            GeneralType expectedParameterType = functionType.Parameters[i];
            if (!CompileType(lambdaExpression.Parameters.Parameters[i].Type, out GeneralType? definedParameterType, diagnostics)) return false;
            if (!expectedParameterType.SameAs(definedParameterType))
            {
                diagnostics.Add(Diagnostic.Error($"Expected `{expectedParameterType}` defined `{definedParameterType}` ", lambdaExpression));
                return false;
            }
            parameterTypes[i] = definedParameterType;
        }

        type = new FunctionType(functionType.ReturnType, parameterTypes.AsImmutableUnsafe(), functionType.HasClosure);
        return true;
    }
    bool FindStatementType(ListExpression list, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        GeneralType? itemType = null;

        for (int i = 0; i < list.Values.Length; i++)
        {
            Expression item = list.Values[i];
            if (!FindStatementType(item, itemType, out GeneralType? currentItemType, diagnostics)) continue;

            if (itemType is null)
            {
                itemType = currentItemType;
            }
            else if (!currentItemType.SameAs(itemType))
            {
                diagnostics.Add(Diagnostic.Critical($"List element at index {i} should be a {itemType} and not {currentItemType}", item));
            }
        }

        if (itemType is null)
        {
            diagnostics.Add(Diagnostic.Error($"Could not infer the list element type", list));
            itemType = BuiltinType.Any;
        }

        type = new ArrayType(itemType, list.Values.Length);
        return true;
    }
    bool FindStatementType(IndexCallExpression index, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        type = null;
        if (!FindStatementType(index.Object, out GeneralType? prevType, diagnostics)) return false;
        if (!FindStatementType(index.Index, out GeneralType? indexType, diagnostics)) return false;

        if (GetIndexGetter(prevType, indexType, index.File, out FunctionQueryResult<CompiledFunctionDefinition>? indexer, out PossibleDiagnostic? notFoundError))
        {
            SetStatementType(index, type = indexer.Function.Type);
            return true;
        }

        if (prevType.Is(out ArrayType? arrayType))
        {
            SetStatementType(index, type = arrayType.Of);
            return true;
        }

        if (prevType.Is(out PointerType? pointerType) &&
            pointerType.To.Is(out arrayType))
        {
            type = arrayType.Of;
            return true;
        }

        diagnostics.Add(notFoundError.ToError(index));
        return false;
    }
    bool FindStatementType(FunctionCallExpression functionCall, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (functionCall.Identifier.Content == StatementKeywords.Sizeof)
        {
            if (GetLiteralType(LiteralType.Integer, out GeneralType? integerType, out PossibleDiagnostic? internalTypeError))
            {
                type = integerType;
            }
            else
            {
                type = SizeofStatementType;
                diagnostics.Add(Diagnostic.Warning($"No type defined for integer literals, using the default {type}", functionCall).WithSuberrors(internalTypeError.ToError(functionCall)));
            }
            return true;
        }

        if (!GetFunction(functionCall, out FunctionQueryResult<CompiledFunctionDefinition>? result, out PossibleDiagnostic? notFoundError))
        {
            type = null;
            diagnostics.Add(notFoundError.ToError(functionCall, false));
            return false;
        }

        // Diagnostics.Add(notFoundError?.SubErrors.FirstOrDefault()?.ToWarning(functionCall));
        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
        SetStatementType(functionCall, type = result.Function.Type);
        return true;
    }
    bool FindStatementType(BinaryOperatorCallExpression @operator, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? _result, out _))
        {
            if (_result.DidReplaceArguments) throw new UnreachableException();
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            SetStatementType(@operator, type = _result.Function.Type);
            return true;
        }

        type = null;

        if (!FindStatementType(@operator.Left, expectedType, out GeneralType? leftType, diagnostics)) return false;
        if (!FindStatementType(@operator.Right, expectedType, out GeneralType? rightType, diagnostics)) return false;

        {
            if (leftType.Is(out BuiltinType? leftBType) &&
                rightType.Is(out BuiltinType? rightBType))
            {
                bool isFloat =
                    leftBType.Type == BasicType.F32 ||
                    rightBType.Type == BasicType.F32;

                if (!FindBitWidth(leftType, out BitWidth leftBitWidth, out PossibleDiagnostic? e1, this))
                {
                    diagnostics.Add(e1.ToError(@operator.Left));
                    return false;
                }

                if (!FindBitWidth(rightType, out BitWidth rightBitWidth, out PossibleDiagnostic? d2, this))
                {
                    diagnostics.Add(d2.ToError(@operator.Left));
                    return false;
                }

                BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

                if (!leftBType.TryGetNumericType(out NumericType leftNType1) ||
                    !rightBType.TryGetNumericType(out NumericType rightNType1))
                {
                    diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{leftType}\" \"{@operator.Operator.Content}\" \"{rightType}\"", @operator.Operator, @operator.File));
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
                            type = BooleanType;
                            diagnostics.Add(Diagnostic.Warning($"No type defined for booleans, using the default {type}", @operator).WithSuberrors(internalTypeError.ToError(@operator)));
                        }
                        else
                        {
                            type = booleanType;
                        }
                        break;

                    case BinaryOperatorCallExpression.LogicalOR:
                    case BinaryOperatorCallExpression.LogicalAND:
                    case BinaryOperatorCallExpression.BitwiseAND:
                    case BinaryOperatorCallExpression.BitwiseOR:
                    case BinaryOperatorCallExpression.BitwiseXOR:
                    case BinaryOperatorCallExpression.BitshiftLeft:
                    case BinaryOperatorCallExpression.BitshiftRight:
                        type = numericResultType;
                        break;

                    case BinaryOperatorCallExpression.Addition:
                    case BinaryOperatorCallExpression.Subtraction:
                    case BinaryOperatorCallExpression.Multiplication:
                    case BinaryOperatorCallExpression.Division:
                    case BinaryOperatorCallExpression.Modulo:
                        type = isFloat ? BuiltinType.F32 : numericResultType;
                        break;

                    default:
                        diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{leftType}\" \"{@operator.Operator.Content}\" \"{rightType}\"", @operator.Operator, @operator.File));
                        type = BuiltinType.Void;
                        break;
                }

                if (expectedType is not null &&
                    CanCastImplicitly(type, expectedType, null, out _))
                { type = expectedType; }

                SetStatementType(@operator, type);
                return true;
            }
        }

        bool ok = true;

        if (!leftType.TryGetNumericType(out NumericType leftNType))
        {
            diagnostics.Add(Diagnostic.Critical($"Type \"{leftType}\" aint a numeric type", @operator.Left));
            ok = false;
        }

        if (!rightType.TryGetNumericType(out NumericType rightNType))
        {
            diagnostics.Add(Diagnostic.Critical($"Type \"{rightType}\" aint a numeric type", @operator.Right));
            ok = false;
        }

        if (!FindBitWidth(leftType, out BitWidth leftBitwidth, out PossibleDiagnostic? error, this))
        {
            diagnostics.Add(error.ToError(@operator.Left));
            ok = false;
        }

        if (!FindBitWidth(rightType, out BitWidth rightBitwidth, out error, this))
        {
            diagnostics.Add(error.ToError(@operator.Right));
            ok = false;
        }

        if (!ok) return false;

        CompiledValue leftValue = GetInitialValue(leftNType, leftBitwidth);
        CompiledValue rightValue = GetInitialValue(rightNType, rightBitwidth);

        if (!TryComputeSimple(@operator.Operator.Content, leftValue, rightValue, out CompiledValue predictedValue, out PossibleDiagnostic? evaluateError))
        {
            diagnostics.Add(evaluateError.ToError(@operator));
            return false;
        }

        if (!CompileType(predictedValue.Type, out type, out PossibleDiagnostic? typeError))
        {
            diagnostics.Add(typeError.ToError(@operator));
            type = expectedType ?? BuiltinType.Void;
        }

        if (expectedType is not null)
        {
            if (type.SameAs(BasicType.I32) &&
                expectedType.Is<PointerType>())
            {
                SetStatementType(@operator, type = expectedType);
                return true;
            }
        }

        SetStatementType(@operator, type);
        return true;
    }
    bool FindStatementType(UnaryOperatorCallExpression @operator, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (GetOperator(@operator, @operator.File, out FunctionQueryResult<CompiledOperatorDefinition>? result_, out _))
        {
            if (result_.DidReplaceArguments) throw new UnreachableException();
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            SetStatementType(@operator, type = result_.Function.Type);
            return true;
        }

        type = null;
        if (!FindStatementType(@operator.Expression, out GeneralType? leftType, diagnostics)) return false;

        switch (@operator.Operator.Content)
        {
            case UnaryOperatorCallExpression.LogicalNOT:
            {
                if (!GetUsedBy("boolean", out GeneralType? booleanType, out PossibleDiagnostic? internalTypeError))
                {
                    type = BooleanType;
                    diagnostics.Add(Diagnostic.Warning($"No type defined for booleans, using the default {type}", @operator).WithSuberrors(internalTypeError.ToError(@operator)));
                }
                else
                {
                    type = booleanType;
                }
                break;
            }
            case UnaryOperatorCallExpression.BinaryNOT:
            {
                type = leftType;
                break;
            }
            case UnaryOperatorCallExpression.UnaryMinus:
            {
                type = leftType;
                break;
            }
            case UnaryOperatorCallExpression.UnaryPlus:
            {
                type = leftType;
                break;
            }
            default:
            {
                diagnostics.Add(Diagnostic.Critical($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, @operator.File));
                return false;
            }
        }

        if (expectedType is not null && expectedType.Is<PointerType>() && type.SameAs(BasicType.I32))
        {
            SetStatementType(@operator, type = expectedType);
            return true;
        }

        SetStatementType(@operator, type);
        return true;
    }
    bool FindStatementType(LiteralExpression literal, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
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
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if (literal.GetInt() is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U16))
                    {
                        if (literal.GetInt() is >= ushort.MinValue and <= ushort.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if (literal.GetInt() is >= short.MinValue and <= short.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.U32))
                    {
                        if (literal.GetInt() >= (int)uint.MinValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        SetStatementType(literal, type = expectedType);
                        return true;
                    }
                }

                if (GetLiteralType(literal.Type, out GeneralType? literalType, out PossibleDiagnostic? internalTypeError))
                {
                    type = literalType;
                    return true;
                }

                SetStatementType(literal, type = BuiltinType.I32);
                diagnostics.Add(Diagnostic.Warning($"No type defined for integer literals, using the default {type}", literal).WithSuberrors(internalTypeError.ToError(literal)));
                return true;
            }
            case LiteralType.Float:
            {
                if (GetLiteralType(literal.Type, out GeneralType? literalType, out PossibleDiagnostic? internalTypeError))
                {
                    type = literalType;
                    return true;
                }

                SetStatementType(literal, type = BuiltinType.F32);
                diagnostics.Add(Diagnostic.Warning($"No type defined for float literals, using the default {type}", literal).WithSuberrors(internalTypeError.ToError(literal)));
                return true;
            }
            case LiteralType.String:
            {
                if (expectedType is not null &&
                    expectedType.Is(out PointerType? pointerType) &&
                    pointerType.To.Is(out ArrayType? arrayType) &&
                    arrayType.Of.SameAs(BasicType.U8))
                {
                    SetStatementType(literal, type = expectedType);
                    return true;
                }

                if (GetLiteralType(literal.Type, out GeneralType? literalType, out PossibleDiagnostic? internalTypeError))
                {
                    type = literalType;
                    return true;
                }

                if (!GetLiteralType(LiteralType.Char, out GeneralType? charType, out PossibleDiagnostic? charInternalTypeError))
                {
                    charType = BuiltinType.Char;
                    diagnostics.Add(Diagnostic.Warning($"No type defined for characters, using the default {charType}", literal).WithSuberrors(charInternalTypeError.ToError(literal)));
                }

                SetStatementType(literal, type = new PointerType(new ArrayType(charType, literal.Value.Length + 1)));
                diagnostics.Add(Diagnostic.Warning($"No type defined for string literals, using the default {type}", literal).WithSuberrors(internalTypeError.ToError(literal)));
                return true;
            }
            case LiteralType.Char:
            {
                if (expectedType is not null)
                {
                    if (expectedType.SameAs(BasicType.U8))
                    {
                        if ((int)literal.Value[0] is >= byte.MinValue and <= byte.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I8))
                    {
                        if ((int)literal.Value[0] is >= sbyte.MinValue and <= sbyte.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.I16))
                    {
                        if ((int)literal.Value[0] is >= short.MinValue and <= short.MaxValue)
                        {
                            SetStatementType(literal, type = expectedType);
                            return true;
                        }
                    }
                    else if (expectedType.SameAs(BasicType.F32))
                    {
                        SetStatementType(literal, type = expectedType);
                        return true;
                    }
                }

                if (GetLiteralType(literal.Type, out GeneralType? literalType, out PossibleDiagnostic? internalTypeError))
                {
                    type = literalType;
                    return true;
                }

                SetStatementType(literal, type = BuiltinType.Char);
                diagnostics.Add(Diagnostic.Warning($"No type defined for character literals, using the default {type}", literal).WithSuberrors(internalTypeError.ToError(literal)));
                return true;
            }
            default:
                throw new UnreachableException($"Unknown literal type \"{literal.Type}\"");
        }
    }
    bool FindStatementType(IdentifierExpression identifier, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (identifier.Content.StartsWith('#'))
        {
            identifier.Reference = null;
            identifier.AnalyzedType = TokenAnalyzedType.ConstantName;
            SetStatementType(identifier, type = BooleanType);
            return true;
        }

        if (BBLang.Generator.CodeGeneratorForMain.RegisterKeywords.TryGetValue(identifier.Content, out (Register Register, BuiltinType Type) registerKeyword))
        {
            type = registerKeyword.Type;
            return true;
        }

        if (!Settings.ExpressionVariables.IsDefault)
        {
            foreach (ExpressionVariable item in Settings.ExpressionVariables)
            {
                if (item.Name != identifier.Content) continue;
                identifier.AnalyzedType = TokenAnalyzedType.VariableName;
                SetStatementReference(identifier, item);
                SetStatementType(identifier, type = item.Type);
                return true;
            }
        }

        if (GetConstant(identifier.Content, identifier.File, out CompiledVariableConstant? constant, out PossibleDiagnostic? constantNotFoundError))
        {
            SetStatementReference(identifier, constant);
            identifier.AnalyzedType = TokenAnalyzedType.ConstantName;
            SetStatementType(identifier, type = constant.Type);
            return true;
        }

        if (GetParameter(identifier.Content, out CompiledParameter? parameter, out PossibleDiagnostic? parameterNotFoundError))
        {
            if (identifier.Content != StatementKeywords.This)
            { identifier.AnalyzedType = TokenAnalyzedType.ParameterName; }
            SetStatementReference(identifier, parameter);
            SetStatementType(identifier, type = parameter.Type);
            return true;
        }

        if (GetVariable(identifier.Content, out CompiledVariableDefinition? variable, out PossibleDiagnostic? variableNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementReference(identifier, variable);
            SetStatementType(identifier, type = variable.Type);
            return true;
        }

        if (GetGlobalVariable(identifier.Content, identifier.File, out CompiledVariableDefinition? globalVariable, out PossibleDiagnostic? globalVariableNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.VariableName;
            SetStatementReference(identifier, globalVariable);
            SetStatementType(identifier, type = globalVariable.Type);
            return true;
        }

        if (GetLocalSymbolType(identifier, out type))
        {
            SetStatementType(identifier, type);
            return true;
        }

        if (GetFunction(identifier.Content, expectedType, out FunctionQueryResult<CompiledFunctionDefinition>? function, out PossibleDiagnostic? functionNotFoundError))
        {
            identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            SetStatementType(identifier, type = new FunctionType(function.Function.Type, function.Function.Parameters.ToImmutableArray(v => v.Type), false));
            return true;
        }

        if (GetInstructionLabel(identifier.Content, out _, out PossibleDiagnostic? instructionLabelNotFound))
        {
            SetStatementType(identifier, type = new FunctionType(BuiltinType.Void, ImmutableArray<GeneralType>.Empty, false));
            return true;
        }

        for (int i = Frames.Count - 2; i >= 0; i--)
        {
            if (GetVariable(identifier.Content, Frames[i], out variable, out _))
            {
                SetStatementType(identifier, type = variable.Type);
                return true;
            }
        }

        if (FindType(identifier.Identifier, identifier.File, out GeneralType? result, out PossibleDiagnostic? typeError))
        {
            SetStatementType(identifier, type = result);
            return true;
        }

        diagnostics.Add(Diagnostic.Critical($"Symbol \"{identifier.Content}\" not found", identifier)
            .WithSuberrors(
                parameterNotFoundError.ToError(identifier),
                variableNotFoundError.ToError(identifier),
                globalVariableNotFoundError.ToError(identifier),
                constantNotFoundError.ToError(identifier),
                functionNotFoundError.ToError(identifier),
                instructionLabelNotFound.ToError(identifier),
                typeError.ToError(identifier)
            ));
        return false;
    }
    bool FindStatementType(GetReferenceExpression addressGetter, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        type = null;
        if (!FindStatementType(addressGetter.Expression, out GeneralType? to, diagnostics)) return false;
        SetStatementType(addressGetter, type = new PointerType(to));
        return true;
    }
    bool FindStatementType(DereferenceExpression pointer, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        type = null;
        if (!FindStatementType(pointer.Expression, out GeneralType? to, diagnostics)) return false;

        if (!to.Is(out PointerType? pointerType))
        { SetStatementType(pointer, type = BuiltinType.Any); }
        else
        {
            SetStatementType(pointer, type = pointerType.To);
        }
        return true;
    }
    bool FindStatementType(NewInstanceExpression newInstance, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (!CompileType(newInstance.Type, out type, diagnostics)) return false;

        SetStatementType(newInstance, type);
        return true;
    }
    bool FindStatementType(ConstructorCallExpression constructorCall, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (!CompileType(constructorCall.Type, out type, diagnostics)) return false;

        FindStatementTypes(constructorCall.Arguments, out ImmutableArray<GeneralType> parameters, diagnostics);

        if (GetConstructor(type, parameters, constructorCall.File, out FunctionQueryResult<CompiledConstructorDefinition>? result, out PossibleDiagnostic? notFound))
        {
            SetStatementType(constructorCall, type = result.Function.Type);
            return true;
        }

        diagnostics.Add(notFound.ToError(constructorCall));
        return false;
    }
    bool FindStatementType(FieldExpression field, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        type = null;
        if (!FindStatementType(field.Object, out GeneralType? prevStatementType, diagnostics)) return false;

        if (prevStatementType.Is<ArrayType>() && field.Identifier.Content == "Length")
        {
            field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            SetStatementType(field, type = ArrayLengthType);
            return true;
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
                { type = definedField.Type; }
                else
                {
                    type = GeneralType.InsertTypeParameters(definedField.Type, structType.TypeArguments) ?? definedField.Type;
                }
                return true;
            }

            diagnostics.Add(Diagnostic.Critical($"Field definition \"{field.Identifier}\" not found in type \"{prevStatementType}\"", field.Identifier, field.File));
            return false;
        }
        else
        {
            diagnostics.Add(Diagnostic.Critical($"Type \"{prevStatementType}\" does not have a field \"{field.Identifier}\"", field));
            return false;
        }
    }
    bool FindStatementType(ReinterpretExpression @as, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (!CompileType(@as.Type, out type, diagnostics)) return false;

        SetStatementType(@as, type);
        return true;
    }
    bool FindStatementType(ManagedTypeCastExpression @as, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        if (!CompileType(@as.Type, out type, diagnostics)) return false;

        SetStatementType(@as, type);
        return true;
    }
    bool FindStatementType(ArgumentExpression modifiedStatement, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        return FindStatementType(modifiedStatement.Value, expectedType, out type, diagnostics);
    }
    bool FindStatementType(Expression statement, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
        => FindStatementType(statement, null, out type, diagnostics);
    bool FindStatementType(Expression statement, GeneralType? expectedType, [NotNullWhen(true)] out GeneralType? type, DiagnosticsCollection diagnostics)
    {
        switch (statement)
        {
            case FunctionCallExpression v: return FindStatementType(v, out type, diagnostics);
            case BinaryOperatorCallExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            case UnaryOperatorCallExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            case LiteralExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            case IdentifierExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            case GetReferenceExpression v: return FindStatementType(v, out type, diagnostics);
            case DereferenceExpression v: return FindStatementType(v, out type, diagnostics);
            case NewInstanceExpression v: return FindStatementType(v, out type, diagnostics);
            case ConstructorCallExpression v: return FindStatementType(v, out type, diagnostics);
            case FieldExpression v: return FindStatementType(v, out type, diagnostics);
            case ReinterpretExpression v: return FindStatementType(v, out type, diagnostics);
            case ManagedTypeCastExpression v: return FindStatementType(v, out type, diagnostics);
            case IndexCallExpression v: return FindStatementType(v, out type, diagnostics);
            case ArgumentExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            case AnyCallExpression v: return FindStatementType(v, out type, diagnostics);
            case ListExpression v: return FindStatementType(v, out type, diagnostics);
            case LambdaExpression v: return FindStatementType(v, expectedType, out type, diagnostics);
            default:
                type = null;
                diagnostics.Add(Diagnostic.Critical($"Statement \"{statement.GetType().Name}\" does not have a type", statement));
                return false;
        }
    }
    bool FindStatementTypes<TExpression>(ImmutableArray<TExpression> statements, [NotNullWhen(true)] out ImmutableArray<GeneralType> type, DiagnosticsCollection diagnostics)
        where TExpression : Expression
    {
        type = default;
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(statements.Length);
        for (int i = 0; i < statements.Length; i++)
        {
            if (!FindStatementType(statements[i], out GeneralType? item, diagnostics)) return false;
            result.Add(item);
        }
        type = result.MoveToImmutable();
        return true;
    }

    #endregion

    #region Inlining

    class InlineContext
    {
        public required ImmutableDictionary<string, CompiledArgument> Arguments { get; init; }
        public List<CompiledArgument> InlinedArguments { get; } = new();
        public Dictionary<CompiledVariableDefinition, CompiledVariableDefinition> VariableReplacements { get; } = new();
    }

    static bool Inline(IEnumerable<CompiledArgument> statements, InlineContext context, [NotNullWhen(true)] out ImmutableArray<CompiledArgument> inlined)
    {
        inlined = ImmutableArray<CompiledArgument>.Empty;
        ImmutableArray<CompiledArgument>.Builder res = ImmutableArray.CreateBuilder<CompiledArgument>();

        foreach (CompiledArgument statement in statements)
        {
            if (!Inline(statement.Value, context, out CompiledExpression? v)) return false;
            res.Add(new CompiledArgument()
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

    static bool Inline(CompiledTypeExpression statement, InlineContext context, out CompiledTypeExpression inlined)
    {
        inlined = statement;

        switch (statement)
        {
            case CompiledAliasTypeExpression v:
                if (!Inline(v.Value, context, out CompiledTypeExpression? vInlined)) return false;
                inlined = new CompiledAliasTypeExpression(vInlined, v.Definition, v.Location);
                break;
            case CompiledArrayTypeExpression v:
                if (!Inline(v.Of, context, out CompiledTypeExpression? ofInlined)) return false;
                if (!Inline(v.Length, context, out CompiledExpression? lengthInlined)) return false;
                inlined = new CompiledArrayTypeExpression(ofInlined, lengthInlined, v.Location);
                break;
            case CompiledBuiltinTypeExpression v:
                inlined = v;
                break;
            case CompiledFunctionTypeExpression v:
                CompiledTypeExpression[] parameters = new CompiledTypeExpression[v.Parameters.Length];
                if (!Inline(v.ReturnType, context, out CompiledTypeExpression? returnTypeInlined)) return false;
                for (int i = 0; i < v.Parameters.Length; i++)
                {
                    if (!Inline(v.Parameters[i], context, out parameters[i])) return false;
                }
                inlined = new CompiledFunctionTypeExpression(returnTypeInlined, parameters.AsImmutableUnsafe(), v.HasClosure, v.Location);
                break;
            case CompiledGenericTypeExpression v:
                inlined = v;
                break;
            case CompiledPointerTypeExpression v:
                if (!Inline(v.To, context, out CompiledTypeExpression? toInlined)) return false;
                inlined = new CompiledPointerTypeExpression(toInlined, v.Location);
                break;
            case CompiledStructTypeExpression v:
                Dictionary<string, CompiledTypeExpression> typeArguments = new(v.TypeArguments.Count);
                foreach (KeyValuePair<string, CompiledTypeExpression> i in v.TypeArguments)
                {
                    if (!Inline(i.Value, context, out CompiledTypeExpression? iInlined)) return false;
                    typeArguments[i.Key] = iInlined;
                }
                inlined = new CompiledStructTypeExpression(v.Struct, v.File, typeArguments.ToImmutableDictionary(), v.Location);
                break;
            default: throw new UnreachableException();
        }

        if (inlined.Equals(statement)) inlined = statement;
        return true;
    }
    static bool Inline(CompiledSizeof statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Of, context, out CompiledTypeExpression? inlinedOf)) return false;
        inlined = new CompiledSizeof()
        {
            Of = inlinedOf,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledBinaryOperatorCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Left, context, out CompiledExpression? inlinedLeft)) return false;
        if (!Inline(statement.Right, context, out CompiledExpression? inlinedRight)) return false;

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
    static bool Inline(CompiledUnaryOperatorCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Left, context, out CompiledExpression? inlinedLeft)) return false;

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
    static bool Inline(CompiledConstantValue statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledRegisterAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledExpressionVariableAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledVariableAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!context.VariableReplacements.TryGetValue(statement.Variable, out CompiledVariableDefinition? replacedVariable))
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

        inlined = new CompiledVariableAccess()
        {
            Variable = replacedVariable,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledParameterAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (context.Arguments.TryGetValue(statement.Parameter.Identifier.Content, out CompiledArgument? inlinedArgument))
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
    static bool Inline(CompiledFunctionReference statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledLabelReference statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledFieldAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Object, context, out CompiledExpression? inlinedObject)) return false;

        while (inlinedObject is CompiledGetReference addressGetter)
        { inlinedObject = addressGetter.Of; }

        inlined = new CompiledFieldAccess()
        {
            Object = inlinedObject,
            Field = statement.Field,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledElementAccess statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Base, context, out CompiledExpression? inlinedBase)) return false;
        if (!Inline(statement.Index, context, out CompiledExpression? inlinedIndex)) return false;

        inlined = new CompiledElementAccess()
        {
            Base = inlinedBase,
            Index = inlinedIndex,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledGetReference statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Of, context, out CompiledExpression? inlinedOf)) return false;

        inlined = new CompiledGetReference()
        {
            Of = inlinedOf,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledDereference statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Address, context, out CompiledExpression? inlinedTo)) return false;

        inlined = new CompiledDereference()
        {
            Address = inlinedTo,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledStackAllocation statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.TypeExpression, context, out CompiledTypeExpression? inlinedType)) return false;
        inlined = new CompiledStackAllocation()
        {
            TypeExpression = inlinedType,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledConstructorCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Object, context, out CompiledExpression? inlinedObject)) return false;
        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledArgument> inlinedArguments)) return false;

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
    static bool Inline(CompiledCast statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;
        if (!Inline(statement.TypeExpression, context, out CompiledTypeExpression? inlinedType)) return false;

        inlined = new CompiledReinterpretation()
        {
            Value = inlinedValue,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
            TypeExpression = inlinedType,
        };
        return true;
    }
    static bool Inline(CompiledReinterpretation statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;
        if (!Inline(statement.TypeExpression, context, out CompiledTypeExpression? inlinedType)) return false;

        inlined = new CompiledReinterpretation()
        {
            Value = inlinedValue,
            TypeExpression = inlinedType,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
            Type = statement.Type,
        };
        return true;
    }
    static bool Inline(CompiledRuntimeCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledArgument> inlinedArguments)) return false;
        if (!Inline(statement.Function, context, out CompiledExpression? inlinedFunction)) return false;

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
    static bool Inline(CompiledFunctionCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledArgument> inlinedArguments)) return false;

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
    static bool Inline(CompiledExternalFunctionCall statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;

        if (!Inline(statement.Arguments, context, out ImmutableArray<CompiledArgument> inlinedArguments)) return false;

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
    static bool Inline(CompiledDummyExpression statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        if (!Inline(statement.Statement, context, out CompiledStatement? inlinedStatement)) return false;

        inlined = new CompiledDummyExpression()
        {
            Statement = inlinedStatement,
            Type = statement.Type,
            Location = statement.Location,
            SaveValue = statement.SaveValue,
        };
        return true;
    }
    static bool Inline(CompiledString statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledStackString statement, InlineContext context, out CompiledExpression inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledVariableDefinition statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        if (!Inline(statement.InitialValue, context, out CompiledExpression? inlinedValue)) return false;
        if (!Inline(statement.TypeExpression, context, out CompiledTypeExpression? inlinedType)) return false;

        CompiledVariableDefinition _inlined = new()
        {
            InitialValue = inlinedValue,
            TypeExpression = inlinedType,
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
        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;

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

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;

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

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;

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

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;

        inlined = new CompiledGoto()
        {
            Value = inlinedValue,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledSetter statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Value, context, out CompiledExpression? inlinedValue)) return false;
        if (!Inline(statement.Target, context, out CompiledStatement? target)) return false;

        inlined = new CompiledSetter()
        {
            Value = inlinedValue,
            Target = (CompiledAccessExpression)target,
            IsCompoundAssignment = statement.IsCompoundAssignment,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledWhileLoop statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Condition, context, out CompiledExpression? inlinedCondition)) return false;
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
        if (statement.Initialization is not null && !Inline(statement.Initialization, context, out inlinedVariableDeclaration)) return false;
        if (!Inline(statement.Condition, context, out CompiledExpression? inlinedCondition)) return false;
        if (!Inline(statement.Step, context, out CompiledStatement? inlinedExpression)) return false;
        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;

        inlined = new CompiledForLoop()
        {
            Initialization = inlinedVariableDeclaration,
            Condition = inlinedCondition,
            Step = inlinedExpression,
            Body = inlinedBody,
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledIf statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;

        if (!Inline(statement.Condition, context, out CompiledExpression? inlinedCondition)) return false;
        if (!Inline(statement.Body, context, out CompiledStatement? inlinedBody)) return false;
        if (!Inline(statement.Next, context, out CompiledStatement? inlinedNext)) return false;

        if (inlinedNext is not CompiledBranch nextBranch) throw new UnreachableException();

        inlined = new CompiledIf()
        {
            Condition = inlinedCondition,
            Body = inlinedBody,
            Next = nextBranch,
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

        ImmutableArray<CompiledStatement>.Builder statements = ImmutableArray.CreateBuilder<CompiledStatement>(statement.Statements.Length);
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
            Statements = statements.DrainToImmutable(),
            Location = statement.Location,
        };
        return true;
    }
    static bool Inline(CompiledLabelDeclaration statement, InlineContext context, out CompiledStatement inlined)
    {
        inlined = statement;
        return true;
    }
    static bool Inline(CompiledEmptyStatement statement, InlineContext context, out CompiledStatement inlined)
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
            case CompiledVariableDefinition v: return Inline(v, context, out inlined);
            case CompiledReturn v: return Inline(v, context, out inlined);
            case CompiledCrash v: return Inline(v, context, out inlined);
            case CompiledBreak v: return Inline(v, context, out inlined);
            case CompiledDelete v: return Inline(v, context, out inlined);
            case CompiledGoto v: return Inline(v, context, out inlined);
            case CompiledSetter v: return Inline(v, context, out inlined);
            case CompiledWhileLoop v: return Inline(v, context, out inlined);
            case CompiledForLoop v: return Inline(v, context, out inlined);
            case CompiledIf v: return Inline(v, context, out inlined);
            case CompiledElse v: return Inline(v, context, out inlined);
            case CompiledBlock v: return Inline(v, context, out inlined);
            case CompiledLabelDeclaration v: return Inline(v, context, out inlined);
            case CompiledEmptyStatement v: return Inline(v, context, out inlined);
            case CompiledExpression v:
                if (Inline(v, context, out CompiledExpression inlinedWithValue))
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
    static bool Inline(CompiledExpression? statement, InlineContext context, [NotNullIfNotNull(nameof(statement))] out CompiledExpression? inlined)
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
            CompiledConstantValue v => Inline(v, context, out inlined),
            CompiledRegisterAccess v => Inline(v, context, out inlined),
            CompiledVariableAccess v => Inline(v, context, out inlined),
            CompiledExpressionVariableAccess v => Inline(v, context, out inlined),
            CompiledParameterAccess v => Inline(v, context, out inlined),
            CompiledFunctionReference v => Inline(v, context, out inlined),
            CompiledLabelReference v => Inline(v, context, out inlined),
            CompiledFieldAccess v => Inline(v, context, out inlined),
            CompiledElementAccess v => Inline(v, context, out inlined),
            CompiledGetReference v => Inline(v, context, out inlined),
            CompiledDereference v => Inline(v, context, out inlined),
            CompiledStackAllocation v => Inline(v, context, out inlined),
            CompiledConstructorCall v => Inline(v, context, out inlined),
            CompiledCast v => Inline(v, context, out inlined),
            CompiledReinterpretation v => Inline(v, context, out inlined),
            CompiledRuntimeCall v => Inline(v, context, out inlined),
            CompiledFunctionCall v => Inline(v, context, out inlined),
            CompiledExternalFunctionCall v => Inline(v, context, out inlined),
            CompiledDummyExpression v => Inline(v, context, out inlined),
            CompiledString v => Inline(v, context, out inlined),
            CompiledStackString v => Inline(v, context, out inlined),

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
        KeywordCallStatement v => FindControlFlowUsage(v, inDepth),
        WhileLoopStatement v => FindControlFlowUsage(v.Body, true),
        ForLoopStatement v => FindControlFlowUsage(v.Block.Statements, true),
        IfContainer v => FindControlFlowUsage(v.Branches, true),
        BranchStatementBase v => FindControlFlowUsage(v.Body, true),

        SimpleAssignmentStatement => ControlFlowUsage.None,
        VariableDefinition => ControlFlowUsage.None,
        AnyCallExpression => ControlFlowUsage.None,
        ShortOperatorCall => ControlFlowUsage.None,
        CompoundAssignmentStatement => ControlFlowUsage.None,
        BinaryOperatorCallExpression => ControlFlowUsage.None,
        IdentifierExpression => ControlFlowUsage.None,
        ConstructorCallExpression => ControlFlowUsage.None,
        FieldExpression => ControlFlowUsage.None,

        _ => throw new NotImplementedException(statement.GetType().Name),
    };
    static ControlFlowUsage FindControlFlowUsage(KeywordCallStatement statement, bool inDepth = false)
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
            UnaryOperatorCallExpression.LogicalNOT => !left,
            UnaryOperatorCallExpression.BinaryNOT => ~left,
            UnaryOperatorCallExpression.UnaryPlus => +left,
            UnaryOperatorCallExpression.UnaryMinus => -left,

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
                BinaryOperatorCallExpression.Addition => left + right,
                BinaryOperatorCallExpression.Subtraction => left - right,
                BinaryOperatorCallExpression.Multiplication => left * right,
                BinaryOperatorCallExpression.Division => left / right,
                BinaryOperatorCallExpression.Modulo => left % right,

                BinaryOperatorCallExpression.LogicalAND => new CompiledValue((bool)left && (bool)right),
                BinaryOperatorCallExpression.LogicalOR => new CompiledValue((bool)left || (bool)right),

                BinaryOperatorCallExpression.BitwiseAND => left & right,
                BinaryOperatorCallExpression.BitwiseOR => left | right,
                BinaryOperatorCallExpression.BitwiseXOR => left ^ right,

                BinaryOperatorCallExpression.BitshiftLeft => left << right,
                BinaryOperatorCallExpression.BitshiftRight => left >> right,

                BinaryOperatorCallExpression.CompLT => new CompiledValue(left < right),
                BinaryOperatorCallExpression.CompGT => new CompiledValue(left > right),
                BinaryOperatorCallExpression.CompEQ => new CompiledValue(left == right),
                BinaryOperatorCallExpression.CompNEQ => new CompiledValue(left != right),
                BinaryOperatorCallExpression.CompLEQ => new CompiledValue(left <= right),
                BinaryOperatorCallExpression.CompGEQ => new CompiledValue(left >= right),

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

    static bool TryComputeSimple(LiteralExpression literal, out CompiledValue value)
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
    static bool TryComputeSimple(BinaryOperatorCallExpression @operator, out CompiledValue value)
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
    static bool TryComputeSimple(UnaryOperatorCallExpression @operator, out CompiledValue value)
    {
        if (!TryComputeSimple(@operator.Expression, out CompiledValue leftValue))
        {
            value = CompiledValue.Null;
            return false;
        }

        switch (@operator.Operator.Content)
        {
            case UnaryOperatorCallExpression.LogicalNOT:
                value = !leftValue;
                return true;
            case UnaryOperatorCallExpression.BinaryNOT:
                value = ~leftValue;
                return true;
            case UnaryOperatorCallExpression.UnaryPlus:
                value = +leftValue;
                return true;
            case UnaryOperatorCallExpression.UnaryMinus:
                value = -leftValue;
                return true;
            default:
                value = leftValue;
                return true;
        }
    }
    static bool TryComputeSimple(IndexCallExpression indexCall, out CompiledValue value)
    {
        if (indexCall.Object is LiteralExpression literal &&
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
    public static bool TryComputeSimple(Expression? statement, out CompiledValue value)
    {
        value = CompiledValue.Null;
        return statement switch
        {
            LiteralExpression v => TryComputeSimple(v, out value),
            BinaryOperatorCallExpression v => TryComputeSimple(v, out value),
            UnaryOperatorCallExpression v => TryComputeSimple(v, out value),
            IndexCallExpression v => TryComputeSimple(v, out value),
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
        public readonly Dictionary<CompiledVariableDefinition, CompiledValue> Variables = new();
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

        public bool TryGetVariable(CompiledVariableDefinition name, out CompiledValue value)
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

        public bool TrySetVariable(CompiledVariableDefinition name, CompiledValue value)
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

    bool TryCompute(CompiledDereference pointer, EvaluationContext context, out CompiledValue value)
    {
        if (pointer.Address is CompiledGetReference addressGetter)
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
            case UnaryOperatorCallExpression.LogicalNOT:
                value = !leftValue;
                return true;
            case UnaryOperatorCallExpression.BinaryNOT:
                value = ~leftValue;
                return true;
            case UnaryOperatorCallExpression.UnaryPlus:
                value = +leftValue;
                return true;
            case UnaryOperatorCallExpression.UnaryMinus:
                value = -leftValue;
                return true;
            default:
                value = CompiledValue.Null;
                return false;
        }
    }
    bool TryCompute(CompiledConstantValue literal, EvaluationContext context, out CompiledValue value)
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

        if (TryEvaluate(found, parameters, context, out CompiledValue? returnValue, out ImmutableArray<RuntimeStatement2> runtimeStatements)
            && returnValue.HasValue
            && runtimeStatements.Length == 0)
        {
            value = returnValue.Value;
            return true;
        }

        return false;
    }
    bool TryCompute(CompiledSizeof functionCall, EvaluationContext context, out CompiledValue value)
    {
        if (!FindSize(functionCall.Of, out int size, out _, this))
        {
            value = CompiledValue.Null;
            return false;
        }

        value = new CompiledValue(size);
        return true;
    }
    bool TryCompute(CompiledVariableAccess identifier, EvaluationContext context, out CompiledValue value)
    {
        if (!context.TryGetVariable(identifier.Variable, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        return true;
    }
    bool TryCompute(CompiledParameterAccess identifier, EvaluationContext context, out CompiledValue value)
    {
        if (!context.TryGetParameter(identifier.Parameter.Identifier.Content, out value))
        {
            value = CompiledValue.Null;
            return false;
        }

        return true;
    }
    bool TryCompute(CompiledReinterpretation typeCast, EvaluationContext context, out CompiledValue value)
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
    bool TryCompute(CompiledCast typeCast, EvaluationContext context, out CompiledValue value)
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
    bool TryCompute(CompiledElementAccess indexCall, EvaluationContext context, out CompiledValue value)
    {
        if (!TryCompute(indexCall.Index, context, out CompiledValue index))
        {
            value = CompiledValue.Null;
            return false;
        }

        if (indexCall.Base is CompiledString stringInstance)
        {
            if (index == stringInstance.Value.Length)
            { value = new CompiledValue('\0'); }
            else
            { value = new CompiledValue(stringInstance.Value[(int)index]); }
            return true;
        }

        if (indexCall.Base is CompiledStackString stackString)
        {
            if (index == stackString.Value.Length)
            { value = new CompiledValue('\0'); }
            else
            { value = new CompiledValue(stackString.Value[(int)index]); }
            return true;
        }

        if (indexCall.Base is CompiledList listLiteral &&
            TryCompute(listLiteral.Values[(int)index], context, out value))
        {
            return true;
        }

        value = CompiledValue.Null;
        return false;
    }
    bool TryCompute(CompiledFunctionReference functionAddressGetter, EvaluationContext context, out CompiledValue value)
    {
        value = CompiledValue.Null;
        return false;
    }

    bool TryCompute([NotNullWhen(true)] CompiledExpression? statement, out CompiledValue value)
        => TryCompute(statement, EvaluationContext.Empty, out value);
    bool TryCompute(IEnumerable<CompiledExpression>? statements, EvaluationContext context, [NotNullWhen(true)] out ImmutableArray<CompiledValue> values)
    {
        if (statements is null)
        {
            values = ImmutableArray<CompiledValue>.Empty;
            return false;
        }

        ImmutableArray<CompiledValue>.Builder result = ImmutableArray.CreateBuilder<CompiledValue>();
        foreach (CompiledExpression statement in statements)
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
    bool TryCompute([NotNullWhen(true)] CompiledExpression? statement, EvaluationContext context, out CompiledValue value)
    {
        value = CompiledValue.Null;
        return statement switch
        {
            CompiledConstantValue v => TryCompute(v, context, out value),
            CompiledBinaryOperatorCall v => TryCompute(v, context, out value),
            CompiledUnaryOperatorCall v => TryCompute(v, context, out value),
            CompiledDereference v => TryCompute(v, context, out value),
            CompiledFunctionCall v => TryCompute(v, context, out value),
            CompiledSizeof v => TryCompute(v, context, out value),
            CompiledVariableAccess v => TryCompute(v, context, out value),
            CompiledParameterAccess v => TryCompute(v, context, out value),
            CompiledReinterpretation v => TryCompute(v, context, out value),
            CompiledCast v => TryCompute(v, context, out value),
            CompiledElementAccess v => TryCompute(v, context, out value),
            CompiledArgument v => TryCompute(v.Value, context, out value),
            CompiledFunctionReference v => TryCompute(v, context, out value),
            CompiledLambda => false, // TODO

            CompiledString => false,
            CompiledStackString => false,
            CompiledExternalFunctionCall => false,
            CompiledRuntimeCall => false,
            CompiledFieldAccess => false,
            CompiledStackAllocation => false,
            CompiledConstructorCall => false,
            CompiledGetReference => false,
            CompiledList => false,
            CompiledDummyExpression => false,
            CompiledRegisterAccess => false,
            CompiledLabelReference => false,
            CompiledExpressionVariableAccess => false,
            null => false,

            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };
    }

    bool TryEvaluate(ICompiledFunctionDefinition function, ImmutableArray<CompiledArgument> parameters, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out ImmutableArray<RuntimeStatement2> runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        CompiledFunction? found = GeneratedFunctions.FirstOrDefault(v => v.Function == function);

        if (found is null)
        { return false; }

        if (TryCompute(parameters, context, out ImmutableArray<CompiledValue> parameterValues)
            && TryEvaluate(found, parameterValues, context, out value, out runtimeStatements))
        { return true; }

        return false;
    }
    bool TryEvaluate(ICompiledFunctionDefinition function, ImmutableArray<CompiledExpression> parameters, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out ImmutableArray<RuntimeStatement2> runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        CompiledFunction? found = GeneratedFunctions.FirstOrDefault(v => v.Function == function);

        if (found is null)
        {
            return false;
        }

        if (TryCompute(parameters, context, out ImmutableArray<CompiledValue> parameterValues)
            && TryEvaluate(found, parameterValues, context, out value, out runtimeStatements))
        { return true; }

        return false;
    }
    bool TryEvaluate(CompiledFunction function, ImmutableArray<CompiledValue> parameterValues, EvaluationContext context, out CompiledValue? value, [NotNullWhen(true)] out ImmutableArray<RuntimeStatement2> runtimeStatements)
    {
        value = null;
        runtimeStatements = default;

        {
            ImmutableArray<CompiledValue>.Builder castedParameterValues = ImmutableArray.CreateBuilder<CompiledValue>(parameterValues.Length);
            for (int i = 0; i < parameterValues.Length; i++)
            {
                if (!parameterValues[i].TryCast(function.Function.Parameters[i].Type, out CompiledValue castedValue))
                {
                    // Debugger.Break();
                    return false;
                }

                if (!function.Function.Parameters[i].Type.SameAs(castedValue.Type))
                {
                    // Debugger.Break();
                    return false;
                }

                castedParameterValues.Add(castedValue);
            }
            parameterValues = castedParameterValues.MoveToImmutable();
        }

        if (function.Function.ReturnSomething)
        {
            if (!function.Function.Type.Is<BuiltinType>())
            { return false; }
        }

        if (context.Frames.Count > 8)
        { return false; }

        using (context.Frames.PushAuto(new EvaluationFrame(function)))
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

        runtimeStatements = context.RuntimeStatements.ToImmutableArray();

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

        if (forLoop.Initialization is not null && !TryEvaluate(forLoop.Initialization, context))
        { return false; }

        while (true)
        {
            CompiledValue condition;
            if (forLoop.Condition is null)
            { condition = true; }
            else if (!TryCompute(forLoop.Condition, context, out condition))
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

            if (forLoop.Step is not null && !TryEvaluate(forLoop.Step, context))
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
    bool TryEvaluate(CompiledVariableDefinition variableDeclaration, EvaluationContext context)
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
    bool TryEvaluate(CompiledSetter anyAssignment, EvaluationContext context)
    {
        if (!TryCompute(anyAssignment.Value, context, out CompiledValue value))
        { return false; }

        if (anyAssignment.Target is CompiledVariableAccess targetVariable)
        {
            if (!context.TrySetVariable(targetVariable.Variable, value))
            { return false; }
        }
        else if (anyAssignment.Target is CompiledParameterAccess targetParameter)
        {
            if (!context.TrySetParameter(targetParameter.Parameter.Identifier.Content, value))
            { return false; }
        }
        else
        {
            return false;
        }

        return true;
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
        CompiledExpression v => TryCompute(v, context, out _),
        CompiledBlock v => TryEvaluate(v, context),
        CompiledVariableDefinition v => TryEvaluate(v, context),
        CompiledWhileLoop v => TryEvaluate(v, context),
        CompiledForLoop v => TryEvaluate(v, context),
        CompiledSetter v => TryEvaluate(v, context),
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

    public static bool FindBitWidth(GeneralType type, out BitWidth size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;
        if (!FindSize(type, out int s, out error, runtime)) return false;
        switch (s)
        {
            case 1: size = BitWidth._8; return true;
            case 2: size = BitWidth._16; return true;
            case 4: size = BitWidth._32; return true;
            case 8: size = BitWidth._64; return true;
            default:
                error = new PossibleDiagnostic($"E");
                return false;
        }
    }

    public static bool FindSize(GeneralType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime) => type switch
    {
        PointerType v => FindSize(v, out size, out error, runtime),
        ArrayType v => FindSize(v, out size, out error, runtime),
        FunctionType v => FindSize(v, out size, out error, runtime),
        StructType v => FindSize(v, out size, out error, runtime),
        GenericType v => FindSize(v, out size, out error, runtime),
        BuiltinType v => FindSize(v, out size, out error, runtime),
        AliasType v => FindSize(v, out size, out error, runtime),
        _ => throw new NotImplementedException(),
    };
    static bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = runtime.PointerSize;
        error = null;
        return true;
    }
    static bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = runtime.PointerSize;
        error = null;
        return true;
    }
    static bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error, runtime)) return false;

        size = elementSize * type.Length.Value;
        error = null;
        return true;
    }
    static bool FindSize(StructType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = 0;

        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!FindSize(fieldType, out int fieldSize, out error, runtime)) return false;
            size += fieldSize;
        }

        error = null;
        return true;
    }
    static bool FindSize(GenericType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;
        error = new PossibleDiagnostic($"Generic type doesn't have a size");
        return false;
    }
    static bool FindSize(BuiltinType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
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
    static bool FindSize(AliasType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        return FindSize(type.Value, out size, out error, runtime);
    }

    public static bool FindSize(CompiledTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime) => type switch
    {
        CompiledPointerTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledArrayTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledFunctionTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledStructTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledGenericTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledBuiltinTypeExpression v => FindSize(v, out size, out error, runtime),
        CompiledAliasTypeExpression v => FindSize(v, out size, out error, runtime),
        _ => throw new NotImplementedException(),
    };
    static bool FindSize(CompiledPointerTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = runtime.PointerSize;
        error = null;
        return true;
    }
    static bool FindSize(CompiledFunctionTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = runtime.PointerSize;
        error = null;
        return true;
    }
    static bool FindSize(CompiledArrayTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size", type);
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error, runtime)) return false;

        if (type.Length is not CompiledConstantValue evaluatedStatement)
        {
            error = new PossibleDiagnostic($"Can't compute the array type's length", type.Length);
            return false;
        }

        size = elementSize * (int)evaluatedStatement.Value;
        error = null;
        return true;
    }
    static bool FindSize(CompiledStructTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = 0;

        foreach (CompiledField field in type.Struct.Fields)
        {
            if (!FindSize(field.Type, out int fieldSize, out error, runtime)) return false;
            size += fieldSize;
        }

        error = null;
        return true;
    }
    static bool FindSize(CompiledGenericTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;
        error = new PossibleDiagnostic($"Generic type doesn't have a size", type);
        return false;
    }
    static bool FindSize(CompiledBuiltinTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        size = default;
        error = default;
        switch (type.Type)
        {
            case BasicType.Void: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\"", type); return false;
            case BasicType.Any: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\"", type); return false;
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
    static bool FindSize(CompiledAliasTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error, IRuntimeInfoProvider runtime)
    {
        return FindSize(type.Value, out size, out error, runtime);
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

    StatementComplexity GetStatementComplexity(IEnumerable<CompiledExpression> statements)
    {
        StatementComplexity res = StatementComplexity.None;
        foreach (CompiledExpression statement in statements) res |= GetStatementComplexity(statement);
        return res;
    }

    StatementComplexity GetStatementComplexity(CompiledTypeExpression statement) => statement.FinalValue switch
    {
        CompiledArrayTypeExpression v => (v.Length is null || v.ComputedLength.HasValue) ? StatementComplexity.None : GetStatementComplexity(v.Length),
        CompiledBuiltinTypeExpression => StatementComplexity.None,
        CompiledFunctionTypeExpression v => GetStatementComplexity(v.ReturnType) | v.Parameters.Select(GetStatementComplexity).Aggregate(StatementComplexity.None, (a, b) => a | b),
        CompiledGenericTypeExpression => StatementComplexity.Bruh,
        CompiledPointerTypeExpression v => GetStatementComplexity(v.To),
        CompiledStructTypeExpression v => v.TypeArguments.Values.Select(GetStatementComplexity).Aggregate(StatementComplexity.None, (a, b) => a | b),
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };

    StatementComplexity GetStatementComplexity(CompiledExpression statement) => statement switch
    {
        CompiledSizeof v => GetStatementComplexity(v.Of),
        CompiledArgument v => GetStatementComplexity(v.Value) | ((v.Cleanup.Destructor is not null || v.Cleanup.Deallocator is not null) ? StatementComplexity.Complex | StatementComplexity.Volatile : StatementComplexity.None),
        CompiledBinaryOperatorCall v => GetStatementComplexity(v.Left) | GetStatementComplexity(v.Right) | StatementComplexity.Complex,
        CompiledUnaryOperatorCall v => GetStatementComplexity(v.Left) | StatementComplexity.Complex,
        CompiledConstantValue => StatementComplexity.None,
        CompiledRegisterAccess => StatementComplexity.None,
        CompiledVariableAccess => StatementComplexity.None,
        CompiledExpressionVariableAccess => StatementComplexity.None,
        CompiledParameterAccess => StatementComplexity.None,
        CompiledFunctionReference => StatementComplexity.None,
        CompiledLabelReference => StatementComplexity.None,
        CompiledFieldAccess v => GetStatementComplexity(v.Object),
        CompiledElementAccess v => GetStatementComplexity(v.Base) | GetStatementComplexity(v.Index),
        CompiledGetReference v => GetStatementComplexity(v.Of),
        CompiledDereference v => GetStatementComplexity(v.Address),
        CompiledStackAllocation => StatementComplexity.Bruh,
        CompiledConstructorCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledCast v => GetStatementComplexity(v.Value) | StatementComplexity.Complex,
        CompiledReinterpretation v => GetStatementComplexity(v.Value),
        CompiledRuntimeCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledFunctionCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledExternalFunctionCall v => GetStatementComplexity(v.Arguments) | StatementComplexity.Volatile,
        CompiledDummyExpression => StatementComplexity.Bruh,
        CompiledString => StatementComplexity.Complex | StatementComplexity.Volatile,
        CompiledStackString => StatementComplexity.Bruh,
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

    static IEnumerable<CompiledStatement> Visit(IEnumerable<CompiledTypeExpression> type)
    {
        foreach (CompiledTypeExpression v in type)
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
                break;
        }
    }

    static IEnumerable<CompiledStatement> Visit(CompiledTypeExpression? type)
    {
        switch (type)
        {
            case CompiledBuiltinTypeExpression:
                break;
            case CompiledAliasTypeExpression v:
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledPointerTypeExpression v:
                foreach (CompiledStatement v2 in Visit(v.To)) yield return v2;
                break;
            case CompiledFunctionTypeExpression v:
                foreach (CompiledStatement v2 in Visit(v.ReturnType)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Parameters)) yield return v2;
                break;
            case CompiledGenericTypeExpression:
                break;
            case CompiledStructTypeExpression v:
                foreach (CompiledStatement v2 in Visit(v.TypeArguments.Values)) yield return v2;
                break;
            case CompiledArrayTypeExpression v:
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
            case CompiledVariableDefinition v:
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
            case CompiledConstantValue v:
                yield return v;
                break;
            case CompiledRegisterAccess v:
                yield return v;
                break;
            case CompiledVariableAccess v:
                yield return v;
                break;
            case CompiledExpressionVariableAccess v:
                yield return v;
                break;
            case CompiledParameterAccess v:
                yield return v;
                break;
            case CompiledFunctionReference v:
                yield return v;
                break;
            case CompiledLabelReference v:
                yield return v;
                break;
            case CompiledFieldAccess v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Object)) yield return v2;
                break;
            case CompiledElementAccess v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Base)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Index)) yield return v2;
                break;
            case CompiledGetReference v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Of)) yield return v2;
                break;
            case CompiledSetter v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Target)) yield return v2;
                break;
            case CompiledDereference v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Address)) yield return v2;
                break;
            case CompiledWhileLoop v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Condition)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Body)) yield return v2;
                break;
            case CompiledForLoop v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Initialization)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Condition)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Step)) yield return v2;
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
            case CompiledCast v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Type)) yield return v2;
                foreach (CompiledStatement v2 in Visit(v.Value)) yield return v2;
                break;
            case CompiledReinterpretation v:
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
            case CompiledLabelDeclaration v:
                yield return v;
                break;
            case CompiledDummyExpression v:
                yield return v;
                break;
            case CompiledString v:
                yield return v;
                foreach (CompiledStatement v2 in Visit(v.Allocator)) yield return v2;
                break;
            case CompiledStackString v:
                yield return v;
                break;
            case CompiledEmptyStatement v:
                yield return v;
                break;
            case CompiledArgument v:
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
            if (!compiledBlock.Statements.Any(v => v is CompiledVariableDefinition or CompiledLabelDeclaration))
            {
                return ReduceStatements(compiledBlock.Statements);
            }
        }

        if (statement is not CompiledExpression statementWithValue)
        {
            return ImmutableArray.Create(statement);
        }

        if (statementWithValue.SaveValue && !forceDiscardValue)
        {
            return ImmutableArray.Create(statement);
        }

        return statementWithValue switch
        {
            CompiledArgument v => ReduceStatements(v.Value, true),
            CompiledBinaryOperatorCall v => ReduceStatements(v.Left, true).AddRange(ReduceStatements(v.Right, true)),
            CompiledUnaryOperatorCall v => ReduceStatements(v.Left, true),
            CompiledElementAccess v => ReduceStatements(v.Base, true).AddRange(ReduceStatements(v.Index, true)),
            CompiledGetReference v => ReduceStatements(v.Of, true),
            CompiledDereference v => ReduceStatements(v.Address, true),
            CompiledConstructorCall v => ReduceStatements(v.Arguments, true),
            CompiledCast v => ReduceStatements(v.Value, true),
            CompiledReinterpretation v => ReduceStatements(v.Value, true),
            CompiledDummyExpression v => ReduceStatements(v.Statement),

            CompiledRuntimeCall or
            CompiledFunctionCall or
            CompiledExternalFunctionCall => ImmutableArray.Create(statement),

            CompiledSizeof or
            CompiledConstantValue or
            CompiledRegisterAccess or
            CompiledVariableAccess or
            CompiledExpressionVariableAccess or
            CompiledParameterAccess or
            CompiledFunctionReference or
            CompiledLabelReference or
            CompiledFieldAccess or
            CompiledStackAllocation or
            CompiledString or
            CompiledStackString => ImmutableArray<CompiledStatement>.Empty,
            _ => throw new NotImplementedException(),
        };
    }

    bool CompileType(TypeInstance type, [NotNullWhen(true)] out GeneralType? result, DiagnosticsCollection diagnostics)
    {
        if (!CompileStatement(type, out CompiledTypeExpression? typeExpression, diagnostics))
        {
            result = null;
            return false;
        }

        if (!CompileType(typeExpression, out result, out PossibleDiagnostic? typeError))
        {
            diagnostics.Add(typeError.ToError(type));
            result = null;
            return false;
        }

        return true;
    }
    public static bool CompileType(CompiledTypeExpression typeExpression, [NotNullWhen(true)] out GeneralType? type, [NotNullWhen(false)] out PossibleDiagnostic? error, bool ignoreValues = false)
    {
        type = null;
        error = null;

        switch (typeExpression)
        {
            case CompiledAliasTypeExpression v:
            {
                if (!CompileType(v.Value, out GeneralType? aliasValue, out error, ignoreValues)) return false;
                type = new AliasType(aliasValue, v.Definition);
                return true;
            }
            case CompiledArrayTypeExpression v:
            {
                if (!CompileType(v.Of, out GeneralType? ofType, out error, ignoreValues)) return false;
                if (v.Length is not null)
                {
                    static bool IsValidArrayLength(CompiledExpression expression, out int result, [NotNullWhen(false)] out PossibleDiagnostic? error)
                    {
                        result = default;
                        error = null;

                        if (expression is not CompiledConstantValue constantLength)
                        {
                            error = new PossibleDiagnostic($"Array type's length must be constant", expression);
                            return false;
                        }
                        if (constantLength.Value.Type == RuntimeType.F32)
                        {
                            error = new PossibleDiagnostic($"Array type's length cannot be a float", expression);
                            return false;
                        }
                        if (constantLength.Value.Type == RuntimeType.Null)
                        {
                            error = new PossibleDiagnostic($"Array type's length cannot be null", expression);
                            return false;
                        }
                        if (constantLength.Value > int.MaxValue)
                        {
                            error = new PossibleDiagnostic($"Array type's length cannot be more than {int.MaxValue}", expression);
                            return false;
                        }
                        if (constantLength.Value < 1)
                        {
                            error = new PossibleDiagnostic($"Array type's length cannot be less than {1}", expression);
                            return false;
                        }
                        if (constantLength.Value != (int)constantLength.Value)
                        {
                            error = new PossibleDiagnostic($"Invalid array length {constantLength.Value}", expression);
                            return false;
                        }

                        result = (int)constantLength.Value;
                        return true;
                    }

                    if (IsValidArrayLength(v.Length, out int length, out PossibleDiagnostic? lengthError))
                    {
                        type = new ArrayType(ofType, length);
                    }
                    else
                    {
                        if (ignoreValues)
                        {
                            type = new ArrayType(ofType, null);
                        }
                        else
                        {
                            error = lengthError;
                            return false;
                        }
                    }
                }
                else
                {
                    type = new ArrayType(ofType, null);
                }
                return true;
            }
            case CompiledBuiltinTypeExpression v:
            {
                type = new BuiltinType(v.Type);
                return true;
            }
            case CompiledFunctionTypeExpression v:
            {
                if (!CompileType(v.ReturnType, out GeneralType? returnType, out error, ignoreValues)) return false;
                GeneralType[] parameters = new GeneralType[v.Parameters.Length];
                for (int i = 0; i < v.Parameters.Length; i++)
                {
                    if (!CompileType(v.Parameters[i], out parameters[i]!, out error, ignoreValues)) return false;
                }
                type = new FunctionType(returnType, parameters.AsImmutableUnsafe(), v.HasClosure);
                return true;
            }
            case CompiledGenericTypeExpression v:
            {
                type = new GenericType(v.Identifier, v.File);
                return true;
            }
            case CompiledPointerTypeExpression v:
            {
                if (!CompileType(v.To, out GeneralType? toType, out error, ignoreValues)) return false;
                type = new PointerType(toType);
                return true;
            }
            case CompiledStructTypeExpression v:
            {
                Dictionary<string, GeneralType> typeArguments = new(v.TypeArguments.Count);
                foreach (KeyValuePair<string, CompiledTypeExpression> item in v.TypeArguments)
                {
                    if (!CompileType(item.Value, out GeneralType? itemV, out error, ignoreValues)) return false;
                    typeArguments.Add(item.Key, itemV);
                }
                type = new StructType(v.Struct, v.File, typeArguments.ToImmutableDictionary());
                return true;
            }
            default: throw new UnreachableException();
        }
    }
}
