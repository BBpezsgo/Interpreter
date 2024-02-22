using System;
using System.IO;

namespace LanguageCore.Tokenizing
{
    public class StreamTokenizer : Tokenizer, IDisposable
    {
        readonly TextReader Stream;
        bool IsDisposed;

        StreamTokenizer(TokenizerSettings settings, TextReader stream) : base(settings)
        { Stream = stream; }

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(string filePath)
            => StreamTokenizer.Tokenize(new StreamReader(filePath), TokenizerSettings.Default);

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(string filePath, TokenizerSettings settings)
            => StreamTokenizer.Tokenize(new StreamReader(filePath), settings);

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(TextReader stream)
            => StreamTokenizer.Tokenize(stream, TokenizerSettings.Default);

        /// <inheritdoc cref="TokenizeInternal"/>
        public static TokenizerResult Tokenize(TextReader stream, TokenizerSettings settings)
        {
            using StreamTokenizer tokenizer = new(settings, stream);
            return tokenizer.TokenizeInternal();
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        /// <exception cref="Exception"/>
        protected override TokenizerResult TokenizeInternal()
        {
            int offsetTotal = 0;
            int next;

            while ((next = Stream.Read()) != -1)
            {
                offsetTotal++;

                ProcessCharacter((char)next, offsetTotal, out bool breakLine, out bool returnLine);

                CurrentColumn++;
                if (breakLine) CurrentLine++;
                if (returnLine) CurrentLine = 0;
            }

            EndToken(offsetTotal);

            CheckTokens(Tokens.ToArray());

            return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToArray(), UnicodeCharacters.ToArray(), Warnings.ToArray());
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
}
