namespace LanguageCore.Parser;

using Statement;
using Tokenizing;

public sealed class Parser
{
    int CurrentTokenIndex;
    readonly Token[] Tokens;
    readonly ImmutableArray<Token> OriginalTokens;
    readonly Uri? File;

    Token? CurrentToken => (CurrentTokenIndex >= 0 && CurrentTokenIndex < Tokens.Length) ? Tokens[CurrentTokenIndex] : null;
    Token? PreviousToken => (CurrentTokenIndex >= 1 && CurrentTokenIndex <= Tokens.Length) ? Tokens[CurrentTokenIndex - 1] : null;

    static readonly string[] Modifiers = new string[]
    {
        ProtectionKeywords.Export,
        ModifierKeywords.Inline,
    };

    static readonly string[] FunctionModifiers = new string[]
    {
        ProtectionKeywords.Export,
        ModifierKeywords.Inline,
    };

    static readonly string[] GeneralStatementModifiers = new string[]
    {
        ModifierKeywords.Temp,
    };

    static readonly string[] VariableDefinitionModifiers = new string[]
    {
        ProtectionKeywords.Export,
        ModifierKeywords.Temp,
        ModifierKeywords.Const,
    };

    static readonly string[] ParameterDefinitionModifiers = new string[]
    {
        ModifierKeywords.This,
        ModifierKeywords.Ref,
        ModifierKeywords.Temp,
    };

    static readonly string[] PassedParameterModifiers = new string[]
    {
        ModifierKeywords.Ref,
        ModifierKeywords.Temp,
    };

    static readonly string[] OverloadableOperators = new string[]
    {
        "<<", ">>",
        "+", "-", "*", "/", "%",
        "&", "|", "^",
        "<", ">", ">=", "<=", "!=", "==",
        "&&", "||",
    };

    static readonly string[] CompoundAssignmentOperators = new string[]
    {
        "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^=",
    };

    static readonly string[] BinaryOperators = new string[]
    {
        "<<", ">>",
        "+", "-", "*", "/", "%",
        "&", "|", "^",
        "<", ">", ">=", "<=", "!=", "==", "&&", "||",
    };

    static readonly string[] UnaryPrefixOperators = new string[]
    {
        "!", "~",
    };

#pragma warning disable RCS1213, IDE0052, CA1823 // Remove unread private members
    static readonly string[] UnaryPostfixOperators = Array.Empty<string>();
#pragma warning restore RCS1213, IDE0052, CA1823

    // === Result ===
    readonly List<Error> Errors = new();
    readonly List<FunctionDefinition> Functions = new();
    readonly List<FunctionDefinition> Operators = new();
    readonly List<EnumDefinition> Enums = new();
    readonly Dictionary<string, StructDefinition> Structs = new();
    readonly List<UsingDefinition> Usings = new();
    readonly List<CompileTag> Hashes = new();
    readonly List<Statement.Statement> TopLevelStatements = new();
    // === ===

