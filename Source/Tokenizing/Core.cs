using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.Tokenizing
{
    public abstract class BaseToken : IThingWithPosition
    {
        public abstract Position Position { get; }
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
        LITERAL_CHAR,
        LITERAL_FLOAT,

        OPERATOR,

        COMMENT,
        COMMENT_MULTILINE,

        STRING_UNICODE_CHARACTER,
        CHAR_ESCAPE_SEQUENCE,
        CHAR_UNICODE_CHARACTER,
        POTENTIAL_COMMENT,
        STRING_ESCAPE_SEQUENCE,
        POTENTIAL_END_MULTILINE_COMMENT,
        POTENTIAL_FLOAT,
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
            Tokenizing.TokenType.LITERAL_STRING => $"\"{Content}\"",
            Tokenizing.TokenType.LITERAL_CHAR => $"\'{Content}\'",
            _ => Content,
        };

        public static Token CreateAnonymous(string content, TokenType type = Tokenizing.TokenType.IDENTIFIER)
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
            Tokenizing.TokenType.LITERAL_STRING => $"\"{Content.Escape()}\"",
            Tokenizing.TokenType.LITERAL_CHAR => $"\'{Content.Escape()}\'",
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
            TokenType = Tokenizing.TokenType.WHITESPACE;
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

    public partial class Tokenizer
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

        readonly TextSource Source;

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

            Source = new TextSource(text ?? string.Empty);
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
    }
}
