namespace LanguageCore.Tokenizing;

public sealed class StringTokenizer : Tokenizer
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

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text, ConsoleProgressBar progress)
        => Tokenize(text, TokenizerSettings.Default, progress);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text, TokenizerSettings settings, ConsoleProgressBar progress)
        => new StringTokenizer(settings, text).TokenizeInternal(progress);

    TokenizerResult TokenizeInternal()
    {
        for (int offsetTotal = 0; offsetTotal < Text.Length; offsetTotal++)
        {
            ProcessCharacter(Text[offsetTotal], offsetTotal);
        }

        EndToken(Text.Length);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters.ToArray(), Warnings.ToArray());
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

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters.ToArray(), Warnings.ToArray());
    }
}
