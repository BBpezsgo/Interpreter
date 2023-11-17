namespace LanguageCore.Tokenizing
{
    public class StringTokenizer : Tokenizer
    {
        readonly string Text;

        StringTokenizer(TokenizerSettings settings, string? text) : base(settings)
        {
            Text = text ?? string.Empty;
        }

        public static TokenizerResult Tokenize(string? sourceCode)
            => new StringTokenizer(TokenizerSettings.Default, sourceCode).TokenizeInternal();

        public static TokenizerResult Tokenize(string? sourceCode, TokenizerSettings settings)
            => new StringTokenizer(settings, sourceCode).TokenizeInternal();

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        protected override TokenizerResult TokenizeInternal()
        {
            for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
            {
                ProcessCharacter(Text[offsetTotal], offsetTotal, out bool breakLine);

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
    }
}
