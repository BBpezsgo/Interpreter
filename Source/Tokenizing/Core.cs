using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.Tokenizing
{
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
        ConstantName,
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

    public readonly struct TokenizerResult
    {
        public readonly Token[] Tokens;
        public readonly SimpleToken[] UnicodeCharacterTokens;
        public readonly Warning[] Warnings;

        public TokenizerResult(
            IEnumerable<Token> tokens,
            IEnumerable<SimpleToken> unicodeCharacterTokens,
            IEnumerable<Warning> warnings)
        {
            Tokens = tokens.ToArray();
            UnicodeCharacterTokens = unicodeCharacterTokens.ToArray();
            Warnings = warnings.ToArray();
        }

        public static TokenizerResult Empty => new(
            Enumerable.Empty<Token>(),
            Enumerable.Empty<SimpleToken>(),
            Enumerable.Empty<Warning>());

        public static implicit operator Token[](TokenizerResult result) => result.Tokens;
    }

    public abstract partial class Tokenizer
    {
        static readonly char[] Bracelets = ['{', '}', '(', ')', '[', ']'];
        static readonly char[] Operators = ['+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&', '~'];
        static readonly string[] DoubleOperators = ["++", "--", "<<", ">>", "&&", "||"];
        static readonly char[] SimpleOperators = [';', ',', '#'];

        protected readonly List<Token> Tokens;
        protected readonly List<SimpleToken> UnicodeCharacters;
        protected readonly List<Warning> Warnings;
        protected readonly TokenizerSettings Settings;

        readonly PreparationToken CurrentToken;
        int CurrentColumn;
        int CurrentLine;
        char PreviousChar;
        string? SavedUnicode;

        protected Tokenizer(TokenizerSettings settings)
        {
            CurrentToken = new(default);
            CurrentColumn = 0;
            CurrentLine = 0;
            PreviousChar = default;

            Tokens = new();
            UnicodeCharacters = new();

            Warnings = new();

            Settings = settings;

            SavedUnicode = null;
        }

        Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

        void RefreshTokenPosition(int offsetTotal)
        {
            CurrentToken.Position.Range.End.Character = CurrentColumn;
            CurrentToken.Position.Range.End.Line = CurrentLine;
            CurrentToken.Position.AbsoluteRange.End = offsetTotal;
        }

        protected static Token[] NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
        {
            List<Token> result = new(tokens.Count);

            foreach (Token token in tokens)
            {
                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                Token lastToken = result[^1];

                if (token.TokenType == TokenType.Whitespace &&
                    lastToken.TokenType == TokenType.Whitespace)
                {
                    result[^1] = lastToken + token;
                    continue;
                }

                if (settings.JoinLinebreaks &&
                    token.TokenType == TokenType.LineBreak &&
                    lastToken.TokenType == TokenType.LineBreak)
                {
                    result[^1] = lastToken + token;
                    continue;
                }

                result.Add(token);
            }

            return result.ToArray();
        }
    }
}
