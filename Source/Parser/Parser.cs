using LanguageCore.Parser.Statements;
using LanguageCore.Tokenizing;

namespace LanguageCore.Parser;

public sealed class Parser
{
    int CurrentTokenIndex;
    readonly Token[] Tokens;
    readonly ImmutableArray<Token> OriginalTokens;
    readonly Uri File;

    Location CurrentLocation => new(CurrentPosition, File);
    Position CurrentPosition => CurrentToken?.Position ?? PreviousToken?.Position.After() ?? Position.UnknownPosition;
    Token? CurrentToken => (CurrentTokenIndex >= 0 && CurrentTokenIndex < Tokens.Length) ? Tokens[CurrentTokenIndex] : null;
    Token? PreviousToken => (CurrentTokenIndex >= 1 && CurrentTokenIndex <= Tokens.Length) ? Tokens[CurrentTokenIndex - 1] : null;

    static readonly ImmutableArray<string> AllModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export,
        ProtectionKeywords.Private,

        ModifierKeywords.Inline,
        ModifierKeywords.Const,
        ModifierKeywords.Ref,
        ModifierKeywords.Temp,
        ModifierKeywords.This
    );

    static readonly ImmutableArray<string> FunctionModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export,
        ModifierKeywords.Inline
    );

    static readonly ImmutableArray<string> AliasModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export
    );

    static readonly ImmutableArray<string> FieldModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Private
    );

    static readonly ImmutableArray<string> GeneralStatementModifiers = ImmutableArray.Create
    (
        ModifierKeywords.Temp
    );

    static readonly ImmutableArray<string> VariableModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export,
        ModifierKeywords.Temp,
        ModifierKeywords.Const
    );

    static readonly ImmutableArray<string> ParameterModifiers = ImmutableArray.Create
    (
        ModifierKeywords.This,
        ModifierKeywords.Ref,
        ModifierKeywords.Temp
    );

    static readonly ImmutableArray<string> ArgumentModifiers = ImmutableArray.Create
    (
        ModifierKeywords.Ref,
        ModifierKeywords.Temp
    );

    static readonly ImmutableArray<string> StructModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export
    );

    static readonly ImmutableArray<string> ConstructorModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export
    );

    static readonly ImmutableArray<string> GeneralFunctionModifiers = ImmutableArray.Create
    (
        ProtectionKeywords.Export
    );

    static readonly ImmutableArray<string> OverloadableOperators = ImmutableArray.Create
    (
        "<<", ">>",
        "+", "-", "*", "/", "%",
        "&", "|", "^",
        "<", ">", ">=", "<=", "!=", "==",
        "&&", "||"
    );

    static readonly ImmutableArray<string> CompoundAssignmentOperators = ImmutableArray.Create
    (
        "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^="
    );

    static readonly ImmutableArray<string> BinaryOperators = ImmutableArray.Create
    (
        "<<", ">>",
        "+", "-", "*", "/", "%",
        "&", "|", "^",
        "<", ">", ">=", "<=", "!=", "==", "&&", "||"
    );

    static readonly ImmutableArray<string> UnaryPrefixOperators = ImmutableArray.Create
    (
        "!", "~",
        "-", "+"
    );

#pragma warning disable RCS1213, IDE0052, CA1823 // Remove unread private members
    static readonly ImmutableArray<string> UnaryPostfixOperators = ImmutableArray<string>.Empty;
#pragma warning restore RCS1213, IDE0052, CA1823

    // === Result ===
    readonly DiagnosticsCollection Diagnostics;
    readonly List<FunctionDefinition> Functions = new();
    readonly List<FunctionDefinition> Operators = new();
    readonly Dictionary<string, StructDefinition> Structs = new();
    readonly List<UsingDefinition> Usings = new();
    readonly List<AliasDefinition> AliasDefinitions = new();
    readonly List<Statement> TopLevelStatements = new();
    // === ===

    Parser(ImmutableArray<Token> tokens, Uri file, DiagnosticsCollection diagnostics)
    {
        OriginalTokens = tokens;
        Tokens = tokens
            .Where(v => v.TokenType
                is not TokenType.Comment
                and not TokenType.CommentMultiline
                and not TokenType.Whitespace
                and not TokenType.LineBreak
                and not TokenType.PreprocessArgument
                and not TokenType.PreprocessIdentifier
                and not TokenType.PreprocessSkipped)
            .ToArray();
        File = file;
        Diagnostics = diagnostics;
    }

    public static ParserResult Parse(ImmutableArray<Token> tokens, Uri file, DiagnosticsCollection diagnostics)
        => new Parser(tokens, file, diagnostics).ParseInternal();

#if UNITY
    static readonly Unity.Profiling.ProfilerMarker _marker = new("LanguageCore.Parser");
