using System;
using System.Collections.Generic;
using System.Linq;

namespace ProgrammingLanguage.BBCode
{
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;
    using ProgrammingLanguage.Tokenizer;

    public enum TokenType
    {
        WHITESPACE,
        LINEBREAK,

        IDENTIFIER,

        LITERAL_NUMBER,
        LITERAL_HEX,
        LITERAL_BIN,
        LITERAL_STRING,
        LITERAL_CHAR,
        LITERAL_FLOAT,

        STRING_UNICODE_CHARACTER,
        CHAR_UNICODE_CHARACTER,

        OPERATOR,

        STRING_ESCAPE_SEQUENCE,
        CHAR_ESCAPE_SEQUENCE,

        POTENTIAL_FLOAT,
        POTENTIAL_COMMENT,
        POTENTIAL_END_MULTILINE_COMMENT,

        COMMENT,
        COMMENT_MULTILINE,
    }

    public enum TokenAnalysedType
    {
        None,
        Attribute,
        Type,
        Struct,
        Keyword,
        FunctionName,
        VariableName,
        FieldName,
        ParameterName,
        Namespace,
        Hash,
        HashParameter,
        Library,
        Class,
        Statement,
        BuiltinType,
        Enum,
        EnumMember,
        TypeParameter,
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

    public class Token : BaseToken, IEquatable<Token>
    {
        public TokenAnalysedType AnalysedType;

        public readonly TokenType TokenType;
        public readonly bool IsAnonymous;

        public readonly string Content;

        public Token(TokenType type, string content, bool isAnonymous)
        {
            TokenType = type;
            AnalysedType = TokenAnalysedType.None;
            Content = content;
            IsAnonymous = isAnonymous;
        }

        public Token Clone() => new(TokenType, Content, IsAnonymous)
        {
            Position = Position,
            AbsolutePosition = AbsolutePosition,

            AnalysedType = AnalysedType,
        };
        public override string ToString() => Content;

        internal string ToFullString()
        {
            return $"Token:{TokenType} {{ \"{Content}\" {Position} {AnalysedType} }}";
        }

        public static Token CreateAnonymous(string content, TokenType type = TokenType.IDENTIFIER) => new(type, content, true)
        {
            AbsolutePosition = Core.Position.UnknownPosition.AbsolutePosition,
            Position = Core.Position.UnknownPosition.Range,
        };

        public override bool Equals(object obj) => obj is Token other && Equals(other);

        public bool Equals(Token other) =>
            other is not null &&
            Position.Equals(other.Position) &&
            AbsolutePosition.Equals(other.AbsolutePosition) &&
            TokenType == other.TokenType &&
            Content == other.Content &&
            IsAnonymous == other.IsAnonymous;

        public override int GetHashCode() => HashCode.Combine(Position, AbsolutePosition, TokenType, Content);

        public static bool operator ==(Token a, string b)
        {
            if (a is null && b is null) return true;
            if (a is null && b is not null) return false;
            if (a is not null && b is null) return false;
            return a.Content == b;
        }
        public static bool operator !=(Token a, string b) => !(a == b);
        public static bool operator ==(string a, Token b) => b == a;
        public static bool operator !=(string a, Token b) => b != a;
    }

    public readonly struct SimpleToken
    {
        public readonly string Content;
        public readonly Range<SinglePosition> Position;
        public readonly Range<int> AbsolutePosition;

        public SimpleToken(string content, Range<SinglePosition> position, Range<int> absolutePosition)
        {
            Content = content;
            Position = position;
            AbsolutePosition = absolutePosition;
        }

        public override string ToString() => Content;
        public Position GetPosition() => new(Position.Start.Line, Position.Start.Character, AbsolutePosition);
    }

    class PreparationToken : BaseToken, IEquatable<PreparationToken>
    {
        public TokenType TokenType;

        public string Content;

