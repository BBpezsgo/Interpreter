using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode.Parser
{
    using System.ComponentModel.DataAnnotations;
    using Core;
    using Errors;
    using Statement;

    public class Parser
    {
        int CurrentTokenIndex;
        readonly Token[] Tokens;

        Token CurrentToken => (CurrentTokenIndex >= 0 && CurrentTokenIndex < Tokens.Length) ? Tokens[CurrentTokenIndex] : null;

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
            "!",
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
            this.Tokens = tokens;
        }

        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="ImpossibleException"/>
        public static ParserResult Parse(Token[] tokens)
            => new Parser(tokens).ParseInternal();

        public static ParserResultHeader ParseCodeHeader(Token[] tokens)
            => new Parser(tokens).ParseCodeHeaderInternal();

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

        ParserResultHeader ParseCodeHeaderInternal()
        {
            CurrentTokenIndex = 0;

            ParseCodeHeader();

            return new ParserResultHeader(this.Usings, this.Hashes);
        }

        #region Parse top level

        bool ExpectHash(out CompileTag hashStatement)
        {
            hashStatement = null;

            if (!ExpectOperator("#", out var hashT))
            { return false; }

            hashT.AnalyzedType = TokenAnalysedType.Hash;

            if (!ExpectIdentifier(out var hashName))
            { throw new SyntaxException($"Expected identifier after '#' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", hashT); }

            hashName.AnalyzedType = TokenAnalysedType.Hash;

            List<Literal> parameters = new();
            int endlessSafe = 50;
            while (!ExpectOperator(";"))
            {
                if (!ExpectLiteral(out var parameter))
                { throw new SyntaxException($"Expected hash literal parameter or ';' , got {CurrentToken.TokenType.ToString().ToLower()} \"{CurrentToken.Content}\"", CurrentToken); }

                parameter.ValueToken.AnalyzedType = TokenAnalysedType.HashParameter;
                parameters.Add(parameter);

                if (ExpectOperator(";"))
                { break; }

                endlessSafe--;
                if (endlessSafe <= 0)
                { throw new EndlessLoopException(); }
            }

            hashStatement = new CompileTag(hashT, hashName, parameters.ToArray());

            return true;
        }

        bool ExpectUsing(out UsingDefinition usingDefinition)
        {
            usingDefinition = null;
            if (!ExpectIdentifier("using", out var keyword))
            { return false; }

            keyword.AnalyzedType = TokenAnalysedType.Keyword;

            List<Token> tokens = new();
            if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                tokens.Add(CurrentToken);
                CurrentTokenIndex++;
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
            else if (ExpectMacroDefinition(out var macroDefinition))
            { Macros.Add(macroDefinition); }
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
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.GetPosition().After())); }
            }
        }

        bool ExpectEnumDefinition(out EnumDefinition enumDefinition)
        {
            int parseStart = CurrentTokenIndex;
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
            { CurrentTokenIndex = parseStart; return false; }

            keyword.AnalyzedType = TokenAnalysedType.Keyword;

            if (!ExpectIdentifier(out Token identifier))
            { throw new SyntaxException($"Expected identifier token after keyword \"{keyword}\"", keyword.After()); }

            if (!ExpectOperator("{"))
            { throw new SyntaxException($"Expected '{{' after enum identifier", identifier.After()); }

            identifier.AnalyzedType = TokenAnalysedType.Enum;

            List<EnumMemberDefinition> members = new();

            while (!ExpectOperator("}"))
            {
                if (!ExpectIdentifier(out Token enumMemberIdentifier))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                enumMemberIdentifier.AnalyzedType = TokenAnalysedType.EnumMember;

                Literal enumMemberValue = null;

                if (ExpectOperator("=", out Token assignOperator))
                {
                    if (!ExpectLiteral(out enumMemberValue))
                    { throw new SyntaxException($"Expected literal after enum member assignment", assignOperator.After()); }

                }

                members.Add(new EnumMemberDefinition()
                {
                    Identifier = enumMemberIdentifier,
                    Value = enumMemberValue,
                });

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
            int parseStart = CurrentTokenIndex;
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

            TypeInstance possibleType = ExpectType(AllowedType.None);
            if (possibleType == null)
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator(OverloadableOperators, out Token possibleName))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleName.AnalyzedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this", "temp");

                TypeInstance possibleParameterType = ExpectType(AllowedType.None);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

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

        bool ExpectMacroDefinition(out MacroDefinition function)
        {
            int parseStart = CurrentTokenIndex;
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
                { Errors.Add(new Error("Attribute '" + attr + "' already applied to the macro", attr.Identifier)); }
            }

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("macro", out Token macroKeyword))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalysedType.FunctionName;

            List<Token> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;
                parameters.Add(possibleParameterNameT);

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

        bool ExpectFunctionDefinition(out FunctionDefinition function)
        {
            int parseStart = CurrentTokenIndex;
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

            TypeInstance possibleType = ExpectType(AllowedType.None);
            if (possibleType == null)
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "this", "ref", "temp");

                TypeInstance possibleParameterType = ExpectType(AllowedType.FunctionPointer);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

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
            int parseStart = CurrentTokenIndex;
            function = null;

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier(out Token possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalysedType.FunctionName;

            List<ParameterDefinition> parameters = new();

            var expectParameter = false;
            while (!ExpectOperator(")") || expectParameter)
            {
                Token[] parameterModifiers = ParseParameterModifiers(parameters.Count);
                CheckModifiers(parameterModifiers, "temp");

                TypeInstance possibleParameterType = ExpectType(AllowedType.None);
                if (possibleParameterType == null)
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

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
            { throw new SyntaxException($"Body is required for general function definition", tIdk); }

            List<Statement.Statement> statements = ParseFunctionBody(out var braceletStart, out var braceletEnd);
            function.BracketStart = braceletStart;
            function.BracketEnd = braceletEnd;

            function.Statements = statements.ToArray();

            return true;
        }

        bool ExpectClassDefinition()
        {
            int startTokenIndex = CurrentTokenIndex;

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
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token possibleClassName))
            { throw new SyntaxException("Expected class identifier after keyword 'class'", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected '{' after class identifier", possibleClassName); }

            possibleClassName.AnalyzedType = TokenAnalysedType.Class;
            keyword.AnalyzedType = TokenAnalysedType.Keyword;

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

            // Warnings.Add(new Warning($"Class is experimental feature!", keyword, classDefinition.FilePath));

            return true;
        }

        bool ExpectStructDefinition()
        {
            int startTokenIndex = CurrentTokenIndex;

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
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token possibleStructName))
            { throw new SyntaxException("Expected struct identifier after keyword 'struct'", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected '{' after struct identifier", possibleStructName); }

            keyword.AnalyzedType = TokenAnalysedType.Keyword;

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
            if (!ExpectOperator("[", out var bracketLeft))
            {
                listValue = null;
                return false;
            }

            List<StatementWithValue> values = new();

            Token bracketRight;

            int endlessSafe = 0;
            while (true)
            {
                var v = ExpectExpression();
                if (v != null)
                {
                    values.Add(v);

                    if (!ExpectOperator(","))
                    {
                        if (!ExpectOperator("]", out bracketRight))
                        { throw new SyntaxException("Unbalanced '['", bracketLeft); }
                        break;
                    }
                }
                else
                {
                    if (!ExpectOperator("]", out bracketRight))
                    { throw new SyntaxException("Unbalanced '['", bracketLeft); }
                    break;
                }

                endlessSafe++;
                if (endlessSafe >= 50) { throw new EndlessLoopException(); }
            }

            listValue = new LiteralList(bracketLeft, values.ToArray(), bracketRight);
            return true;
        }

        bool ExpectLiteral(out Literal statement)
        {
            int savedToken = CurrentTokenIndex;

            string v = CurrentToken.Content;

            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.FLOAT, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.INT, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                v = v[2..];
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.INT, Convert.ToInt32(v, 16).ToString(), CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
            {
                v = v[2..];
                v = v.Replace("_", "");

                Literal literal = new(LiteralType.INT, Convert.ToInt32(v, 2).ToString(), CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                Literal literal = new(LiteralType.STRING, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_CHAR)
            {
                Literal literal = new(LiteralType.CHAR, v, CurrentToken);

                CurrentTokenIndex++;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("true", out var tTrue))
            {
                Literal literal = new(LiteralType.BOOLEAN, tTrue.Content, tTrue);

                tTrue.AnalyzedType = TokenAnalysedType.Keyword;

                statement = literal;
                return true;
            }
            else if (ExpectIdentifier("false", out var tFalse))
            {
                Literal literal = new(LiteralType.BOOLEAN, tFalse.Content, tFalse);

                tFalse.AnalyzedType = TokenAnalysedType.Keyword;

                statement = literal;
                return true;
            }

            CurrentTokenIndex = savedToken;

            statement = null;
            return false;
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

            statement = new IndexCall(bracketLeft, expression, bracketRight);
            return true;
        }

        StatementWithValue ExpectOneValue()
        {
            int savedToken = CurrentTokenIndex;

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
                newIdentifier.AnalyzedType = TokenAnalysedType.Keyword;

                TypeInstance instanceTypeName = ExpectType(AllowedType.None);

                if (instanceTypeName == null)
                { throw new SyntaxException("Expected instance constructor after keyword 'new'", newIdentifier); }

                if (ExpectOperator("(", out Token bracketLeft))
                {
                    bool expectParameter = false;
                    List<StatementWithValue> parameters = new();

                    int endlessSafe = 0;
                    Token bracketRight;
                    while (!ExpectOperator(")", out bracketRight) || expectParameter)
                    {
                        StatementWithValue parameter = ExpectExpression();
                        if (parameter == null)
                        { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

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

                    ConstructorCall newStructStatement = new(newIdentifier, instanceTypeName, bracketLeft, parameters, bracketRight);

                    returnStatement = newStructStatement;
                }
                else
                {
                    returnStatement = new NewInstance(newIdentifier, instanceTypeName);
                }
            }
            else if (ExpectIdentifier(out Token variableName))
            {
                if (ExpectOperator("("))
                {
                    CurrentTokenIndex = savedToken;
                    returnStatement = ExpectFunctionCall();
                }
                else
                {
                    Identifier variableNameStatement = new(variableName);

                    if (variableName.Content == "this")
                    { variableName.AnalyzedType = TokenAnalysedType.Keyword; }
                    else
                    { variableName.AnalyzedType = TokenAnalysedType.VariableName; }

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
                    if (ExpectMethodCall(false, returnStatement, out var methodCall))
                    {
                        returnStatement = methodCall;
                    }
                    else
                    {
                        if (!ExpectIdentifier(out Token fieldName))
                        { throw new SyntaxException("Expected field or method", tokenDot); }

                        returnStatement = new Field(returnStatement, fieldName);
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
                    TypeInstance type = ExpectType(AllowedType.None);

                    if (type == null)
                    { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

                    returnStatement = new TypeCast(returnStatement, keyword, type);
                }
            }

            return returnStatement;
        }

        bool ExpectVariableAddressGetter(out AddressGetter statement)
        {
            if (!ExpectOperator("&", out var refToken))
            {
                statement = null;
                return false;
            }

            StatementWithValue prevStatement = ExpectOneValue();

            statement = new AddressGetter(refToken, prevStatement);
            return true;
        }

        bool ExpectVariableAddressFinder(out Pointer statement)
        {
            if (!ExpectOperator("*", out var refToken))
            {
                statement = null;
                return false;
            }

            StatementWithValue prevStatement = ExpectOneValue();

            statement = new Pointer(refToken, prevStatement);
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
            if (!ExpectOperator("{", out Token braceletStart))
            {
                block = null;
                return false;
            }

            List<Statement.Statement> statements = new();

            int endlessSafe = 0;
            Token braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                Statement.Statement statement = ExpectStatement();
                SetStatementThings(statement);

                statements.Add(statement);

                if (!ExpectOperator(";"))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.GetPosition().After())); }


                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }

            block = new Block(braceletStart, statements, braceletEnd);
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
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.GetPosition().After())); }


                endlessSafe++;
                if (endlessSafe > 500) throw new EndlessLoopException();
            }

            return statements;
        }

        VariableDeclaretion ExpectVariableDeclaration()
        {
            int startTokenIndex = CurrentTokenIndex;

            List<Token> modifiers = new();
            while (ExpectIdentifier(out Token modifier, VariableModifiers))
            { modifiers.Add(modifier); }

            TypeInstance possibleType = ExpectType(AllowedType.Implicit | AllowedType.FunctionPointer);
            if (possibleType == null)
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (!ExpectIdentifier(out Token possibleVariableName))
            { CurrentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.AnalyzedType = TokenAnalysedType.VariableName;

            StatementWithValue initialValue = null;

            if (ExpectOperator("=", out Token eqOperatorToken))
            {
                initialValue = ExpectExpression() ?? throw new SyntaxException("Expected initial value after '=' in variable declaration", eqOperatorToken);
            }
            else
            {
                if (possibleType.Identifier.Content == "var")
                { throw new SyntaxException("Initial value for variable declaration with implicit type is required", possibleType.Identifier); }
            }

            return new VariableDeclaretion(modifiers.ToArray(), possibleType, possibleVariableName, initialValue);
        }

        ForLoop ExpectForStatement()
        {
            if (!ExpectIdentifier("for", out Token tokenFor))
            { return null; }

            tokenFor.AnalyzedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token tokenParenthesesOpen))
            { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor.After()); }

            VariableDeclaretion variableDeclaration = ExpectVariableDeclaration();
            if (variableDeclaration == null)
            { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(";"))
            { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.GetPosition().After()); }

            StatementWithValue condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenParenthesesOpen); }

            if (!ExpectOperator(";"))
            { throw new SyntaxException($"Expected ';' after \"for\" condition, got {CurrentToken}", variableDeclaration.GetPosition().After()); }

            AnyAssignment expression = ExpectAnySetter();
            if (expression == null)
            { throw new SyntaxException($"Expected setter after \"for\" condition, got {CurrentToken}", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token tokenParenthesesClosed))
            { throw new SyntaxException($"Expected ')' after \"for\" condition, got {CurrentToken}", condition.GetPosition().After()); }

            if (!ExpectBlock(out Block block))
            { throw new SyntaxException($"Expected block, got {CurrentToken}", tokenParenthesesClosed.After()); }

            return new ForLoop(tokenFor, variableDeclaration, condition, expression, block);
        }

        WhileLoop ExpectWhileStatement()
        {
            if (!ExpectIdentifier("while", out Token tokenWhile))
            { return null; }

            tokenWhile.AnalyzedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token tokenParenthesesOpen))
            { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

            StatementWithValue condition = ExpectExpression();
            if (condition == null)
            { throw new SyntaxException("Expected condition after \"while\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token tokenParenthesesClose))
            { throw new SyntaxException("Expected ')' after \"while\" condition", condition); }

            if (!ExpectBlock(out Block block))
            { throw new SyntaxException("Expected block", tokenParenthesesClose.After()); }

            return new WhileLoop(tokenWhile, condition, block);
        }

        IfContainer ExpectIfStatement()
        {
            BaseBranch ifStatement = ExpectIfSegmentStatement("if", BaseBranch.IfPart.If, true);
            if (ifStatement == null) return null;

            List<BaseBranch> branches = new()
            { ifStatement };

            int endlessSafe = 0;
            while (true)
            {
                BaseBranch elseifStatement = ExpectIfSegmentStatement("elseif", BaseBranch.IfPart.ElseIf, true);
                if (elseifStatement == null) break;
                branches.Add(elseifStatement);

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            }

            BaseBranch elseStatement = ExpectIfSegmentStatement("else", BaseBranch.IfPart.Else, false);
            if (elseStatement != null)
            {
                branches.Add(elseStatement);
            }

            return new IfContainer(branches);
        }

        BaseBranch ExpectIfSegmentStatement(string ifSegmentName, BaseBranch.IfPart ifSegmentType, bool needParameters)
        {
            if (!ExpectIdentifier(ifSegmentName, out Token tokenIf))
            { return null; }

            tokenIf.AnalyzedType = TokenAnalysedType.Statement;

            StatementWithValue condition = null;
            if (needParameters)
            {
                if (!ExpectOperator("(", out Token tokenParenthesesOpen))
                { throw new SyntaxException("Expected '(' after \"" + ifSegmentName + "\" statement", tokenIf); }
                condition = ExpectExpression();
                if (condition == null)
                { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenParenthesesOpen); }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Expected ')' after \"" + ifSegmentName + "\" condition", condition); }
            }
            if (!ExpectBlock(out Block block))
            { throw new SyntaxException("Expected block", tokenIf.After()); }

            return ifSegmentType switch
            {
                BaseBranch.IfPart.If => new IfBranch(tokenIf, condition, block),
                BaseBranch.IfPart.ElseIf => new ElseIfBranch(tokenIf, condition, block),
                BaseBranch.IfPart.Else => new ElseBranch(tokenIf, block),
                _ => throw new ImpossibleException(),
            };
        }

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
            statement ??= ExpectAnySetter();
            statement ??= ExpectExpression();
            return statement;
        }

        bool ExpectMethodCall(bool expectDot, StatementWithValue prevStatement, out FunctionCall methodCall)
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

            if (!ExpectOperator("(", out Token bracketLeft))
            {
                CurrentTokenIndex = startTokenIndex;
                methodCall = null;
                return false;
            }

            possibleFunctionName.AnalyzedType = TokenAnalysedType.FunctionName;

            bool expectParameter = false;

            List<StatementWithValue> parameters = new();
            int endlessSafe = 0;
            Token bracketRight;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                StatementWithValue parameter = ExpectExpression();
                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

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

            methodCall = new(prevStatement, possibleFunctionName, bracketLeft, parameters, bracketRight);

            return true;
        }

        StatementWithValue ExpectExpression()
        {
            if (ExpectOperator(UnaryPrefixOperators, out Token unaryPrefixOperator))
            {
                StatementWithValue statement = ExpectOneValue();
                if (statement == null)
                { throw new SyntaxException($"Expected value after operator \"{unaryPrefixOperator}\", got \"{CurrentToken}\"", CurrentToken); }

                return new OperatorCall(unaryPrefixOperator, statement);
            }

            StatementWithValue leftStatement = ExpectModifiedOrOneValue(GeneralStatementModifiers);
            if (leftStatement == null) return null;

            while (true)
            {
                if (!ExpectOperator(BinaryOperators, out Token binaryOperator)) break;

                StatementWithValue rightStatement = ExpectModifiedOrOneValue(GeneralStatementModifiers);

                if (rightStatement == null)
                { throw new SyntaxException($"Expected value after operator \"{binaryOperator}\", got \"{CurrentToken}\"", CurrentToken); }

                int rightSidePrecedence = OperatorPrecedence(binaryOperator.Content);

                OperatorCall rightmostStatement = FindRightmostStatement(leftStatement, rightSidePrecedence);
                if (rightmostStatement != null)
                {
                    OperatorCall operatorCall = new(binaryOperator, rightmostStatement.Right, rightStatement);
                    rightmostStatement.Right = operatorCall;
                }
                else
                {
                    OperatorCall operatorCall = new(binaryOperator, leftStatement, rightStatement);
                    leftStatement = operatorCall;
                }
            }

            return leftStatement;
        }

        AnyAssignment ExpectAnySetter()
        {
            AnyAssignment statement = null;
            statement ??= ExpectShortOperator();
            statement ??= ExpectCompoundSetter();
            statement ??= ExpectSetter();
            return statement;
        }

        Assignment ExpectSetter()
        {
            int parseStart = CurrentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();
            if (leftStatement == null)
            {
                CurrentTokenIndex = parseStart;
                return null;
            }

            if (!ExpectOperator("=", out Token @operator))
            {
                CurrentTokenIndex = parseStart;
                return null;
            }

            StatementWithValue valueToAssign = ExpectExpression();
            if (valueToAssign == null)
            { throw new SyntaxException("Expected expression after assignment operator", @operator); }

            return new Assignment(@operator, leftStatement, valueToAssign);
        }

        CompoundAssignment ExpectCompoundSetter()
        {
            int parseStart = CurrentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();
            if (leftStatement == null)
            {
                CurrentTokenIndex = parseStart;
                return null;
            }

            if (!ExpectOperator(CompoundAssignmentOperators, out var @operator))
            {
                CurrentTokenIndex = parseStart;
                return null;
            }

            StatementWithValue valueToAssign = ExpectExpression();
            if (valueToAssign == null)
            { throw new SyntaxException("Expected expression after compound assignment operator", @operator); }

            return new CompoundAssignment(@operator, leftStatement, valueToAssign);
        }

        ShortOperatorCall ExpectShortOperator()
        {
            int parseStart = CurrentTokenIndex;
            StatementWithValue leftStatement = ExpectExpression();

            if (leftStatement == null)
            {
                CurrentTokenIndex = parseStart;
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

            CurrentTokenIndex = parseStart;
            return null;
        }

        StatementWithValue ExpectModifiedOrOneValue(params string[] validModifiers)
        {
            if (!ExpectIdentifier(out Token modifier, validModifiers))
            {
                return ExpectOneValue();
            }

            modifier.AnalyzedType = TokenAnalysedType.Keyword;

            var value = ExpectOneValue();

            if (value == null)
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.After()); }

            return new ModifiedStatement(modifier, value);
        }

        bool ExpectModifiedValue(out ModifiedStatement modifiedStatement, params string[] validModifiers)
        {
            if (!ExpectIdentifier(out Token modifier, validModifiers))
            {
                modifiedStatement = null;
                return false;
            }

            modifier.AnalyzedType = TokenAnalysedType.Keyword;

            var value = ExpectOneValue();

            if (value == null)
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.After()); }

            modifiedStatement = new ModifiedStatement(modifier, value);
            return true;
        }

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

        static int OperatorPrecedence(string @operator)
        {
            if (Constants.Operators.Precedencies.TryGetValue(@operator, out int precedence))
            { return precedence; }
            throw new InternalException($"Precedence for operator \"{@operator}\" not found");
        }

        FunctionCall ExpectFunctionCall()
        {
            int startTokenIndex = CurrentTokenIndex;

            if (!ExpectIdentifier(out Token possibleFunctionName))
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (possibleFunctionName == null)
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (!ExpectOperator("(", out Token bracketLeft))
            { CurrentTokenIndex = startTokenIndex; return null; }

            possibleFunctionName.AnalyzedType = TokenAnalysedType.BuiltinType;

            bool expectParameter = false;
            List<StatementWithValue> parameters = new();

            int endlessSafe = 0;
            Token bracketRight;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                StatementWithValue parameter;

                if (ExpectModifiedValue(out ModifiedStatement modifiedStatement, PassedParameterModifiers))
                {
                    parameter = modifiedStatement;
                }
                else
                {
                    parameter = ExpectExpression();
                }

                if (parameter == null)
                { throw new SyntaxException("Expected expression as parameter", CurrentToken); }

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
            };

            return new FunctionCall(null, possibleFunctionName, bracketLeft, parameters, bracketRight);
        }

        KeywordCall ExpectKeywordCall(string name, int parameterCount)
            => ExpectKeywordCall(name, parameterCount, parameterCount);
        KeywordCall ExpectKeywordCall(string name, int minParameterCount, int maxParameterCount)
        {
            int startTokenIndex = CurrentTokenIndex;

            if (!ExpectIdentifier(out Token possibleFunctionName))
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (possibleFunctionName.Content != name)
            { CurrentTokenIndex = startTokenIndex; return null; }

            possibleFunctionName.AnalyzedType = TokenAnalysedType.Statement;

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

            KeywordCall functionCall = new(possibleFunctionName, parameters);

            if (functionCall.Parameters.Length < minParameterCount)
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requires minimum {minParameterCount} parameters but you passed {parameters.Count}", functionCall); }

            if (functionCall.Parameters.Length > maxParameterCount)
            { throw new SyntaxException($"This keyword-call (\"{possibleFunctionName}\") requires maximum {maxParameterCount} parameters but you passed {parameters.Count}", functionCall); }

            return functionCall;
        }

        #endregion

        bool ExpectAttribute(out FunctionDefinition.Attribute attribute)
        {
            int parseStart = CurrentTokenIndex;
            attribute = new();

            if (!ExpectOperator("[", out var t0))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token attributeT))
            { CurrentTokenIndex = parseStart; return false; }

            attributeT.AnalyzedType = TokenAnalysedType.Attribute;

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
            int startTokenIndex = CurrentTokenIndex;

            if (ExpectIdentifier("private", out Token protectionToken))
            {

            }

            TypeInstance possibleType = ExpectType(AllowedType.Implicit);
            if (possibleType == null)
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (!ExpectIdentifier(out Token possibleVariableName))
            { CurrentTokenIndex = startTokenIndex; return null; }

            if (ExpectOperator("("))
            { CurrentTokenIndex = startTokenIndex; return null; }

            possibleVariableName.AnalyzedType = TokenAnalysedType.None;

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

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                value = int.Parse(CurrentToken.Content.Replace("_", ""));

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                value = Convert.ToInt32(CurrentToken.Content, 16);

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_BIN)
            {
                value = Convert.ToInt32(CurrentToken.Content.Replace("_", ""), 2);

                CurrentTokenIndex++;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
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
                if (ExpectIdentifier(out Token modifier, ParameterModifiers))
                {
                    modifier.AnalyzedType = TokenAnalysedType.Keyword;
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
                    modifier.AnalyzedType = TokenAnalysedType.Keyword;
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

        bool ExpectIdentifier(out Token result) => ExpectIdentifier("", out result);
        bool ExpectIdentifier(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            result = CurrentToken;
            CurrentTokenIndex++;

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
            CurrentTokenIndex++;

            return true;
        }
        bool ExpectOperator(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
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
        }

        TypeInstance ExpectType(AllowedType flags)
        {
            if (!ExpectIdentifier(out Token possibleType)) return null;

            if (possibleType == "macro")
            { return null; }

            possibleType.AnalyzedType = TokenAnalysedType.Keyword;

            if (possibleType.Content == "any")
            {
                if ((flags & AllowedType.ExplicitAny) == 0)
                {
                    Errors.Add(new Error($"Type \"{possibleType.Content}\" is not valid in the current context", possibleType));
                    return null;
                }

                if (ExpectOperator(new string[] { "<", "(", "[" }, out Token illegalT))
                { throw new SyntaxException($"This is not allowed", illegalT); }

                return new TypeInstance(possibleType, TypeInstanceKind.Simple);
            }

            if (possibleType.Content == "var")
            {
                if ((flags & AllowedType.Implicit) == 0)
                {
                    Errors.Add(new Error($"implicit type not allowed in the current context", possibleType));
                    return null;
                }

                if (ExpectOperator(new string[] { "<", "(", "[" }, out Token illegalT))
                { throw new SyntaxException($"This is not allowed", illegalT); }

                return new TypeInstance(possibleType, TypeInstanceKind.Simple);
            }

            TypeInstance newType;

            int afterIdentifier = CurrentTokenIndex;

            if (ExpectOperator("<"))
            {
                newType = new TypeInstance(possibleType, TypeInstanceKind.Template);

                while (true)
                {
                    TypeInstance type = ExpectType(AllowedType.FunctionPointer);

                    if (type == null)
                    { throw new SyntaxException($"Expected type as generic parameter", CurrentToken); }

                    newType.GenericTypes.Add(type);

                    if (ExpectOperator(">"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }
            }
            else if (ExpectOperator("("))
            {
                if ((flags & AllowedType.FunctionPointer) == 0)
                {
                    CurrentTokenIndex = afterIdentifier;
                    return new TypeInstance(possibleType, TypeInstanceKind.Simple);
                }

                newType = new TypeInstance(possibleType, TypeInstanceKind.Function);

                while (true)
                {
                    TypeInstance type = ExpectType(AllowedType.FunctionPointer);

                    if (type == null)
                    {
                        CurrentTokenIndex = afterIdentifier;
                        return new TypeInstance(possibleType, TypeInstanceKind.Simple);
                        // throw new SyntaxException($"Expected type as function-pointer parameter type", CurrentToken);
                    }

                    newType.ParameterTypes.Add(type);

                    if (ExpectOperator(")"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }
            }
            else
            {
                newType = new TypeInstance(possibleType, TypeInstanceKind.Simple);
            }

            if (Constants.BuiltinTypes.Contains(possibleType.Content))
            { newType.Identifier.AnalyzedType = TokenAnalysedType.BuiltinType; }

            if (ExpectOperator("[", out _))
            {
                return null;
            }

            return newType;
        }

        #endregion
    }
}
