using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LanguageCore.Tokenizing
{
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

    public enum TokenAnalyzedType
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
        /// <summary> The Tokenizer will produce <see cref="TokenType.COMMENT"/> and <see cref="TokenType.COMMENT_MULTILINE"/> </summary>
        public bool TokenizeComments;

        public static TokenizerSettings Default => new()
        {
            TokenizeWhitespaces = false,
            DistinguishBetweenSpacesAndNewlines = false,
            JoinLinebreaks = true,
            TokenizeComments = false,
        };
    }

    public class Token : BaseToken, IEquatable<Token>, IEquatable<string>, IDuplicatable<Token>
    {
        readonly Position position;

        public TokenAnalyzedType AnalyzedType;

        public readonly TokenType TokenType;
        public readonly bool IsAnonymous;

        public readonly string Content;

        public Token(TokenType type, string content, bool isAnonymous, Position position) : base()
        {
            TokenType = type;
            AnalyzedType = TokenAnalyzedType.None;
            Content = content;
            IsAnonymous = isAnonymous;
            this.position = position;
        }

        public override Position Position => position;

        public override string ToString() => Content;
        public string ToOriginalString() => TokenType switch
        {
            TokenType.WHITESPACE => Content,
            TokenType.LINEBREAK => Content,
            TokenType.IDENTIFIER => Content,
            TokenType.LITERAL_NUMBER => Content,
            TokenType.LITERAL_HEX => Content,
            TokenType.LITERAL_BIN => Content,
            TokenType.LITERAL_STRING => $"\"{Content}\"",
            TokenType.LITERAL_CHAR => $"\'{Content}\'",
            TokenType.LITERAL_FLOAT => Content,
            TokenType.STRING_UNICODE_CHARACTER => Content,
            TokenType.CHAR_UNICODE_CHARACTER => Content,
            TokenType.OPERATOR => Content,
            TokenType.STRING_ESCAPE_SEQUENCE => Content,
            TokenType.CHAR_ESCAPE_SEQUENCE => Content,
            TokenType.POTENTIAL_FLOAT => Content,
            TokenType.POTENTIAL_COMMENT => Content,
            TokenType.POTENTIAL_END_MULTILINE_COMMENT => Content,
            TokenType.COMMENT => Content,
            TokenType.COMMENT_MULTILINE => Content,
            _ => Content,
        };

        public static Token CreateAnonymous(string content, TokenType type = TokenType.IDENTIFIER)
            => new(type, content, true, Position.UnknownPosition);

        public static Token CreateAnonymous(string content, TokenType type, Position position)
            => new(type, content, true, position);

        public override bool Equals(object? obj) => obj is Token other && Equals(other);
        public bool Equals(Token? other) =>
            other is not null &&
            Position.Equals(other.Position) &&
            TokenType == other.TokenType &&
            Content == other.Content &&
            IsAnonymous == other.IsAnonymous;
        public bool Equals(string? other) =>
            other is not null &&
            Content == other;

        public override int GetHashCode() => HashCode.Combine(Position, TokenType, Content);

        public Token Duplicate() => new(TokenType, new string(Content), IsAnonymous, Position)
        { AnalyzedType = AnalyzedType };

        public static bool operator ==(Token? a, string? b)
        {
            if (a is null && b is null) return true;
            if (a is null && b is not null) return false;
            if (a is not null && b is null) return false;
            return a?.Content == b;
        }
        public static bool operator !=(Token? a, string? b) => !(a == b);
        public static bool operator ==(string? a, Token? b) => b == a;
        public static bool operator !=(string? a, Token? b) => b != a;
    }

    public readonly struct SimpleToken : IThingWithPosition
    {
        public readonly string Content;

        public SimpleToken(string content, Position position)
        {
            Content = content;
            Position = position;
        }

        public override string ToString() => Content;
        public readonly Position Position { get; }
    }

    class PreparationToken : BaseToken
    {
        public Position position;
        public TokenType TokenType;
        public readonly StringBuilder Content;

        public PreparationToken(Position position) : base()
        {
            this.position = position;
            TokenType = TokenType.WHITESPACE;
            Content = new StringBuilder();
        }

        public override Position Position => position;

        public override string ToString() => Content.ToString();

        public Token Instantiate() => new(TokenType, Content.ToString(), false, Position);
    }

    public readonly struct TokenizerResult
    {
        public readonly Warning[] Warnings;

        public readonly Token[] Tokens;
        public readonly SimpleToken[] UnicodeCharacterTokens;

        public TokenizerResult(Token[] tokens, SimpleToken[] unicodeCharacterTokens, Warning[] warnings)
        {
            Tokens = tokens;
            UnicodeCharacterTokens = unicodeCharacterTokens;
            Warnings = warnings;
        }

        public static implicit operator Token[](TokenizerResult result) => result.Tokens;
    }

    public class Tokenizer
    {
        static readonly char[] Bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
        static readonly char[] Banned = new char[] { '\r', '\u200B' };
        static readonly char[] Operators = new char[] { '+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&' };
        static readonly string[] DoubleOperators = new string[] { "++", "--", "<<", ">>", "&&", "||" };
        static readonly char[] SimpleOperators = new char[] { ';', ',', '#' };

        readonly PreparationToken CurrentToken;
        int CurrentColumn;
        int CurrentLine;

        readonly string Text;
        readonly string? File;

        readonly List<Token> Tokens;
        readonly List<SimpleToken> UnicodeCharacters;

        readonly List<Warning> Warnings;

        readonly TokenizerSettings Settings;

        Tokenizer(TokenizerSettings settings, string text, string? file)
        {
            CurrentToken = new(new Position(Range<SinglePosition>.Default, Range<int>.Default));
            CurrentColumn = 0;
            CurrentLine = 1;

            Tokens = new();
            UnicodeCharacters = new();

            Warnings = new();

            Settings = settings;
            Text = text;
            File = file;
        }

        public static TokenizerResult Tokenize(string sourceCode, string? filePath = null)
            => new Tokenizer(TokenizerSettings.Default, sourceCode, filePath).TokenizeInternal();

        public static TokenizerResult Tokenize(string sourceCode, TokenizerSettings settings, string? filePath = null)
            => new Tokenizer(settings, sourceCode, filePath).TokenizeInternal();

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        TokenizerResult TokenizeInternal()
        {
            string? savedUnicode = null;

            for (int OffsetTotal = 0; OffsetTotal < Text.Length; OffsetTotal++)
            {
                char currChar = Text[OffsetTotal];

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
                    CurrentToken.Content.Clear();
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                }

                if (currChar > byte.MaxValue)
                {
                    RefreshTokenPosition(OffsetTotal);
                    if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                    {
                        Warnings.Add(new Warning($"Don't use special characters. Use \\u{(((int)currChar).ToString("X").PadLeft(4, '0'))}", CurrentToken.Position.After(), File));
                    }
                    else
                    {
                        Warnings.Add(new Warning($"Don't use special characters.", CurrentToken.Position.After(), File));
                    }
                }

                if (CurrentToken.TokenType == TokenType.STRING_UNICODE_CHARACTER)
                {
                    if (savedUnicode == null) throw new InternalException($"savedUnicode is null");
                    if (savedUnicode.Length == 4)
                    {
                        string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(savedUnicode, 16));
                        UnicodeCharacters.Add(new SimpleToken(
                                unicodeChar,
                                new Position(
                                    new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                                    new Range<int>(OffsetTotal - 6, OffsetTotal)
                                )
                            ));
                        CurrentToken.Content.Append(unicodeChar);
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
                        UnicodeCharacters.Add(new SimpleToken(
                            unicodeChar,
                            new Position(
                                new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                                new Range<int>(OffsetTotal - 6, OffsetTotal)
                            )
                        ));
                        CurrentToken.Content.Append(unicodeChar);
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
                        savedUnicode = string.Empty;
                        continue;
                    }
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        '0' => "\0",
                        _ => throw new TokenizerException("Unknown escape sequence: \\" + currChar + " in string.", GetCurrentPosition(OffsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.CHAR_ESCAPE_SEQUENCE)
                {
                    if (currChar == 'u')
                    {
                        CurrentToken.TokenType = TokenType.CHAR_UNICODE_CHARACTER;
                        savedUnicode = string.Empty;
                        continue;
                    }
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '\'' => "\'",
                        '0' => "\0",
                        _ => throw new TokenizerException("Unknown escape sequence: \\" + currChar + " in char.", GetCurrentPosition(OffsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    if (currChar == '=')
                    {
                        CurrentToken.Content.Append(currChar);
                    }
                    EndToken(OffsetTotal);
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.COMMENT && currChar != '\n')
                {
                    CurrentToken.Content.Append(currChar);
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_STRING && currChar != '"')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.TokenType = TokenType.STRING_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.Content.Append(currChar);
                    continue;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR && currChar != '\'')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.TokenType = TokenType.CHAR_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.Content.Append(currChar);
                    continue;
                }

                if (CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
                {
                    CurrentToken.Content.Append(currChar);
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                    EndToken(OffsetTotal);
                    continue;
                }

                if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE || CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
                {
                    CurrentToken.Content.Append(currChar);
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
                    CurrentToken.Content.Append(currChar);
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                    EndToken(OffsetTotal);
                }
                else if (currChar == 'e' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
                {
                    if (CurrentToken.ToString().Contains(currChar))
                    { throw new TokenizerException($"Invalid float literal format", CurrentToken.Position); }
                    CurrentToken.Content.Append(currChar);
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                }
                else if (currChar == 'x' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.ToString().EndsWith('0'))
                    { throw new TokenizerException($"Invalid hex number literal format", CurrentToken.Position); }
                    CurrentToken.Content.Append(currChar);
                    CurrentToken.TokenType = TokenType.LITERAL_HEX;
                }
                else if (currChar == 'b' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    if (!CurrentToken.ToString().EndsWith('0'))
                    { throw new TokenizerException($"Invalid bin number literal format", CurrentToken.Position); }
                    CurrentToken.Content.Append(currChar);
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
                        if (CurrentToken.ToString() != "-")
                        { EndToken(OffsetTotal); }
                        CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    }

                    if (CurrentToken.TokenType == TokenType.LITERAL_BIN)
                    {
                        if (currChar != '0' && currChar != '1')
                        {
                            RefreshTokenPosition(OffsetTotal);
                            throw new TokenizerException($"Invalid bin digit \'{currChar}\'", CurrentToken.Position.After());
                        }
                    }

                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_BIN && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
                {
                    CurrentToken.Content.Append(currChar);
                }
                else if (currChar == '.')
                {
                    if (CurrentToken.TokenType == TokenType.WHITESPACE)
                    {
                        CurrentToken.TokenType = TokenType.POTENTIAL_FLOAT;
                        CurrentToken.Content.Append(currChar);
                    }
                    else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                    {
                        CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                        CurrentToken.Content.Append(currChar);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content.Append(currChar);
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '/')
                {
                    if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.TokenType = TokenType.COMMENT;
                        CurrentToken.Content.Clear();
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.POTENTIAL_COMMENT;
                        CurrentToken.Content.Append(currChar);
                    }
                }
                else if (Bracelets.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                    EndToken(OffsetTotal);
                }
                else if (SimpleOperators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                    EndToken(OffsetTotal);
                }
                else if (currChar == '=')
                {
                    if (CurrentToken.Content.Length == 1 && Operators.Contains(CurrentToken.Content[0]))
                    {
                        CurrentToken.Content.Append(currChar);
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content.Append(currChar);
                    }
                }
                else if (DoubleOperators.Contains(CurrentToken.ToString() + currChar))
                {
                    CurrentToken.Content.Append(currChar);
                    EndToken(OffsetTotal);
                }
                else if (currChar == '*' && CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                {
                    if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                        CurrentToken.Content.Append(currChar);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.TokenType = TokenType.OPERATOR;
                        CurrentToken.Content.Append(currChar);
                    }
                }
                else if (Operators.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.TokenType = TokenType.WHITESPACE;
                    CurrentToken.Content.Append(currChar);
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
                        CurrentToken.Content.Append(currChar);
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
                    CurrentToken.Content.Append(currChar);
                    EndToken(OffsetTotal);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_HEX)
                {
                    if (!(new char[] { '_', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                    {
                        RefreshTokenPosition(OffsetTotal);
                        throw new TokenizerException($"Invalid hex digit \'{currChar}\'", CurrentToken.Position.After());
                    }
                    CurrentToken.Content.Append(currChar);
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
                        CurrentToken.Content.Append(currChar);
                    }
                    else
                    {
                        CurrentToken.Content.Append(currChar);
                    }
                }
            }

            EndToken(Text.Length);

            CheckTokens(Tokens.ToArray());

            return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToArray(), UnicodeCharacters.ToArray(), Warnings.ToArray());
        }

        Position GetCurrentPosition(int OffsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(OffsetTotal, OffsetTotal + 1));

        /// <exception cref="TokenizerException"/>
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
                                throw new TokenizerException($"Literal char should only contain one character. You specified {token.Content.Length}.", token.Position);
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
            CurrentToken.position.Range.End.Character = CurrentColumn;
            CurrentToken.position.Range.End.Line = CurrentLine;
            CurrentToken.position.AbsoluteRange.End = OffsetTotal;
        }

        void EndToken(int OffsetTotal)
        {
            CurrentToken.position.Range.End.Character = CurrentColumn;
            CurrentToken.position.Range.End.Line = CurrentLine;
            CurrentToken.position.AbsoluteRange.End = OffsetTotal;

            // Skip comments if they should be ignored
            if (!Settings.TokenizeComments &&
                (CurrentToken.TokenType == TokenType.COMMENT ||
                CurrentToken.TokenType == TokenType.COMMENT_MULTILINE))
            { goto Finish; }

            // Skip whitespaces if they should be ignored
            if (!Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.WHITESPACE)
            { goto Finish; }

            // Skip empty whitespaces
            if (Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.WHITESPACE &&
                string.IsNullOrEmpty(CurrentToken.ToString()))
            { goto Finish; }

            if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                if (CurrentToken.ToString().EndsWith('.'))
                {
                    CurrentToken.position.Range.End.Character--;
                    CurrentToken.position.Range.End.Line--;
                    CurrentToken.position.AbsoluteRange.End--;
                    CurrentToken.Content.Remove(CurrentToken.Content.Length - 1, 1);
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    Tokens.Add(CurrentToken.Instantiate());

                    CurrentToken.position.Range.Start.Character = CurrentToken.position.Range.End.Character + 1;
                    CurrentToken.position.Range.Start.Line = CurrentToken.position.Range.End.Line + 1;
                    CurrentToken.position.AbsoluteRange.Start = CurrentToken.position.AbsoluteRange.End + 1;

                    CurrentToken.position.Range.End.Character++;
                    CurrentToken.position.Range.End.Line++;
                    CurrentToken.position.AbsoluteRange.End++;
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Clear();
                    CurrentToken.Content.Append('.');
                }
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.ToString().CompareTo(".") == 0)
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                }
            }

            Tokens.Add(CurrentToken.Instantiate());

        Finish:
            CurrentToken.TokenType = TokenType.WHITESPACE;
            CurrentToken.Content.Clear();
            CurrentToken.position.Range.Start.Line = CurrentLine;
            CurrentToken.position.Range.Start.Character = CurrentColumn;
            CurrentToken.position.AbsoluteRange.Start = OffsetTotal;
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
                        lastToken.IsAnonymous,
                        lastToken.Position);
                    continue;
                }

                if (token.TokenType == TokenType.LINEBREAK && lastToken.TokenType == TokenType.LINEBREAK && settings.JoinLinebreaks)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous,
                        lastToken.Position);
                    continue;
                }


                result.Add(token);
            }

            return result;
        }
    }
}
