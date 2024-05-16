﻿using System.IO;

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
    public readonly ImmutableArray<CompileTag> Hashes;
    public readonly ImmutableArray<CompiledEnum> Enums;

    public readonly ImmutableArray<(ImmutableArray<Statement> Statements, Uri? File)> TopLevelStatements;

    public readonly Uri? File;

    public readonly IEnumerable<Uri> Files
    {
        get
        {
            HashSet<Uri> alreadyExists = new();

            foreach (CompiledFunction function in Functions)
            {
                Uri? file = function.FilePath;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledGeneralFunction generalFunction in GeneralFunctions)
            {
                Uri? file = generalFunction.FilePath;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledOperator @operator in Operators)
            {
                Uri? file = @operator.FilePath;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledStruct @struct in Structs)
            {
                Uri? file = @struct.FilePath;
                if (file is not null && !alreadyExists.Contains(file))
                {
                    alreadyExists.Add(file);
                    yield return file;
                }
            }

            foreach (CompiledEnum @enum in Enums)
            {
                Uri? file = @enum.FilePath;
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
                for (int i = 0; i < topLevelStatements.Length; i++)
                { yield return topLevelStatements[i]; }
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

    public static CompilerResult Empty => new(
        Enumerable.Empty<KeyValuePair<Uri, CollectedAST>>(),
        Enumerable.Empty<CompiledFunction>(),
        Enumerable.Empty<CompiledGeneralFunction>(),
        Enumerable.Empty<CompiledOperator>(),
        Enumerable.Empty<CompiledConstructor>(),
        Enumerable.Empty<KeyValuePair<int, ExternalFunctionBase>>(),
        Enumerable.Empty<CompiledStruct>(),
        Enumerable.Empty<CompileTag>(),
        Enumerable.Empty<CompiledEnum>(),
        Enumerable.Empty<(ImmutableArray<Statement>, Uri?)>(),
        null);

    public CompilerResult(
        IEnumerable<KeyValuePair<Uri, CollectedAST>> tokens,
        IEnumerable<CompiledFunction> functions,
        IEnumerable<CompiledGeneralFunction> generalFunctions,
        IEnumerable<CompiledOperator> operators,
        IEnumerable<CompiledConstructor> constructors,
        IEnumerable<KeyValuePair<int, ExternalFunctionBase>> externalFunctions,
        IEnumerable<CompiledStruct> structs,
        IEnumerable<CompileTag> hashes,
        IEnumerable<CompiledEnum> enums,
        IEnumerable<(ImmutableArray<Statement> Statements, Uri? File)> topLevelStatements,
        Uri? file)
    {
        Raw = tokens.ToImmutableDictionary();
        Functions = functions.ToImmutableArray();
        GeneralFunctions = generalFunctions.ToImmutableArray();
        Operators = operators.ToImmutableArray();
        Constructors = constructors.ToImmutableArray();
        ExternalFunctions = externalFunctions.ToDictionary();
        Structs = structs.ToImmutableArray();
        Hashes = hashes.ToImmutableArray();
        Enums = enums.ToImmutableArray();
        TopLevelStatements = topLevelStatements.ToImmutableArray();
        File = file;
    }

    public static bool GetThingAt<TThing, TIdentifier>(IEnumerable<TThing> things, Uri file, SinglePosition position, [NotNullWhen(true)] out TThing? result)
        where TThing : IInFile, IIdentifiable<TIdentifier>
        where TIdentifier : IPositioned
    {
        foreach (TThing? thing in things)
        {
            if (thing.FilePath != file)
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

    public bool GetEnumAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledEnum? result)
        => GetThingAt<CompiledEnum, Token>(Enums, file, position, out result);

    public bool GetFieldAt(Uri file, SinglePosition position, [NotNullWhen(true)] out CompiledField? result)
    {
        foreach (CompiledStruct @struct in Structs)
        {
            if (@struct.FilePath != file) continue;

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
    readonly List<CompiledEnum> CompiledEnums = new();

    readonly List<FunctionDefinition> Operators = new();
    readonly List<FunctionDefinition> Functions = new();
    readonly List<StructDefinition> Structs = new();
    readonly List<EnumDefinition> Enums = new();

    readonly List<(ImmutableArray<Statement> Statements, Uri? File)> TopLevelStatements = new();

    readonly Stack<ImmutableArray<Token>> GenericParameters = new();

    readonly List<CompileTag> Tags = new();

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

    bool FindType(Token name, [NotNullWhen(true)] out GeneralType? result)
    {
        if (CodeGenerator.GetStruct(CompiledStructs, name.Content, out CompiledStruct? @struct, out _))
        {
            result = new StructType(@struct);
            return true;
        }

        if (CodeGenerator.GetEnum(CompiledEnums, name.Content, out CompiledEnum? @enum))
        {
            result = new EnumType(@enum);
            return true;
        }

        for (int i = 0; i < GenericParameters.Count; i++)
        {
            for (int j = 0; j < GenericParameters[i].Length; j++)
            {
                if (GenericParameters[i][j].Content == name.Content)
                {
                    GenericParameters[i][j].AnalyzedType = TokenAnalyzedType.TypeParameter;
                    result = new GenericType(GenericParameters[i][j]);
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

            CodeGenerator.FunctionQuery.Create<CompiledFunction, string, GeneralType>(name.Content),
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
        { throw new CompilerException($"Illegal struct name \"{@struct.Identifier.Content}\"", @struct.Identifier, @struct.FilePath); }

        @struct.Identifier.AnalyzedType = TokenAnalyzedType.Struct;

        if (CodeGenerator.GetStruct(CompiledStructs, @struct.Identifier.Content, out _, out _))
        { throw new CompilerException($"Struct \"{@struct.Identifier.Content}\" already exist", @struct.Identifier, @struct.FilePath); }

        if (@struct.Template is not null)
        {
            GenericParameters.Push(@struct.Template.Parameters);
            foreach (Token typeParameter in @struct.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        CompiledField[] compiledFields = new CompiledField[@struct.Fields.Length];

        for (int j = 0; j < @struct.Fields.Length; j++)
        {
            FieldDefinition field = @struct.Fields[j];
            CompiledField newField = new(GeneralType.From(field.Type, FindType), null! /* CompiledStruct constructor will set this */, field);
            compiledFields[j] = newField;
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

        if (function.Attributes.TryGetAttribute<string>(AttributeConstants.ExternalIdentifier, out string? externalName, out AttributeUsage? attribute))
        {
            if (!ExternalFunctions.TryGet(externalName, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                // AnalysisCollection?.Warnings.Add(exception.InstantiateWarning(attribute, function.FilePath));
            }
            else
            {
                if (externalFunction.Parameters.Length != function.Parameters.Count)
                { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.FilePath); }
                if (externalFunction.ReturnSomething != (type != BasicType.Void))
                { throw new CompilerException($"Wrong type defined for function '{externalFunction.ToReadable()}'", function.Type, function.FilePath); }

                for (int i = 0; i < externalFunction.Parameters.Length; i++)
                {
                    RuntimeType definedParameterType = externalFunction.Parameters[i];
                    GeneralType passedParameterType = GeneralType.From(function.Parameters[i].Type, FindType);
                    function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                    if (passedParameterType == definedParameterType)
                    { continue; }

                    throw new CompilerException($"Wrong type of parameter passed to function \"{externalFunction.ToReadable()}\". Parameter index: {i} Required type: {definedParameterType} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                }

                if (function.Template is not null)
                { GenericParameters.Pop(); }

                return new CompiledFunction(
                    type,
                    externalFunction.Parameters.Select(v => new BuiltinType(v)).ToArray(),
                    context,
                    function);
            }
        }

        if (function.Attributes.TryGetAttribute<string>(AttributeConstants.BuiltinIdentifier, out string? builtinName, out attribute))
        {
            if (!BuiltinFunctions.Prototypes.TryGetValue(builtinName, out (GeneralType ReturnValue, GeneralType[] Parameters) builtinFunction))
            {
                // AnalysisCollection?.Warnings.Add(new Warning($"Builtin function \"{builtinName}\" not found", attribute, function.FilePath));
            }
            else
            {
                if (builtinFunction.Parameters.Length != function.Parameters.Count)
                { throw new CompilerException($"Wrong number of parameters passed to function \"{builtinName}\"", function.Identifier, function.FilePath); }

                if (builtinFunction.ReturnValue != type)
                { throw new CompilerException($"Wrong type defined for function \"{builtinName}\"", function.Type, function.FilePath); }

                for (int i = 0; i < builtinFunction.Parameters.Length; i++)
                {
                    GeneralType definedParameterType = builtinFunction.Parameters[i];
                    GeneralType passedParameterType = GeneralType.From(function.Parameters[i].Type, FindType);
                    function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                    if (passedParameterType == definedParameterType)
                    { continue; }

                    if (passedParameterType is PointerType && definedParameterType is PointerType)
                    { continue; }

                    throw new CompilerException($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: {definedParameterType} Passed: {passedParameterType}", function.Parameters[i].Type, function.FilePath);
                }
            }
        }

        CompiledFunction result = new(
            type,
            GeneralType.FromArray(function.Parameters, FindType),
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

        if (function.Attributes.TryGetAttribute<string>(AttributeConstants.ExternalIdentifier, out string? name, out AttributeUsage? attribute))
        {
            if (ExternalFunctions.TryGet(name, out ExternalFunctionBase? externalFunction, out WillBeCompilerException? exception))
            {
                if (externalFunction.Parameters.Length != function.Parameters.Count)
                { throw new CompilerException($"Wrong number of parameters passed to function '{externalFunction.ToReadable()}'", function.Identifier, function.FilePath); }
                if (externalFunction.ReturnSomething != (type != BasicType.Void))
                { throw new CompilerException($"Wrong type defined for function '{externalFunction.ToReadable()}'", function.Type, function.FilePath); }

                for (int i = 0; i < externalFunction.Parameters.Length; i++)
                {
                    if (TypeKeywords.BasicTypes.TryGetValue(function.Parameters[i].Type.ToString(), out BasicType builtinType))
                    {
                        if (externalFunction.Parameters[i].Convert() != builtinType)
                        { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ToReadable()}'. Parameter index: {i} Required type: {externalFunction.Parameters[i]} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                    }
                    else
                    { throw new CompilerException($"Wrong type of parameter passed to function '{externalFunction.ToReadable()}'. Parameter index: {i} Required type: {externalFunction.Parameters[i]} Passed: {function.Parameters[i].Type}", function.Parameters[i].Type, function.FilePath); }
                }

                return new CompiledOperator(
                    type,
                    externalFunction.Parameters.Select(v => new BuiltinType(v)).ToArray(),
                    context,
                    function);
            }

            AnalysisCollection?.Errors.Add(exception.InstantiateError(attribute.Identifier, function.FilePath));
        }

        return new CompiledOperator(
            type,
            GeneralType.FromArray(function.Parameters, FindType),
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

    static CompiledEnumMember CompileEnumMember(EnumMemberDefinition member)
    {
        CompiledEnumMember compiledMember = new(member);

        if (!CodeGenerator.TryComputeSimple(member.Value, out DataItem computedValue))
        { throw new CompilerException($"I can't compute this. The developer should make a better preprocessor for this case I think...", member.Value, member.Context.FilePath); }

        compiledMember.ComputedValue = computedValue;

        return compiledMember;
    }

    static IEnumerable<CompiledEnumMember> CompileEnumMembers(IEnumerable<EnumMemberDefinition> members)
        => members.Select(CompileEnumMember);

    static CompiledEnum CompileEnum(EnumDefinition @enum)
        => new(CompileEnumMembers(@enum.Members), @enum);

    void AddAST(CollectedAST collectedAST, bool addTopLevelStatements = true)
    {
        if (addTopLevelStatements)
        { TopLevelStatements.Add((collectedAST.ParserResult.TopLevelStatements, collectedAST.Uri)); }

        Functions.AddRange(collectedAST.ParserResult.Functions);
        Operators.AddRange(collectedAST.ParserResult.Operators);
        Structs.AddRange(collectedAST.ParserResult.Structs);
        Enums.AddRange(collectedAST.ParserResult.Enums);
        Tags.AddRange(collectedAST.ParserResult.Hashes);
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
            Tags,
            CompiledEnums,
            TopLevelStatements,
            file);
    }

    CompilerResult CompileInteractiveInternal(Statement statement, UsingDefinition[] usings)
    {
        ImmutableDictionary<Uri, CollectedAST> files = SourceCodeManager.Collect(usings, null, PrintCallback, Settings.BasePath, AnalysisCollection, PreprocessorVariables, TokenizerSettings, null);

        foreach (CollectedAST file in files.Values)
        { AddAST(file); }

        TopLevelStatements.Add(([statement], null));

        CompileInternal();

        return new CompilerResult(
            files,
            CompiledFunctions,
            CompiledGeneralFunctions,
            CompiledOperators,
            CompiledConstructors,
            ExternalFunctions,
            CompiledStructs,
            Tags,
            CompiledEnums,
            TopLevelStatements,
            null);
    }

    void CompileTag(CompileTag tag)
    {
        switch (tag.Identifier.Content)
        {
            case "bf":
            {
                if (tag.Parameters.Length < 2)
                { AnalysisCollection?.Errors.Add(new Error($"Compile tag \"{tag.Identifier}\" requires minimum 2 parameter", tag.Identifier, tag.FilePath)); break; }
                string name = tag.Parameters[0].Value;

                if (ExternalFunctions.TryGet(name, out _, out _)) break;

                string[] bfParams = new string[tag.Parameters.Length - 1];
                for (int i = 1; i < tag.Parameters.Length; i++)
                { bfParams[i - 1] = tag.Parameters[i].Value; }

                BasicType[] parameterTypes = new BasicType[bfParams.Length];
                for (int i = 0; i < bfParams.Length; i++)
                {
                    if (TypeKeywords.BasicTypes.TryGetValue(bfParams[i], out BasicType paramType))
                    {
                        parameterTypes[i] = paramType;

                        if (paramType == BasicType.Void && i > 0)
                        {
                            AnalysisCollection?.Errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", tag.Parameters[i + 1], tag.FilePath));
                            return;
                        }
                    }
                    else
                    {
                        AnalysisCollection?.Errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", tag.Parameters[i + 1], tag.FilePath));
                        return;
                    }
                }

                BasicType returnType = parameterTypes[0];
                List<BasicType> x = parameterTypes.ToList();
                x.RemoveAt(0);
                RuntimeType[] pTypes = x.ToArray().Select(v => v.Convert()).ToArray();

                if (returnType == BasicType.Void)
                {
                    ExternalFunctions.AddSimpleExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                        Output.LogDebug($"{name}({string.Join(", ", p)})")
                    );
                }
                else
                {
                    ExternalFunctions.AddSimpleExternalFunction(name, pTypes, (BytecodeProcessor sender, DataItem[] p) =>
                    {
                        Output.LogDebug($"{name}({string.Join(", ", p)})");
                        return DataItem.GetDefaultValue(returnType);
                    });
                }
            }
            break;
            default:
                AnalysisCollection?.Warnings.Add(new Warning($"Hash \"{tag.Identifier}\" does not exists, so this is ignored", tag.Identifier, tag.FilePath));
                break;
        }
    }

    void CompileInternal()
    {
        static bool ThingEquality<TThing1, TThing2>(TThing1 a, TThing2 b)
            where TThing1 : IIdentifiable<Token>, IInFile
            where TThing2 : IIdentifiable<Token>, IInFile
        {
            if (!a.Identifier.Content.Equals(b.Identifier.Content)) return false;
            if (a.FilePath != b.FilePath) return false;
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
            if (CompiledEnums.Any(other => ThingEquality(other, thing)))
            { return true; }

            if (CompiledStructs.Any(other => ThingEquality(other, thing)))
            { return true; }

            if (CompiledFunctions.Any(other => ThingEquality(other, thing)))
            { return true; }

            return false;
        }

        foreach (CompileTag tag in Tags)
        {
            CompileTag(tag);
        }

        foreach (EnumDefinition @enum in Enums)
        {
            if (IsThingExists(@enum))
            { throw new CompilerException("Symbol already exists", @enum.Identifier, @enum.FilePath); }

            CompiledEnums.Add(CompileEnum(@enum));
        }

        foreach (StructDefinition @struct in Structs)
        {
            if (IsThingExists(@struct))
            { throw new CompilerException("Symbol already exists", @struct.Identifier, @struct.FilePath); }

            CompiledStructs.Add(CompileStruct(@struct));
        }

        foreach (FunctionDefinition @operator in Operators)
        {
            CompiledOperator compiled = CompileOperator(@operator, null);

            if (CompiledOperators.Any(other => FunctionEquality(compiled, other)))
            { throw new CompilerException($"Operator {compiled.ToReadable()} already defined", @operator.Identifier, @operator.FilePath); }

            CompiledOperators.Add(compiled);
        }

        foreach (FunctionDefinition function in Functions)
        {
            CompiledFunction compiled = CompileFunction(function, null);

            if (CompiledFunctions.Any(other => FunctionEquality(compiled, other)))
            { throw new CompilerException($"Function {compiled.ToReadable()} already defined", function.Identifier, function.FilePath); }

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
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                }

                GeneralType returnType = new StructType(compiledStruct);

                if (method.Identifier.Content == BuiltinFunctionIdentifiers.Destructor)
                {
                    List<ParameterDefinition> parameters = method.Parameters.ToList();
                    parameters.Insert(0, new ParameterDefinition(
                        new Token[] { Token.CreateAnonymous(ModifierKeywords.Ref), Token.CreateAnonymous(ModifierKeywords.This) },
                        TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.Template?.Parameters),
                        Token.CreateAnonymous(StatementKeywords.This),
                        method)
                    );

                    GeneralFunctionDefinition copy = new(
                        method.Identifier,
                        method.Modifiers,
                        new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                        method.FilePath)
                    {
                        Context = method.Context,
                        Block = method.Block,
                    };

                    returnType = new BuiltinType(BasicType.Void);

                    CompiledGeneralFunction methodWithRef = CompileGeneralFunction(copy, returnType, compiledStruct);

                    parameters = method.Parameters.ToList();
                    parameters.Insert(0, new ParameterDefinition(
                        new Token[] { Token.CreateAnonymous(ModifierKeywords.This) },
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.Template?.Parameters)),
                        Token.CreateAnonymous(StatementKeywords.This),
                        method)
                    );

                    copy = new GeneralFunctionDefinition(
                        method.Identifier,
                        method.Modifiers,
                        new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                        method.FilePath)
                    {
                        Context = method.Context,
                        Block = method.Block,
                    };

                    CompiledGeneralFunction methodWithPointer = CompileGeneralFunction(copy, returnType, compiledStruct);

                    if (CompiledGeneralFunctions.Any(methodWithRef.IsSame))
                    { throw new CompilerException($"Function with name \'{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.FilePath); }

                    if (CompiledGeneralFunctions.Any(methodWithPointer.IsSame))
                    { throw new CompilerException($"Function with name \'{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.FilePath); }

                    CompiledGeneralFunctions.Add(methodWithRef);
                    CompiledGeneralFunctions.Add(methodWithPointer);
                }
                else
                {
                    List<ParameterDefinition> parameters = method.Parameters.ToList();

                    CompiledGeneralFunction methodWithRef = CompileGeneralFunction(method, returnType, compiledStruct);

                    if (CompiledGeneralFunctions.Any(methodWithRef.IsSame))
                    { throw new CompilerException($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.FilePath); }

                    CompiledGeneralFunctions.Add(methodWithRef);
                }
            }

            foreach (FunctionDefinition method in compiledStruct.Functions)
            {
                foreach (ParameterDefinition parameter in method.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                }

                List<ParameterDefinition> parameters = method.Parameters.ToList();
                parameters.Insert(0, new ParameterDefinition(
                    new Token[] { Token.CreateAnonymous(ModifierKeywords.Ref), Token.CreateAnonymous(ModifierKeywords.This) },
                    TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.Template?.Parameters),
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
                    method.FilePath)
                {
                    Context = method.Context,
                    Block = method.Block,
                };

                CompiledFunction methodWithRef = CompileFunction(copy, compiledStruct);

                parameters = method.Parameters.ToList();
                parameters.Insert(0,
                    new ParameterDefinition(
                        new Token[] { Token.CreateAnonymous(ModifierKeywords.This) },
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, compiledStruct.Template?.Parameters)),
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
                    method.FilePath)
                {
                    Context = method.Context,
                    Block = method.Block,
                };

                CompiledFunction methodWithPointer = CompileFunction(copy, compiledStruct);

                if (CompiledFunctions.Any(methodWithRef.IsSame))
                { throw new CompilerException($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.FilePath); }

                if (CompiledFunctions.Any(methodWithPointer.IsSame))
                { throw new CompilerException($"Function with name \"{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.FilePath); }

                CompiledFunctions.Add(methodWithRef);
                CompiledFunctions.Add(methodWithPointer);
            }

            foreach (ConstructorDefinition constructor in compiledStruct.Constructors)
            {
                foreach (ParameterDefinition parameter in constructor.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { throw new CompilerException($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.FilePath); }
                }

                if (constructor.Type is TypeInstancePointer)
                {
                    List<ParameterDefinition> parameters = constructor.Parameters.ToList();
                    parameters.Insert(0,
                        new ParameterDefinition(
                            new Token[] { Token.CreateAnonymous(ModifierKeywords.This) },
                            constructor.Type,
                            Token.CreateAnonymous(StatementKeywords.This),
                            constructor)
                        );

                    ConstructorDefinition constructorDefWithNothing = new(
                        constructor.Type,
                        constructor.Modifiers,
                        new ParameterDefinitionCollection(parameters, constructor.Parameters.Brackets),
                        constructor.FilePath)
                    {
                        Block = constructor.Block,
                        Context = constructor.Context,
                    };

                    CompiledConstructor constructorWithNothing = CompileConstructor(constructorDefWithNothing, compiledStruct);

                    if (CompiledConstructors.Any(constructorWithNothing.IsSame))
                    { throw new CompilerException($"Constructor with name '{constructorWithNothing.ToReadable()}' already defined", constructor.Type, compiledStruct.FilePath); }

                    CompiledConstructors.Add(constructorWithNothing);
                }
                else
                {
                    List<ParameterDefinition> parameters = constructor.Parameters.ToList();
                    parameters.Insert(0,
                        new ParameterDefinition(
                            new Token[] { Token.CreateAnonymous(ModifierKeywords.This), Token.CreateAnonymous(ModifierKeywords.Ref) },
                            constructor.Type,
                            Token.CreateAnonymous(StatementKeywords.This),
                            constructor)
                        );

                    ConstructorDefinition constructorDefWithRef = new(
                        constructor.Type,
                        constructor.Modifiers,
                        new ParameterDefinitionCollection(parameters, constructor.Parameters.Brackets),
                        constructor.FilePath)
                    {
                        Block = constructor.Block,
                        Context = constructor.Context,
                    };

                    CompiledConstructor constructorWithRef = CompileConstructor(constructorDefWithRef, compiledStruct);

                    if (CompiledConstructors.Any(constructorWithRef.IsSame))
                    { throw new CompilerException($"Constructor with name \"{constructorWithRef.ToReadable()}\" already defined", constructor.Type, compiledStruct.FilePath); }

                    CompiledConstructors.Add(constructorWithRef);

                    parameters = constructor.Parameters.ToList();
                    parameters.Insert(0,
                        new ParameterDefinition(
                            new Token[] { Token.CreateAnonymous(ModifierKeywords.This) },
                            TypeInstancePointer.CreateAnonymous(constructor.Type),
                            Token.CreateAnonymous(StatementKeywords.This),
                            constructor)
                        );

                    ConstructorDefinition constructorDefWithPointer = new(
                        constructor.Type,
                        constructor.Modifiers,
                        new ParameterDefinitionCollection(parameters, constructor.Parameters.Brackets),
                        constructor.FilePath)
                    {
                        Block = constructor.Block,
                        Context = constructor.Context,
                    };

                    CompiledConstructor constructorWithPointer = CompileConstructor(constructorDefWithPointer, compiledStruct);

                    if (CompiledConstructors.Any(constructorWithPointer.IsSame))
                    { throw new CompilerException($"Constructor with name '{constructorWithPointer.ToReadable()}' already defined", constructor.Type, compiledStruct.FilePath); }

                    CompiledConstructors.Add(constructorWithPointer);
                }
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
        PrintCallback? printCallback,
        AnalysisCollection? analysisCollection,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser)
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
        PrintCallback? printCallback,
        AnalysisCollection? analysisCollection,
        TokenizerSettings? tokenizerSettings,
        FileParser? fileParser)
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
        UsingDefinition[] usings,
        IEnumerable<string> preprocessorVariables,
        PrintCallback? printCallback,
        AnalysisCollection? analysisCollection,
        TokenizerSettings? tokenizerSettings)
    {
        Compiler compiler = new(
            externalFunctions,
            printCallback,
            settings,
            analysisCollection,
            preprocessorVariables,
            tokenizerSettings);
        return compiler.CompileInteractiveInternal(statement, usings);
    }
}
