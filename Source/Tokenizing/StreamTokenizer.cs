using System.IO;

namespace LanguageCore.Tokenizing;

public class StreamTokenizer : Tokenizer,
    IDisposable
{
    readonly Stream Stream;
    bool IsDisposed;

    StreamTokenizer(TokenizerSettings settings, Stream stream, Uri? file) : base(settings, file)
    { Stream = stream; }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string filePath)
        => StreamTokenizer.Tokenize(System.IO.File.OpenRead(filePath), new Uri(filePath), TokenizerSettings.Default);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string filePath, TokenizerSettings settings)
        => StreamTokenizer.Tokenize(System.IO.File.OpenRead(filePath), new Uri(filePath), settings);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(Stream stream, Uri? file)
        => StreamTokenizer.Tokenize(stream, file, TokenizerSettings.Default);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(Stream stream, Uri? file, TokenizerSettings settings)
    {
        using StreamTokenizer tokenizer = new(settings, stream, file);
        return tokenizer.TokenizeInternal();
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string filePath, ConsoleProgressBar progress)
    {
        FileStream stream = System.IO.File.OpenRead(filePath);
        return StreamTokenizer.Tokenize(stream, new Uri(filePath), TokenizerSettings.Default, progress, (int)stream.Length);
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string filePath, TokenizerSettings settings, ConsoleProgressBar progress)
    {
        FileStream stream = System.IO.File.OpenRead(filePath);
        return StreamTokenizer.Tokenize(stream, new Uri(filePath), settings, progress, (int)stream.Length);
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(Stream stream, Uri? file, ConsoleProgressBar progress, int totalBytes)
        => StreamTokenizer.Tokenize(stream, file, TokenizerSettings.Default, progress, totalBytes);

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(Stream stream, Uri? file, TokenizerSettings settings, ConsoleProgressBar progress, int totalBytes)
    {
        using StreamTokenizer tokenizer = new(settings, stream, file);
        return tokenizer.TokenizeInternal(progress, totalBytes);
    }

    TokenizerResult TokenizeInternal()
    {
        int offsetTotal = 0;
        Span<byte> buffer = stackalloc byte[64];

        while (true)
        {
            int read = Stream.Read(buffer);
            if (read == 0)
            { break; }

            UTF8Encoding temp = new(true);
            string block = temp.GetString(buffer[..read]);

            for (int i = 0; i < block.Length; i++)
            {
                offsetTotal++;
                ProcessCharacter(block[i], offsetTotal);
            }
        }

        EndToken(offsetTotal);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters.ToArray(), Warnings.ToArray());
    }

    TokenizerResult TokenizeInternal(ConsoleProgressBar progress, int total)
    {
        int offsetTotal = 0;
        int totalBytesRead = 0;
        Span<byte> buffer = stackalloc byte[64];

        while (true)
        {
            int read = Stream.Read(buffer);
            if (read == 0)
            { break; }

            UTF8Encoding temp = new(true);
            string block = temp.GetString(buffer[..read]);

            for (int i = 0; i < block.Length; i++)
            {
                offsetTotal++;
                ProcessCharacter(block[i], offsetTotal);
            }

            totalBytesRead += read;

            progress.Print(totalBytesRead, total);
        }

        EndToken(offsetTotal);

        progress.Print(1f);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters.ToArray(), Warnings.ToArray());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing)
        { Stream.Dispose(); }

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
