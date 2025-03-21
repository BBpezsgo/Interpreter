﻿using System.IO;

namespace LanguageCore.Tokenizing;

public class StreamTokenizer : Tokenizer,
    IDisposable
{
    readonly Stream Stream;
    bool IsDisposed;

    StreamTokenizer(TokenizerSettings settings, Stream stream, Uri? file, IEnumerable<string>? preprocessorVariables, DiagnosticsCollection diagnostics) : base(settings, file, preprocessorVariables, diagnostics)
    {
        Stream = stream;
    }

    public static TokenizerResult Tokenize(string file, DiagnosticsCollection diagnostics, IEnumerable<string>? preprocessorVariables = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null)
    {
        FileStream stream = System.IO.File.OpenRead(file);
        return StreamTokenizer.Tokenize(stream, diagnostics, preprocessorVariables, new Uri(file), settings, progress, (int)stream.Length);
    }

    public static TokenizerResult Tokenize(Stream stream, DiagnosticsCollection diagnostics, IEnumerable<string>? preprocessorVariables = null, Uri? file = null, TokenizerSettings? settings = null, ConsoleProgressBar? progress = null, int? totalBytes = null)
    {
        settings ??= TokenizerSettings.Default;

        using StreamTokenizer tokenizer = new(settings.Value, stream, file, preprocessorVariables, diagnostics);

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
                ProcessCharacter(block[i], offsetTotal);
                offsetTotal++;
            }
        }

        EndToken(offsetTotal);

        return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToImmutableArray(), UnicodeCharacters);
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

        return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToImmutableArray(), UnicodeCharacters);
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
