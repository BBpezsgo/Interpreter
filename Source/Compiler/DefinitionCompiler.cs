using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
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
        return new CompiledStruct(@struct.Fields.ToImmutableArray(v => new CompiledField(BuiltinType.Any, null!, v)), @struct);
    }

    void CompileStructFields(CompiledStruct @struct)
    {
        if (@struct.Template is not null)
        {
            GenericParameters.Push(@struct.Template.Parameters);
            foreach (Token typeParameter in @struct.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        ImmutableArray<CompiledField>.Builder compiledFields = ImmutableArray.CreateBuilder<CompiledField>(@struct.Fields.Length);

        for (int i = 0; i < @struct.Fields.Length; i++)
        {
            FieldDefinition field = @struct.Fields[i];

            if (!GeneralType.From(field.Type, FindType, out GeneralType? fieldType, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field.Type));
                continue;
            }
            compiledFields.Add(new CompiledField(fieldType, null! /* CompiledStruct constructor will set this */, field));
        }

        if (@struct.Template is not null)
        { GenericParameters.Pop(); }

        @struct.SetFields(compiledFields.MoveToImmutable());
    }

    void CompileFunctionAttributes(FunctionThingDefinition function)
    {
        GeneralType? type = null;
        TypeInstance? typeInstance = null;
        if (function is IHaveType haveType)
        {
            if (!GeneralType.From(typeInstance = haveType.Type, FindType, out type, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(typeInstance));
                return;
            }
        }

        foreach (AttributeUsage attribute in function.Attributes)
        {
            switch (attribute.Identifier.Content)
            {
                case AttributeConstants.ExternalIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
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
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
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
                        Diagnostics.Add(Diagnostic.Critical($"Wrong number of arguments passed to function \"{builtinName}\"", function.Identifier, function.File));
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
                        if (!GeneralType.From(function.Parameters.Parameters[i].Type, FindType, out GeneralType? passedParameterType, out PossibleDiagnostic? error))
                        {
                            Diagnostics.Add(error.ToError(function.Parameters.Parameters[i].Type));
                            continue;
                        }
                        function.Parameters[i].Type.SetAnalyzedType(passedParameterType);

                        if (definedParameterType.Invoke(passedParameterType))
                        { continue; }

                        Diagnostics.Add(Diagnostic.Critical($"Wrong type of parameter passed to function \"{builtinName}\". Parameter index: {i} Required type: \"{definedParameterType}\" Passed: \"{passedParameterType}\"", function.Parameters[i].Type, function.Parameters[i].File));
                    }
                    break;
                }
                case AttributeConstants.ExposeIdentifier:
                {
                    if (attribute.Parameters.Length == 0)
                    {
                        break;
                    }

                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
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

    void CompileVariableAttributes(VariableDeclaration function)
    {
        foreach (AttributeUsage attribute in function.Attributes)
        {
            switch (attribute.Identifier.Content)
            {
                case AttributeConstants.ExternalIdentifier:
                {
                    if (attribute.Parameters.Length != 1)
                    {
                        Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to attribute \"{attribute.Identifier}\": required {1}, passed {attribute.Parameters.Length}", attribute));
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
                Diagnostics.Add(Diagnostic.Error($"Wrong number of arguments passed to attribute \"{attribute.Identifier}\": required {userDefinedAttribute.Parameters.Length}, passed {attribute.Parameters.Length}", attribute));
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

    public static void CheckExternalFunctionDeclaration<TFunction>(IRuntimeInfoProvider runtime, TFunction definition, IExternalFunction externalFunction, DiagnosticsCollection diagnostics)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition
    {
        CheckExternalFunctionDeclaration(runtime, definition, externalFunction, definition.Type, ((ICompiledFunctionDefinition)definition).Parameters.ToImmutableArray(v => v.Type), diagnostics);
    }

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

    bool CompileFunctionDefinition(FunctionDefinition function, CompiledStruct? context, [NotNullWhen(true)] out CompiledFunctionDefinition? result)
    {
        result = null;

        if (function.Template is not null)
        {
            GenericParameters.Push(function.Template.Parameters);
            foreach (Token typeParameter in function.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        if (!GeneralType.From(function.Type, FindType, out GeneralType? type, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(function.Type));
            return false;
        }

        ImmutableArray<CompiledParameter>.Builder parameters = ImmutableArray.CreateBuilder<CompiledParameter>(function.Parameters.Count);
        foreach (ParameterDefinition item in function.Parameters.Parameters)
        {
            if (!GeneralType.From(item.Type, FindType, out var parameterType, out typeError))
            {
                Diagnostics.Add(typeError.ToError(item.Type));
                return false;
            }
            parameters.Add(new CompiledParameter(parameterType, item));
        }

        CompileFunctionAttributes(function);

        result = new(
            type,
            parameters.MoveToImmutable(),
            context,
            function
        );

        /*
        if (type.Is(out StructType? structType) &&
            structType.Struct == GeneratorStructDefinition?.Struct)
        {
            List<GeneralType> _parameterTypes = new(parameterTypes.Length + 1)
            {
                new PointerType(new StructType(GetGeneratorState(function).Struct, function.File))
            };
            _parameterTypes.AddRange(parameterTypes);
            parameterTypes = _parameterTypes.ToArray();

            result = new(
                type,
                parameterTypes,
                context,
                new FunctionDefinition(
                    result.Attributes,
                    result.Modifiers,
                    result.TypeToken,
                    result.Identifier,
                    new ParameterDefinitionCollection(
                        Enumerable.Empty<ParameterDefinition>()
                        .Append(new ParameterDefinition(
                            new Token[] { Token.CreateAnonymous(ModifierKeywords.This) },
                            new TypeInstancePointer(new TypeInstanceSimple(Token.CreateAnonymous(GetGeneratorState(function).Struct.Identifier.Content), function.File), Token.CreateAnonymous("*"), function.File),
                            Token.CreateAnonymous("this"),
                            null
                        ))
                        .Append(result.Parameters.Select(v => v))
                        .ToArray(),
                        result.Parameters.Brackets),
                    result.Template,
                    result.File)
            );
        }
        */

        if (function.Template is not null)
        { GenericParameters.Pop(); }

        return true;
    }

    bool CompileOperatorDefinition(FunctionDefinition function, CompiledStruct? context, [NotNullWhen(true)] out CompiledOperatorDefinition? result)
    {
        result = null;

        if (!GeneralType.From(function.Type, FindType, out GeneralType? type, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(function.Type));
            return false;
        }

        ImmutableArray<CompiledParameter>.Builder parameters = ImmutableArray.CreateBuilder<CompiledParameter>(function.Parameters.Count);
        foreach (ParameterDefinition item in function.Parameters.Parameters)
        {
            if (!GeneralType.From(item.Type, FindType, out var parameterType, out typeError))
            {
                Diagnostics.Add(typeError.ToError(item.Type));
                return false;
            }
            parameters.Add(new CompiledParameter(parameterType, item));
        }

        CompileFunctionAttributes(function);

        result = new(
            type,
            parameters.MoveToImmutable(),
            context,
            function
        );
        return true;
    }

    bool CompileGeneralFunctionDefinition(GeneralFunctionDefinition function, GeneralType returnType, CompiledStruct context, [NotNullWhen(true)] out CompiledGeneralFunctionDefinition? result)
    {
        result = null;

        CompileFunctionAttributes(function);

        ImmutableArray<CompiledParameter>.Builder parameters = ImmutableArray.CreateBuilder<CompiledParameter>(function.Parameters.Count);
        foreach (ParameterDefinition item in function.Parameters.Parameters)
        {
            if (!GeneralType.From(item.Type, FindType, out var parameterType, out var typeError))
            {
                Diagnostics.Add(typeError.ToError(item.Type));
                return false;
            }
            parameters.Add(new CompiledParameter(parameterType, item));
        }

        result = new(
            returnType,
            parameters.MoveToImmutable(),
            context,
            function
        );
        return true;
    }

    bool CompileConstructorDefinition(ConstructorDefinition function, CompiledStruct context, [NotNullWhen(true)] out CompiledConstructorDefinition? result)
    {
        result = null;

        if (function.Template is not null)
        {
            GenericParameters.Push(function.Template.Parameters);
            foreach (Token typeParameter in function.Template.Parameters)
            { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
        }

        if (!GeneralType.From(function.Type, FindType, out GeneralType? type, out PossibleDiagnostic? typeError))
        {
            Diagnostics.Add(typeError.ToError(function.Type));
            return false;
        }

        ImmutableArray<CompiledParameter>.Builder parameters = ImmutableArray.CreateBuilder<CompiledParameter>(function.Parameters.Count);
        foreach (ParameterDefinition item in function.Parameters.Parameters)
        {
            if (!GeneralType.From(item.Type, FindType, out var parameterType, out typeError))
            {
                Diagnostics.Add(typeError.ToError(item.Type));
                return false;
            }
            parameters.Add(new CompiledParameter(parameterType, item));
        }

        CompileFunctionAttributes(function);

        result = new(
            type,
            parameters.MoveToImmutable(),
            context,
            function);

        if (function.Template is not null)
        { GenericParameters.Pop(); }

        return true;
    }

    void AddAST(ParsedFile collectedAST, bool addTopLevelStatements = true)
    {
        if (addTopLevelStatements)
        { TopLevelStatements.Add((collectedAST.AST.TopLevelStatements, collectedAST.File)); }

        FunctionDefinitions.AddRange(collectedAST.AST.Functions);
        OperatorDefinitions.AddRange(collectedAST.AST.Operators);
        StructDefinitions.AddRange(collectedAST.AST.Structs);
        AliasDefinitions.AddRange(collectedAST.AST.AliasDefinitions);
    }

    static bool ThingEquality<TThing1, TThing2>(TThing1 a, TThing2 b)
        where TThing1 : IIdentifiable<Token>, IInFile
        where TThing2 : IIdentifiable<Token>, IInFile
    {
        if (a.Identifier.Content != b.Identifier.Content) return false;
        if (a.File != b.File) return false;
        return true;
    }

    static bool FunctionEquality<TFunction>(TFunction a, TFunction b)
        where TFunction : FunctionThingDefinition, ICompiledFunctionDefinition
    {
        if (!a.Type.Equals(b.Type)) return false;
        if (!Utils.SequenceEquals(((ICompiledFunctionDefinition)a).Parameters.Select(v => v.Type), ((ICompiledFunctionDefinition)b).Parameters.Select(v => v.Type))) return false;
        if (!ThingEquality(a, b)) return false;
        return true;
    }

    bool IsSymbolDefined<TThing>(TThing thing)
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

    bool IsGeneratorStruct(CompiledStruct @struct)
    {
        if (!@struct.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? builtinAttribute)) return false;
        if (!builtinAttribute.TryGetValue(out string? v)) return false;
        if (v != "generator") return false;

        return true;
    }

    bool CompileGeneratorStruct(CompiledStruct @struct)
    {
        if (!IsGeneratorStruct(@struct)) return false;

        if (@struct.Template is null)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct should be generic", @struct.Identifier.Position, @struct.File));
            return true;
        }

        if (@struct.Template.Parameters.Length != 1)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct should have one generic parameter", new Location(@struct.Template.Position, @struct.File)));
            return true;
        }

        CompiledFunctionDefinition? nextFunction = null;

        Token genericParameter = @struct.Template.Parameters[0];
        GenericType generatorType = new(genericParameter, @struct.File);
        genericParameter.AnalyzedType = TokenAnalyzedType.TypeParameter;

        using StackAuto<ImmutableArray<Token>> _ = GenericParameters.PushAuto(@struct.Template!.Parameters);

        foreach (FunctionDefinition method in @struct.Functions)
        {
            if (!method.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? builtinAttribute) || !builtinAttribute.TryGetValue(out string? v) || v != "next")
            {
                Diagnostics.Add(Diagnostic.Critical($"Generator struct shouldn't have any methods other than one \"next\" method", method));
                continue;
            }

            foreach (ParameterDefinition parameter in method.Parameters.Parameters)
            {
                if (parameter.Modifiers.Contains(ModifierKeywords.This))
                { Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, @struct.File)); }
            }

            ImmutableArray<ParameterDefinition> parameters = method.Parameters.Parameters.Insert(0, new ParameterDefinition(
                ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(@struct.Identifier.Content, method.File, @struct.Template?.Parameters), method.File),
                Token.CreateAnonymous(StatementKeywords.This),
                null,
                method
            ));

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

            if (!CompileFunctionDefinition(copy, @struct, out CompiledFunctionDefinition? compiledMethod))
            {
                continue;
            }

            if (CompiledFunctions.Any(compiledMethod.IsSame))
            {
                Diagnostics.Add(Diagnostic.Critical($"Function with name \"{compiledMethod.ToReadable()}\" already defined", method.Identifier, @struct.File));
                continue;
            }

            if (nextFunction is not null)
            {
                Diagnostics.Add(Diagnostic.Critical($"Generator struct should only have only one \"next\" function", method.Identifier, @struct.File));
                continue;
            }

            nextFunction = compiledMethod;

            CompiledFunctions.Add(compiledMethod);
        }

        if (@struct.GeneralFunctions.Length > 0)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct shouldn't have any general functions", @struct.Identifier.Position, @struct.File));
            return true;
        }

        if (@struct.Constructors.Length > 0)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct shouldn't have any constructors", @struct.Identifier.Position, @struct.File));
            return true;
        }

        if (@struct.Fields.Length > 0)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct shouldn't have any fields", @struct.Identifier.Position, @struct.File));
            return true;
        }

        if (nextFunction is null)
        {
            Diagnostics.Add(Diagnostic.Error($"Generator struct should only have a \"next\" function", @struct.Identifier.Position, @struct.File));
            return true;
        }

        CompiledField stateField;
        CompiledField functionField;

        @struct.SetFields(ImmutableArray.Create<CompiledField>(
            stateField = new(
                PointerType.Any,
                @struct,
                new FieldDefinition(
                    Token.CreateAnonymous("_genstate"),
                    null,
                    ImmutableArray<Token>.Empty,
                    ImmutableArray<AttributeUsage>.Empty
                )
            ),
            functionField = new(
                new FunctionType(
                    BuiltinType.U8,
                    ImmutableArray.Create<GeneralType>(
                        PointerType.Any,
                        new PointerType(generatorType)
                    ),
                    false
                ),
                @struct,
                new FieldDefinition(
                    nextFunction.Identifier,
                    null,
                    ImmutableArray<Token>.Empty,
                    ImmutableArray<AttributeUsage>.Empty
                )
            )
        ));

        if (!nextFunction.Type.SameAs(BuiltinType.U8))
        {
            Diagnostics.Add(Diagnostic.Error($"The \"next\" function should return {BuiltinType.U8}", nextFunction.TypeToken));
        }

        if (nextFunction.ParameterCount != 2)
        {
            Diagnostics.Add(Diagnostic.Error($"The \"next\" function should have one parameter of type \"{new PointerType(generatorType)}\"", nextFunction, nextFunction.File));
        }

        if (!nextFunction.Parameters[1].Type.SameAs(new PointerType(generatorType)))
        {
            Diagnostics.Add(Diagnostic.Error($"The \"next\" function should have one parameter of type \"{new PointerType(generatorType)}\"", ((FunctionThingDefinition)nextFunction).Parameters[1].Type, nextFunction.File));
        }

        if (nextFunction.Block is not null)
        {
            Diagnostics.Add(Diagnostic.Error($"The \"next\" function should not have a body", nextFunction.Block));
        }

        if (GeneratorStructDefinition is not null)
        {
            Diagnostics.Add(Diagnostic.Error($"A generator struct is already defined somewhere", @struct.Identifier.Position, @struct.File));
        }

        ImmutableArray<CompiledParameter> _parameters = ImmutableArray.Create<CompiledParameter>(
            new CompiledParameter(
                new PointerType(new StructType(@struct, @struct.File)),
                new ParameterDefinition(
                    ImmutableArray.Create<Token>(Token.CreateAnonymous(ModifierKeywords.This)),
                    new TypeInstancePointer(new TypeInstanceSimple(Token.CreateAnonymous(@struct.Identifier.Content), @struct.File), Token.CreateAnonymous("*"), @struct.File),
                    Token.CreateAnonymous("this"),
                    null
                )
            ),
            new CompiledParameter(
                new FunctionType(BuiltinType.U8, ImmutableArray.Create<GeneralType>(
                    PointerType.Any,
                    new PointerType(generatorType)
                ), false),
                new ParameterDefinition(
                    ImmutableArray<Token>.Empty,
                    new TypeInstanceFunction(
                        new TypeInstanceSimple(Token.CreateAnonymous(TypeKeywords.U8, TokenType.Identifier), @struct.File),
                        ImmutableArray.Create<TypeInstance>(
                            new TypeInstancePointer(new TypeInstanceSimple(Token.CreateAnonymous(TypeKeywords.Any), @struct.File), Token.CreateAnonymous("*", TokenType.Operator), @struct.File),
                            new TypeInstancePointer(new TypeInstanceSimple(genericParameter, @struct.File), Token.CreateAnonymous("*", TokenType.Operator), @struct.File)
                        ),
                        null,
                        @struct.File),
                    Token.CreateAnonymous("func"),
                    null
                )
            ),
            new CompiledParameter(
                PointerType.Any,
                new ParameterDefinition(
                    ImmutableArray<Token>.Empty,
                    new TypeInstancePointer(new TypeInstanceSimple(Token.CreateAnonymous(TypeKeywords.Any), @struct.File), Token.CreateAnonymous("*"), @struct.File),
                    Token.CreateAnonymous("state"),
                    null
                )
            )
        );

        CompiledConstructorDefinition constructor = new(
            new StructType(@struct, @struct.File),
            _parameters,
            @struct,
            new ConstructorDefinition(
                new TypeInstanceSimple(@struct.Identifier, @struct.File, ImmutableArray.Create<TypeInstance>(new TypeInstanceSimple(genericParameter, @struct.File))),
                ImmutableArray<Token>.Empty,
                new ParameterDefinitionCollection(
                    _parameters.As<ParameterDefinition>(),
                    TokenPair.CreateAnonymous("(", ")")
                ),
                @struct.File
            )
            {
                Context = @struct,
            })
        {
            Block = new Block(
                ImmutableArray.Create<Statement>(
                    new Assignment(
                        Token.CreateAnonymous("="),
                        new Field(
                            new Identifier(Token.CreateAnonymous("this"), @struct.File),
                            new Identifier(Token.CreateAnonymous("_genstate"), @struct.File),
                            @struct.File
                        ),
                        new Identifier(Token.CreateAnonymous("state"), @struct.File),
                        @struct.File
                    ),
                    new Assignment(
                        Token.CreateAnonymous("="),
                        new Field(
                            new Identifier(Token.CreateAnonymous("this"), @struct.File),
                            new Identifier(nextFunction.Identifier, @struct.File),
                            @struct.File
                        ),
                        new Identifier(Token.CreateAnonymous("func"), @struct.File),
                        @struct.File
                    )
                ),
                TokenPair.CreateAnonymous("{", "}"),
                @struct.File
            ),
            Context = @struct,
        };

        GeneratorStructDefinition = new CompiledGeneratorStructDefinition(
            @struct,
            nextFunction,
            stateField,
            functionField,
            constructor
        );

        CompiledConstructors.Add(constructor);

        return true;
    }

    void CompileDefinitions(Uri file, ImmutableArray<ParsedFile> parsedFiles)
    {
        // First compile the structs without fields
        // so it can reference other structs that are
        // not compiled but will be.
        foreach (StructDefinition @struct in StructDefinitions)
        {
            if (IsSymbolDefined(@struct))
            {
                Diagnostics.Add(Diagnostic.Critical("Symbol already exists", @struct.Identifier, @struct.File));
                continue;
            }

            CompiledStructs.Add(CompileStructNoFields(@struct));
        }

        foreach (AliasDefinition aliasDefinition in AliasDefinitions)
        {
            if (IsSymbolDefined(@aliasDefinition))
            {
                Diagnostics.Add(Diagnostic.Critical("Symbol already exists", @aliasDefinition.Identifier, @aliasDefinition.File));
                continue;
            }

            if (!GeneralType.From(aliasDefinition.Value, FindType, out GeneralType? aliasType, out PossibleDiagnostic? typeError))
            {
                Diagnostics.Add(typeError.ToError(aliasDefinition.Value));
                continue;
            }

            CompiledAlias alias = new(
                aliasType,
                aliasDefinition
            );
            aliasDefinition.Value.SetAnalyzedType(alias.Value);
            CompiledAliases.Add(alias);
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

        foreach (CompiledStruct compiledStruct in CompiledStructs)
        {
            CompileGeneratorStruct(compiledStruct);
        }

        foreach (FunctionDefinition @operator in OperatorDefinitions)
        {
            if (!CompileOperatorDefinition(@operator, null, out CompiledOperatorDefinition? compiled))
            {
                continue;
            }

            if (CompiledOperators.Any(other => FunctionEquality(compiled, other)))
            {
                Diagnostics.Add(Diagnostic.Critical($"Operator \"{compiled.ToReadable()}\" already defined", @operator.Identifier, @operator.File));
                continue;
            }

            CompiledOperators.Add(compiled);
        }

        foreach (FunctionDefinition function in FunctionDefinitions)
        {
            if (!CompileFunctionDefinition(function, null, out CompiledFunctionDefinition? compiled))
            {
                continue;
            }

            if (CompiledFunctions.Any(other => FunctionEquality(compiled, other)))
            {
                Diagnostics.Add(Diagnostic.Critical($"Function \"{compiled.ToReadable()}\" already defined", function.Identifier, function.File));
                continue;
            }

            CompiledFunctions.Add(compiled);
        }

        foreach (CompiledStruct compiledStruct in CompiledStructs)
        {
            if (IsGeneratorStruct(compiledStruct)) continue;

            if (compiledStruct.Template is not null)
            {
                GenericParameters.Push(compiledStruct.Template.Parameters);
                foreach (Token typeParameter in compiledStruct.Template.Parameters)
                { typeParameter.AnalyzedType = TokenAnalyzedType.TypeParameter; }
            }

            foreach (GeneralFunctionDefinition method in compiledStruct.GeneralFunctions)
            {
                foreach (ParameterDefinition parameter in method.Parameters.Parameters)
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
                    ImmutableArray<ParameterDefinition> parameters = method.Parameters.Parameters.Insert(0, new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters),
                        Token.CreateAnonymous(StatementKeywords.This),
                        null,
                        method
                    ));

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

                    if (!CompileGeneralFunctionDefinition(copy, returnType, compiledStruct, out CompiledGeneralFunctionDefinition? methodWithRef))
                    {
                        continue;
                    }

                    parameters = method.Parameters.Parameters.Insert(0, new ParameterDefinition(
                        ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                        TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters), method.File),
                        Token.CreateAnonymous(StatementKeywords.This),
                        null,
                        method
                    ));

                    copy = new GeneralFunctionDefinition(
                        method.Identifier,
                        method.Modifiers,
                        new ParameterDefinitionCollection(parameters, method.Parameters.Brackets),
                        method.File)
                    {
                        Context = method.Context,
                        Block = method.Block,
                    };

                    if (!CompileGeneralFunctionDefinition(copy, returnType, compiledStruct, out CompiledGeneralFunctionDefinition? methodWithPointer))
                    {
                        continue;
                    }

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
                    if (!CompileGeneralFunctionDefinition(method, returnType, compiledStruct, out CompiledGeneralFunctionDefinition? methodWithRef))
                    {
                        continue;
                    }

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
                foreach (ParameterDefinition parameter in method.Parameters.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    { Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File)); }
                }

                ImmutableArray<ParameterDefinition> parameters = method.Parameters.Parameters.Insert(0, new ParameterDefinition(
                    ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                    TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, method.File, compiledStruct.Template?.Parameters), method.File),
                    Token.CreateAnonymous(StatementKeywords.This),
                    null,
                    method
                ));

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

                if (!CompileFunctionDefinition(copy, compiledStruct, out CompiledFunctionDefinition? methodWithPointer))
                {
                    continue;
                }

                if (CompiledFunctions.Any(methodWithPointer.IsSame))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Function with name \"{methodWithPointer.ToReadable()}\" already defined", method.Identifier, compiledStruct.File));
                    continue;
                }

                CompiledFunctions.Add(methodWithPointer);
            }

            foreach (ConstructorDefinition constructor in compiledStruct.Constructors)
            {
                foreach (ParameterDefinition parameter in constructor.Parameters.Parameters)
                {
                    if (parameter.Modifiers.Contains(ModifierKeywords.This))
                    {
                        Diagnostics.Add(Diagnostic.Critical($"Keyword \"{ModifierKeywords.This}\" is not valid in the current context", parameter.Identifier, compiledStruct.File));
                        continue;
                    }
                }

                ImmutableArray<ParameterDefinition> parameters = constructor.Parameters.Parameters.Insert(0, new ParameterDefinition(
                    ImmutableArray.Create(Token.CreateAnonymous(ModifierKeywords.This)),
                    TypeInstancePointer.CreateAnonymous(TypeInstanceSimple.CreateAnonymous(compiledStruct.Identifier.Content, constructor.File, compiledStruct.Template?.Parameters), constructor.File),
                    Token.CreateAnonymous(StatementKeywords.This),
                    null,
                    constructor
                ));

                ConstructorDefinition constructorWithThisParameter = new(
                    constructor.Type,
                    constructor.Modifiers,
                    new ParameterDefinitionCollection(parameters, constructor.Parameters.Brackets),
                    constructor.File)
                {
                    Block = constructor.Block,
                    Context = constructor.Context,
                };

                if (!CompileConstructorDefinition(constructorWithThisParameter, compiledStruct, out CompiledConstructorDefinition? compiledConstructor))
                {
                    continue;
                }

                if (CompiledConstructors.Any(compiledConstructor.IsSame))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Constructor \"{compiledConstructor.ToReadable()}\" already defined", constructor.Type, compiledStruct.File));
                    continue;
                }

                CompiledConstructors.Add(compiledConstructor);
            }

            // TODO: this
            foreach (FunctionDefinition @operator in compiledStruct.Operators)
            {
                if (!CompileOperatorDefinition(@operator, null, out CompiledOperatorDefinition? compiled))
                {
                    continue;
                }

                if (CompiledOperators.Any(other => FunctionEquality(compiled, other)))
                {
                    Diagnostics.Add(Diagnostic.Critical($"Operator \"{compiled.ToReadable()}\" already defined", @operator.Identifier, @operator.File));
                    continue;
                }

                CompiledOperators.Add(compiled);
            }

            if (compiledStruct.Template is not null)
            { GenericParameters.Pop(); }
        }
    }

    CompilerResult CompileMainFile(string file)
    {
        SourceCodeManagerResult res = SourceCodeManager.Collect(file, Diagnostics, PreprocessorVariables, Settings.AdditionalImports, Settings.SourceProviders, Settings.TokenizerSettings);

        foreach (ParsedFile parsedFile in res.ParsedFiles)
        { AddAST(parsedFile, parsedFile.File != res.ResolvedEntry); }

        foreach (ParsedFile parsedFile in res.ParsedFiles)
        {
            if (parsedFile.File != res.ResolvedEntry) continue;
            TopLevelStatements.Add((parsedFile.AST.TopLevelStatements, parsedFile.File));
        }

        // This should not be null ...
        if (res.ResolvedEntry is null) throw new InternalExceptionWithoutContext($"I can't really explain this error ...");
        return CompileInternal(res.ResolvedEntry, res.ParsedFiles);
    }

    CompilerResult CompileFiles(ReadOnlySpan<string> files)
    {
        SourceCodeManagerResult res = SourceCodeManager.CollectMultiple(files, Diagnostics, PreprocessorVariables, Settings.AdditionalImports, Settings.SourceProviders, Settings.TokenizerSettings);

        foreach (ParsedFile parsedFile in res.ParsedFiles)
        { AddAST(parsedFile, parsedFile.File != res.ResolvedEntry); }

        foreach (ParsedFile parsedFile in res.ParsedFiles)
        {
            if (parsedFile.File != res.ResolvedEntry) continue;
            TopLevelStatements.Add((parsedFile.AST.TopLevelStatements, parsedFile.File));
        }

        // This should not be null ...
        if (res.ResolvedEntry is null) throw new InternalExceptionWithoutContext($"I can't really explain this error ...");
        return CompileInternal(res.ResolvedEntry, res.ParsedFiles);
    }

    CompilerResult CompileInteractiveInternal(Statement statement, Uri file)
    {
        SourceCodeManagerResult res = SourceCodeManager.Collect(
            null,
            Diagnostics,
            PreprocessorVariables,
            ImmutableArray.Create("Primitives", "System"),
            Settings.SourceProviders,
            Settings.TokenizerSettings
        );

        foreach (ParsedFile parsedFile in res.ParsedFiles)
        { AddAST(parsedFile); }

        TopLevelStatements.Add((ImmutableArray.Create(statement), file));

        return CompileInternal(file, res.ParsedFiles);
    }

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _m3 = new("LanguageCore.Compiler");
#endif
    CompilerResult CompileInternal(Uri file, ImmutableArray<ParsedFile> parsedFiles)
    {
#if UNITY
        using var _1 = _m3.Auto();
#endif

        CompiledFrame dummyFrame = Frames.Push(CompiledFrame.Empty);

        CompileDefinitions(file, parsedFiles);

        GenerateCode(
            parsedFiles,
            file
        );

        if (Frames.Pop() != dummyFrame) throw new InternalExceptionWithoutContext("Bruh");

        return new CompilerResult(
            parsedFiles,
            CompiledFunctions.ToImmutableArray(),
            CompiledGeneralFunctions.ToImmutableArray(),
            CompiledOperators.ToImmutableArray(),
            CompiledConstructors.ToImmutableArray(),
            CompiledAliases.ToImmutableArray(),
            ExternalFunctions,
            CompiledStructs.ToImmutableArray(),
            TopLevelStatements.ToImmutableArray(),
            file,
            false,
            CompiledTopLevelStatements.ToImmutable(),
            GeneratedFunctions.ToImmutableArray()
        );
    }

    public static CompilerResult CompileFiles(
        ReadOnlySpan<string> files,
        CompilerSettings settings,
        DiagnosticsCollection diagnostics)
    {
        StatementCompiler compiler = new(settings, diagnostics, null);
        return compiler.CompileFiles(files);
    }

    public static CompilerResult CompileFile(
        string file,
        CompilerSettings settings,
        DiagnosticsCollection diagnostics)
    {
        StatementCompiler compiler = new(settings, diagnostics, null);
        return compiler.CompileMainFile(file);
    }

    public static CompilerResult CompileInteractive(
        Statement statement,
        CompilerSettings settings,
        DiagnosticsCollection diagnostics,
        Uri file)
    {
        StatementCompiler compiler = new(settings, diagnostics, null);
        return compiler.CompileInteractiveInternal(statement, file);
    }
}
