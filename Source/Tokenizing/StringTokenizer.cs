namespace LanguageCore.Tokenizing;

public sealed class StringTokenizer : Tokenizer
{
    readonly string Text;

    StringTokenizer(TokenizerSettings settings, string text, Uri? file, IEnumerable<string> preprocessorVariables) : base(settings, file, preprocessorVariables)
    {
        Text = text;
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string text, IEnumerable<string> preprocessorVariables, Uri? file = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null)
    {
        settings ??= TokenizerSettings.Default;

        StringTokenizer tokenizer = new(settings.Value, text, file, preprocessorVariables);

        if (progress.HasValue)
        { return tokenizer.TokenizeInternal(progress.Value); }
        else
        { return tokenizer.TokenizeInternal(); }
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="TokenizerException"/>
    TokenizerResult TokenizeInternal()
    {
        for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
        {
            ProcessCharacter(Text[offsetTotal], offsetTotal);
        }

        EndToken(Text.Length);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters, Warnings);
    }

    TokenizerResult TokenizeInternal(ConsoleProgressBar progress)
    {
        for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
        {
            progress.Print(offsetTotal, Text.Length);
            ProcessCharacter(Text[offsetTotal], offsetTotal);
        }

        EndToken(Text.Length);

        progress.Print(1f);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters, Warnings);
    }
}
