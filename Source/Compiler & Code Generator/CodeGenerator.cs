namespace LanguageCore.Compiler;

using BBCode.Generator;
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

            foreach (KeyValuePair<string, GeneralType> pair in TypeArguments)
            {
                if (pair.Value is GenericType)
                { throw new InternalException($"{pair.Value} is generic"); }
            }

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

    protected delegate void BuiltinFunctionCompiler(params StatementWithValue[] parameters);

    protected ImmutableArray<CompiledStruct> CompiledStructs;
    protected ImmutableArray<CompiledFunction> CompiledFunctions;
    protected ImmutableArray<MacroDefinition> CompiledMacros;
    protected ImmutableArray<CompiledOperator> CompiledOperators;
    protected ImmutableArray<CompiledConstructor> CompiledConstructors;
    protected ImmutableArray<CompiledGeneralFunction> CompiledGeneralFunctions;
    protected ImmutableArray<CompiledEnum> CompiledEnums;

    protected readonly Stack<IConstant> CompiledConstants;
    protected readonly Stack<int> ConstantsStack;

    protected readonly List<CompiledParameter> CompiledParameters;
    protected readonly List<CompiledVariable> CompiledVariables;
    protected readonly List<CompiledVariable> CompiledGlobalVariables;

    protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
    protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;
    protected IReadOnlyList<CompliableTemplate<CompiledConstructor>> CompilableConstructors => compilableConstructors;

    readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
    readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
    readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();
    readonly List<CompliableTemplate<CompiledConstructor>> compilableConstructors = new();

    protected Uri? CurrentFile;
    protected bool InFunction;
    protected readonly Stack<bool> InMacro;
    protected readonly Dictionary<string, GeneralType> TypeArguments;
    protected DebugInformation? DebugInfo;

    protected readonly GeneratorSettings Settings;
    protected readonly PrintCallback? Print;
    protected readonly AnalysisCollection? AnalysisCollection;

    protected CodeGenerator()
    {
        CompiledStructs = ImmutableArray.Create<CompiledStruct>();
        CompiledFunctions = ImmutableArray.Create<CompiledFunction>();
        CompiledMacros = ImmutableArray.Create<MacroDefinition>();
        CompiledOperators = ImmutableArray.Create<CompiledOperator>();
        CompiledGeneralFunctions = ImmutableArray.Create<CompiledGeneralFunction>();
        CompiledConstructors = ImmutableArray.Create<CompiledConstructor>();
        CompiledEnums = ImmutableArray.Create<CompiledEnum>();

        CompiledConstants = new Stack<IConstant>();
        ConstantsStack = new Stack<int>();

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

        Settings = GeneratorSettings.Default;
        Print = null;
        AnalysisCollection = null;
    }

    protected CodeGenerator(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : this()
    {
        CompiledStructs = compilerResult.Structs.ToImmutableArray();
        CompiledFunctions = compilerResult.Functions.ToImmutableArray();
        CompiledMacros = compilerResult.Macros.ToImmutableArray();
        CompiledOperators = compilerResult.Operators.ToImmutableArray();
        CompiledConstructors = compilerResult.Constructors.ToImmutableArray();
        CompiledGeneralFunctions = compilerResult.GeneralFunctions.ToImmutableArray();
        CompiledEnums = compilerResult.Enums.ToImmutableArray();

        AnalysisCollection = analysisCollection;

        Settings = settings;

        Print = print;
    }

    #region Helper Functions

    protected int CompileConstants(IEnumerable<Statement> statements)
    {
        int count = 0;
        foreach (Statement statement in statements)
        {
            if (statement is not VariableDeclaration variableDeclaration ||
                !variableDeclaration.Modifiers.Contains(ModifierKeywords.Const))
            { continue; }
            CompileConstant(variableDeclaration);
            count++;
        }
        ConstantsStack.Push(count);
        return count;
    }

    protected int CompileConstants(IEnumerable<VariableDeclaration> statements)
    {
        int count = 0;
        foreach (VariableDeclaration statement in statements)
        {
            if (!statement.Modifiers.Contains(ModifierKeywords.Const))
            { continue; }
            CompileConstant(statement);
            count++;
        }
        ConstantsStack.Push(count);
        return count;
    }

    protected void CompileConstant(VariableDeclaration variableDeclaration)
    {
        if (variableDeclaration.InitialValue == null)
        { throw new CompilerException($"Constant value must have initial value", variableDeclaration, variableDeclaration.FilePath); }

        if (!TryCompute(variableDeclaration.InitialValue, out DataItem constantValue))
        { throw new CompilerException($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue, variableDeclaration.FilePath); }

        if (variableDeclaration.Type != StatementKeywords.Var)
        {
            GeneralType constantType = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);
            variableDeclaration.Type.SetAnalyzedType(constantType);

            if (constantType is not BuiltinType builtinType)
            { throw new NotSupportedException($"Only builtin types supported as a constant value", variableDeclaration.Type, CurrentFile); }

            DataItem.TryCast(ref constantValue, builtinType.RuntimeType);
        }

        if (GetConstant(variableDeclaration.Identifier.Content, out _))
        { throw new CompilerException($"Constant \"{variableDeclaration.Identifier}\" already defined", variableDeclaration.Identifier, variableDeclaration.FilePath); }

        CompiledConstants.Push(new CompiledVariableConstant(constantValue, variableDeclaration));
    }

    protected void CleanupConstants()
    {
        int count = ConstantsStack.Pop();
        CompiledConstants.Pop(count);
    }

    protected bool GetConstant(string identifier, [NotNullWhen(true)] out IConstant? constant)
    {
        constant = null;

        foreach (IConstant _constant in CompiledConstants)
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

    protected CompliableTemplate<CompiledFunction> AddCompilable(CompliableTemplate<CompiledFunction> compilable)
    {
        for (int i = 0; i < compilableFunctions.Count; i++)
        {
            if (compilableFunctions[i].Function.IsSame(compilable.Function))
            { return compilableFunctions[i]; }
        }
        compilableFunctions.Add(compilable);
        return compilable;
    }

    protected CompliableTemplate<CompiledOperator> AddCompilable(CompliableTemplate<CompiledOperator> compilable)
    {
        for (int i = 0; i < compilableOperators.Count; i++)
        {
            if (compilableOperators[i].Function.IsSame(compilable.Function))
            { return compilableOperators[i]; }
        }
        compilableOperators.Add(compilable);
        return compilable;
    }

    protected CompliableTemplate<CompiledGeneralFunction> AddCompilable(CompliableTemplate<CompiledGeneralFunction> compilable)
    {
        for (int i = 0; i < compilableGeneralFunctions.Count; i++)
        {
            if (compilableGeneralFunctions[i].Function.IsSame(compilable.Function))
            { return compilableGeneralFunctions[i]; }
        }
        compilableGeneralFunctions.Add(compilable);
        return compilable;
    }

    protected CompliableTemplate<CompiledConstructor> AddCompilable(CompliableTemplate<CompiledConstructor> compilable)
    {
        for (int i = 0; i < compilableConstructors.Count; i++)
        {
            if (compilableConstructors[i].Function.IsSame(compilable.Function))
            { return compilableConstructors[i]; }
        }
        compilableConstructors.Add(compilable);
        return compilable;
    }

    #endregion

    protected void SetTypeArguments(Dictionary<string, GeneralType> typeArguments)
    {
        TypeArguments.Clear();
        foreach (KeyValuePair<string, GeneralType> typeArgument in typeArguments)
        { TypeArguments.Add(typeArgument.Key, typeArgument.Value); }
    }

    protected void SetTypeArguments(Dictionary<string, GeneralType> typeArguments, out Dictionary<string, GeneralType> replaced)
    {
        replaced = new Dictionary<string, GeneralType>(TypeArguments);
        TypeArguments.Clear();
        foreach (KeyValuePair<string, GeneralType> typeArgument in typeArguments)
        { TypeArguments.Add(typeArgument.Key, typeArgument.Value); }
    }

    public static bool SameType(CompiledEnum @enum, GeneralType type)
    {
        if (type is not BuiltinType builtinType) return false;
        RuntimeType runtimeType;
        try
        { runtimeType = builtinType.RuntimeType; }
        catch (NotImplementedException)
        { return false; }

        for (int i = 0; i < @enum.Members.Length; i++)
        {
            if (@enum.Members[i].ComputedValue.Type != runtimeType)
            { return false; }
        }

        return true;
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

    protected bool GetConstructor(GeneralType type, GeneralType[] parameters, [NotNullWhen(true)] out CompiledConstructor? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        {
            List<GeneralType> _parameters = new();
            _parameters.Add(type);
            _parameters.AddRange(parameters);
            parameters = _parameters.ToArray();
        }

        foreach (CompiledConstructor function in CompiledConstructors)
        {
            if (function.IsTemplate) continue;
            if (function.Type != type) continue;

            if (!GeneralType.AreEquals(function.ParameterTypes, parameters))
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: parameter types doesn't match with {function}");
                continue;
            }

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: multiple constructors matched");
                return false;
            }

            compiledFunction = function;
        }

        foreach (CompliableTemplate<CompiledConstructor> function in compilableConstructors)
        {
            if (function.Function.Type != type) continue;

            if (!GeneralType.AreEquals(function.Function.ParameterTypes, parameters))
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: parameter types doesn't match with {function}");
                continue;
            }

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: multiple constructors matched");
                return false;
            }

            compiledFunction = function.Function;
        }

        if (compiledFunction is not null)
        { return true; }

        error ??= new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found");
        return false;
    }

    protected bool GetConstructorTemplate(GeneralType type, GeneralType[] parameters, out CompliableTemplate<CompiledConstructor> compiledConstructor, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        bool found = false;
        compiledConstructor = default;
        error = null;

        {
            List<GeneralType> _parameters = new();
            _parameters.Add(type);
            _parameters.AddRange(parameters);
            parameters = _parameters.ToArray();
        }

        foreach (CompiledConstructor constructor in CompiledConstructors)
        {
            if (!constructor.IsTemplate) continue;

            if (constructor.ParameterCount != parameters.Length)
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: parameter count doesn't match with {constructor.ParameterCount}");
                continue;
            }

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(constructor.ParameterTypes, parameters, typeArguments)) continue;

            compiledConstructor = new CompliableTemplate<CompiledConstructor>(constructor, typeArguments);

            if (found)
            {
                error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found: multiple constructors matched");
                return false;
            }

            found = true;
        }

        if (found)
        { return true; }

        error = new WillBeCompilerException($"Constructor {CompiledConstructor.ToReadable(type, parameters)} not found");
        return false;
    }

    protected bool GetIndexGetter(GeneralType prevType, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction(
            BuiltinFunctionNames.IndexerGet,
            new GeneralType[] { prevType, new BuiltinType(BasicType.Integer) },
            out compiledFunction,
            out error);

    protected bool GetIndexSetter(GeneralType prevType, GeneralType elementType, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction(
            BuiltinFunctionNames.IndexerSet,
            new GeneralType[] { prevType, new BuiltinType(BasicType.Integer), elementType },
            out compiledFunction,
            out error);

    protected bool GetIndexGetterTemplate(GeneralType prevType, out CompliableTemplate<CompiledFunction> compiledFunction)
        => GetFunctionTemplate(
            BuiltinFunctionNames.IndexerGet,
            new GeneralType[] { prevType, new BuiltinType(BasicType.Integer) },
            out compiledFunction);

    protected bool GetIndexSetterTemplate(GeneralType prevType, GeneralType elementType, out CompliableTemplate<CompiledFunction> compiledFunction)
    {
        if (prevType is StructType structType)
        {
            CompiledStruct context = structType.Struct;

            context.AddTypeArguments(structType.TypeParameters);
            context.AddTypeArguments(TypeArguments);

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[2] is not GenericType && function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != BasicType.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                Dictionary<string, GeneralType> typeParameters = new(context.CurrentTypeArguments);

                compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeParameters);
                context.ClearTypeArguments();
                return true;
            }

            compiledFunction = default;
            context.ClearTypeArguments();
            return false;
        }

        compiledFunction = default;
        return false;
    }

    protected bool TryGetBuiltinFunction(string builtinName, GeneralType[] parameters, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
    {
        compiledFunction = null;

        foreach (CompiledFunction function in this.CompiledFunctions)
        {
            if (function.IsTemplate) continue;
            if (function.BuiltinFunctionName != builtinName) continue;

            if (compiledFunction is not null)
            { return false; }

            compiledFunction = function;
        }

        foreach (CompiledFunction function in this.CompiledFunctions)
        {
            if (!function.IsTemplate) continue;
            if (function.BuiltinFunctionName != builtinName) continue;

            if (compiledFunction is not null)
            { return false; }

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

            compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeArguments).Function;
        }

        return compiledFunction is not null;
    }

    #region TryGetMacro()

    protected bool TryGetMacro(FunctionCall functionCallStatement, [NotNullWhen(true)] out MacroDefinition? macro)
        => TryGetMacro(functionCallStatement.Identifier.Content, functionCallStatement.MethodParameters.Length, out macro);

    protected bool TryGetMacro(string name, int parameterCount, [NotNullWhen(true)] out MacroDefinition? macro)
    {
        macro = null;

        foreach (MacroDefinition _macro in CompiledMacros)
        {
            if (_macro.Identifier.Content != name) continue;
            if (_macro.Parameters.Length != parameterCount) continue;

            if (macro is not null)
            { return false; }

            macro = _macro;
        }

        return macro is not null;
    }

    #endregion

    #region GetFunction()

    protected bool GetFunction(string identifier, GeneralType? type, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        if (type is null || type is not FunctionType functionType)
        { return GetFunction(identifier, out compiledFunction, out error); }
        return GetFunction(identifier, functionType, out compiledFunction, out error);
    }

    protected bool GetFunction(FunctionCall functionCallStatement, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        Token functionIdentifier = functionCallStatement.Identifier;
        StatementWithValue[] passedParameters = functionCallStatement.MethodParameters;

        bool res;
        if (GetFunction(functionIdentifier.Content, passedParameters.Length, out CompiledFunction? possibleFunction, out _))
        { res = GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters, possibleFunction.ParameterTypes), out compiledFunction, out error); }
        else
        { res = GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters), out compiledFunction, out error); }

        if (!res && compiledFunction is not null)
        {
            res = res || Utils.SequenceEquals(functionCallStatement.MethodParameters, compiledFunction.ParameterTypes, (passed, defined) =>
            {
                GeneralType passedType = FindStatementType(passed);

                if (passedType.Equals(defined))
                { return true; }

                if (defined is BuiltinType builtinType &&
                    TryCompute(passed, out DataItem passedValue) &&
                    DataItem.TryCast(ref passedValue, builtinType.RuntimeType))
                { return true; }

                return false;
            });
        }

        return res;
    }

    protected bool GetFunction(string identifier, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
        => GetFunction(CompiledFunctions, identifier, out compiledFunction, out error);

    protected bool GetFunctionTemplate(FunctionCall functionCallStatement, out CompliableTemplate<CompiledFunction> compiledFunction)
        => GetFunctionTemplate(functionCallStatement.Identifier.Content, FindStatementTypes(functionCallStatement.MethodParameters), out compiledFunction);

    protected bool GetFunctionTemplate(string identifier, GeneralType[] parameters, out CompliableTemplate<CompiledFunction> compiledFunction)
    {
        bool found = false;
        compiledFunction = default;

        foreach (CompiledFunction element in CompiledFunctions)
        {
            if (!element.IsTemplate) continue;
            if (!element.Identifier.Equals(identifier)) continue;

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(element.ParameterTypes, parameters, typeArguments)) continue;

            CompliableTemplate<CompiledFunction> compiledFunction_ = new(element, typeArguments);

            if (found)
            { throw new CompilerException($"Duplicated function definitions: {compiledFunction} and {compiledFunction_} are the same", element.Identifier, element.FilePath); }

            compiledFunction = compiledFunction_;
            found = true;
        }

        return found;
    }

    protected bool GetFunction(string identifier, GeneralType[] parameters, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;
        bool isPerfect = false;

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (function.IsTemplate) continue;
            if (function.Identifier.Content != identifier) continue;

            if (!GeneralType.AreEquals(function.ParameterTypes, parameters))
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: parameter types doesn not match with ({function.ToReadable()})");
                compiledFunction ??= function;
                continue;
            }

            if (compiledFunction is not null && error is null)
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: multiple functions matched");
                return false;
            }

            compiledFunction = function;
            error = null;
            isPerfect = true;
        }

        foreach (CompliableTemplate<CompiledFunction> function in compilableFunctions)
        {
            if (function.Function.Identifier.Content != identifier) continue;

            if (!GeneralType.AreEquals(function.Function.ParameterTypes, parameters))
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: parameter types doesn not match with ({function.Function.ToReadable()})");
                compiledFunction ??= function.Function;
                continue;
            }

            if (compiledFunction is not null && error is null)
            {
                error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: multiple functions matched");
                return false;
            }

            compiledFunction = function.Function;
            error = null;
            isPerfect = true;
        }

        if (compiledFunction is null)
        {
            parameters = GeneralType.InsertTypeParameters(parameters, TypeArguments).ToArray();

            foreach (CompiledFunction function in CompiledFunctions)
            {
                if (function.IsTemplate) continue;
                if (function.Identifier.Content != identifier) continue;

                if (!GeneralType.AreEquals(function.ParameterTypes, parameters))
                {
                    error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: parameter types doesn not match with ({function.ToReadable()})");
                    compiledFunction ??= function;
                    continue;
                }

                if (compiledFunction is not null && error is null)
                {
                    error = new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found: multiple functions matched");
                    return false;
                }

                compiledFunction = function;
                error = null;
                isPerfect = true;
            }
        }

        if (compiledFunction is not null && isPerfect)
        { return true; }

        error ??= new WillBeCompilerException($"Function {CompiledFunction.ToReadable(identifier, parameters)} not found");
        return false;
    }

    protected bool GetFunction(string identifier, int parameterCount, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (!function.Identifier.Equals(identifier)) continue;

            if (function.ParameterCount != parameterCount)
            {
                error = new WillBeCompilerException($"Function \"{identifier}\" (with {parameterCount} parameters) not found: parameter count doesn't match ({parameterCount} and {function.ParameterCount})");
                continue;
            }

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Function \"{identifier}\" (with {parameterCount} parameters) not found: multiple functions matched");
                return false;
            }

            compiledFunction = function;
        }

        if (compiledFunction is not null)
        { return true; }

        error ??= new WillBeCompilerException($"Function \"{identifier}\" (with {parameterCount} parameters) not found");
        return false;
    }

    protected bool GetFunction(FunctionType type, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        foreach (CompiledFunction function in CompiledFunctions)
        {
            if (!GeneralType.AreEquals(function.ParameterTypes, type.Parameters)) continue;
            if (!function.Type.Equals(type.ReturnType)) continue;

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Function {type} not found: multiple functions matched");
                return false;
            }

            compiledFunction = function;
        }

        foreach (CompliableTemplate<CompiledFunction> function in compilableFunctions)
        {
            if (!GeneralType.AreEquals(function.Function.ParameterTypes, type.Parameters)) continue;
            if (!function.Function.Type.Equals(type.ReturnType)) continue;

            if (compiledFunction is not null)
            {
                error = new WillBeCompilerException($"Function {type} not found: multiple functions matched");
                return false;
            }

            compiledFunction = function.Function;
        }

        if (compiledFunction is not null)
        { return true; }

        error ??= new WillBeCompilerException($"Function {type} not found");
        return false;
    }

    public static bool GetFunction(IEnumerable<CompiledFunction> compiledFunctions, string identifier, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
    {
        compiledFunction = null;
        error = null;

        foreach (CompiledFunction function in compiledFunctions)
        {
            if (!function.Identifier.Equals(identifier)) continue;

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

    protected bool GetFunction(string identifier, FunctionType type, [NotNullWhen(true)] out CompiledFunction? compiledFunction, [NotNullWhen(false)] out WillBeCompilerException? error)
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

    /// <exception cref="CompilerException"/>
    protected bool GetOperator(BinaryOperatorCall @operator, [NotNullWhen(true)] out CompiledOperator? compiledOperator)
    {
        GeneralType[] parameters = FindStatementTypes(@operator.Parameters);

        bool found = false;
        compiledOperator = null;

        foreach (CompiledOperator function in CompiledOperators)
        {
            if (function.IsTemplate) continue;
            if (function.Identifier.Content != @operator.Operator.Content) continue;
            if (!GeneralType.AreEquals(function.ParameterTypes, parameters)) continue;

            if (found)
            { throw new CompilerException($"Duplicated operator definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

            compiledOperator = function;
            found = true;
        }

        return found;
    }

    /// <exception cref="CompilerException"/>
    protected bool GetOperator(UnaryOperatorCall @operator, [NotNullWhen(true)] out CompiledOperator? compiledOperator)
    {
        GeneralType[] parameters = FindStatementTypes(@operator.Parameters);

        bool found = false;
        compiledOperator = null;

        foreach (CompiledOperator function in CompiledOperators)
        {
            if (function.IsTemplate) continue;
            if (function.Identifier.Content != @operator.Operator.Content) continue;
            if (!GeneralType.AreEquals(function.ParameterTypes, parameters)) continue;

            if (found)
            { throw new CompilerException($"Duplicated operator definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

            compiledOperator = function;
            found = true;
        }

        return found;
    }

    protected bool GetOperatorTemplate(BinaryOperatorCall @operator, out CompliableTemplate<CompiledOperator> compiledOperator)
    {
        GeneralType[] parameters = FindStatementTypes(@operator.Parameters);

        bool found = false;
        compiledOperator = default;

        foreach (CompiledOperator function in CompiledOperators)
        {
            if (!function.IsTemplate) continue;
            if (function.Identifier.Content != @operator.Operator.Content) continue;

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

            if (found)
            { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

            compiledOperator = new CompliableTemplate<CompiledOperator>(function, typeArguments);

            found = true;
        }

        return found;
    }

    protected bool GetOperatorTemplate(UnaryOperatorCall @operator, out CompliableTemplate<CompiledOperator> compiledOperator)
    {
        GeneralType[] parameters = FindStatementTypes(@operator.Parameters);

        bool found = false;
        compiledOperator = default;

        foreach (CompiledOperator function in CompiledOperators)
        {
            if (!function.IsTemplate) continue;
            if (function.Identifier.Content != @operator.Operator.Content) continue;

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

            if (found)
            { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

            compiledOperator = new CompliableTemplate<CompiledOperator>(function, typeArguments);

            found = true;
        }

        return found;
    }

    static bool ContextIs(CompiledGeneralFunction function, GeneralType type) =>
        type is StructType structType &&
        function.Context is not null &&
        function.Context == structType.Struct;

    protected bool GetGeneralFunction(GeneralType context, GeneralType[] parameters, string name, [NotNullWhen(true)] out CompiledGeneralFunction? compiledFunction)
    {
        compiledFunction = null;

        foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
        {
            if (function.IsTemplate) continue;
            if (function.Identifier.Content != name) continue;
            if (!ContextIs(function, context)) continue;
            if (!GeneralType.AreEquals(function.ParameterTypes, parameters)) continue;

            if (compiledFunction is not null)
            { throw new CompilerException($"Duplicated general function definitions: {compiledFunction} and {function} are the same", function.Identifier, function.FilePath); }

            compiledFunction = function;
        }

        foreach (CompliableTemplate<CompiledGeneralFunction> function in CompilableGeneralFunctions)
        {
            if (function.Function.Identifier.Content != name) continue;
            if (!ContextIs(function.Function, context)) continue;
            if (!GeneralType.AreEquals(function.Function.ParameterTypes, parameters)) continue;

            if (compiledFunction is not null)
            { throw new CompilerException($"Duplicated general function definitions: {compiledFunction} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

            compiledFunction = function.Function;
        }

        return compiledFunction is not null;
    }

    protected bool GetGeneralFunctionTemplate(GeneralType type, GeneralType[] parameters, string name, out CompliableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
    {
        bool found = false;
        compiledGeneralFunction = default;

        foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
        {
            if (!function.IsTemplate) continue;
            if (function.Identifier.Content != name) continue;
            if (function.ParameterCount != parameters.Length) continue;
            if (!ContextIs(function, type is PointerType pointerType ? pointerType.To : type)) continue;

            Dictionary<string, GeneralType> typeArguments = new(TypeArguments);

            if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

            compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeArguments);

            if (found)
            { throw new CompilerException($"Duplicated general function definitions: {compiledGeneralFunction} and {function} are the same", function.Identifier, function.FilePath); }

            found = true;
        }

        return found;
    }

    protected bool GetOutputWriter(GeneralType type, [NotNullWhen(true)] out CompiledFunction? function)
    {
        foreach (CompiledFunction _function in CompiledFunctions)
        {
            if (!_function.Attributes.HasAttribute(AttributeConstants.ExternalIdentifier, ExternalFunctionNames.StdOut))
            { continue; }

            if (!_function.CanUse(CurrentFile))
            { continue; }

            if (_function.Parameters.Count != 1)
            { continue; }

            if (type != _function.ParameterTypes[0])
            { continue; }

            function = _function;
            return true;
        }

        function = null;
        return false;
    }

    #endregion

    #region GetStruct()

    protected bool GetStruct(TypeInstanceSimple type, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
    {
        if (!type.GenericTypes.HasValue)
        { return GetStruct(type.Identifier.Content, out compiledStruct); }
        else
        { return GetStruct(type.Identifier.Content, type.GenericTypes.Value.Length, out compiledStruct); }
    }

    protected bool GetStruct(TypeInstance type, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
    {
        if (type is not TypeInstanceSimple typeSimple)
        {
            compiledStruct = null;
            return false;
        }
        return GetStruct(typeSimple, out compiledStruct);
    }

    protected bool GetStruct(string structName, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        => CodeGenerator.GetStruct(CompiledStructs, structName, out compiledStruct);

    public static bool GetStruct(IEnumerable<CompiledStruct?> structs, string structName, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
    {
        foreach (CompiledStruct? @struct in structs)
        {
            if (@struct is null) continue;

            if (@struct.Identifier.Content == structName)
            {
                compiledStruct = @struct;
                return true;
            }
        }

        compiledStruct = null;
        return false;
    }

    protected bool GetStruct(string structName, int typeParameterCount, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        => CodeGenerator.GetStruct(CompiledStructs, structName, typeParameterCount, out compiledStruct);
    public static bool GetStruct(IEnumerable<CompiledStruct?> structs, string structName, int typeParameterCount, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
    {
        foreach (CompiledStruct? @struct in structs)
        {
            if (@struct is null) continue;

            if (@struct.Identifier.Content != structName) continue;
            if (typeParameterCount > 0 && @struct.TemplateInfo != null)
            { if (@struct.TemplateInfo.TypeParameters.Length != typeParameterCount) continue; }

            compiledStruct = @struct;
            return true;
        }

        compiledStruct = null;
        return false;
    }

    #endregion

    #region FindType()

    /// <exception cref="CompilerException"/>
    protected GeneralType FindType(Token name)
    {
        if (!FindType(name, out GeneralType? result))
        { throw new CompilerException($"Type \"{name}\" not found", name, CurrentFile); }

        return result;
    }

    protected bool FindType(Token name, [NotNullWhen(true)] out GeneralType? result)
    {
        if (TypeKeywords.BasicTypes.TryGetValue(name.Content, out BasicType builtinType))
        {
            result = new BuiltinType(builtinType);
            return true;
        }

        if (GetStruct(name.Content, out CompiledStruct? @struct))
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
            function.References.Add(new Reference<StatementWithValue>(new Identifier(name), CurrentFile));
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
        { if (CodeGenerator.SameType(destEnumType.Enum, valueType)) return; }

        if (valueType is EnumType valEnumType)
        { if (CodeGenerator.SameType(valEnumType.Enum, destination)) return; }

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
        { if (CodeGenerator.SameType(destEnumType.Enum, valueType)) return; }

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

        IReadOnlyDictionary<string, int> fieldOffsets;

        if (prevType is StructType structType)
        {
            structType.Struct.AddTypeArguments(TypeArguments);
            structType.Struct.AddTypeArguments(structType.TypeParameters);

            fieldOffsets = structType.Struct.FieldOffsets;

            structType.Struct.ClearTypeArguments();
        }
        else
        { throw new NotImplementedException(); }

        if (!fieldOffsets.TryGetValue(field.Identifier.Content, out int fieldOffset))
        { throw new InternalException($"Field \"{field.Identifier}\" does not have an offset value", CurrentFile); }

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

    protected CompiledFunction? FindCodeEntry()
    {
        for (int i = 0; i < CompiledFunctions.Length; i++)
        {
            CompiledFunction function = this.CompiledFunctions[i];

            for (int j = 0; j < function.Attributes.Length; j++)
            {
                if (function.Attributes[j].Identifier.Content != "CodeEntry") continue;

                if (function.IsTemplate)
                { throw new CompilerException($"Code entry can not be a template function", function.TemplateInfo, function.FilePath); }

                return function;
            }
        }

        return null;
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

    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="CompilerException"></exception>
    /// <exception cref="InternalException"></exception>
    protected static DataItem[] GetInitialValue(CompiledStruct @struct)
    {
        List<DataItem> result = new();

        foreach (CompiledField field in @struct.Fields)
        { result.Add(GetInitialValue(field.Type)); }

        if (result.Count != @struct.Size)
        { throw new NotImplementedException(); }

        return result.ToArray();
    }

    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="CompilerException"></exception>
    /// <exception cref="InternalException"></exception>
    protected static DataItem GetInitialValue(TypeInstance type)
        => type.ToString() switch
        {
            TypeKeywords.Int => new DataItem((int)0),
            TypeKeywords.Byte => new DataItem((byte)0),
            TypeKeywords.Float => new DataItem((float)0f),
            TypeKeywords.Char => new DataItem((char)'\0'),
            StatementKeywords.Var => throw new InternalException("Undefined type"),
            TypeKeywords.Void => throw new InternalException("Invalid type"),
            _ => throw new InternalException($"Initial value for type \"{type}\" is unimplemented"),
        };

    /// <exception cref="NotImplementedException"></exception>
    /// <exception cref="InternalException"></exception>
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
            "sizeof" => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Integer)),
            StatementKeywords.Delete => OnGotStatementType(keywordCall, new BuiltinType(BasicType.Void)),
            _ => throw new CompilerException($"Unknown keyword-function \"{keywordCall.Identifier}\"", keywordCall.Identifier, CurrentFile)
        };
    }
    protected GeneralType FindStatementType(IndexCall index)
    {
        GeneralType prevType = FindStatementType(index.PrevStatement);

        if (prevType is ArrayType arrayType)
        { return OnGotStatementType(index, arrayType.Of); }

        if (!GetIndexGetter(prevType, out CompiledFunction? indexer, out WillBeCompilerException? notFoundException))
        {
            if (GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
            {
                indexer = indexerTemplate.Function;
                notFoundException = null;
            }
        }

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

        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(functionCall, FindMacroType(macro, functionCall.Parameters));
        }

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction, out WillBeCompilerException? notFoundError))
        {
            if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compiledFunctionTemplate))
            {
                throw notFoundError.Instantiate(functionCall, CurrentFile);
            }

            compiledFunction = compiledFunctionTemplate.Function;
        }

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

        if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
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

        if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
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

        if (GetConstructor(type, parameters, out CompiledConstructor? constructor, out WillBeCompilerException? notFound))
        {
            constructorCall.Type.SetAnalyzedType(constructor.Type);
            return OnGotStatementType(constructorCall, constructor.Type);
        }

        if (GetConstructorTemplate(type, parameters, out CompliableTemplate<CompiledConstructor> compilableGeneralFunction, out notFound))
        {
            constructorCall.Type.SetAnalyzedType(compilableGeneralFunction.Function.Type);
            return OnGotStatementType(constructorCall, compilableGeneralFunction.Function.Type);
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

                if (structType.Struct.TemplateInfo is null)
                { return definedField.Type; }

                return GeneralType.InsertTypeParameters(definedField.Type, structType.TypeParameters) ?? definedField.Type;
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

    protected GeneralType FindMacroType(MacroDefinition macro, IEnumerable<StatementWithValue> parameters)
        => FindMacroType(macro, parameters.ToArray());
    protected GeneralType FindMacroType(MacroDefinition macro, params StatementWithValue[] parameters)
    {
        if (!InlineMacro(macro, out Statement? inlinedMacro, parameters))
        { throw new CompilerException($"Failed to inline the macro", new Position(parameters), CurrentFile); }

        if (inlinedMacro is StatementWithValue statementWithValue)
        { return FindStatementType(statementWithValue); }

        List<GeneralType> result = new();

        if (inlinedMacro.TryGetStatement(out KeywordCall? keywordCall, s => s.Identifier.Equals(StatementKeywords.Return)))
        {
            if (keywordCall.Parameters.Length == 0)
            { result.Add(new BuiltinType(BasicType.Void)); }
            else
            { result.Add(FindStatementType(keywordCall.Parameters[0])); }
        }

        if (result.Count == 0)
        { return new BuiltinType(BasicType.Void); }

        for (int i = 1; i < result.Count; i++)
        {
            if (!result[i].Equals(result[0]))
            { throw new CompilerException($"Macro \"{macro.ToReadable()}\" returns more than one type of value", macro.Block, macro.FilePath); }
        }

        return result[0];
    }

    #endregion

    #region InlineMacro()

    protected bool TryInlineMacro(StatementWithValue statement, [NotNullWhen(true)] out Statement? inlined)
    {
        if (statement is AnyCall anyCall)
        { return TryInlineMacro(anyCall, out inlined); }

        inlined = null;
        return false;
    }
    protected bool TryInlineMacro(AnyCall anyCall, [NotNullWhen(true)] out Statement? inlined)
    {
        if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
        { return TryInlineMacro(functionCall, out inlined); }

        inlined = null;
        return false;
    }
    protected bool TryInlineMacro(FunctionCall functionCall, [NotNullWhen(true)] out Statement? inlined)
    {
        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            return InlineMacro(macro, out inlined, functionCall.MethodParameters);
        }

        inlined = null;
        return false;
    }

    protected static bool InlineMacro(MacroDefinition macro, [NotNullWhen(true)] out Statement? inlined, IEnumerable<StatementWithValue> parameters)
        => InlineMacro(macro, out inlined, parameters.ToArray());

    protected static bool InlineMacro(MacroDefinition macro, [NotNullWhen(true)] out Statement? inlined, params StatementWithValue[] parameters)
    {
        Dictionary<string, StatementWithValue> _parameters = Utils.Map(
            macro.Parameters.ToArray(),
            parameters,
            (key, value) => (key.Content, value));

        return InlineMacro(macro, _parameters, out inlined);
    }

    static bool InlineMacro(MacroDefinition macro, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Statement? inlined)
    {
        inlined = null;

        if (macro.Block.Statements.Length == 0)
        {
            // throw new CompilerException($"Macro \"{macro.ToReadable()}\" has no statements", macro.Block, macro.FilePath);
            return false;
        }

        if (macro.Block.Statements.Length == 1)
        {
            if (!InlineMacro(macro.Block.Statements[0], parameters, out inlined))
            { return false; }
        }
        else
        {
            if (!InlineMacro(macro.Block, parameters, out inlined))
            { return false; }
        }

        // inlined = Collapse(inlined, parameters);

        if (inlined is KeywordCall keywordCall &&
            keywordCall.Identifier.Equals(StatementKeywords.Return) &&
            keywordCall.Parameters.Length == 1)
        { inlined = keywordCall.Parameters[0]; }

        return true;
    }

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
            right: InlineMacro(operatorCall.Right, parameters))
        {
            SurroundingBracelet = operatorCall.SurroundingBracelet,
            SaveValue = operatorCall.SaveValue,
            Semicolon = operatorCall.Semicolon,
        };

    static UnaryOperatorCall InlineMacro(UnaryOperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            op: operatorCall.Operator,
            left: InlineMacro(operatorCall.Left, parameters))
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
        return new FunctionCall(prevStatement, functionCall.Identifier, _parameters, functionCall.Brackets);
    }

    static AnyCall InlineMacro(AnyCall anyCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(anyCall.PrevStatement, parameters),
            parameters: InlineMacro(anyCall.Parameters, parameters),
            brackets: anyCall.Brackets)
        {
            SaveValue = anyCall.SaveValue,
            Semicolon = anyCall.Semicolon,
        };

    static ConstructorCall InlineMacro(ConstructorCall constructorCall, Dictionary<string, StatementWithValue> parameters)
        => new(
            keyword: constructorCall.Keyword,
            typeName: constructorCall.Type,
            parameters: InlineMacro(constructorCall.Parameters, parameters),
            brackets: constructorCall.Brackets)
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

    /// <exception cref="InlineException"/>
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

    /// <exception cref="InlineException"/>
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

    /// <exception cref="InlineException"/>
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

    /// <exception cref="InlineException"/>
    static bool InlineMacro(Assignment statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out Assignment? inlined)
    {
        inlined = null;
        if (statement.Left is Identifier identifier &&
            parameters.ContainsKey(identifier.Content))
        { return false; }

        inlined = new Assignment(
            @operator: statement.Operator,
            left: statement.Left,
            right: InlineMacro(statement.Right, parameters))
        {
            Semicolon = statement.Semicolon,
        };
        return true;
    }

    /// <exception cref="InlineException"/>
    static bool InlineMacro(ShortOperatorCall statement, Dictionary<string, StatementWithValue> parameters, [NotNullWhen(true)] out ShortOperatorCall? inlined)
    {
        inlined = null;
        if (statement.Left is Identifier identifier &&
            parameters.ContainsKey(identifier.Content))
        { return false; }

        inlined = new ShortOperatorCall(
             op: statement.Operator,
             left: statement.Left)
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
            right: InlineMacro(statement.Right, parameters))
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
            initialValue: InlineMacro(statement.InitialValue, parameters))
        {
            FilePath = statement.FilePath,
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
            fieldName: statement.Identifier)
        {
            SaveValue = statement.SaveValue,
            Semicolon = statement.Semicolon,
        };

    static IndexCall InlineMacro(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
        => new(
            prevStatement: InlineMacro(statement.PrevStatement, parameters),
            indexStatement: InlineMacro(statement.Index, parameters),
            brackets: statement.Brackets)
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

        if (GetOperator(@operator, out CompiledOperator? compiledOperator))
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

        if (GetOperator(@operator, out CompiledOperator? compiledOperator))
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
                break;
            case LiteralType.Float:
                value = new DataItem(literal.GetFloat());
                break;
            case LiteralType.Char:
                if (literal.Value.Length != 1)
                {
                    value = DataItem.Null;
                    return false;
                }
                value = new DataItem(literal.Value[0]);
                break;
            case LiteralType.String:
            default:
                value = DataItem.Null;
                return false;
        }
        return true;
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

        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            if (!InlineMacro(macro, out Statement? inlined, functionCall.Parameters))
            { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

            if (inlined is StatementWithValue statementWithValue)
            { return TryCompute(statementWithValue, context, out value); }
        }

        if (GetFunction(functionCall, out CompiledFunction? function, out _))
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
                    functionCall.Brackets)
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

        value = DataItem.Null;
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
            value = GetInitialValue(variableDeclaration.Type);
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
        if (loop.Block.TryGetStatement<KeywordCall>(out _, (statement) => statement.Identifier.Content is StatementKeywords.Break or StatementKeywords.Return))
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