        public PreparationToken()
        {
            TokenType = TokenType.WHITESPACE;
            Content = "";
        }

        public override string ToString() => Content;

        internal string ToFullString()
        {
            return $"Token:{TokenType} {{ \"{Content}\" {Position} }}";
        }

        public override bool Equals(object obj) => obj is PreparationToken other && Equals(other);

        public bool Equals(PreparationToken other) =>
            other is not null &&
            Position.Equals(other.Position) &&
            AbsolutePosition.Equals(other.AbsolutePosition) &&
            TokenType == other.TokenType &&
            Content == other.Content;

        public override int GetHashCode() => HashCode.Combine(Position, AbsolutePosition, TokenType, Content);

        public static bool operator ==(PreparationToken a, string b)
        {
            if (a is null && b is null) return true;
            if (a is null && b is not null) return false;
            if (a is not null && b is null) return false;
            return a.Content == b;
        }
        public static bool operator !=(PreparationToken a, string b) => !(a == b);
        public static bool operator ==(string a, PreparationToken b) => b == a;
        public static bool operator !=(string a, PreparationToken b) => b != a;

        public Token Instantiate() => new(TokenType, Content, false)
        {
            AbsolutePosition = AbsolutePosition,
            Position = Position,
        };
    }

    /// <summary>
    /// The tokenizer for the BBCode language
    /// </summary>
    public class Tokenizer
    {
        readonly PreparationToken CurrentToken;
        int CurrentColumn;
        int CurrentLine;

        readonly List<Token> Tokens;

        readonly TokenizerSettings Settings;
        readonly Output.PrintCallback Print;

        static readonly char[] Bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
        static readonly char[] Banned = new char[] { '\r', '\u200B' };
        static readonly char[] Operators = new char[] { '+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&' };
        static readonly string[] DoubleOperators = new string[] { "++", "--", "<<", ">>", "&&", "||" };
        static readonly char[] SimpleOperators = new char[] { ';', ',', '#' };

        /// <param name="settings">
        /// Tokenizer settings<br/>
        /// Use <see cref="TokenizerSettings.Default"/> if you don't know
        /// </param>
        public Tokenizer(TokenizerSettings settings, Output.PrintCallback printCallback = null)
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

            Tokens = new();

            this.Settings = settings;
            this.Print = printCallback;
        }

