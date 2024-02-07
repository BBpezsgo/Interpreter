using System;
using System.Collections.Generic;

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
        public readonly Warning[] Warnings;

        public readonly Token[] Tokens;
        public readonly SimpleToken[] UnicodeCharacterTokens;

        public TokenizerResult(Token[] tokens, SimpleToken[] unicodeCharacterTokens, Warning[] warnings)
        {
            Tokens = tokens;
            UnicodeCharacterTokens = unicodeCharacterTokens;
            Warnings = warnings;
        }

        public static TokenizerResult Empty => new(
            Array.Empty<Token>(),
            Array.Empty<SimpleToken>(),
            Array.Empty<Warning>());

        public static implicit operator Token[](TokenizerResult result) => result.Tokens;
    }

    public abstract partial class Tokenizer
    {
        static readonly char[] Bracelets = ['{', '}', '(', ')', '[', ']'];
        static readonly char[] Operators = ['+', '-', '*', '/', '=', '<', '>', '!', '%', '^', '|', '&', '~'];
        static readonly string[] DoubleOperators = ["++", "--", "<<", ">>", "&&", "||"];
        static readonly char[] SimpleOperators = [';', ',', '#'];

        readonly PreparationToken CurrentToken;
        protected int CurrentColumn;
        protected int CurrentLine;
        char PreviousChar;

        protected readonly List<Token> Tokens;
        protected readonly List<SimpleToken> UnicodeCharacters;

        protected readonly List<Warning> Warnings;

        protected readonly TokenizerSettings Settings;

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

        protected abstract TokenizerResult TokenizeInternal();

        Position GetCurrentPosition(int offsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(offsetTotal, offsetTotal + 1));

        protected static void CheckTokens(Token[] tokens)
        {
            for (int i = 0; i < tokens.Length; i++)
            { CheckToken(tokens[i]); }
        }

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

            return result;
        }
    }
}
