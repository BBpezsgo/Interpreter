namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;
using LiteralStatement = Parser.Statement.Literal;

public struct GeneratorSettings
{
    public bool GenerateComments;
    public bool PrintInstructions;
    public bool DontOptimize;
    public bool GenerateDebugInstructions;
    public bool ExternalFunctionsCache;
    public bool CheckNullPointers;
    public CompileLevel CompileLevel;

    public readonly bool OptimizeCode => !DontOptimize;

    public GeneratorSettings(GeneratorSettings other)
    {
        GenerateComments = other.GenerateComments;
        PrintInstructions = other.PrintInstructions;
        DontOptimize = other.DontOptimize;
        GenerateDebugInstructions = other.GenerateDebugInstructions;
        ExternalFunctionsCache = other.ExternalFunctionsCache;
        CheckNullPointers = other.CheckNullPointers;
        CompileLevel = other.CompileLevel;
    }

    public static GeneratorSettings Default => new()
    {
        GenerateComments = true,
        PrintInstructions = false,
        DontOptimize = false,
        GenerateDebugInstructions = true,
        ExternalFunctionsCache = false,
        CheckNullPointers = true,
        CompileLevel = CompileLevel.Minimal,
    };
}

public abstract class CodeGenerator
{
    protected readonly struct CompliableTemplate<T> where T : ITemplateable<T>
    {
        public readonly T OriginalFunction;
        public readonly T Function;
        public readonly Dictionary<string, GeneralType> TypeArguments;

        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        public CompliableTemplate(T function, Dictionary<string, GeneralType> typeArguments)
        {
            OriginalFunction = function;
            TypeArguments = typeArguments;

            CodeGenerator.ValidateTypeArguments(TypeArguments);
            Function = OriginalFunction.InstantiateTemplate(typeArguments);
        }

        public override string ToString() => Function?.ToString() ?? "null";
    }

    protected class ControlFlowBlock : IDuplicatable<ControlFlowBlock>
    {
        public readonly int FlagAddress;
        public readonly Stack<int> PendingJumps;
        public readonly Stack<bool> Doings;

        public ControlFlowBlock(int flagAddress)
        {
            FlagAddress = flagAddress;

            PendingJumps = new Stack<int>();
            PendingJumps.Push(0);

            Doings = new Stack<bool>();
            Doings.Push(false);
        }

        public ControlFlowBlock Duplicate()
        {
            ControlFlowBlock result = new(FlagAddress);
            result.PendingJumps.Set(PendingJumps);
            result.Doings.Set(Doings);
            return result;
        }
    }

    #region Protected Fields

    protected ImmutableArray<CompiledStruct> CompiledStructs;
    protected ImmutableArray<CompiledFunction> CompiledFunctions;
    protected ImmutableArray<CompiledOperator> CompiledOperators;
    protected ImmutableArray<CompiledConstructor> CompiledConstructors;
    protected ImmutableArray<CompiledGeneralFunction> CompiledGeneralFunctions;
    protected ImmutableArray<CompiledEnum> CompiledEnums;
    protected ImmutableArray<IConstant> CompiledGlobalConstants;

    protected readonly Stack<IConstant> CompiledLocalConstants;
    protected readonly Stack<int> LocalConstantsStack;

    protected readonly List<CompiledParameter> CompiledParameters;
    protected readonly List<CompiledVariable> CompiledVariables;
    protected readonly List<CompiledVariable> CompiledGlobalVariables;

    protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
    protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledConstructor>> CompilableConstructors => compilableConstructors;

    protected Uri? CurrentFile;
    protected bool InFunction;
    protected readonly Stack<bool> InMacro;
    protected readonly Dictionary<string, GeneralType> TypeArguments;
    protected DebugInformation? DebugInfo;

    protected readonly GeneratorSettings Settings;
    protected readonly PrintCallback? Print;
    protected readonly AnalysisCollection? AnalysisCollection;

    #endregion

    #region Private Fields

    readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
    readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
    readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();
    readonly List<CompliableTemplate<CompiledConstructor>> compilableConstructors = new();

    #endregion

    protected CodeGenerator(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print)
    {
        CompiledGlobalConstants = ImmutableArray.Create<IConstant>();

        CompiledLocalConstants = new Stack<IConstant>();
        LocalConstantsStack = new Stack<int>();

        CompiledParameters = new List<CompiledParameter>();
        CompiledVariables = new List<CompiledVariable>();
        CompiledGlobalVariables = new List<CompiledVariable>();

        compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
        compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
        compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();

        CurrentFile = null;
        InFunction = false;
        InMacro = new Stack<bool>();
        TypeArguments = new Dictionary<string, GeneralType>();

        CompiledStructs = compilerResult.Structs;
        CompiledFunctions = compilerResult.Functions;
        CompiledOperators = compilerResult.Operators;
        CompiledConstructors = compilerResult.Constructors;
        CompiledGeneralFunctions = compilerResult.GeneralFunctions;
        CompiledEnums = compilerResult.Enums;

        AnalysisCollection = analysisCollection;
        Settings = settings;
        Print = print;
    }

