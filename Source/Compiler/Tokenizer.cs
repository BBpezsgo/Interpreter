using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Core;
    using IngameCoding.Errors;
    using IngameCoding.Tokenizer;

    public class TokenAnalysis
    {
        public TokenSubtype Subtype = TokenSubtype.None;
        public TokenSubSubtype SubSubtype = TokenSubSubtype.None;

        public bool ParserReached;
        public bool CompilerReached;

        public RefBase Reference;

        string ReachedUnit => CompilerReached ? "compiled" : (ParserReached ? "parsed" : "tokenized");

        public override string ToString() => $"TokenAnalysis {{ {ReachedUnit} {Subtype} {SubSubtype} {(Reference == null ? "null" : Reference.ToString())} }}";

        public abstract class RefBase
        {
            public override abstract string ToString();
        }

        public abstract class RefLocal : RefBase
        {

        }
        public abstract class Ref : RefBase
        {
            public string FilePath;

            public Ref(string filePath)
            {
                FilePath = filePath;
            }
        }

        public class RefVariable : RefLocal
        {
            public Statement_NewVariable Declaration;
            public bool IsGlobal;
            public CompiledType Type;

            public RefVariable(Statement_NewVariable declaration, bool isGlobal, CompiledType type)
            {
                Declaration = declaration;
                IsGlobal = isGlobal;
                Type = type;
            }

            public override string ToString() => $"Ref Variable";
        }

        public class RefParameter : RefLocal
        {
            public string Type;

            public RefParameter(string type)
            {
                Type = type;
            }

            public override string ToString() => $"Ref Parameter";
        }

        public class RefField : Ref
        {
            public string Type;
            public string Name => (NameToken != null) ? NameToken.text : fieldName;
            public Token NameToken;
            public string StructName;

            readonly string fieldName;

            public RefField(string type, Token name, string structName, string filePath) : base(filePath)
            {
                Type = type;
                NameToken = name;
                StructName = structName;
            }

            public override string ToString() => $"Ref Field";
        }

        public class RefFunction : Ref
        {
            public CompiledFunction Definition;

            public RefFunction(CompiledFunction definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Function";
        }

        public class RefStruct : Ref
        {
            public CompiledStruct Definition;

            public RefStruct(CompiledStruct definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Struct";
        }

        public class RefClass : Ref
        {
            public CompiledClass Definition;

            public RefClass(CompiledClass definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Class";
        }

        public class RefBuiltinFunction : Ref
        {
            public readonly string Name;
            public readonly string ReturnType;
            public readonly string[] ParameterTypes;
            public readonly string[] ParameterNames;

            public RefBuiltinFunction(string name, string returnType, string[] parameterTypes, string[] parameterNames) : base(null)
            {
                this.Name = name;
                this.ReturnType = returnType;
                this.ParameterTypes = parameterTypes;
                this.ParameterNames = parameterNames;
            }

            public override string ToString() => $"Ref BuiltinFunction";
        }

        public class RefBuiltinMethod : Ref
        {
            public readonly string Name;
            public readonly string ReturnType;
            public readonly string PrevType;
            public readonly string[] ParameterTypes;
            public readonly string[] ParameterNames;

            public RefBuiltinMethod(string name, string returnType, string prevType, string[] parameterTypes, string[] parameterNames) : base(null)
            {
                this.Name = name;
                this.ReturnType = returnType;
                this.PrevType = prevType;
                this.ParameterTypes = parameterTypes;
                this.ParameterNames = parameterNames;
            }

            public override string ToString() => $"Ref BuiltinMethod";
        }
    }

    public enum TokenType
    {
        WHITESPACE,
        LINEBREAK,

        IDENTIFIER,

        LITERAL_NUMBER,
        LITERAL_HEX,
        LITERAL_BIN,
        LITERAL_STRING,
        LITERAL_FLOAT,

        UNICODE_CHARACTER,

        OPERATOR,
        STRING_ESCAPE_SEQUENCE,

        POTENTIAL_FLOAT,
        POTENTIAL_COMMENT,
        POTENTIAL_END_MULTILINE_COMMENT,

        COMMENT,
        COMMENT_MULTILINE,
    }

    public struct TokenizerSettings
    {
        /// <summary> The Tokenizer will produce <see cref="TokenType.WHITESPACE"/> </summary>
        public bool TokenizeWhitespaces;
        /// <summary> The Tokenizer will produce <see cref="TokenType.LINEBREAK"/> </summary>
        public bool DistinguishBetweenSpacesAndNewlines;
        public bool JoinLinebreaks;

        public static TokenizerSettings Default => new()
        {
            TokenizeWhitespaces = false,
            DistinguishBetweenSpacesAndNewlines = false,
            JoinLinebreaks = true,
        };
    }

    public class Token : BaseToken
    {
        public TokenType type = TokenType.WHITESPACE;
        public string text = "";
        public TokenAnalysis Analysis;

        public Token()
        {
            Analysis = new()
            {
                Subtype = TokenSubtype.None,
                SubSubtype = TokenSubSubtype.None,
            };
        }

        public Token Clone() => new()
        {
            Position = Position,
            AbsolutePosition = AbsolutePosition,

            text = text,
            type = type,
            Analysis = new TokenAnalysis()
            {
                Subtype = Analysis.Subtype,
            },
        };
        public override string ToString() => text;

        internal string ToFullString()
        {
            return $"Token:{type} {{ \"{text}\" {Position} {(Analysis == null ? "No analysis" : Analysis.ToString())} }}";
        }
    }

    public readonly struct SimpleToken
    {
        public readonly string Text;
        public readonly Range<SinglePosition> Position;
        public readonly Range<int> AbsolutePosition;

        public SimpleToken(string text, Range<SinglePosition> position, Range<int> absolutePosition)
        {
            Text = text;
            Position = position;
            AbsolutePosition = absolutePosition;
        }

        public override string ToString() => Text;
        public Position GetPosition() => new(Position.Start.Line, Position.Start.Character, AbsolutePosition);
    }

    /// <summary>
    /// The tokenizer for the BBCode language
    /// </summary>
    public class Tokenizer
    {
        readonly Token CurrentToken;
        int CurrentColumn;
        int CurrentLine;

        readonly List<Token> tokens;
        readonly List<Token> tokensWithComments;

        readonly TokenizerSettings settings;
        readonly Action<string, Output.LogType> printCallback;

        void Print(string message, Output.LogType type) => printCallback?.Invoke(message, type);

        static readonly char[] bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
        static readonly char[] banned = new char[] { '\r', '\u200B' };
        static readonly char[] operators = new char[] { '+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&' };
        static readonly string[] doubleOperators = new string[] { "++", "--", "<<", ">>" };
        static readonly char[] simpleOperators = new char[] { ';', ',', '#' };

        /// <param name="settings">
        /// Tokenizer settings<br/>
        /// Use <see cref="TokenizerSettings.Default"/> if you don't know
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        public Tokenizer(TokenizerSettings settings, Action<string, Output.LogType> printCallback = null)
        {
            CurrentToken = new()
            {
                Position = new Range<SinglePosition>()
                {
                    Start = new SinglePosition(0, 0),
                    End = new SinglePosition(0, 0),
                },
                AbsolutePosition = new Range<int>(0, 0),
            };
            CurrentColumn = 0;
            CurrentLine = 1;

            tokens = new();
            tokensWithComments = new();

            this.settings = settings;
            this.printCallback = printCallback;
        }

        Position GetCurrentPosition(int OffsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(OffsetTotal, OffsetTotal + 1));

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="Errors.TokenizerException"/>
        public Token[] Parse(string sourceCode) => Parse(sourceCode, null, null, out _, out _);
        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="Errors.TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings) => Parse(sourceCode, warnings, null, out _, out _);
        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="Errors.TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings, string filePath) => Parse(sourceCode, warnings, filePath, out _, out _);
        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="Errors.TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings, string filePath, out Token[] tokensWithComments) => Parse(sourceCode, warnings, filePath, out tokensWithComments, out _);

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="Errors.TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings, string filePath, out Token[] tokensWithComments, out SimpleToken[] unicodeCharacters)
        {
            DateTime tokenizingStarted = DateTime.Now;
            Print("Tokenizing ...", Output.LogType.Debug);

            string savedUnicode = null;
            List<SimpleToken> _unicodeCharacters = new();

            for (int OffsetTotal = 0; OffsetTotal < sourceCode.Length; OffsetTotal++)
            {
                char currChar = sourceCode[OffsetTotal];

                CurrentColumn++;
                if (currChar == '\n')
                {
                    CurrentColumn = 1;
                    CurrentLine++;
                }

                if (banned.Contains(currChar)) continue;

                if (currChar == '\n' && CurrentToken.type == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(OffsetTotal);
                    CurrentToken.text = "";
                    CurrentToken.type = TokenType.COMMENT_MULTILINE;
                }

                if (currChar > byte.MaxValue && warnings != null)
                {
                    RefreshTokenPosition(OffsetTotal);
                    if (CurrentToken.type == TokenType.LITERAL_STRING)
                    {
                        warnings.Add(new Warning($"Don't use special characters. Use \\u{(((int)currChar).ToString("X").PadLeft(4, '0'))}", CurrentToken.After(), filePath));
                    }
                    else
                    {
                        warnings.Add(new Warning($"Don't use special characters.", CurrentToken.After(), filePath));
                    }
                }

                if (CurrentToken.type == TokenType.UNICODE_CHARACTER)
                {
                    if (savedUnicode == null) throw new InternalException($"savedUnicode is null");
                    if (savedUnicode.Length == 4)
                    {
                        string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(savedUnicode, 16));
                        _unicodeCharacters.Add(new SimpleToken(
                            unicodeChar,
                            new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                            new Range<int>(OffsetTotal - 6, OffsetTotal)
                        ));
                        CurrentToken.text += unicodeChar;
                        CurrentToken.type = TokenType.LITERAL_STRING;
                        savedUnicode = null;
                    }
                    else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                    {
                        throw new TokenizerException("Invalid hex digit in unicode character: '" + currChar + "' inside literal string", GetCurrentPosition(OffsetTotal));
                    }
                    else
                    {
                        savedUnicode += currChar;
                        continue;
                    }
                }

                if (CurrentToken.type == TokenType.STRING_ESCAPE_SEQUENCE)
                {
                    if (currChar == 'u')
                    {
                        CurrentToken.type = TokenType.UNICODE_CHARACTER;
                        savedUnicode = "";
                        continue;
                    }
                    CurrentToken.text += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        _ => throw new TokenizerException("Unknown escape sequence: \\" + currChar + " in string.", GetCurrentPosition(OffsetTotal)),
                    };
                    CurrentToken.type = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (CurrentToken.type == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
                {
                    CurrentToken.type = TokenType.OPERATOR;
                    if (currChar == '=')
                    {
                        CurrentToken.text += currChar;
                    }
                    EndToken(OffsetTotal);
                    continue;
                }
                else if (CurrentToken.type == TokenType.COMMENT && currChar != '\n')
                {
                    CurrentToken.text += currChar;
                    continue;
                }
                else if (CurrentToken.type == TokenType.LITERAL_STRING && currChar != '"')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.type = TokenType.STRING_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.text += currChar;
                    continue;
                }

                if (CurrentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
                {
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    EndToken(OffsetTotal);
                    continue;
                }

                if (CurrentToken.type == TokenType.COMMENT_MULTILINE || CurrentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
                {
                    CurrentToken.text += currChar;
                    if (CurrentToken.type == TokenType.COMMENT_MULTILINE && currChar == '*')
                    {
                        CurrentToken.type = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                    }
                    else
                    {
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    continue;
                }

                if (CurrentToken.type == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
                {
                    CurrentToken.type = TokenType.OPERATOR;
                    EndToken(OffsetTotal);
                }

                if (currChar == 'f' && (CurrentToken.type == TokenType.LITERAL_NUMBER || CurrentToken.type == TokenType.LITERAL_FLOAT))
                {
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.LITERAL_FLOAT;
                    EndToken(OffsetTotal);
                }
                else if (currChar == 'e' && (CurrentToken.type == TokenType.LITERAL_NUMBER || CurrentToken.type == TokenType.LITERAL_FLOAT))
                {
                    if (CurrentToken.text.Contains(currChar))
                    { throw new TokenizerException($"Invalid float literal format", CurrentToken); }
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.LITERAL_FLOAT;
                }
                else if (currChar == 'x' && CurrentToken.type == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.text.EndsWith('0'))
                    { throw new TokenizerException($"Invalid hex number literal format", CurrentToken); }
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.LITERAL_HEX;
                }
                else if (currChar == 'b' && CurrentToken.type == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.text.EndsWith('0'))
                    { throw new TokenizerException($"Invalid bin number literal format", CurrentToken); }
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.LITERAL_BIN;
                }
                else if ((new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', }).Contains(currChar))
                {
                    if (CurrentToken.type == TokenType.WHITESPACE)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.LITERAL_NUMBER;
                    }
                    else if (CurrentToken.type == TokenType.POTENTIAL_FLOAT)
                    { CurrentToken.type = TokenType.LITERAL_FLOAT; }
                    else if (CurrentToken.type == TokenType.OPERATOR)
                    {
                        if (CurrentToken.text != "-")
                        {
                            EndToken(OffsetTotal);
                        }
                        else
                        {
                            CurrentToken.type = TokenType.LITERAL_NUMBER;
                        }
                    }

                    if (CurrentToken.type == TokenType.LITERAL_BIN)
                    {
                        if (currChar != '0' && currChar != '1')
                        {
                            RefreshTokenPosition(OffsetTotal);
                            throw new TokenizerException($"Invalid bin digit \'{currChar}\'", CurrentToken.After());
                        }
                    }

                    CurrentToken.text += currChar;
                }
                else if (CurrentToken.type == TokenType.LITERAL_BIN && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.text += currChar;
                }
                else if (CurrentToken.type == TokenType.LITERAL_NUMBER && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.text += currChar;
                }
                else if (CurrentToken.type == TokenType.LITERAL_FLOAT && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.text += currChar;
                }
                else if (currChar == '.')
                {
                    if (CurrentToken.type == TokenType.WHITESPACE)
                    {
                        CurrentToken.type = TokenType.POTENTIAL_FLOAT;
                        CurrentToken.text += currChar;
                    }
                    else if (CurrentToken.type == TokenType.LITERAL_NUMBER)
                    {
                        CurrentToken.type = TokenType.LITERAL_FLOAT;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '/')
                {
                    if (CurrentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.type = TokenType.COMMENT;
                        CurrentToken.text = "";
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.POTENTIAL_COMMENT;
                        CurrentToken.text += currChar;
                    }
                }
                else if (bracelets.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (simpleOperators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '=')
                {
                    if (CurrentToken.text.Length == 1 && operators.Contains(CurrentToken.text[0]))
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (doubleOperators.Contains(CurrentToken.text + currChar))
                {
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '*' && CurrentToken.type == TokenType.POTENTIAL_COMMENT)
                {
                    if (CurrentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (operators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.WHITESPACE;
                    CurrentToken.text = currChar.ToString();
                }
                else if (currChar == '\n')
                {
                    if (CurrentToken.type == TokenType.COMMENT_MULTILINE)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                        CurrentToken.text = currChar.ToString();
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '"')
                {
                    if (CurrentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.LITERAL_STRING;
                    }
                    else if (CurrentToken.type == TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '\\')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (CurrentToken.type == TokenType.LITERAL_HEX)
                {
                    if (!(new char[] { '_', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                    {
                        RefreshTokenPosition(OffsetTotal);
                        throw new TokenizerException($"Invalid hex digit \'{currChar}\'", CurrentToken.After());
                    }
                    CurrentToken.text += currChar;
                }
                else
                {
                    if (CurrentToken.type == TokenType.WHITESPACE ||
                        CurrentToken.type == TokenType.LITERAL_NUMBER ||
                        CurrentToken.type == TokenType.LITERAL_HEX ||
                        CurrentToken.type == TokenType.LITERAL_FLOAT ||
                        CurrentToken.type == TokenType.OPERATOR)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.IDENTIFIER;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        CurrentToken.text += currChar;
                    }
                }
            }

            EndToken(sourceCode.Length);

            Print($"Tokenized in {(DateTime.Now - tokenizingStarted).TotalMilliseconds} ms", Output.LogType.Debug);

            tokensWithComments = NormalizeTokens(this.tokensWithComments, settings).ToArray();
            unicodeCharacters = _unicodeCharacters.ToArray();
            return NormalizeTokens(tokens, settings).ToArray();
        }

        void RefreshTokenPosition(int OffsetTotal)
        {
            CurrentToken.Position.End.Character = CurrentColumn;
            CurrentToken.Position.End.Line = CurrentLine;
            CurrentToken.AbsolutePosition.End = OffsetTotal;
        }

        void EndToken(int OffsetTotal)
        {
            CurrentToken.Position.End.Character = CurrentColumn;
            CurrentToken.Position.End.Line = CurrentLine;
            CurrentToken.AbsolutePosition.End = OffsetTotal;

            if (CurrentToken.type == TokenType.LITERAL_FLOAT)
            {
                if (CurrentToken.text.EndsWith('.'))
                {
                    CurrentToken.Position.End.Character--;
                    CurrentToken.Position.End.Line--;
                    CurrentToken.AbsolutePosition.End--;
                    CurrentToken.text = CurrentToken.text[..^1];
                    CurrentToken.type = TokenType.LITERAL_NUMBER;
                    tokens.Add(CurrentToken.Clone());

                    CurrentToken.Position.Start.Character = CurrentToken.Position.End.Character + 1;
                    CurrentToken.Position.Start.Line = CurrentToken.Position.End.Line + 1;
                    CurrentToken.AbsolutePosition.Start = CurrentToken.AbsolutePosition.End + 1;

                    CurrentToken.Position.End.Character++;
                    CurrentToken.Position.End.Line++;
                    CurrentToken.AbsolutePosition.End++;
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text = ".";
                }
            }

            if (CurrentToken.type != TokenType.WHITESPACE)
            {
                tokensWithComments.Add(CurrentToken.Clone());
                if (CurrentToken.type != TokenType.COMMENT && CurrentToken.type != TokenType.COMMENT_MULTILINE)
                {
                    tokens.Add(CurrentToken.Clone());
                }
            }
            else if (!string.IsNullOrEmpty(CurrentToken.text) && settings.TokenizeWhitespaces)
            {
                tokensWithComments.Add(CurrentToken.Clone());
                tokens.Add(CurrentToken.Clone());
            }

            if (CurrentToken.type == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.text.CompareTo(".") == 0)
                {
                    CurrentToken.type = TokenType.OPERATOR;
                }
                else
                {
                    CurrentToken.type = TokenType.LITERAL_FLOAT;
                }
            }

            CurrentToken.type = TokenType.WHITESPACE;
            CurrentToken.text = "";
            CurrentToken.Position.Start.Line = CurrentLine;
            CurrentToken.Position.Start.Character = CurrentColumn;
            CurrentToken.AbsolutePosition.Start = OffsetTotal;

        }

        static List<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
        {
            List<Token> result = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                var lastToken = result.Last();

                if (token.type == TokenType.WHITESPACE && lastToken.type == TokenType.WHITESPACE)
                {
                    lastToken.text += token.text;
                    continue;
                }

                if (token.type == TokenType.LINEBREAK && lastToken.type == TokenType.LINEBREAK && settings.JoinLinebreaks)
                {
                    lastToken.text += token.text;
                    continue;
                }


                result.Add(token);
            }

            return result;
        }
    }
}