#endif
    ParserResult ParseInternal()
    {
#if UNITY
        using Unity.Profiling.ProfilerMarker.AutoScope _1 = _marker.Auto();
#endif
        CurrentTokenIndex = 0;

        ParseCodeHeader();

        EndlessCheck endlessSafe = new();
        while (CurrentToken != null)
        {
            ParseCodeBlock();

            SkipCrapTokens();

            endlessSafe.Step();
        }

        return new ParserResult(
            Functions.ToImmutableArray(),
            Operators.ToImmutableArray(),
            Structs.Values.ToImmutableArray(),
            Usings.ToImmutableArray(),
            AliasDefinitions.ToImmutableArray(),
            TopLevelStatements.ToImmutableArray(),
            OriginalTokens,
            Tokens.ToImmutableArray()
        );
    }

    Statement ParseStatementInternal()
    {
        if (ExpectStatementUnchecked(out Statement? statement))
        {
            Diagnostics.Throw();
            return statement;
        }
        else if (CurrentToken is null)
        { throw new SyntaxException($"Expected something but got nothing", PreviousToken?.Position.After() ?? Position.UnknownPosition, File); }
        else
        { throw new SyntaxException($"Expected something but not \"{CurrentToken}\"", CurrentToken, File); }
    }

    #region Parse top level

    bool ExpectUsing([NotNullWhen(true)] out UsingDefinition? usingDefinition)
    {
        usingDefinition = null;
        if (!ExpectIdentifier(DeclarationKeywords.Using, out Token? keyword))
        { return false; }

        SkipCrapTokens();

        if (CurrentToken == null) throw new SyntaxException($"Expected url after keyword \"{DeclarationKeywords.Using}\"", keyword.Position.After(), File);

        keyword.AnalyzedType = TokenAnalyzedType.Keyword;

        List<Token> tokens = new();
        if (CurrentToken.TokenType == TokenType.LiteralString)
        {
            tokens.Add(CurrentToken);
            CurrentTokenIndex++;
        }
        else
        {
            EndlessCheck endlessSafe = new();
            while (ExpectIdentifier(out Token? pathIdentifier))
            {
                tokens.Add(pathIdentifier);

                ExpectOperator(".");

                endlessSafe.Step();
            }
        }

        if (tokens.Count == 0)
        {
            if (!ExpectOperator(";"))
            {
                throw new SyntaxException($"Expected library name after \"{DeclarationKeywords.Using}\"", keyword, File);
            }
            else
            {
                Diagnostics.Add(Diagnostic.Error($"Expected library name after \"{DeclarationKeywords.Using}\"", keyword, File));
            }
            return false;
        }

        if (!ExpectOperator(";"))
        { throw new SyntaxException($"Please put a \";\" here (after \"{DeclarationKeywords.Using}\")", keyword.Position.After(), File); }

        usingDefinition = new UsingDefinition(keyword, tokens.ToImmutableArray(), File);

        return true;
    }

    void ParseCodeHeader()
    {
        while (true)
        {
            if (ExpectUsing(out UsingDefinition? usingDefinition))
            { Usings.Add(usingDefinition); }
            else
            { break; }
        }
    }

    bool ParseCodeBlock()
    {
        OrderedDiagnosticCollection diagnostics = new();
        if (ExpectStructDefinition(diagnostics)) { }
        else if (ExpectFunctionDefinition(out FunctionDefinition? functionDefinition, diagnostics))
        { Functions.Add(functionDefinition); }
        else if (ExpectOperatorDefinition(out FunctionDefinition? operatorDefinition, diagnostics))
        { Operators.Add(operatorDefinition); }
        else if (ExpectAliasDefinition(out AliasDefinition? aliasDefinition, diagnostics))
        { AliasDefinitions.Add(aliasDefinition); }
        else if (ExpectStatement(out Statement? statement))
        { TopLevelStatements.Add(statement); }
        else
        {
            Diagnostics.Add(Diagnostic.Error($"Expected something but not \"{CurrentToken}\"", CurrentToken, File).WithSuberrors(diagnostics.Compile()));
            return false;
        }

        return true;
    }

    bool ExpectOperatorDefinition([NotNullWhen(true)] out FunctionDefinition? function, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectType(AllowedType.None, out TypeInstance? possibleType, out _))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected type for operator definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator(OverloadableOperators, out Token? possibleName))
        {
            if (OverloadableOperators.Contains("*") &&
                possibleType is TypeInstancePointer _possibleTypePointer)
            {
                possibleType = _possibleTypePointer.To;
                possibleName = _possibleTypePointer.Operator;
            }
            else
            {
                int callOperatorParseStart = CurrentTokenIndex;
                if (ExpectOperator("(", out Token? opening) && ExpectOperator(")", out Token? closing) && CurrentToken?.Content == "(")
                {
                    possibleName = opening + closing;
                }
                else
                {
                    CurrentTokenIndex = callOperatorParseStart;

                    diagnostic.Add(0, Diagnostic.Critical($"Expected an operator for operator definition", CurrentLocation, false));
                    CurrentTokenIndex = parseStart;
                    return false;
                }
            }
        }

        OrderedDiagnosticCollection parameterDiagnostics = new();
        if (!ExpectParameters(ImmutableArray.Create(ModifierKeywords.Temp), false, out ParameterDefinitionCollection? parameters, parameterDiagnostics))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected parameter list for operator", CurrentLocation, false), parameterDiagnostics);
            CurrentTokenIndex = parseStart;
            return false;
        }

        possibleName.AnalyzedType = TokenAnalyzedType.FunctionName;

        CheckModifiers(modifiers, FunctionModifiers);

        Block? block = null;

        if (!ExpectOperator(";") && !ExpectBlock(out block))
        {
            diagnostic.Add(1, Diagnostic.Critical($"Expected \";\" or block", parameters.Brackets.End.Position.After(), File));
            CurrentTokenIndex = parseStart;
            return false;
        }

        function = new FunctionDefinition(
            attributes,
            modifiers,
            possibleType,
            possibleName,
            parameters,
            null,
            File)
        {
            Block = block
        };
        return true;
    }

    bool ExpectAliasDefinition([NotNullWhen(true)] out AliasDefinition? aliasDefinition, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        aliasDefinition = null;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectIdentifier(DeclarationKeywords.Alias, out Token? keyword))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected keyword `{DeclarationKeywords.Alias}` for alias definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectIdentifier(out Token? identifier))
        {
            diagnostic.Add(1, Diagnostic.Critical($"Expected identifier after keyword \"{keyword}\"", keyword.Position.After(), File));
            CurrentTokenIndex = parseStart;
            return false;
        }

        identifier.AnalyzedType = TokenAnalyzedType.Type;

        if (!ExpectType(AllowedType.Any | AllowedType.FunctionPointer | AllowedType.StackArrayWithoutLength, out TypeInstance? type))
        {
            diagnostic.Add(2, Diagnostic.Critical($"Expected type after alias identifier", identifier.Position.After(), File));
            CurrentTokenIndex = parseStart;
            return false;
        }

        CheckModifiers(modifiers, AliasModifiers);

        if (!ExpectOperator(";"))
        {
            diagnostic.Add(3, Diagnostic.Critical($"Pls put a semicolon here", type.Position.After(), File));
            CurrentTokenIndex = parseStart;
            return false;
        }

        aliasDefinition = new AliasDefinition(
            attributes,
            modifiers,
            keyword,
            identifier,
            type,
            File
        );
        return true;
    }

    bool ExpectTemplateInfo([NotNullWhen(true)] out TemplateInfo? templateInfo)
    {
        if (!ExpectOperator("<", out Token? startBracket))
        {
            templateInfo = null;
            return false;
        }

        List<Token> parameters = new();

        Token? endBracket;

        bool expectParameter = false;
        while (!ExpectOperator(">", out endBracket) || expectParameter)
        {
            if (!ExpectIdentifier(out Token? parameter))
            { throw new SyntaxException("Expected identifier or \">\"", PreviousToken!.Position.After(), File); }

            parameter.AnalyzedType = TokenAnalyzedType.TypeParameter;

            parameters.Add(parameter);

            if (ExpectOperator(">", out endBracket))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" or \">\"", parameter.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        templateInfo = new(new TokenPair(startBracket, endBracket), parameters.ToImmutableArray());

        return true;
    }

    bool ExpectFunctionDefinition([NotNullWhen(true)] out FunctionDefinition? function, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectType(AllowedType.None, out TypeInstance? possibleType, out _))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected type for function definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectIdentifier(out Token? possibleNameT))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected identifier for function definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        ExpectTemplateInfo(out TemplateInfo? templateInfo);

        OrderedDiagnosticCollection parameterDiagnostics = new();
        if (!ExpectParameters(ParameterModifiers, true, out ParameterDefinitionCollection? parameters, parameterDiagnostics))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected parameter list for function definition", CurrentLocation, false), parameterDiagnostics);
            CurrentTokenIndex = parseStart;
            return false;
        }

        possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

        CheckModifiers(modifiers, FunctionModifiers);

        Block? block = null;

        if (!ExpectOperator(";") && !ExpectBlock(out block))
        {
            diagnostic.Add(1, Diagnostic.Critical($"Expected \";\" or block", parameters.Brackets.End.Position.After(), File));
            CurrentTokenIndex = parseStart;
            return false;
        }

        function = new FunctionDefinition(
            attributes,
            modifiers,
            possibleType,
            possibleNameT,
            parameters,
            templateInfo,
            File)
        {
            Block = block
        };
        return true;
    }

    bool ExpectGeneralFunctionDefinition([NotNullWhen(true)] out GeneralFunctionDefinition? function, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectIdentifier(out Token? possibleNameT))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected identifier for general function definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (possibleNameT.Content is
            not BuiltinFunctionIdentifiers.IndexerGet and
            not BuiltinFunctionIdentifiers.IndexerSet and
            not BuiltinFunctionIdentifiers.Destructor)
        {
            diagnostic.Add(0, Diagnostic.Critical($"Invalid identifier `{possibleNameT.Content}` for general function definition", possibleNameT, File, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        OrderedDiagnosticCollection parameterDiagnostics = new();
        if (!ExpectParameters(ImmutableArray.Create(ModifierKeywords.Temp), false, out ParameterDefinitionCollection? parameters, parameterDiagnostics))
        {
            diagnostic.Add(1, Diagnostic.Critical($"Expected parameter list for general function definition", CurrentLocation), parameterDiagnostics);
            CurrentTokenIndex = parseStart;
            return false;
        }

        possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

        CheckModifiers(modifiers, GeneralFunctionModifiers);

        if (!ExpectBlock(out Block? block))
        {
            diagnostic.Add(2, Diagnostic.Error($"Body is required for general function definition", CurrentPosition, File, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        function = new GeneralFunctionDefinition(
            possibleNameT,
            modifiers,
            parameters,
            File)
        {
            Block = block
        };
        return true;
    }

    bool ExpectConstructorDefinition([NotNullWhen(true)] out ConstructorDefinition? function, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectType(AllowedType.None, out TypeInstance? type))
        {
            diagnostic.Add(0, Diagnostic.Error($"Expected a type for constructor definition", CurrentPosition, File, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        OrderedDiagnosticCollection parameterDiagnostics = new();
        if (!ExpectParameters(ImmutableArray.Create(ModifierKeywords.Temp), true, out ParameterDefinitionCollection? parameters, parameterDiagnostics))
        {
            diagnostic.Add(0, Diagnostic.Error($"Expected a parameter list for constructor definition", CurrentPosition, File, false), parameterDiagnostics);
            CurrentTokenIndex = parseStart;
            return false;
        }

        CheckModifiers(modifiers, ConstructorModifiers);

        if (!ExpectBlock(out Block? block))
        {
            diagnostic.Add(0, Diagnostic.Error($"Body is required for constructor definition", CurrentPosition, File, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        function = new ConstructorDefinition(
            type,
            modifiers,
            parameters,
            File)
        {
            Block = block
        };

        return true;
    }

    bool ExpectStructDefinition(OrderedDiagnosticCollection diagnostic)
    {
        int startTokenIndex = CurrentTokenIndex;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectIdentifier(DeclarationKeywords.Struct, out Token? keyword))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected keyword `{DeclarationKeywords.Struct}` for struct definition", CurrentLocation, false));
            CurrentTokenIndex = startTokenIndex;
            return false;
        }

        if (!ExpectIdentifier(out Token? possibleStructName))
        {
            diagnostic.Add(1, Diagnostic.Critical($"Expected struct identifier after keyword `{keyword}`", keyword.Position.After(), File));
            CurrentTokenIndex = startTokenIndex;
            return false;
        }

        ExpectTemplateInfo(out TemplateInfo? templateInfo);

        if (!ExpectOperator("{", out Token? bracketStart))
        {
            diagnostic.Add(2, Diagnostic.Critical($"Expected `{{` after struct identifier `{keyword}`", possibleStructName.Position.After(), File));
            CurrentTokenIndex = startTokenIndex;
            return false;
        }

        possibleStructName.AnalyzedType = TokenAnalyzedType.Struct;
        keyword.AnalyzedType = TokenAnalyzedType.Keyword;

        List<FieldDefinition> fields = new();
        List<FunctionDefinition> methods = new();
        List<FunctionDefinition> operators = new();
        List<GeneralFunctionDefinition> generalMethods = new();
        List<ConstructorDefinition> constructors = new();

        Token? bracketEnd;
        EndlessCheck endlessSafe = new();
        while (!ExpectOperator("}", out bracketEnd))
        {
            OrderedDiagnosticCollection diagnostics = new();
            if (ExpectField(out FieldDefinition? field, diagnostics))
            {
                fields.Add(field);
                if (ExpectOperator(";", out Token? semicolon))
                { field.Semicolon = semicolon; }
            }
            else if (ExpectFunctionDefinition(out FunctionDefinition? methodDefinition, diagnostics))
            {
                methods.Add(methodDefinition);
            }
            else if (ExpectGeneralFunctionDefinition(out GeneralFunctionDefinition? generalMethodDefinition, diagnostics))
            {
                generalMethods.Add(generalMethodDefinition);
            }
            else if (ExpectConstructorDefinition(out ConstructorDefinition? constructorDefinition, diagnostics))
            {
                constructors.Add(constructorDefinition);
            }
            else if (ExpectOperatorDefinition(out FunctionDefinition? operatorDefinition, diagnostics))
            {
                operators.Add(operatorDefinition);
            }
            else
            {
                Diagnostics.Add(Diagnostic.Critical("Expected field definition or \"}\"", CurrentToken?.Position ?? PreviousToken!.Position.After(), File).WithSuberrors(diagnostics.Compile()));
                return false;
            }

            endlessSafe.Step();
        }

        CheckModifiers(modifiers, StructModifiers);

        StructDefinition structDefinition = new(
            possibleStructName,
            bracketStart,
            bracketEnd,
            attributes,
            modifiers,
            fields.ToImmutableArray(),
            methods.ToImmutableArray(),
            generalMethods.ToImmutableArray(),
            operators.ToImmutableArray(),
            constructors.ToImmutableArray(),
            File)
        {
            Template = templateInfo,
        };

        Structs.Add(structDefinition.Identifier.Content, structDefinition);

        return true;
    }

    bool ExpectParameters(ImmutableArray<string> allowedParameterModifiers, bool allowDefaultValues, [NotNullWhen(true)] out ParameterDefinitionCollection? parameterDefinitions, OrderedDiagnosticCollection diagnostic)
    {
        int parseStart = CurrentTokenIndex;
        parameterDefinitions = null;

        if (!ExpectOperator("(", out Token? bracketStart))
        {
            diagnostic.Add(0, Diagnostic.Error("Expected a `(` for parameter list", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        List<ParameterDefinition> parameters = new();

        bool expectParameter = false;
        bool expectOptionalParameters = false;
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            ImmutableArray<Token> parameterModifiers = ExpectModifiers();
            CheckParameterModifiers(parameterModifiers, parameters.Count, allowedParameterModifiers);

            if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? parameterType))
            {
                diagnostic.Add(1, Diagnostic.Error("Expected parameter type", CurrentLocation, false));
                CurrentTokenIndex = parseStart;
                return false;
            }

            if (!ExpectIdentifier(out Token? parameterIdentifier))
            {
                diagnostic.Add(1, Diagnostic.Error("Expected a parameter name", CurrentLocation, false));
                CurrentTokenIndex = parseStart;
                return false;
            }

            parameterIdentifier.AnalyzedType = TokenAnalyzedType.ParameterName;

            StatementWithValue? defaultValue = null;
            if (ExpectOperator("=", out Token? assignmentOperator))
            {
                if (!allowDefaultValues)
                {
                    diagnostic.Add(2, Diagnostic.Error("Default parameter values are not valid in the current context", assignmentOperator, File, false));
                    CurrentTokenIndex = parseStart;
                    return false;
                }
                if (!ExpectExpression(out defaultValue))
                {
                    diagnostic.Add(2, Diagnostic.Error("Expected expression after \"=\" in parameter definition", assignmentOperator, File, false));
                    CurrentTokenIndex = parseStart;
                    return false;
                }
                expectOptionalParameters = true;
            }
            else if (expectOptionalParameters)
            {
                diagnostic.Add(2, Diagnostic.Error("Parameters without default value after a parameter that has one is not supported", parameterIdentifier.Position.After(), File));
                CurrentTokenIndex = parseStart;
                return false;
            }

            parameters.Add(new ParameterDefinition(parameterModifiers, parameterType, parameterIdentifier, defaultValue));

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            {
                diagnostic.Add(2, Diagnostic.Error("Expected \",\" or \")\"", PreviousToken!.Position.After(), File, false));
                CurrentTokenIndex = parseStart;
                return false;
            }
            else
            { expectParameter = true; }
        }

        parameterDefinitions = new ParameterDefinitionCollection(parameters.ToImmutableArray(), new TokenPair(bracketStart, bracketEnd));
        return true;
    }

    #endregion

    #region Parse low level

    bool ExpectLambda([NotNullWhen(true)] out LambdaStatement? lambdaStatement)
    {
        int parseStart = CurrentTokenIndex;
        lambdaStatement = null;

        OrderedDiagnosticCollection parametersDiagnostics = new();
        if (!ExpectParameters(ParameterModifiers, false, out ParameterDefinitionCollection? parameters, parametersDiagnostics))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator("=>", out Token? arrow))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        Statement body;

        if (ExpectBlock(out Block? block, false))
        {
            body = block;
        }
        else if (ExpectExpression(out StatementWithValue? expression))
        {
            body = expression;
        }
        else
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        arrow.AnalyzedType = TokenAnalyzedType.OtherOperator;

        lambdaStatement = new LambdaStatement(
            parameters,
            arrow,
            body,
            File
        );
        return true;
    }

    bool ExpectListValue([NotNullWhen(true)] out LiteralList? listValue)
    {
        if (!ExpectOperator("[", out Token? bracketStart))
        {
            listValue = null;
            return false;
        }

        ImmutableArray<StatementWithValue>.Builder? values = null;

        Token? bracketEnd;
        EndlessCheck endlessSafe = new();
        while (true)
        {
            if (ExpectExpression(out StatementWithValue? v))
            {
                values ??= ImmutableArray.CreateBuilder<StatementWithValue>();
                values.Add(v);

                if (!ExpectOperator(","))
                {
                    if (!ExpectOperator("]", out bracketEnd))
                    { throw new SyntaxException("Unbalanced \"[\"", bracketStart, File); }
                    break;
                }
            }
            else
            {
                if (!ExpectOperator("]", out bracketEnd))
                { throw new SyntaxException("Unbalanced \"[\"", bracketStart, File); }
                break;
            }

            endlessSafe.Step();
        }

        listValue = new LiteralList(values?.DrainToImmutable() ?? ImmutableArray<StatementWithValue>.Empty, new TokenPair(bracketStart, bracketEnd), File);
        return true;
    }

    bool ExpectLiteral([NotNullWhen(true)] out Literal? statement)
    {
        int savedToken = CurrentTokenIndex;

        SkipCrapTokens();

        string v = CurrentToken?.Content ?? string.Empty;

        if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralFloat)
        {
            v = v.Replace("_", string.Empty, StringComparison.Ordinal);

            Literal literal = new(LiteralType.Float, v, CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralNumber)
        {
            v = v.Replace("_", string.Empty, StringComparison.Ordinal);

            Literal literal = new(LiteralType.Integer, v, CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralHex)
        {
            if (v.Length < 3)
            {
                Diagnostics.Add(Diagnostic.Error($"Invalid hex literal \"{CurrentToken}\"", CurrentToken, File));
                v = "0";
            }
            else
            {
                v = v[2..];
                v = v.Replace("_", string.Empty, StringComparison.Ordinal);
            }

            if (!int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            {
                Diagnostics.Add(Diagnostic.Error($"Invalid hex number \"{v}\"", CurrentToken.Position[2..], File));
                value = 0;
            }

            Literal literal = new(LiteralType.Integer, value.ToString(CultureInfo.InvariantCulture), CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralBinary)
        {
            if (v.Length < 3)
            {
                Diagnostics.Add(Diagnostic.Error($"Invalid binary literal \"{CurrentToken}\"", CurrentToken, File));
                v = "0";
            }
            else
            {
                v = v[2..];
                v = v.Replace("_", string.Empty, StringComparison.Ordinal);
            }

            // if (!int.TryParse(v, NumberStyles.BinaryNumber, CultureInfo.InvariantCulture, out int value))
            // {
            //     Diagnostics.Add(Diagnostic.Error($"Invalid binary number \"{v}\"", CurrentToken.Position[2..], File));
            //     value = 0;
            // }
            int value = Convert.ToInt32(v, 2);

            Literal literal = new(LiteralType.Integer, value.ToString(CultureInfo.InvariantCulture), CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralString)
        {
            Literal literal = new(LiteralType.String, v, CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralCharacter)
        {
            Literal literal = new(LiteralType.Char, v, CurrentToken, File);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }

        CurrentTokenIndex = savedToken;

        statement = null;
        return false;
    }

    bool ExpectIndex(StatementWithValue prevStatement, [NotNullWhen(true)] out IndexCall? statement)
    {
        int savedToken = CurrentTokenIndex;

        if (!ExpectOperator("[", out Token? bracketStart))
        {
            statement = null;
            CurrentTokenIndex = savedToken;
            return false;
        }

        if (!ExpectExpression(out StatementWithValue? expression))
        {
            statement = null;
            CurrentTokenIndex = savedToken;
            return false;
        }

        if (!ExpectOperator("]", out Token? bracketEnd))
        { throw new SyntaxException("Unbalanced [", bracketStart, File); }

        statement = new IndexCall(prevStatement, expression, new TokenPair(bracketStart, bracketEnd), File);
        return true;
    }

    bool ExpectExpressionInBrackets([NotNullWhen(true)] out StatementWithValue? expressionInBrackets)
    {
        expressionInBrackets = null;

        if (!ExpectOperator("(", out Token? bracketStart1))
        { return false; }

        if (!ExpectExpression(out StatementWithValue? expression))
        { throw new SyntaxException("Expected expression after \"(\"", bracketStart1.Position.After(), File); }

        if (!ExpectOperator(")", out Token? bracketEnd1))
        { throw new SyntaxException("Unbalanced \"(\"", bracketStart1, File); }

        expression.SurroundingBrackets = new TokenPair(bracketStart1, bracketEnd1);

        expressionInBrackets = expression;
        return true;
    }

    bool ExpectNewExpression([NotNullWhen(true)] out StatementWithValue? newExpression)
    {
        newExpression = null;

        if (!ExpectIdentifier(StatementKeywords.New, out Token? keywordNew))
        {
            return false;
        }

        keywordNew.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectType(AllowedType.None, out TypeInstance? instanceTypeName))
        { throw new SyntaxException($"Expected instance constructor after keyword \"{StatementKeywords.New}\"", keywordNew, File); }

        if (ExpectOperator("(", out Token? bracketStart2))
        {
            bool expectParameter = false;
            ImmutableArray<StatementWithValue>.Builder parameters = ImmutableArray.CreateBuilder<StatementWithValue>();

            Token? bracketEnd2;
            EndlessCheck endlessSafe = new();
            while (!ExpectOperator(")", out bracketEnd2) || expectParameter)
            {
                if (!ExpectExpression(out StatementWithValue? parameter))
                { throw new SyntaxException("Expected expression as parameter", CurrentToken?.Position ?? bracketStart2.Position.After(), File); }

                parameters.Add(parameter);

                if (ExpectOperator(")", out bracketEnd2))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" to separate parameters", parameter.Position.After(), File); }
                else
                { expectParameter = true; }

                endlessSafe.Step();
            }

            newExpression = new ConstructorCall(keywordNew, instanceTypeName, parameters.DrainToImmutable(), new TokenPair(bracketStart2, bracketEnd2), File);
            return true;
        }
        else
        {
            newExpression = new NewInstance(keywordNew, instanceTypeName, File);
            return true;
        }
    }

    bool ExpectFieldAccessor(StatementWithValue prevStatement, [NotNullWhen(true)] out Field? fieldAccessor)
    {
        fieldAccessor = null;

        if (!ExpectOperator(".", out Token? tokenDot))
        {
            return false;
        }

        if (!ExpectIdentifier(out Token? fieldName))
        { throw new SyntaxException("Expected a symbol after \".\"", tokenDot.Position.After(), File); }

        fieldAccessor = new Field(
            prevStatement,
            new(fieldName, File),
            File);

        return true;
    }

    bool ExpectAsStatement(StatementWithValue prevStatement, [NotNullWhen(true)] out BasicTypeCast? basicTypeCast)
    {
        basicTypeCast = null;

        if (!ExpectIdentifier(StatementKeywords.As, out Token? keyword))
        {
            return false;
        }

        if (!ExpectType(AllowedType.StackArrayWithoutLength, out TypeInstance? type))
        { throw new SyntaxException($"Expected type after keyword \"{keyword}\"", keyword.Position.After(), File); }

        basicTypeCast = new BasicTypeCast(prevStatement, keyword, type, File);
        return true;
    }

    bool ExpectOneValue([NotNullWhen(true)] out StatementWithValue? statementWithValue, bool allowAsStatement = true)
    {
        statementWithValue = null;

        if (ExpectLambda(out LambdaStatement? lambdaStatement))
        {
            statementWithValue = lambdaStatement;
        }
        else if (ExpectListValue(out LiteralList? listValue))
        {
            statementWithValue = listValue;
        }
        else if (ExpectLiteral(out Literal? literal))
        {
            statementWithValue = literal;
        }
        else if (ExpectTypeCast(out ManagedTypeCast? typeCast))
        {
            statementWithValue = typeCast;
        }
        else if (ExpectExpressionInBrackets(out StatementWithValue? expressionInBrackets))
        {
            statementWithValue = expressionInBrackets;
        }
        else if (ExpectNewExpression(out StatementWithValue? newExpression))
        {
            statementWithValue = newExpression;
        }
        else if (ExpectVariableAddressGetter(out AddressGetter? memoryAddressGetter))
        {
            statementWithValue = memoryAddressGetter;
        }
        else if (ExpectVariableAddressFinder(out Pointer? pointer))
        {
            statementWithValue = pointer;
        }
        else if (ExpectIdentifier(out Token? simpleIdentifier))
        {
            if (simpleIdentifier.Content == StatementKeywords.Type &&
                ExpectType(AllowedType.FunctionPointer, out TypeInstance? typeInstance))
            {
                simpleIdentifier.AnalyzedType = TokenAnalyzedType.Keyword;
                statementWithValue = new TypeStatement(simpleIdentifier, typeInstance, File);
            }
            else
            {
                Identifier identifierStatement = new(simpleIdentifier, File);

                if (simpleIdentifier.Content == StatementKeywords.This)
                { simpleIdentifier.AnalyzedType = TokenAnalyzedType.Keyword; }

                statementWithValue = identifierStatement;
            }
        }

        if (statementWithValue == null)
        { return false; }

        while (true)
        {
            if (ExpectFieldAccessor(statementWithValue, out Field? fieldAccessor))
            {
                statementWithValue = fieldAccessor;
            }
            else if (ExpectIndex(statementWithValue, out IndexCall? statementIndex))
            {
                statementWithValue = statementIndex;
            }
            else if (ExpectAnyCall(statementWithValue, out AnyCall? anyCall))
            {
                statementWithValue = anyCall;
            }
            else
            {
                break;
            }
        }

        if (allowAsStatement && ExpectAsStatement(statementWithValue, out BasicTypeCast? basicTypeCast))
        {
            statementWithValue = basicTypeCast;
        }

        return statementWithValue != null;
    }

    bool ExpectTypeCast([NotNullWhen(true)] out ManagedTypeCast? typeCast)
    {
        typeCast = default;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("(", out Token? leftBracket))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectType(AllowedType.Any | AllowedType.FunctionPointer | AllowedType.StackArrayWithoutLength, out TypeInstance? type))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator(")", out Token? rightBracket))
        // { throw new SyntaxException($"Expected ')' after type of the type cast", type.Position.After(), File); }
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? value, false))
        // { throw new SyntaxException($"Expected one value for the type cast", rightTypeBracket.Position.After(), File); }
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        typeCast = new ManagedTypeCast(value, type, new TokenPair(leftBracket, rightBracket), File);
        return true;
    }

    bool ExpectVariableAddressGetter([NotNullWhen(true)] out AddressGetter? statement)
    {
        statement = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("&", out Token? refToken))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? prevStatement, false))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        refToken.AnalyzedType = TokenAnalyzedType.OtherOperator;

        statement = new AddressGetter(refToken, prevStatement, File);
        return true;
    }

    bool ExpectVariableAddressFinder([NotNullWhen(true)] out Pointer? statement)
    {
        statement = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("*", out Token? refToken))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? prevStatement, false))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        refToken.AnalyzedType = TokenAnalyzedType.OtherOperator;

        statement = new Pointer(refToken, prevStatement, File);
        return true;
    }

    void SetStatementThings(Statement statement)
    {
        if (statement == null)
        {
            if (CurrentToken != null)
            { throw new SyntaxException($"Unknown statement null", CurrentToken, File); }
            else
            { throw new SyntaxException($"Unknown statement null", Position.UnknownPosition, File); }
        }

        if (statement is Literal)
        { throw new SyntaxException($"Unexpected kind of statement \"{statement.GetType().Name}\"", statement, File); }

        if (statement is Identifier)
        { throw new SyntaxException($"Unexpected kind of statement \"{statement.GetType().Name}\"", statement, File); }

        if (statement is NewInstance)
        { throw new SyntaxException($"Unexpected kind of statement \"{statement.GetType().Name}\"", statement, File); }

        if (statement is StatementWithValue statementWithReturnValue)
        { statementWithReturnValue.SaveValue = false; }
    }

    bool ExpectBlock([NotNullWhen(true)] out Block? block, bool consumeSemicolon = true)
    {
        block = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("{", out Token? bracketStart))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        ImmutableArray<Statement>.Builder statements = ImmutableArray.CreateBuilder<Statement>();

        EndlessCheck endlessSafe = new();
        Token? bracketEnd;
        while (!ExpectOperator("}", out bracketEnd))
        {
            if (!ExpectStatement(out Statement? statement))
            {
                SkipCrapTokens();
                throw new SyntaxException($"Expected a statement", CurrentToken?.Position ?? bracketStart.Position.After(), File);
            }

            statements.Add(statement);

            endlessSafe.Step();
        }

        block = new Block(statements.DrainToImmutable(), new TokenPair(bracketStart, bracketEnd), File);

        if (consumeSemicolon && ExpectOperator(";", out Token? semicolon))
        { block.Semicolon = semicolon; }

        return true;
    }

    bool ExpectVariableDeclaration([NotNullWhen(true)] out VariableDeclaration? variableDeclaration)
    {
        variableDeclaration = null;
        int parseStart = CurrentTokenIndex;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers(VariableModifiers);

        TypeInstance? possibleType;
        if (ExpectIdentifier(StatementKeywords.Var, out Token? implicitTypeKeyword))
        {
            implicitTypeKeyword.AnalyzedType = TokenAnalyzedType.Keyword;
            possibleType = new TypeInstanceSimple(implicitTypeKeyword, File);
        }
        else if (!ExpectType(AllowedType.StackArrayWithoutLength | AllowedType.FunctionPointer, out possibleType))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectIdentifier(out Token? possibleVariableName))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        possibleVariableName.AnalyzedType = TokenAnalyzedType.VariableName;

        StatementWithValue? initialValue = null;

        if (ExpectOperator("=", out Token? eqOperatorToken))
        {
            if (!ExpectExpression(out initialValue))
            { throw new SyntaxException("Expected initial value after \"=\" in variable declaration", eqOperatorToken, File); }
        }
        else
        {
            if (possibleType == StatementKeywords.Var)
            { throw new SyntaxException("Initial value for variable declaration with implicit type is required", possibleType, File); }
        }

        variableDeclaration = new VariableDeclaration(
            attributes,
            modifiers,
            possibleType,
            new(possibleVariableName, File),
            initialValue,
            File);
        return true;
    }

    bool ExpectForStatement([NotNullWhen(true)] out ForLoop? forLoop)
    {
        forLoop = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIdentifier(StatementKeywords.For, out Token? keyword))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        if (!ExpectOperator("(", out Token? bracketStart))
        { throw new SyntaxException($"Expected \"(\" after \"{keyword}\" keyword", keyword.Position.After(), File); }

        VariableDeclaration? variableDeclaration;
        if (ExpectOperator(";", out Token? semicolon1))
        {
            variableDeclaration = null;
        }
        else
        {
            if (!ExpectVariableDeclaration(out variableDeclaration))
            { throw new SyntaxException("Expected variable declaration", bracketStart.Position.After(), File); }

            if (!ExpectOperator(";", out semicolon1))
            { throw new SyntaxException($"Expected \";\" after for-loop variable declaration", variableDeclaration.Position.After(), File); }
            variableDeclaration.Semicolon = semicolon1;
        }

        if (!ExpectExpression(out StatementWithValue? condition))
        { throw new SyntaxException($"Expected condition after \"{keyword}\" variable declaration", semicolon1.Position.After(), File); }

        if (!ExpectOperator(";", out Token? semicolon2))
        { throw new SyntaxException($"Expected \";\" after \"{keyword}\" condition", condition.Position.After(), File); }
        condition.Semicolon = semicolon2;

        if (!ExpectAnySetter(out AnyAssignment? anyAssignment))
        { throw new SyntaxException($"Expected an assignment after \"{keyword}\" condition", semicolon2.Position.After(), File); }

        if (!ExpectOperator(")", out Token? bracketEnd))
        { throw new SyntaxException($"Expected \")\" after \"{keyword}\" assignment", anyAssignment.Position.After(), File); }

        if (!ExpectBlock(out Block? block))
        { throw new SyntaxException($"Expected block", bracketEnd.Position.After(), File); }

        forLoop = new ForLoop(keyword, variableDeclaration, condition, anyAssignment, block, File);
        return true;
    }

    bool ExpectWhileStatement([NotNullWhen(true)] out WhileLoop? whileLoop)
    {
        whileLoop = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIdentifier(StatementKeywords.While, out Token? keyword))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        if (!ExpectOperator("(", out Token? bracketStart))
        { throw new SyntaxException($"Expected \"(\" after \"{keyword}\" keyword", keyword.Position.After(), File); }

        if (!ExpectExpression(out StatementWithValue? condition))
        { throw new SyntaxException($"Expected condition after \"{bracketStart}\"", bracketStart.Position.After(), File); }

        if (!ExpectOperator(")", out Token? bracketEnd))
        { throw new SyntaxException($"Expected \")\" after while-loop condition", condition.Position.After(), File); }

        if (!ExpectStatement(out Statement? block))
        { throw new SyntaxException($"Expected a statement after \"{keyword}\" condition", bracketEnd.Position.After(), File); }

        whileLoop = new WhileLoop(keyword, condition, block, File);
        return true;
    }

    bool ExpectIfStatement([NotNullWhen(true)] out IfContainer? ifContainer)
    {
        ifContainer = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIfSegmentStatement(StatementKeywords.If, BaseBranch.IfPart.If, true, out BaseBranch? ifStatement))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        ImmutableArray<BaseBranch>.Builder branches = ImmutableArray.CreateBuilder<BaseBranch>();
        branches.Add(ifStatement);

        EndlessCheck endlessSafe = new();
        while (true)
        {
            if (!ExpectIfSegmentStatement(StatementKeywords.ElseIf, BaseBranch.IfPart.ElseIf, true, out BaseBranch? elseifStatement))
            {
                break;
            }
            branches.Add(elseifStatement);

            endlessSafe.Step();
        }

        if (ExpectIfSegmentStatement(StatementKeywords.Else, BaseBranch.IfPart.Else, false, out BaseBranch? elseStatement))
        {
            branches.Add(elseStatement);
        }

        ifContainer = new IfContainer(branches.DrainToImmutable(), File);
        return true;
    }

    bool ExpectIfSegmentStatement(string keywordName, BaseBranch.IfPart ifSegmentType, bool needParameters, [NotNullWhen(true)] out BaseBranch? branch)
    {
        branch = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIdentifier(keywordName, out Token? keyword))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        StatementWithValue? condition = null;

        Statement? block;

        if (needParameters)
        {
            if (!ExpectOperator("(", out Token? bracketStart))
            { throw new SyntaxException($"Expected \"(\" after keyword \"{keyword}\"", keyword.Position.After(), File); }

            if (!ExpectExpression(out condition))
            { throw new SyntaxException($"Expected condition after \"{keyword} (\"", bracketStart.Position.After(), File); }

            if (!ExpectOperator(")", out Token? bracketEnd))
            { throw new SyntaxException($"Expected \")\" after \"{keyword}\" condition condition", condition.Position.After(), File); }

            if (!ExpectStatement(out block))
            { throw new SyntaxException($"Expected a statement after \"{keyword}\" condition", bracketEnd.Position.After(), File); }
        }
        else
        {
            if (!ExpectStatement(out block))
            { throw new SyntaxException($"Expected a statement after \"{keyword}\" condition", keyword.Position.After(), File); }
        }

        branch = ifSegmentType switch
        {
            BaseBranch.IfPart.If => new IfBranch(keyword, condition ?? throw new UnreachableException(), block, File),
            BaseBranch.IfPart.ElseIf => new ElseIfBranch(keyword, condition ?? throw new UnreachableException(), block, File),
            BaseBranch.IfPart.Else => new ElseBranch(keyword, block, File),
            _ => throw new UnreachableException(),
        };
        return true;
    }

    bool ExpectStatement([NotNullWhen(true)] out Statement? statement)
    {
        if (!ExpectStatementUnchecked(out statement))
        {
            return false;
        }

        SetStatementThings(statement);

        Token? semicolon;
        if (NeedSemicolon(statement))
        {
            if (!ExpectOperator(";", out semicolon))
            { Diagnostics.Add(Diagnostic.Error($"Please put a \";\" here (after \"{statement.GetType().Name}\")", statement.Position.After(), File)); }
        }
        else
        { ExpectOperator(";", out semicolon); }

        statement.Semicolon = semicolon;

        return true;
    }

    bool ExpectStatementUnchecked([NotNullWhen(true)] out Statement? statement)
    {
        if (ExpectInstructionLabel(out InstructionLabel? instructionLabel))
        {
            statement = instructionLabel;
            return true;
        }

        if (ExpectWhileStatement(out WhileLoop? whileLoop))
        {
            statement = whileLoop;
            return true;
        }

        if (ExpectForStatement(out ForLoop? forLoop))
        {
            statement = forLoop;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Return, 0, 1, out KeywordCall? keywordCallReturn))
        {
            statement = keywordCallReturn;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Yield, 0, 1, out KeywordCall? keywordCallYield))
        {
            statement = keywordCallYield;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Goto, 1, out KeywordCall? keywordCallGoto))
        {
            statement = keywordCallGoto;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Crash, 1, out KeywordCall? keywordCallThrow))
        {
            statement = keywordCallThrow;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Break, 0, out KeywordCall? keywordCallBreak))
        {
            statement = keywordCallBreak;
            return true;
        }

        if (ExpectKeywordCall(StatementKeywords.Delete, 1, out KeywordCall? keywordCallDelete))
        {
            statement = keywordCallDelete;
            return true;
        }

        if (ExpectIfStatement(out IfContainer? ifContainer))
        {
            statement = ifContainer;
            return true;
        }

        if (ExpectVariableDeclaration(out VariableDeclaration? variableDeclaration))
        {
            statement = variableDeclaration;
            return true;
        }

        if (ExpectAnySetter(out AnyAssignment? assignment))
        {
            statement = assignment;
            return true;
        }

        if (ExpectExpression(out StatementWithValue? expression))
        {
            statement = expression;
            return true;
        }

        if (ExpectBlock(out Block? block))
        {
            statement = block;
            return true;
        }

        statement = null;
        return false;
    }

    bool ExpectUnaryOperatorCall([NotNullWhen(true)] out UnaryOperatorCall? result)
    {
        result = null;

        if (!ExpectOperator(UnaryPrefixOperators, out Token? unaryPrefixOperator))
        {
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? statement))
        { throw new SyntaxException($"Expected value after operator \"{unaryPrefixOperator}\" (not \"{CurrentToken}\")", unaryPrefixOperator.Position.After(), File); }

        unaryPrefixOperator.AnalyzedType = TokenAnalyzedType.MathOperator;

        result = new UnaryOperatorCall(unaryPrefixOperator, statement, File);
        return true;
    }

    bool ExpectExpression([NotNullWhen(true)] out StatementWithValue? result)
    {
        result = null;

        if (ExpectUnaryOperatorCall(out UnaryOperatorCall? unaryOperatorCall))
        {
            result = unaryOperatorCall;
            return true;
        }

        if (!ExpectModifiedOrOneValue(out StatementWithValue? leftStatement, GeneralStatementModifiers)) return false;

        while (true)
        {
            if (!ExpectOperator(BinaryOperators, out Token? binaryOperator)) break;

            if (!ExpectModifiedOrOneValue(out StatementWithValue? rightStatement, GeneralStatementModifiers))
            {
                if (!ExpectUnaryOperatorCall(out UnaryOperatorCall? rightUnaryOperatorCall))
                { throw new SyntaxException($"Expected value after operator \"{binaryOperator}\" (not \"{CurrentToken}\")", binaryOperator.Position.After(), File); }
                else
                { rightStatement = rightUnaryOperatorCall; }
            }

            binaryOperator.AnalyzedType = TokenAnalyzedType.MathOperator;

            int rightSidePrecedence = OperatorPrecedence(binaryOperator.Content);

            BinaryOperatorCall? rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
            if (rightmostStatement != null)
            {
                rightmostStatement.Right = new BinaryOperatorCall(binaryOperator, rightmostStatement.Right, rightStatement, File);
            }
            else
            {
                leftStatement = new BinaryOperatorCall(binaryOperator, leftStatement, rightStatement, File);
            }
        }

        result = leftStatement;
        return true;
    }

    bool ExpectAnySetter([NotNullWhen(true)] out AnyAssignment? assignment)
    {
        if (ExpectShortOperator(out ShortOperatorCall? shortOperatorCall))
        {
            assignment = shortOperatorCall;
            return true;
        }

        if (ExpectCompoundSetter(out CompoundAssignment? compoundAssignment))
        {
            assignment = compoundAssignment;
            return true;
        }

        if (ExpectSetter(out Assignment? simpleSetter))
        {
            assignment = simpleSetter;
            return true;
        }

        assignment = null;
        return false;
    }

    bool ExpectSetter([NotNullWhen(true)] out Assignment? assignment)
    {
        assignment = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectExpression(out StatementWithValue? leftStatement))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator("=", out Token? @operator))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectExpression(out StatementWithValue? valueToAssign))
        { throw new SyntaxException("Expected expression after assignment operator", @operator, File); }

        @operator.AnalyzedType = TokenAnalyzedType.OtherOperator;

        assignment = new Assignment(@operator, leftStatement, valueToAssign, File);
        return true;
    }

    bool ExpectCompoundSetter([NotNullWhen(true)] out CompoundAssignment? compoundAssignment)
    {
        compoundAssignment = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectExpression(out StatementWithValue? leftStatement))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator(CompoundAssignmentOperators, out Token? @operator))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectExpression(out StatementWithValue? valueToAssign))
        { throw new SyntaxException("Expected expression after compound assignment operator", @operator, File); }

        @operator.AnalyzedType = TokenAnalyzedType.MathOperator;

        compoundAssignment = new CompoundAssignment(@operator, leftStatement, valueToAssign, File);
        return true;
    }

    bool ExpectShortOperator([NotNullWhen(true)] out ShortOperatorCall? shortOperatorCall)
    {
        int parseStart = CurrentTokenIndex;

        if (!ExpectExpression(out StatementWithValue? leftStatement))
        {
            CurrentTokenIndex = parseStart;
            shortOperatorCall = null;
            return false;
        }

        if (ExpectOperator("++", out Token? incrementOperator))
        {
            incrementOperator.AnalyzedType = TokenAnalyzedType.MathOperator;
            shortOperatorCall = new ShortOperatorCall(incrementOperator, leftStatement, File);
            return true;
        }

        if (ExpectOperator("--", out Token? decrementOperator))
        {
            decrementOperator.AnalyzedType = TokenAnalyzedType.MathOperator;
            shortOperatorCall = new ShortOperatorCall(decrementOperator, leftStatement, File);
            return true;
        }

        CurrentTokenIndex = parseStart;
        shortOperatorCall = null;
        return false;
    }

    bool ExpectModifiedOrOneValue([NotNullWhen(true)] out StatementWithValue? oneValue, ImmutableArray<string> validModifiers)
    {
        if (!ExpectIdentifier(out Token? modifier, validModifiers))
        {
            return ExpectOneValue(out oneValue);
        }

        modifier.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectOneValue(out StatementWithValue? value))
        {
            oneValue = new Identifier(modifier, File);
            Diagnostics.Add(Diagnostic.Warning($"is this ok?", oneValue));
            return true;
            // throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After(), File);
        }

        oneValue = new ModifiedStatement(modifier, value, File);
        return true;
    }

    bool ExpectModifiedValue([NotNullWhen(true)] out ModifiedStatement? modifiedStatement, ImmutableArray<string> validModifiers)
    {
        if (!ExpectIdentifier(out Token? modifier, validModifiers))
        {
            modifiedStatement = null;
            return false;
        }

        modifier.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectOneValue(out StatementWithValue? value))
        { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After(), File); }

        modifiedStatement = new ModifiedStatement(modifier, value, File);
        return true;
    }

    static BinaryOperatorCall? FindRightmostStatement(Statement? statement, int rightSidePrecedence)
    {
        if (statement is not BinaryOperatorCall leftSide) return null;
        if (OperatorPrecedence(leftSide.Operator.Content) >= rightSidePrecedence) return null;
        if (leftSide.SurroundingBrackets.HasValue) return null;

        BinaryOperatorCall? right = FindRightmostStatement(leftSide.Right, rightSidePrecedence);

        if (right == null) return leftSide;
        return right;
    }

    static int OperatorPrecedence(string @operator)
    {
        if (LanguageOperators.Precedencies.TryGetValue(@operator, out int precedence))
        { return precedence; }
        throw new InternalExceptionWithoutContext($"Precedence for operator \"{@operator}\" not found");
    }

    bool ExpectAnyCall(StatementWithValue prevStatement, [NotNullWhen(true)] out AnyCall? anyCall)
    {
        anyCall = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("(", out Token? bracketStart))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        bool expectParameter = false;
        ImmutableArray<StatementWithValue>.Builder parameters = ImmutableArray.CreateBuilder<StatementWithValue>();
        ImmutableArray<Token>.Builder commas = ImmutableArray.CreateBuilder<Token>();

        EndlessCheck endlessSafe = new();
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            StatementWithValue? parameter;

            if (ExpectModifiedValue(out ModifiedStatement? modifiedStatement, ArgumentModifiers))
            {
                parameter = modifiedStatement;
            }
            else if (ExpectExpression(out StatementWithValue? simpleParameter))
            {
                parameter = simpleParameter;
            }
            else
            { throw new SyntaxException("Expected expression as a parameter", CurrentToken?.Position ?? PreviousToken!.Position.After(), File); }

            parameters.Add(parameter);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(",", out Token? comma))
            { throw new SyntaxException("Expected \",\" to separate parameters", parameter.Position.After(), File); }
            else
            { expectParameter = true; }
            commas.Add(comma);

            endlessSafe.Step();
        }

        anyCall = new AnyCall(prevStatement, parameters.DrainToImmutable(), commas.DrainToImmutable(), new TokenPair(bracketStart, bracketEnd), File);
        return true;
    }

    bool ExpectKeywordCall(string name, int parameterCount, [NotNullWhen(true)] out KeywordCall? keywordCall)
        => ExpectKeywordCall(name, parameterCount, parameterCount, out keywordCall);
    bool ExpectKeywordCall(string name, int minParameterCount, int maxParameterCount, [NotNullWhen(true)] out KeywordCall? keywordCall)
    {
        keywordCall = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIdentifier(out Token? possibleFunctionName))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (possibleFunctionName.Content != name)
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        possibleFunctionName.AnalyzedType = TokenAnalyzedType.Statement;

        ImmutableArray<StatementWithValue>.Builder? parameters = null;

        EndlessCheck endlessSafe = new();
        while (true)
        {
            endlessSafe.Step();

            if (!ExpectExpression(out StatementWithValue? parameter)) break;

            parameters ??= ImmutableArray.CreateBuilder<StatementWithValue>();
            parameters.Add(parameter);
        }

        keywordCall = new(possibleFunctionName, parameters?.DrainToImmutable() ?? ImmutableArray<StatementWithValue>.Empty, File);

        if (keywordCall.Arguments.Length < minParameterCount)
        { Diagnostics.Add(Diagnostic.Error($"This keyword-call (\"{possibleFunctionName}\") requires minimum {minParameterCount} parameters but you passed {parameters?.Count ?? 0}", keywordCall, File)); }

        if (keywordCall.Arguments.Length > maxParameterCount)
        { Diagnostics.Add(Diagnostic.Error($"This keyword-call (\"{possibleFunctionName}\") requires maximum {maxParameterCount} parameters but you passed {parameters?.Count ?? 0}", keywordCall, File)); }

        return true;
    }

    #endregion

    bool ExpectAttribute([NotNullWhen(true)] out AttributeUsage? attribute)
    {
        attribute = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectOperator("[", out Token? bracketStart))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectIdentifier(out Token? attributeT))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        attributeT.AnalyzedType = TokenAnalyzedType.Attribute;

        List<Literal>? parameters = null;
        if (ExpectOperator("(", out Token? bracketParametersStart))
        {
            EndlessCheck endlessSafe = new();
            while (!ExpectOperator(")"))
            {
                ExpectLiteral(out Literal? param);
                if (param == null)
                { throw new SyntaxException("Expected parameter", bracketParametersStart, File); }
                ExpectOperator(",");

                parameters ??= new();
                parameters.Add(param);

                endlessSafe.Step();
            }
        }

        if (!ExpectOperator("]"))
        { throw new SyntaxException("Unbalanced ]", bracketStart, File); }

        attribute = new AttributeUsage(attributeT, parameters?.ToImmutableArray() ?? ImmutableArray<Literal>.Empty, File);
        return true;
    }
    ImmutableArray<AttributeUsage> ExpectAttributes()
    {
        ImmutableArray<AttributeUsage>.Builder? attributes = null;
        while (ExpectAttribute(out AttributeUsage? attr))
        {
            attributes ??= ImmutableArray.CreateBuilder<AttributeUsage>();
            attributes.Add(attr);
        }
        return attributes?.DrainToImmutable() ?? ImmutableArray<AttributeUsage>.Empty;
    }

    bool ExpectField([NotNullWhen(true)] out FieldDefinition? field, OrderedDiagnosticCollection diagnostic)
    {
        field = null;
        int parseStart = CurrentTokenIndex;

        ImmutableArray<AttributeUsage> attributes = ExpectAttributes();

        ImmutableArray<Token> modifiers = ExpectModifiers();

        if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? possibleType))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected type for field definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectIdentifier(out Token? fieldName))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Expected identifier for field definition", CurrentLocation, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (ExpectOperator("(", out Token? unexpectedThing))
        {
            diagnostic.Add(0, Diagnostic.Critical($"Unexpected `(` after field identifier", unexpectedThing, File, false));
            CurrentTokenIndex = parseStart;
            return false;
        }

        fieldName.AnalyzedType = TokenAnalyzedType.FieldName;

        CheckModifiers(modifiers, FieldModifiers);

        field = new(fieldName, possibleType, modifiers, attributes);
        return true;
    }

    #region Basic parsing

    ImmutableArray<Token> ExpectModifiers() => ExpectModifiers(AllModifiers);

    ImmutableArray<Token> ExpectModifiers(ImmutableArray<string> modifiers)
    {
        ImmutableArray<Token>.Builder? result = null;

        EndlessCheck endlessSafe = new();
        while (true)
        {
            if (ExpectIdentifier(out Token? modifier, modifiers))
            {
                result ??= ImmutableArray.CreateBuilder<Token>();
                modifier.AnalyzedType = TokenAnalyzedType.Keyword;
                result.Add(modifier);
            }
            else
            { break; }

            endlessSafe.Step();
        }

        return result?.DrainToImmutable() ?? ImmutableArray<Token>.Empty;
    }

    void CheckParameterModifiers(IEnumerable<Token> modifiers, int parameterIndex, ImmutableArray<string> validModifiers)
    {
        foreach (Token modifier in modifiers)
        {
            if (!validModifiers.Contains(modifier.Content))
            { Diagnostics.Add(Diagnostic.Error($"Modifier \"{modifier}\" not valid in the current context", modifier, File)); }

            if (modifier.Content == ModifierKeywords.This &&
                parameterIndex != 0)
            { Diagnostics.Add(Diagnostic.Error($"Modifier \"{ModifierKeywords.This}\" only valid on the first parameter", modifier, File)); }
        }
    }

    void CheckModifiers(IEnumerable<Token> modifiers, ImmutableArray<string> validModifiers)
    {
        foreach (Token modifier in modifiers)
        {
            if (!validModifiers.Contains(modifier.Content))
            { Diagnostics.Add(Diagnostic.Error($"Modifier \"{modifier}\" not valid in the current context", modifier, File)); }
        }
    }

    bool ExpectInstructionLabel([NotNullWhen(true)] out InstructionLabel? instructionLabel)
    {
        instructionLabel = null;
        int parseStart = CurrentTokenIndex;

        if (!ExpectIdentifier(out Token? identifier))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        if (!ExpectOperator(":", out Token? colon))
        {
            CurrentTokenIndex = parseStart;
            return false;
        }

        identifier.AnalyzedType = TokenAnalyzedType.InstructionLabel;

        instructionLabel = new InstructionLabel(
            new Identifier(identifier, File),
            colon,
            File
        );
        return true;
    }

    bool ExpectIdentifier([NotNullWhen(true)] out Token? result) => ExpectIdentifier("", out result);
    bool ExpectIdentifier(string name, [NotNullWhen(true)] out Token? result)
    {
        result = null;
        SkipCrapTokens();
        if (CurrentToken == null) return false;
        if (CurrentToken.TokenType != TokenType.Identifier) return false;
        if (name.Length > 0 && CurrentToken.Content != name) return false;

        result = CurrentToken;
        CurrentTokenIndex++;

        return true;
    }
    bool ExpectIdentifier([NotNullWhen(true)] out Token? result, ImmutableArray<string> names)
    {
        foreach (string name in names)
        {
            if (ExpectIdentifier(name, out result))
            { return true; }
        }
        result = null;
        return false;
    }

    bool ExpectOperator(string name) => ExpectOperator(name, out _);
    bool ExpectOperator(ImmutableArray<string> name, [NotNullWhen(true)] out Token? result)
    {
        result = null;
        SkipCrapTokens();
        if (CurrentToken == null) return false;
        if (CurrentToken.TokenType != TokenType.Operator) return false;
        if (!name.Contains(CurrentToken.Content)) return false;

        result = CurrentToken;
        CurrentTokenIndex++;

        return true;
    }
    bool ExpectOperator(string name, [NotNullWhen(true)] out Token? result)
    {
        result = null;
        SkipCrapTokens();
        if (CurrentToken == null) return false;
        if (CurrentToken.TokenType != TokenType.Operator) return false;
        if (name.Length > 0 && CurrentToken.Content != name) return false;

        result = CurrentToken;
        CurrentTokenIndex++;

        return true;
    }

    void SkipCrapTokens()
    {
        while (CurrentToken is not null &&
               CurrentToken.TokenType is
               TokenType.PreprocessIdentifier or
               TokenType.PreprocessArgument or
               TokenType.PreprocessSkipped)
        { CurrentTokenIndex++; }
    }

    [Flags]
    enum AllowedType
    {
        None = 0x0,
        Any = 0x1,
        FunctionPointer = 0x2,
        StackArrayWithoutLength = 0x4,
    }

    static readonly ImmutableArray<string> TheseCharactersIndicateThatTheIdentifierWillBeFollowedByAComplexType = ImmutableArray.Create("<", "(", "[");

    bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type)
    {
        if (ExpectType(flags, out type, out Diagnostic? error))
        { return true; }
        if (error is not null)
        { Diagnostics.Add(error.Break()); }
        return false;
    }

    bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type, [MaybeNullWhen(true)] out Diagnostic? error)
    {
        type = default;
        error = null;

        if (!ExpectIdentifier(out Token? possibleType)) return false;

        if (possibleType.Equals(StatementKeywords.Return))
        { return false; }

        Token? closureModifier = null;
        if (possibleType.Content.StartsWith('@'))
        {
            closureModifier = possibleType[..1];
            possibleType = possibleType[1..];
        }

        type = new TypeInstanceSimple(possibleType, File);

        if (possibleType.Content.Equals(TypeKeywords.Any))
        {
            possibleType.AnalyzedType = TokenAnalyzedType.Keyword;

            if (ExpectOperator(TheseCharactersIndicateThatTheIdentifierWillBeFollowedByAComplexType, out Token? illegalT))
            { Diagnostics.Add(Diagnostic.Error($"This is not allowed", illegalT, File)); }

            if (ExpectOperator("*", out Token? pointerOperator))
            {
                pointerOperator.AnalyzedType = TokenAnalyzedType.TypeModifier;
                type = new TypeInstancePointer(type, pointerOperator, File);
            }
            else
            {
                if ((flags & AllowedType.Any) == 0)
                {
                    error = Diagnostic.Error($"Type \"{TypeKeywords.Any}\" is not valid in the current context", possibleType, File);
                    return false;
                }
            }

            goto end;
        }

        if (TypeKeywords.List.Contains(possibleType.Content))
        {
            possibleType.AnalyzedType = TokenAnalyzedType.BuiltinType;
        }
        else
        {
            possibleType.AnalyzedType = TokenAnalyzedType.Type;
        }

        int afterIdentifier = CurrentTokenIndex;
        bool withGenerics = false;

        while (true)
        {
            if (ExpectOperator("*", out Token? pointerOperator))
            {
                pointerOperator.AnalyzedType = TokenAnalyzedType.TypeModifier;
                type = new TypeInstancePointer(type, pointerOperator, File);
            }
            else if (ExpectOperator("<"))
            {
                if (type is not TypeInstanceSimple)
                { throw new NotImplementedException(); }

                List<TypeInstance> genericTypes = new();

                while (true)
                {
                    if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? typeParameter))
                    {
                        CurrentTokenIndex = afterIdentifier;
                        goto end;
                    }

                    genericTypes.Add(typeParameter);

                    if (ExpectOperator(">"))
                    { break; }

                    if (ExpectOperator(">>", out Token? doubleEnd))
                    {
                        (Token? newA, Token? newB) = doubleEnd.Slice(1);
                        if (newA == null || newB == null)
                        { throw new UnreachableException($"I failed at token splitting :("); }
                        CurrentTokenIndex--;
                        Tokens[CurrentTokenIndex] = newB;
                        break;
                    }

                    if (ExpectOperator(","))
                    { continue; }
                }

                type = new TypeInstanceSimple(possibleType, File, genericTypes.ToImmutableArray());
                withGenerics = true;
            }
            else if (!withGenerics && ExpectOperator("(", out Token? bracketStart))
            {
                if (!flags.HasFlag(AllowedType.FunctionPointer))
                {
                    CurrentTokenIndex--;
                    goto end;
                }

                List<TypeInstance> parameterTypes = new();
                Token? bracketEnd;
                while (!ExpectOperator(")", out bracketEnd))
                {
                    if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? subtype))
                    {
                        CurrentTokenIndex = afterIdentifier;
                        goto end;
                    }

                    parameterTypes.Add(subtype);

                    if (ExpectOperator(")", out bracketEnd))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }

                type = new TypeInstanceFunction(type, parameterTypes.ToImmutableArray(), closureModifier, File, new(bracketStart, bracketEnd));
            }
            else if (ExpectOperator("[", out _))
            {
                if (ExpectOperator("]"))
                {
                    type = new TypeInstanceStackArray(type, null, File);
                }
                else if (ExpectExpression(out StatementWithValue? sizeValue))
                {
                    if (!ExpectOperator("]"))
                    { return false; }

                    type = new TypeInstanceStackArray(type, sizeValue, File);
                }
                else
                {
                    return false;
                }
            }
            else
            { break; }
        }

    end:
        if (type is not TypeInstanceFunction && closureModifier is not null)
        {
            error = Diagnostic.Error($"This type modifier is bruh", closureModifier, File);
            return false;
        }

        return true;
    }

    static bool NeedSemicolon(Statement statement) => statement is not (
        ForLoop or
        WhileLoop or
        Block or
        IfContainer or
        BaseBranch or
        InstructionLabel or
        LambdaStatement
    );

    #endregion
}
