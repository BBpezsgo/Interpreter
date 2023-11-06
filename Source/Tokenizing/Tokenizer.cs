using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

// TODO: new lines aren't working

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
        /// <summary> The tokenizer will produce <see cref="TokenType.WHITESPACE"/> tokens </summary>
        public bool TokenizeWhitespaces;
        /// <summary> The tokenizer will produce <see cref="TokenType.LINEBREAK"/> tokens </summary>
        public bool DistinguishBetweenSpacesAndNewlines;
        public bool JoinLinebreaks;
        /// <summary> The tokenizer will produce <see cref="TokenType.COMMENT"/> and <see cref="TokenType.COMMENT_MULTILINE"/> tokens </summary>
        public bool TokenizeComments;

        public static TokenizerSettings Default => new()
        {
            TokenizeWhitespaces = false,
            DistinguishBetweenSpacesAndNewlines = false,
            JoinLinebreaks = true,
            TokenizeComments = false,
        };
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
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
            if (a is null || b is null) return false;
            return a.Equals(b);
        }
        public static bool operator !=(Token? a, string? b) => !(a == b);

        string GetDebuggerDisplay() => TokenType switch
        {
            TokenType.LITERAL_STRING => $"\"{Content.Escape()}\"",
            TokenType.LITERAL_CHAR => $"\'{Content.Escape()}\'",
            _ => Content.Escape(),
        };
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
        static readonly char[] Operators = new char[] { '+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&' };
        static readonly string[] DoubleOperators = new string[] { "++", "--", "<<", ">>", "&&", "||" };
        static readonly char[] SimpleOperators = new char[] { ';', ',', '#' };
        static readonly char[] Whitespaces = new char[] { ' ', '\t', '\u200B', '\r' };

        readonly PreparationToken CurrentToken;
        int CurrentColumn;
        int CurrentLine;

        readonly string Text;
        readonly string? File;

        readonly List<Token> Tokens;
        readonly List<SimpleToken> UnicodeCharacters;

        readonly List<Warning> Warnings;

        readonly TokenizerSettings Settings;

        string? SavedUnicode;

        Tokenizer(TokenizerSettings settings, string? text, string? file)
        {
            CurrentToken = new(new Position(Range<SinglePosition>.Default, Range<int>.Default));
            CurrentColumn = 0;
            CurrentLine = 0;

            Tokens = new();
            UnicodeCharacters = new();

            Warnings = new();

            Settings = settings;
            Text = text ?? string.Empty;
            File = file;

            SavedUnicode = null;
        }

        public static TokenizerResult Tokenize(string? sourceCode, string? filePath = null)
            => new Tokenizer(TokenizerSettings.Default, sourceCode, filePath).TokenizeInternal();

        public static TokenizerResult Tokenize(string? sourceCode, TokenizerSettings settings, string? filePath = null)
            => new Tokenizer(settings, sourceCode, filePath).TokenizeInternal();

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        TokenizerResult TokenizeInternal()
        {
            for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
            {
                char? prev = (offsetTotal - 1 < 0) ? null : Text[offsetTotal - 1];
                char curr = Text[offsetTotal];
                char? next = (offsetTotal + 1 >= Text.Length) ? null : Text[offsetTotal + 1];

                /*
                CurrentColumn++;
                if (currChar == '\n')
                {
                    CurrentColumn = 0;
                    CurrentLine++;
                }
                */

                ProcessCharacter((prev, curr, next), offsetTotal, out bool breakLine);

                CurrentColumn++;
                if (breakLine)
                {
                    CurrentColumn = 0;
                    CurrentLine++;
                }
            }

            EndToken(Text.Length);

            CheckTokens(Tokens.ToArray());

            return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToArray(), UnicodeCharacters.ToArray(), Warnings.ToArray());
        }

        void ProcessCharacter((char? Prev, char Curr, char? Next) ctx, int offsetTotal, out bool breakLine)
        {
            breakLine = false;

            char currChar = ctx.Curr;

            if (currChar == '\n')
            {
                breakLine = true;
            }

            if (currChar == '\n' && CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
            {
                EndToken(offsetTotal);
                CurrentToken.Content.Clear();
                CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
            }

            /*
            if (currChar > byte.MaxValue)
            {
                RefreshTokenPosition(offsetTotal);
                if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                { Warnings.Add(new Warning($"Don't use special characters (please). Use \\u{((int)currChar).ToString("X").PadLeft(4, '0')}", CurrentToken.Position.After(), File)); }
                else
                { Warnings.Add(new Warning($"Don't use special characters.", CurrentToken.Position.After(), File)); }
            }
            */

            if (CurrentToken.TokenType == TokenType.STRING_UNICODE_CHARACTER)
            {
                if (SavedUnicode == null) throw new InternalException($"{nameof(SavedUnicode)} is null");
                if (SavedUnicode.Length == 4)
                {
                    string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                    UnicodeCharacters.Add(new SimpleToken(
                            unicodeChar,
                            new Position(
                                new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                                new Range<int>(offsetTotal - 6, offsetTotal)
                            )
                        ));
                    CurrentToken.Content.Append(unicodeChar);
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                    SavedUnicode = null;
                }
                else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    throw new TokenizerException($"This isn't a hex digit \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_UNICODE_CHARACTER)
            {
                if (SavedUnicode == null) throw new InternalException($"{nameof(SavedUnicode)} is null"); 
                if (SavedUnicode.Length == 4)
                {
                    string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                    UnicodeCharacters.Add(new SimpleToken(
                        unicodeChar,
                        new Position(
                            new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                            new Range<int>(offsetTotal - 6, offsetTotal)
                        )
                    ));
                    CurrentToken.Content.Append(unicodeChar);
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                    SavedUnicode = null;
                }
                else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    throw new TokenizerException($"This isn't a hex digit: \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }
            
            if (CurrentToken.TokenType == TokenType.STRING_ESCAPE_SEQUENCE)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.STRING_UNICODE_CHARACTER;
                    SavedUnicode = string.Empty;
                }
                else
                {
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        '0' => "\0",
                        _ => throw new TokenizerException($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_ESCAPE_SEQUENCE)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.CHAR_UNICODE_CHARACTER;
                    SavedUnicode = string.Empty;
                }
                else
                {
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '\'' => "\'",
                        '0' => "\0",
                        _ => throw new TokenizerException($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
            {
                CurrentToken.TokenType = TokenType.OPERATOR;
                if (currChar == '=')
                { CurrentToken.Content.Append(currChar); }
                EndToken(offsetTotal);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.COMMENT && currChar != '\n')
            {
                CurrentToken.Content.Append(currChar);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_STRING && currChar != '"')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.STRING_ESCAPE_SEQUENCE; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR && currChar != '\'')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.CHAR_ESCAPE_SEQUENCE; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                EndToken(offsetTotal);
                return;
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
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
            {
                CurrentToken.TokenType = TokenType.OPERATOR;
                EndToken(offsetTotal);
            }

            if (currChar == 'f' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                EndToken(offsetTotal);
            }
            else if (currChar == 'e' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
            {
                if (CurrentToken.ToString().Contains(currChar))
                { throw new TokenizerException($"Am I stupid or is this not a float number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
            }
            else if (currChar == 'x' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or is this not a hex number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_HEX;
            }
            else if (currChar == 'b' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or is this not a binary number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_BIN;
            }
            else if ((new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', }).Contains(currChar))
            {
                if (CurrentToken.TokenType == TokenType.WHITESPACE)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                }
                else if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
                { CurrentToken.TokenType = TokenType.LITERAL_FLOAT; }
                else if (CurrentToken.TokenType == TokenType.OPERATOR)
                {
                    if (CurrentToken.ToString() != "-")
                    { EndToken(offsetTotal); }
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                }

                if (CurrentToken.TokenType == TokenType.LITERAL_BIN)
                {
                    if (currChar != '0' && currChar != '1')
                    {
                        RefreshTokenPosition(offsetTotal);
                        throw new TokenizerException($"This isn't a binary digit am i right? \'{currChar}\'", CurrentToken.Position.After());
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
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
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
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.POTENTIAL_COMMENT;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (Bracelets.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (SimpleOperators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (currChar == '=')
            {
                if (CurrentToken.Content.Length == 1 && Operators.Contains(CurrentToken.Content[0]))
                {
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (DoubleOperators.Contains(CurrentToken.ToString() + currChar))
            {
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
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
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (Operators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
            }
            else if (Whitespaces.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.WHITESPACE;
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '\n')
            {
                if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = Settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '"')
            {
                if (CurrentToken.TokenType != TokenType.LITERAL_STRING)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\'')
            {
                if (CurrentToken.TokenType != TokenType.LITERAL_CHAR)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\\')
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                if (!(new char[] { '_', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    RefreshTokenPosition(offsetTotal);
                    throw new TokenizerException($"This isn't a hex digit am i right? \'{currChar}\'", CurrentToken.Position.After());
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
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.IDENTIFIER;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    CurrentToken.Content.Append(currChar);
                }
            }
        }

        Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

        /// <exception cref="TokenizerException"/>
        static void CheckTokens(Token[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            { CheckToken(tokens[i]); }
        }

        /// <exception cref="TokenizerException"/>
        static void CheckToken(Token token)
        {
            if (token.TokenType == TokenType.LITERAL_CHAR)
            {
                if (token.Content.Length > 1)
                { throw new TokenizerException($"I think there are more characters than there should be ({token.Content.Length})", token.Position); }
                else if (token.Content.Length < 1)
                { throw new TokenizerException($"I think there are less characters than there should be ({token.Content.Length})", token.Position); }
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
