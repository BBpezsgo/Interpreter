using System;
using System.Linq;
using System.Text;

namespace LanguageCore.Tokenizing
{
    public class TextSource
    {
        readonly string text;
        int position;

        public char this[int i] => text[i];

        public int Position => position;
        public bool Has => position < text.Length;
        public int Remaining => text.Length - position;

        public TextSource(string text)
        {
            this.text = text;
            this.position = 0;
        }

        public char Next() => this[position++];
        public char Peek() => this[position + 1];
        public string Peek(int length) => text.Substring(position, length);

        public bool NextIs(string match)
        {
            if (Remaining < match.Length) return false;
            return Peek(match.Length) == match;
        }

        public string ConsumeAll(params char[] characters)
        {
            StringBuilder raw = new();

            while (Has)
            {
                if (characters.Contains(Peek()))
                { raw.Append(Next()); }
            }

            return raw.ToString();
        }

        public string ConsumeAll(int maxLength, params char[] characters)
        {
            StringBuilder raw = new();

            while (Has && maxLength-- > 0)
            {
                if (characters.Contains(Peek()))
                { raw.Append(Next()); }
            }

            return raw.ToString();
        }

        public string Consume(int length)
        {
            StringBuilder raw = new(length);

            while (Has && length-- > 0)
            {
                raw.Append(Next());
            }

            return raw.ToString();
        }

        public string? ConsumeUntil(string end)
        {
            int i = text.IndexOf(end, position, StringComparison.Ordinal);
            if (i == 0)
            { return null; }
            return Consume(i - position);
        }

        public string? ConsumeUntil(params char[] end)
        {
            int i = text.IndexOfAny(end, position);
            if (i == 0)
            { return null; }
            return Consume(i - position);
        }
    }
}