    /// <exception cref="InternalException"/>
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
            { throw new CompilerException($"Constant \"{constant.Identifier}\" defined more than once", constant, constant.FilePath); }

            constant = _constant;
        }

        foreach (IConstant _constant in CompiledGlobalConstants)
        {
            if (_constant.Identifier != identifier)
            { continue; }

            if (constant is not null)
            { throw new CompilerException($"Constant \"{constant.Identifier}\" defined more than once", constant, constant.FilePath); }

            constant = _constant;
        }

        return constant is not null;
    }

    protected bool StatementCanBeDeallocated(StatementWithValue statement, out bool explicitly)
    {
        if (statement is ModifiedStatement modifiedStatement &&
            modifiedStatement.Modifier.Equals(ModifierKeywords.Temp))
        {
            if (modifiedStatement.Statement is LiteralStatement ||
                modifiedStatement.Statement is BinaryOperatorCall ||
                modifiedStatement.Statement is UnaryOperatorCall)
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

    protected void AddCompilable<TFunction>(CompliableTemplate<TFunction> compilable)
        where TFunction : ITemplateable<TFunction>
    {
        switch (compilable)
        {
            case CompliableTemplate<CompiledFunction> template: AddCompilable(template); break;
            case CompliableTemplate<CompiledOperator> template: AddCompilable(template); break;
            case CompliableTemplate<CompiledGeneralFunction> template: AddCompilable(template); break;
            case CompliableTemplate<CompiledConstructor> template: AddCompilable(template); break;
            default:
                throw new NotImplementedException();
        }
    }

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

    protected void SetTypeArguments(Dictionary<string, GeneralType> arguments)
    {
        TypeArguments.Clear();
        TypeArguments.AddRange(arguments);
    }

    protected void SetTypeArguments(Dictionary<string, GeneralType> arguments, out Dictionary<string, GeneralType> old)
    {
        old = new Dictionary<string, GeneralType>(TypeArguments);
        SetTypeArguments(arguments);
    }

    #region GetEnum()

    protected bool GetEnum(string name, [NotNullWhen(true)] out CompiledEnum? @enum)
        => CodeGenerator.GetEnum(CompiledEnums, name, out @enum);

    public static bool GetEnum(IEnumerable<CompiledEnum?> enums, string name, [NotNullWhen(true)] out CompiledEnum? @enum)
    {
        foreach (CompiledEnum? @enum_ in enums)
        {
            if (@enum_ == null) continue;
            if (@enum_.Identifier.Content == name)
            {
                @enum = @enum_;
                return true;
            }
        }
        @enum = null;
        return false;
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

    protected bool GetConstructor(
        GeneralType type,
        GeneralType[] arguments,
        [NotNullWhen(true)] out CompiledConstructor? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        {
            List<GeneralType> newArguments = new(arguments.Length + 1);
            newArguments.Add(type);
            newArguments.AddRange(arguments);
            arguments = newArguments.ToArray();
        }

        return GetFunction<CompiledConstructor, GeneralType, GeneralType>(
            CompiledConstructors,
            compilableConstructors,
            "constructor",
            CompiledConstructor.ToReadable(type, arguments),

            type,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

    protected bool GetIndexGetter(
        GeneralType prevType,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction<CompiledFunction, Token, string>(
            CompiledFunctions,
            compilableFunctions,
            "function",
            null,

            BuiltinFunctionNames.IndexerGet,
            new GeneralType[] { prevType, new BuiltinType(BasicType.Integer) },
            out result,
            out typeArguments,
            addCompilable,
            out error);

    protected bool GetIndexSetter(
        GeneralType prevType,
        GeneralType elementType,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction<CompiledFunction, Token, string>(
            CompiledFunctions,
            compilableFunctions,
            "function",
            null,

            BuiltinFunctionNames.IndexerSet,
            new GeneralType[] { prevType, new BuiltinType(BasicType.Integer), elementType },
            out result,
            out typeArguments,
            addCompilable,
            out error);

    protected bool TryGetBuiltinFunction(
        string builtinName,
        GeneralType[] arguments,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        IEnumerable<CompiledFunction> builtinCompiledFunctions =
            CompiledFunctions
            .Where(v => v.BuiltinFunctionName == builtinName);

        IEnumerable<CompliableTemplate<CompiledFunction>> builtinCompilableFunctions =
            compilableFunctions
            .Where(v => v.Function.BuiltinFunctionName == builtinName);

        return GetFunction<CompiledFunction, Token, string>(
            builtinCompiledFunctions,
            builtinCompilableFunctions,
            "builtin function",
            $"[Builtin(\"{builtinName}\")] ?({string.Join<GeneralType>(", ", arguments)})",

            null,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

    #region GetFunction()

    protected bool GetFunction(
        string identifier,
        GeneralType? type,
        [NotNullWhen(true)] out CompiledFunction? result,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        if (type is null || type is not FunctionType functionType)
        { return GetFunction(identifier, out result, out error); }
        return GetFunction(identifier, functionType, out result, out error);
    }

    protected bool GetFunction(
        AnyCall call,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        result = null;
        typeArguments = null;
        error = null;

        if (!call.ToFunctionCall(out FunctionCall? functionCall))
        {
            error ??= new WillBeCompilerException($"Function {call.ToReadable(FindStatementType)} not found");
            return false;
        }

        return GetFunction(
            functionCall.Identifier.Content,
            functionCall.MethodParameters,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

    protected bool GetFunction(
        FunctionCall functionCallStatement,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction(
            functionCallStatement.Identifier.Content,
            functionCallStatement.MethodParameters,
            out result,
            out typeArguments,
            addCompilable,
            out error);

    protected bool GetFunction(
        string identifier,
        StatementWithValue[] arguments,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        GeneralType[] argumentTypes;

        if (GetFunction(identifier, Enumerable.Repeat<GeneralType?>(null, arguments.Length), out CompiledFunction? possibleFunction, out _, false, out _))
        { argumentTypes = FindStatementTypes(arguments, possibleFunction.ParameterTypes); }
        else
        { argumentTypes = FindStatementTypes(arguments); }

        return GetFunction(identifier, argumentTypes, out result, out typeArguments, addCompilable, out error);
    }

    protected bool GetFunction(
        string identifier,
        [NotNullWhen(true)] out CompiledFunction? compiledFunction,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction(CompiledFunctions, identifier, out compiledFunction, out error);

    enum Perfectus
    {
        None,
        Context,
        Identifier,
        ParameterCount,
        ParameterTypes,
    }

    protected bool GetFunction(
        string identifier,
        IEnumerable<GeneralType?> arguments,
        [NotNullWhen(true)] out CompiledFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction<CompiledFunction, Token, string>(
            CompiledFunctions,
            compilableFunctions,
            "function",
            null,

            identifier,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);

    protected bool GetOperator(
        string identifier,
        IEnumerable<GeneralType?> arguments,
        [NotNullWhen(true)] out CompiledOperator? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction<CompiledOperator, Token, string>(
            CompiledOperators,
            compilableOperators,
            "operator",
            null,

            identifier,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);

    protected bool GetFunction<TFunction, TDefinedIdentifier, TPassedIdentifier>(
        IEnumerable<TFunction> compiledFunctions,
        IEnumerable<CompliableTemplate<TFunction>> compilableFunctions,
        string kindName,
        string? readableName,

        TPassedIdentifier? identifier,
        IEnumerable<GeneralType?> arguments,
        [NotNullWhen(true)] out TFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
        where TFunction : ICompiledFunction, ITemplateable<TFunction>, ISimpleReadable, IIdentifiable<TDefinedIdentifier>
        where TDefinedIdentifier : notnull, IEquatable<TPassedIdentifier>
    {
        result = default;
        typeArguments = null;
        error = null;
        readableName ??= CompiledFunction.ToReadable(identifier?.ToString() ?? "?", arguments);

        string kindNameLower = kindName.ToLowerInvariant();
        string kindNameCapital = char.ToUpperInvariant(kindName[0]) + kindName[1..];

        Perfectus perfectus = Perfectus.None;
        int passedArgumentCount = arguments.Count();

        foreach (TFunction function in compiledFunctions)
        {
            if (function.IsTemplate) continue;
            if (identifier is not null &&
                !function.Identifier.Equals(identifier))
            { continue; }

            if (function.ParameterTypes.Count != passedArgumentCount)
            {
                if (perfectus == Perfectus.None)
                {
                    error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: wrong number of parameters passed to {function.ToReadable()}");
                    result = function;
                }
                continue;
            }

            if (!arguments.ContainsNull(out IEnumerable<GeneralType>? _parameters) &&
                !GeneralType.AreEquals(function.ParameterTypes, _parameters))
            {
                if (perfectus == Perfectus.ParameterCount)
                {
                    error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: parameter types doesn't match with {function.ToReadable()}");
                    result = function;
                }
                continue;
            }

            if (result is not null && error is null)
            {
                error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: multiple {kindNameLower}s matched");
                return false;
            }

            result = function;
            error = null;
            perfectus = Perfectus.ParameterTypes;
        }

        foreach (CompliableTemplate<TFunction> function in compilableFunctions)
        {
            if (identifier is not null &&
                !function.Function.Identifier.Equals(identifier))
            { continue; }

            if (function.Function.ParameterTypes.Count != passedArgumentCount)
            {
                if (perfectus == Perfectus.None)
                {
                    error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: wrong number of parameters passed to {function.Function.ToReadable()}");
                    result = function.Function;
                }
                continue;
            }

            if (!arguments.ContainsNull(out IEnumerable<GeneralType>? _parameters) &&
                !GeneralType.AreEquals(function.Function.ParameterTypes, _parameters))
            {
                if (perfectus == Perfectus.ParameterCount)
                {
                    error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: parameter types doesn not match with ({function.Function.ToReadable()})");
                    result = function.Function;
                }
                continue;
            }

            if (result is not null && error is null)
            {
                error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: multiple {kindNameLower}s matched");
                return false;
            }

            result = function.Function;
            error = null;
            perfectus = Perfectus.ParameterTypes;
        }

        if (perfectus == Perfectus.ParameterTypes)
        { goto Finish; }

        foreach (TFunction function in compiledFunctions)
        {
            if (!function.IsTemplate) continue;
            if (identifier is not null &&
                !function.Identifier.Equals(identifier))
            { continue; }

            if (function.ParameterTypes.Count != passedArgumentCount)
            {
                if (perfectus == Perfectus.None)
                {
                    error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: wrong number of parameters passed to {function.ToReadable()}");
                    result = function;
                }
                continue;
            }

            if (arguments.ContainsNull(out IEnumerable<GeneralType>? _parameters))
            { continue; }

            Dictionary<string, GeneralType> _typeArguments = new();

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, _parameters, _typeArguments))
            { continue; }

            if (result is not null && error is null)
            {
                error = new WillBeCompilerException($"{kindNameCapital} {readableName} not found: multiple {kindNameLower}s matched");
                return false;
            }

            result = function;
            typeArguments = _typeArguments;
            perfectus = Perfectus.ParameterTypes;
        }

        Finish:

        if (result is not null && perfectus == Perfectus.ParameterTypes)
        {
            if (typeArguments is not null)
            {
                CompliableTemplate<TFunction> template = new(result, typeArguments);
                if (addCompilable)
                { AddCompilable(template); }
                result = template.Function;
            }
            return true;
        }

        error ??= new WillBeCompilerException($"{kindNameCapital} {readableName} not found");
        return false;
    }

    public static bool GetFunction(
        IEnumerable<CompiledFunction> compiledFunctions,
        string identifier,
        [NotNullWhen(true)] out CompiledFunction? compiledFunction,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        foreach (CompiledFunction function in compiledFunctions)
        {
            if (function.Identifier.Content != identifier) continue;

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Function \"{identifier}\" not found: multiple functions matched");
                return false;
            }

            compiledFunction = function;
        }

        if (compiledFunction is not null)
        { return true; }

        error ??= new WillBeCompilerException($"Function \"{identifier}\" not found");
        return false;
    }

    protected bool GetFunction(
        string identifier,
        FunctionType type,
        [NotNullWhen(true)] out CompiledFunction? compiledFunction,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (!function.Identifier.Equals(identifier)) continue;

            if (!type.ReturnType.Equals(function.Type))
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, type)} not found: return types doesn't match ({type.ReturnType} and {function.Type})");
                continue;
            }

            if (!GeneralType.AreEquals(function.ParameterTypes, type.Parameters))
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, type)} not found: parameter types doesn't match");
                continue;
            }

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, type)} not found: multiple functions matched");
                return false;
            }

            compiledFunction = function;
        }

        if (compiledFunction is not null)
        { return true; }

        error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, type)} not found");
        return false;
    }

    #endregion

    #region GetOperator

    /// <exception cref="CompilerException"/>
    protected bool GetOperator(
        BinaryOperatorCall @operator,
        [NotNullWhen(true)] out CompiledOperator? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        GeneralType[] arguments = FindStatementTypes(@operator.Parameters);
        return GetOperator(
            @operator.Operator.Content,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

    /// <exception cref="CompilerException"/>
    protected bool GetOperator(
        UnaryOperatorCall @operator,
        [NotNullWhen(true)] out CompiledOperator? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        GeneralType[] arguments = FindStatementTypes(@operator.Parameters);
        return GetOperator(
            @operator.Operator.Content,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

    #endregion

    static bool ContextIs(CompiledGeneralFunction function, GeneralType type) =>
        type is StructType structType &&
        function.Context is not null &&
        function.Context == structType.Struct;

    protected bool GetGeneralFunction(
        GeneralType context,
        GeneralType[] arguments,
        string identifier,
        [NotNullWhen(true)] out CompiledGeneralFunction? result,
        [MaybeNullWhen(false)] out Dictionary<string, GeneralType>? typeArguments,
        bool addCompilable,
        [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        IEnumerable<CompiledGeneralFunction> compiledGeneralFunctionsInContext =
            CompiledGeneralFunctions
            .Where(v => ContextIs(v, context));

        IEnumerable<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctionsInContext =
            compilableGeneralFunctions
            .Where(v => ContextIs(v.Function, context));

        return GetFunction<CompiledGeneralFunction, Token, string>(
            compiledGeneralFunctionsInContext,
            compilableGeneralFunctionsInContext,
            "general function",
            null,

            identifier,
            arguments,
            out result,
            out typeArguments,
            addCompilable,
            out error);
    }

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
        { throw new CompilerException($"Constant value must have initial value", variableDeclaration, variableDeclaration.FilePath); }

        if (!TryCompute(variableDeclaration.InitialValue, out DataItem constantValue))
        { throw new CompilerException($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue, variableDeclaration.FilePath); }

        if (variableDeclaration.Type != StatementKeywords.Var)
        {
            GeneralType constantType = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);
            variableDeclaration.Type.SetAnalyzedType(constantType);

            if (constantType is not BuiltinType builtinType)
            { throw new NotSupportedException($"Only builtin types supported as a constant value", variableDeclaration.Type, variableDeclaration.FilePath); }

            DataItem.TryCast(ref constantValue, builtinType.RuntimeType);
        }

        if (GetConstant(variableDeclaration.Identifier.Content, out _))
        { throw new CompilerException($"Constant \"{variableDeclaration.Identifier}\" already defined", variableDeclaration.Identifier, variableDeclaration.FilePath); }

        return new(constantValue, variableDeclaration);
    }

    #endregion

    #region GetStruct()

    protected bool GetStruct(string structName, [NotNullWhen(true)] out CompiledStruct? result, [NotNullWhen(false)] out WillBeCompilerException? error)
        => CodeGenerator.GetStruct(CompiledStructs, structName, out result, out error);

    public static bool GetStruct(IEnumerable<CompiledStruct> structs, string structName, [NotNullWhen(true)] out CompiledStruct? result, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        result = null;
        error = null;

        foreach (CompiledStruct @struct in structs)
        {
            if (@struct.Identifier.Content != structName)
            { continue; }

            if (result is not null)
            {
                error = new WillBeCompilerException($"Struct \"{structName}\" not found: multiple structs matched");
                return false;
            }

            result = @struct;
            return true;
        }

        error = new WillBeCompilerException($"Struct \"{structName}\" not found");
        return false;
    }

    #endregion

    #region FindType()

    protected bool FindType(Token name, [NotNullWhen(true)] out GeneralType? result)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            return true;
        }

        if (GetStruct(name.Content, out CompiledStruct? @struct, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.Struct;
            @struct.References.Add(new Reference<TypeInstance>(new TypeInstanceSimple(name), CurrentFile));

            result = new StructType(@struct);
            return true;
        }

        if (GetEnum(name.Content, out CompiledEnum? @enum))
        {
            name.AnalyzedType = TokenAnalyzedType.Enum;
            result = new EnumType(@enum);
            return true;
        }

        if (TypeArguments.TryGetValue(name.Content, out GeneralType? typeArgument))
        {
            result = typeArgument;
            return true;
        }

        if (GetFunction(name.Content, out CompiledFunction? function, out _))
        {
            name.AnalyzedType = TokenAnalyzedType.FunctionName;
            function.References.Add(new Reference<StatementWithValue>(new Identifier(name, null), CurrentFile));
            result = new FunctionType(function);
            return true;
        }

        if (GetGlobalVariable(name.Content, out CompiledVariable? globalVariable))
        {
            result = globalVariable.Type;
            return true;
        }

        result = null;
        return false;
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="CompilerException"/>
    protected GeneralType FindType(TypeInstance name)
        => GeneralType.From(name, FindType, TryCompute);

    #endregion

    #region Memory Helpers

    protected virtual void StackStore(ValueAddress address, int size)
    {
        for (int i = size - 1; i >= 0; i--)
        { StackStore(address + i); }
    }
    protected virtual void StackLoad(ValueAddress address, int size)
    {
        for (int currentOffset = 0; currentOffset < size; currentOffset++)
        { StackLoad(address + currentOffset); }
    }

    protected abstract void StackLoad(ValueAddress address);
    protected abstract void StackStore(ValueAddress address);

    #endregion

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

    protected bool GetGlobalVariable(string variableName, [NotNullWhen(true)] out CompiledVariable? compiledVariable)
    {
        foreach (CompiledVariable compiledVariable_ in CompiledGlobalVariables)
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

        if (destination is EnumType destEnumType)
        { if (destEnumType.Enum.IsSameType(valueType)) return; }

        if (valueType is EnumType valEnumType)
        { if (valEnumType.Enum.IsSameType(destination)) return; }

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

        if (destination is EnumType destEnumType)
        { if (destEnumType.Enum.IsSameType(valueType)) return; }

        if (destination is PointerType)
        { return; }

        if (destination == BasicType.Byte &&
            value.Type == RuntimeType.Integer)
        { return; }

        throw new CompilerException($"Can not set a {valueType} type value to the {destination} type", valuePosition, CurrentFile);
    }

    #region Addressing Helpers

    /// <exception cref="NotImplementedException"/>
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

        if (GetGlobalVariable(variable.Content, out CompiledVariable? globalVariable))
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

    /// <exception cref="NotImplementedException"/>
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

    /// <exception cref="NotImplementedException"/>
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
    /// <exception cref="CompilerException"/>
    protected ValueAddress GetBaseAddress(Identifier variable)
    {
        if (GetConstant(variable.Content, out _))
        { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

        if (GetParameter(variable.Content, out CompiledParameter? parameter))
        { return GetBaseAddress(parameter); }

        if (GetVariable(variable.Content, out CompiledVariable? localVariable))
        { return new ValueAddress(localVariable); }

        if (GetGlobalVariable(variable.Content, out CompiledVariable? globalVariable))
        { return GetGlobalVariableAddress(globalVariable); }

        throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
    }
    /// <exception cref="NotImplementedException"/>
    protected ValueAddress GetBaseAddress(Field statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement) is PointerType;
        return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
    }
    /// <exception cref="NotImplementedException"/>
    protected ValueAddress GetBaseAddress(IndexCall statement)
    {
        ValueAddress address = GetBaseAddress(statement.PrevStatement);
        bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement) is PointerType;
        return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
    }

    /// <exception cref="NotImplementedException"/>
    protected bool IsItInHeap(StatementWithValue value) => value switch
    {
        Identifier => false,
        Field field => IsItInHeap(field),
        IndexCall indexCall => IsItInHeap(indexCall),
        _ => throw new NotImplementedException()
    };

    /// <exception cref="NotImplementedException"/>
    protected bool IsItInHeap(IndexCall indexCall)
        => IsItInHeap(indexCall.PrevStatement) || FindStatementType(indexCall.PrevStatement) is PointerType;

    /// <exception cref="NotImplementedException"/>
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
            { throw new CompilerException($"Initial value for variable declaration with implicit type is required", newVariable, newVariable.FilePath); }

            type = FindStatementType(newVariable.InitialValue);
        }
        else
        {
            type = GeneralType.From(newVariable.Type, FindType, TryCompute);

            newVariable.Type.SetAnalyzedType(type);
            newVariable.CompiledType = type;
        }

        if (!type.AllGenericsDefined())
        { throw new InternalException($"Failed to qualify all generics in variable \"{newVariable.Identifier}\" type \"{type}\"", newVariable.FilePath); }

        return new CompiledVariable(memoryOffset, type, newVariable);
    }

    #region GetInitialValue()

    /// <exception cref="NotImplementedException"></exception>
    protected static DataItem GetInitialValue(BasicType type) => type switch
    {
        BasicType.Byte => new DataItem((byte)0),
        BasicType.Integer => new DataItem((int)0),
        BasicType.Float => new DataItem((float)0f),
        BasicType.Char => new DataItem((char)'\0'),

        _ => throw new NotImplementedException($"Initial value for type \"{type}\" isn't implemented"),
    };

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
    protected static DataItem GetInitialValue(TypeInstance type) => type.ToString() switch
    {
        TypeKeywords.Int => new DataItem((int)0),
        TypeKeywords.Byte => new DataItem((byte)0),
        TypeKeywords.Float => new DataItem((float)0f),
        TypeKeywords.Char => new DataItem((char)'\0'),
        StatementKeywords.Var => throw new InternalException("Undefined type"),
        TypeKeywords.Void => throw new InternalException("Invalid type"),
        _ => throw new InternalException($"Initial value for type \"{type}\" is unimplemented"),
    };

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="CompilerException"/>
    /// <exception cref="InternalException"/>
    protected static bool GetInitialValue(TypeInstance type, out DataItem value)
    {
        switch (type.ToString())
        {
            case TypeKeywords.Int:
            {
                value = new DataItem((int)0);
                return true;
            }
            case TypeKeywords.Byte:
            {
                value = new DataItem((byte)0);
                return true;
            }
            case TypeKeywords.Float:
            {
                value = new DataItem((float)0f);
                return true;
            }
            case TypeKeywords.Char:
            {
                value = new DataItem((char)'\0');
                return true;
            }
            default:
            {
                value = default;
                return false;
            }
        };
    }

    /// <exception cref="NotImplementedException"/>
    /// <exception cref="InternalException"/>
    protected static DataItem GetInitialValue(GeneralType type)
    {
        switch (type)
        {
            case GenericType:
                throw new NotImplementedException($"Initial value for type arguments is bruh moment");
            case StructType:
                throw new NotImplementedException($"Initial value for structs is not implemented");
            case EnumType enumType:
                if (enumType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{enumType.Enum.Identifier.Content}\" initial value: enum has no members", enumType.Enum.Identifier, enumType.Enum.FilePath); }
                return enumType.Enum.Members[0].ComputedValue;
            case FunctionType:
                return new DataItem(int.MaxValue);
            case BuiltinType builtinType:
                return GetInitialValue(builtinType.Type);
            case PointerType:
                return new DataItem(0);
            default:
                throw new NotImplementedException();
        }
    }

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

        if (!GetIndexGetter(prevType, out CompiledFunction? indexer, out _, false, out WillBeCompilerException? notFoundException))
        { }

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

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction, out _, false, out WillBeCompilerException? notFoundError))
        { throw notFoundError.Instantiate(functionCall, CurrentFile); }

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
        return OnGotStatementType(functionCall, compiledFunction.Type);
    }

    protected GeneralType FindStatementType(BinaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != BinaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {BinaryOperatorCall.ParameterCount}", @operator.Operator, CurrentFile); }
        }
        else
        { opcode = Opcode._; }

        if (opcode == Opcode._)
        { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }

        if (GetOperator(@operator, out CompiledOperator? operatorDefinition, out _, false, out _))
        {
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, operatorDefinition.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left);
        GeneralType rightType = FindStatementType(@operator.Right);

        if (!leftType.CanBeBuiltin || !rightType.CanBeBuiltin || leftType == BasicType.Void || rightType == BasicType.Void)
        { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile); }

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
            if (CanConvertImplicitly(result, expectedType))
            { return OnGotStatementType(@operator, expectedType); }

            if (result == BasicType.Integer &&
                expectedType is PointerType)
            { return OnGotStatementType(@operator, expectedType); }
        }

        return OnGotStatementType(@operator, result);
    }
    protected GeneralType FindStatementType(UnaryOperatorCall @operator, GeneralType? expectedType)
    {
        if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
        {
            if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != UnaryOperatorCall.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {UnaryOperatorCall.ParameterCount}", @operator.Operator, CurrentFile); }
        }
        else
        { opcode = Opcode._; }

        if (opcode == Opcode._)
        { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }

        if (GetOperator(@operator, out CompiledOperator? operatorDefinition, out _, false, out _))
        {
            @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(@operator, operatorDefinition.Type);
        }

        GeneralType leftType = FindStatementType(@operator.Left);

        if (!leftType.CanBeBuiltin || leftType == BasicType.Void)
        { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content}", @operator.Operator, CurrentFile); }

        DataItem leftValue = GetInitialValue(leftType);

        DataItem predictedValue = @operator.Operator.Content switch
        {
            "!" => !leftValue,

            _ => throw new NotImplementedException($"Unknown operator \"{@operator}\""),
        };

        BuiltinType result = new(predictedValue.Type);

        if (expectedType is not null)
        {
            if (CanConvertImplicitly(result, expectedType))
            { return OnGotStatementType(@operator, expectedType); }

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
        if (identifier.Content == "nullptr")
        { return new BuiltinType(BasicType.Integer); }

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
            else if (GetGlobalVariable(identifier.Content, out CompiledVariable? globalVariable))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.VariableName;
                identifier.Reference = globalVariable;
            }

            return OnGotStatementType(identifier, type);
        }

        if (GetEnum(identifier.Content, out CompiledEnum? @enum))
        {
            identifier.Token.AnalyzedType = TokenAnalyzedType.Enum;
            return OnGotStatementType(identifier, new EnumType(@enum));
        }

        if (GetFunction(identifier.Token.Content, expectedType, out CompiledFunction? function, out _))
        {
            identifier.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(identifier, new FunctionType(function));
        }

        for (int i = CurrentEvaluationContext.Count - 1; i >= 0; i--)
        {
            if (CurrentEvaluationContext[i].TryGetType(identifier, out GeneralType? _type))
            { return _type; }
        }

        if (FindType(identifier.Token, out GeneralType? result))
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
        GeneralType[] parameters = FindStatementTypes(constructorCall.Parameters);

        if (GetConstructor(type, parameters, out CompiledConstructor? constructor, out _, false, out WillBeCompilerException? notFound))
        {
            constructorCall.Type.SetAnalyzedType(constructor.Type);
            return OnGotStatementType(constructorCall, constructor.Type);
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

        if (prevStatementType is EnumType enumType)
        {
            foreach (CompiledEnumMember enumMember in enumType.Enum.Members)
            {
                if (enumMember.Identifier.Content != field.Identifier.Content) continue;
                field.Identifier.AnalyzedType = TokenAnalyzedType.EnumMember;
                return OnGotStatementType(field, new BuiltinType(enumMember.ComputedValue.Type));
            }

            throw new CompilerException($"Enum member \"{enumType}\" not found in enum \"{enumType.Enum.Identifier.Content}\"", field.Identifier, CurrentFile);
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

    protected IEnumerable<GeneralType> FindStatementTypes(IEnumerable<StatementWithValue> statements)
    {
        return statements.Select<StatementWithValue, GeneralType>(FindStatementType);
    }

    protected GeneralType[] FindStatementTypes(ImmutableArray<StatementWithValue> statements)
    {
        GeneralType[] result = new GeneralType[statements.Length];
        for (int i = 0; i < statements.Length; i++)
        { result[i] = FindStatementType(statements[i]); }
        return result;
    }

    protected GeneralType[] FindStatementTypes(StatementWithValue[] statements)
    {
        GeneralType[] result = new GeneralType[statements.Length];
        for (int i = 0; i < statements.Length; i++)
        { result[i] = FindStatementType(statements[i]); }
        return result;
    }

    protected GeneralType[] FindStatementTypes(IEnumerable<StatementWithValue> statements, IEnumerable<GeneralType> expectedTypes)
        => FindStatementTypes(statements.ToArray(), expectedTypes.ToArray());
    protected GeneralType[] FindStatementTypes(StatementWithValue[] statements, GeneralType[] expectedTypes)
    {
        GeneralType[] result = new GeneralType[statements.Length];
        for (int i = 0; i < statements.Length; i++)
        {
            GeneralType? expectedType = null;
            if (i < expectedTypes.Length) expectedType = expectedTypes[i];
            result[i] = FindStatementType(statements[i], expectedType);
        }
        return result;
    }

    #endregion

    #region InlineMacro()

    protected static bool InlineMacro(FunctionThingDefinition function, [NotNullWhen(true)] out Statement? inlined, params StatementWithValue[] parameters)
    {
        Dictionary<string, StatementWithValue> _parameters = Utils.Map(
            function.Parameters.ToArray(),
            parameters,
            (key, value) => (key.Identifier.Content, value));

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

    static IEnumerable<StatementWithValue> InlineMacro(IEnumerable<StatementWithValue> statements, Dictionary<string, StatementWithValue> parameters)
    { return statements.Select(statement => InlineMacro(statement, parameters)); }

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
        BaseBranch[] branches = new BaseBranch[statement.Parts.Length];
        for (int i = 0; i < branches.Length; i++)
        {
            if (!InlineMacro(statement.Parts[i], parameters, out BaseBranch? inlinedBranch))
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
            file: statement.FilePath)
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
        IfContainer v => FindControlFlowUsage(v.Parts, true),
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

    /// <exception cref="NotImplementedException"/>
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

        if (GetOperator(@operator, out CompiledOperator? compiledOperator, out _, false, out _))
        {
            if (TryCompute(@operator.Parameters, context, out DataItem[]? parameterValues) &&
                TryEvaluate(compiledOperator, parameterValues, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
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

        if (GetOperator(@operator, out CompiledOperator? compiledOperator, out _, false, out _))
        {
            if (TryCompute(@operator.Parameters, context, out DataItem[]? parameterValues) &&
                TryEvaluate(compiledOperator, parameterValues, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
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

        if (GetFunction(functionCall, out CompiledFunction? function, out _, false, out _))
        {
            if (!function.CanUse(CurrentFile))
            {
                value = default;
                return false;
            }

            if (function.IsExternal &&
                !functionCall.SaveValue &&
                TryCompute(functionCall.MethodParameters, context, out DataItem[]? parameters))
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

            if (TryEvaluate(function, convertedParameters, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
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
    bool TryCompute(StatementWithValue[]? statements, EvaluationContext context, [NotNullWhen(true)] out DataItem[]? values)
    {
        if (statements is null)
        {
            values = null;
            return false;
        }

        values = new DataItem[statements.Length];

        for (int i = 0; i < statements.Length; i++)
        {
            StatementWithValue statement = statements[i];

            if (!TryCompute(statement, context, out DataItem value))
            {
                values = null;
                return false;
            }

            values[i] = value;
        }

        return true;
    }
    protected bool TryCompute(StatementWithValue[]? statements, [NotNullWhen(true)] out DataItem[]? values)
        => TryCompute(statements, EvaluationContext.Empty, out values);

    protected bool TryEvaluate(CompiledFunction function, StatementWithValue[] parameters, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
    {
        value = default;
        runtimeStatements = default;

        if (TryCompute(parameters, EvaluationContext.Empty, out DataItem[]? parameterValues) &&
            TryEvaluate(function, parameterValues, out value, out runtimeStatements))
        { return true; }

        if (!InlineMacro(function, out Statement? inlined, parameters))
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
    bool TryEvaluate(ICompiledFunction function, DataItem[] parameterValues, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
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
        CurrentFile = (function as IInFile)?.FilePath;

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
        foreach (BaseBranch _branch in ifContainer.Parts)
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
            if (!GetInitialValue(variableDeclaration.Type, out value))
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
    bool TryEvaluate(IEnumerable<Statement> statements, EvaluationContext context)
    {
        foreach (Statement statement in statements)
        {
            if (!TryEvaluate(statement, context))
            { return false; }
            if (context.IsReturning)
            { break; }
            if (context.IsBreaking)
            { break; }
        }
        return true;
    }

    protected bool TryCompute([NotNullWhen(true)] StatementWithValue? statement, out DataItem value)
        => TryCompute(statement, EvaluationContext.Empty, out value);

    readonly Stack<EvaluationContext> CurrentEvaluationContext = new();

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

    protected Block[] Unroll(ForLoop loop, Dictionary<StatementWithValue, DataItem> values)
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

        List<Block> statements = new();

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

        return statements.ToArray();
    }

    protected static bool CanConvertImplicitly(GeneralType? from, GeneralType? to)
    {
        if (from is null || to is null) return false;

        if (to is EnumType enumType && enumType.Enum.Type == from)
        { return true; }

        return false;
    }

    protected static bool TryConvertType(ref GeneralType? type, GeneralType? targetType)
    {
        if (type is null || targetType is null) return false;

        if (targetType is EnumType enumType && enumType.Enum.Type == type)
        {
            type = targetType;
            return true;
        }

        return false;
    }
}
