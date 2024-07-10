using System.IO;

namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;

public readonly struct CompilerResult
{
    public readonly ImmutableArray<CompiledFunction> Functions;
    public readonly ImmutableArray<CompiledGeneralFunction> GeneralFunctions;
    public readonly ImmutableArray<CompiledOperator> Operators;
    public readonly ImmutableArray<CompiledConstructor> Constructors;

    public readonly ImmutableDictionary<Uri, CollectedAST> Raw;

    public readonly Dictionary<int, ExternalFunctionBase> ExternalFunctions;

    public readonly ImmutableArray<CompiledStruct> Structs;

    public readonly ImmutableArray<(ImmutableArray<Statement> Statements, Uri File)> TopLevelStatements;

    public readonly Uri File;

    public readonly IEnumerable<Uri> Files
    {
        get
        {
            HashSet<Uri> alreadyExists = new();

            foreach (CompiledFunction function in Functions)
            {
                Uri? file = function.File;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledGeneralFunction generalFunction in GeneralFunctions)
            {
                Uri? file = generalFunction.File;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledOperator @operator in Operators)
            {
                Uri? file = @operator.File;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledStruct @struct in Structs)
            {
                Uri? file = @struct.File;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }
        }
    }

    public readonly IEnumerable<Statement> Statements
    {
        get
        {
            foreach ((ImmutableArray<Statement> topLevelStatements, _) in TopLevelStatements)
            {
                foreach (Statement statement in topLevelStatements)
                { yield return statement; }
            }

            foreach (CompiledFunction function in Functions)
            {
                if (function.Block != null) yield return function.Block;
            }

            foreach (CompiledGeneralFunction function in GeneralFunctions)
            {
                if (function.Block != null) yield return function.Block;
            }

            foreach (CompiledOperator @operator in Operators)
            {
                if (@operator.Block != null) yield return @operator.Block;
            }
        }
    }

    public readonly IEnumerable<Statement> StatementsIn(Uri file)
    {
        foreach ((ImmutableArray<Statement> topLevelStatements, Uri? _file) in TopLevelStatements)
        {
            if (file != _file) continue;
            foreach (Statement statement in topLevelStatements)
            { yield return statement; }
        }

        foreach (CompiledFunction function in Functions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledGeneralFunction function in GeneralFunctions)
        {
            if (file != function.File) continue;
            if (function.Block != null) yield return function.Block;
        }

        foreach (CompiledOperator @operator in Operators)
        {
            if (file != @operator.File) continue;
            if (@operator.Block != null) yield return @operator.Block;
        }
    }

    public static CompilerResult MakeEmpty(Uri file) => new(
        Enumerable.Empty<KeyValuePair<Uri, CollectedAST>>(),
        Enumerable.Empty<CompiledFunction>(),
        Enumerable.Empty<CompiledGeneralFunction>(),
        Enumerable.Empty<CompiledOperator>(),
        Enumerable.Empty<CompiledConstructor>(),
        Enumerable.Empty<KeyValuePair<int, ExternalFunctionBase>>(),
        Enumerable.Empty<CompiledStruct>(),
        Enumerable.Empty<(ImmutableArray<Statement>, Uri)>(),
        file);

    public CompilerResult(
        IEnumerable<KeyValuePair<Uri, CollectedAST>> tokens,
        IEnumerable<CompiledFunction> functions,
        IEnumerable<CompiledGeneralFunction> generalFunctions,
        IEnumerable<CompiledOperator> operators,
        IEnumerable<CompiledConstructor> constructors,
        IEnumerable<KeyValuePair<int, ExternalFunctionBase>> externalFunctions,
        IEnumerable<CompiledStruct> structs,
        IEnumerable<(ImmutableArray<Statement> Statements, Uri File)> topLevelStatements,
        Uri file)
    {
        Raw = tokens.ToImmutableDictionary();
        Functions = functions.ToImmutableArray();
        GeneralFunctions = generalFunctions.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Constructors = constructors.ToImmutableArray();
        ExternalFunctions = externalFunctions.ToDictionary();
        Structs = structs.ToImmutableArray();
        TopLevelStatements = topLevelStatements.ToImmutableArray();
        File = file;
    }

    public static bool GetThingAt<TThing, TIdentifier>(IEnumerable<TThing> things, Uri file, SinglePosition position, [NotNullWhen(true)] out TThing? result)
        where TThing : IInFile, IIdentifiable<TIdentifier>
        where TIdentifier : IPositioned
    {
        foreach (TThing? thing in things)
        {
            if (thing.File != file)
            { continue; }

            if (!thing.Identifier.Position.Range.Contains(position))
            { continue; }

            result = thing;
            return true;
        }

        result = default;
        return false;
    }

    public bool GetFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledFunction? result)
        => GetThingAt<CompiledFunction, Token>(Functions, file, position, out result);

    public bool GetGeneralFunctionAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledGeneralFunction? result)
        => GetThingAt<CompiledGeneralFunction, Token>(GeneralFunctions, file, position, out result);

    public bool GetOperatorAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledOperator? result)
        => GetThingAt<CompiledOperator, Token>(Operators, file, position, out result);

    public bool GetStructAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledStruct? result)
        => GetThingAt<CompiledStruct, Token>(Structs, file, position, out result);

    public bool GetFieldAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledField? result)
    {
        foreach (CompiledStruct @struct in Structs)
        {
            if (@struct.File != file) continue;

            foreach (CompiledField field in @struct.Fields)
            {
                if (field.Identifier.Position.Range.Contains(position))
                {
                    result = field;
                    return true;
                }
            }
        }

        result = null;
        return false;
    }
}

