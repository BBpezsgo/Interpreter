﻿namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using LiteralStatement = Parser.Statement.Literal;

public abstract class CodeGenerator
{
    public readonly struct CompliableTemplate<T> where T : ITemplateable<T>
    {
        public readonly T OriginalFunction;
        public readonly T Function;
        public readonly Dictionary<string, GeneralType> TypeArguments;

        public CompliableTemplate(T function, Dictionary<string, GeneralType> typeArguments)
        {
            OriginalFunction = function;
            TypeArguments = typeArguments;

            CodeGenerator.ValidateTypeArguments(TypeArguments);
            Function = OriginalFunction.InstantiateTemplate(typeArguments);
        }

        public override string ToString() => Function?.ToString() ?? "null";
    }

    public readonly struct ControlFlowBlock
    {
        public int FlagAddress { get; }
        public Stack<int> PendingJumps { get; }
        public Stack<bool> Doings { get; }

        public ControlFlowBlock(int flagAddress)
        {
            FlagAddress = flagAddress;
            PendingJumps = new Stack<int>();
            Doings = new Stack<bool>();

            PendingJumps.Push(0);
            Doings.Push(false);
        }
    }

    #region Protected Fields

    protected ImmutableArray<CompiledStruct> CompiledStructs;
    protected ImmutableArray<CompiledFunction> CompiledFunctions;
    protected ImmutableArray<CompiledOperator> CompiledOperators;
    protected ImmutableArray<CompiledConstructor> CompiledConstructors;
    protected ImmutableArray<CompiledGeneralFunction> CompiledGeneralFunctions;
    protected ImmutableArray<IConstant> CompiledGlobalConstants;

    protected readonly Stack<IConstant> CompiledLocalConstants;
    protected readonly Stack<int> LocalConstantsStack;

    protected readonly Stack<CompiledParameter> CompiledParameters;
    protected readonly Stack<CompiledVariable> CompiledVariables;
    protected readonly Stack<CompiledVariable> CompiledGlobalVariables;

    protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
    protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledConstructor>> CompilableConstructors => compilableConstructors;

    protected Uri? CurrentFile;
    protected bool InFunction;
    protected readonly Dictionary<string, GeneralType> TypeArguments;
    protected DebugInformation? DebugInfo;

    protected readonly PrintCallback? Print;
    protected readonly AnalysisCollection? AnalysisCollection;

    #endregion

    #region Private Fields

    readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
    readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
    readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();
    readonly List<CompliableTemplate<CompiledConstructor>> compilableConstructors = new();

    #endregion

    protected CodeGenerator(CompilerResult compilerResult, AnalysisCollection? analysisCollection, PrintCallback? print)
    {
        CompiledGlobalConstants = ImmutableArray.Create<IConstant>();

        CompiledLocalConstants = new Stack<IConstant>();
        LocalConstantsStack = new Stack<int>();

        CompiledParameters = new Stack<CompiledParameter>();
        CompiledVariables = new Stack<CompiledVariable>();
        CompiledGlobalVariables = new Stack<CompiledVariable>();

        compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
        compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
        compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();

        CurrentFile = null;
        InFunction = false;
        TypeArguments = new Dictionary<string, GeneralType>();

        CompiledStructs = compilerResult.Structs;
        CompiledFunctions = compilerResult.Functions;
        CompiledOperators = compilerResult.Operators;
        CompiledConstructors = compilerResult.Constructors;
        CompiledGeneralFunctions = compilerResult.GeneralFunctions;

        AnalysisCollection = analysisCollection;
        Print = print;
    }

    public static void ValidateTypeArguments(IEnumerable<KeyValuePair<string, GeneralType>> arguments)
    {
        foreach (KeyValuePair<string, GeneralType> pair in arguments)
        {
            if (pair.Value is GenericType)
            { throw new InternalException($"{pair.Value} is generic"); }
        }
    }

    protected void CleanupLocalConstants()
    {
        int count = LocalConstantsStack.Pop();
        CompiledLocalConstants.Pop(count);
    }

    protected bool GetConstant(string identifier, [NotNullWhen(true)] out IConstant? constant)
    {
        constant = null;

        foreach (IConstant _constant in CompiledLocalConstants)
        {
            if (_constant.Identifier != identifier)
            { continue; }

            if (constant is not null)
            { throw new CompilerException($"Constant \"{constant.Identifier}\" defined more than once", constant, constant.File); }

            constant = _constant;
        }

        foreach (IConstant _constant in CompiledGlobalConstants)
        {
            if (_constant.Identifier != identifier)
            { continue; }

            if (constant is not null)
            { throw new CompilerException($"Constant \"{constant.Identifier}\" defined more than once", constant, constant.File); }

            constant = _constant;
        }

        return constant is not null;
    }

