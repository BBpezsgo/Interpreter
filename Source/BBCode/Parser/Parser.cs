using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Parser
{
    using Core;

    using Errors;

    using Statement;

    /// <summary>
    /// The parser for the BBCode language
    /// </summary>
    public class Parser
    {
        int currentTokenIndex;
        readonly List<Token> tokens = new();
        public Token[] Tokens => tokens.ToArray();

        Token CurrentToken => (currentTokenIndex >= 0 && currentTokenIndex < tokens.Count) ? tokens[currentTokenIndex] : null;

        static readonly string[] Modifiers = new string[]
        {
                "export",
                "macro",
                "adaptive",
        };

        static readonly string[] VariableModifiers = new string[]
        {
                "const",
        };

        static readonly string[] ParameterModifiers = new string[]
        {
                "this",
                "ref",
                "const",
        };

        static readonly string[] PassedParameterModifiers = new string[]
        {
                "ref",
                "const",
        };

        static readonly string[] types = new string[]
        {
                "int",
                "void",
                "float",
                "bool",
                "byte",
                "char",
        };
        readonly Dictionary<string, int> operators = new();

        List<Warning> Warnings;
        public readonly List<Error> Errors = new();

        // === Result ===
        readonly List<FunctionDefinition> Functions = new();
        readonly List<EnumDefinition> Enums = new();
        readonly Dictionary<string, StructDefinition> Structs = new();
        readonly Dictionary<string, ClassDefinition> Classes = new();
        readonly List<UsingDefinition> Usings = new();
        readonly List<CompileTag> Hashes = new();
        readonly List<Statement.Statement> TopLevelStatements = new();
        // === ===

        public Parser()
        {
            operators.Add("|", 4);
            operators.Add("&", 4);
            operators.Add("^", 4);
            operators.Add("<<", 4);
            operators.Add(">>", 4);

            operators.Add("!=", 5);
            operators.Add(">=", 5);
            operators.Add("<=", 5);
            operators.Add("==", 5);

            operators.Add("=", 10);

            operators.Add("+=", 11);
            operators.Add("-=", 11);

            operators.Add("*=", 12);
            operators.Add("/=", 12);
            operators.Add("%=", 12);

            operators.Add("<", 20);
            operators.Add(">", 20);

            operators.Add("+", 30);
            operators.Add("-", 30);

            operators.Add("*", 31);
            operators.Add("/", 31);
            operators.Add("%", 31);

            operators.Add("++", 40);
            operators.Add("--", 40);

            operators.Add("&&", 2);
            operators.Add("||", 2);
        }

        /// <summary>
        /// Parses tokens into AST
        /// </summary>
        /// <param name="_tokens">
        /// The list of tokens<br/>
        /// This should be generated using <see cref="ProgrammingLanguage.BBCode.Tokenizer"/>
        /// </param>
        /// <param name="warnings">
        /// A list that the parser can fill with warnings
        /// </param>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="Exception"/>
        /// <returns>
        /// The generated AST
        /// </returns>
        public ParserResult Parse(Token[] _tokens, List<Warning> warnings)
        {
            Warnings = warnings;
            tokens.Clear();
            tokens.AddRange(_tokens);

            currentTokenIndex = 0;

            ParseCodeHeader();

            int endlessSafe = 0;
            while (CurrentToken != null)
            {
                ParseCodeBlock();

                endlessSafe++;
                if (endlessSafe > 500) { throw new EndlessLoopException(); }
            }

            return new ParserResult(this.Functions, this.Structs.Values, this.Usings, this.Hashes, this.Classes.Values, this.TopLevelStatements, this.Enums, this.tokens.ToArray());
        }

        public ParserResultHeader ParseCodeHeader(Token[] _tokens, List<Warning> warnings)
        {
            Warnings = warnings;
            tokens.Clear();
            tokens.AddRange(_tokens);

            currentTokenIndex = 0;

            ParseCodeHeader();

            return new ParserResultHeader(this.Usings, this.Hashes);
        }

        #region Parse top level

        bool ExpectHash(out CompileTag hashStatement)
        {
            hashStatement = null;

            if (!ExpectOperator("#", out var hashT))
            { return false; }

            hashT.AnalysedType = TokenAnalysedType.Hash;

            if (!ExpectIdentifier(out var hashName))
            { throw new SyntaxException($"Expected identifier after '#' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", hashT); }

            hashName.AnalysedType = TokenAnalysedType.Hash;

            List<Literal> parameters = new();
            int endlessSafe = 50;
            while (!ExpectOperator(";"))
            {
                if (!ExpectLiteral(out var parameter))
                { throw new SyntaxException($"Expected hash literal parameter or ';' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", CurrentToken); }

                parameter.ValueToken.AnalysedType = TokenAnalysedType.HashParameter;
                parameters.Add(parameter);

                if (ExpectOperator(";"))
                { break; }

                endlessSafe--;
                if (endlessSafe <= 0)
                { throw new EndlessLoopException(); }
            }

            hashStatement = new CompileTag
            {
                HashToken = hashT,
                HashName = hashName,
                Parameters = parameters.ToArray()
            };

            return true;
        }

        bool ExpectUsing(out UsingDefinition usingDefinition)
        {
            usingDefinition = null;
            if (!ExpectIdentifier("using", out var keyword))
            { return false; }

            keyword.AnalysedType = TokenAnalysedType.Keyword;

            List<Token> tokens = new();
            if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                tokens.Add(CurrentToken);
                currentTokenIndex++;
            }
            else
            {
                int endlessSafe = 50;
                while (ExpectIdentifier(out Token pathIdentifier))
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
                    throw new SyntaxException("Expected library name after 'using'", keyword);
                }
                else
                {
                    Errors.Add(new Error("Expected library name after 'using'", keyword));
                    return true;
                }
            }

            if (!ExpectOperator(";"))
            { throw new SyntaxException("Expected ';' at end of statement (after 'using')", keyword.After()); }

            usingDefinition = new UsingDefinition
            {
                Path = tokens.ToArray(),
                Keyword = keyword,
            };

            return true;
        }

        void ParseCodeHeader()
        {
            while (ExpectHash(out var hash))
            { Hashes.Add(hash); }
            while (ExpectUsing(out var usingNamespace))
            { Usings.Add(usingNamespace); }
        }

        void ParseCodeBlock()
        {
            if (ExpectStructDefinition()) { }
            else if (ExpectClassDefinition()) { }
            else if (ExpectFunctionDefinition(out var functionDefinition))
            { Functions.Add(functionDefinition); }
            else if (ExpectEnumDefinition(out var enumDefinition))
            { Enums.Add(enumDefinition); }
            else
            {
                Statement.Statement statement = ExpectStatement();
                if (statement == null)
                { throw new SyntaxException($"Expected top-level statement, type, macro or function definition. Got a token {CurrentToken}", CurrentToken); }

                SetStatementThings(statement);

                TopLevelStatements.Add(statement);

                if (!ExpectOperator(";"))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition().After())); }
            }
        }

        bool ExpectEnumDefinition(out EnumDefinition enumDefinition)
        {
            int parseStart = currentTokenIndex;
            enumDefinition = null;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
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
                { Errors.Add(new Error($"Attribute \"{attr}\" already applied", attr.Identifier)); }
            }

            if (!ExpectIdentifier("enum", out Token keyword))
            { currentTokenIndex = parseStart; return false; }

            keyword.AnalysedType = TokenAnalysedType.Keyword;

            if (!ExpectIdentifier(out Token identifier))
            { throw new SyntaxException($"Expected identifier token after keyword \"{keyword}\"", keyword.After()); }

            if (!ExpectOperator("{"))
            { throw new SyntaxException($"Expected '{{' after enum identifier", identifier.After()); }

            identifier.AnalysedType = TokenAnalysedType.Enum;

            List<EnumMemberDefinition> members = new();

            while (!ExpectOperator("}"))
            {
                if (!ExpectIdentifier(out Token enumMemberIdentifier))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                enumMemberIdentifier.AnalysedType = TokenAnalysedType.EnumMember;

                EnumMemberDefinition newMember = new();
                newMember.Identifier = enumMemberIdentifier;

                if (ExpectOperator("=", out Token assignOperator))
                {
                    if (!ExpectLiteral(out Literal value))
                    { throw new SyntaxException($"Expected literal after enum member assignment", assignOperator.After()); }

                    newMember.Value = value;
                }

                members.Add(newMember);

                if (ExpectOperator("}"))
                { break; }

                if (ExpectOperator(","))
                { continue; }

                throw new SyntaxException("Expected ',' or '}'", CurrentToken);
            }

            enumDefinition = new()
            {
                Identifier = identifier,
                Attributes = attributes.ToArray(),
                Members = members.ToArray(),
            };

            return true;
        }

        bool ExpectOperatorDefinition(out FunctionDefinition function)
        {
            int parseStart = currentTokenIndex;
            function = null;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
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
                { Errors.Add(new Error("Attribute '" + attr + "' already applied to the function", attr.Identifier)); }
            }

            Token[] modifiers = ParseModifiers();

            TypeInstance possibleType = ExpectType(false);
            if (possibleType == null)
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectOperator(new string[]
            {
                    "<<", ">>",
                    "+", "-", "*", "/", "%", "&", "|",
                    "<", ">", ">=", "<=", "!=", "==", "&&", "||", "^"
            }, out Token possibleName))
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { currentTokenIndex = parseStart; return false; }

            possibleName.AnalysedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this");

                TypeInstance possibleParameterType = ExpectType(false, true);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalysedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new()
                {
                    Type = possibleParameterType,
                    Identifier = possibleParameterNameT,
                    Modifiers = parameterModifiers,
                };
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            function = new(possibleName, modifiers, null)
            {
                Type = possibleType,
                Attributes = attributes.ToArray(),
                Parameters = parameters.ToArray(),
            };

            List<Statement.Statement> statements = new();

            if (!ExpectOperator(";"))
            {
                statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
                function.BracketStart = braceletStart;
                function.BracketEnd = braceletEnd;
            }

            function.Statements = statements.ToArray();

            return true;
        }

        bool ExpectTemplateInfo(out TemplateInfo templateInfo)
        {
            if (!ExpectIdentifier("template", out Token keyword))
            {
                templateInfo = null;
                return false;
            }

            if (!ExpectOperator("<", out Token leftP))
            { throw new SyntaxException($"Expected '<' after keyword \"{keyword}\"", keyword.After()); }

            List<Token> parameters = new();

            Token rightP;

            var expectParameter = false;
            while (!ExpectOperator(">", out rightP) || expectParameter)
            {
                if (!ExpectIdentifier(out Token parameter))
                { throw new SyntaxException("Expected identifier or '>'", CurrentToken); }

                parameters.Add(parameter);

                if (ExpectOperator(">", out rightP))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or '>'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            templateInfo = new(keyword, leftP, parameters, rightP);

            return true;
        }

        bool ExpectFunctionDefinition(out FunctionDefinition function)
        {
            int parseStart = currentTokenIndex;
            function = null;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
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
                { Errors.Add(new Error("Attribute '" + attr + "' already applied to the function", attr.Identifier)); }
            }

            ExpectTemplateInfo(out TemplateInfo templateInfo);

            Token[] modifiers = ParseModifiers();

            TypeInstance possibleType = ExpectType(false);
            if (possibleType == null)
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token possibleNameT))
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { currentTokenIndex = parseStart; return false; }

            possibleNameT.AnalysedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this", "ref");

                TypeInstance possibleParameterType = ExpectType(false, true);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalysedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new()
                {
                    Type = possibleParameterType,
                    Identifier = possibleParameterNameT,
                    Modifiers = parameterModifiers,
                };
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export", "macro", "adaptive");

            function = new(possibleNameT, modifiers, templateInfo)
            {
                Type = possibleType,
                Attributes = attributes.ToArray(),
                Parameters = parameters.ToArray(),
            };

            List<Statement.Statement> statements = new();

            if (!ExpectOperator(";"))
            {
                statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
                function.BracketStart = braceletStart;
                function.BracketEnd = braceletEnd;
            }

            function.Statements = statements.ToArray();

            return true;
        }

        bool ExpectGeneralFunctionDefinition(out GeneralFunctionDefinition function)
        {
            int parseStart = currentTokenIndex;
            function = null;

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier(out Token possibleNameT))
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { currentTokenIndex = parseStart; return false; }

            possibleNameT.AnalysedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers);

                TypeInstance possibleParameterType = ExpectType(false, true);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalysedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new()
                {
                    Type = possibleParameterType,
                    Identifier = possibleParameterNameT,
                    Modifiers = parameterModifiers,
                };
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            function = new(possibleNameT, modifiers)
            {
                Parameters = parameters.ToArray(),
            };

            if (ExpectOperator(";", out var tIdk))
            { throw new SyntaxException($"Body is requied for general function definition", tIdk); }

            List<Statement.Statement> statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
            function.BracketStart = braceletStart;
            function.BracketEnd = braceletEnd;

            function.Statements = statements.ToArray();

            return true;
        }

        bool ExpectClassDefinition()
        {
            int startTokenIndex = currentTokenIndex;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
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
                { Errors.Add(new Error("Attribute '" + attr + "' already applied to the class", attr.Identifier)); }
            }

            ExpectTemplateInfo(out TemplateInfo templateInfo);

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("class", out Token keyword))
            { currentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token possibleClassName))
            { throw new SyntaxException("Expected class identifier after keyword 'class'", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected '{' after class identifier", possibleClassName); }

            possibleClassName.AnalysedType = TokenAnalysedType.Class;
            keyword.AnalysedType = TokenAnalysedType.Keyword;

            List<FieldDefinition> fields = new();
            List<FunctionDefinition> methods = new();
            List<FunctionDefinition> operators = new();
            List<GeneralFunctionDefinition> generalMethods = new();

            int endlessSafe = 0;
            Token braceletEnd;
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
                else if (ExpectField(out FieldDefinition field))
                {
                    fields.Add(field);
                    if (!ExpectOperator(";"))
                    { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", field.Identifier.After())); }
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

            ClassDefinition classDefinition = new(possibleClassName, attributes, modifiers, fields, methods, generalMethods, operators)
            {
                BracketStart = braceletStart,
                BracketEnd = braceletEnd,
                TemplateInfo = templateInfo,
            };

            Classes.Add(classDefinition.Name.Content, classDefinition);

            Warnings.Add(new Warning($"Class is experimental feature!", keyword, classDefinition.FilePath));

            return true;
        }

        bool ExpectStructDefinition()
        {
            int startTokenIndex = currentTokenIndex;

            List<FunctionDefinition.Attribute> attributes = new();
            while (ExpectAttribute(out var attr))
            {
                bool alreadyHave = false;
                foreach (var attribute in attributes)
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
                { Errors.Add(new Error("Attribute '" + attr + "' already applied to the struct", attr.Identifier)); }
            }

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("struct", out Token keyword))
            { currentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token possibleStructName))
            { throw new SyntaxException("Expected struct identifier after keyword 'struct'", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected '{' after struct identifier", possibleStructName); }

            keyword.AnalysedType = TokenAnalysedType.Keyword;

            List<FieldDefinition> fields = new();
            Dictionary<string, FunctionDefinition> methods = new();

            int endlessSafe = 0;
            Token braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                FieldDefinition field = ExpectField();
                if (field == null)
                { throw new SyntaxException($"Expected field definition", CurrentToken); }

                fields.Add(field);
                if (!ExpectOperator(";"))
                { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", field.Identifier.After())); }

                endlessSafe++;
                if (endlessSafe > 50)
                {
                    throw new EndlessLoopException();
                }
            }

            CheckModifiers(modifiers, "export");

            StructDefinition structDefinition = new(possibleStructName, attributes, fields, methods, modifiers)
            {
                BracketStart = braceletStart,
                BracketEnd = braceletEnd,
            };

            Structs.Add(structDefinition.Name.Content, structDefinition);

            return true;
        }

        #endregion

        #region Parse low level

        bool ExpectListValue(out LiteralList listValue)
        {
            listValue = null;

            if (!ExpectOperator("[", out var o0))
            { return false; }

            List<StatementWithValue> values = new();
            listValue = new LiteralList()
            {
                Values = Array.Empty<StatementWithValue>(),
                BracketLeft = o0,
            };

            int endlessSafe = 0;
            while (true)
            {
                var v = ExpectExpression();
                if (v != null)
                {
                    values.Add(v);

                    if (!ExpectOperator(","))
                    {
                        if (!ExpectOperator("]", out var o1))
                        { throw new SyntaxException("Unbalanced '['", o0); }
                        listValue.BracketRight = o1;
                        break;
                    }
                }
                else
                {
                    if (!ExpectOperator("]", out var o1))
                    { throw new SyntaxException("Unbalanced '['", o0); }
                    listValue.BracketRight = o1;
                    break;
                }

                endlessSafe++;
                if (endlessSafe >= 50) { throw new EndlessLoopException(); }
            }
            listValue.Values = values.ToArray();

            return true;
        }

        bool ExpectLiteral(out Literal statement)
        {
            int savedToken = currentTokenIndex;

            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                Literal literal = new()
                {
                    Value = CurrentToken.Content.Replace("_", ""),
                    Type = LiteralType.FLOAT,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                Literal literal = new()
                {
                    Value = CurrentToken.Content.Replace("_", ""),
                    Type = LiteralType.INT,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                Literal literal = new()
                {
                    Value = Convert.ToInt32(CurrentToken.Content, 16).ToString(),
                    Type = LiteralType.INT,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
            {
                Literal literal = new()
                {
                    Value = Convert.ToInt32(CurrentToken.Content[2..].Replace("_", ""), 2).ToString(),
                    Type = LiteralType.INT,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                Literal literal = new()
                {
                    Value = CurrentToken.Content,
                    Type = LiteralType.STRING,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_CHAR)
            {
                Literal literal = new()
                {
                    Value = CurrentToken.Content,
                    Type = LiteralType.CHAR,
                    ValueToken = CurrentToken,
                };

                currentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("true", out var tTrue))
            {
                Literal literal = new()
                {
                    Value = "true",
                    Type = LiteralType.BOOLEAN,
                    ValueToken = CurrentToken,
                };

                tTrue.AnalysedType = TokenAnalysedType.Keyword;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("false", out var tFalse))
            {
                Literal literal = new()
                {
                    Value = "false",
                    Type = LiteralType.BOOLEAN,
                    ValueToken = CurrentToken,
                };

                tFalse.AnalysedType = TokenAnalysedType.Keyword;

                statement = literal;
                return true;
            }

            currentTokenIndex = savedToken;

            statement = null;
            return false;
        }

        bool ExpectAs(out TypeCast statement)
        {
            int parseStart = currentTokenIndex;
            statement = null;

            StatementWithValue prevStatement = ExpectOneValue();
            if (prevStatement == null)
            {
                currentTokenIndex = parseStart;
                return false;
            }

            if (!ExpectIdentifier("as", out Token keyword))
            {
                currentTokenIndex = parseStart;
                return false;
            }

            TypeInstance type = ExpectType(false, false);

            if (type == null)
            { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

            statement = new TypeCast(prevStatement, keyword, type);
            return true;
        }

        bool ExpectIndex(out IndexCall statement)
        {
            if (!ExpectOperator("[", out Token bracketLeft))
            {
                statement = null;
                return false;
            }

            StatementWithValue expression = ExpectExpression();

            if (!ExpectOperator("]", out Token bracketRight))
            { throw new SyntaxException("Unbalanced [", bracketLeft); }

            statement = new IndexCall(expression, bracketLeft, bracketRight);
            return true;
        }

        /// <returns>
        /// <list type="bullet">
        /// <item>
        ///  <seealso cref="FunctionCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Literal"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="NewInstance"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Field"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Identifier"></seealso>
        /// </item>
        /// </list>
        /// </returns>
        StatementWithValue ExpectOneValue()
        {
            int savedToken = currentTokenIndex;

            StatementWithValue returnStatement = null;

            returnStatement ??= ExpectKeywordCall("clone", 1);

            if (returnStatement != null)
            { }
            else if (ExpectListValue(out var listValue))
            {
                returnStatement = listValue;
            }
            else if (ExpectLiteral(out var literal))
            {
                returnStatement = literal;
            }
            else if (ExpectOperator("(", out var braceletT))
            {
                var expression = ExpectExpression();
                if (expression == null)
                { throw new SyntaxException("Expected expression after '('", braceletT); }

                if (expression is OperatorCall operation)
                { operation.InsideBracelet = true; }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Unbalanced '('", braceletT); }

                returnStatement = expression;
            }
            else if (ExpectIdentifier("new", out Token newIdentifier))
            {
                newIdentifier.AnalysedType = TokenAnalysedType.Keyword;

                TypeInstance instanceTypeName = ExpectType(false, false);

                if (instanceTypeName == null)
                { throw new SyntaxException("Expected instance constructor after keyword 'new'", newIdentifier); }

                if (ExpectOperator("(", out Token bracketLeft))
                {
                    ConstructorCall newStructStatement = new()
                    {
                        TypeName = instanceTypeName,
                        Keyword = newIdentifier,
                        BracketLeft = bracketLeft,
                    };

                    bool expectParameter = false;
                    List<StatementWithValue> parameters = new();

                    int endlessSafe = 0;
                    Token bracketRight = null;
                    while (!ExpectOperator(")", out bracketRight) || expectParameter)
                    {
                        StatementWithValue parameter = ExpectExpression();
                        if (parameter == null)
                        { throw new SyntaxException("Expected expression as parameter", newStructStatement); }

                        parameters.Add(parameter);

                        if (ExpectOperator(")"))
                        { break; }

                        if (!ExpectOperator(","))
                        { throw new SyntaxException("Expected ',' to separate parameters", parameter); }
                        else
                        { expectParameter = true; }

                        endlessSafe++;
                        if (endlessSafe > 100)
                        { throw new EndlessLoopException(); }
                    }
                    newStructStatement.Parameters = parameters.ToArray();
                    newStructStatement.BracketRight = bracketRight;

                    returnStatement = newStructStatement;
                }
                else
                {
                    NewInstance newStructStatement = new()
                    {
                        TypeName = instanceTypeName,
                        Keyword = newIdentifier,
                    };

                    returnStatement = newStructStatement;
                }
            }
            else if (ExpectIdentifier(out Token variableName))
            {
                // if (variableName.Content == "this")
                // { Errors.Add(new Error("The keyword 'this' does not avaiable in the current context", variableName)); }

                if (ExpectOperator("("))
                {
                    currentTokenIndex = savedToken;
                    returnStatement = ExpectFunctionCall();
                }
                else
                {
                    Identifier variableNameStatement = new()
                    {
                        VariableName = variableName,
                    };

                    if (variableName.Content == "this")
                    { variableName.AnalysedType = TokenAnalysedType.Keyword; }
                    else
                    { variableName.AnalysedType = TokenAnalysedType.VariableName; }

                    returnStatement = variableNameStatement;
                }
            }
            else if (ExpectVariableAddressGetter(out AddressGetter memoryAddressGetter))
            {
                returnStatement = memoryAddressGetter;
            }
            else if (ExpectVariableAddressFinder(out Pointer pointer))
            {
                returnStatement = pointer;
            }

            while (true)
            {
                if (ExpectOperator(".", out var tokenDot))
                {
                    if (ExpectMethodCall(false, out var methodCall))
                    {
                        methodCall.PrevStatement = returnStatement;
                        returnStatement = methodCall;
                    }
                    else
                    {
                        if (!ExpectIdentifier(out Token fieldName))
                        { throw new SyntaxException("Expected field or method", tokenDot); }

                        var fieldStatement = new Field()
                        {
                            FieldName = fieldName,
                            PrevStatement = returnStatement
                        };
                        returnStatement = fieldStatement;
                    }

                    continue;
                }

                if (ExpectIndex(out var statementIndex))
                {
                    statementIndex.PrevStatement = returnStatement;
                    returnStatement = statementIndex;

                    continue;
                }

                break;
            }

            {
                if (ExpectIdentifier("as", out Token keyword))
                {
                    TypeInstance type = ExpectType(false, false);

                    if (type == null)
                    { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

                    returnStatement = new TypeCast(returnStatement, keyword, type);
                }
            }

            return returnStatement;
        }

        bool ExpectVariableAddressGetter(out AddressGetter statement)
        {
            var parseStart = currentTokenIndex;
            if (!ExpectOperator("&", out var refToken))
            {
                statement = null;
                currentTokenIndex = parseStart;
                return false;
            }

            var prevStatement = ExpectOneValue();

            statement = new AddressGetter()
            {
                OperatorToken = refToken,
                PrevStatement = prevStatement,
            };
            return true;
        }

        bool ExpectVariableAddressFinder(out Pointer statement)
        {
            var parseStart = currentTokenIndex;
            if (!ExpectOperator("*", out var refToken))
            {
                statement = null;
                currentTokenIndex = parseStart;
                return false;
            }

            var prevStatement = ExpectOneValue();

            statement = new Pointer()
            {
                OperatorToken = refToken,
                PrevStatement = prevStatement,
            };
            return true;
        }

        void SetStatementThings(Statement.Statement statement)
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

        bool ExpectBlock(out Block block)
        {
            if (!ExpectOperator("{", out var braceletStart))
            {
                block = null;
                return false;
            }

            List<Statement.Statement> statements = new();

            int endlessSafe = 0;
            Token braceletEnd = null;
            while (!ExpectOperator("}", out braceletEnd))
            {
                Statement.Statement statement = ExpectStatement();
                SetStatementThings(statement);

                statements.Add(statement);

                if (!ExpectOperator(";"))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition().After())); }


                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }

            block = new Block(statements)
            {
                BracketStart = braceletStart,
                BracketEnd = braceletEnd,
            };
            return true;
        }

        List<Statement.Statement> ParseFunctionBody(out Token braceletStart, out Token braceletEnd)
        {
            braceletEnd = null;

            if (!ExpectOperator("{", out braceletStart))
            { return null; }

            List<Statement.Statement> statements = new();

            int endlessSafe = 0;
            while (!ExpectOperator("}", out braceletEnd))
            {
                Statement.Statement statement = ExpectStatement();
                SetStatementThings(statement);

                statements.Add(statement);

                if (!ExpectOperator(";"))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.TotalPosition().After())); }


                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }

            return statements;
        }

        VariableDeclaretion ExpectVariableDeclaration()
        {
            int startTokenIndex = currentTokenIndex;

            ExpectIdentifier("const", out Token constModifier);

            TypeInstance possibleType = ExpectType();
            if (possibleType == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (!ExpectIdentifier(out Token possibleVariableName))
            { currentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.AnalysedType = TokenAnalysedType.VariableName;

            VariableDeclaretion statement = new()
            {
                VariableName = possibleVariableName,
                Type = possibleType,
                Modifiers = constModifier == null ? Array.Empty<Token>() : new Token[1] { constModifier },
            };

            if (ExpectOperator("=", out var eqT))
            {
                statement.InitialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqT);
            }
            else
            {
                if (possibleType.Identifier.Content == "var")
                { throw new SyntaxException("Initial value for 'var' variable declaration is requied", possibleType.Identifier); }
            }

            return statement;
        }

        ForLoop ExpectForStatement()
        {
            if (!ExpectIdentifier("for", out Token tokenFor))
            { return null; }

            tokenFor.AnalysedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token tokenZarojel))
            { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor.After()); }

            var variableDeclaration = ExpectVariableDeclaration();
            if (variableDeclaration == null)
            { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenZarojel); }

            if (!ExpectOperator(";"))
            { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.TotalPosition().After()); }

            StatementWithValue condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenZarojel); }

            if (!ExpectOperator(";"))
            { throw new SyntaxException($"Expected ';' after \"for\" condition, got {CurrentToken}", variableDeclaration.TotalPosition().After()); }

            AnyAssignment expression = ExpectAnySetter();
            if (expression == null)
            { throw new SyntaxException($"Expected setter after \"for\" condition, got {CurrentToken}", tokenZarojel); }

            if (!ExpectOperator(")", out Token tokenZarojel2))
            { throw new SyntaxException($"Expected ')' after \"for\" condition, got {CurrentToken}", condition.TotalPosition().After()); }

            if (!ExpectBlock(out Block block))
            { throw new SyntaxException($"Expected block, got {CurrentToken}", tokenZarojel2.After()); }

            ForLoop forStatement = new()
            {
                Keyword = tokenFor,
                VariableDeclaration = variableDeclaration,
                Condition = condition,
                Expression = expression,
                Block = block,
            };

            return forStatement;
        }

        WhileLoop ExpectWhileStatement()
        {
            if (!ExpectIdentifier("while", out Token tokenWhile))
            { return null; }

            tokenWhile.AnalysedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token tokenZarojel))
            { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

            StatementWithValue condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"while\" statement", tokenZarojel); }

            if (!ExpectOperator(")", out Token tokenZarojel2))
            { throw new SyntaxException("Expected ')' after \"while\" condition", condition); }

            if (!ExpectBlock(out Block block))
            { throw new SyntaxException("Expected block", tokenZarojel2.After()); }

            WhileLoop whileStatement = new()
            {
                Keyword = tokenWhile,
                Condition = condition,
                Block = block,
            };

            return whileStatement;
        }

        IfContainer ExpectIfStatement()
        {
            BaseBranch ifStatement = ExpectIfSegmentStatement("if", BaseBranch.IfPart.If, true);
            if (ifStatement == null) return null;

            IfContainer statement = new();
            statement.Parts.Add(ifStatement);

            int endlessSafe = 0;
            while (true)
            {
                BaseBranch elseifStatement = ExpectIfSegmentStatement("elseif", BaseBranch.IfPart.ElseIf, true);
                if (elseifStatement == null) break;
                statement.Parts.Add(elseifStatement);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            BaseBranch elseStatement = ExpectIfSegmentStatement("else", BaseBranch.IfPart.Else, false);
            if (elseStatement != null)
            {
                statement.Parts.Add(elseStatement);
            }

            return statement;
        }

        BaseBranch ExpectIfSegmentStatement(string ifSegmentName, BaseBranch.IfPart ifSegmentType, bool needParameters)
        {
            if (!ExpectIdentifier(ifSegmentName, out Token tokenIf))
            { return null; }

            tokenIf.AnalysedType = TokenAnalysedType.Statement;

            StatementWithValue condition = null;
            if (needParameters)
            {
                if (!ExpectOperator("(", out Token tokenZarojel))
                { throw new SyntaxException("Expected '(' after \"" + ifSegmentName + "\" statement", tokenIf); }
                condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenZarojel); }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Expected ')' after \"" + ifSegmentName + "\" condition", condition); }
            }
            if (!ExpectBlock(out Block block))
            { throw new SyntaxException("Expected block", tokenIf.After()); }

            return ifSegmentType switch
            {
                BaseBranch.IfPart.If => new IfBranch()
                {
                    Keyword = tokenIf,
                    Condition = condition,
                    Block = block,
                },
                BaseBranch.IfPart.ElseIf => new ElseIfBranch()
                {
                    Keyword = tokenIf,
                    Condition = condition,
                    Block = block,
                },
                BaseBranch.IfPart.Else => new ElseBranch()
                {
                    Keyword = tokenIf,
                    Block = block,
                },
                _ => throw new ImpossibleException(),
            };
        }

        /// <returns>
        /// <list type="bullet">
        /// <item>
        ///  <seealso cref="WhileLoop"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="ForLoop"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="FunctionCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="KeywordCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="IfContainer"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="VariableDeclaretion"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Assignment"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="ExpectExpression"></seealso>
        /// </item>
        /// </list>
        /// </returns>
        Statement.Statement ExpectStatement()
        {
            Statement.Statement statement = ExpectWhileStatement();
            statement ??= ExpectForStatement();
            statement ??= ExpectKeywordCall("return", 0, 1);
            statement ??= ExpectKeywordCall("throw", 1);
            statement ??= ExpectKeywordCall("break", 0);
            statement ??= ExpectKeywordCall("delete", 1);
            statement ??= ExpectKeywordCall("clone", 1);
            statement ??= ExpectKeywordCall("out", 1, 64);
            statement ??= ExpectIfStatement();
            statement ??= ExpectVariableDeclaration();
            statement ??= ExpectShortOperator();
            statement ??= ExpectCompoundSetter();
            statement ??= ExpectSetter();
            statement ??= ExpectExpression();
            return statement;
        }

        bool ExpectMethodCall(bool expectDot, out FunctionCall methodCall)
        {
            int startTokenIndex = currentTokenIndex;

            methodCall = null;

            if (expectDot)
            {
                if (!ExpectOperator("."))
                { currentTokenIndex = startTokenIndex; return false; }
            }

            if (!ExpectIdentifier(out var possibleFunctionName))
            { currentTokenIndex = startTokenIndex; return false; }

            if (!ExpectOperator("(", out Token bracketLeft))
            { currentTokenIndex = startTokenIndex; return false; }

            possibleFunctionName.AnalysedType = TokenAnalysedType.FunctionName;

            methodCall = new()
            {
                Identifier = possibleFunctionName,
                BracketLeft = bracketLeft,
            };

            bool expectParameter = false;

            List<StatementWithValue> parameters = new();
            int endlessSafe = 0;
            Token bracketRight = null;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                StatementWithValue parameter = ExpectExpression();
                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", methodCall); }

                parameters.Add(parameter);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException($"Expected ',' to separate parameters, got {CurrentToken}", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }
            methodCall.Parameters = parameters.ToArray();
            methodCall.BracketRight = bracketRight;

            return true;
        }

        /// <returns>
        /// <list type="bullet">
        /// <item>
        ///  <seealso cref="FunctionCall"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Literal"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="NewInstance"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Field"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="Identifier"></seealso>
        /// </item>
        /// <item>
        ///  <seealso cref="OperatorCall"></seealso>
        /// </item>
        /// </list>
        /// </returns>
        /// <exception cref="SyntaxException"></exception>
        StatementWithValue ExpectExpression()
        {
            if (ExpectOperator("!", out var tNotOperator))
            {
                StatementWithValue statement = ExpectOneValue();
                if (statement == null)
                { throw new SyntaxException($"Expected OneValue after operator ('{tNotOperator}'), got {CurrentToken}", CurrentToken); }

                return new OperatorCall(tNotOperator, statement);
            }

            /*
            if (ExpectOperator("-", out var tMinusOperator))
            {
                Statement statement = ExpectOneValue();
                if (statement == null)
                { throw new SyntaxException("Expected value or expression after '-' operator", tMinusOperator); }

                return new Statement_Operator(tMinusOperator, statement);
            }
            */

            StatementWithValue leftStatement = ExpectOneValue();
            if (leftStatement == null) return null;

            while (true)
            {
                if (!ExpectOperator(new string[] {
                        "<<", ">>",
                        "+", "-", "*", "/", "%", "&", "|",
                        "<", ">", ">=", "<=", "!=", "==", "&&", "||", "^"
                    }, out Token op)) break;

                StatementWithValue rightStatement = ExpectOneValue();

                if (rightStatement == null)
                { throw new SyntaxException($"Expected OneValue after operator ('{op}'), got {CurrentToken}", CurrentToken); }

                int rightSidePrecedence = OperatorPrecedence(op.Content);

                OperatorCall rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
                if (rightmostStatement != null)
                {
                    OperatorCall operatorCall = new(op, rightmostStatement.Right, rightStatement);
                    rightmostStatement.Right = operatorCall;
                }
                else
                {
                    OperatorCall operatorCall = new(op, leftStatement, rightStatement);
                    leftStatement = operatorCall;
                }
            }

            return leftStatement;
        }

        /// <exception cref="SyntaxException"></exception>
        AnyAssignment ExpectAnySetter()
        {
            AnyAssignment statement = null;
            statement ??= ExpectShortOperator();
            statement ??= ExpectCompoundSetter();
            statement ??= ExpectSetter();
            return statement;
        }

        /// <exception cref="SyntaxException"></exception>
        Assignment ExpectSetter()
        {
            int parseStart = currentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();
            if (leftStatement == null)
            {
                currentTokenIndex = parseStart;
                return null;
            }

            if (!ExpectOperator("=", out Token @operator))
            {
                currentTokenIndex = parseStart;
                return null;
            }

            StatementWithValue valueToAssign = ExpectExpression();
            if (valueToAssign == null)
            { throw new SyntaxException("Expected expression after assignment operator", @operator); }

            return new Assignment(@operator, leftStatement, valueToAssign);
        }

        /// <exception cref="SyntaxException"></exception>
        CompoundAssignment ExpectCompoundSetter()
        {
            int parseStart = currentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();
            if (leftStatement == null)
            {
                currentTokenIndex = parseStart;
                return null;
            }

            if (!ExpectOperator(new string[] {
                    "+=", "-=", "*=", "/=", "%=",
                    "&=", "|=", "^=",
                }, out var @operator))
            {
                currentTokenIndex = parseStart;
                return null;
            }

            StatementWithValue valueToAssign = ExpectExpression();
            if (valueToAssign == null)
            { throw new SyntaxException("Expected expression after compound assignment operator", @operator); }

            return new CompoundAssignment(@operator, leftStatement, valueToAssign);
        }

        /// <exception cref="SyntaxException"></exception>
        ShortOperatorCall ExpectShortOperator()
        {
            int parseStart = currentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();

            if (leftStatement == null)
            {
                currentTokenIndex = parseStart;
                return null;
            }

            if (ExpectOperator("++", out var t0))
            {
                return new ShortOperatorCall(t0, leftStatement);
            }

            if (ExpectOperator("--", out var t1))
            {
                return new ShortOperatorCall(t1, leftStatement);
            }

            currentTokenIndex = parseStart;
            return null;
        }

        /// <exception cref="SyntaxException"></exception>
        bool ExpectModifiedValue(out ModifiedStatement modifiedStatement, params string[] validModifiers)
        {
            if (!ExpectIdentifier(out Token modifier, validModifiers))
            {
                modifiedStatement = null;
                return false;
            }

            modifier.AnalysedType = TokenAnalysedType.Keyword;

            var value = ExpectOneValue();

            if (value == null)
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.After()); }

            modifiedStatement = new ModifiedStatement(value, modifier);
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="statement"></param>
        /// <param name="rightSidePrecedence"></param>
        /// <returns>
        /// <see langword="null"/> or <see cref="OperatorCall"/>
        /// </returns>
        OperatorCall FindRightmostStatement(Statement.Statement statement, int rightSidePrecedence)
        {
            if (statement is not OperatorCall leftSide) return null;
            if (OperatorPrecedence(leftSide.Operator.Content) >= rightSidePrecedence) return null;
            if (leftSide.InsideBracelet) return null;

            OperatorCall right = FindRightmostStatement(leftSide.Right, rightSidePrecedence);

            if (right == null) return leftSide;
            return right;
        }

        int OperatorPrecedence(string str)
        {
            if (operators.TryGetValue(str, out int precedence))
            { return precedence; }
            else throw new InternalException($"Precedence for operator {str} not found");
        }

        FunctionCall ExpectFunctionCall()
        {
            int startTokenIndex = currentTokenIndex;

            if (!ExpectIdentifier(out Token possibleFunctionName))
            { currentTokenIndex = startTokenIndex; return null; }

            if (possibleFunctionName == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (!ExpectOperator("(", out Token bracketLeft))
            { currentTokenIndex = startTokenIndex; return null; }

            possibleFunctionName.AnalysedType = TokenAnalysedType.BuiltinType;

            FunctionCall functionCall = new()
            {
                Identifier = possibleFunctionName,
                BracketLeft = bracketLeft,
            };

            bool expectParameter = false;
            List<StatementWithValue> parameters = new();

            int endlessSafe = 0;
            Token bracketRight = null;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {

                StatementWithValue parameter = null;

                if (ExpectModifiedValue(out ModifiedStatement modifiedStatement, PassedParameterModifiers))
                {
                    parameter = modifiedStatement;
                }
                else
                {
                    parameter = ExpectExpression();
                }

                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", functionCall); }

                parameters.Add(parameter);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' to separate parameters", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }
            functionCall.Parameters = parameters.ToArray();
            functionCall.BracketRight = bracketRight;

            return functionCall;
        }

        KeywordCall ExpectKeywordCall(string name, int parameterCount)
            => ExpectKeywordCall(name, parameterCount, parameterCount);
        KeywordCall ExpectKeywordCall(string name, int minParameterCount, int maxParameterCount)
        {
            int startTokenIndex = currentTokenIndex;

            if (!ExpectIdentifier(out Token possibleFunctionName))
            { currentTokenIndex = startTokenIndex; return null; }

            if (possibleFunctionName.Content != name)
            { currentTokenIndex = startTokenIndex; return null; }

            possibleFunctionName.AnalysedType = TokenAnalysedType.Statement;

            KeywordCall functionCall = new()
            {
                Identifier = possibleFunctionName,
            };
            List<StatementWithValue> parameters = new();

            int endlessSafe = 16;
            while (true)
            {
                if (endlessSafe-- < 0) throw new EndlessLoopException();

                StatementWithValue parameter = ExpectExpression();

                if (parameter != null)
                { parameters.Add(parameter); }
                else
                { break; }
            }

            functionCall.Parameters = parameters.ToArray();

            if (functionCall.Parameters.Length < minParameterCount)
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requies minimum {minParameterCount} parameters but you passed {parameters.Count}", functionCall); }

            if (functionCall.Parameters.Length > maxParameterCount)
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requies maximum {maxParameterCount} parameters but you passed {parameters.Count}", functionCall); }

            return functionCall;
        }

        #endregion

        bool ExpectAttribute(out FunctionDefinition.Attribute attribute)
        {
            int parseStart = currentTokenIndex;
            attribute = new();

            if (!ExpectOperator("[", out var t0))
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token attributeT))
            { currentTokenIndex = parseStart; return false; }

            attributeT.AnalysedType = TokenAnalysedType.Attribute;

            if (ExpectOperator("(", out var t3))
            {
                List<object> parameters = new();
                int endlessSafe = 50;
                while (!ExpectOperator(")"))
                {
                    ExpectOneLiteral(out object param);
                    if (param == null)
                    { throw new SyntaxException("Expected parameter", t3); }
                    ExpectOperator(",");

                    parameters.Add(param);

                    endlessSafe--;
                    if (endlessSafe <= 0)
                    { throw new EndlessLoopException(); }
                }
                attribute.Parameters = parameters.ToArray();
            }

            if (!ExpectOperator("]"))
            { throw new SyntaxException("Unbalanced ]", t0); }

            attribute.Identifier = attributeT;
            return true;
        }

        FieldDefinition ExpectField()
        {
            int startTokenIndex = currentTokenIndex;

            if (ExpectIdentifier("private", out Token protectionToken))
            {

            }

            TypeInstance possibleType = ExpectType();
            if (possibleType == null)
            { currentTokenIndex = startTokenIndex; return null; }

            if (!ExpectIdentifier(out Token possibleVariableName))
            { currentTokenIndex = startTokenIndex; return null; }

            if (ExpectOperator("("))
            { currentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.AnalysedType = TokenAnalysedType.None;

            FieldDefinition field = new()
            {
                Identifier = possibleVariableName,
                Type = possibleType,
                ProtectionToken = protectionToken,
            };

            return field;
        }
        bool ExpectField(out FieldDefinition field)
        {
            field = ExpectField();
            return field != null;
        }

        void ExpectOneLiteral(out object value)
        {
            value = null;

            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                value = float.Parse(CurrentToken.Content.Replace("_", ""));

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                value = int.Parse(CurrentToken.Content.Replace("_", ""));

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                value = Convert.ToInt32(CurrentToken.Content, 16);

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
            {
                value = Convert.ToInt32(CurrentToken.Content.Replace("_", ""), 2);

                currentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                value = CurrentToken.Content;

                currentTokenIndex++;
            }
        }

        #region Basic parsing

        Token[] ParseParameterModifiers(int parameterIndex)
        {
            List<Token> modifiers = new();
            int endlessSafe = 16;
            while (true)
            {
                if (ExpectIdentifier(out Token modifier, ParameterModifiers))
                {
                    modifier.AnalysedType = TokenAnalysedType.Keyword;
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
                if (ExpectIdentifier(out Token modifier, Modifiers))
                {
                    modifier.AnalysedType = TokenAnalysedType.Keyword;
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
            foreach (var modifier in modifiers)
            {
                if (!validModifiers.Contains(modifier.Content))
                { throw new SyntaxException($"Modifier \"{modifier}\" not valid in the current context", modifier); }
            }
        }

        bool ExpectIdentifier(out Token result) => ExpectIdentifier("", out result);
        bool ExpectIdentifier(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            result = CurrentToken;
            currentTokenIndex++;

            return true;
        }
        bool ExpectIdentifier(out Token result, params string[] names)
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
        bool ExpectOperator(string[] name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
            if (name.Contains(CurrentToken.Content) == false) return false;

            result = CurrentToken;
            currentTokenIndex++;

            return true;
        }
        bool ExpectOperator(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            result = CurrentToken;
            currentTokenIndex++;

            return true;
        }

        TypeInstance ExpectType(bool allowVarKeyword = true, bool allowAnyKeyword = false)
        {
            int parseStart = currentTokenIndex;

            if (!ExpectIdentifier(out Token possibleType)) return null;

            possibleType.AnalysedType = TokenAnalysedType.Keyword;

            if (possibleType.Content == "any")
            {
                if (!allowAnyKeyword)
                {
                    Errors.Add(new Error($"Type '{possibleType.Content}' is not valid in the current context", possibleType));
                    return null;
                }

                return new TypeInstance(possibleType);
            }

            if (possibleType.Content == "var")
            {
                if (!allowVarKeyword)
                {
                    Errors.Add(new Error($"Type '{possibleType.Content}' is not valid in the current context", possibleType));
                    return null;
                }

                return new TypeInstance(possibleType);
            }

            TypeInstance newType = new(possibleType);

            if (types.Contains(possibleType.Content))
            {

                newType.Identifier.AnalysedType = TokenAnalysedType.BuiltinType;
            }
            else
            {
                if (TryGetStruct(possibleType.Content, out _))
                { newType.Identifier.AnalysedType = TokenAnalysedType.Struct; }
                else if (TryGetClass(possibleType.Content, out _))
                { newType.Identifier.AnalysedType = TokenAnalysedType.Class; }
            }

            if (ExpectOperator("<"))
            {
                while (true)
                {
                    var type = ExpectType(false, false);
                    if (type == null)
                    { throw new SyntaxException($"Expected type as generic parameter", CurrentToken); }

                    newType.GenericTypes.Add(type);

                    if (ExpectOperator(">"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }
            }

            if (ExpectOperator("[", out var listToken0))
            { return null; }

            return newType;
        }

        bool TryGetStruct(string name, out StructDefinition @struct)
            => Structs.TryGetValue(name, out @struct);
        bool TryGetClass(string name, out ClassDefinition @class)
            => Classes.TryGetValue(name, out @class);

        #endregion
    }
}
