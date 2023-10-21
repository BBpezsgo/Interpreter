﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LanguageCore.Parser
{
    using LanguageCore.Tokenizing;
    using Statement;

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

        bool ExpectHash([NotNullWhen(true)] out CompileTag? hashStatement)
        {
            hashStatement = null;

            if (!ExpectOperator("#", out var hashT))
            { return false; }

            hashT.AnalyzedType = TokenAnalysedType.Hash;

            if (!ExpectIdentifier(out var hashName))
            { throw new SyntaxException($"Expected identifier after '#' , got {CurrentToken?.TokenType.ToString().ToLower()} \"{CurrentToken?.Content}\"", hashT); }

            hashName.AnalyzedType = TokenAnalysedType.Hash;

            List<Literal> parameters = new();
            int endlessSafe = 50;
            Token? semicolon;
            while (!ExpectOperator(";", out semicolon))
            {
                if (!ExpectLiteral(out var parameter))
                { throw new SyntaxException($"Expected hash literal parameter or ';' , got {CurrentToken?.TokenType.ToString().ToLower()} \"{CurrentToken?.Content}\"", CurrentToken); }

                parameter.ValueToken.AnalyzedType = TokenAnalysedType.HashParameter;
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

            if (CurrentToken == null) throw new SyntaxException($"Expected url after keyword \"using\"", keyword.After());

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
                    throw new SyntaxException("Expected library name after 'using'", keyword);
                }
                else
                {
                    Errors.Add(new Error("Expected library name after 'using'", keyword));
                }
                return false;
            }

            if (!ExpectOperator(";"))
            { throw new SyntaxException("Expected ';' at end of statement (after 'using')", keyword.After()); }

            usingDefinition = new UsingDefinition(keyword, tokens.ToArray());

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
            else if (ExpectStatement(out Statement.Statement? statement))
            {
                SetStatementThings(statement);

                TopLevelStatements.Add(statement);

                if (!ExpectOperator(";", out Token? semicolon))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.GetPosition().After())); }

                statement.Semicolon = semicolon;
            }
            else
            { throw new SyntaxException($"Expected top-level statement, type, macro or function definition. Got a token \"{CurrentToken}\"", CurrentToken); }
        }

        bool ExpectEnumDefinition([NotNullWhen(true)] out EnumDefinition? enumDefinition)
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

            if (!ExpectIdentifier("enum", out Token? keyword))
            { CurrentTokenIndex = parseStart; return false; }

            keyword.AnalyzedType = TokenAnalysedType.Keyword;

            if (!ExpectIdentifier(out Token? identifier))
            { throw new SyntaxException($"Expected identifier token after keyword \"{keyword}\"", keyword.After()); }

            if (!ExpectOperator("{"))
            { throw new SyntaxException($"Expected '{{' after enum identifier", identifier.After()); }

            identifier.AnalyzedType = TokenAnalysedType.Enum;

            List<EnumMemberDefinition> members = new();

            while (!ExpectOperator("}"))
            {
                if (!ExpectIdentifier(out Token? enumMemberIdentifier))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                enumMemberIdentifier.AnalyzedType = TokenAnalysedType.EnumMember;

                Literal? enumMemberValue = null;

                if (ExpectOperator("=", out Token? assignOperator))
                {
                    if (!ExpectLiteral(out enumMemberValue))
                    { throw new SyntaxException($"Expected literal after enum member assignment", assignOperator.After()); }
                }

                members.Add(new EnumMemberDefinition(enumMemberIdentifier, enumMemberValue));

                if (ExpectOperator("}"))
                { break; }

                if (ExpectOperator(","))
                { continue; }

                throw new SyntaxException("Expected ',' or '}'", CurrentToken);
            }

            enumDefinition = new(identifier, attributes.ToArray(), members.ToArray());

            return true;
        }

        bool ExpectOperatorDefinition([NotNullWhen(true)] out FunctionDefinition? function)
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

            if (!ExpectType(AllowedType.None, out TypeInstance? possibleType))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator(OverloadableOperators, out Token? possibleName))
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

                if (!ExpectType(AllowedType.None, out TypeInstance? possibleParameterType))
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            function = new(modifiers, possibleType, possibleName, null)
            {
                Attributes = attributes.ToArray(),
                Parameters = parameters.ToArray(),
            };

            Block? block = null;

            if (!ExpectOperator(";") && !ExpectBlock(out block))
            { throw new NotImplementedException(); }

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
            { throw new SyntaxException($"Expected '<' after keyword \"{keyword}\"", keyword.After()); }

            List<Token> parameters = new();

            Token? rightP;

            var expectParameter = false;
            while (!ExpectOperator(">", out rightP) || expectParameter)
            {
                if (!ExpectIdentifier(out Token? parameter))
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

        bool ExpectMacroDefinition([NotNullWhen(true)] out MacroDefinition? macro)
        {
            int parseStart = CurrentTokenIndex;
            macro = null;

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

            if (!ExpectIdentifier("macro", out Token? macroKeyword))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token? possibleNameT))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectOperator("("))
            { CurrentTokenIndex = parseStart; return false; }

            possibleNameT.AnalyzedType = TokenAnalysedType.FunctionName;

            List<Token> parameters = new();

            Token? bracketRight;

            var expectParameter = false;
            while (!ExpectOperator(")", out bracketRight) || expectParameter)
            {
                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;
                parameters.Add(possibleParameterNameT);

                if (ExpectOperator(")", out bracketRight))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export");

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException($"Expected block", bracketRight?.After() ?? Position.UnknownPosition); }

            macro = new MacroDefinition(modifiers, macroKeyword, possibleNameT, parameters, block);

            return true;
        }

        bool ExpectFunctionDefinition([NotNullWhen(true)] out FunctionDefinition? function)
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

            ExpectTemplateInfo(out TemplateInfo? templateInfo);

            Token[] modifiers = ParseModifiers();

            if (!ExpectType(AllowedType.None, out TypeInstance? possibleType))
            { CurrentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out Token? possibleNameT))
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

                if (!ExpectType(AllowedType.FunctionPointer, out TypeInstance? possibleParameterType))
                { throw new SyntaxException("Expected parameter type", CurrentToken); }

                if (!ExpectIdentifier(out Token? possibleParameterNameT))
                { throw new SyntaxException("Expected a parameter name", CurrentToken); }

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
                parameters.Add(parameterDefinition);

                if (ExpectOperator(")"))
                { break; }

                if (!ExpectOperator(","))
                { throw new SyntaxException("Expected ',' or ')'", CurrentToken); }
                else
                { expectParameter = true; }
            }

            CheckModifiers(modifiers, "export", "macro", "adaptive");

            function = new(modifiers, possibleType, possibleNameT, templateInfo)
            {
                Attributes = attributes.ToArray(),
                Parameters = parameters.ToArray(),
            };

            Block? block = null;

            if (!ExpectOperator(";") && !ExpectBlock(out block))
            { throw new NotImplementedException(); }

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

            possibleNameT.AnalyzedType = TokenAnalysedType.FunctionName;

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

                possibleParameterNameT.AnalyzedType = TokenAnalysedType.VariableName;

                ParameterDefinition parameterDefinition = new(parameterModifiers, possibleParameterType, possibleParameterNameT);
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

            Block? block = null;

            if (!ExpectOperator(";") && !ExpectBlock(out block))
            { throw new NotImplementedException(); }

            function.Block = block;

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

            ExpectTemplateInfo(out TemplateInfo? templateInfo);

            Token[] modifiers = ParseModifiers();

            if (!ExpectIdentifier("class", out Token? keyword))
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token? possibleClassName))
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
                    if (!ExpectOperator(";", out Token? semicolon))
                    { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", field.Identifier.After())); }
                    field.Semicolon = semicolon;
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

            if (!ExpectIdentifier("struct", out Token? keyword))
            { CurrentTokenIndex = startTokenIndex; return false; }

            if (!ExpectIdentifier(out Token? possibleStructName))
            { throw new SyntaxException("Expected struct identifier after keyword 'struct'", keyword); }

            if (!ExpectOperator("{", out var braceletStart))
            { throw new SyntaxException("Expected '{' after struct identifier", possibleStructName); }

            keyword.AnalyzedType = TokenAnalysedType.Keyword;

            List<FieldDefinition> fields = new();
            Dictionary<string, FunctionDefinition> methods = new();

            int endlessSafe = 0;
            Token? braceletEnd;
            while (!ExpectOperator("}", out braceletEnd))
            {
                if (!ExpectField(out FieldDefinition? field))
                { throw new SyntaxException($"Expected field definition", CurrentToken); }

                fields.Add(field);
                if (!ExpectOperator(";", out Token? semicolon))
                { Errors.Add(new Error("Expected ';' at end of statement (after field definition)", field.Identifier.After())); }
                field.Semicolon = semicolon;

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

        bool ExpectLiteral([NotNullWhen(true)] out Literal? statement)
        {
            int savedToken = CurrentTokenIndex;

            string v = CurrentToken?.Content ?? string.Empty;

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

        bool ExpectIndex([NotNullWhen(true)] out IndexCall? statement)
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

            statement = new IndexCall(bracketLeft, expression, bracketRight);
            return true;
        }

        bool ExpectOneValue([NotNullWhen(true)] out StatementWithValue? statementWithValue)
        {
            int savedToken = CurrentTokenIndex;

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
                { throw new SyntaxException("Expected expression after '('", braceletT); }

                if (expression is OperatorCall operation)
                { operation.InsideBracelet = true; }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Unbalanced '('", braceletT); }

                statementWithValue = expression;
            }
            else if (ExpectIdentifier("new", out Token? newIdentifier))
            {
                newIdentifier.AnalyzedType = TokenAnalysedType.Keyword;

                if (!ExpectType(AllowedType.None, out TypeInstance? instanceTypeName))
                { throw new SyntaxException("Expected instance constructor after keyword 'new'", newIdentifier); }

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
                        { throw new SyntaxException("Expected ',' to separate parameters", parameter); }
                        else
                        { expectParameter = true; }

                        endlessSafe++;
                        if (endlessSafe > 100)
                        { throw new EndlessLoopException(); }
                    }

                    ConstructorCall newStructStatement = new(newIdentifier, instanceTypeName, bracketLeft, parameters, bracketRight);

                    statementWithValue = newStructStatement;
                }
                else
                {
                    statementWithValue = new NewInstance(newIdentifier, instanceTypeName);
                }
            }
            else if (ExpectIdentifier(out Token? variableName))
            {
                if (ExpectOperator("("))
                {
                    CurrentTokenIndex = savedToken;
                    ExpectFunctionCall(out FunctionCall? functionCall);
                    statementWithValue = functionCall;
                }
                else
                {
                    Identifier variableNameStatement = new(variableName);

                    if (variableName.Content == "this")
                    { variableName.AnalyzedType = TokenAnalysedType.Keyword; }
                    else
                    { variableName.AnalyzedType = TokenAnalysedType.VariableName; }

                    statementWithValue = variableNameStatement;
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

            if (statementWithValue == null)
            { return false; }

            while (true)
            {
                if (ExpectOperator(".", out var tokenDot))
                {
                    if (ExpectMethodCall(false, statementWithValue, out var methodCall))
                    {
                        statementWithValue = methodCall;
                    }
                    else
                    {
                        if (!ExpectIdentifier(out Token? fieldName))
                        { throw new SyntaxException("Expected field or method", tokenDot); }

                        statementWithValue = new Field(statementWithValue, fieldName);
                    }

                    continue;
                }

                if (ExpectIndex(out var statementIndex))
                {
                    statementIndex.PrevStatement = statementWithValue;
                    statementWithValue = statementIndex;

                    continue;
                }

                break;
            }

            {
                if (ExpectIdentifier("as", out Token? keyword))
                {
                    if (!ExpectType(AllowedType.None, out TypeInstance? type))
                    { throw new SyntaxException($"Expected type after 'as' keyword", keyword.After()); }

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

            if (!ExpectOneValue(out StatementWithValue? prevStatement))
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

            if (!ExpectOneValue(out StatementWithValue? prevStatement))
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

                if (!ExpectOperator(";", out Token? semicolon))
                { Errors.Add(new Error($"Expected ';' at end of statement (after {statement.GetType().Name})", statement.GetPosition().After())); }
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

            possibleVariableName.AnalyzedType = TokenAnalysedType.VariableName;

            StatementWithValue? initialValue = null;

            if (ExpectOperator("=", out Token? eqOperatorToken))
            {
                if (!ExpectExpression(out initialValue))
                { throw new SyntaxException("Expected initial value after '=' in variable declaration", eqOperatorToken); }
            }
            else
            {
                if (possibleType.Identifier.Content == "var")
                { throw new SyntaxException("Initial value for variable declaration with implicit type is required", possibleType.Identifier); }
            }

            variableDeclaration = new VariableDeclaration(modifiers.ToArray(), possibleType, possibleVariableName, initialValue);
            return true;
        }

        bool ExpectForStatement([NotNullWhen(true)] out ForLoop? forLoop)
        {
            if (!ExpectIdentifier("for", out Token? tokenFor))
            { forLoop = null; return false; }

            tokenFor.AnalyzedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
            { throw new SyntaxException("Expected '(' after \"for\" statement", tokenFor.After()); }

            if (!ExpectVariableDeclaration(out VariableDeclaration? variableDeclaration))
            { throw new SyntaxException("Expected variable declaration after \"for\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(";", out Token? semicolon1))
            { throw new SyntaxException("Expected ';' after \"for\" variable declaration", variableDeclaration.GetPosition().After()); }
            variableDeclaration.Semicolon = semicolon1;

            if (!ExpectExpression(out StatementWithValue? condition))
            { throw new SyntaxException("Expected condition after \"for\" variable declaration", tokenParenthesesOpen); }

            if (!ExpectOperator(";", out Token? semicolon2))
            { throw new SyntaxException($"Expected ';' after \"for\" condition, got {CurrentToken}", variableDeclaration.GetPosition().After()); }
            condition.Semicolon = semicolon2;

            if (!ExpectAnySetter(out AnyAssignment? expression))
            { throw new SyntaxException($"Expected setter after \"for\" condition, got {CurrentToken}", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token? tokenParenthesesClosed))
            { throw new SyntaxException($"Expected ')' after \"for\" condition, got {CurrentToken}", condition.GetPosition().After()); }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException($"Expected block, got {CurrentToken}", tokenParenthesesClosed.After()); }

            forLoop = new ForLoop(tokenFor, variableDeclaration, condition, expression, block);
            return true;
        }

        bool ExpectWhileStatement([NotNullWhen(true)] out WhileLoop? whileLoop)
        {
            if (!ExpectIdentifier("while", out Token? tokenWhile))
            { whileLoop = null; return false; }

            tokenWhile.AnalyzedType = TokenAnalysedType.Statement;

            if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
            { throw new SyntaxException("Expected '(' after \"while\" statement", tokenWhile); }

            if (!ExpectExpression(out StatementWithValue? condition))
            { throw new SyntaxException("Expected condition after \"while\" statement", tokenParenthesesOpen); }

            if (!ExpectOperator(")", out Token? tokenParenthesesClose))
            { throw new SyntaxException("Expected ')' after \"while\" condition", condition); }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException("Expected block", tokenParenthesesClose.After()); }

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

            tokenIf.AnalyzedType = TokenAnalysedType.Statement;

            StatementWithValue? condition = null;

            if (needParameters)
            {
                if (!ExpectOperator("(", out Token? tokenParenthesesOpen))
                { throw new SyntaxException("Expected '(' after \"" + ifSegmentName + "\" statement", tokenIf); }

                if (!ExpectExpression(out condition))
                { throw new SyntaxException("Expected condition after \"" + ifSegmentName + "\" statement", tokenParenthesesOpen); }

                if (!ExpectOperator(")"))
                { throw new SyntaxException("Expected ')' after \"" + ifSegmentName + "\" condition", condition); }
            }

            if (!ExpectBlock(out Block? block))
            { throw new SyntaxException("Expected block", tokenIf.After()); }

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

            possibleFunctionName.AnalyzedType = TokenAnalysedType.FunctionName;

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

            modifier.AnalyzedType = TokenAnalysedType.Keyword;

            if (!ExpectOneValue(out StatementWithValue? value))
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.After()); }

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

            modifier.AnalyzedType = TokenAnalysedType.Keyword;

            if (!ExpectOneValue(out StatementWithValue? value))
            { throw new SyntaxException($"Expected one value after modifier \"{modifier}\"", modifier.After()); }

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
            if (Constants.Operators.Precedencies.TryGetValue(@operator, out int precedence))
            { return precedence; }
            throw new InternalException($"Precedence for operator \"{@operator}\" not found");
        }

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

            possibleFunctionName.AnalyzedType = TokenAnalysedType.BuiltinType;

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
                { throw new SyntaxException("Expected ',' to separate parameters", parameter); }
                else
                { expectParameter = true; }

                endlessSafe++;
                if (endlessSafe > 100)
                { throw new EndlessLoopException(); }
            };

            functionCall = new FunctionCall(null, possibleFunctionName, bracketLeft, parameters, bracketRight);
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

            possibleFunctionName.AnalyzedType = TokenAnalysedType.Statement;

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

            attributeT.AnalyzedType = TokenAnalysedType.Attribute;

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

            possibleVariableName.AnalyzedType = TokenAnalysedType.None;

            field = new(possibleVariableName, possibleType, protectionToken);

            return true;
        }

        void ExpectOneLiteral(out object? value)
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
                if (ExpectIdentifier(out Token? modifier, ParameterModifiers))
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
                if (ExpectIdentifier(out Token? modifier, Modifiers))
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

        bool ExpectIdentifier([NotNullWhen(true)] out Token? result) => ExpectIdentifier("", out result);
        bool ExpectIdentifier(string name, [NotNullWhen(true)] out Token? result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
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
            if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
            if (name.Contains(CurrentToken.Content) == false) return false;

            result = CurrentToken;
            CurrentTokenIndex++;

            return true;
        }
        bool ExpectOperator(string name, [NotNullWhen(true)] out Token? result)
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
            StackArrayWithLength = 0b_0000_1000,
            StackArrayWithoutLength = 0b_0001_0000,
        }

        bool ExpectType(AllowedType flags, [NotNullWhen(true)] out TypeInstance? type)
        {
            type = default;

            if (!ExpectIdentifier(out Token? possibleType)) return false;

            if (possibleType == "macro")
            { return false; }

            possibleType.AnalyzedType = TokenAnalysedType.Keyword;

            if (possibleType.Content == "any")
            {
                if ((flags & AllowedType.ExplicitAny) == 0)
                {
                    Errors.Add(new Error($"Type \"{possibleType.Content}\" is not valid in the current context", possibleType));
                    return false;
                }

                if (ExpectOperator(new string[] { "<", "(", "[" }, out Token? illegalT))
                { throw new SyntaxException($"This is not allowed", illegalT); }

                type = new TypeInstance(possibleType, TypeInstanceKind.Simple);
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

                type = new TypeInstance(possibleType, TypeInstanceKind.Simple);
                return true;
            }

            TypeInstance newType;

            int afterIdentifier = CurrentTokenIndex;

            if (ExpectOperator("<"))
            {
                newType = new TypeInstance(possibleType, TypeInstanceKind.Template);

                while (true)
                {
                    if (!ExpectType(AllowedType.FunctionPointer, out var typeParameter))
                    { return false; }

                    newType.GenericTypes.Add(typeParameter);

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
                    type = new TypeInstance(possibleType, TypeInstanceKind.Simple);
                    return true;
                }

                newType = new TypeInstance(possibleType, TypeInstanceKind.Function);

                while (true)
                {
                    if (!ExpectType(AllowedType.FunctionPointer, out var subtype))
                    {
                        CurrentTokenIndex = afterIdentifier;
                        type = new TypeInstance(possibleType, TypeInstanceKind.Simple);
                        return true;
                        // throw new SyntaxException($"Expected type as function-pointer parameter type", CurrentToken);
                    }

                    newType.ParameterTypes.Add(subtype);

                    if (ExpectOperator(")"))
                    { break; }

                    if (ExpectOperator(","))
                    { continue; }
                }
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

                newType = new TypeInstance(possibleType, TypeInstanceKind.StackArray, sizeValue);
            }
            else
            {
                newType = new TypeInstance(possibleType, TypeInstanceKind.Simple);
            }

            if (Constants.BuiltinTypes.Contains(possibleType.Content))
            { newType.Identifier.AnalyzedType = TokenAnalysedType.BuiltinType; }

            if (ExpectOperator("["))
            { return false; }

            type = newType;
            return true;
        }

        #endregion
    }
}
