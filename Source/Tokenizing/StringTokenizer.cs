namespace LanguageCore.Tokenizing
{
    public class StringTokenizer : Tokenizer
    {
        readonly string Text;

        StringTokenizer(TokenizerSettings settings, string? text) : base(settings)
        {
            Text = text ?? string.Empty;
        }

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(string? text)
            => Tokenize(text, TokenizerSettings.Default);

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(string? text, TokenizerSettings settings)
            => new StringTokenizer(settings, text).TokenizeInternal();

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        /// <exception cref="System.Exception"/>
        protected override TokenizerResult TokenizeInternal()
        {
            for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
            {
                ProcessCharacter(Text[offsetTotal], offsetTotal, out bool breakLine, out bool returnLine);

                CurrentColumn++;
                if (breakLine) CurrentLine++;
                if (returnLine) CurrentColumn = 0;
            }

            EndToken(Text.Length);

            CheckTokens(Tokens.ToArray());

            return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToArray(), UnicodeCharacters.ToArray(), Warnings.ToArray());
        }
    }
}
