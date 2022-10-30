namespace IngameCoding.Tokenizer
{
    using Core;

    public class BaseToken
    {
        public int startOffset;
        public int endOffset;
        public int lineNumber;

        public int startOffsetTotal;
        public int endOffsetTotal;
        public Position Position => new(lineNumber, startOffset, new Interval(startOffsetTotal, endOffsetTotal));
    }
}