        Position GetCurrentPosition(int OffsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(OffsetTotal, OffsetTotal + 1));

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public Token[] Parse(string sourceCode)
            => Parse(sourceCode, null, null, out _);

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings)
            => Parse(sourceCode, warnings, null, out _);

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings, string filePath)
            => Parse(sourceCode, warnings, filePath, out _);

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public Token[] Parse(string sourceCode, List<Warning> warnings, string filePath, out SimpleToken[] unicodeCharacters)
        {
            DateTime tokenizingStarted = DateTime.Now;
            Print?.Invoke("Tokenizing ...", Output.LogType.Debug);

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

                if (Banned.Contains(currChar)) continue;

                if (currChar == '\n' && CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(OffsetTotal);
                    CurrentToken.Content = "";
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                }

                if (currChar > byte.MaxValue && warnings != null)
                {
                    RefreshTokenPosition(OffsetTotal);
                    if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                    {
                        warnings.Add(new Warning($"Don't use special characters. Use \\u{(((int)currChar).ToString("X").PadLeft(4, '0'))}", CurrentToken.After(), filePath));
                    }
                    else
                    {
                        warnings.Add(new Warning($"Don't use special characters.", CurrentToken.After(), filePath));
                    }
                }

                if (CurrentToken.TokenType == TokenType.STRING_UNICODE_CHARACTER)
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
                        CurrentToken.Content += unicodeChar;
                        CurrentToken.TokenType = TokenType.LITERAL_STRING;
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
                else if (CurrentToken.TokenType == TokenType.CHAR_UNICODE_CHARACTER)
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
                        CurrentToken.Content += unicodeChar;
                        CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                        savedUnicode = null;
                    }
                    else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                    {
                        throw new TokenizerException("Invalid hex digit in unicode character: '" + currChar + "' inside literal char", GetCurrentPosition(OffsetTotal));
                    }
                    else
                    {
                        savedUnicode += currChar;
                        continue;
                    }
                }

                if (CurrentToken.TokenType == TokenType.STRING_ESCAPE_SEQUENCE)
                {
                    if (currChar == 'u')
                    {
                        CurrentToken.TokenType = TokenType.STRING_UNICODE_CHARACTER;
                        savedUnicode = "";
                        continue;
                    }
                    CurrentToken.Content += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        '0' => "\0",
                        _ => throw new TokenizerException("Unknown escape sequence: \\" + currChar + " in string.", GetCurrentPosition(OffsetTotal)),
                    };
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.CHAR_ESCAPE_SEQUENCE)
                {
                    if (currChar == 'u')
                    {
                        CurrentToken.TokenType = TokenType.CHAR_UNICODE_CHARACTER;
                        savedUnicode = "";
                        continue;
                    }
                    CurrentToken.Content += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '\'' => "\'",
                        '0' => "\0",
                        _ => throw new TokenizerException("Unknown escape sequence: \\" + currChar + " in char.", GetCurrentPosition(OffsetTotal)),
                    };
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    if (currChar == '=')
                    {
                        CurrentToken.Content += currChar;
                    }
                    EndToken(OffsetTotal);
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.COMMENT && currChar != '\n')
                {
                    CurrentToken.Content += currChar;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_STRING && currChar != '"')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.TokenType = TokenType.STRING_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.Content += currChar;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR && currChar != '\'')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.TokenType = TokenType.CHAR_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.Content += currChar;
                    continue;
                }

                if (CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
                {
                    CurrentToken.Content += currChar;
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                    EndToken(OffsetTotal);
                    continue;
                }

                if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE || CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
                {
                    CurrentToken.Content += currChar;
                    if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE && currChar == '*')
                    {
                        CurrentToken.TokenType = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                    }
                    else
                    {
                        CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                    }
                    continue;
                }

                if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    EndToken(OffsetTotal);
                }

                if (currChar == 'f' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
                {
                    CurrentToken.Content += currChar;
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                    EndToken(OffsetTotal);
                }
                else if (currChar == 'e' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
                {
                    if (CurrentToken.Content.Contains(currChar))
                    { throw new TokenizerException($"Invalid float literal format", CurrentToken.GetPosition()); }
                    CurrentToken.Content += currChar;
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                }
                else if (currChar == 'x' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.Content.EndsWith('0'))
                    { throw new TokenizerException($"Invalid hex number literal format", CurrentToken.GetPosition()); }
                    CurrentToken.Content += currChar;
                    CurrentToken.TokenType = TokenType.LITERAL_HEX;
                }
                else if (currChar == 'b' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.Content.EndsWith('0'))
                    { throw new TokenizerException($"Invalid bin number literal format", CurrentToken.GetPosition()); }
                    CurrentToken.Content += currChar;
                    CurrentToken.TokenType = TokenType.LITERAL_BIN;
                }
                else if ((new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', }).Contains(currChar))
                {
                    if (CurrentToken.TokenType == TokenType.WHITESPACE)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    }
                    else if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
                    { CurrentToken.TokenType = TokenType.LITERAL_FLOAT; }
                    else if (CurrentToken.TokenType == TokenType.OPERATOR)
                    {
                        if (CurrentToken.Content != "-")
                        { EndToken(OffsetTotal); }
                        CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    }

                    if (CurrentToken.TokenType == TokenType.LITERAL_BIN)
                    {
                        if (currChar != '0' && currChar != '1')
                        {
                            RefreshTokenPosition(OffsetTotal);
                            throw new TokenizerException($"Invalid bin digit \'{currChar}\'", CurrentToken.After());
                        }
                    }

                    CurrentToken.Content += currChar;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_BIN && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content += currChar;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content += currChar;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content += currChar;
                }
                else if (currChar == '.')
                {
                    if (CurrentToken.TokenType == TokenType.WHITESPACE)
                    {
                        CurrentToken.TokenType = TokenType.POTENTIAL_FLOAT;
                        CurrentToken.Content += currChar;
                    }
                    else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                    {
                        CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                        CurrentToken.Content += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content += currChar;
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '/')
                {
                    if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.TokenType = TokenType.COMMENT;
                        CurrentToken.Content = "";
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.POTENTIAL_COMMENT;
                        CurrentToken.Content += currChar;
                    }
                }
                else if (Bracelets.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content += currChar;
                    EndToken(OffsetTotal);
                }
                else if (SimpleOperators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '=')
                {
                    if (CurrentToken.Content.Length == 1 && Operators.Contains(CurrentToken.Content[0]))
                    {
                        CurrentToken.Content += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content += currChar;
                    }
                }
                else if (DoubleOperators.Contains(CurrentToken.Content + currChar))
                {
                    CurrentToken.Content += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '*' && CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                {
                    if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                        CurrentToken.Content += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content += currChar;
                    }
                }
                else if (Operators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content += currChar;
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.WHITESPACE;
                    CurrentToken.Content = currChar.ToString();
                }
                else if (currChar == '\n')
                {
                    if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = Settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                        CurrentToken.Content = currChar.ToString();
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '"')
                {
                    if (CurrentToken.TokenType != TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.LITERAL_STRING;
                    }
                    else if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '\'')
                {
                    if (CurrentToken.TokenType != TokenType.LITERAL_CHAR)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                    }
                    else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR)
                    {
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '\\')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content += currChar;
                    EndToken(OffsetTotal);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_HEX)
                {
                    if (!(new char[] { '_', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                    {
                        RefreshTokenPosition(OffsetTotal);
                        throw new TokenizerException($"Invalid hex digit \'{currChar}\'", CurrentToken.After());
                    }
                    CurrentToken.Content += currChar;
                }
                else
                {
                    if (CurrentToken.TokenType == TokenType.WHITESPACE ||
                        CurrentToken.TokenType == TokenType.LITERAL_NUMBER ||
                        CurrentToken.TokenType == TokenType.LITERAL_HEX ||
                        CurrentToken.TokenType == TokenType.LITERAL_FLOAT ||
                        CurrentToken.TokenType == TokenType.OPERATOR)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.IDENTIFIER;
                        CurrentToken.Content += currChar;
                    }
                    else
                    {
                        CurrentToken.Content += currChar;
                    }
                }
            }

            EndToken(sourceCode.Length);

            Print?.Invoke($"Tokenized in {(DateTime.Now - tokenizingStarted).TotalMilliseconds} ms", Output.LogType.Debug);

            CheckTokens(Tokens.ToArray());

            unicodeCharacters = _unicodeCharacters.ToArray();
            return NormalizeTokens(Tokens, Settings).ToArray();
        }

        /// <exception cref="TokenizerException"></exception>
        static void CheckTokens(Token[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                Token token = tokens[i];

                switch (token.TokenType)
                {
                    case TokenType.WHITESPACE:
                        break;
                    case TokenType.LINEBREAK:
                        break;
                    case TokenType.IDENTIFIER:
                        break;
                    case TokenType.LITERAL_NUMBER:
                        break;
                    case TokenType.LITERAL_HEX:
                        break;
                    case TokenType.LITERAL_BIN:
                        break;
                    case TokenType.LITERAL_STRING:
                        break;
                    case TokenType.LITERAL_CHAR:
                        {
                            if (token.Content.Length != 1)
                            {
                                throw new TokenizerException($"Literal char should only contain one character. You specified {token.Content.Length}.", token.GetPosition());
                            }
                        }
                        break;
                    case TokenType.LITERAL_FLOAT:
                        break;
                    case TokenType.STRING_UNICODE_CHARACTER:
                        break;
                    case TokenType.CHAR_UNICODE_CHARACTER:
                        break;
                    case TokenType.OPERATOR:
                        break;
                    case TokenType.STRING_ESCAPE_SEQUENCE:
                        break;
                    case TokenType.CHAR_ESCAPE_SEQUENCE:
                        break;
                    case TokenType.POTENTIAL_FLOAT:
                        break;
                    case TokenType.POTENTIAL_COMMENT:
                        break;
                    case TokenType.POTENTIAL_END_MULTILINE_COMMENT:
                        break;
                    case TokenType.COMMENT:
                        break;
                    case TokenType.COMMENT_MULTILINE:
                        break;
                    default:
                        break;
                }
            }
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

            if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                if (CurrentToken.Content.EndsWith('.'))
                {
                    CurrentToken.Position.End.Character--;
                    CurrentToken.Position.End.Line--;
                    CurrentToken.AbsolutePosition.End--;
                    CurrentToken.Content = CurrentToken.Content[..^1];
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    Tokens.Add(CurrentToken.Instantiate());

                    CurrentToken.Position.Start.Character = CurrentToken.Position.End.Character + 1;
                    CurrentToken.Position.Start.Line = CurrentToken.Position.End.Line + 1;
                    CurrentToken.AbsolutePosition.Start = CurrentToken.AbsolutePosition.End + 1;

                    CurrentToken.Position.End.Character++;
                    CurrentToken.Position.End.Line++;
                    CurrentToken.AbsolutePosition.End++;
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content = ".";
                }
            }

            if (CurrentToken.TokenType != TokenType.WHITESPACE)
            {
                Tokens.Add(CurrentToken.Instantiate());
            }
            else if (!string.IsNullOrEmpty(CurrentToken.Content) && Settings.TokenizeWhitespaces)
            {
                Tokens.Add(CurrentToken.Instantiate());
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.Content.CompareTo(".") == 0)
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                }
            }

            CurrentToken.TokenType = TokenType.WHITESPACE;
            CurrentToken.Content = "";
            CurrentToken.Position.Start.Line = CurrentLine;
            CurrentToken.Position.Start.Character = CurrentColumn;
            CurrentToken.AbsolutePosition.Start = OffsetTotal;

        }

        static List<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
        {
            List<Token> result = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                Token lastToken = result[^1];

                if (token.TokenType == TokenType.WHITESPACE && lastToken.TokenType == TokenType.WHITESPACE)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous)
                    {
                        Position = lastToken.Position,
                        AbsolutePosition = lastToken.AbsolutePosition,
                    };
                    continue;
                }

                if (token.TokenType == TokenType.LINEBREAK && lastToken.TokenType == TokenType.LINEBREAK && settings.JoinLinebreaks)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous)
                    {
                        Position = lastToken.Position,
                        AbsolutePosition = lastToken.AbsolutePosition,
                    };
                    continue;
                }


                result.Add(token);
            }

            return result;
        }
    }

    public static class Extensions
    {
        public static Token[] RemoveTokens(this IEnumerable<Token> tokens, TokenType tokenType)
        {
            List<Token> _tokens = new(tokens);

            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                if (_tokens[i].TokenType != tokenType) continue;

                _tokens.RemoveAt(i);
            }

            return _tokens.ToArray();
        }

        public static Token[] RemoveTokens(this IEnumerable<Token> tokens, params TokenType[] tokenTypes)
        {
            List<Token> _tokens = new(tokens);

            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                if (!tokenTypes.Contains(_tokens[i].TokenType)) continue;

                _tokens.RemoveAt(i);
            }

            return _tokens.ToArray();
        }
    }
}
