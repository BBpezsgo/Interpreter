namespace LanguageCore.Tokenizing;

public sealed class StringTokenizer : Tokenizer
{
    readonly string Text;

    StringTokenizer(TokenizerSettings settings, string? text, Uri? file) : base(settings, file)
    {
        Text = text ?? string.Empty;
    }

    StringTokenizer(TokenizerSettings settings, Uri? file) : base(settings, file)
    {
        Text = string.Empty;
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text)
        => Tokenize(text, null, TokenizerSettings.Default);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text, Uri? file)
        => Tokenize(text, file, TokenizerSettings.Default);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text, Uri? file, TokenizerSettings settings)
        => new StringTokenizer(settings, text, file).TokenizeInternal();

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string? text, Uri? file, TokenizerSettings settings, ConsoleProgressBar progress)
        => new StringTokenizer(settings, text, file).TokenizeInternal(progress);

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