    Parser(ImmutableArray<Token> tokens, Uri? file)
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
    }

    /// <exception cref="EndlessLoopException"/>
    /// <exception cref="SyntaxException"/>
    /// <exception cref="InternalException"/>
    /// <exception cref="TokenizerException"/>
    public static ParserResult ParseFile(string filePath, IEnumerable<string> preprocessorVariables, TokenizerSettings? tokenizerSettings = null, ConsoleProgressBar? tokenizerProgressBar = null)
    {
        TokenizerResult tokens = StreamTokenizer.Tokenize(filePath, preprocessorVariables, tokenizerSettings, tokenizerProgressBar);
        Uri uri = new(filePath, UriKind.Absolute);
        ParserResult result = new Parser(tokens.Tokens, uri).ParseInternal();
        result.SetFile(uri);
        return result;
    }

    /// <exception cref="EndlessLoopException"/>
    /// <exception cref="SyntaxException"/>
    public static ParserResult Parse(ImmutableArray<Token> tokens, Uri? file)
        => new Parser(tokens, file).ParseInternal();

    public static Statement.Statement ParseStatement(ImmutableArray<Token> tokens, Uri? file)
        => new Parser(tokens, file).ParseStatementInternal();

    ParserResult ParseInternal()
    {
        CurrentTokenIndex = 0;

        ParseCodeHeader();

        int endlessSafe = 0;
        while (CurrentToken != null)
        {
            ParseCodeBlock();

            SkipCrapTokens();

            endlessSafe++;
            if (endlessSafe > 500) { throw new EndlessLoopException(); }
        }

        return new ParserResult(
            Errors,
            Functions,
            Operators,
            Structs.Values,
            Usings,
            Hashes,
            TopLevelStatements,
            Enums,
            OriginalTokens,
            Tokens);
    }

    Statement.Statement ParseStatementInternal()
    {
        if (ExpectStatementUnchecked(out Statement.Statement? statement))
        {
            if (Errors.Count > 0) { throw Errors[0].ToException(); }
            return statement;
        }
        else
        { throw new SyntaxException($"Expected something but not \"{CurrentToken}\"", CurrentToken, File); }
    }

    #region Parse top level

    bool ExpectHash([NotNullWhen(true)] out CompileTag? hashStatement)
    {
        hashStatement = null;

        if (!ExpectOperator("#", out Token? hashT))
        { return false; }

        hashT.AnalyzedType = TokenAnalyzedType.CompileTag;

        if (!ExpectIdentifier(out Token? hashName))
        { throw new SyntaxException($"There should be an identifier (after \"#\"), but you wrote {CurrentToken?.TokenType} \"{CurrentToken?.Content}\"", hashT, File); }

        hashName.AnalyzedType = TokenAnalyzedType.CompileTag;

        List<Literal> parameters = new();
        int endlessSafe = 50;
        Token? semicolon;
        while (!ExpectOperator(";", out semicolon))
        {
            if (!ExpectLiteral(out Literal? parameter))
            {
                if (CurrentToken is null)
                {
                    throw new SyntaxException($"There should be a literal or \";\", but you wrote nothing", PreviousToken?.Position.After(), File);
                }
                else
                {
                    throw new SyntaxException($"There should be a literal or \";\", but you wrote {CurrentToken.TokenType} \"{CurrentToken.Content}\"", CurrentToken.Position, File);
                }
            }

            parameter.ValueToken.AnalyzedType = TokenAnalyzedType.CompileTagParameter;
            parameters.Add(parameter);

            if (ExpectOperator(";", out semicolon))
            { break; }

            endlessSafe--;
            if (endlessSafe <= 0)
            { throw new EndlessLoopException(); }
        }

        hashStatement = new CompileTag(hashT, hashName, parameters.ToArray())
        {
            Semicolon = semicolon,
        };

        return true;
    }

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
            int endlessSafe = 50;
            while (ExpectIdentifier(out Token? pathIdentifier))
            {
                tokens.Add(pathIdentifier);

                ExpectOperator(".");

                endlessSafe--;
                if (endlessSafe <= 0)
                { throw new EndlessLoopException(); }
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
                Errors.Add(new Error($"Expected library name after \"{DeclarationKeywords.Using}\"", keyword, File));
            }
            return false;
        }

        if (!ExpectOperator(";"))
        { throw new SyntaxException($"Please put a \";\" here (after \"{DeclarationKeywords.Using}\")", keyword.Position.After(), File); }

        usingDefinition = new UsingDefinition(keyword, tokens.ToArray());

        return true;
    }

    void ParseCodeHeader()
    {
        while (true)
        {
            if (ExpectHash(out CompileTag? hashStatement))
            { Hashes.Add(hashStatement); }
            else if (ExpectUsing(out UsingDefinition? usingDefinition))
            { Usings.Add(usingDefinition); }
            else
            { break; }
        }
    }

    void ParseCodeBlock()
    {
        if (ExpectStructDefinition()) { }
        else if (ExpectClassDefinition()) { }
        else if (ExpectFunctionDefinition(out FunctionDefinition? functionDefinition))
        { Functions.Add(functionDefinition); }
        else if (ExpectOperatorDefinition(out FunctionDefinition? operatorDefinition))
        { Operators.Add(operatorDefinition); }
        else if (ExpectEnumDefinition(out EnumDefinition? enumDefinition))
        { Enums.Add(enumDefinition); }
        else if (ExpectStatement(out Statement.Statement? statement))
        { TopLevelStatements.Add(statement); }
        else
        { throw new SyntaxException($"Expected something but not \"{CurrentToken}\"", CurrentToken, File); }
    }

    bool ExpectEnumDefinition([NotNullWhen(true)] out EnumDefinition? enumDefinition)
    {
        int parseStart = CurrentTokenIndex;
        enumDefinition = null;

        AttributeUsage[] attributes = ExpectAttributes();

        if (!ExpectIdentifier(DeclarationKeywords.Enum, out Token? keyword))
        { CurrentTokenIndex = parseStart; return false; }

        keyword.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectIdentifier(out Token? identifier))
        { throw new SyntaxException($"Expected an identifier (the enum's name) after keyword \"{keyword}\"", keyword.Position.After(), File); }

        if (!ExpectOperator("{"))
        { throw new SyntaxException($"There should be a \"{{\" (after enum identifier)", identifier.Position.After(), File); }

        identifier.AnalyzedType = TokenAnalyzedType.Enum;

        List<EnumMemberDefinition> members = new();

        while (!ExpectOperator("}"))
        {
            if (!ExpectIdentifier(out Token? enumMemberIdentifier))
            { throw new SyntaxException($"There should be an identifier (the enum member's name) (not \"{CurrentToken}\")", CurrentToken?.Position ?? PreviousToken?.Position.After(), File); }

            enumMemberIdentifier.AnalyzedType = TokenAnalyzedType.EnumMember;

            StatementWithValue? enumMemberValue = null;

            if (ExpectOperator("=", out Token? assignOperator))
            {
                if (!ExpectOneValue(out enumMemberValue))
                { throw new SyntaxException($"There should be a value (after enum member) and not \"{CurrentToken}\"", assignOperator.Position.After(), File); }
            }

            members.Add(new EnumMemberDefinition(enumMemberIdentifier, enumMemberValue));

            if (ExpectOperator("}"))
            { break; }

            if (ExpectOperator(","))
            { continue; }

            throw new SyntaxException($"Expected \",\" or \"}}\" (not \"{CurrentToken}\")", PreviousToken?.Position.After(), File);
        }

        enumDefinition = new(identifier, attributes, members);

        return true;
    }

    bool ExpectOperatorDefinition([NotNullWhen(true)] out FunctionDefinition? function)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        AttributeUsage[] attributes = ExpectAttributes();

        Token[] modifiers = ParseModifiers();

        if (!ExpectType(AllowedType.None, out TypeInstance? possibleType, out _))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectOperator(OverloadableOperators, out Token? possibleName))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectOperator("(", out Token? bracketStart))
        { CurrentTokenIndex = parseStart; return false; }

        possibleName.AnalyzedType = TokenAnalyzedType.FunctionName;

        List<ParameterDefinition> parameters = new();

        bool expectParameter = false;
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
            CheckModifiers(parameterModifiers, ModifierKeywords.This, ModifierKeywords.Temp);

            if (!ExpectType(AllowedType.None, out TypeInstance? parameterType))
            { throw new SyntaxException("Expected a type (the parameter's type)", PreviousToken?.Position.After(), File); }

            if (!ExpectIdentifier(out Token? parameterIdentifier))
            { throw new SyntaxException("Expected an identifier (the parameter's name)", parameterType.Position.After(), File); }

            parameterIdentifier.AnalyzedType = TokenAnalyzedType.VariableName;

            ParameterDefinition parameterDefinition = new(parameterModifiers, parameterType, parameterIdentifier);
            parameters.Add(parameterDefinition);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException($"Expected \",\" or \")\" (not \"{CurrentToken}\")", PreviousToken?.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        CheckModifiers(modifiers, ProtectionKeywords.Export);

        Block? block = null;

        if (!ExpectOperator(";") && !ExpectBlock(out block))
        { throw new SyntaxException($"Expected \";\" or block", PreviousToken?.Position.After(), File); }

        function = new FunctionDefinition(
            attributes,
            modifiers,
            possibleType,
            possibleName,
            new ParameterDefinitionCollection(parameters, new TokenPair(bracketStart, bracketEnd)),
            null)
        {
            Block = block
        };

        return true;
    }

    bool ExpectTemplateInfo([NotNullWhen(true)] out TemplateInfo? templateInfo)
    {
        if (!ExpectIdentifier(DeclarationKeywords.Template, out Token? keyword))
        {
            templateInfo = null;
            return false;
        }

        if (!ExpectOperator("<", out Token? startBracket))
        { throw new SyntaxException($"There should be an \"<\" (after \"{keyword}\" keyword) and not \"{CurrentToken}\"", keyword.Position.After(), File); }

        List<Token> parameters = new();

        Token? endBracket;

        bool expectParameter = false;
        while (!ExpectOperator(">", out endBracket) || expectParameter)
        {
            if (!ExpectIdentifier(out Token? parameter))
            { throw new SyntaxException("Expected identifier or \">\"", PreviousToken?.Position.After(), File); }

            parameters.Add(parameter);

            if (ExpectOperator(">", out endBracket))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" or \">\"", parameter.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        templateInfo = new(keyword, new TokenPair(startBracket, endBracket), parameters);

        return true;
    }

    bool ExpectFunctionDefinition([NotNullWhen(true)] out FunctionDefinition? function)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        AttributeUsage[] attributes = ExpectAttributes();

        ExpectTemplateInfo(out TemplateInfo? templateInfo);

        Token[] modifiers = ParseModifiers();

        if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? possibleType, out _))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectIdentifier(out Token? possibleNameT))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectOperator("(", out Token? bracketStart))
        { CurrentTokenIndex = parseStart; return false; }

        possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

        List<ParameterDefinition> parameters = new();

        Token? bracketEnd;

        bool expectParameter = false;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
            CheckModifiers(parameterModifiers, ModifierKeywords.This, ModifierKeywords.Ref, ModifierKeywords.Temp);

            if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? parameterType))
            { throw new SyntaxException("Expected parameter type", PreviousToken?.Position.After(), File); }

            if (!ExpectIdentifier(out Token? parameterIdentifier))
            { throw new SyntaxException("Expected a parameter name", parameterType.Position.After(), File); }

            parameterIdentifier.AnalyzedType = TokenAnalyzedType.VariableName;

            ParameterDefinition parameterDefinition = new(parameterModifiers, parameterType, parameterIdentifier);
            parameters.Add(parameterDefinition);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" or \")\" after parameter definition", parameterDefinition.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        CheckModifiers(modifiers, FunctionModifiers);

        Block? block = null;

        if (!ExpectOperator(";") && !ExpectBlock(out block))
        { throw new SyntaxException($"Expected \";\" or block", bracketEnd.Position.After(), File); }

        function = new FunctionDefinition(
            attributes,
            modifiers,
            possibleType,
            possibleNameT,
            new ParameterDefinitionCollection(parameters, new TokenPair(bracketStart, bracketEnd)),
            templateInfo)
        {
            Block = block
        };

        return true;
    }

    bool ExpectGeneralFunctionDefinition([NotNullWhen(true)] out GeneralFunctionDefinition? function)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        Token[] modifiers = ParseModifiers();

        if (!ExpectIdentifier(out Token? possibleNameT))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectOperator("(", out Token? bracketStart))
        { CurrentTokenIndex = parseStart; return false; }

        possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

        List<ParameterDefinition> parameters = new();

        bool expectParameter = false;
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
            CheckModifiers(parameterModifiers, ModifierKeywords.Temp);

            if (!ExpectType(AllowedType.None, out TypeInstance? parameterType))
            { throw new SyntaxException("Expected parameter type", PreviousToken?.Position.After(), File); }

            if (!ExpectIdentifier(out Token? parameterIdentifier))
            { throw new SyntaxException("Expected a parameter name", parameterType.Position.After(), File); }

            parameterIdentifier.AnalyzedType = TokenAnalyzedType.VariableName;

            ParameterDefinition parameterDefinition = new(parameterModifiers, parameterType, parameterIdentifier);
            parameters.Add(parameterDefinition);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" or \")\"", PreviousToken?.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        CheckModifiers(modifiers, ProtectionKeywords.Export);

        Block? block = null;
        if (ExpectOperator(";", out Token? semicolon) || !ExpectBlock(out block))
        { Errors.Add(new Error($"Body is required for general function definition", semicolon?.Position ?? CurrentToken?.Position ?? PreviousToken?.Position.After(), File)); }

        function = new GeneralFunctionDefinition(
            possibleNameT,
            modifiers,
            new ParameterDefinitionCollection(parameters, new TokenPair(bracketStart, bracketEnd)))
        {
            Block = block
        };

        return true;
    }

    bool ExpectConstructorDefinition([NotNullWhen(true)] out ConstructorDefinition? function)
    {
        int parseStart = CurrentTokenIndex;
        function = null;

        Token[] modifiers = ParseModifiers();

        if (!ExpectType(AllowedType.None, out TypeInstance? type))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectOperator("(", out Token? bracketStart))
        { CurrentTokenIndex = parseStart; return false; }

        List<ParameterDefinition> parameters = new();

        bool expectParameter = false;
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
            CheckModifiers(parameterModifiers, ModifierKeywords.Temp);

            if (!ExpectType(AllowedType.None, out TypeInstance? parameterType))
            { throw new SyntaxException("Expected parameter type", PreviousToken?.Position.After(), File); }

            if (!ExpectIdentifier(out Token? parameterIdentifier))
            { throw new SyntaxException("Expected a parameter name", parameterType.Position.After(), File); }

            parameterIdentifier.AnalyzedType = TokenAnalyzedType.VariableName;

            ParameterDefinition parameterDefinition = new(parameterModifiers, parameterType, parameterIdentifier);
            parameters.Add(parameterDefinition);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" or \")\"", PreviousToken?.Position.After(), File); }
            else
            { expectParameter = true; }
        }

        CheckModifiers(modifiers, ProtectionKeywords.Export);

        Block? block = null;
        if (ExpectOperator(";", out Token? semicolon) || !ExpectBlock(out block))
        { Errors.Add(new Error($"Body is required for constructor definition", semicolon?.Position ?? CurrentToken?.Position ?? PreviousToken?.Position.After(), File)); }

        function = new ConstructorDefinition(
            type,
            modifiers,
            new ParameterDefinitionCollection(parameters, new TokenPair(bracketStart, bracketEnd)))
        {
            Block = block
        };

        return true;
    }

    bool ExpectClassDefinition()
    {
        int startTokenIndex = CurrentTokenIndex;

        ExpectAttributes();

        ExpectTemplateInfo(out _);

        Token[] modifiers = ParseModifiers();

        if (!ExpectIdentifier("class", out Token? keyword))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (!ExpectIdentifier(out Token? possibleClassName))
        { throw new SyntaxException($"Expected class identifier after keyword \"{keyword}\"", keyword.Position.After(), File); }

        if (!ExpectOperator("{", out _))
        { throw new SyntaxException("Expected \"{\" after class identifier", possibleClassName.Position.After(), File); }

        keyword.AnalyzedType = TokenAnalyzedType.Keyword;

        List<FieldDefinition> fields = new();
        List<FunctionDefinition> methods = new();
        List<FunctionDefinition> operators = new();
        List<GeneralFunctionDefinition> generalMethods = new();

        int endlessSafe = 0;
        while (!ExpectOperator("}", out _))
        {
            if (ExpectOperatorDefinition(out FunctionDefinition? operatorDefinition))
            { operators.Add(operatorDefinition); }
            else if (ExpectFunctionDefinition(out FunctionDefinition? methodDefinition))
            { methods.Add(methodDefinition); }
            else if (ExpectGeneralFunctionDefinition(out GeneralFunctionDefinition? generalMethodDefinition))
            { generalMethods.Add(generalMethodDefinition); }
            else if (ExpectField(out FieldDefinition? field))
            {
                fields.Add(field);
                if (ExpectOperator(";", out Token? semicolon))
                { field.Semicolon = semicolon; }
            }
            else if (ExpectConstructorDefinition(out _))
            { }
            else
            { throw new SyntaxException($"Expected field definition or \"}}\"", CurrentToken?.Position ?? PreviousToken?.Position.After(), File); }

            endlessSafe++;
            if (endlessSafe > 50)
            {
                throw new EndlessLoopException();
            }
        }

        CheckModifiers(modifiers, ProtectionKeywords.Export);

        return true;
    }

    bool ExpectStructDefinition()
    {
        int startTokenIndex = CurrentTokenIndex;

        AttributeUsage[] attributes = ExpectAttributes();

        ExpectTemplateInfo(out TemplateInfo? templateInfo);

        Token[] modifiers = ParseModifiers();

        if (!ExpectIdentifier(DeclarationKeywords.Struct, out Token? keyword))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (!ExpectIdentifier(out Token? possibleStructName))
        { throw new SyntaxException($"Expected struct identifier after keyword \"{keyword}\"", keyword.Position.After(), File); }

        if (!ExpectOperator("{", out Token? bracketStart))
        { throw new SyntaxException("Expected \"{\" after struct identifier", possibleStructName.Position.After(), File); }

        possibleStructName.AnalyzedType = TokenAnalyzedType.Struct;
        keyword.AnalyzedType = TokenAnalyzedType.Keyword;

        List<FieldDefinition> fields = new();
        List<FunctionDefinition> methods = new();
        List<FunctionDefinition> operators = new();
        List<GeneralFunctionDefinition> generalMethods = new();
        List<ConstructorDefinition> constructors = new();

        int endlessSafe = 0;
        Token? bracketEnd;
        while (!ExpectOperator("}", out bracketEnd))
        {
            if (ExpectOperatorDefinition(out FunctionDefinition? operatorDefinition))
            {
                operators.Add(operatorDefinition);
            }
            else if (ExpectFunctionDefinition(out FunctionDefinition? methodDefinition))
            {
                methods.Add(methodDefinition);
            }
            else if (ExpectGeneralFunctionDefinition(out GeneralFunctionDefinition? generalMethodDefinition))
            {
                generalMethods.Add(generalMethodDefinition);
            }
            else if (ExpectConstructorDefinition(out ConstructorDefinition? constructorDefinition))
            {
                constructors.Add(constructorDefinition);
            }
            else if (ExpectField(out FieldDefinition? field))
            {
                fields.Add(field);
                if (ExpectOperator(";", out Token? semicolon))
                { field.Semicolon = semicolon; }
            }
            else
            {
                throw new SyntaxException("Expected field definition or \"}\"", CurrentToken?.Position ?? PreviousToken?.Position.After(), File);
            }

            endlessSafe++;
            if (endlessSafe > 50)
            {
                throw new EndlessLoopException();
            }
        }

        CheckModifiers(modifiers, ProtectionKeywords.Export);

        StructDefinition structDefinition = new(
            possibleStructName,
            bracketStart,
            bracketEnd,
            attributes,
            modifiers,
            fields,
            methods,
            generalMethods,
            operators,
            constructors)
        {
            Template = templateInfo,
        };

        Structs.Add(structDefinition.Identifier.Content, structDefinition);

        return true;
    }

    #endregion

    #region Parse low level

    bool ExpectListValue([NotNullWhen(true)] out LiteralList? listValue)
    {
        if (!ExpectOperator("[", out Token? bracketStart))
        {
            listValue = null;
            return false;
        }

        List<StatementWithValue> values = new();

        Token? bracketEnd;

        int endlessSafe = 0;
        while (true)
        {
            if (ExpectExpression(out StatementWithValue? v))
            {
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

            endlessSafe++;
            if (endlessSafe >= 50) { throw new EndlessLoopException(); }
        }

        listValue = new LiteralList(values, new TokenPair(bracketStart, bracketEnd));
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

            Literal literal = new(LiteralType.Float, v, CurrentToken);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralNumber)
        {
            v = v.Replace("_", string.Empty, StringComparison.Ordinal);

            Literal literal = new(LiteralType.Integer, v, CurrentToken);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralHex)
        {
            v = v[2..];
            v = v.Replace("_", string.Empty, StringComparison.Ordinal);

            Literal literal = new(LiteralType.Integer, Convert.ToInt32(v, 16).ToString(CultureInfo.InvariantCulture), CurrentToken);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralBinary)
        {
            v = v[2..];
            v = v.Replace("_", string.Empty, StringComparison.Ordinal);

            Literal literal = new(LiteralType.Integer, Convert.ToInt32(v, 2).ToString(CultureInfo.InvariantCulture), CurrentToken);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralString)
        {
            Literal literal = new(LiteralType.String, v, CurrentToken);

            CurrentTokenIndex++;

            statement = literal;
            return true;
        }
        else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralCharacter)
        {
            Literal literal = new(LiteralType.Char, v, CurrentToken);

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
        if (!ExpectOperator("[", out Token? bracketStart))
        {
            statement = null;
            return false;
        }

        if (!ExpectExpression(out StatementWithValue? expression))
        {
            statement = null;
            return false;
        }

        if (!ExpectOperator("]", out Token? bracketEnd))
        { throw new SyntaxException("Unbalanced [", bracketStart, File); }

        statement = new IndexCall(prevStatement, expression, new TokenPair(bracketStart, bracketEnd));
        return true;
    }

    bool ExpectOneValue([NotNullWhen(true)] out StatementWithValue? statementWithValue, bool allowAsStatement = true)
    {
        statementWithValue = null;

        if (ExpectListValue(out LiteralList? listValue))
        {
            statementWithValue = listValue;
        }
        else if (ExpectLiteral(out Literal? literal))
        {
            statementWithValue = literal;
        }
        else if (ExpectOperator("(", out Token? bracketStart1))
        {
            if (!ExpectExpression(out StatementWithValue? expression))
            { throw new SyntaxException("Expected expression after \"(\"", bracketStart1.Position.After(), File); }

            if (!ExpectOperator(")", out Token? bracketEnd1))
            { throw new SyntaxException("Unbalanced \"(\"", bracketStart1, File); }

            expression.SurroundingBracelet = new TokenPair(bracketStart1, bracketEnd1);

            statementWithValue = expression;
        }
        else if (ExpectIdentifier(StatementKeywords.New, out Token? keywordNew))
        {
            keywordNew.AnalyzedType = TokenAnalyzedType.Keyword;

            if (!ExpectType(AllowedType.None, out TypeInstance? instanceTypeName))
            { throw new SyntaxException("Expected instance constructor after keyword \"new\"", keywordNew, File); }

            if (ExpectOperator("(", out Token? bracketStart2))
            {
                bool expectParameter = false;
                List<StatementWithValue> parameters = new();

                int endlessSafe = 0;
                Token? bracketEnd2;
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

                    endlessSafe++;
                    if (endlessSafe > 100)
                    { throw new EndlessLoopException(); }
                }

                ConstructorCall newStructStatement = new(keywordNew, instanceTypeName, parameters, new TokenPair(bracketStart2, bracketEnd2));

                statementWithValue = newStructStatement;
            }
            else
            {
                statementWithValue = new NewInstance(keywordNew, instanceTypeName);
            }
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
                ExpectType(AllowedType.None, out TypeInstance? typeInstance))
            {
                simpleIdentifier.AnalyzedType = TokenAnalyzedType.Keyword;
                statementWithValue = new TypeStatement(simpleIdentifier, typeInstance);
            }
            else
            {
                Identifier identifierStatement = new(simpleIdentifier);

                if (simpleIdentifier.Content == StatementKeywords.This)
                { simpleIdentifier.AnalyzedType = TokenAnalyzedType.Keyword; }

                statementWithValue = identifierStatement;
            }
        }

        if (statementWithValue == null)
        { return false; }

        while (true)
        {
            if (ExpectOperator(".", out Token? tokenDot))
            {
                if (!ExpectIdentifier(out Token? fieldName))
                { throw new SyntaxException("Expected a symbol after \".\"", tokenDot.Position.After(), File); }

                statementWithValue = new Field(statementWithValue, fieldName);

                continue;
            }

            if (ExpectIndex(statementWithValue, out IndexCall? statementIndex))
            {
                statementWithValue = statementIndex;
                continue;
            }

            if (ExpectAnyCall(statementWithValue, out AnyCall? anyCall))
            {
                statementWithValue = anyCall;
                continue;
            }

            break;
        }

        if (allowAsStatement)
        {
            if (ExpectIdentifier(StatementKeywords.As, out Token? keyword))
            {
                if (!ExpectType(AllowedType.None, out TypeInstance? type))
                { throw new SyntaxException($"Expected type after keyword \"{keyword}\"", keyword.Position.After(), File); }

                statementWithValue = new TypeCast(statementWithValue, keyword, type);
            }
        }

        return statementWithValue != null;
    }

    bool ExpectVariableAddressGetter([NotNullWhen(true)] out AddressGetter? statement)
    {
        if (!ExpectOperator("&", out Token? refToken))
        {
            statement = null;
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? prevStatement, false))
        {
            statement = null;
            return false;
        }

        statement = new AddressGetter(refToken, prevStatement);
        return true;
    }

    bool ExpectVariableAddressFinder([NotNullWhen(true)] out Pointer? statement)
    {
        if (!ExpectOperator("*", out Token? refToken))
        {
            statement = null;
            return false;
        }

        if (!ExpectOneValue(out StatementWithValue? prevStatement, false))
        {
            statement = null;
            return false;
        }

        statement = new Pointer(refToken, prevStatement);
        return true;
    }

    void SetStatementThings(Statement.Statement statement)
    {
        if (statement == null)
        {
            if (CurrentToken != null)
            { throw new SyntaxException($"Unknown statement null", CurrentToken, File); }
            else
            { throw new SyntaxException($"Unknown statement null", Position.UnknownPosition, File); }
        }

        if (statement is Literal)
        { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement, File); }

        if (statement is Identifier)
        { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement, File); }

        if (statement is NewInstance)
        { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement, File); }

        if (statement is StatementWithValue statementWithReturnValue)
        { statementWithReturnValue.SaveValue = false; }
    }

    bool ExpectBlock([NotNullWhen(true)] out Block? block)
    {
        if (!ExpectOperator("{", out Token? bracketStart))
        {
            block = null;
            return false;
        }

        List<Statement.Statement> statements = new();

        int endlessSafe = 0;
        Token? bracketEnd;
        while (!ExpectOperator("}", out bracketEnd))
        {
            if (!ExpectStatement(out Statement.Statement? statement))
            {
                SkipCrapTokens();
                if (CurrentToken is null)
                { throw new SyntaxException($"Expected a statement, got nothing", bracketStart.Position.After(), File); }
                else
                { throw new SyntaxException($"Expected a statement, got a token \"{CurrentToken}\"", CurrentToken, File); }
            }

            statements.Add(statement);

            endlessSafe++;
            if (endlessSafe > 500) throw new EndlessLoopException();
        }

        block = new Block(statements, new TokenPair(bracketStart, bracketEnd));

        if (ExpectOperator(";", out Token? semicolon))
        { block.Semicolon = semicolon; }

        return true;
    }

    bool ExpectVariableDeclaration([NotNullWhen(true)] out VariableDeclaration? variableDeclaration)
    {
        variableDeclaration = null;
        int startTokenIndex = CurrentTokenIndex;

        List<Token> modifiers = new();
        while (ExpectIdentifier(out Token? modifier, VariableDefinitionModifiers))
        { modifiers.Add(modifier); }

        if (!ExpectType(AllowedType.Implicit | AllowedType.FunctionPointer | AllowedType.StackArrayWithLength, out TypeInstance? possibleType))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (!ExpectIdentifier(out Token? possibleVariableName))
        { CurrentTokenIndex = startTokenIndex; return false; }

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

        variableDeclaration = new VariableDeclaration(modifiers.ToArray(), possibleType, possibleVariableName, initialValue);
        return true;
    }

    bool ExpectForStatement([NotNullWhen(true)] out ForLoop? forLoop)
    {
        if (!ExpectIdentifier(StatementKeywords.For, out Token? keyword))
        { forLoop = null; return false; }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        if (!ExpectOperator("(", out Token? bracketStart))
        { throw new SyntaxException($"Expected \"(\" after \"{keyword}\" keyword", keyword.Position.After(), File); }

        if (!ExpectVariableDeclaration(out VariableDeclaration? variableDeclaration))
        { throw new SyntaxException("Expected variable declaration", bracketStart.Position.After(), File); }

        if (!ExpectOperator(";", out Token? semicolon1))
        { throw new SyntaxException($"Expected \";\" after for-loop variable declaration", variableDeclaration.Position.After(), File); }
        variableDeclaration.Semicolon = semicolon1;

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

        forLoop = new ForLoop(keyword, variableDeclaration, condition, anyAssignment, block);
        return true;
    }

    bool ExpectWhileStatement([NotNullWhen(true)] out WhileLoop? whileLoop)
    {
        if (!ExpectIdentifier(StatementKeywords.While, out Token? keyword))
        { whileLoop = null; return false; }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        if (!ExpectOperator("(", out Token? bracketStart))
        { throw new SyntaxException($"Expected \"(\" after \"{keyword}\" keyword", keyword.Position.After(), File); }

        if (!ExpectExpression(out StatementWithValue? condition))
        { throw new SyntaxException($"Expected condition after \"{bracketStart}\"", bracketStart.Position.After(), File); }

        if (!ExpectOperator(")", out Token? bracketEnd))
        { throw new SyntaxException($"Expected \")\" after while-loop condition", condition.Position.After(), File); }

        if (!ExpectBlock(out Block? block))
        { throw new SyntaxException("Expected block", bracketEnd.Position.After(), File); }

        whileLoop = new WhileLoop(keyword, condition, block);
        return true;
    }

    bool ExpectIfStatement([NotNullWhen(true)] out IfContainer? ifContainer)
    {
        ifContainer = null;

        if (!ExpectIfSegmentStatement(StatementKeywords.If, BaseBranch.IfPart.If, true, out BaseBranch? ifStatement)) return false;

        List<BaseBranch> branches = new()
        { ifStatement };

        int endlessSafe = 0;
        while (true)
        {
            if (!ExpectIfSegmentStatement(StatementKeywords.ElseIf, BaseBranch.IfPart.ElseIf, true, out BaseBranch? elseifStatement)) break;
            branches.Add(elseifStatement);

            endlessSafe++;
            if (endlessSafe > 100)
            { throw new EndlessLoopException(); }
        }

        if (ExpectIfSegmentStatement(StatementKeywords.Else, BaseBranch.IfPart.Else, false, out BaseBranch? elseStatement))
        {
            branches.Add(elseStatement);
        }

        ifContainer = new IfContainer(branches);
        return true;
    }

    bool ExpectIfSegmentStatement(string keywordName, BaseBranch.IfPart ifSegmentType, bool needParameters, [NotNullWhen(true)] out BaseBranch? baseBranch)
    {
        if (!ExpectIdentifier(keywordName, out Token? keyword))
        { baseBranch = null; return false; }

        keyword.AnalyzedType = TokenAnalyzedType.Statement;

        StatementWithValue? condition = null;

        Statement.Statement? block;

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

        baseBranch = ifSegmentType switch
        {
            BaseBranch.IfPart.If => new IfBranch(keyword, condition ?? throw new InternalException(), block),
            BaseBranch.IfPart.ElseIf => new ElseIfBranch(keyword, condition ?? throw new InternalException(), block),
            BaseBranch.IfPart.Else => new ElseBranch(keyword, block),
            _ => throw new UnreachableException(),
        };
        return true;
    }

    bool ExpectStatement([NotNullWhen(true)] out Statement.Statement? statement)
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
            { Errors.Add(new Error($"Please put a \";\" here (after {statement.GetType().Name})", statement.Position.After(), File)); }
        }
        else
        { ExpectOperator(";", out semicolon); }

        statement.Semicolon = semicolon;

        return true;
    }

    bool ExpectStatementUnchecked([NotNullWhen(true)] out Statement.Statement? statement)
    {
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

        if (ExpectKeywordCall(StatementKeywords.Throw, 1, out KeywordCall? keywordCallThrow))
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

    bool ExpectExpression([NotNullWhen(true)] out StatementWithValue? result)
    {
        result = null;

        if (ExpectOperator(UnaryPrefixOperators, out Token? unaryPrefixOperator))
        {
            if (!ExpectOneValue(out StatementWithValue? statement))
            { throw new SyntaxException($"Expected value after operator \"{unaryPrefixOperator}\" (not \"{CurrentToken}\")", unaryPrefixOperator.Position.After(), File); }

            result = new UnaryOperatorCall(unaryPrefixOperator, statement);
            return true;
        }

        if (!ExpectModifiedOrOneValue(out StatementWithValue? leftStatement, GeneralStatementModifiers)) return false;

        while (true)
        {
            if (!ExpectOperator(BinaryOperators, out Token? binaryOperator)) break;

            if (!ExpectModifiedOrOneValue(out StatementWithValue? rightStatement, GeneralStatementModifiers))
            { throw new SyntaxException($"Expected value after operator \"{binaryOperator}\" (not \"{CurrentToken}\")", binaryOperator.Position.After(), File); }

            int rightSidePrecedence = OperatorPrecedence(binaryOperator.Content);

            BinaryOperatorCall? rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
            if (rightmostStatement != null)
            {
                BinaryOperatorCall operatorCall = new(binaryOperator, rightmostStatement.Right, rightStatement);
                rightmostStatement.Right = operatorCall;
            }
            else
            {
                BinaryOperatorCall operatorCall = new(binaryOperator, leftStatement, rightStatement);
                leftStatement = operatorCall;
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

        assignment = new Assignment(@operator, leftStatement, valueToAssign);
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

        compoundAssignment = new CompoundAssignment(@operator, leftStatement, valueToAssign);
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

        if (ExpectOperator("++", out Token? t0))
        {
            shortOperatorCall = new ShortOperatorCall(t0, leftStatement);
            return true;
        }

        if (ExpectOperator("--", out Token? t1))
        {
            shortOperatorCall = new ShortOperatorCall(t1, leftStatement);
            return true;
        }

        CurrentTokenIndex = parseStart;
        shortOperatorCall = null;
        return false;
    }

    bool ExpectModifiedOrOneValue([NotNullWhen(true)] out StatementWithValue? oneValue, params string[] validModifiers)
    {
        if (!ExpectIdentifier(out Token? modifier, validModifiers))
        {
            return ExpectOneValue(out oneValue);
        }

        modifier.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectOneValue(out StatementWithValue? value))
        { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After(), File); }

        oneValue = new ModifiedStatement(modifier, value);
        return true;
    }

    bool ExpectModifiedValue([NotNullWhen(true)] out ModifiedStatement? modifiedStatement, params string[] validModifiers)
    {
        if (!ExpectIdentifier(out Token? modifier, validModifiers))
        {
            modifiedStatement = null;
            return false;
        }

        modifier.AnalyzedType = TokenAnalyzedType.Keyword;

        if (!ExpectOneValue(out StatementWithValue? value))
        { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After(), File); }

        modifiedStatement = new ModifiedStatement(modifier, value);
        return true;
    }

    static BinaryOperatorCall? FindRightmostStatement(Statement.Statement? statement, int rightSidePrecedence)
    {
        if (statement is not BinaryOperatorCall leftSide) return null;
        if (OperatorPrecedence(leftSide.Operator.Content) >= rightSidePrecedence) return null;
        if (leftSide.SurroundingBracelet.HasValue) return null;

        BinaryOperatorCall? right = FindRightmostStatement(leftSide.Right, rightSidePrecedence);

        if (right == null) return leftSide;
        return right;
    }

    static int OperatorPrecedence(string @operator)
    {
        if (LanguageOperators.Precedencies.TryGetValue(@operator, out int precedence))
        { return precedence; }
        throw new InternalException($"Precedence for operator \"{@operator}\" not found");
    }

    bool ExpectAnyCall(StatementWithValue prevStatement, [NotNullWhen(true)] out AnyCall? anyCall)
    {
        anyCall = null;
        int startTokenIndex = CurrentTokenIndex;

        if (!ExpectOperator("(", out Token? bracketStart))
        { CurrentTokenIndex = startTokenIndex; return false; }

        bool expectParameter = false;
        List<StatementWithValue> parameters = new();

        int endlessSafe = 0;
        Token? bracketEnd;
        while (!ExpectOperator(")", out bracketEnd) || expectParameter)
        {
            StatementWithValue? parameter;

            if (ExpectModifiedValue(out ModifiedStatement? modifiedStatement, PassedParameterModifiers))
            {
                parameter = modifiedStatement;
            }
            else if (ExpectExpression(out StatementWithValue? simpleParameter))
            {
                parameter = simpleParameter;
            }
            else
            { throw new SyntaxException("Expected expression as a parameter", CurrentToken?.Position ?? PreviousToken?.Position.After(), File); }

            parameters.Add(parameter);

            if (ExpectOperator(")", out bracketEnd))
            { break; }

            if (!ExpectOperator(","))
            { throw new SyntaxException("Expected \",\" to separate parameters", parameter.Position.After(), File); }
            else
            { expectParameter = true; }

            endlessSafe++;
            if (endlessSafe > 100)
            { throw new EndlessLoopException(); }
        }

        anyCall = new AnyCall(prevStatement, parameters, new TokenPair(bracketStart, bracketEnd));
        return true;
    }

    bool ExpectKeywordCall(string name, int parameterCount, [NotNullWhen(true)] out KeywordCall? keywordCall)
        => ExpectKeywordCall(name, parameterCount, parameterCount, out keywordCall);
    bool ExpectKeywordCall(string name, int minParameterCount, int maxParameterCount, [NotNullWhen(true)] out KeywordCall? keywordCall)
    {
        keywordCall = null;
        int startTokenIndex = CurrentTokenIndex;

        if (!ExpectIdentifier(out Token? possibleFunctionName))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (possibleFunctionName.Content != name)
        { CurrentTokenIndex = startTokenIndex; return false; }

        possibleFunctionName.AnalyzedType = TokenAnalyzedType.Statement;

        List<StatementWithValue> parameters = new();

        int endlessSafe = 16;
        while (true)
        {
            if (endlessSafe-- < 0) throw new EndlessLoopException();

            if (!ExpectExpression(out StatementWithValue? parameter)) break;

            parameters.Add(parameter);
        }

        keywordCall = new(possibleFunctionName, parameters);

        if (keywordCall.Parameters.Length < minParameterCount)
        { Errors.Add(new Error($"This keyword-call (\"{possibleFunctionName}\") requires minimum {minParameterCount} parameters but you passed {parameters.Count}", keywordCall, File)); }

        if (keywordCall.Parameters.Length > maxParameterCount)
        { Errors.Add(new Error($"This keyword-call (\"{possibleFunctionName}\") requires maximum {maxParameterCount} parameters but you passed {parameters.Count}", keywordCall, File)); }

        return true;
    }

    #endregion

    bool ExpectAttribute([NotNullWhen(true)] out AttributeUsage? attribute)
    {
        int parseStart = CurrentTokenIndex;
        attribute = null;

        if (!ExpectOperator("[", out Token? bracketStart))
        { CurrentTokenIndex = parseStart; return false; }

        if (!ExpectIdentifier(out Token? attributeT))
        { CurrentTokenIndex = parseStart; return false; }

        attributeT.AnalyzedType = TokenAnalyzedType.Attribute;

        List<Literal> parameters = new();
        if (ExpectOperator("(", out Token? bracketParametersStart))
        {
            int endlessSafe = 50;
            while (!ExpectOperator(")"))
            {
                ExpectLiteral(out Literal? param);
                if (param == null)
                { throw new SyntaxException("Expected parameter", bracketParametersStart, File); }
                ExpectOperator(",");

                parameters.Add(param);

                endlessSafe--;
                if (endlessSafe <= 0)
                { throw new EndlessLoopException(); }
            }
        }

        if (!ExpectOperator("]"))
        { throw new SyntaxException("Unbalanced ]", bracketStart, File); }

        attribute = new AttributeUsage(attributeT, parameters);
        return true;
    }
    AttributeUsage[] ExpectAttributes()
    {
        List<AttributeUsage> attributes = new();
        while (ExpectAttribute(out AttributeUsage? attr))
        {
            bool alreadyHave = false;
            foreach (AttributeUsage attribute in attributes)
            {
                if (attribute.Identifier == attr.Identifier)
                {
                    alreadyHave = true;
                    break;
                }
            }
            if (!alreadyHave)
            {
                attributes.Add(attr);
            }
            else
            { Errors.Add(new Error($"Attribute \"{attr}\" already defined", attr.Identifier, File)); }
        }
        return attributes.ToArray();
    }

    bool ExpectField([NotNullWhen(true)] out FieldDefinition? field)
    {
        field = null;

        int startTokenIndex = CurrentTokenIndex;

        if (ExpectIdentifier(ProtectionKeywords.Private, out Token? protectionToken))
        { }

        if (!ExpectType(AllowedType.Implicit | AllowedType.StackArrayWithLength, out TypeInstance? possibleType))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (!ExpectIdentifier(out Token? possibleVariableName))
        { CurrentTokenIndex = startTokenIndex; return false; }

        if (ExpectOperator("("))
        { CurrentTokenIndex = startTokenIndex; return false; }

        possibleVariableName.AnalyzedType = TokenAnalyzedType.None;

        field = new(possibleVariableName, possibleType, protectionToken);

        return true;
    }

    #region Basic parsing

    Token[] ParseParameterModifiers(int parameterIndex)
    {
        List<Token> modifiers = new();
        int endlessSafe = 16;
        while (true)
        {
            if (ExpectIdentifier(out Token? modifier, ParameterDefinitionModifiers))
            {
                modifier.AnalyzedType = TokenAnalyzedType.Keyword;
                modifiers.Add(modifier);

                if (modifier.Equals(ModifierKeywords.This) && parameterIndex != 0)
                {
                    Errors.Add(new Error($"Modifier \"{ModifierKeywords.This}\" only valid on the first parameter", modifier, File));
                }
            }
            else
            { break; }

            if (endlessSafe-- <= 0)
            { throw new EndlessLoopException(); }
        }
        return modifiers.ToArray();
    }

    Token[] ParseModifiers()
    {
        List<Token> modifiers = new();
        int endlessSafe = 16;
        while (true)
        {
            if (ExpectIdentifier(out Token? modifier, Modifiers))
            {
                modifier.AnalyzedType = TokenAnalyzedType.Keyword;
                modifiers.Add(modifier);
            }
            else
            { break; }

            if (endlessSafe-- <= 0)
            { throw new EndlessLoopException(); }
        }
        return modifiers.ToArray();
    }

    void CheckModifiers(IEnumerable<Token> modifiers, params string[] validModifiers)
    {
        foreach (Token modifier in modifiers)
        {
            if (!validModifiers.Contains(modifier.Content))
            { Errors.Add(new Error($"Modifier \"{modifier}\" not valid in the current context", modifier, File)); }
        }
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
    bool ExpectIdentifier([NotNullWhen(true)] out Token? result, params string[] names)
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
    bool ExpectOperator(string[] name, [NotNullWhen(true)] out Token? result)
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
        None = 0b_0000_0000,
        Implicit = 0b_0000_0001,
        ExplicitAny = 0b_0000_0010,
        FunctionPointer = 0b_0000_0100,
        StackArrayWithLength = 0b_0000_1000,
        StackArrayWithoutLength = 0b_0001_0000,
    }

    static readonly string[] TheseCharactersIndicateThatTheIdentifierWillBeFollowedByAComplexType = new string[] { "<", "(", "[" };

    bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type)
    {
        if (ExpectType(flags, out type, out Error? error))
        { return true; }
        if (error is not null)
        { Errors.Add(error.Break()); }
        return false;
    }

    bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type, [MaybeNullWhen(true)] out Error? error)
    {
        type = default;
        error = null;

        if (!ExpectIdentifier(out Token? possibleType)) return false;

        if (possibleType.Equals(StatementKeywords.Return))
        { return false; }

        type = new TypeInstanceSimple(possibleType);

        if (possibleType.Content.Equals("any"))
        {
            possibleType.AnalyzedType = TokenAnalyzedType.Keyword;

            if ((flags & AllowedType.ExplicitAny) == 0)
            {
                error = new Error($"Type \"{possibleType.Content}\" is not valid in the current context", possibleType, File, false);
                return false;
            }

            if (ExpectOperator(TheseCharactersIndicateThatTheIdentifierWillBeFollowedByAComplexType, out Token? illegalT))
            { Errors.Add(new Error($"This is not allowed", illegalT, File)); }

            if (ExpectOperator("*", out Token? pointerOperator))
            { type = new TypeInstancePointer(type, pointerOperator); }

            return true;
        }

        if (possibleType.Content.Equals(StatementKeywords.Var))
        {
            possibleType.AnalyzedType = TokenAnalyzedType.Keyword;

            if ((flags & AllowedType.Implicit) == 0)
            {
                error = new Error($"Implicit type not allowed in the current context", possibleType, File, false);
                return false;
            }

            if (ExpectOperator(TheseCharactersIndicateThatTheIdentifierWillBeFollowedByAComplexType, out Token? illegalT))
            { Errors.Add(new Error($"This is not allowed", illegalT, File)); }

            return true;
        }

        if (TypeKeywords.List.Contains(possibleType.Content))
        { possibleType.AnalyzedType = TokenAnalyzedType.Keyword; }

        int afterIdentifier = CurrentTokenIndex;
        bool withGenerics = false;

        while (true)
        {
            if (ExpectOperator("*", out Token? pointerOperator))
            {
                type = new TypeInstancePointer(type, pointerOperator);
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
                        goto End;
                    }

                    genericTypes.Add(typeParameter);

                    if (ExpectOperator(">"))
                    { break; }

                    if (ExpectOperator(">>", out Token? doubleEnd))
                    {
                        (Token? newA, Token? newB) = doubleEnd.Slice(1);
                        if (newA == null || newB == null)
                        { throw new InternalException($"I failed at token splitting :("); }
                        CurrentTokenIndex--;
                        Tokens[CurrentTokenIndex] = newB;
                        break;
                    }

                    if (ExpectOperator(","))
                    { continue; }
                }

                type = new TypeInstanceSimple(possibleType, genericTypes);
                withGenerics = true;
            }
            else if (!withGenerics && ExpectOperator("("))
            {
                if (!flags.HasFlag(AllowedType.FunctionPointer))
                {
                    CurrentTokenIndex = afterIdentifier;
                    goto End;
                }

                List<TypeInstance> parameterTypes = new();
                while (!ExpectOperator(")"))
                {
                    if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? subtype))
                    {
                        CurrentTokenIndex = afterIdentifier;
                        goto End;
                    }

                    parameterTypes.Add(subtype);

                    if (ExpectOperator(")"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }

                type = new TypeInstanceFunction(type, parameterTypes);
            }
            else if (ExpectOperator("[", out Token? bracketStart1))
            {
                if (!flags.HasFlag(AllowedType.StackArrayWithLength) &&
                    !flags.HasFlag(AllowedType.StackArrayWithoutLength))
                { return false; }

                ExpectOneValue(out StatementWithValue? sizeValue);

                if (sizeValue == null && !flags.HasFlag(AllowedType.StackArrayWithoutLength))
                { Errors.Add(new Error($"Expected value as array size", bracketStart1, File)); }

                if (sizeValue != null && !flags.HasFlag(AllowedType.StackArrayWithLength))
                { Errors.Add(new Error($"Expected value as array size", bracketStart1, File)); }

                if (!ExpectOperator("]"))
                { return false; }

                type = new TypeInstanceStackArray(type, sizeValue);
            }
            else
            { break; }
        }

        End:
        return true;
    }

    static bool NeedSemicolon(Statement.Statement statement) => statement switch
    {
        ForLoop => false,
        WhileLoop => false,
        Block => false,
        IfContainer => false,
        BaseBranch => false,
        _ => true
    };

    #endregion
}
