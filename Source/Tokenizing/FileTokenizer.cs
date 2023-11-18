using System;
using System.IO;

namespace LanguageCore.Tokenizing
{
    public class FileTokenizer : Tokenizer, IDisposable
    {
        readonly StreamReader InputFile;
        bool IsDisposed;

        FileTokenizer(TokenizerSettings settings, string file) : base(settings)
        {
            InputFile = File.OpenText(file);
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public static TokenizerResult Tokenize(string file)
        {
            using FileTokenizer tokenizer = new(TokenizerSettings.Default, file);
            return tokenizer.TokenizeInternal();
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        public static TokenizerResult Tokenize(string file, TokenizerSettings settings)
        {
            using FileTokenizer tokenizer = new(settings, file);
            return tokenizer.TokenizeInternal();
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="TokenizerException"/>
        protected override TokenizerResult TokenizeInternal()
        {
            int offsetTotal = 0;
            while (true)
            {
                int next = InputFile.Read();
                if (next == -1) break;
                offsetTotal++;

                ProcessCharacter((char)next, offsetTotal, out bool breakLine);

                CurrentColumn++;
                if (breakLine)
                {
                    CurrentColumn = 0;
                    CurrentLine++;
                }
            }

            EndToken(offsetTotal);

            CheckTokens(Tokens.ToArray());

            return new TokenizerResult(NormalizeTokens(Tokens, Settings).ToArray(), UnicodeCharacters.ToArray(), Warnings.ToArray());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed) return;

            if (disposing)
            { InputFile.Dispose(); }

            IsDisposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
