namespace ProgrammingLanguage.Tokenizer
{
    using Core;

    public class BaseToken : IThingWithPosition
    {
        public Range<SinglePosition> Position;
        public Range<int> AbsolutePosition;

        public Position GetPosition() => new(Position.Start.Line, Position.Start.Character, AbsolutePosition);
    }
}