public struct CompilerSettings
{
    public string? BasePath;

    public CompilerSettings(CompilerSettings other)
    {
        BasePath = other.BasePath;
    }

    public static CompilerSettings Default => new()
    {
        BasePath = null,
    };
}

public enum CompileLevel
{
    Minimal,
    Exported,
    All,
}

public sealed class Compiler
{
    readonly List<CompiledStruct> CompiledStructs = new();
    readonly List<CompiledOperator> CompiledOperators = new();
    readonly List<CompiledConstructor> CompiledConstructors = new();
    readonly List<CompiledFunction> CompiledFunctions = new();
    readonly List<CompiledGeneralFunction> CompiledGeneralFunctions = new();

    readonly List<FunctionDefinition> Operators = new();
    readonly List<FunctionDefinition> Functions = new();
    readonly List<StructDefinition> Structs = new();

    readonly List<(ImmutableArray<Statement> Statements, Uri File)> TopLevelStatements = new();

    readonly Stack<ImmutableArray<Token>> GenericParameters = new();

    readonly CompilerSettings Settings;
    readonly TokenizerSettings? TokenizerSettings;
    readonly Dictionary<int, ExternalFunctionBase> ExternalFunctions;
    readonly PrintCallback? PrintCallback;

    readonly AnalysisCollection? AnalysisCollection;
    readonly IEnumerable<string> PreprocessorVariables;

    Compiler(Dictionary<int, ExternalFunctionBase>? externalFunctions, PrintCallback? printCallback, CompilerSettings settings, AnalysisCollection? analysisCollection, IEnumerable<string> preprocessorVariables, TokenizerSettings? tokenizerSettings)
    {
        ExternalFunctions = externalFunctions ?? new Dictionary<int, ExternalFunctionBase>();
        Settings = settings;
        PrintCallback = printCallback;
        AnalysisCollection = analysisCollection;
        PreprocessorVariables = preprocessorVariables;
        TokenizerSettings = tokenizerSettings;
    }

    bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out GeneralType? result)
    {
        if (CodeGenerator.GetStruct(CompiledStructs, name.Content, relevantFile, out CompiledStruct? @struct, out _))
        {
            result = new StructType(@struct, relevantFile);
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

        if (CodeGenerator.GetFunction<CompiledFunction, Token, string>(
            new CodeGenerator.Functions<CompiledFunction>()
            {
                Compiled = CompiledFunctions,
                Compilable = Enumerable.Empty<CodeGenerator.CompliableTemplate<CompiledFunction>>(),
            },
            "function",
            null,

            CodeGenerator.FunctionQuery.Create<CompiledFunction, string, GeneralType>(name.Content, null, relevantFile),
            out CodeGenerator.FunctionQueryResult<CompiledFunction>? result_,
            out _
        ))
        {
            result = new FunctionType(result_.Function);
            return true;
        }

        result = null;
        return false;
    }

    CompiledStruct CompileStruct(StructDefinition @struct)
    {
        if (LanguageConstants.KeywordList.Contains(@struct.Identifier.Content))
        { throw new CompilerException($"Illegal struct name \"{@struct.Identifier.Content}\"", @struct.Identifier, @struct.File); }

        @struct.Identifier.AnalyzedType = TokenAnalyzedType.Struct;

        if (@struct.Template is not null)
        {
            GenericParameters.Push(@struct.Template.Parameters);
            foreach (Token typeParameter in @struct.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        CompiledField[] compiledFields = new CompiledField[@struct.Fields.Length];

        for (int i = 0; i < @struct.Fields.Length; i++)
        {
            FieldDefinition field = @struct.Fields[i];
            compiledFields[i] = new CompiledField(GeneralType.From(field.Type, FindType), null! /* CompiledStruct constructor will set this */, field);
        }

        if (@struct.Template is not null)
        { GenericParameters.Pop(); }

        return new CompiledStruct(compiledFields, @struct);
    }

    CompiledFunction CompileFunction(FunctionDefinition function, CompiledStruct? context)
    {
        if (function.Template is not null)
        {
            GenericParameters.Push(function.Template.Parameters);
            foreach (Token typeParameter in function.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        GeneralType type = GeneralType.From(function.Type, FindType);
        function.Type.SetAnalyzedType(type);

        GeneralType[] parameterTypes = GeneralType.FromArray(function.Parameters, FindType).ToArray();

        foreach (AttributeUsage attribute in function.Attributes)
        {
            switch (attribute.Identifier.Content)
            {
                case AttributeConstants.ExternalIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        AnalysisCollection?.Errors.Add(new LanguageError($"Wrong number of parameters passed to attribute {attribute.Identifier}: required {1}, passed {attribute.Parameters.Length}", attribute, function.File));
                        break;
                    }

                    if (attribute.Parameters[0].Type != LiteralType.String)
                    {
                        AnalysisCollection?.Errors.Add(new LanguageError($"Invalid parameter type {attribute.Parameters[0].Type} for attribute {attribute.Identifier} at {0}: expected {LiteralType.String}", attribute, function.File));
                        break;
                    }

                    string externalName = attribute.Parameters[0].Value;

                    if (!ExternalFunctions.TryGet(externalName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
                    {
                        // AnalysisCollection?.Warnings.Add(exception.InstantiateWarning(attribute, function.File));
                        break;
                    }

                    if (externalFunction.ParametersSize != parameterTypes.Sum(v => v == BasicType.Void ? 0 : v.SizeBytes))
                    { throw new CompilerException($"Wrong size of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.File); }

                    if (externalFunction.ReturnValueSize != (type == BasicType.Void ? 0 : type.SizeBytes))
                    { throw new CompilerException($"Wrong size of return type passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.File); }

                    break;
                }
                case AttributeConstants.BuiltinIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        AnalysisCollection?.Errors.Add(new LanguageError($"Wrong number of parameters passed to attribute {attribute.Identifier}: required {1}, passed {attribute.Parameters.Length}", attribute, function.File));
                        break;
                    }

                    if (attribute.Parameters[0].Type != LiteralType.String)
                    {
                        AnalysisCollection?.Errors.Add(new LanguageError($"Invalid parameter type {attribute.Parameters[0].Type} for attribute {attribute.Identifier} at {0}: expected {LiteralType.String}", attribute, function.File));
                        break;
                    }

                    string builtinName = attribute.Parameters[0].Value;

                    if (!BuiltinFunctions.Prototypes.TryGetValue(builtinName, out (GeneralType ReturnValue, ImmutableArray<GeneralType> Parameters) builtinFunction))
                    {
                        // AnalysisCollection?.Warnings.Add(new Warning($"Builtin function \"{builtinName}\" not found", attribute, function.File));
                        break;
                    }

                    if (builtinFunction.Parameters.Length != function.Parameters.Count)
                    { throw new CompilerException($"Wrong number of parameters passed to function \"{builtinName}\"", function.Identifier, function.File); }

                    if (builtinFunction.ReturnValue != type)
                    { throw new CompilerException($"Wrong type defined for function \"{builtinName}\"", function.Type, function.File); }

                    for (int i = 0; i < builtinFunction.Parameters.Length; i++)
                    {
                        GeneralType definedParameterType = builtinFunction.Parameters[i];
                        GeneralType passedParameterType = parameterTypes[i];
                        function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                        if (passedParameterType == definedParameterType)
                        { continue; }

                        if (passedParameterType is PointerType && definedParameterType is PointerType)
                        { continue; }

                        throw new CompilerException($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: {definedParameterType} Passed: {passedParameterType}", function.Parameters[i].Type, function.File);
                    }
                    break;
                }
                default:
                {
                    AnalysisCollection?.Warnings.Add(new Warning($"Unknown attribute {attribute.Identifier}", attribute.Identifier, function.File));
                    break;
                }
            }
        }

        CompiledFunction result = new(
            type,
            parameterTypes,
            context,
            function);

        if (function.Template is not null)
        { GenericParameters.Pop(); }

        return result;
    }

    CompiledOperator CompileOperator(FunctionDefinition function, CompiledStruct? context)
    {
        GeneralType type = GeneralType.From(function.Type, FindType);
        function.Type.SetAnalyzedType(type);

        GeneralType[] parametersType = GeneralType.FromArray(function.Parameters, FindType).ToArray();

        if (function.Attributes.TryGetAttribute<string>(AttributeConstants.ExternalIdentifier, out string? name, out AttributeUsage? attribute))
        {
            if (ExternalFunctions.TryGet(name, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                if (externalFunction.ParametersSize != parametersType.Sum(v => v == BasicType.Void ? 0 : v.SizeBytes))
                { throw new CompilerException($"Wrong size of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.File); }

                if (externalFunction.ReturnValueSize != (type == BasicType.Void ? 0 : type.SizeBytes))
                { throw new CompilerException($"Wrong size of return type passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.File); }
            }
            else
            {
                AnalysisCollection?.Errors.Add(exception.InstantiateError(attribute.Identifier, function.File));
            }
        }

        return new CompiledOperator(
            type,
            parametersType,
            context,
            function);
    }

    CompiledGeneralFunction CompileGeneralFunction(GeneralFunctionDefinition function, GeneralType returnType, CompiledStruct context)
    {
        return new CompiledGeneralFunction(
            returnType,
            GeneralType.FromArray(function.Parameters, FindType),
            context,
            function
            );
    }

    CompiledConstructor CompileConstructor(ConstructorDefinition function, CompiledStruct context)
    {
        if (function.Template is not null)
        {
            GenericParameters.Push(function.Template.Parameters);
            foreach (Token typeParameter in function.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        GeneralType type = GeneralType.From(function.Type, FindType);
        function.Type.SetAnalyzedType(type);

        CompiledConstructor result = new(
            type,
            GeneralType.FromArray(function.Parameters, FindType),
            context,
            function);

        if (function.Template is not null)
        { GenericParameters.Pop(); }

        return result;
    }

    void AddAST(CollectedAST collectedAST, bool addTopLevelStatements = true)
    {
        if (addTopLevelStatements)
        { TopLevelStatements.Add((collectedAST.ParserResult.TopLevelStatements, collectedAST.Uri)); }

        Functions.AddRange(collectedAST.ParserResult.Functions);
        Operators.AddRange(collectedAST.ParserResult.Operators);
        Structs.AddRange(collectedAST.ParserResult.Structs);
    }

    CompilerResult CompileMainFile(Uri file, FileParser? fileParser)
    {
        ImmutableDictionary<Uri, CollectedAST> files = SourceCodeManager.Collect(file, PrintCallback, Settings.BasePath, AnalysisCollection, PreprocessorVariables, TokenizerSettings, fileParser);

        foreach ((Uri file_, CollectedAST ast) in files)
        { AddAST(ast, file_ != file); }

        foreach ((Uri file_, CollectedAST ast) in files)
        {
            if (file_ == file)
            { TopLevelStatements.Add((ast.ParserResult.TopLevelStatements, file_)); }
        }

        CompileInternal();

        return new CompilerResult(
            files,
            CompiledFunctions,
            CompiledGeneralFunctions,
            CompiledOperators,
            CompiledConstructors,
            ExternalFunctions,
            CompiledStructs,
            TopLevelStatements,
            file);
    }

    CompilerResult CompileInteractiveInternal(Statement statement, ImmutableArray<UsingDefinition> usings, Uri file)
    {
        ImmutableDictionary<Uri, CollectedAST> files = SourceCodeManager.Collect(usings, file, PrintCallback, Settings.BasePath, AnalysisCollection, PreprocessorVariables, TokenizerSettings, null);

        foreach (CollectedAST file_ in files.Values)
        { AddAST(file_); }

        TopLevelStatements.Add(([statement], file));

        CompileInternal();

        return new CompilerResult(
            files,
            CompiledFunctions,
            CompiledGeneralFunctions,
            CompiledOperators,
            CompiledConstructors,
            ExternalFunctions,
            CompiledStructs,
            TopLevelStatements,
            file);
    }

    void CompileInternal()
    {
        static bool ThingEquality<TThing1, TThing2>(TThing1 a, TThing2 b)
            where TThing1 : IIdentifiable<Token>, IInFile
            where TThing2 : IIdentifiable<Token>, IInFile
        {
            if (!a.Identifier.Content.Equals(b.Identifier.Content)) return false;
            if (a.File != b.File) return false;
            return true;
        }

        static bool FunctionEquality<TFunction>(TFunction a, TFunction b)
            where TFunction : FunctionThingDefinition, ICompiledFunction
        {
            if (!a.Type.Equals(b.Type)) return false;
            if (!Utils.SequenceEquals(a.ParameterTypes, b.ParameterTypes)) return false;
            if (!ThingEquality(a, b)) return false;
            return true;
        }

        bool IsThingExists<TThing>(TThing thing)
            where TThing : IIdentifiable<Token>, IInFile
        {
            if (CompiledStructs.Any(other => ThingEquality(other, thing)))
            { return true; }

            if (CompiledFunctions.Any(other => ThingEquality(other, thing)))
            { return true; }

            return false;
        }

        foreach (StructDefinition @struct in Structs)
        {
            if (IsThingExists(@struct))
            { throw new CompilerException("Symbol already exists", @struct.Identifier, @struct.File); }

            CompiledStructs.Add(CompileStruct(@struct));
        }

        foreach (FunctionDefinition @operator in Operators)
        {
            CompiledOperator compiled = CompileOperator(@operator, null);

            if (CompiledOperators.Any(other => FunctionEquality(compiled, other)))
            { throw new CompilerException($"Operator {compiled.ToReadable()} already defined", @operator.Identifier, @operator.File); }

            CompiledOperators.Add(compiled);
        }

        foreach (FunctionDefinition function in Functions)
        {
            CompiledFunction compiled = CompileFunction(function, null);

            if (CompiledFunctions.Any(other => FunctionEquality(compiled, other)))
            { throw new CompilerException($"Function {compiled.ToReadable()} already defined", function.Identifier, function.File); }

            CompiledFunctions.Add(compiled);
        }

        foreach (CompiledStruct compiledStruct in CompiledStructs)
        {
            if (compiledStruct.Template is not null)
            {
                GenericParameters.Push(compiledStruct.Template.Parameters);
                foreach (Token typeParameter in compiledStruct.Template.Parameters)
                { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
            }

            foreach (GeneralFunctionDefinition method in compiledStruct.GeneralFunctions)
            {
                foreach (ParameterDefinition parameter in method.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File); }
                }

                GeneralType returnType = new StructType(compiledStruct, method.File);

                if (method.Identifier.Content == BuiltinFunctionIdentifiers.Destructor)
                {
                    List<ParameterDefinition> parameters = method.Parameters.ToList();
                    parameters.Insert(0, new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters),
                        Token.CreateAnonymous(StatementKeywords.This),
                        method)
                    );

                    GeneralFunctionDefinition copy = new(
                        method.Identifier,
                        method.Modifiers,
                        new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                        method.File)
                    {
                        Context = method.Context,
                        Block = method.Block,
                    };

                    returnType = BuiltinType.Void;

                    CompiledGeneralFunction methodWithRef = CompileGeneralFunction(copy, returnType, compiledStruct);

                    parameters = method.Parameters.ToList();
                    parameters.Insert(0, new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters)),
                        Token.CreateAnonymous(StatementKeywords.This),
                        method)
                    );

                    copy = new GeneralFunctionDefinition(
                        method.Identifier,
                        method.Modifiers,
                        new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                        method.File)
                    {
                        Context = method.Context,
                        Block = method.Block,
                    };

                    CompiledGeneralFunction methodWithPointer = CompileGeneralFunction(copy, returnType, compiledStruct);

                    if (CompiledGeneralFunctions.Any(methodWithRef.IsSame))
                    { throw new CompilerException($"Function with name \'{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.File); }

                    if (CompiledGeneralFunctions.Any(methodWithPointer.IsSame))
                    { throw new CompilerException($"Function with name \'{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.File); }

                    CompiledGeneralFunctions.Add(methodWithRef);
                    CompiledGeneralFunctions.Add(methodWithPointer);
                }
                else
                {
                    List<ParameterDefinition> parameters = method.Parameters.ToList();

                    CompiledGeneralFunction methodWithRef = CompileGeneralFunction(method, returnType, compiledStruct);

                    if (CompiledGeneralFunctions.Any(methodWithRef.IsSame))
                    { throw new CompilerException($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.File); }

                    CompiledGeneralFunctions.Add(methodWithRef);
                }
            }

            foreach (FunctionDefinition method in compiledStruct.Functions)
            {
                foreach (ParameterDefinition parameter in method.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File); }
                }

                List<ParameterDefinition> parameters = method.Parameters.ToList();
                parameters.Insert(0, new ParameterDefinition(
                    ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.Ref), Token.CreateAnonymous(ModifierKeywords.This)),
                    TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters),
                    Token.CreateAnonymous(StatementKeywords.This),
                    method)
                );

                FunctionDefinition copy = new(
                    method.Attributes,
                    method.Modifiers,
                    method.Type,
                    method.Identifier,
                    new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                    method.Template,
                    method.File)
                {
                    Context = method.Context,
                    Block = method.Block,
                };

                CompiledFunction methodWithRef = CompileFunction(copy, compiledStruct);

                parameters = method.Parameters.ToList();
                parameters.Insert(0,
                    new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters)),
                        Token.CreateAnonymous(StatementKeywords.This),
                        method)
                    );

                copy = new FunctionDefinition(
                    method.Attributes,
                    method.Modifiers,
                    method.Type,
                    method.Identifier,
                    new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                    method.Template,
                    method.File)
                {
                    Context = method.Context,
                    Block = method.Block,
                };

                CompiledFunction methodWithPointer = CompileFunction(copy, compiledStruct);

                if (CompiledFunctions.Any(methodWithRef.IsSame))
                { throw new CompilerException($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.File); }

                if (CompiledFunctions.Any(methodWithPointer.IsSame))
                { throw new CompilerException($"Function with name \"{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.File); }

                CompiledFunctions.Add(methodWithRef);
                CompiledFunctions.Add(methodWithPointer);
            }

            foreach (ConstructorDefinition constructor in compiledStruct.Constructors)
            {
                foreach (ParameterDefinition parameter in constructor.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File); }
                }

                List<ParameterDefinition> parameters = constructor.Parameters.ToList();
                parameters.Insert(0,
                    new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        constructor.Type,
                        Token.CreateAnonymous(StatementKeywords.This),
                        constructor)
                    );

                ConstructorDefinition constructorWithThisParameter = new(
                    constructor.Type,
                    constructor.Modifiers,
                    new ParameterDefinitionCollection(parameters, constructor.Parameters.Brackets),
                    constructor.File)
                {
                    Block = constructor.Block,
                    Context = constructor.Context,
                };

                CompiledConstructor compiledConstructor = CompileConstructor(constructorWithThisParameter, compiledStruct);

                if (CompiledConstructors.Any(compiledConstructor.IsSame))
                { throw new CompilerException($"Constructor \"{compiledConstructor.ToReadable()}\" already defined", constructor.Type, compiledStruct.File); }

                CompiledConstructors.Add(compiledConstructor);
            }

            if (compiledStruct.Template is not null)
            { GenericParameters.Pop(); }
        }
    }

    public static CompilerResult CompileFile(
        Uri file,
        Dictionary<int, ExternalFunctionBase>? externalFunctions,
        CompilerSettings settings,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null,
        TokenizerSettings? tokenizerSettings = null,
        FileParser? fileParser = null)
    {
        Compiler compiler = new(
            externalFunctions,
            printCallback,
            settings,
            analysisCollection,
            preprocessorVariables,
            tokenizerSettings);
        return compiler.CompileMainFile(file, fileParser);
    }

    public static CompilerResult CompileFile(
        FileInfo file,
        Dictionary<int, ExternalFunctionBase>? externalFunctions,
        CompilerSettings settings,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null,
        TokenizerSettings? tokenizerSettings = null,
        FileParser? fileParser = null)
    {
        Compiler compiler = new(
            externalFunctions,
            printCallback,
            settings,
            analysisCollection,
            preprocessorVariables,
            tokenizerSettings);
        Uri uri = new(file.FullName, UriKind.Absolute);
        return compiler.CompileMainFile(uri, fileParser);
    }

    public static CompilerResult CompileInteractive(
        Statement statement,
        Dictionary<int, ExternalFunctionBase>? externalFunctions,
        CompilerSettings settings,
        ImmutableArray<UsingDefinition> usings,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback,
        AnalysisCollection? analysisCollection,
        TokenizerSettings? tokenizerSettings,
        Uri file)
    {
        Compiler compiler = new(
            externalFunctions,
            printCallback,
            settings,
            analysisCollection,
            preprocessorVariables,
            tokenizerSettings);
        return compiler.CompileInteractiveInternal(statement, usings, file);
    }
}
