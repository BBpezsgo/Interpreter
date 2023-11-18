using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LanguageCore.Parser
{
    using Statement;
    using Tokenizing;

    public class Parser
    {
        int CurrentTokenIndex;
        readonly Token[] Tokens;

        Token? CurrentToken => (CurrentTokenIndex >= 0 && CurrentTokenIndex < Tokens.Length) ? Tokens[CurrentTokenIndex] : null;

        static readonly string[] Modifiers = new string[]
        {
            "export",
            "adaptive",
        };

        static readonly string[] GeneralStatementModifiers = new string[]
        {
            "temp",
        };

        static readonly string[] VariableModifiers = new string[]
        {
            "const",
            "temp",
        };

        static readonly string[] ParameterModifiers = new string[]
        {
            "this",
            "ref",
            "const",
            "temp",
        };

        static readonly string[] PassedParameterModifiers = new string[]
        {
            "ref",
            "const",
            "temp",
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

        static readonly string[] UnaryPostfixOperators = Array.Empty<string>();

        // === Result ===
        readonly List<Error> Errors = new();
        readonly List<FunctionDefinition> Functions = new();
        readonly List<MacroDefinition> Macros = new();
        readonly List<EnumDefinition> Enums = new();
        readonly Dictionary<string, StructDefinition> Structs = new();
        readonly Dictionary<string, ClassDefinition> Classes = new();
        readonly List<UsingDefinition> Usings = new();
        readonly List<CompileTag> Hashes = new();
        readonly List<Statement.Statement> TopLevelStatements = new();
        // === ===

        Parser(Token[] tokens)
        {
            Tokens = tokens;
        }

        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="ImpossibleException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public static ParserResult ParseFile(string filePath)
        {
            TokenizerResult tokens = FileTokenizer.Tokenize(filePath);
            return new Parser(tokens.Tokens).ParseInternal();
        }

        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="ImpossibleException"/>
        public static ParserResult Parse(Token[] tokens)
            => new Parser(tokens).ParseInternal();

        public static ParserResultHeader ParseHeader(Token[] tokens)
            => new Parser(tokens).ParseHeaderInternal();

        ParserResult ParseInternal()
        {
            CurrentTokenIndex = 0;

            ParseCodeHeader();

            int endlessSafe = 0;
            while (CurrentToken != null)
            {
                ParseCodeBlock();

                endlessSafe++;
                if (endlessSafe > 500) { throw new EndlessLoopException(); }
            }

            return new ParserResult(this.Errors, this.Functions, this.Macros, this.Structs.Values, this.Usings, this.Hashes, this.Classes.Values, this.TopLevelStatements, this.Enums, this.Tokens.ToArray());
        }

        ParserResultHeader ParseHeaderInternal()
        {
            CurrentTokenIndex = 0;

            ParseCodeHeader();

            return new ParserResultHeader(this.Usings, this.Hashes);
        }

        #region Parse top level

        bool ExpectHash([NotNullWhen(true)] out CompileTag? hashStatement)
        {
            hashStatement = null;

            if (!ExpectOperator("#", out var hashT))
            { return false; }

            hashT.AnalyzedType = TokenAnalyzedType.Hash;

            if (!ExpectIdentifier(out var hashName))
            { throw new SyntaxException($"There should be an identifier (after \"#\"), but you wrote {CurrentToken?.TokenType.ToString().ToLower()} \"{CurrentToken?.Content}\"", hashT); }

            hashName.AnalyzedType = TokenAnalyzedType.Hash;

            List<Literal> parameters = new();
            int endlessSafe = 50;
            Token? semicolon;
            while (!ExpectOperator(";", out semicolon))
            {
                if (!ExpectLiteral(out var parameter))
                { throw new SyntaxException($"There should be a literal or \";\", but you wrote {CurrentToken?.TokenType.ToString().ToLower()} \"{CurrentToken?.Content}\"", CurrentToken); }

                parameter.ValueToken.AnalyzedType = TokenAnalyzedType.HashParameter;
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
            if (!ExpectIdentifier("using", out var keyword))
            { return false; }

            if (CurrentToken == null) throw new SyntaxException($"Expected url after keyword \"using\"", keyword.Position.After());

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
                    throw new SyntaxException("Expected library name after \"using\"", keyword);
                }
                else
                {
                    Errors.Add(new Error("Expected library name after \"using\"", keyword));
                }
                return false;
            }

            if (!ExpectOperator(";"))
            { throw new SyntaxException("Please put a \";\" here (after \"using\")", keyword.Position.After()); }

            usingDefinition = new UsingDefinition(keyword, tokens.ToArray());

            return true;
        }

        void ParseCodeHeader()
        {
            while (true)
            {
                if (ExpectHash(out var hashStatement))
                { Hashes.Add(hashStatement); }
                else if (ExpectUsing(out var usingDefinition))
                { Usings.Add(usingDefinition); }
                else
                { break; }
            }
        }

        void ParseCodeBlock()
        {
            if (ExpectStructDefinition()) { }
            else if (ExpectClassDefinition()) { }
            else if (ExpectMacroDefinition(out var macroDefinition))
            { Macros.Add(macroDefinition); }
            else if (ExpectFunctionDefinition(out var functionDefinition))
            { Functions.Add(functionDefinition); }
            else if (ExpectEnumDefinition(out var enumDefinition))
            { Enums.Add(enumDefinition); }
            else if (ExpectStatement(out Statement.Statement? statement))
            {
                SetStatementThings(statement);

                TopLevelStatements.Add(statement);

                Token? semicolon;
                if (NeedSemicolon(statement))
                {
                    if (!ExpectOperator(";", out semicolon))
                    { Errors.Add(new Error($"Please put a \";\" here (after {statement.GetType().Name})", statement.Position.After())); }
                }
                else
                { ExpectOperator(";", out semicolon); }

                statement.Semicolon = semicolon;
            }
            else if (ExpectBlock(out Block? block))
            {
                if (ExpectOperator(";", out Token? semicolon))
                { block.Semicolon = semicolon; }

                SetStatementThings(block);

                TopLevelStatements.Add(block);
            }
            else
            { throw new SyntaxException($"Expected something but not \"{CurrentToken}\"", CurrentToken); }
        }

        bool ExpectEnumDefinition([NotNullWhen(true)] out EnumDefinition? enumDefinition)
        {
            int parseStart = CurrentTokenIndex;
            enumDefinition = null;

            FunctionDefinition.Attribute[] attributes = ExpectAttributes();

            if (!ExpectIdentifier("enum", out Token? keyword))
            { CurrentTokenIndex = parseStart; return false; }

            keyword.AnalyzedType = TokenAnalyzedType.Keyword;

            if (!ExpectIdentifier(out Token? identifier))
            { throw new SyntaxException($"Expected an identifier (the enum's name) after keyword \"{keyword}\"", keyword.Position.After()); }

            if (!ExpectOperator("{"))
            { throw new SyntaxException($"There should be a \"{{\" (after enum identifier)", identifier.Position.After()); }

            identifier.AnalyzedType = TokenAnalyzedType.Enum;

            List<EnumMemberDefinition> members = new();

            while (!ExpectOperator("}"))
            {
                if (!ExpectIdentifier(out Token? enumMemberIdentifier))
                { throw new SyntaxException($"There should be an identifier (the enum member's name) and not \"{CurrentToken}\"", CurrentToken); }

                enumMemberIdentifier.AnalyzedType = TokenAnalyzedType.EnumMember;

                StatementWithValue? enumMemberValue = null;

                if (ExpectOperator("=", out Token? assignOperator))
                {
                    if (!ExpectOneValue(out enumMemberValue))
                    { throw new SyntaxException($"There should be a value (after enum member) and not \"{CurrentToken}\"", assignOperator.Position.After()); }
                }

                members.Add(new EnumMemberDefinition(enumMemberIdentifier, enumMemberValue));

                if (ExpectOperator("}"))
                { break; }

                if (ExpectOperator(","))
                { continue; }

                throw new SyntaxException($"Expected \",\" or \"}}\" and not \"{CurrentToken}\"", CurrentToken);
            }

            enumDefinition = new(identifier, attributes, members.ToArray());

            return true;
        }

        bool ExpectOperatorDefinition([NotNullWhen(true)] out FunctionDefinition? function)
        {
            int parseStart = CurrentTokenIndex;
            function = null;

            FunctionDefinition.Attribute[] attributes = ExpectAttributes();

            Token[] modifiers = ParseModifiers();

            if (!ExpectType(AllowedType.None, out TypeInstance? possibleType))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator(OverloadableOperators, out Token? possibleName))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleName.AnalyzedType = TokenAnalyzedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this", "temp");

                if (!ExpectType(AllowedType.None, out TypeInstance? possibleParameterType))
                { throw new SyntaxException("Expected a type (the parameter's type)", CurrentToken); }

                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected an identifier (the parameter's name)", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalyzedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException($"Expected \",\" or \")\" and not \"{CurrentToken}\"", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            function = new(modifiers, possibleType, possibleName, null)
            {
                Attributes = attributes,
                Parameters = parameters.ToArray(),
            };

            Block? block = null;

            if (!ExpectOperator(";") && !ExpectBlock(out block))
            { throw new SyntaxException($"Expected \";\" or block", CurrentToken); }

            function.Block = block;

            return true;
        }

        bool ExpectTemplateInfo([NotNullWhen(true)] out TemplateInfo? templateInfo)
        {
            if (!ExpectIdentifier("template", out Token? keyword))
            {
                templateInfo = null;
                return false;
            }

            if (!ExpectOperator("<", out Token? leftP))
            { throw new SyntaxException($"There should be an \"<\" (after \"{keyword}\" keyword) and not \"{CurrentToken}\"", keyword.Position.After()); }

            List<Token> parameters = new();

            Token? rightP;

            var expectParameter = false;
            while (!ExpectOperator(">", out rightP) || expectParameter)
            {
                if (!ExpectIdentifier(out Token? parameter))
                { throw new SyntaxException("Expected identifier or \">\"", CurrentToken); }

                parameters.Add(parameter);

                if (ExpectOperator(">", out rightP))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" or \">\"", CurrentToken); }
                else
                { expectParameter = true; }
            }

            templateInfo = new(keyword, leftP, parameters, rightP);

            return true;
        }

        bool ExpectMacroDefinition([NotNullWhen(true)] out MacroDefinition? macro)
        {
            int parseStart = CurrentTokenIndex;
            macro = null;

            _ = ExpectAttributes();

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("macro", out Token? macroKeyword))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token? possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

            List<Token> parameters = new();

            Token? bracketRight;

            var expectParameter = false;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalyzedType.VariableName;
                parameters.Add(possibleParameterNameT);

                if (ExpectOperator(")", out bracketRight))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" or \")\"", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException($"Expected block", bracketRight?.Position.After() ?? Position.UnknownPosition); }

            macro = new MacroDefinition(modifiers, macroKeyword, possibleNameT, parameters, block);

            return true;
        }

        bool ExpectFunctionDefinition([NotNullWhen(true)] out FunctionDefinition? function)
        {
            int parseStart = CurrentTokenIndex;
            function = null;

            FunctionDefinition.Attribute[] attributes = ExpectAttributes();

            ExpectTemplateInfo(out TemplateInfo? templateInfo);

            Token[] modifiers = ParseModifiers();

            if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? possibleType))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token? possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this", "ref", "temp");

                if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? possibleParameterType))
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalyzedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" or \")\"", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export", "macro", "adaptive");

            function = new(modifiers, possibleType, possibleNameT, templateInfo)
            {
                Attributes = attributes,
                Parameters = parameters.ToArray(),
            };

            Block? block = null;

            if (!ExpectOperator(";") && !ExpectBlock(out block))
            { throw new SyntaxException($"Expected \";\" or block", CurrentToken); }

            function.Block = block;

            return true;
        }

        bool ExpectGeneralFunctionDefinition([NotNullWhen(true)] out GeneralFunctionDefinition? function)
        {
            int parseStart = CurrentTokenIndex;
            function = null;

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier(out Token? possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalyzedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "temp");

                if (!ExpectType(AllowedType.None, out TypeInstance? possibleParameterType))
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalyzedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" or \")\"", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            function = new(possibleNameT, modifiers)
            {
                Parameters = parameters.ToArray(),
            };

            if (ExpectOperator(";", out Token? semicolon) || !ExpectBlock(out Block? block))
            { throw new SyntaxException($"Body is required for general function definition", semicolon ?? CurrentToken); }

            function.Block = block;

            return true;
        }

        bool ExpectClassDefinition()
        {
            int startTokenIndex = CurrentTokenIndex;

            FunctionDefinition.Attribute[] attributes = ExpectAttributes();

            ExpectTemplateInfo(out TemplateInfo? templateInfo);

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("class", out Token? keyword))
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token? possibleClassName))
            { throw new SyntaxException("Expected class identifier after keyword \"class\"", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected \"{\" after class identifier", possibleClassName); }

            possibleClassName.AnalyzedType = TokenAnalyzedType.Class;
            keyword.AnalyzedType = TokenAnalyzedType.Keyword;

            List<FieldDefinition> fields = new();
            List<FunctionDefinition> methods = new();
            List<FunctionDefinition> operators = new();
            List<GeneralFunctionDefinition> generalMethods = new();

            int endlessSafe = 0;
            Token? braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                if (ExpectOperatorDefinition(out var operatorDefinition))
                {
                    operators.Add(operatorDefinition);
                }
                else if (ExpectFunctionDefinition(out var methodDefinition))
                {
                    methods.Add(methodDefinition);
                }
                else if (ExpectGeneralFunctionDefinition(out var generalMethodDefinition))
                {
                    generalMethods.Add(generalMethodDefinition);
                }
                else if (ExpectField(out FieldDefinition? field))
                {
                    fields.Add(field);
                    if (ExpectOperator(";", out Token? semicolon))
                    { field.Semicolon = semicolon; }
                }
                else
                {
                    throw new SyntaxException($"Expected field definition", CurrentToken);
                }

                endlessSafe++;
                if (endlessSafe > 50)
                {
                    throw new EndlessLoopException();
                }
            }

            CheckModifiers(modifiers, "export");

            ClassDefinition classDefinition = new(possibleClassName, braceletStart, braceletEnd, attributes, modifiers, fields, methods, generalMethods, operators)
            {
                TemplateInfo = templateInfo,
            };

            Classes.Add(classDefinition.Name.Content, classDefinition);

            // Warnings.Add(new Warning($"Class is experimental feature!", keyword, classDefinition.FilePath));

            return true;
        }

        bool ExpectStructDefinition()
        {
            int startTokenIndex = CurrentTokenIndex;

            FunctionDefinition.Attribute[] attributes = ExpectAttributes();

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("struct", out Token? keyword))
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token? possibleStructName))
            { throw new SyntaxException("Expected struct identifier after keyword \"struct\"", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected \"{\" after struct identifier", possibleStructName); }

            keyword.AnalyzedType = TokenAnalyzedType.Keyword;

            List<FieldDefinition> fields = new();
            Dictionary<string, FunctionDefinition> methods = new();

            int endlessSafe = 0;
            Token? braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                if (!ExpectField(out FieldDefinition? field))
                { throw new SyntaxException($"Expected field definition", CurrentToken); }

                fields.Add(field);
                if (ExpectOperator(";", out Token? semicolon))
                { field.Semicolon = semicolon; }

                endlessSafe++;
                if (endlessSafe > 50)
                {
                    throw new EndlessLoopException();
                }
            }

            CheckModifiers(modifiers, "export");

            StructDefinition structDefinition = new(possibleStructName, braceletStart, braceletEnd, attributes, fields, methods, modifiers);

            Structs.Add(structDefinition.Name.Content, structDefinition);

            return true;
        }

        #endregion

        #region Parse low level

        bool ExpectListValue([NotNullWhen(true)] out LiteralList? listValue)
        {
            if (!ExpectOperator("[", out var bracketLeft))
            {
                listValue = null;
                return false;
            }

            List<StatementWithValue> values = new();

            Token? bracketRight;

            int endlessSafe = 0;
            while (true)
            {
                if (ExpectExpression(out StatementWithValue? v))
                {
                    values.Add(v);

                    if (!ExpectOperator(","))
                    {
                        if (!ExpectOperator("]", out bracketRight))
                        { throw new SyntaxException("Unbalanced \"[\"", bracketLeft); }
                        break;
                    }
                }
                else
                {
                    if (!ExpectOperator("]", out bracketRight))
                    { throw new SyntaxException("Unbalanced \"[\"", bracketLeft); }
                    break;
                }

                endlessSafe++;
                if (endlessSafe >= 50) { throw new EndlessLoopException(); }
            }

            listValue = new LiteralList(bracketLeft, values.ToArray(), bracketRight);
            return true;
        }

        bool ExpectLiteral([NotNullWhen(true)] out Literal? statement)
        {
            int savedToken = CurrentTokenIndex;

            string v = CurrentToken?.Content ?? string.Empty;

            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralFloat)
            {
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.Float, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralNumber)
            {
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.Integer, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralHex)
            {
                v = v[2..];
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.Integer, Convert.ToInt32(v, 16).ToString(), CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralBinary)
            {
                v = v[2..];
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.Integer, Convert.ToInt32(v, 2).ToString(), CurrentToken);

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
            else if (ExpectIdentifier("true", out var tTrue))
            {
                Literal literal = new(LiteralType.Boolean, tTrue.Content, tTrue);

                tTrue.AnalyzedType = TokenAnalyzedType.Keyword;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("false", out var tFalse))
            {
                Literal literal = new(LiteralType.Boolean, tFalse.Content, tFalse);

                tFalse.AnalyzedType = TokenAnalyzedType.Keyword;

                statement = literal;
                return true;
            }

            CurrentTokenIndex = savedToken;

            statement = null;
            return false;
        }

        bool ExpectIndex(StatementWithValue prevStatement, [NotNullWhen(true)] out IndexCall? statement)
        {
            if (!ExpectOperator("[", out Token? bracketLeft))
            {
                statement = null;
                return false;
            }

            if (!ExpectExpression(out var expression))
            {
                statement = null;
                return false;
            }

            if (!ExpectOperator("]", out Token? bracketRight))
            { throw new SyntaxException("Unbalanced [", bracketLeft); }

            statement = new IndexCall(prevStatement, bracketLeft, expression, bracketRight);
            return true;
        }

        bool ExpectOneValue([NotNullWhen(true)] out StatementWithValue? statementWithValue, bool allowAsStatement = true)
        {
            statementWithValue = null;

            {
                if (ExpectKeywordCall("clone", 1, out var keywordCallClone))
                {
                    statementWithValue = keywordCallClone;
                }
            }

            if (statementWithValue != null)
            { }
            else if (ExpectListValue(out LiteralList? listValue))
            {
                statementWithValue = listValue;
            }
            else if (ExpectLiteral(out Literal? literal))
            {
                statementWithValue = literal;
            }
            else if (ExpectOperator("(", out Token? braceletT))
            {
                if (!ExpectExpression(out StatementWithValue? expression))
                { throw new SyntaxException("Expected expression after \"(\"", braceletT.Position.After()); }

                if (expression is OperatorCall operation)
                { operation.InsideBracelet = true; }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Unbalanced \"(\"", braceletT); }

                statementWithValue = expression;
            }
            else if (ExpectIdentifier("new", out Token? keywordNew))
            {
                keywordNew.AnalyzedType = TokenAnalyzedType.Keyword;

                if (!ExpectType(AllowedType.None, out TypeInstance? instanceTypeName))
                { throw new SyntaxException("Expected instance constructor after keyword \"new\"", keywordNew); }

                if (ExpectOperator("(", out Token? bracketLeft))
                {
                    bool expectParameter = false;
                    List<StatementWithValue> parameters = new();

                    int endlessSafe = 0;
                    Token? bracketRight;
                    while (!ExpectOperator(")", out bracketRight) || expectParameter)
                    {
                        if (!ExpectExpression(out StatementWithValue? parameter))
                        { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

                        parameters.Add(parameter);

                        if (ExpectOperator(")", out bracketRight))
                        { break; }

                        if (!ExpectOperator(","))
                        { throw new SyntaxException("Expected \",\" to separate parameters", parameter); }
                        else
                        { expectParameter = true; }

                        endlessSafe++;
                        if (endlessSafe > 100)
                        { throw new EndlessLoopException(); }
                    }

                    ConstructorCall newStructStatement = new(keywordNew, instanceTypeName, bracketLeft, parameters, bracketRight);

                    statementWithValue = newStructStatement;
                }
                else
                {
                    statementWithValue = new NewInstance(keywordNew, instanceTypeName);
                }
            }
            else if (ExpectIdentifier(out Token? simpleIdentifier))
            {
                Identifier identifierStatement = new(simpleIdentifier);

                if (simpleIdentifier.Content == "this")
                { simpleIdentifier.AnalyzedType = TokenAnalyzedType.Keyword; }

                statementWithValue = identifierStatement;
            }
            else if (ExpectVariableAddressGetter(out AddressGetter? memoryAddressGetter))
            {
                statementWithValue = memoryAddressGetter;
            }
            else if (ExpectVariableAddressFinder(out Pointer? pointer))
            {
                statementWithValue = pointer;
            }

            if (statementWithValue == null)
            { return false; }

            while (true)
            {
                if (ExpectOperator(".", out var tokenDot))
                {
                    if (!ExpectIdentifier(out Token? fieldName))
                    { throw new SyntaxException("Expected a symbol after \".\"", tokenDot.Position.After()); }

                    statementWithValue = new Field(statementWithValue, fieldName);

                    continue;
                }

                if (ExpectIndex(statementWithValue, out var statementIndex))
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
                if (ExpectIdentifier("as", out Token? keyword))
                {
                    if (!ExpectType(AllowedType.None, out TypeInstance? type))
                    { throw new SyntaxException($"Expected type after \"as\" keyword", keyword.Position.After()); }

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

        void SetStatementThings([NotNull] Statement.Statement? statement)
        {
            if (statement == null)
            {
                if (CurrentToken != null)
                { throw new SyntaxException($"Unknown statement null", CurrentToken); }
                else
                { throw new SyntaxException($"Unknown statement null", Position.UnknownPosition); }
            }

            if (statement is Literal)
            { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement); }

            if (statement is Identifier)
            { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement); }

            if (statement is NewInstance)
            { throw new SyntaxException($"Unexpected kind of statement {statement.GetType().Name}", statement); }

            if (statement is StatementWithValue statementWithReturnValue)
            {
                statementWithReturnValue.SaveValue = false;
            }
        }

        bool ExpectBlock([NotNullWhen(true)] out Block? block)
        {
            if (!ExpectOperator("{", out Token? braceletStart))
            {
                block = null;
                return false;
            }

            List<Statement.Statement> statements = new();

            int endlessSafe = 0;
            Token? braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                if (!ExpectStatement(out var statement))
                { throw new SyntaxException($"Expected a statement, got a token \"{CurrentToken}\"", CurrentToken); }

                SetStatementThings(statement);

                statements.Add(statement);

                Token? semicolon;
                if (NeedSemicolon(statement))
                {
                    if (!ExpectOperator(";", out semicolon))
                    { Errors.Add(new Error($"Expected \";\" at end of statement (after {statement.GetType().Name})", statement.Position.After())); }
                }
                else
                { ExpectOperator(";", out semicolon); }

                statement.Semicolon = semicolon;

                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }

            block = new Block(braceletStart, statements, braceletEnd);
            return true;
        }

        bool ExpectVariableDeclaration([NotNullWhen(true)] out VariableDeclaration? variableDeclaration)
        {
            variableDeclaration = null;
            int startTokenIndex = CurrentTokenIndex;

            List<Token> modifiers = new();
            while (ExpectIdentifier(out Token? modifier, VariableModifiers))
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
                { throw new SyntaxException("Expected initial value after \"=\" in variable declaration", eqOperatorToken); }
            }
            else
            {
                if (possibleType == "var")
                { throw new SyntaxException("Initial value for variable declaration with implicit type is required", possibleType); }
            }

            variableDeclaration = new VariableDeclaration(modifiers.ToArray(), possibleType, possibleVariableName, initialValue);
            return true;
        }

        bool ExpectForStatement([NotNullWhen(true)] out ForLoop? forLoop)
        {
            if (!ExpectIdentifier("for", out Token? tokenFor))
            { forLoop = null; return false; }

            tokenFor.AnalyzedType = TokenAnalyzedType.Statement;

            if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
            { throw new SyntaxException("Expected \"(\" after \"for\" statement", tokenFor.Position.After()); }

            if (!ExpectVariableDeclaration(out VariableDeclaration? variableDeclaration))
            { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(";", out Token? semicolon1))
            { throw new SyntaxException("Expected \";\" after \"for\" variable declaration", variableDeclaration.Position.After()); }
            variableDeclaration.Semicolon = semicolon1;

            if (!ExpectExpression(out StatementWithValue? condition))
            { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenParenthesesOpen); }

            if (!ExpectOperator(";", out Token? semicolon2))
            { throw new SyntaxException($"Expected \";\" after \"for\" condition, but you wrote {CurrentToken}", variableDeclaration.Position.After()); }
            condition.Semicolon = semicolon2;

            if (!ExpectAnySetter(out AnyAssignment? anyAssignment))
            { throw new SyntaxException($"Expected an assignment after \"for\" condition, but you wrote {CurrentToken}", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token? tokenParenthesesClosed))
            { throw new SyntaxException($"Expected \")\" after \"for\" condition, but you wrote {CurrentToken}", condition.Position.After()); }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException($"Expected block, but you wrote {CurrentToken}", tokenParenthesesClosed.Position.After()); }

            forLoop = new ForLoop(tokenFor, variableDeclaration, condition, anyAssignment, block);
            return true;
        }

        bool ExpectWhileStatement([NotNullWhen(true)] out WhileLoop? whileLoop)
        {
            if (!ExpectIdentifier("while", out Token? tokenWhile))
            { whileLoop = null; return false; }

            tokenWhile.AnalyzedType = TokenAnalyzedType.Statement;

            if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
            { throw new SyntaxException("Expected \"(\" after \"while\" statement", tokenWhile); }

            if (!ExpectExpression(out StatementWithValue? condition))
            { throw new SyntaxException("Expected condition after \"while\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token? tokenParenthesesClose))
            { throw new SyntaxException("Expected \")\" after \"while\" condition", condition); }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException("Expected block", tokenParenthesesClose.Position.After()); }

            whileLoop = new WhileLoop(tokenWhile, condition, block);
            return true;
        }

        bool ExpectIfStatement([NotNullWhen(true)] out IfContainer? ifContainer)
        {
            ifContainer = null;

            if (!ExpectIfSegmentStatement("if", BaseBranch.IfPart.If, true, out BaseBranch? ifStatement)) return false;

            List<BaseBranch> branches = new()
            { ifStatement };

            int endlessSafe = 0;
            while (true)
            {
                if (!ExpectIfSegmentStatement("elseif", BaseBranch.IfPart.ElseIf, true, out BaseBranch? elseifStatement)) break;
                branches.Add(elseifStatement);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            if (ExpectIfSegmentStatement("else", BaseBranch.IfPart.Else, false, out BaseBranch? elseStatement))
            {
                branches.Add(elseStatement);
            }

            ifContainer = new IfContainer(branches);
            return true;
        }

        bool ExpectIfSegmentStatement(string ifSegmentName, BaseBranch.IfPart ifSegmentType, bool needParameters, [NotNullWhen(true)] out BaseBranch? baseBranch)
        {
            if (!ExpectIdentifier(ifSegmentName, out Token? tokenIf))
            { baseBranch = null; return false; }

            tokenIf.AnalyzedType = TokenAnalyzedType.Statement;

            StatementWithValue? condition = null;

            if (needParameters)
            {
                if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
                { throw new SyntaxException("Expected \"(\" after \"" + ifSegmentName + "\" statement", tokenIf); }

                if (!ExpectExpression(out condition))
                { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenParenthesesOpen); }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Expected \")\" after \"" + ifSegmentName + "\" condition", condition); }
            }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException("Expected block", tokenIf.Position.After()); }

            baseBranch = ifSegmentType switch
            {
                BaseBranch.IfPart.If => new IfBranch(tokenIf, condition!, block),
                BaseBranch.IfPart.ElseIf => new ElseIfBranch(tokenIf, condition!, block),
                BaseBranch.IfPart.Else => new ElseBranch(tokenIf, block),
                _ => throw new ImpossibleException(),
            };
            return true;
        }

        bool ExpectStatement([NotNullWhen(true)] out Statement.Statement? statement)
        {
            if (ExpectWhileStatement(out var whileLoop))
            {
                statement = whileLoop;
                return true;
            }

            if (ExpectForStatement(out var forLoop))
            {
                statement = forLoop;
                return true;
            }

            if (ExpectKeywordCall("return", 0, 1, out var keywordCallReturn))
            {
                statement = keywordCallReturn;
                return true;
            }

            if (ExpectKeywordCall("throw", 1, out var keywordCallThrow))
            {
                statement = keywordCallThrow;
                return true;
            }

            if (ExpectKeywordCall("break", 0, out var keywordCallBreak))
            {
                statement = keywordCallBreak;
                return true;
            }

            if (ExpectKeywordCall("delete", 1, out var keywordCallDelete))
            {
                statement = keywordCallDelete;
                return true;
            }

            if (ExpectKeywordCall("clone", 1, out var keywordCallClone))
            {
                statement = keywordCallClone;
                return true;
            }

            if (ExpectIfStatement(out var ifContainer))
            {
                statement = ifContainer;
                return true;
            }

            if (ExpectVariableDeclaration(out var variableDeclaration))
            {
                statement = variableDeclaration;
                return true;
            }

            if (ExpectAnySetter(out var assignment))
            {
                statement = assignment;
                return true;
            }

            if (ExpectExpression(out var expression))
            {
                statement = expression;
                return true;
            }

            statement = null;
            return false;
        }

        /*
        bool ExpectMethodCall(bool expectDot, StatementWithValue prevStatement, [NotNullWhen(true)] out FunctionCall? methodCall)
        {
            int startTokenIndex = CurrentTokenIndex;

            if (expectDot && !ExpectOperator("."))
            {
                CurrentTokenIndex = startTokenIndex;
                methodCall = null;
                return false;
            }

            if (!ExpectIdentifier(out var possibleFunctionName))
            {
                CurrentTokenIndex = startTokenIndex;
                methodCall = null;
                return false;
            }

            if (!ExpectOperator("(", out Token? bracketLeft))
            {
                CurrentTokenIndex = startTokenIndex;
                methodCall = null;
                return false;
            }

            possibleFunctionName.AnalyzedType = TokenAnalyzedType.FunctionName;

            bool expectParameter = false;

            List<StatementWithValue> parameters = new();
            int endlessSafe = 0;
            Token? bracketRight;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                if (!ExpectExpression(out StatementWithValue? parameter))
                { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

                parameters.Add(parameter);

                if (ExpectOperator(")", out bracketRight))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException($"Expected \",\" to separate parameters, got {CurrentToken}", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            methodCall = new(prevStatement, possibleFunctionName, bracketLeft, parameters, bracketRight);

            return true;
        }
        */

        bool ExpectExpression([NotNullWhen(true)] out StatementWithValue? result)
        {
            result = null;

            if (ExpectOperator(UnaryPrefixOperators, out Token? unaryPrefixOperator))
            {
                if (!ExpectOneValue(out StatementWithValue? statement))
                { throw new SyntaxException($"Expected value after operator \"{unaryPrefixOperator}\", got \"{CurrentToken}\"", CurrentToken); }

                result = new OperatorCall(unaryPrefixOperator, statement);
                return true;
            }

            if (!ExpectModifiedOrOneValue(out var leftStatement, GeneralStatementModifiers)) return false;

            while (true)
            {
                if (!ExpectOperator(BinaryOperators, out Token? binaryOperator)) break;

                if (!ExpectModifiedOrOneValue(out StatementWithValue? rightStatement, GeneralStatementModifiers))
                { throw new SyntaxException($"Expected value after operator \"{binaryOperator}\", got \"{CurrentToken}\"", CurrentToken); }

                int rightSidePrecedence = OperatorPrecedence(binaryOperator.Content);

                OperatorCall? rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
                if (rightmostStatement != null)
                {
                    OperatorCall operatorCall = new(binaryOperator, rightmostStatement.Right!, rightStatement);
                    rightmostStatement.Right = operatorCall;
                }
                else
                {
                    OperatorCall operatorCall = new(binaryOperator, leftStatement, rightStatement);
                    leftStatement = operatorCall;
                }
            }

            result = leftStatement;
            return true;
        }

        bool ExpectAnySetter([NotNullWhen(true)] out AnyAssignment? assignment)
        {
            if (ExpectShortOperator(out var shortOperatorCall))
            {
                assignment = shortOperatorCall;
                return true;
            }
            if (ExpectCompoundSetter(out var compoundAssignment))
            {
                assignment = compoundAssignment;
                return true;
            }
            if (ExpectSetter(out var simpleSetter))
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
            { throw new SyntaxException("Expected expression after assignment operator", @operator); }

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

            if (!ExpectOperator(CompoundAssignmentOperators, out var @operator))
            {
                CurrentTokenIndex = parseStart;
                return false;
            }

            if (!ExpectExpression(out StatementWithValue? valueToAssign))
            { throw new SyntaxException("Expected expression after compound assignment operator", @operator); }

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

            if (ExpectOperator("++", out var t0))
            {
                shortOperatorCall = new ShortOperatorCall(t0, leftStatement);
                return true;
            }

            if (ExpectOperator("--", out var t1))
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
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After()); }

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
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.Position.After()); }

            modifiedStatement = new ModifiedStatement(modifier, value);
            return true;
        }

        /// <returns>
        /// <see langword="null"/> or <see cref="OperatorCall"/>
        /// </returns>
        OperatorCall? FindRightmostStatement(Statement.Statement? statement, int rightSidePrecedence)
        {
            if (statement is not OperatorCall leftSide) return null;
            if (OperatorPrecedence(leftSide.Operator.Content) >= rightSidePrecedence) return null;
            if (leftSide.InsideBracelet) return null;

            OperatorCall? right = FindRightmostStatement(leftSide.Right, rightSidePrecedence);

            if (right == null) return leftSide;
            return right;
        }

        static int OperatorPrecedence(string @operator)
        {
            if (LanguageConstants.Operators.Precedencies.TryGetValue(@operator, out int precedence))
            { return precedence; }
            throw new InternalException($"Precedence for operator \"{@operator}\" not found");
        }

        /*
        bool ExpectFunctionCall([NotNullWhen(true)] out FunctionCall? functionCall)
        {
            functionCall = null;
            int startTokenIndex = CurrentTokenIndex;

            if (!ExpectIdentifier(out Token? possibleFunctionName))
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (possibleFunctionName == null)
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectOperator("(", out Token? bracketLeft))
            { CurrentTokenIndex = startTokenIndex; return false; }

            possibleFunctionName.AnalyzedType = TokenAnalyzedType.BuiltinType;

            bool expectParameter = false;
            List<StatementWithValue> parameters = new();

            int endlessSafe = 0;
            Token? bracketRight;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
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
                { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

                parameters.Add(parameter);

                if (ExpectOperator(")", out bracketRight))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" to separate parameters", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            };

            functionCall = new FunctionCall(null, possibleFunctionName, bracketLeft, parameters, bracketRight);
            return true;
        }
        */

        bool ExpectAnyCall(StatementWithValue prevStatement, [NotNullWhen(true)] out AnyCall? anyCall)
        {
            anyCall = null;
            int startTokenIndex = CurrentTokenIndex;

            if (!ExpectOperator("(", out Token? bracketLeft))
            { CurrentTokenIndex = startTokenIndex; return false; }

            bool expectParameter = false;
            List<StatementWithValue> parameters = new();

            int endlessSafe = 0;
            Token? bracketRight;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
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
                { throw new SyntaxException("Expected expression as a parameter", CurrentToken); }

                parameters.Add(parameter);

                if (ExpectOperator(")", out bracketRight))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected \",\" to separate parameters", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            };

            anyCall = new AnyCall(prevStatement, bracketLeft, parameters, bracketRight);
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
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requires minimum {minParameterCount} parameters but you passed {parameters.Count}", keywordCall); }

            if (keywordCall.Parameters.Length > maxParameterCount)
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requires maximum {maxParameterCount} parameters but you passed {parameters.Count}", keywordCall); }

            return true;
        }

        #endregion

        bool ExpectAttribute([NotNullWhen(true)] out FunctionDefinition.Attribute? attribute)
        {
            int parseStart = CurrentTokenIndex;
            attribute = null;

            if (!ExpectOperator("[", out var t0))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token? attributeT))
            { CurrentTokenIndex = parseStart; return false; }

            attributeT.AnalyzedType = TokenAnalyzedType.Attribute;

            List<object> parameters = new();
            if (ExpectOperator("(", out var t3))
            {
                int endlessSafe = 50;
                while (!ExpectOperator(")"))
                {
                    ExpectOneLiteral(out object? param);
                    if (param == null)
                    { throw new SyntaxException("Expected parameter", t3); }
                    ExpectOperator(",");

                    parameters.Add(param);

                    endlessSafe--;
                    if (endlessSafe <= 0)
                    { throw new EndlessLoopException(); }
                }
            }

            if (!ExpectOperator("]"))
            { throw new SyntaxException("Unbalanced ]", t0); }

            attribute = new FunctionDefinition.Attribute(attributeT, parameters.ToArray());
            return true;
        }
        FunctionDefinition.Attribute[] ExpectAttributes()
        {
            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out FunctionDefinition.Attribute? attr))
            {
                bool alreadyHave = false;
                foreach (FunctionDefinition.Attribute attribute in attributes)
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
                { Errors.Add(new Error($"Attribute \"{attr}\" already defined", attr.Identifier)); }
            }
            return attributes.ToArray();
        }

        bool ExpectField([NotNullWhen(true)] out FieldDefinition? field)
        {
            field = null;

            int startTokenIndex = CurrentTokenIndex;

            if (ExpectIdentifier("private", out Token? protectionToken))
            {

            }

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

        void ExpectOneLiteral(out object? value)
        {
            value = null;

            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralFloat)
            {
                value = float.Parse(CurrentToken.Content.Replace("_", ""));

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralNumber)
            {
                value = int.Parse(CurrentToken.Content.Replace("_", ""));

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralHex)
            {
                value = Convert.ToInt32(CurrentToken.Content, 16);

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralBinary)
            {
                value = Convert.ToInt32(CurrentToken.Content.Replace("_", ""), 2);

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LiteralString)
            {
                value = CurrentToken.Content;

                CurrentTokenIndex++;
            }
        }

        #region Basic parsing

        Token[] ParseParameterModifiers(int parameterIndex)
        {
            List<Token> modifiers = new();
            int endlessSafe = 16;
            while (true)
            {
                if (ExpectIdentifier(out Token? modifier, ParameterModifiers))
                {
                    modifier.AnalyzedType = TokenAnalyzedType.Keyword;
                    modifiers.Add(modifier);

                    if (modifier == "this" && parameterIndex != 0)
                    {
                        Errors.Add(new Error($"Modifier \"{modifier}\" only valid on the first parameter", modifier));
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

        static void CheckModifiers(IEnumerable<Token> modifiers, params string[] validModifiers)
        {
            foreach (Token modifier in modifiers)
            {
                if (!validModifiers.Contains(modifier.Content))
                { throw new SyntaxException($"Modifier \"{modifier}\" not valid in the current context", modifier); }
            }
        }

        bool ExpectIdentifier([NotNullWhen(true)] out Token? result) => ExpectIdentifier("", out result);
        bool ExpectIdentifier(string name, [NotNullWhen(true)] out Token? result)
        {
            result = null;
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
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.Operator) return false;
            if (name.Contains(CurrentToken.Content) == false) return false;

            result = CurrentToken;
            CurrentTokenIndex++;

            return true;
        }
        bool ExpectOperator(string name, [NotNullWhen(true)] out Token? result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.Operator) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            result = CurrentToken;
            CurrentTokenIndex++;

            return true;
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

        bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type)
        {
            type = default;

            if (!ExpectIdentifier(out Token? possibleType)) return false;

            if (possibleType == "macro")
            { return false; }

            possibleType.AnalyzedType = TokenAnalyzedType.Keyword;
            type = new TypeInstanceSimple(possibleType);

            if (possibleType.Content == "any")
            {
                if ((flags & AllowedType.ExplicitAny) == 0)
                {
                    Errors.Add(new Error($"Type \"{possibleType.Content}\" is not valid in the current context", possibleType));
                    return false;
                }

                if (ExpectOperator(new string[] { "<", "(", "[" }, out Token? illegalT))
                { throw new SyntaxException($"This is not allowed", illegalT); }

                return true;
            }

            if (possibleType.Content == "var")
            {
                if ((flags & AllowedType.Implicit) == 0)
                {
                    Errors.Add(new Error($"implicit type not allowed in the current context", possibleType));
                    return false;
                }

                if (ExpectOperator(new string[] { "<", "(", "[" }, out Token? illegalT))
                { throw new SyntaxException($"This is not allowed", illegalT); }

                return true;
            }

            int afterIdentifier = CurrentTokenIndex;

            while (true)
            {
                if (ExpectOperator("<"))
                {
                    if (type is not TypeInstanceSimple)
                    { throw new NotImplementedException(); }

                    List<TypeInstance> genericTypes = new();

                    while (true)
                    {
                        if (!ExpectType(AllowedType.FunctionPointer, out var typeParameter))
                        { return false; }

                        genericTypes.Add(typeParameter);

                        if (ExpectOperator(">"))
                        { break; }

                        if (ExpectOperator(">>", out Token? doubleEnd))
                        {
                            (Token? newA, Token? newB) = doubleEnd.CutInHalf();
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
                    return true;
                }
                else if (ExpectOperator("("))
                {
                    if (!flags.HasFlag(AllowedType.FunctionPointer))
                    {
                        CurrentTokenIndex = afterIdentifier;
                        return true;
                    }

                    List<TypeInstance> parameterTypes = new();
                    while (!ExpectOperator(")"))
                    {
                        if (!ExpectType(AllowedType.FunctionPointer, out var subtype))
                        {
                            CurrentTokenIndex = afterIdentifier;
                            return true;
                        }

                        parameterTypes.Add(subtype);

                        if (ExpectOperator(")"))
                        { break; }

                        if (ExpectOperator(","))
                        { continue; }
                    }

                    type = new TypeInstanceFunction(type, parameterTypes);
                }
                else if (ExpectOperator("[", out var bracket1Left))
                {
                    if (!flags.HasFlag(AllowedType.StackArrayWithLength) &&
                        !flags.HasFlag(AllowedType.StackArrayWithoutLength))
                    { return false; }

                    ExpectOneValue(out StatementWithValue? sizeValue);

                    if (sizeValue == null && !flags.HasFlag(AllowedType.StackArrayWithoutLength))
                    { throw new SyntaxException($"Expected value as array size", bracket1Left); }

                    if (sizeValue != null && !flags.HasFlag(AllowedType.StackArrayWithLength))
                    { throw new SyntaxException($"Expected value as array size", bracket1Left); }

                    if (!ExpectOperator("]"))
                    { return false; }

                    type = new TypeInstanceStackArray(type, sizeValue);
                }
                else
                { break; }
            }

            return true;
        }

        static bool NeedSemicolon(Statement.Statement? statement)
        {
            if (statement == null) return false;

            if (statement is ForLoop)
            { return false; }

            if (statement is WhileLoop)
            { return false; }

            if (statement is Block)
            { return false; }

            if (statement is IfContainer)
            { return false; }

            if (statement is BaseBranch)
            { return false; }

            return true;
        }

        #endregion
    }
}
