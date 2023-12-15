using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.Tokenizing
{
    public enum TokenType
    {
        Whitespace,
        LineBreak,

        Identifier,

        LiteralNumber,
        LiteralHex,
        LiteralBinary,
        LiteralString,
        LiteralCharacter,
        LiteralFloat,

        Operator,

        Comment,
        CommentMultiline,
    }

    public enum PreparationTokenType
    {
        Whitespace,
        LineBreak,

        Identifier,

        LiteralNumber,
        LiteralHex,
        LiteralBinary,
        LiteralString,
        LiteralCharacter,
        LiteralFloat,

        Operator,

        Comment,
        CommentMultiline,

        STRING_EscapeSequence,
        STRING_UnicodeCharacter,

        CHAR_EscapeSequence,
        CHAR_UnicodeCharacter,

        POTENTIAL_COMMENT,
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
        /// <summary> The tokenizer will produce <see cref="TokenType.Whitespace"/> tokens </summary>
        public bool TokenizeWhitespaces;
        /// <summary> The tokenizer will produce <see cref="TokenType.LineBreak"/> tokens </summary>
        public bool DistinguishBetweenSpacesAndNewlines;
        public bool JoinLinebreaks;
        /// <summary> The tokenizer will produce <see cref="TokenType.Comment"/> and <see cref="TokenType.CommentMultiline"/> tokens </summary>
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
    public class Token : IThingWithPosition, IEquatable<Token>, IEquatable<string>, IDuplicatable<Token>
    {
        readonly Position position;

        public TokenAnalyzedType AnalyzedType;

        public readonly TokenType TokenType;
        public readonly bool IsAnonymous;

        public readonly string Content;

        public static Token Empty => new(TokenType.Whitespace, string.Empty, true, new Position(new Range<SinglePosition>(new SinglePosition(0, 0), new SinglePosition(0, 0)), new Range<int>(0, 0)));

        public Token(TokenType type, string content, bool isAnonymous, Position position) : base()
        {
            TokenType = type;
            AnalyzedType = TokenAnalyzedType.None;
            Content = content;
            IsAnonymous = isAnonymous;
            this.position = position;
        }

        public Position Position => position;

        public override string ToString() => Content;
        public string ToOriginalString() => TokenType switch
        {
            TokenType.LiteralString => $"\"{Content}\"",
            TokenType.LiteralCharacter => $"\'{Content}\'",
            TokenType.Comment => $"//{Content}",
            _ => Content,
        };

        public static Token CreateAnonymous(string content, TokenType type = TokenType.Identifier)
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
            TokenType.LiteralString => $"\"{Content.Escape()}\"",
            TokenType.LiteralCharacter => $"\'{Content.Escape()}\'",
            _ => Content.Escape(),
        };

        public (Token?, Token?) CutInHalf()
        {
            if (string.IsNullOrEmpty(Content))
            { return (null, null); }

            Token left;
            Token right;

            if (Content.Length == 1)
            {
                left = Duplicate();
                return (left, null);
            }

            int leftSize = Content.Length / 2;

            (Position leftPosition, Position rightPosition) = position.CutInHalf();

            left = new Token(TokenType, Content[..leftSize], IsAnonymous, leftPosition);
            right = new Token(TokenType, Content[leftSize..], IsAnonymous, rightPosition);

            return (left, right);
        }
    }

    public readonly struct SimpleToken : IThingWithPosition
    {
        public readonly string Content;
        readonly Position position;

        public SimpleToken(string content, Position position)
        {
            this.Content = content;
            this.position = position;
        }

        public override string ToString() => Content;

        public Position Position => position;
    }

    class PreparationToken : IThingWithPosition
    {
        Position position;
        public PreparationTokenType TokenType;
        public readonly StringBuilder Content;

        public ref Position Position => ref position;
        Position IThingWithPosition.Position => position;

        public PreparationToken(Position position)
        {
            this.position = position;
            this.TokenType = PreparationTokenType.Whitespace;
            this.Content = new StringBuilder();
        }

        public override string ToString() => Content.ToString();

        public Token Instantiate() => new(TokenType switch
        {
            PreparationTokenType.Whitespace => Tokenizing.TokenType.Whitespace,
            PreparationTokenType.LineBreak => Tokenizing.TokenType.LineBreak,
            PreparationTokenType.Identifier => Tokenizing.TokenType.Identifier,
            PreparationTokenType.LiteralNumber => Tokenizing.TokenType.LiteralNumber,
            PreparationTokenType.LiteralHex => Tokenizing.TokenType.LiteralHex,
            PreparationTokenType.LiteralBinary => Tokenizing.TokenType.LiteralBinary,
            PreparationTokenType.LiteralString => Tokenizing.TokenType.LiteralString,
            PreparationTokenType.LiteralCharacter => Tokenizing.TokenType.LiteralCharacter,
            PreparationTokenType.LiteralFloat => Tokenizing.TokenType.LiteralFloat,
            PreparationTokenType.Operator => Tokenizing.TokenType.Operator,
            PreparationTokenType.Comment => Tokenizing.TokenType.Comment,
            PreparationTokenType.CommentMultiline => Tokenizing.TokenType.CommentMultiline,
            _ => throw new InternalException($"Token {this} isn't finished (type is {TokenType})"),
        }, Content.ToString(), false, Position);
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

    public abstract partial class Tokenizer
    {
        static readonly char[] Bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
        static readonly char[] Operators = new char[] { '+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&', '~' };
        static readonly string[] DoubleOperators = new string[] { "++", "--", "<<", ">>", "&&", "||" };
        static readonly char[] SimpleOperators = new char[] { ';', ',', '#' };
        static readonly char[] Whitespaces = new char[] { ' ', '\t', '\u200B', '\r' };
        static readonly char[] DigitsHex = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };

        readonly PreparationToken CurrentToken;
        protected int CurrentColumn;
        protected int CurrentLine;

        protected readonly List<Token> Tokens;
        protected readonly List<SimpleToken> UnicodeCharacters;

        protected readonly List<Warning> Warnings;

        protected readonly TokenizerSettings Settings;

        string? SavedUnicode;

        protected Tokenizer(TokenizerSettings settings)
        {
            CurrentToken = new(new Position(Range<SinglePosition>.Default, Range<int>.Default));
            CurrentColumn = 0;
            CurrentLine = 0;

            Tokens = new();
            UnicodeCharacters = new();

            Warnings = new();

            Settings = settings;

            SavedUnicode = null;
        }

        protected abstract TokenizerResult TokenizeInternal();

        Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

        /// <exception cref="TokenizerException"/>
        protected static void CheckTokens(Token[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            { CheckToken(tokens[i]); }
        }
        /// <exception cref="TokenizerException"/>
        static void CheckToken(Token token)
        {
            if (token.TokenType == TokenType.LiteralCharacter)
            {
                if (token.Content.Length > 1)
                { throw new TokenizerException($"I think there are more characters than there should be ({token.Content.Length})", token.Position); }
                else if (token.Content.Length < 1)
                { throw new TokenizerException($"I think there are less characters than there should be ({token.Content.Length})", token.Position); }
            }
        }

        void RefreshTokenPosition(int offsetTotal)
        {
            CurrentToken.Position.Range.End.Character = CurrentColumn;
            CurrentToken.Position.Range.End.Line = CurrentLine;
            CurrentToken.Position.AbsoluteRange.End = offsetTotal;
        }

        protected static List<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
        {
            List<Token> result = new(tokens.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                Token lastToken = result[^1];

                if (token.TokenType == TokenType.Whitespace && lastToken.TokenType == TokenType.Whitespace)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous,
                        lastToken.Position);
                    continue;
                }

                if (token.TokenType == TokenType.LineBreak && lastToken.TokenType == TokenType.LineBreak && settings.JoinLinebreaks)
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
