using System;
using System.IO;

namespace LanguageCore.Tokenizing
{
    public class StreamTokenizer : Tokenizer, IDisposable
    {
        readonly TextReader InputStream;
        bool IsDisposed;

        StreamTokenizer(TokenizerSettings settings, TextReader inputStream) : base(settings)
        { InputStream = inputStream; }

        /// <inheritdoc cref="Tokenize(TextReader, TokenizerSettings)"/>
        public static TokenizerResult Tokenize(string file)
            => StreamTokenizer.Tokenize(new StreamReader(file), TokenizerSettings.Default);

        /// <inheritdoc cref="Tokenize(TextReader, TokenizerSettings)"/>
        public static TokenizerResult Tokenize(string file, TokenizerSettings settings)
            => StreamTokenizer.Tokenize(new StreamReader(file), settings);

        /// <inheritdoc cref="Tokenize(TextReader, TokenizerSettings)"/>
        public static TokenizerResult Tokenize(TextReader inputStream)
            => StreamTokenizer.Tokenize(inputStream, TokenizerSettings.Default);

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public static TokenizerResult Tokenize(TextReader inputStream, TokenizerSettings settings)
        {
            using StreamTokenizer tokenizer = new(settings, inputStream);
            return tokenizer.TokenizeInternal();
        }

        protected override TokenizerResult TokenizeInternal()
        {
            int offsetTotal = 0;
            while (true)
            {
                int next = InputStream.Read();
                if (next == -1) break;
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
            { InputStream.Dispose(); }

            IsDisposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
