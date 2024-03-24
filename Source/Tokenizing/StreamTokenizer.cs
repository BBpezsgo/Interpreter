using System.IO;

namespace LanguageCore.Tokenizing;

public class StreamTokenizer : Tokenizer,
    IDisposable
{
    readonly Stream Stream;
    bool IsDisposed;

    StreamTokenizer(TokenizerSettings settings, Stream stream, Uri? file, IEnumerable<string> preprocessorVariables) : base(settings, file, preprocessorVariables)
    {
        Stream = stream;
    }

    public static TokenizerResult Tokenize(string filePath, IEnumerable<string> preprocessorVariables, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null)
    {
        FileStream stream = System.IO.File.OpenRead(filePath);
        return StreamTokenizer.Tokenize(stream, preprocessorVariables, new Uri(filePath), settings, progress, (int)stream.Length);
    }

    public static TokenizerResult Tokenize(Stream stream, IEnumerable<string> preprocessorVariables, Uri? file = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null, int? totalBytes = null)
    {
        settings ??= TokenizerSettings.Default;

        using StreamTokenizer tokenizer = new(settings.Value, stream, file, preprocessorVariables);

        if (progress.HasValue && totalBytes.HasValue)
        { return tokenizer.TokenizeInternal(progress.Value, totalBytes.Value); }
        else
        { return tokenizer.TokenizeInternal(); }
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

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters, Warnings);
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

        return new TokenizerResult(NormalizeTokens(Tokens, Settings), UnicodeCharacters, Warnings);
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
