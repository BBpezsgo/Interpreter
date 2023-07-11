using ProgrammingLanguage.BBCode;
using ProgrammingLanguage.Core;
using ProgrammingLanguage.Errors;

using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.Tester.Parser
{
    using Statements;

    using System.Reflection;

    public struct ParserSettings
    {
        public bool PrintInfo;

        public static ParserSettings Default => new()
        {
            PrintInfo = false,
        };
    }

    public struct ParserResult
    {
        public TestDefinition[] TestDefinitions;
        public SimpleSegment[] Disabled;

        public ParserResult(TestDefinition[] testDefinitions, SimpleSegment[] disabled)
        {
            TestDefinitions = testDefinitions;
            Disabled = disabled;
        }

        /*
        public void SetFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            { throw new ArgumentException($"'{nameof(path)}' cannot be null or empty.", nameof(path)); }

            for (int i = 0; i < this.Functions.Count; i++)
            { this.Functions[i].FilePath = path; }
            for (int j = 0; j < this.Namespaces.Count; j++)
            { this.Namespaces[j].FilePath = path; }
            for (int i = 0; i < this.GlobalVariables.Count; i++)
            { this.GlobalVariables[i].FilePath = path; }
            for (int i = 0; i < this.Structs.Count; i++)
            { this.Structs.ElementAt(i).Value.FilePath = path; }
            for (int i = 0; i < this.Hashes.Length; i++)
            { this.Hashes[i].FilePath = path; }
            StatementFinder.GetAllStatement(this, statement =>
            {
                if (statement is not IDefinition def) return false;
                def.FilePath = path;
                return false;
            });
        }

        public void CheckFilePaths(System.Action<string> NotSetCallback)
        {
            for (int i = 0; i < this.Functions.Count; i++)
            {
                if (string.IsNullOrEmpty(this.Functions[i].FilePath))
                { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"FunctionDefinition.FilePath {this.Functions[i]} : {this.Functions[i].FilePath}"); }
            }
            for (int i = 0; i < this.Namespaces.Count; i++)
            {
                if (string.IsNullOrEmpty(this.Namespaces[i].FilePath))
                { NotSetCallback?.Invoke($"Namespace.FilePath {this.Namespaces[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"Namespace.FilePath {this.Namespaces[i]} : {this.Namespaces[i].FilePath}"); }
            }
            for (int i = 0; i < this.GlobalVariables.Count; i++)
            {
                if (string.IsNullOrEmpty(this.GlobalVariables[i].FilePath))
                { NotSetCallback?.Invoke($"GlobalVariable.FilePath {this.GlobalVariables[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"GlobalVariable.FilePath {this.GlobalVariables[i]} : {this.GlobalVariables[i].FilePath}"); }
            }
            for (int i = 0; i < this.Structs.Count; i++)
            {
                if (string.IsNullOrEmpty(this.Structs.ElementAt(i).Value.FilePath))
                { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs.ElementAt(i).Value} is null"); }
                else
                { NotSetCallback?.Invoke($"StructDefinition.FilePath {this.Structs.ElementAt(i).Value} : {this.Structs.ElementAt(i).Value.FilePath}"); }
            }
            for (int i = 0; i < this.Hashes.Length; i++)
            {
                if (string.IsNullOrEmpty(this.Hashes[i].FilePath))
                { NotSetCallback?.Invoke($"Hash.FilePath {this.Hashes[i]} is null"); }
                else
                { NotSetCallback?.Invoke($"Hash.FilePath {this.Hashes[i]} : {this.Hashes[i].FilePath}"); }
            }
            StatementFinder.GetAllStatement(this, statement =>
            {
                if (statement is not IDefinition def) return false;
                if (string.IsNullOrEmpty(def.FilePath))
                { NotSetCallback?.Invoke($"IDefinition.FilePath {def} is null"); }
                else
                { NotSetCallback?.Invoke($"IDefinition.FilePath {def} : {def.FilePath}"); }
                return false;
            });
        }
        */
    }

    public class Attribute
    {
        internal Token LeftBracket;
        internal Token RightBracket;
        internal Token Name;
        internal Token[] Parameters = System.Array.Empty<Token>();
    }

    public class TestDefinition
    {
        public Attribute[] Attributes = System.Array.Empty<Attribute>();
        public Token RightBracket;
        public Token LeftBracket;
        public Token Keyword;
        public Token Name;

        public Attribute GetAttribute(string name)
        {
            foreach (var attr in Attributes)
            {
                if (attr.Name.Content.ToLower() == name.ToLower())
                { return attr; }
            }
            return null;
        }

        public bool TryGetAttribute(string name, out Attribute attribute)
        {
            attribute = GetAttribute(name);
            return attribute != null;
        }
    }

    public class SimpleSegment
    {
        internal Token Keyword;
        internal Token[] Parameters;
    }

    public class Parser
    {
        int currentTokenIndex;
        readonly List<Token> tokens = new();
        public Token[] Tokens => tokens.ToArray();

        Token CurrentToken => (currentTokenIndex >= 0 && currentTokenIndex < tokens.Count) ? tokens[currentTokenIndex] : null;

        List<Warning> Warnings;
        public readonly List<Error> Errors = new();

        // === Result ===
        readonly List<TestDefinition> testDefinitions = new();
        readonly List<SimpleSegment> disableDefinitions = new();
        // === ===

        public Parser()
        { }

        public ParserResult Parse(Token[] _tokens, List<Warning> warnings)
        {
            Warnings = warnings;
            tokens.Clear();
            tokens.AddRange(_tokens);

            currentTokenIndex = 0;

            int endlessSafe = 0;
            while (CurrentToken != null)
            {
                if (ParseTestSegment(out var testDefinition))
                { testDefinitions.Add(testDefinition); }
                else if (ParseSimpleSegment("disable", out var disableDefinition))
                { disableDefinitions.Add(disableDefinition); }
                else
                { throw new SyntaxException("Expected test definition", CurrentToken); }

                endlessSafe++;
                if (endlessSafe > 500) { throw new EndlessLoopException(); }
            }

            return new ParserResult(testDefinitions.ToArray(), disableDefinitions.ToArray());
        }

        public static ParserResult Parse(string code, List<Warning> warnings, System.Action<string, Output.LogType> printCallback = null)
        {
            var tokenizer = new BBCode.Tokenizer(TokenizerSettings.Default);
            var tokens = tokenizer.Parse(code, warnings);
            tokens = tokens.RemoveTokens(TokenType.COMMENT, TokenType.COMMENT_MULTILINE);

            System.DateTime parseStarted = System.DateTime.Now;
            if (printCallback != null)
            { printCallback?.Invoke("Parsing ...", Output.LogType.Debug); }

            Parser parser = new();
            var result = parser.Parse(tokens, warnings);

            if (parser.Errors.Count > 0)
            {
                throw new Exception("Failed to parse", parser.Errors[0].ToException());
            }

            if (printCallback != null)
            { printCallback?.Invoke($"Parsed in {(System.DateTime.Now - parseStarted).TotalMilliseconds} ms", Output.LogType.Debug); }

            return result;
        }

        bool ExpectAttribute(out Attribute result)
        {
            var parseStart = currentTokenIndex;
            result = null;

            if (!ExpectOperator("[", out var leftBracket))
            { currentTokenIndex = parseStart; return false; }

            if (!ExpectIdentifier(out var name))
            { currentTokenIndex = parseStart; return false; }

            List<Token> parameters = new();

            if (ExpectOperator("(", out var t0))
            {
                int endlessSafe = 50;
                while (true)
                {
                    if (ExpectLiteral(out var parameterValue))
                    {
                        parameters.Add(parameterValue);
                        if (ExpectOperator(",") != null)
                        { }
                        else if (ExpectOperator(")") != null)
                        { break; }
                        else
                        { throw new SyntaxException("Expected ',' or ')' after attribute parameter", CurrentToken ?? parameterValue); }
                    }
                    else if (ExpectOperator(",") != null)
                    { }
                    else if (ExpectOperator(")") != null)
                    { break; }
                    else
                    {
                        if (CurrentToken == null)
                        { throw new SyntaxException("Unbalanced '('", t0); }
                        throw new SyntaxException($"Unexpected token '{CurrentToken}'", CurrentToken);
                    }

                    endlessSafe--;
                    if (endlessSafe <= 0) throw new EndlessLoopException();
                }
            }

            if (!ExpectOperator("]", out var rightBracket))
            { throw new SyntaxException($"Unbalanced '['", leftBracket); }

            result = new Attribute()
            {
                LeftBracket = leftBracket,
                RightBracket = rightBracket,
                Name = name,
                Parameters = parameters.ToArray(),
            };
            return true;
        }

        bool ParseTestSegment(out TestDefinition result)
        {
            int parseStart = currentTokenIndex;
            result = null;

            if (!ExpectIdentifier("test", out var keyword))
            { currentTokenIndex = parseStart; return false; }

            Token name;
            if (!ExpectLiteral(out name))
            {
                if (!ExpectIdentifier(out name))
                { throw new SyntaxException("Expected test name after keyword 'test'", CurrentToken ?? keyword); }
            }

            if (!ExpectOperator("{", out var leftBracket))
            { throw new SyntaxException("Expected '{' after test definition name", CurrentToken ?? keyword); }

            List<Attribute> attributes = new();
            int endlessSafe = 10;
            while (ExpectAttribute(out var attr))
            {
                attributes.Add(attr);
                endlessSafe--;
                if (endlessSafe <= 0) throw new EndlessLoopException();
            }

            if (!ExpectOperator("}", out var rightBracket))
            { throw new SyntaxException("Unbalanced '{'", leftBracket); }

            result = new TestDefinition()
            {
                LeftBracket = leftBracket,
                RightBracket = rightBracket,
                Keyword = keyword,
                Attributes = attributes.ToArray(),
                Name = name,
            };

            return true;
        }

        bool ParseSimpleSegment(string keywordName, out SimpleSegment result)
        {
            int parseStart = currentTokenIndex;
            result = null;

            if (!ExpectIdentifier(keywordName, out var keyword))
            { currentTokenIndex = parseStart; return false; }

            List<Token> parameters = new();
            int endlessSafe = 50;
            while (true)
            {
                if (ExpectOperator(";") != null) break;

                Token parameter;
                if (!ExpectLiteral(out parameter))
                {
                    if (!ExpectIdentifier(out parameter))
                    { throw new SyntaxException("Expected parameter or ';'", CurrentToken ?? keyword); }
                }
                parameters.Add(parameter);

                endlessSafe--;
                if (endlessSafe <= 0) throw new EndlessLoopException();
            }

            result = new SimpleSegment()
            {
                Keyword = keyword,
                Parameters = parameters.ToArray(),
            };

            return true;
        }

        bool ExpectLiteral(out Token value)
        {
            value = null;
            if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                value = CurrentToken;
                currentTokenIndex++;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                value = CurrentToken;
                currentTokenIndex++;
                return true;
            }
            else if (CurrentToken != null && CurrentToken.TokenType == TokenType.LITERAL_STRING)
            {
                value = CurrentToken;
                currentTokenIndex++;
                return true;
            }
            return false;
        }

        #region Basic parsing

        bool ExpectIdentifier(out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
            if ("".Length > 0 && CurrentToken.Content != "") return false;

            Token returnToken = CurrentToken;
            result = returnToken;
            currentTokenIndex++;

            return true;
        }
        bool ExpectIdentifier(string name, out Token result)
        {
            result = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.IDENTIFIER) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            Token returnToken = CurrentToken;
            result = returnToken;
            currentTokenIndex++;

            return true;
        }

        Token ExpectOperator(string name)
        {
            if (CurrentToken == null) return null;
            if (CurrentToken.TokenType != TokenType.OPERATOR) return null;
            if (name.Length > 0 && CurrentToken.Content != name) return null;

            Token returnToken = CurrentToken;
            currentTokenIndex++;

            return returnToken;
        }
        bool ExpectOperator(string name, out Token outToken)
        {
            outToken = null;
            if (CurrentToken == null) return false;
            if (CurrentToken.TokenType != TokenType.OPERATOR) return false;
            if (name.Length > 0 && CurrentToken.Content != name) return false;

            Token returnToken = CurrentToken;
            outToken = returnToken;
            currentTokenIndex++;

            return true;
        }

        #endregion
    }
}