    protected bool StatementCanBeDeallocated(StatementWithValue statement, out bool explicitly)
    {
        if (statement is ModifiedStatement modifiedStatement &&
            modifiedStatement.Modifier.Equals(ModifierKeywords.Temp))
        {
            if (modifiedStatement.Statement is
                LiteralStatement or
                BinaryOperatorCall or
                UnaryOperatorCall)
            {
                AnalysisCollection?.Hints.Add(new Hint($"Unnecessary explicit temp modifier ({modifiedStatement.Statement.GetType().Name} statements are implicitly deallocated)", modifiedStatement.Modifier, CurrentFile));
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

    #region AddCompilable()

    protected void AddCompilable(CompliableTemplate<CompiledFunction> compilable)
    {
        for (int i = 0; i < compilableFunctions.Count; i++)
        {
            if (compilableFunctions[i].Function.IsSame(compilable.Function))
            { return; }
        }
        compilableFunctions.Add(compilable);
    }

    protected void AddCompilable(CompliableTemplate<CompiledOperator> compilable)
    {
        for (int i = 0; i < compilableOperators.Count; i++)
        {
            if (compilableOperators[i].Function.IsSame(compilable.Function))
            { return; }
        }
        compilableOperators.Add(compilable);
    }

    protected void AddCompilable(CompliableTemplate<CompiledGeneralFunction> compilable)
    {
        for (int i = 0; i < compilableGeneralFunctions.Count; i++)
        {
            if (compilableGeneralFunctions[i].Function.IsSame(compilable.Function))
            { return; }
        }
        compilableGeneralFunctions.Add(compilable);
    }

    protected void AddCompilable(CompliableTemplate<CompiledConstructor> compilable)
    {
        for (int i = 0; i < compilableConstructors.Count; i++)
        {
            if (compilableConstructors[i].Function.IsSame(compilable.Function))
            { return; }
        }
        compilableConstructors.Add(compilable);
    }

    #endregion

    #region SetTypeArguments()

    protected void SetTypeArguments(Dictionary<string, GeneralType> arguments)
    {
        TypeArguments.Clear();
        TypeArguments.AddRange(arguments);
    }

    protected void SetTypeArguments(ImmutableDictionary<string, GeneralType> arguments)
    {
        TypeArguments.Clear();
        TypeArguments.AddRange(arguments);
    }

    protected void SetTypeArguments(Dictionary<string, GeneralType> arguments, out Dictionary<string, GeneralType> old)
    {
        old = new Dictionary<string, GeneralType>(TypeArguments);
        TypeArguments.Clear();
        TypeArguments.AddRange(arguments);
    }

    #endregion

    protected virtual bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (GetVariable(symbolName, out CompiledVariable? variable))
        {
            type = variable.Type;
            return true;
        }

        if (GetParameter(symbolName, out CompiledParameter? parameter))
        {
            type = parameter.Type;
            return true;
        }

        type = null;
        return false;
    }

    #region Get Functions ...

    public static class FunctionQuery
    {
        public static FunctionQuery<TFunction, string, GeneralType> Create<TFunction>(
            string? identifier)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
            };

        public static FunctionQuery<TFunction, TIdentifier, TArgument> Create<TFunction, TIdentifier, TArgument>(
            TIdentifier? identifier,
            ImmutableArray<TArgument>? arguments = null,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments?.Length,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };

        public static FunctionQuery<TFunction, TIdentifier, TArgument> Create<TFunction, TIdentifier, TArgument>(
            TIdentifier? identifier,
            ImmutableArray<TArgument> arguments,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments.Length,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };

        public static FunctionQuery<TFunction, TIdentifier, TArgument> Create<TFunction, TIdentifier, TArgument>(
            TIdentifier? identifier,
            int argumentCount,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                ArgumentCount = argumentCount,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };
    }

    public readonly struct FunctionQuery<TFunction, TIdentifier, TArgument>
        where TFunction : ITemplateable<TFunction>
    {
        public TIdentifier? Identifier { get; init; }
        public Uri? RelevantFile { get; init; }
        public ImmutableArray<TArgument>? Arguments { get; init; }
        public int? ArgumentCount { get; init; }
        public GeneralType? ReturnType { get; init; }
        public Action<CompliableTemplate<TFunction>>? AddCompilable { get; init; }

        public string ToReadable()
        {
            string identifier = Identifier?.ToString() ?? "?";
            IEnumerable<string?>? arguments = Arguments?.Select(v => v?.ToString()) ?? (ArgumentCount.HasValue ? Enumerable.Repeat(default(string), ArgumentCount.Value) : null);
            return CompiledFunction.ToReadable(identifier, arguments);
        }

        public override string ToString() => ToReadable();
    }

    public class FunctionQueryResult<TFunction> where TFunction : notnull
    {
        public required TFunction Function { get; init; }
        public Dictionary<string, GeneralType>? TypeArguments { get; init; }
        public FunctionPerfectus Perfectus { get; init; }

        public void Deconstruct(
            out TFunction function)
        {
            function = Function;
        }

        public void Deconstruct(
            out TFunction function,
            out Dictionary<string, GeneralType>? typeArguments)
        {
            function = Function;
            typeArguments = TypeArguments;
        }

        public void Deconstruct(
            out TFunction function,
            out Dictionary<string, GeneralType>? typeArguments,
            out FunctionPerfectus perfectus)
        {
            function = Function;
            typeArguments = TypeArguments;
            perfectus = Perfectus;
        }

        public override string? ToString() => Function.ToString();
    }

    public enum FunctionPerfectus
    {
        None,

        /// <summary>
        /// Both function's identifier is the same
        /// </summary>
        Identifier,

        /// <summary>
        /// Both function has the same number of parameters
        /// </summary>
        ParameterCount,

        /// <summary>
        /// All the parameter types are almost the same
        /// </summary>
        ParameterTypes,

        /// <summary>
        /// Boundary between good and bad functions
        /// </summary>
        Good,

        // == MATCHED --> Searching for the most relevant function ==

        /// <summary>
        /// Return types are almost the same
        /// </summary>
        ReturnType,

        /// <summary>
        /// All the parameter types are the same
        /// </summary>
        PerfectParameterTypes,

        /// <summary>
        /// Return types are the same
        /// </summary>
        PerfectReturnType,

        /// <summary>
        /// Both function are in the same file
        /// </summary>
        File,
    }

    public readonly struct Functions<TFunction> where TFunction : ITemplateable<TFunction>
    {
        public IEnumerable<TFunction> Compiled { get; init; }
        public IEnumerable<CompliableTemplate<TFunction>> Compilable { get; init; }
    }

    Functions<CompiledFunction> GetFunctions() => new()
    {
        Compiled = CompiledFunctions,
        Compilable = compilableFunctions,
    };

    Functions<CompiledOperator> GetOperators() => new()
    {
        Compiled = CompiledOperators,
        Compilable = compilableOperators,
    };

    Functions<CompiledGeneralFunction> GetGeneralFunctions() => new()
    {
        Compiled = CompiledGeneralFunctions,
        Compilable = compilableGeneralFunctions,
    };

    Functions<CompiledConstructor> GetConstructors() => new()
    {
        Compiled = CompiledConstructors,
        Compilable = compilableConstructors,
    };

    protected bool GetConstructor(
        GeneralType type,
        ImmutableArray<GeneralType> arguments,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledConstructor>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledConstructor>>? addCompilable = null)
    {
        arguments = [type, .. arguments];

        return GetFunction<CompiledConstructor, GeneralType, GeneralType>(
            GetConstructors(),
            "constructor",
            CompiledConstructor.ToReadable(type, arguments),

            FunctionQuery.Create(type, arguments, relevantFile, null, addCompilable),

            out result,
            out error
        );
    }

    protected bool GetIndexGetter(
        GeneralType prevType,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledFunction>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = ImmutableArray.Create(prevType, new BuiltinType(BasicType.Integer));
        FunctionQuery<CompiledFunction, string, GeneralType> query = FunctionQuery.Create(BuiltinFunctionIdentifiers.IndexerGet, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledFunction, Token, string>(
            GetFunctions(),
            "function",
            null,

            query,

            out result,
            out error
        ) && result.Perfectus >= FunctionPerfectus.PerfectParameterTypes;
    }

    protected bool GetIndexSetter(
        GeneralType prevType,
        GeneralType elementType,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledFunction>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = ImmutableArray.Create(prevType, new BuiltinType(BasicType.Integer), elementType);
        FunctionQuery<CompiledFunction, string, GeneralType> query = FunctionQuery.Create(BuiltinFunctionIdentifiers.IndexerSet, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledFunction, Token, string>(
            GetFunctions(),
            "function",
            null,

            query,

            out result,
            out error
        ) && result.Perfectus >= FunctionPerfectus.PerfectParameterTypes;
    }

    protected bool TryGetBuiltinFunction(
        string builtinName,
        ImmutableArray<GeneralType> arguments,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledFunction>>? addCompilable = null)
    {
        IEnumerable<CompiledFunction> builtinCompiledFunctions =
            CompiledFunctions
            .Where(v => v.BuiltinFunctionName == builtinName);

        IEnumerable<CompliableTemplate<CompiledFunction>> builtinCompilableFunctions =
            compilableFunctions
            .Where(v => v.Function.BuiltinFunctionName == builtinName);

        string readable = $"[Builtin(\"{builtinName}\")] ?({string.Join(", ", arguments)})";
        FunctionQuery<CompiledFunction, string, GeneralType> query = FunctionQuery.Create(null as string, arguments, relevantFile, null, addCompilable);

        return GetFunction<CompiledFunction, Token, string>(
            new Functions<CompiledFunction>()
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

    protected bool GetOperator(
        BinaryOperatorCall @operator,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperator>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledOperator>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = FindStatementTypes(@operator.Parameters);
        FunctionQuery<CompiledOperator, string, GeneralType> query = FunctionQuery.Create(@operator.Operator.Content, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledOperator, Token, string>(
            GetOperators(),
            "operator",
            null,

            query,

            out result,
            out error
        ) && result.Perfectus >= FunctionPerfectus.PerfectParameterTypes;
    }

    protected bool GetOperator(
        UnaryOperatorCall @operator,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledOperator>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledOperator>>? addCompilable = null)
    {
        ImmutableArray<GeneralType> arguments = FindStatementTypes(@operator.Parameters);
        FunctionQuery<CompiledOperator, string, GeneralType> query = FunctionQuery.Create(@operator.Operator.Content, arguments, relevantFile, null, addCompilable);
        return GetFunction<CompiledOperator, Token, string>(
            GetOperators(),
            "operator",
            null,

            query,

            out result,
            out error
        ) && result.Perfectus >= FunctionPerfectus.PerfectParameterTypes;
    }

    protected bool GetGeneralFunction(
        GeneralType context,
        ImmutableArray<GeneralType> arguments,
        string identifier,
        Uri? relevantFile,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledGeneralFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledGeneralFunction>>? addCompilable = null)
    {
        IEnumerable<CompiledGeneralFunction> compiledGeneralFunctionsInContext =
            CompiledGeneralFunctions
            .Where(v => ContextIs(v, context));

        IEnumerable<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctionsInContext =
            compilableGeneralFunctions
            .Where(v => ContextIs(v.Function, context));

        return GetFunction<CompiledGeneralFunction, Token, string>(
            new Functions<CompiledGeneralFunction>()
            {
                Compiled = compiledGeneralFunctionsInContext,
                Compilable = compilableGeneralFunctionsInContext,
            },
            "general function",
            null,

            FunctionQuery.Create(identifier, arguments, relevantFile, null, addCompilable),

            out result,
            out error
        );
    }

    protected bool GetFunction(
        string identifier,
        GeneralType? type,
        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        if (type is null || type is not FunctionType functionType)
        {
            return GetFunction(
                FunctionQuery.Create<CompiledFunction, string, GeneralType>(identifier),
                out result,
                out error
            );
        }

        return GetFunction(
            FunctionQuery.Create<CompiledFunction, string, GeneralType>(identifier, functionType.Parameters, null, functionType.ReturnType, null),
            out result,
            out error
        );
    }

    protected bool GetFunction(
        AnyCall call,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledFunction>>? addCompilable = null)
    {
        result = null;
        error = null;

        if (!call.ToFunctionCall(out FunctionCall? functionCall))
        {
            error ??= new WillBeCompilerException($"Function {call.ToReadable(FindStatementType)} not found");
            return false;
        }

        return GetFunction(
            functionCall,

            out result,
            out error,
            addCompilable
        );
    }

    protected bool GetFunction(
        FunctionCall functionCallStatement,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error,
        Action<CompliableTemplate<CompiledFunction>>? addCompilable = null)
    {
        string identifier = functionCallStatement.Identifier.Content;
        ImmutableArray<GeneralType> argumentTypes = FindStatementTypes(functionCallStatement.MethodParameters);
        FunctionQuery<CompiledFunction, string, GeneralType> query = FunctionQuery.Create(identifier, argumentTypes, functionCallStatement.OriginalFile, null, addCompilable);
        return GetFunction(
            query,
            out result,
            out error
        );
    }

    protected bool GetFunction(
        FunctionQuery<CompiledFunction, string, GeneralType> query,

        [NotNullWhen(true)] out FunctionQueryResult<CompiledFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction<CompiledFunction, Token, string>(
            GetFunctions(),
            "function",
            null,

            query,
            out result,
            out error
        );

    public static bool GetFunction<TFunction, TDefinedIdentifier, TPassedIdentifier>(
        Functions<TFunction> functions,
        string kindName,
        string? readableName,

        FunctionQuery<TFunction, TPassedIdentifier, GeneralType> query,

        [NotNullWhen(true)] out FunctionQueryResult<TFunction>? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        where TFunction : ICompiledFunction, IInFile, ITemplateable<TFunction>, ISimpleReadable, IIdentifiable<TDefinedIdentifier>
        where TDefinedIdentifier : notnull, IEquatable<TPassedIdentifier>
    {
        TFunction? result_ = default;
        Dictionary<string, GeneralType>? typeArguments = null;
        WillBeCompilerException? error_ = null;
        readableName ??= query.ToReadable();

        string kindNameLower = kindName.ToLowerInvariant();
        string kindNameCapital = char.ToUpperInvariant(kindName[0]) + kindName[1..];

        FunctionPerfectus perfectus = FunctionPerfectus.None;

        static FunctionPerfectus Max(FunctionPerfectus a, FunctionPerfectus b) => a > b ? a : b;

        static bool CheckTypeConversion(GeneralType from, GeneralType to)
        {
            if (from.Equals(to))
            { return true; }

            if (to is PointerType && from == BasicType.Integer)
            { return true; }

            return false;
        }

        bool HandleIdentifier(TFunction function)
        {
            if (query.Identifier is not null &&
                !function.Identifier.Equals(query.Identifier))
            { return false; }

            perfectus = Max(perfectus, FunctionPerfectus.Identifier);
            return true;
        }

        bool HandleParameterCount(TFunction function)
        {
            if (query.ArgumentCount.HasValue &&
                function.ParameterTypes.Count != query.ArgumentCount.Value)
            {
                if (perfectus < FunctionPerfectus.ParameterCount)
                {
                    result_ = function;
                    error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: wrong number of parameters passed to {function.ToReadable()}");
                }
                return false;
            }

            perfectus = Max(perfectus, FunctionPerfectus.ParameterCount);
            return true;
        }

        bool HandleParameterTypes(TFunction function)
        {
            if (query.Arguments.HasValue &&
                !Utils.SequenceEquals(function.ParameterTypes, query.Arguments.Value, (defined, passed) =>
                {
                    if (passed.Equals(defined))
                    { return true; }

                    if (CheckTypeConversion(passed, defined))
                    { return true; }

                    return false;
                }))
            {
                if (perfectus < FunctionPerfectus.ParameterTypes)
                {
                    result_ = function;
                    error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: parameter types doesn't match with {function.ToReadable()}");
                }
                return false;
            }

            perfectus = Max(perfectus, FunctionPerfectus.ParameterTypes);
            return true;
        }

        bool HandlePerfectParameterTypes(TFunction function)
        {
            if (query.Arguments.HasValue &&
                !Utils.SequenceEquals(function.ParameterTypes, query.Arguments.Value))
            {
                if (perfectus < FunctionPerfectus.PerfectReturnType)
                {
                    result_ = function;
                    error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: return types doesn't match with {function.ToReadable()}");
                }
                return false;
            }

            if (perfectus < FunctionPerfectus.PerfectReturnType)
            {
                perfectus = Max(perfectus, FunctionPerfectus.PerfectReturnType);
                result_ = function;
            }
            return true;
        }

        bool HandleReturnType(TFunction function)
        {
            if (query.ReturnType != null && (
                query.ReturnType.Equals(function.Type) ||
                CheckTypeConversion(function.Type, query.ReturnType)
                ))
            {
                if (perfectus < FunctionPerfectus.ReturnType)
                {
                    result_ = function;
                    error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: return types doesn't match with {function.ToReadable()}");
                }
                return false;
            }

            perfectus = Max(perfectus, FunctionPerfectus.ReturnType);
            return true;
        }

        bool HandlePerfectReturnType(TFunction function)
        {
            if (query.ReturnType != null &&
                !function.Type.Equals(query.ReturnType))
            {
                if (perfectus < FunctionPerfectus.PerfectParameterTypes)
                {
                    result_ = function;
                    error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: parameter types doesn't match with {function.ToReadable()}");
                }
                return false;
            }

            if (perfectus < FunctionPerfectus.PerfectParameterTypes)
            {
                perfectus = Max(perfectus, FunctionPerfectus.PerfectParameterTypes);
                result_ = function;
            }
            return true;
        }

        bool HandleFile(TFunction function)
        {
            if (query.RelevantFile is null ||
                function.File != query.RelevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= FunctionPerfectus.File)
            {
                error_ = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: multiple {kindNameLower}s matched in the same file");
                // Debugger.Break();
            }

            perfectus = FunctionPerfectus.File;
            result_ = function;
            return true;
        }

        foreach (TFunction function in functions.Compiled.Where(f => !f.IsTemplate).Concat(functions.Compilable.Select(f => f.Function)))
        {
            if (!HandleIdentifier(function))
            { continue; }

            if (!HandleParameterCount(function))
            { continue; }

            if (!HandleParameterTypes(function))
            { continue; }

            if (!HandleReturnType(function))
            { continue; }

            // MATCHED --> Searching for most relevant function

            if (perfectus < FunctionPerfectus.Good)
            {
                result_ = function;
                perfectus = FunctionPerfectus.Good;
            }

            if (!HandlePerfectParameterTypes(function))
            { continue; }

            if (!HandlePerfectReturnType(function))
            { continue; }

            if (!HandleFile(function))
            { continue; }
        }

        if (perfectus >= FunctionPerfectus.Good)
        { goto Finish; }

        foreach (TFunction function in functions.Compiled.Where(f => f.IsTemplate))
        {
            if (!HandleIdentifier(function))
            { continue; }

            if (!HandleParameterCount(function))
            { continue; }

            if (!query.Arguments.HasValue)
            { continue; }

            Dictionary<string, GeneralType> _typeArguments = new();

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, query.Arguments.Value, _typeArguments))
            { continue; }

            perfectus = FunctionPerfectus.PerfectParameterTypes;
            typeArguments = _typeArguments;

            // MATCHED --> Searching for most relevant function

            if (perfectus < FunctionPerfectus.File)
            { result_ = function; }

            if (!HandleFile(function))
            { continue; }
        }

    Finish:

        if (result_ is not null && perfectus >= FunctionPerfectus.Good)
        {
            if (typeArguments is not null)
            {
                CompliableTemplate<TFunction> template = new(result_, typeArguments);
                query.AddCompilable?.Invoke(template);
                result_ = template.Function;
            }

            result = new FunctionQueryResult<TFunction>()
            {
                Function = result_,
                TypeArguments = typeArguments,
                Perfectus = perfectus,
            };
            error = error_;
            return true;
        }

        error = error_ ?? new WillBeCompilerException($"{kindNameCapital} {readableName} not found");
        result = null;
        return false;
    }

    #endregion

    static bool ContextIs(CompiledGeneralFunction function, GeneralType type) =>
        type is StructType structType &&
        function.Context is not null &&
        function.Context == structType.Struct;

    #region CompileConstant()

    protected void CompileGlobalConstants(IEnumerable<Statement> statements)
    {
        foreach (Statement statement in statements)
        {
            if (statement is not VariableDeclaration variableDeclaration ||
                !variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
            { continue; }

            CompiledGlobalConstants = CompiledGlobalConstants.Add(CompileConstant(variableDeclaration));
        }
    }

    protected int CompileLocalConstants(IEnumerable<Statement> statements)
    {
        int count = 0;
        foreach (Statement statement in statements)
        {
            if (statement is not VariableDeclaration variableDeclaration ||
                !variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
            { continue; }
            CompiledLocalConstants.Push(CompileConstant(variableDeclaration));
            count++;
        }
        LocalConstantsStack.Push(count);
        return count;
    }

    protected int CompileLocalConstants(IEnumerable<VariableDeclaration> statements)
    {
        int count = 0;
        foreach (VariableDeclaration statement in statements)
        {
            if (!statement.Modifiers.Contains(ModifierKeywords.Const))
            { continue; }
            CompiledLocalConstants.Push(CompileConstant(statement));
            count++;
        }
        LocalConstantsStack.Push(count);
        return count;
    }

    protected CompiledVariableConstant CompileConstant(VariableDeclaration variableDeclaration)
    {
        variableDeclaration.Identifier.AnalyzedType = TokenAnalyzedType.ConstantName;

        if (variableDeclaration.InitialValue == null)
        { throw new CompilerException($"Constant value must have initial value", variableDeclaration, variableDeclaration.File); }

        if (!TryCompute(variableDeclaration.InitialValue, out DataItem constantValue))
        { throw new CompilerException($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue, variableDeclaration.File); }

        if (variableDeclaration.Type != StatementKeywords.Var)
        {
            GeneralType constantType = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);
            variableDeclaration.Type.SetAnalyzedType(constantType);

            if (constantType is not BuiltinType builtinType)
            { throw new NotSupportedException($"Only builtin types supported as a constant value", variableDeclaration.Type, variableDeclaration.File); }

            DataItem.TryCast(ref constantValue, builtinType.RuntimeType);
        }

        if (GetConstant(variableDeclaration.Identifier.Content, out _))
        { throw new CompilerException($"Constant \"{variableDeclaration.Identifier}\" already defined", variableDeclaration.Identifier, variableDeclaration.File); }

        return new(constantValue, variableDeclaration);
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

    protected bool GetStruct(
        string structName,
        Uri? relevantFile,

        [NotNullWhen(true)] out CompiledStruct? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => CodeGenerator.GetStruct(
            CompiledStructs,

            structName,
            relevantFile,

            out result,
            out error);

    public static bool GetStruct(
        IEnumerable<CompiledStruct> structs,

        string structName,
        Uri? relevantFile,

        [NotNullWhen(true)] out CompiledStruct? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        CompiledStruct? result_ = default;
        WillBeCompilerException? error_ = null;

        StructPerfectus perfectus = StructPerfectus.None;

        static StructPerfectus Max(StructPerfectus a, StructPerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledStruct function)
        {
            if (structName is not null &&
                !function.Identifier.Equals(structName))
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
                error_ = new WillBeCompilerException($"Struct {structName} not found: multiple structs matched in the same file");
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

        error = error_ ?? new WillBeCompilerException($"Struct {structName} not found");
        result = null;
        return false;
    }

    #endregion

    protected bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out GeneralType? result)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            return true;
        }

        if (GetStruct(name.Content, relevantFile, out CompiledStruct? @struct, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.Struct;
            @struct.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name, null), CurrentFile));

            result = new StructType(@struct, relevantFile);
            return true;
        }

        if (TypeArguments.TryGetValue(name.Content, out GeneralType? typeArgument))
        {
            result = typeArgument;
            return true;
        }

        if (GetFunction(FunctionQuery.Create<CompiledFunction, string, GeneralType>(name.Content, null, relevantFile), out FunctionQueryResult<CompiledFunction>? function, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.FunctionName;
            function.Function.References.Add(new Reference<StatementWithValue>(new Identifier(name, null), CurrentFile));
            result = new FunctionType(function.Function);
            return true;
        }

        if (GetGlobalVariable(name.Content, relevantFile, out CompiledVariable? globalVariable, out _))
        {
            result = globalVariable.Type;
            return true;
        }

        result = null;
        return false;
    }

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

    protected bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledVariable? compiledVariable)
    {
        foreach (CompiledVariable compiledVariable_ in CompiledVariables)
        {
            if (compiledVariable_.Identifier.Content == variableName)
            {
                compiledVariable = compiledVariable_;
                return true;
            }
        }
        compiledVariable = null;
        return false;
    }

    protected bool GetGlobalVariable(
        string variableName,
        Uri relevantFile,

        [NotNullWhen(true)] out CompiledVariable? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        CompiledVariable? result_ = default;
        WillBeCompilerException? error_ = null;

        GlobalVariablePerfectus perfectus = GlobalVariablePerfectus.None;

        static GlobalVariablePerfectus Max(GlobalVariablePerfectus a, GlobalVariablePerfectus b) => a > b ? a : b;

        bool HandleIdentifier(CompiledVariable variable)
        {
            if (variableName is not null &&
                !variable.Identifier.Equals(variableName))
            { return false; }

            perfectus = Max(perfectus, GlobalVariablePerfectus.Identifier);
            return true;
        }

        bool HandleFile(CompiledVariable variable)
        {
            if (relevantFile is null ||
                variable.File != relevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= GlobalVariablePerfectus.File)
            {
                error_ = new WillBeCompilerException($"Global variable {variableName} not found: multiple variables matched in the same file");
                // Debugger.Break();
            }

            perfectus = GlobalVariablePerfectus.File;
            result_ = variable;
            return true;
        }

        foreach (CompiledVariable variable in CompiledGlobalVariables)
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

        error = error_ ?? new WillBeCompilerException($"Global variable {variableName} not found");
        result = null;
        return false;
    }

    protected bool GetParameter(string parameterName, [NotNullWhen(true)] out CompiledParameter? parameter)
    {
        foreach (CompiledParameter compiledParameter_ in CompiledParameters)
        {
            if (compiledParameter_.Identifier.Content == parameterName)
            {
                parameter = compiledParameter_;
                return true;
            }
        }
        parameter = null;
        return false;
    }

    protected void AssignTypeCheck(GeneralType destination, GeneralType valueType, StatementWithValue value)
    {
        if (destination == valueType)
        { return; }

        if (destination.Size != valueType.Size)
        { throw new CompilerException($"Can not set \"{valueType}\" (size of {valueType.Size}) value to {destination} (size of {destination.Size})", value, CurrentFile); }

        if (destination is PointerType &&
            valueType == BasicType.Integer)
        { return; }

        if (destination is BuiltinType destBuiltinType &&
            destBuiltinType.Type == BasicType.Byte &&
            TryCompute(value, out DataItem yeah) &&
            yeah.Type == RuntimeType.Integer)
        { return; }

        if (value is LiteralStatement literal &&
            literal.Type == LiteralType.String)
        {
            if (destination is ArrayType destArrayType &&
                destArrayType.Of == BasicType.Char)
            {
                string literalValue = literal.Value;
                if (literalValue.Length != destArrayType.Length)
                { throw new CompilerException($"Can not set \"{literalValue}\" (length of {literalValue.Length}) value to stack array {destination} (length of {destArrayType.Length})", value, CurrentFile); }
                return;
            }

            if (destination is PointerType pointerType &&
                pointerType.To == BasicType.Char)
            { return; }
        }

        throw new CompilerException($"Can not set a {valueType} type value to the {destination} type", value, CurrentFile);
    }

    protected void AssignTypeCheck(GeneralType destination, DataItem value, IPositioned valuePosition)
    {
        BuiltinType valueType = new(value.Type);

        if (destination == valueType)
        { return; }

        if (destination.Size != valueType.Size)
        { throw new CompilerException($"Can not set \"{valueType}\" (size of {valueType.Size}) value to {destination} (size of {destination.Size})", valuePosition, CurrentFile); }

        if (destination is PointerType)
        { return; }

        if (destination == BasicType.Byte &&
            value.Type == RuntimeType.Integer)
        { return; }

        if (destination is BuiltinType builtinDestination &&
            valueType is BuiltinType builtinValueType)
        {
            BitWidth destinationBitWidth = GetBitWidth(builtinDestination);
            BitWidth valueBitWidth = GetBitWidth(builtinValueType);
            if (destinationBitWidth >= valueBitWidth)
            { return; }
        }

        throw new CompilerException($"Can not set a {valueType} type value to the {destination} type", valuePosition, CurrentFile);
    }

    protected static BitWidth MaxBitWidth(BitWidth a, BitWidth b) => a > b ? a : b;

    protected static BitWidth GetBitWidth(GeneralType type) => type switch
    {
        BuiltinType v => GetBitWidth(v),
        PointerType v => GetBitWidth(v),
        _ => throw new InternalException($"Invalid type {type}"),
    };

    protected static BitWidth GetBitWidth(BuiltinType type) => type.Type switch
    {
        BasicType.Byte => BitWidth._8,
        BasicType.Integer => BitWidth._32,
        BasicType.Float => BitWidth._32,
        BasicType.Char => BitWidth._16,
        _ => throw new InternalException($"Invalid type {type}"),
    };

    protected static BitWidth GetBitWidth(PointerType _) => BitWidth._32;

    #region Addressing Helpers

    protected ValueAddress GetDataAddress(StatementWithValue value) => value switch
    {
        IndexCall v => GetDataAddress(v),
        Identifier v => GetDataAddress(v),
        Field v => GetDataAddress(v),
        _ => throw new NotImplementedException()
    };
    protected ValueAddress GetDataAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetBaseAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return new ValueAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.OriginalFile, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
    }
    protected ValueAddress GetDataAddress(Field field)
    {
        ValueAddress address = GetBaseAddress(field);
        if (address.IsReference)
        { throw new NotImplementedException(); }
        int offset = GetDataOffset(field);
        return new ValueAddress(address.Address + offset, address.AddressingMode, address.IsReference, address.InHeap);
    }
    protected ValueAddress GetDataAddress(IndexCall indexCall)
    {
        ValueAddress address = GetBaseAddress(indexCall.PrevStatement);
        if (address.IsReference)
        { throw new NotImplementedException(); }
        int currentOffset = GetDataOffset(indexCall);
        return new ValueAddress(address.Address + currentOffset, address.AddressingMode, address.IsReference, address.InHeap);
    }

    protected int GetDataOffset(StatementWithValue value) => value switch
    {
        IndexCall v => GetDataOffset(v),
        Field v => GetDataOffset(v),
        Identifier => 0,
        _ => throw new NotImplementedException()
    };
    protected int GetDataOffset(Field field)
    {
        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is not StructType structType)
        { throw new NotImplementedException(); }

        if (!structType.GetField(field.Identifier.Content, out _, out int fieldOffset))
        { throw new CompilerException($"Field \"{field.Identifier}\" not found in struct \"{structType.Struct.Identifier}\"", field.Identifier, CurrentFile); }

        int prevOffset = GetDataOffset(field.PrevStatement);
        return prevOffset + fieldOffset;
    }
    protected int GetDataOffset(IndexCall indexCall)
    {
        GeneralType prevType = FindStatementType(indexCall.PrevStatement);

        if (prevType is not ArrayType arrayType)
        { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

        if (!TryCompute(indexCall.Index, out DataItem index))
        { throw new CompilerException($"Can't compute the index value", indexCall.Index, CurrentFile); }

        int prevOffset = GetDataOffset(indexCall.PrevStatement);
        int offset = (int)index * arrayType.Of.Size;
        return prevOffset + offset;
    }

    protected ValueAddress GetBaseAddress(StatementWithValue statement) => statement switch
    {
        Identifier v => GetBaseAddress(v),
        Field v => GetBaseAddress(v),
        IndexCall v => GetBaseAddress(v),
        _ => throw new NotImplementedException()
    };
    protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter);
    protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter, int offset);
    protected abstract ValueAddress GetGlobalVariableAddress(CompiledVariable variable);
    protected ValueAddress GetBaseAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetBaseAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return new ValueAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, variable.OriginalFile, out CompiledVariable? globalVariable, out _))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
    }
    protected ValueAddress GetBaseAddress(Field statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement) is PointerType;
        return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
    }
    protected ValueAddress GetBaseAddress(IndexCall statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement) is PointerType;
        return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
    }

    protected bool IsItInHeap(StatementWithValue value) => value switch
    {
        Identifier => false,
        Field field => IsItInHeap(field),
        IndexCall indexCall => IsItInHeap(indexCall),
        _ => throw new NotImplementedException()
    };

    protected bool IsItInHeap(IndexCall indexCall)
        => IsItInHeap(indexCall.PrevStatement) || FindStatementType(indexCall.PrevStatement) is PointerType;

    protected bool IsItInHeap(Field field)
        => IsItInHeap(field.PrevStatement) || FindStatementType(field.PrevStatement) is PointerType;

    #endregion

    protected CompiledVariable CompileVariable(VariableDeclaration newVariable, int memoryOffset)
    {
        if (LanguageConstants.KeywordList.Contains(newVariable.Identifier.Content))
        { throw new CompilerException($"Illegal variable name \"{newVariable.Identifier.Content}\"", newVariable.Identifier, CurrentFile); }

        GeneralType type;
        if (newVariable.Type == StatementKeywords.Var)
        {
            if (newVariable.InitialValue == null)
            { throw new CompilerException($"Initial value for variable declaration with implicit type is required", newVariable, newVariable.File); }

            type = FindStatementType(newVariable.InitialValue);
        }
        else
        {
            type = GeneralType.From(newVariable.Type, FindType, TryCompute);

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        if (!type.AllGenericsDefined())
        { throw new InternalException($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\"", newVariable.Type, newVariable.File); }

        return new CompiledVariable(memoryOffset, type, newVariable);
    }

    #region GetInitialValue()

    protected static DataItem GetInitialValue(BasicType type) => type switch
    {
        BasicType.Byte => new DataItem((byte)0),
        BasicType.Integer => new DataItem((int)0),
        BasicType.Float => new DataItem((float)0f),
        BasicType.Char => new DataItem((char)'\0'),

        _ => throw new NotImplementedException($"Initial value for type \"{type}\" isn't implemented"),
    };

    protected static bool GetInitialValue(GeneralType type, out DataItem value)
    {
        switch (type)
        {
            case GenericType:
            case StructType:
            case FunctionType:
                value = default;
                return false;
            case BuiltinType builtinType:
                value = GetInitialValue(builtinType.Type);
                return true;
            case PointerType:
                value = new DataItem(0);
                return true;
            default: throw new NotImplementedException();
        }
    }

    protected static DataItem GetInitialValue(GeneralType type) => type switch
    {
        GenericType => throw new NotImplementedException($"Initial value for type arguments is bruh moment"),
        StructType => throw new NotImplementedException($"Initial value for structs is not implemented"),
        FunctionType => new DataItem(int.MaxValue),
        BuiltinType builtinType => GetInitialValue(builtinType.Type),
        PointerType => new DataItem(0),
        _ => throw new NotImplementedException(),
    };

    #endregion

    #region FindStatementType()

    protected virtual GeneralType OnGotStatementType(StatementWithValue statement, GeneralType type)
    {
        statement.CompiledType = type;
        return type;
    }

    protected GeneralType FindStatementType(AnyCall anyCall)
    {
        if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
        {
            return OnGotStatementType(anyCall, FindStatementType(functionCall));
        }

        GeneralType prevType = FindStatementType(anyCall.PrevStatement);

        if (prevType is not FunctionType functionType)
        { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

        return OnGotStatementType(anyCall, functionType.ReturnType);
    }
    protected GeneralType FindStatementType(KeywordCall keywordCall)
    {
        return keywordCall.Identifier.Content switch
        {
            StatementKeywords.Return => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Void)),
            StatementKeywords.Throw => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Void)),
            StatementKeywords.Break => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Void)),
            StatementKeywords.Delete => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Void)),
            _ => throw new CompilerException($"Unknown keyword-function \"{keywordCall.Identifier}\"", keywordCall.Identifier, CurrentFile)
        };
    }
    protected GeneralType FindStatementType(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);

        if (prevType is ArrayType arrayType)
        { return OnGotStatementType(index, arrayType.Of); }

        // TODO: (index.PrevStatement as IReferenceableTo)?.OriginalFile can be null
        if (!GetIndexGetter(prevType, (index.PrevStatement as IReferenceableTo)?.OriginalFile, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFoundException))
        { }

        CompiledFunction? indexer = result?.Function;

        if (notFoundException != null) indexer = null;

        if (indexer == null && prevType is PointerType pointerType)
        {
            return pointerType.To;
        }

        if (indexer == null)
        { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }

        return OnGotStatementType(index, indexer.Type);
    }
    protected GeneralType FindStatementType(FunctionCall functionCall)
    {
        if (functionCall.Identifier.Content == "sizeof") return new BuiltinType(BasicType.Integer);

        if (!GetFunction(functionCall, out FunctionQueryResult<CompiledFunction>? result, out WillBeCompilerException? notFoundError))
        { throw notFoundError.Instantiate(functionCall, CurrentFile); }

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
        return OnGotStatementType(functionCall, result.Function.Type);
    }

    protected GeneralType FindStatementType(BinaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (GetOperator(@operator, @operator.OriginalFile, out FunctionQueryResult<CompiledOperator>? _result, out _))
        {
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, _result.Function.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left);
        GeneralType rightType = FindStatementType(@operator.Right);

        CompilerException unknownOperator = new($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile);

        if (!leftType.CanBeBuiltin ||
            !rightType.CanBeBuiltin ||
            leftType == BasicType.Void ||
            rightType == BasicType.Void)
        { throw unknownOperator; }

        {
            if (leftType is BuiltinType leftBType &&
                rightType is BuiltinType rightBType)
            {
                bool isFloat =
                    leftBType.Type == BasicType.Float ||
                    rightBType.Type == BasicType.Float;

                BitWidth leftBitWidth = GetBitWidth(leftType);
                BitWidth rightBitWidth = GetBitWidth(rightType);
                BitWidth bitWidth = MaxBitWidth(leftBitWidth, rightBitWidth);

                return @operator.Operator.Content switch
                {
                    BinaryOperatorCall.CompLT or
                    BinaryOperatorCall.CompGT or
                    BinaryOperatorCall.CompLEQ or
                    BinaryOperatorCall.CompGEQ or
                    BinaryOperatorCall.CompEQ or
                    BinaryOperatorCall.CompNEQ
                        => new BuiltinType(isFloat ? BasicType.Float : BasicType.Integer),

                    BinaryOperatorCall.LogicalOR or
                    BinaryOperatorCall.LogicalAND or
                    BinaryOperatorCall.BitwiseAND or
                    BinaryOperatorCall.BitwiseOR or
                    BinaryOperatorCall.BitwiseXOR or
                    BinaryOperatorCall.BitshiftLeft or
                    BinaryOperatorCall.BitshiftRight
                        => new BuiltinType(bitWidth.ToType()),

                    BinaryOperatorCall.Addition or
                    BinaryOperatorCall.Subtraction or
                    BinaryOperatorCall.Multiplication or
                    BinaryOperatorCall.Division or
                    BinaryOperatorCall.Modulo
                    => new BuiltinType(isFloat ? BasicType.Float : bitWidth.ToType()),

                    _ => throw unknownOperator,
                };
            }
        }

        // switch (leftType)
        // {
        //     case BuiltinType: break;
        //     case PointerType:
        //         return rightType switch
        //         {
        //             // BuiltinType => leftType,
        //             // PointerType => leftType,
        //             _ => throw unknownOperator,
        //         };
        //     default: throw unknownOperator;
        // }
        // 
        // switch (rightType)
        // {
        //     case BuiltinType: break;
        //     case PointerType:
        //         return leftType switch
        //         {
        //             // BuiltinType => rightType,
        //             // PointerType => leftType,
        //             _ => throw unknownOperator,
        //         };
        //     default: throw unknownOperator;
        // }

        // if (leftType is not BuiltinType || rightType is not BuiltinType)
        // { throw unknownOperator; }

        DataItem leftValue = GetInitialValue(leftType);
        DataItem rightValue = GetInitialValue(rightType);

        DataItem predictedValue = @operator.Operator.Content switch
        {
            "+" => leftValue + rightValue,
            "-" => leftValue - rightValue,
            "*" => leftValue * rightValue,
            "/" => leftValue,
            "%" => leftValue,

            "&&" => new DataItem((bool)leftValue && (bool)rightValue),
            "||" => new DataItem((bool)leftValue || (bool)rightValue),

            "&" => leftValue & rightValue,
            "|" => leftValue | rightValue,
            "^" => leftValue ^ rightValue,

            "<<" => leftValue << rightValue,
            ">>" => leftValue >> rightValue,

            "<" => new DataItem(leftValue < rightValue),
            ">" => new DataItem(leftValue > rightValue),
            "==" => new DataItem(leftValue == rightValue),
            "!=" => new DataItem(leftValue != rightValue),
            "<=" => new DataItem(leftValue <= rightValue),
            ">=" => new DataItem(leftValue >= rightValue),

            _ => throw new NotImplementedException($"Unknown operator \"{@operator}\""),
        };

        BuiltinType result = new(predictedValue.Type);

        if (expectedType is not null)
        {
            if (result == BasicType.Integer &&
                expectedType is PointerType)
            { return OnGotStatementType(@operator, expectedType); }
        }

        return OnGotStatementType(@operator, result);
    }
    protected GeneralType FindStatementType(UnaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (GetOperator(@operator, @operator.OriginalFile, out FunctionQueryResult<CompiledOperator>? result_, out _))
        {
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, result_.Function.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left);

        if (!leftType.CanBeBuiltin || leftType == BasicType.Void)
        { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content}", @operator.Operator, CurrentFile); }

        DataItem leftValue = GetInitialValue(leftType);

        DataItem predictedValue = @operator.Operator.Content switch
        {
            UnaryOperatorCall.LogicalNOT => !leftValue,

            _ => throw new CompilerException($"Unknown operator {@operator.Operator.Content}", @operator.Operator, CurrentFile),
        };

        BuiltinType result = new(predictedValue.Type);

        if (expectedType is not null)
        {
            if (result == BasicType.Integer &&
                expectedType is PointerType)
            { return OnGotStatementType(@operator, expectedType); }
        }

        return OnGotStatementType(@operator, result);
    }
    protected GeneralType FindStatementType(LiteralStatement literal, GeneralType? expectedType)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
                if (expectedType == BasicType.Byte &&
                    int.TryParse(literal.Value, out int value) &&
                    value >= byte.MinValue && value <= byte.MaxValue)
                { return OnGotStatementType(literal, new BuiltinType(BasicType.Byte)); }
                return OnGotStatementType(literal, new BuiltinType(BasicType.Integer));
            case LiteralType.Float:
                return OnGotStatementType(literal, new BuiltinType(BasicType.Float));
            case LiteralType.String:
                return OnGotStatementType(literal, new PointerType(new BuiltinType(BasicType.Char)));
            case LiteralType.Char:
                return OnGotStatementType(literal, new BuiltinType(BasicType.Char));
            default:
                throw new UnreachableException($"Unknown literal type {literal.Type}");
        }
    }
    protected GeneralType FindStatementType(Identifier identifier, GeneralType? expectedType = null)
    {
        if (GetConstant(identifier.Content, out IConstant? constant))
        {
            identifier.Reference = constant;
            identifier.Token.AnalyzedType = TokenAnalyzedType.ConstantName;
            return OnGotStatementType(identifier, constant.Type);
        }

        if (GetLocalSymbolType(identifier.Content, out GeneralType? type))
        {
            if (GetParameter(identifier.Content, out CompiledParameter? parameter))
            {
                if (identifier.Content != StatementKeywords.This)
                { identifier.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
                identifier.Reference = parameter;
            }
            else if (GetVariable(identifier.Content, out CompiledVariable? variable))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.VariableName;
                identifier.Reference = variable;
            }
            else if (GetGlobalVariable(identifier.Content, identifier.OriginalFile, out CompiledVariable? globalVariable, out _))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.VariableName;
                identifier.Reference = globalVariable;
            }

            return OnGotStatementType(identifier, type);
        }

        if (GetFunction(identifier.Token.Content, expectedType, out FunctionQueryResult<CompiledFunction>? function, out _))
        {
            identifier.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(identifier, new FunctionType(function.Function));
        }

        for (int i = CurrentEvaluationContext.Count - 1; i >= 0; i--)
        {
            if (CurrentEvaluationContext[i].TryGetType(identifier, out GeneralType? _type))
            { return _type; }
        }

        if (FindType(identifier.Token, identifier.OriginalFile, out GeneralType? result))
        { return OnGotStatementType(identifier, result); }

        throw new CompilerException($"Symbol \"{identifier.Content}\" not found", identifier, CurrentFile);
    }
    protected GeneralType FindStatementType(AddressGetter addressGetter)
    {
        GeneralType to = FindStatementType(addressGetter.PrevStatement);
        return OnGotStatementType(addressGetter, new PointerType(to));
    }
    protected GeneralType FindStatementType(Pointer pointer)
    {
        GeneralType to = FindStatementType(pointer.PrevStatement);
        if (to is not PointerType pointerType)
        { return OnGotStatementType(pointer, new BuiltinType(BasicType.Integer)); }
        return OnGotStatementType(pointer, pointerType.To);
    }
    protected GeneralType FindStatementType(NewInstance newInstance)
    {
        GeneralType type = GeneralType.From(newInstance.Type, FindType);
        newInstance.Type.SetAnalyzedType(type);
        return OnGotStatementType(newInstance, type);
    }
    protected GeneralType FindStatementType(ConstructorCall constructorCall)
    {
        GeneralType type = GeneralType.From(constructorCall.Type, FindType);
        ImmutableArray<GeneralType> parameters = FindStatementTypes(constructorCall.Parameters);

        // TODO: constructorCall.OriginalFile can be null
        if (GetConstructor(type, parameters, constructorCall.OriginalFile, out FunctionQueryResult<CompiledConstructor>? result, out WillBeCompilerException? notFound))
        {
            constructorCall.Type.SetAnalyzedType(result.Function.Type);
            return OnGotStatementType(constructorCall, result.Function.Type);
        }

        throw notFound.Instantiate(constructorCall.Keyword, CurrentFile);
    }
    protected GeneralType FindStatementType(Field field)
    {
        GeneralType prevStatementType = FindStatementType(field.PrevStatement);

        if (prevStatementType is ArrayType && field.Identifier.Equals("Length"))
        {
            field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;
            return OnGotStatementType(field, new BuiltinType(BasicType.Integer));
        }

        if (prevStatementType is PointerType pointerType)
        { prevStatementType = pointerType.To; }

        if (prevStatementType is StructType structType)
        {
            foreach (CompiledField definedField in structType.Struct.Fields)
            {
                if (definedField.Identifier.Content != field.Identifier.Content) continue;
                field.Identifier.AnalyzedType = TokenAnalyzedType.FieldName;

                if (structType.Struct.Template is null)
                { return definedField.Type; }

                return GeneralType.InsertTypeParameters(definedField.Type, structType.TypeArguments) ?? definedField.Type;
            }

            throw new CompilerException($"Field definition \"{field.Identifier}\" not found in type \"{prevStatementType}\"", field.Identifier, CurrentFile);
        }

        throw new CompilerException($"Type \"{prevStatementType}\" does not have a field \"{field.Identifier}\"", field, CurrentFile);
    }
    protected GeneralType FindStatementType(TypeCast @as)
    {
        GeneralType type = GeneralType.From(@as.Type, FindType);
        @as.Type.SetAnalyzedType(type);
        return OnGotStatementType(@as, type);
    }
    protected GeneralType FindStatementType(ModifiedStatement modifiedStatement, GeneralType? expectedType)
    {
        if (modifiedStatement.Modifier.Equals(ModifierKeywords.Ref))
        {
            return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
        }

        if (modifiedStatement.Modifier.Equals(ModifierKeywords.Temp))
        {
            return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
        }

        throw new CompilerException($"Unimplemented modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
    }

    [return: NotNullIfNotNull(nameof(statement))]
    protected GeneralType? FindStatementType(StatementWithValue? statement)
        => FindStatementType(statement, null);

    [return: NotNullIfNotNull(nameof(statement))]
    protected GeneralType? FindStatementType(StatementWithValue? statement, GeneralType? expectedType)
    {
        return statement switch
        {
            null => null,
            FunctionCall v => FindStatementType(v),
            BinaryOperatorCall v => FindStatementType(v, expectedType),
            UnaryOperatorCall v => FindStatementType(v, expectedType),
            LiteralStatement v => FindStatementType(v, expectedType),
            Identifier v => FindStatementType(v, expectedType),
            AddressGetter v => FindStatementType(v),
            Pointer v => FindStatementType(v),
            NewInstance v => FindStatementType(v),
            ConstructorCall v => FindStatementType(v),
            Field v => FindStatementType(v),
            TypeCast v => FindStatementType(v),
            KeywordCall v => FindStatementType(v),
            IndexCall v => FindStatementType(v),
            ModifiedStatement v => FindStatementType(v, expectedType),
            AnyCall v => FindStatementType(v),
            _ => throw new CompilerException($"Statement {statement.GetType().Name} does not have a type", statement, CurrentFile)
        };
    }

    protected ImmutableArray<GeneralType> FindStatementTypes(ImmutableArray<StatementWithValue> statements)
    {
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(statements.Length);
        for (int i = 0; i < statements.Length; i++)
        { result.Add(FindStatementType(statements[i])); }
        return result.ToImmutable();
    }

    protected ImmutableArray<GeneralType> FindStatementTypes(ImmutableArray<StatementWithValue> statements, ImmutableArray<GeneralType> expectedTypes)
    {
        ImmutableArray<GeneralType>.Builder result = ImmutableArray.CreateBuilder<GeneralType>(statements.Length);
        for (int i = 0; i < statements.Length; i++)
        {
            GeneralType? expectedType = null;
            if (i < expectedTypes.Length) expectedType = expectedTypes[i];
            result.Add(FindStatementType(statements[i], expectedType));
        }
        return result.ToImmutable();
    }

    #endregion

    #region InlineMacro()

    static IEnumerable<StatementWithValue> InlineMacro(IEnumerable<StatementWithValue> statements, Dictionary<string, StatementWithValue> parameters)
        => statements.Select(statement => InlineMacro(statement, parameters));

    protected static bool InlineMacro(FunctionThingDefinition function, ImmutableArray<StatementWithValue> parameters, [NotNullWhen(true)] out Statement? inlined)
    {
        Dictionary<string, StatementWithValue> _parameters =
            function.Parameters
            .Select((value, index) => (value.Identifier.Content, parameters[index]))
            .ToDictionary();

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
            keywordCall.Identifier.Equals(StatementKeywords.Return) &&
            keywordCall.Parameters.Length == 1)
        { inlined = keywordCall.Parameters[0]; }

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
                keywordCall.Identifier.Equals(StatementKeywords.Return))
            { break; }
        }
        inlined = new Block(statements.ToArray(), block.Brackets)
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
            file: operatorCall.OriginalFile)
        {
            SurroundingBracelet = operatorCall.SurroundingBracelet,
            SaveValue = operatorCall.SaveValue,
            Semicolon = operatorCall.Semicolon,
        };

    static UnaryOperatorCall InlineMacro(UnaryOperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            op: operatorCall.Operator,
            left: InlineMacro(operatorCall.Left, parameters),
            file: operatorCall.OriginalFile)
        {
            SurroundingBracelet = operatorCall.SurroundingBracelet,
            SaveValue = operatorCall.SaveValue,
            Semicolon = operatorCall.Semicolon,
        };

    static KeywordCall InlineMacro(KeywordCall keywordCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            identifier: keywordCall.Identifier,
            parameters: InlineMacro(keywordCall.Parameters, parameters))
        {
            SaveValue = keywordCall.SaveValue,
            Semicolon = keywordCall.Semicolon,
        };

    static FunctionCall InlineMacro(FunctionCall functionCall, Dictionary<string, StatementWithValue> parameters)
    {
        IEnumerable<StatementWithValue> _parameters = InlineMacro(functionCall.Parameters, parameters);
        StatementWithValue? prevStatement = functionCall.PrevStatement;
        if (prevStatement != null)
        { prevStatement = InlineMacro(prevStatement, parameters); }
        return new FunctionCall(prevStatement, functionCall.Identifier, _parameters, functionCall.Brackets, functionCall.OriginalFile);
    }

    static AnyCall InlineMacro(AnyCall anyCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(anyCall.PrevStatement, parameters),
            parameters: InlineMacro(anyCall.Parameters, parameters),
            brackets: anyCall.Brackets,
            file: anyCall.OriginalFile)
        {
            SaveValue = anyCall.SaveValue,
            Semicolon = anyCall.Semicolon,
        };

    static ConstructorCall InlineMacro(ConstructorCall constructorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            keyword: constructorCall.Keyword,
            typeName: constructorCall.Type,
            parameters: InlineMacro(constructorCall.Parameters, parameters),
            brackets: constructorCall.Brackets,
            file: constructorCall.OriginalFile)
        {
            SaveValue = constructorCall.SaveValue,
            Semicolon = constructorCall.Semicolon,
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
        inlined = new IfContainer(branches)
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
             block: block)
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
              block: block)
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
            block: block)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }

    static bool InlineMacro(WhileLoop statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out WhileLoop? inlined)
    {
        inlined = null;

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Block, parameters, out Block? block))
        { return false; }

        inlined = new WhileLoop(
            keyword: statement.Keyword,
            condition: condition,
            block: block)
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }

    static bool InlineMacro(ForLoop statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out ForLoop? inlined)
    {
        inlined = null;

        if (!InlineMacro(statement.VariableDeclaration, parameters, out VariableDeclaration? variableDeclaration))
        { return false; }

        StatementWithValue condition = InlineMacro(statement.Condition, parameters);

        if (!InlineMacro(statement.Expression, parameters, out AnyAssignment? expression))
        { return false; }

        if (!InlineMacro(statement.Block, parameters, out Block? block))
        { return false; }

        inlined = new ForLoop(
            keyword: statement.Keyword,
            variableDeclaration: variableDeclaration,
            condition: condition,
            expression: expression,
            block: block)
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
            left: statement.Left,
            right: InlineMacro(statement.Right, parameters),
            file: statement.OriginalFile)
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
             file: statement.OriginalFile)
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
            file: statement.OriginalFile)
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
            prevStatement: InlineMacro(statement.PrevStatement, parameters))
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };

    static AddressGetter InlineMacro(AddressGetter statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            operatorToken: statement.Operator,
            prevStatement: InlineMacro(statement.PrevStatement, parameters))
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
        => new(
            type: statement.Type,
            value: statement.Value,
            valueToken: statement.ValueToken)
        {
            ImaginaryPosition = statement.ImaginaryPosition,
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };

    static Field InlineMacro(Field statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            fieldName: statement.Identifier,
            file: statement.OriginalFile)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };

    static IndexCall InlineMacro(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            indexStatement: InlineMacro(statement.Index, parameters),
            brackets: statement.Brackets,
            file: statement.OriginalFile)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };

    static TypeCast InlineMacro(TypeCast statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            keyword: statement.Keyword,
            type: statement.Type)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,

            CompiledType = statement.CompiledType,
        };

    static ModifiedStatement InlineMacro(ModifiedStatement modifiedStatement, Dictionary<string, StatementWithValue> parameters)
        => new(
            modifier: modifiedStatement.Modifier,
            statement: InlineMacro(modifiedStatement.Statement, parameters))
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
        KeywordCall v => InlineMacro(v, parameters),
        FunctionCall v => InlineMacro(v, parameters),
        AnyCall v => InlineMacro(v, parameters),
        Pointer v => InlineMacro(v, parameters),
        AddressGetter v => InlineMacro(v, parameters),
        LiteralStatement v => InlineMacro(v, parameters),
        Field v => InlineMacro(v, parameters),
        IndexCall v => InlineMacro(v, parameters),
        TypeCast v => InlineMacro(v, parameters),
        ModifiedStatement v => InlineMacro(v, parameters),
        TypeStatement v => v,
        ConstructorCall v => InlineMacro(v, parameters),
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };

    #endregion

    #region Control Flow Usage

    [Flags]
    public enum ControlFlowUsage
    {
        None = 0x0,

        Return = 0x1,
        ConditionalReturn = 0x2,
        AnyReturn = Return | ConditionalReturn,

        Break = 0x4,
    }

    public static ControlFlowUsage FindControlFlowUsage(IEnumerable<Statement> statements, bool inDepth = false)
    {
        ControlFlowUsage result = ControlFlowUsage.None;
        foreach (Statement statement in statements)
        { result |= FindControlFlowUsage(statement, inDepth); }
        return result;
    }

    public static ControlFlowUsage FindControlFlowUsage(Statement statement, bool inDepth = false) => statement switch
    {
        Block v => FindControlFlowUsage(v.Statements, true),
        KeywordCall v => FindControlFlowUsage(v, inDepth),
        WhileLoop v => FindControlFlowUsage(v.Block.Statements, true),
        ForLoop v => FindControlFlowUsage(v.Block.Statements, true),
        IfContainer v => FindControlFlowUsage(v.Branches, true),
        BaseBranch v => FindControlFlowUsage(v.Block, true),

        Assignment => ControlFlowUsage.None,
        VariableDeclaration => ControlFlowUsage.None,
        AnyCall => ControlFlowUsage.None,
        ShortOperatorCall => ControlFlowUsage.None,
        CompoundAssignment => ControlFlowUsage.None,

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
            case StatementKeywords.Throw:
                return ControlFlowUsage.None;

            default: throw new NotImplementedException(statement.ToString());
        }
    }

    #endregion

    protected class EvaluationContext
    {
        readonly Dictionary<StatementWithValue, DataItem> _values;
        readonly Stack<Stack<Dictionary<string, DataItem>>>? _frames;

        public readonly List<Statement> RuntimeStatements;
        public Dictionary<string, DataItem>? LastScope => _frames?.Last.Last;
        public bool IsReturning;
        public bool IsBreaking;

        public static EvaluationContext Empty => new(null, null);

        public static EvaluationContext EmptyWithVariables => new(null, new Dictionary<string, DataItem>());

        public EvaluationContext(IDictionary<StatementWithValue, DataItem>? values, IDictionary<string, DataItem>? variables)
        {
            if (values != null)
            { _values = new Dictionary<StatementWithValue, DataItem>(values); }
            else
            { _values = new Dictionary<StatementWithValue, DataItem>(); }

            if (variables != null)
            { _frames = new Stack<Stack<Dictionary<string, DataItem>>>() { new() { new Dictionary<string, DataItem>(variables) } }; }
            else
            { _frames = null; }

            RuntimeStatements = new List<Statement>();
        }

        public bool TryGetValue(StatementWithValue statement, out DataItem value)
        {
            if (_values.TryGetValue(statement, out value))
            { return true; }

            if (statement is Identifier identifier &&
                TryGetVariable(identifier.Content, out value))
            { return true; }

            value = default;
            return false;
        }

        public bool TryGetVariable(string name, out DataItem value)
        {
            value = default;

            if (_frames == null)
            { return false; }

            Stack<Dictionary<string, DataItem>> frame = _frames.Last;
            foreach (Dictionary<string, DataItem> scope in frame)
            {
                if (scope.TryGetValue(name, out value))
                { return true; }
            }

            return false;
        }

        public bool TrySetVariable(string name, DataItem value)
        {
            if (_frames == null)
            { return false; }

            Stack<Dictionary<string, DataItem>> frame = _frames.Last;
            foreach (Dictionary<string, DataItem> scope in frame)
            {
                if (scope.ContainsKey(name))
                {
                    scope[name] = value;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetType(StatementWithValue statement, [NotNullWhen(true)] out GeneralType? type)
        {
            if (!TryGetValue(statement, out DataItem value))
            {
                type = null;
                return false;
            }

            type = new BuiltinType(value.Type);
            return true;
        }

        public void PushScope(IDictionary<string, DataItem>? variables = null)
        {
            if (_frames is null) return;
            if (variables is null)
            { _frames?.Last.Push(new Dictionary<string, DataItem>()); }
            else
            { _frames?.Last.Push(new Dictionary<string, DataItem>(variables)); }
        }

        public void PopScope()
        {
            if (_frames is null) return;
            _frames.Last.Pop();
        }
    }

    #region TryCompute()

    public static DataItem Compute(string @operator, DataItem left, DataItem right) => @operator switch
    {
        "!" => !left,

        "+" => left + right,
        "-" => left - right,
        "*" => left * right,
        "/" => left / right,
        "%" => left % right,

        "&&" => new DataItem((bool)left && (bool)right),
        "||" => new DataItem((bool)left || (bool)right),

        "&" => left & right,
        "|" => left | right,
        "^" => left ^ right,

        "<<" => left << right,
        ">>" => left >> right,

        "<" => new DataItem(left < right),
        ">" => new DataItem(left > right),
        "==" => new DataItem(left == right),
        "!=" => new DataItem(left != right),
        "<=" => new DataItem(left <= right),
        ">=" => new DataItem(left >= right),

        _ => throw new NotImplementedException($"Unknown operator \"{@operator}\""),
    };

    bool TryCompute(Pointer pointer, EvaluationContext context, out DataItem value)
    {
        if (pointer.PrevStatement is AddressGetter addressGetter)
        { return TryCompute(addressGetter.PrevStatement, context, out value); }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(BinaryOperatorCall @operator, EvaluationContext context, out DataItem value)
    {
        if (context.TryGetValue(@operator, out value))
        { return true; }

        // TODO: @operator.OriginalFile can be null
        if (GetOperator(@operator, @operator.OriginalFile, out FunctionQueryResult<CompiledOperator>? result, out _))
        {
            if (TryCompute(@operator.Parameters, context, out ImmutableArray<DataItem> parameterValues) &&
                TryEvaluate(result.Function, parameterValues, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
                returnValue.HasValue &&
                runtimeStatements.Length == 0)
            {
                value = returnValue.Value;
                return true;
            }

            value = DataItem.Null;
            return false;
        }

        if (!TryCompute(@operator.Left, context, out DataItem leftValue))
        {
            value = DataItem.Null;
            return false;
        }

        string op = @operator.Operator.Content;

        if (TryCompute(@operator.Right, context, out DataItem rightValue))
        {
            value = Compute(op, leftValue, rightValue);
            return true;
        }

        switch (op)
        {
            case "&&":
            {
                if (!(bool)leftValue)
                {
                    value = new DataItem(false);
                    return true;
                }
                break;
            }
            case "||":
            {
                if ((bool)leftValue)
                {
                    value = new DataItem(true);
                    return true;
                }
                break;
            }
            default:
                if (context is not null &&
                    context.TryGetValue(@operator, out value))
                { return true; }

                value = DataItem.Null;
                return false;
        }

        value = leftValue;
        return true;
    }
    bool TryCompute(UnaryOperatorCall @operator, EvaluationContext context, out DataItem value)
    {
        if (context.TryGetValue(@operator, out value))
        { return true; }

        // TODO: @operator.OriginalFile can be null
        if (GetOperator(@operator, @operator.OriginalFile, out FunctionQueryResult<CompiledOperator>? result, out _))
        {
            if (TryCompute(@operator.Parameters, context, out ImmutableArray<DataItem> parameterValues) &&
                TryEvaluate(result.Function, parameterValues, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
                returnValue.HasValue &&
                runtimeStatements.Length == 0)
            {
                value = returnValue.Value;
                return true;
            }

            value = DataItem.Null;
            return false;
        }

        if (!TryCompute(@operator.Left, context, out DataItem leftValue))
        {
            value = DataItem.Null;
            return false;
        }

        string op = @operator.Operator.Content;

        if (op == "!")
        {
            value = !leftValue;
            return true;
        }

        value = leftValue;
        return true;
    }
    static bool TryCompute(LiteralStatement literal, out DataItem value)
    {
        switch (literal.Type)
        {
            case LiteralType.Integer:
                value = new DataItem(literal.GetInt());
                return true;
            case LiteralType.Float:
                value = new DataItem(literal.GetFloat());
                return true;
            case LiteralType.Char:
                if (literal.Value.Length != 1)
                {
                    value = DataItem.Null;
                    return false;
                }
                value = new DataItem(literal.Value[0]);
                return true;
            case LiteralType.String:
            default:
                value = DataItem.Null;
                return false;
        }
    }
    bool TryCompute(KeywordCall keywordCall, out DataItem value)
    {
        if (keywordCall.Identifier.Content == "sizeof")
        {
            if (keywordCall.Parameters.Length != 1)
            {
                value = DataItem.Null;
                return false;
            }

            StatementWithValue param0 = keywordCall.Parameters[0];
            GeneralType param0Type = FindStatementType(param0);

            value = new DataItem(param0Type.Size);
            return true;
        }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(AnyCall anyCall, EvaluationContext context, out DataItem value)
    {
        if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
        { return TryCompute(functionCall, context, out value); }

        if (context.TryGetValue(anyCall, out value))
        { return true; }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(FunctionCall functionCall, EvaluationContext context, out DataItem value)
    {
        if (functionCall.Identifier.Content == "sizeof")
        {
            if (functionCall.Parameters.Length != 1)
            {
                value = DataItem.Null;
                return false;
            }

            StatementWithValue param0 = functionCall.Parameters[0];
            GeneralType param0Type = FindStatementType(param0);

            value = new DataItem(param0Type.Size);
            return true;
        }

        if (GetFunction(functionCall, out FunctionQueryResult<CompiledFunction>? result, out _))
        {
            CompiledFunction? function = result.Function;

            if (!function.CanUse(CurrentFile))
            {
                value = default;
                return false;
            }

            if (function.IsExternal &&
                !functionCall.SaveValue &&
                TryCompute(functionCall.MethodParameters, context, out ImmutableArray<DataItem> parameters))
            {
                FunctionCall newFunctionCall = new(
                    null,
                    functionCall.Identifier,
                    Literal.CreateAnonymous(parameters, functionCall.MethodParameters),
                    functionCall.Brackets,
                    functionCall.OriginalFile)
                {
                    SaveValue = functionCall.SaveValue,
                    Semicolon = functionCall.Semicolon,
                };
                context.RuntimeStatements.Add(newFunctionCall);
                value = DataItem.Null;
                return true;
            }

            StatementWithValue[] convertedParameters = new StatementWithValue[functionCall.MethodParameters.Length];
            for (int i = 0; i < functionCall.MethodParameters.Length; i++)
            {
                convertedParameters[i] = functionCall.MethodParameters[i];
                GeneralType passed = FindStatementType(functionCall.MethodParameters[i]);
                GeneralType defined = function.ParameterTypes[i];
                if (passed.Equals(defined)) continue;
                convertedParameters[i] = new TypeCast(
                    convertedParameters[i],
                    Token.CreateAnonymous("as"),
                    defined.ToTypeInstance());
            }

            if (TryEvaluate(function, convertedParameters.ToImmutableArray(), out DataItem? returnValue, out Statement[]? runtimeStatements) &&
                returnValue.HasValue &&
                runtimeStatements.Length == 0)
            {
                value = returnValue.Value;
                return true;
            }
        }

        if (context.TryGetValue(functionCall, out value))
        { return true; }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(Identifier identifier, EvaluationContext context, out DataItem value)
    {
        if (GetConstant(identifier.Content, out IConstant? constantValue))
        {
            identifier.Reference = constantValue;
            value = constantValue.Value;
            return true;
        }

        if (context.TryGetValue(identifier, out value))
        { return true; }

        if (TryGetVariableValue(identifier, out value))
        { return true; }

        value = DataItem.Null;
        return false;
    }
    protected virtual bool TryGetVariableValue(Identifier identifier, out DataItem value)
    {
        value = default;
        return false;
    }
    bool TryCompute(Field field, EvaluationContext context, out DataItem value)
    {
        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is ArrayType arrayType && field.Identifier.Equals("Length"))
        {
            value = new DataItem(arrayType.Length);
            return true;
        }

        if (context.TryGetValue(field, out value))
        { return true; }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(TypeCast typeCast, EvaluationContext context, out DataItem value)
    {
        if (TryCompute(typeCast.PrevStatement, context, out value))
        {
            GeneralType type = GeneralType.From(typeCast.Type, FindType, TryCompute);
            if (type is not BuiltinType builtinType) return false;
            if (!DataItem.TryCast(ref value, builtinType.RuntimeType))
            { return false; }
            return true;
        }

        value = DataItem.Null;
        return false;
    }
    bool TryCompute(IndexCall indexCall, EvaluationContext context, out DataItem value)
    {
        if (indexCall.PrevStatement is LiteralStatement literal &&
            literal.Type == LiteralType.String &&
            TryCompute(indexCall.Index, context, out DataItem index))
        {
            if (index == literal.Value.Length)
            { value = new DataItem('\0'); }
            else
            { value = new DataItem(literal.Value[(int)index]); }
            return true;
        }

        value = DataItem.Null;
        return false;
    }
    protected bool TryCompute([NotNullWhen(true)] StatementWithValue? statement, out DataItem value)
        => TryCompute(statement, EvaluationContext.Empty, out value);
    bool TryCompute(IEnumerable<StatementWithValue>? statements, EvaluationContext context, [NotNullWhen(true)] out ImmutableArray<DataItem> values)
    {
        if (statements is null)
        {
            values = ImmutableArray<DataItem>.Empty;
            return false;
        }

        ImmutableArray<DataItem>.Builder result = ImmutableArray.CreateBuilder<DataItem>();
        foreach (StatementWithValue statement in statements)
        {
            if (!TryCompute(statement, context, out DataItem value))
            {
                values = ImmutableArray<DataItem>.Empty;
                return false;
            }

            result.Add(value);
        }

        values = result.ToImmutable();
        return true;
    }
    bool TryCompute([NotNullWhen(true)] StatementWithValue? statement, EvaluationContext context, out DataItem value)
    {
        value = DataItem.Null;

        if (statement is null)
        { return false; }

        if (context.TryGetValue(statement, out value))
        { return true; }

        return statement switch
        {
            LiteralStatement v => TryCompute(v, out value),
            BinaryOperatorCall v => TryCompute(v, context, out value),
            UnaryOperatorCall v => TryCompute(v, context, out value),
            Pointer v => TryCompute(v, context, out value),
            KeywordCall v => TryCompute(v, out value),
            FunctionCall v => TryCompute(v, context, out value),
            AnyCall v => TryCompute(v, context, out value),
            Identifier v => TryCompute(v, context, out value),
            TypeCast v => TryCompute(v, context, out value),
            Field v => TryCompute(v, context, out value),
            IndexCall v => TryCompute(v, context, out value),
            ModifiedStatement => false,
            NewInstance => false,
            ConstructorCall => false,
            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };
    }

    public static bool TryComputeSimple(BinaryOperatorCall @operator, out DataItem value)
    {
        if (!TryComputeSimple(@operator.Left, out DataItem leftValue))
        {
            value = DataItem.Null;
            return false;
        }

        string op = @operator.Operator.Content;

        if (TryComputeSimple(@operator.Right, out DataItem rightValue))
        {
            value = Compute(op, leftValue, rightValue);
            return true;
        }

        switch (op)
        {
            case "&&":
            {
                if (!leftValue)
                {
                    value = new DataItem(false);
                    return true;
                }
                break;
            }
            case "||":
            {
                if (leftValue)
                {
                    value = new DataItem(true);
                    return true;
                }
                break;
            }
            default:
                value = DataItem.Null;
                return false;
        }

        value = leftValue;
        return true;
    }
    public static bool TryComputeSimple(UnaryOperatorCall @operator, out DataItem value)
    {
        if (!TryComputeSimple(@operator.Left, out DataItem leftValue))
        {
            value = DataItem.Null;
            return false;
        }

        string op = @operator.Operator.Content;

        if (op == "!")
        {
            value = leftValue;
            return true;
        }

        value = leftValue;
        return true;
    }
    public static bool TryComputeSimple(IndexCall indexCall, out DataItem value)
    {
        if (indexCall.PrevStatement is LiteralStatement literal &&
            literal.Type == LiteralType.String &&
            TryComputeSimple(indexCall.Index, out DataItem index))
        {
            if (index == literal.Value.Length)
            { value = new DataItem('\0'); }
            else
            { value = new DataItem(literal.Value[(int)index]); }
            return true;
        }

        value = DataItem.Null;
        return false;
    }
    public static bool TryComputeSimple(StatementWithValue? statement, out DataItem value)
    {
        value = DataItem.Null;
        return statement switch
        {
            LiteralStatement v => TryCompute(v, out value),
            BinaryOperatorCall v => TryComputeSimple(v, out value),
            UnaryOperatorCall v => TryComputeSimple(v, out value),
            IndexCall v => TryComputeSimple(v, out value),
            _ => false,
        };
    }

    protected bool TryEvaluate(CompiledFunction function, ImmutableArray<StatementWithValue> parameters, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        if (TryCompute(parameters, EvaluationContext.Empty, out ImmutableArray<DataItem> parameterValues) &&
            TryEvaluate(function, parameterValues, out value, out runtimeStatements))
        { return true; }

        if (!InlineMacro(function, parameters, out Statement? inlined))
        { return false; }

        EvaluationContext context = EvaluationContext.EmptyWithVariables;
        CurrentEvaluationContext.Push(context);
        if (TryEvaluate(inlined, context))
        {
            value = null;
            runtimeStatements = context.RuntimeStatements.ToArray();
            CurrentEvaluationContext.Pop();
            return true;
        }
        CurrentEvaluationContext.Pop();

        return false;
    }
    bool TryEvaluate(ICompiledFunction function, ImmutableArray<DataItem> parameterValues, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
    {
        value = null;
        runtimeStatements = null;

        if (function.Block is null)
        { return false; }

        for (int i = 0; i < parameterValues.Length; i++)
        {
            if (!function.Parameters[i].Type.Equals(new BuiltinType(parameterValues[i].Type).ToTypeInstance()))
            {
                Debugger.Break();
                return false;
            }
        }

        Dictionary<string, DataItem> variables = new();

        if (function.ReturnSomething)
        {
            if (function.Type is not BuiltinType returnType)
            { return false; }

            if (function.ReturnSomething)
            { variables.Add("@return", GetInitialValue(returnType.Type)); }
        }

        for (int i = 0; i < parameterValues.Length; i++)
        { variables.Add(function.Parameters[i].Identifier.Content, parameterValues[i]); }

        EvaluationContext context = new(null, variables);

        CurrentEvaluationContext.Push(context);
        Uri? prevFile = CurrentFile;
        CurrentFile = function.File;

        bool success = TryEvaluate(function.Block, context);

        CurrentFile = prevFile;
        CurrentEvaluationContext.Pop();

        if (!success)
        { return false; }

        if (function.ReturnSomething)
        {
            if (context.LastScope is null)
            { throw new InternalException(); }
            value = context.LastScope["@return"];
        }

        runtimeStatements = context.RuntimeStatements.ToArray();

        return true;
    }

    bool TryEvaluate(WhileLoop whileLoop, EvaluationContext context)
    {
        int iterations = 64;

        while (true)
        {
            if (!TryCompute(whileLoop.Condition, context, out DataItem condition))
            { return false; }

            if (!condition)
            { break; }

            if (iterations-- < 0)
            { return false; }

            if (!TryEvaluate(whileLoop.Block, context))
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
    bool TryEvaluate(ForLoop forLoop, EvaluationContext context)
    {
        int iterations = 5048;

        context.PushScope();

        if (!TryEvaluate(forLoop.VariableDeclaration, context))
        { return false; }

        while (true)
        {
            if (!TryCompute(forLoop.Condition, context, out DataItem condition))
            { return false; }

            if (!condition)
            { break; }

            if (iterations-- < 0)
            { return false; }

            if (!TryEvaluate(forLoop.Block, context))
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
    bool TryEvaluate(IfContainer ifContainer, EvaluationContext context)
    {
        foreach (BaseBranch _branch in ifContainer.Branches)
        {
            switch (_branch)
            {
                case IfBranch branch:
                {
                    if (!TryCompute(branch.Condition, context, out DataItem condition))
                    { return false; }

                    if (!condition)
                    { continue; }

                    if (!TryEvaluate(branch.Block, context))
                    { return false; }

                    return true;
                }

                case ElseIfBranch branch:
                {
                    if (!TryCompute(branch.Condition, context, out DataItem condition))
                    { return false; }

                    if (!condition)
                    { continue; }

                    if (!TryEvaluate(branch.Block, context))
                    { return false; }

                    return true;
                }

                case ElseBranch branch:
                {
                    if (!TryEvaluate(branch.Block, context))
                    { return false; }

                    return true;
                }

                default: throw new UnreachableException();
            }
        }

        return true;
    }
    bool TryEvaluate(Block block, EvaluationContext context)
    {
        context.PushScope();
        bool result = TryEvaluate(block.Statements, context);
        context.PopScope();
        return result;
    }
    bool TryEvaluate(VariableDeclaration variableDeclaration, EvaluationContext context)
    {
        DataItem value;

        if (context.LastScope is null)
        { return false; }

        if (variableDeclaration.InitialValue is null &&
            variableDeclaration.Type.ToString() != StatementKeywords.Var)
        {
            GeneralType variableType = GeneralType.From(variableDeclaration.Type, FindType, TryCompute, variableDeclaration.File);
            if (!GetInitialValue(variableType, out value))
            { return false; }
        }
        else
        {
            if (!TryCompute(variableDeclaration.InitialValue, context, out value))
            { return false; }
        }

        if (!context.LastScope.TryAdd(variableDeclaration.Identifier.Content, value))
        { return false; }

        return true;
    }
    bool TryEvaluate(AnyAssignment anyAssignment, EvaluationContext context)
    {
        Assignment assignment = anyAssignment.ToAssignment();

        if (assignment.Left is not Identifier identifier)
        { return false; }

        if (!TryCompute(assignment.Right, context, out DataItem value))
        { return false; }

        if (!context.TrySetVariable(identifier.Content, value))
        { return false; }

        return true;
    }
    bool TryEvaluate(KeywordCall keywordCall, EvaluationContext context)
    {
        if (keywordCall.Identifier.Content == StatementKeywords.Return)
        {
            context.IsReturning = true;

            if (keywordCall.Parameters.Length == 0)
            { return true; }

            if (keywordCall.Parameters.Length == 1)
            {
                if (!TryCompute(keywordCall.Parameters[0], context, out DataItem returnValue))
                { return false; }

                if (!context.TrySetVariable("@return", returnValue))
                { return false; }

                return true;
            }

            return false;
        }

        if (keywordCall.Identifier.Content == StatementKeywords.Break)
        {
            context.IsBreaking = true;

            if (keywordCall.Parameters.Length != 0)
            { return false; }

            return true;
        }

        return false;
    }
    bool TryEvaluate(Statement statement, EvaluationContext context) => statement switch
    {
        Block v => TryEvaluate(v, context),
        VariableDeclaration v => TryEvaluate(v, context),
        WhileLoop v => TryEvaluate(v, context),
        ForLoop v => TryEvaluate(v, context),
        AnyAssignment v => TryEvaluate(v, context),
        KeywordCall v => TryEvaluate(v, context),
        IfContainer v => TryEvaluate(v, context),
        StatementWithValue v => TryCompute(v, context, out _),
        _ => throw new NotImplementedException(statement.GetType().ToString()),
    };
    bool TryEvaluate(ImmutableArray<Statement> statements, EvaluationContext context)
    {
        foreach (Statement statement in statements)
        {
            if (!TryEvaluate(statement, context))
            { return false; }

            if (context.IsReturning || context.IsBreaking)
            { break; }
        }
        return true;
    }

    readonly Stack<EvaluationContext> CurrentEvaluationContext = new();

    #endregion

    protected bool IsUnrollable(ForLoop loop)
    {
        string iteratorVariable = loop.VariableDeclaration.Identifier.Content;
        Dictionary<string, StatementWithValue> _params = new()
        {
            { iteratorVariable, Literal.CreateAnonymous(new DataItem(0), loop.VariableDeclaration) }
        };

        StatementWithValue condition = loop.Condition;
        Assignment iteratorExpression = loop.Expression.ToAssignment();

        if (iteratorExpression.Left is not Identifier iteratorExpressionLeft ||
            iteratorExpressionLeft.Content != iteratorVariable)
        { return false; }

        condition = InlineMacro(condition, _params);
        StatementWithValue iteratorExpressionRight = InlineMacro(iteratorExpression.Right, _params);

        if (!TryCompute(condition, EvaluationContext.Empty, out _) ||
            !TryCompute(iteratorExpressionRight, EvaluationContext.Empty, out _))
        { return false; }

        // TODO: return and break in unrolled loop
        if (loop.Block.GetStatement<KeywordCall>(out _, (statement) => statement.Identifier.Content is StatementKeywords.Break or StatementKeywords.Return))
        { return false; }

        return true;
    }

    protected ImmutableArray<Block> Unroll(ForLoop loop, Dictionary<StatementWithValue, DataItem> values)
    {
        VariableDeclaration iteratorVariable = loop.VariableDeclaration;
        StatementWithValue condition = loop.Condition;
        AnyAssignment iteratorExpression = loop.Expression;

        DataItem iterator;
        if (iteratorVariable.InitialValue is not null)
        {
            if (!TryCompute(iteratorVariable.InitialValue, EvaluationContext.Empty, out iterator))
            { throw new CompilerException($"Failed to compute the iterator initial value (\"{iteratorVariable.InitialValue}\") for loop unrolling", iteratorVariable.InitialValue, CurrentFile); }
        }
        else
        {
            GeneralType iteratorType = GeneralType.From(iteratorVariable.Type, FindType, TryCompute);
            iteratorVariable.Type.SetAnalyzedType(iteratorType);
            iterator = GetInitialValue(iteratorType);
        }

        KeyValuePair<string, StatementWithValue> GetIteratorStatement()
            => new(iteratorVariable.Identifier.Content, Literal.CreateAnonymous(iterator, Position.UnknownPosition));

        DataItem ComputeIterator()
        {
            KeyValuePair<string, StatementWithValue> _yeah = GetIteratorStatement();
            StatementWithValue _condition = InlineMacro(condition, new Dictionary<string, StatementWithValue>()
            {
                {_yeah.Key, _yeah.Value }
            });

            if (!TryCompute(_condition, new EvaluationContext(values, null), out DataItem result))
            { throw new CompilerException($"Failed to compute the condition value (\"{_condition}\") for loop unrolling", condition, CurrentFile); }

            return result;
        }

        DataItem ComputeExpression()
        {
            KeyValuePair<string, StatementWithValue> _yeah = GetIteratorStatement();
            Assignment assignment = iteratorExpression.ToAssignment();

            if (assignment.Left is not Identifier leftIdentifier)
            { throw new CompilerException($"Failed to unroll for loop", assignment.Left, CurrentFile); }

            StatementWithValue _value = InlineMacro(assignment.Right, new Dictionary<string, StatementWithValue>()
            {
                { _yeah.Key, _yeah.Value }
            });

            if (!TryCompute(_value, new EvaluationContext(values, null), out DataItem result))
            { throw new CompilerException($"Failed to compute the condition value (\"{_value}\") for loop unrolling", condition, CurrentFile); }

            return result;
        }

        ImmutableArray<Block>.Builder statements = ImmutableArray.CreateBuilder<Block>();

        while (ComputeIterator())
        {
            KeyValuePair<string, StatementWithValue> iteratorStatement = GetIteratorStatement();
            Dictionary<string, StatementWithValue> parameters = new()
            {
                { iteratorStatement.Key, iteratorStatement.Value }
            };

            if (!InlineMacro(loop.Block, parameters, out Block? subBlock))
            { throw new CompilerException($"Failed to inline", loop.Block, CurrentFile); }

            statements.Add(subBlock);

            iterator = ComputeExpression();
        }

        return statements.ToImmutable();
    }
}
