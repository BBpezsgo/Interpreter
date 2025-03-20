using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public sealed class Compiler
{
    readonly List<CompiledStruct> CompiledStructs = new();
    readonly List<CompiledOperator> CompiledOperators = new();
    readonly List<CompiledConstructor> CompiledConstructors = new();
    readonly List<CompiledFunction> CompiledFunctions = new();
    readonly List<CompiledGeneralFunction> CompiledGeneralFunctions = new();
    readonly List<CompiledAlias> CompiledAliases = new();

    readonly List<FunctionDefinition> Operators = new();
    readonly List<FunctionDefinition> Functions = new();
    readonly List<StructDefinition> Structs = new();
    readonly List<AliasDefinition> AliasDefinitions = new();

    readonly List<(ImmutableArray<Statement> Statements, Uri File)> TopLevelStatements = new();

    readonly Stack<ImmutableArray<Token>> GenericParameters = new();

    readonly CompilerSettings Settings;
    readonly ImmutableArray<IExternalFunction> ExternalFunctions;
    readonly ImmutableArray<UserDefinedAttribute> UserDefinedAttributes;

    readonly DiagnosticsCollection Diagnostics;
    readonly IEnumerable<string> PreprocessorVariables;

    Compiler(CompilerSettings settings, DiagnosticsCollection diagnostics, IEnumerable<string> preprocessorVariables, IEnumerable<UserDefinedAttribute>? userDefinedAttributes)
    {
        ExternalFunctions = settings.ExternalFunctions;
        Settings = settings;
        Diagnostics = diagnostics;
        PreprocessorVariables = preprocessorVariables;
        UserDefinedAttributes = (userDefinedAttributes ?? Enumerable.Empty<UserDefinedAttribute>()).ToImmutableArray();
    }

    bool FindType(Token name, Uri relevantFile, [NotNullWhen(true)] out GeneralType? result)
    {
        if (StatementCompiler.GetAlias(CompiledAliases, name.Content, relevantFile, out CompiledAlias? alias, out _))
        {
            // HERE
            result = new AliasType(alias.Value, alias);
            return true;
        }

        if (StatementCompiler.GetStruct(CompiledStructs, name.Content, relevantFile, out CompiledStruct? @struct, out _))
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

        if (StatementCompiler.GetFunction<CompiledFunction, Token, string, GeneralType>(
            new StatementCompiler.Functions<CompiledFunction>()
            {
                Compiled = CompiledFunctions,
                Compilable = Enumerable.Empty<StatementCompiler.CompliableTemplate<CompiledFunction>>(),
            },
            "function",
            null,

            StatementCompiler.FunctionQuery.Create<CompiledFunction, string, GeneralType>(name.Content, null, (v, _) => v, relevantFile),
            out StatementCompiler.FunctionQueryResult<CompiledFunction>? result_,
            out _
        ))
        {
            result = new FunctionType(result_.Function);
            return true;
        }

        result = null;
        return false;
    }

    CompiledStruct CompileStructNoFields(StructDefinition @struct)
    {
        if (LanguageConstants.KeywordList.Contains(@struct.Identifier.Content))
        { Diagnostics.Add(Diagnostic.Critical($"Illegal struct name \"{@struct.Identifier.Content}\"", @struct.Identifier, @struct.File)); }

        @struct.Identifier.AnalyzedType = TokenAnalyzedType.Struct;

        if (@struct.Template is not null)
        {
            GenericParameters.Push(@struct.Template.Parameters);
            foreach (Token typeParameter in @struct.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        if (@struct.Template is not null)
        { GenericParameters.Pop(); }

        // Oh no wtf
        return new CompiledStruct(@struct.Fields.Select(v => new CompiledField(BuiltinType.Any, null!, v)), @struct);
    }

    void CompileFunctionAttributes(FunctionThingDefinition function)
    {
        GeneralType? type = null;
        TypeInstance? typeInstance = null;
        if (function is IHaveType haveType)
        {
            type = GeneralType.From(typeInstance = haveType.Type, FindType);
        }
        GeneralType[] parameterTypes = GeneralType.FromArray(function.Parameters, FindType).ToArray();

        foreach (AttributeUsage attribute in function.Attributes)
        {
            switch (attribute.Identifier.Content)
            {
                case AttributeConstants.ExternalIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of parameters passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
                        break;
                    }

                    if (attribute.Parameters[0].Type != LiteralType.String)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Invalid parameter type \"{attribute.Parameters[0].Type}\" for attribute \"{attribute.Identifier}\" at {0}: expected \"{LiteralType.String}\"", attribute));
                        break;
                    }

                    string externalName = attribute.Parameters[0].Value;

                    if (!ExternalFunctions.TryGet(externalName, out IExternalFunction? externalFunction, out PossibleDiagnostic? exception))
                    {
                        Diagnostics.Add(exception.ToWarning(attribute, function.File));
                        break;
                    }

                    // CheckExternalFunctionDeclaration(this, function, externalFunction, type, parameterTypes);

                    break;
                }
                case AttributeConstants.BuiltinIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of parameters passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
                        break;
                    }

                    if (attribute.Parameters[0].Type != LiteralType.String)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Invalid parameter type \"{attribute.Parameters[0].Type}\" for attribute \"{attribute.Identifier}\" at {0}: expected \"{LiteralType.String}\"", attribute));
                        break;
                    }

                    string builtinName = attribute.Parameters[0].Value;

                    if (!BuiltinFunctions.Prototypes.TryGetValue(builtinName, out BuiltinFunction? builtinFunction))
                    {
                        Diagnostics.Add(Diagnostic.Warning($"Builtin function \"{builtinName}\" not found", attribute, function.File));
                        break;
                    }

                    if (builtinFunction.Parameters.Length != function.Parameters.Count)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Wrong number of parameters passed to function \"{builtinName}\"", function.Identifier, function.File));
                    }

                    if (type is null)
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Can't use attribute \"{attribute.Identifier}\" on this function because its aint have a return type", typeInstance, function.File));
                        continue;
                    }

                    if (!builtinFunction.Type.Invoke(type))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Wrong type defined for function \"{builtinName}\"", typeInstance, function.File));
                    }

                    for (int i = 0; i < builtinFunction.Parameters.Length; i++)
                    {
                        if (i >= function.Parameters.Count) break;

                        Predicate<GeneralType> definedParameterType = builtinFunction.Parameters[i];
                        GeneralType passedParameterType = parameterTypes[i];
                        function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                        if (definedParameterType.Invoke(passedParameterType))
                        { continue; }

                        Diagnostics.Add(Diagnostic.Critical($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: \"{definedParameterType}\" Passed: \"{passedParameterType}\"", function.Parameters[i].Type, function.Parameters[i].File));
                    }
                    break;
                }
                case AttributeConstants.ExposeIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of parameters passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
                        break;
                    }

                    if (attribute.Parameters[0].Type != LiteralType.String)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Invalid parameter type \"{attribute.Parameters[0].Type}\" for attribute \"{attribute.Identifier}\" at {0}: expected \"{LiteralType.String}\"", attribute));
                        break;
                    }

                    break;
                }
                default:
                {
                    CompileUserAttribute(function, attribute);
                    break;
                }
            }
        }
    }

    void CompileUserAttribute(IHaveAttributes context, AttributeUsage attribute)
    {
        foreach (UserDefinedAttribute userDefinedAttribute in UserDefinedAttributes)
        {
            if (userDefinedAttribute.Name != attribute.Identifier.Content) continue;

            if (!userDefinedAttribute.CanUseOn.HasFlag(CanUseOn.Function))
            { Diagnostics.Add(Diagnostic.Error($"Can't use attribute \"{attribute.Identifier}\" on \"{context.GetType().Name}\". Valid usages: {userDefinedAttribute.CanUseOn}", attribute)); }

            if (attribute.Parameters.Length != userDefinedAttribute.Parameters.Length)
            {
                Diagnostics.Add(Diagnostic.Error($"Wrong number of parameters passed to attribute \"{attribute.Identifier}\": required {userDefinedAttribute.Parameters.Length}, passed {attribute.Parameters.Length}", attribute));
                break;
            }

            for (int i = 0; i < attribute.Parameters.Length; i++)
            {
                if (attribute.Parameters[i].Type != userDefinedAttribute.Parameters[i])
                {
                    Diagnostics.Add(Diagnostic.Error($"Invalid parameter type \"{attribute.Parameters[i].Type}\" for attribute \"{attribute.Identifier}\" at {i}: expected \"{userDefinedAttribute.Parameters[i]}\"", attribute));
                }
            }

            if (userDefinedAttribute.Verifier is not null &&
                !userDefinedAttribute.Verifier.Invoke(context, attribute, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(attribute));
            }

            break;
        }
    }

    void CompileStructFields(CompiledStruct @struct)
    {
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

        @struct.SetFields(compiledFields);
    }

    public static void CheckExternalFunctionDeclaration<TFunction>(IRuntimeInfoProvider runtime, TFunction definition, IExternalFunction externalFunction, DiagnosticsCollection diagnostics)
        where TFunction : FunctionThingDefinition, ICompiledFunction
        => CheckExternalFunctionDeclaration(runtime, definition, externalFunction, definition.Type, definition.ParameterTypes, diagnostics);

    public static void CheckExternalFunctionDeclaration(IRuntimeInfoProvider runtime, FunctionThingDefinition definition, IExternalFunction externalFunction, GeneralType returnType, IReadOnlyList<GeneralType> parameterTypes, DiagnosticsCollection diagnostics)
    {
        if (externalFunction.ParametersSize != parameterTypes.Sum(v => v.SameAs(BasicType.Void) ? 0 : v.GetSize(runtime)))
        {
            diagnostics?.Add(Diagnostic.Critical($"Wrong size of parameters defined for external function \"{externalFunction.ToReadable()}\"", definition.Identifier, definition.File));
            return;
        }

        if (externalFunction.ReturnValueSize != (returnType.SameAs(BasicType.Void) ? 0 : returnType.GetSize(runtime)))
        {
            diagnostics?.Add(Diagnostic.Critical($"Wrong size of return type defined for external function \"{externalFunction.ToReadable()}\"", definition.Identifier, definition.File));
            return;
        }
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

        CompileFunctionAttributes(function);

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

        GeneralType[] parameterTypes = GeneralType.FromArray(function.Parameters, FindType).ToArray();

        CompileFunctionAttributes(function);

        return new CompiledOperator(
            type,
            parameterTypes,
            context,
            function);
    }

    CompiledGeneralFunction CompileGeneralFunction(GeneralFunctionDefinition function, GeneralType returnType, CompiledStruct context)
    {
        CompileFunctionAttributes(function);

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

        CompileFunctionAttributes(function);

        CompiledConstructor result = new(
            type,
            GeneralType.FromArray(function.Parameters, FindType),
            context,
            function);

        if (function.Template is not null)
        { GenericParameters.Pop(); }

        return result;
    }

    void AddAST(ParsedFile collectedAST, bool addTopLevelStatements = true)
    {
        if (addTopLevelStatements)
        { TopLevelStatements.Add((collectedAST.AST.TopLevelStatements, collectedAST.File)); }

        Functions.AddRange(collectedAST.AST.Functions);
        Operators.AddRange(collectedAST.AST.Operators);
        Structs.AddRange(collectedAST.AST.Structs);
        AliasDefinitions.AddRange(collectedAST.AST.AliasDefinitions);
    }

    CompilerResult2 CompileMainFile(Uri file, FileParser? fileParser, IEnumerable<string>? additionalImports)
    {
        ImmutableArray<ParsedFile> parsedFiles = SourceCodeManager.Collect(file, Settings.BasePath, Diagnostics, PreprocessorVariables, fileParser, additionalImports);

        foreach (ParsedFile parsedFile in parsedFiles)
        { AddAST(parsedFile, parsedFile.File != file); }

        foreach (ParsedFile parsedFile in parsedFiles)
        {
            if (parsedFile.File != file) continue;
            TopLevelStatements.Add((parsedFile.AST.TopLevelStatements, parsedFile.File));
        }

        return CompileInternal(file, parsedFiles);
    }

    CompilerResult2 CompileInteractiveInternal(Statement statement, Uri file)
    {
        ImmutableArray<ParsedFile> parsedFiles = SourceCodeManager.Collect(
            null,
            Settings.BasePath,
            Diagnostics,
            PreprocessorVariables,
            null,
            new string[] { "Primitives", "System" }
        );

        foreach (ParsedFile parsedFile in parsedFiles)
        { AddAST(parsedFile); }

        TopLevelStatements.Add((ImmutableArray.Create(statement), file));

        return CompileInternal(file, parsedFiles);
    }

    CompilerResult2 CompileInternal(Uri file, ImmutableArray<ParsedFile> parsedFiles)
    {
        static bool ThingEquality<TThing1, TThing2>(TThing1 a, TThing2 b)
            where TThing1 : IIdentifiable<Token>, IInFile
            where TThing2 : IIdentifiable<Token>, IInFile
        {
            if (a.Identifier.Content != b.Identifier.Content) return false;
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

            if (CompiledAliases.Any(other => ThingEquality(other, thing)))
            { return true; }

            return false;
        }

        foreach (AliasDefinition aliasDefinition in AliasDefinitions)
        {
            if (IsThingExists(@aliasDefinition))
            {
                Diagnostics.Add(Diagnostic.Critical("Symbol already exists", @aliasDefinition.Identifier, @aliasDefinition.File));
                continue;
            }

            CompiledAlias alias = new(
                GeneralType.From(aliasDefinition.Value, FindType),
                aliasDefinition
            );
            aliasDefinition.Value.SetAnalyzedType(alias.Value);
            CompiledAliases.Add(alias);
        }

        // First compile the structs without fields
        // so it can reference other structs that are
        // not compiled but will be.
        foreach (StructDefinition @struct in Structs)
        {
            if (IsThingExists(@struct))
            {
                Diagnostics.Add(Diagnostic.Critical("Symbol already exists", @struct.Identifier, @struct.File));
                continue;
            }

            CompiledStructs.Add(CompileStructNoFields(@struct));
        }

        // Now compile the fields. Now every struct is compiled
        // so it can reference other structs.
        foreach (CompiledStruct @struct in CompiledStructs)
        {
            CompileStructFields(@struct);
        }

        foreach (CompiledStruct @struct in CompiledStructs)
        {
            foreach (AttributeUsage attribute in @struct.Attributes)
            {
                CompileUserAttribute(@struct, attribute);
            }

            foreach (CompiledField field in @struct.Fields)
            {
                foreach (AttributeUsage attribute in field.Attributes)
                {
                    CompileUserAttribute(field, attribute);
                }
            }
        }

        foreach (FunctionDefinition @operator in Operators)
        {
            CompiledOperator compiled = CompileOperator(@operator, null);

            if (CompiledOperators.Any(other => FunctionEquality(compiled, other)))
            {
                Diagnostics.Add(Diagnostic.Critical($"Operator \"{compiled.ToReadable()}\" already defined", @operator.Identifier, @operator.File));
                continue;
            }

            CompiledOperators.Add(compiled);
        }

        foreach (FunctionDefinition function in Functions)
        {
            CompiledFunction compiled = CompileFunction(function, null);

            if (CompiledFunctions.Any(other => FunctionEquality(compiled, other)))
            {
                Diagnostics.Add(Diagnostic.Critical($"Function \"{compiled.ToReadable()}\" already defined", function.Identifier, function.File));
                continue;
            }

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
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File));
                        continue;
                    }
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
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters), method.File),
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
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.File));
                        continue;
                    }

                    if (CompiledGeneralFunctions.Any(methodWithPointer.IsSame))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Function with name \"{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.File));
                        continue;
                    }

                    CompiledGeneralFunctions.Add(methodWithRef);
                    CompiledGeneralFunctions.Add(methodWithPointer);
                }
                else
                {
                    List<ParameterDefinition> parameters = method.Parameters.ToList();

                    CompiledGeneralFunction methodWithRef = CompileGeneralFunction(method, returnType, compiledStruct);

                    if (CompiledGeneralFunctions.Any(methodWithRef.IsSame))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Function with name \"{methodWithRef.ToReadable()}\" already defined", method.Identifier, compiledStruct.File));
                        continue;
                    }

                    CompiledGeneralFunctions.Add(methodWithRef);
                }
            }

            foreach (FunctionDefinition method in compiledStruct.Functions)
            {
                foreach (ParameterDefinition parameter in method.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File)); }
                }

                List<ParameterDefinition> parameters = method.Parameters.ToList();
                parameters.Insert(0,
                    new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters), method.File),
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

                CompiledFunction methodWithPointer = CompileFunction(copy, compiledStruct);

                if (CompiledFunctions.Any(methodWithPointer.IsSame))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Function with name \"{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.File));
                    continue;
                }

                CompiledFunctions.Add(methodWithPointer);
            }

            foreach (ConstructorDefinition constructor in compiledStruct.Constructors)
            {
                foreach (ParameterDefinition parameter in constructor.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File));
                        continue;
                    }
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
                {
                    Diagnostics.Add(Diagnostic.Critical($"Constructor \"{compiledConstructor.ToReadable()}\" already defined", constructor.Type, compiledStruct.File));
                    continue;
                }

                CompiledConstructors.Add(compiledConstructor);
            }

            if (compiledStruct.Template is not null)
            { GenericParameters.Pop(); }
        }

        return StatementCompiler.Compile(new CompilerResult(
            parsedFiles,
            CompiledFunctions,
            CompiledGeneralFunctions,
            CompiledOperators,
            CompiledConstructors,
            CompiledAliases,
            ExternalFunctions,
            CompiledStructs,
            TopLevelStatements,
            file,
            false), Settings, null, Diagnostics);
    }

    public static CompilerResult2 CompileFile(
        Uri file,
        CompilerSettings settings,
        IEnumerable<string> preprocessorVariables,
        DiagnosticsCollection diagnostics,
        FileParser? fileParser = null,
        IEnumerable<string>? additionalImports = null,
        IEnumerable<UserDefinedAttribute>? userDefinedAttributes = null)
    {
        Compiler compiler = new(
            settings,
            diagnostics,
            preprocessorVariables,
            userDefinedAttributes);
        return compiler.CompileMainFile(file, fileParser, additionalImports);
    }

    public static CompilerResult2 CompileInteractive(
        Statement statement,
        CompilerSettings settings,
        IEnumerable<string> preprocessorVariables,
        DiagnosticsCollection diagnostics,
        Uri file,
        IEnumerable<UserDefinedAttribute>? userDefinedAttributes = null)
    {
        Compiler compiler = new(
            settings,
            diagnostics,
            preprocessorVariables,
            userDefinedAttributes);
        return compiler.CompileInteractiveInternal(statement, file);
    }
}
