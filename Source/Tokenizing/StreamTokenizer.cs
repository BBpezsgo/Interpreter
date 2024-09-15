using System.IO;

namespace LanguageCore.Tokenizing;

public class StreamTokenizer : Tokenizer,
    IDisposable
{
    readonly Stream Stream;
    bool IsDisposed;

    StreamTokenizer(TokenizerSettings settings, Stream stream, Uri? file, IEnumerable<string>? preprocessorVariables) : base(settings, file, preprocessorVariables)
    {
        Stream = stream;
    }

    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="PathTooLongException"/>
    /// <exception cref="DirectoryNotFoundException"/>
    /// <exception cref="UnauthorizedAccessException"/>
    /// <exception cref="FileNotFoundException"/>
    /// <exception cref="System.NotSupportedException"/>
    /// <exception cref="IOException"/>
    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(string file, IEnumerable<string>? preprocessorVariables = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null)
    {
        FileStream stream = System.IO.File.OpenRead(file);
        return StreamTokenizer.Tokenize(stream, preprocessorVariables, new Uri(file), settings, progress, (int)stream.Length);
    }

    /// <inheritdoc cref="TokenizeInternal"/>
    public static TokenizerResult Tokenize(Stream stream, IEnumerable<string>? preprocessorVariables = null, Uri? file = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null, int? totalBytes = null)
    {
        settings ??= TokenizerSettings.Default;

        using StreamTokenizer tokenizer = new(settings.Value, stream, file, preprocessorVariables);

        if (progress.HasValue && totalBytes.HasValue)
        { return tokenizer.TokenizeInternal(progress.Value, totalBytes.Value); }
        else
        { return tokenizer.TokenizeInternal(); }
    }

    /// <exception cref="InternalException"/>
    /// <exception cref="TokenizerException"/>
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
                ProcessCharacter(block[i], offsetTotal);
                offsetTotal++;
            }
        }

        EndToken(offsetTotal);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToImmutableArray(), UnicodeCharacters, Warnings);
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
                ProcessCharacter(block[i], offsetTotal);
                offsetTotal++;
            }

            totalBytesRead += read;

            progress.Print(totalBytesRead, total);
        }

        EndToken(offsetTotal);

        progress.Print(1f);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToImmutableArray(), UnicodeCharacters, Warnings);
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
