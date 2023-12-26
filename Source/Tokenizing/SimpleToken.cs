namespace LanguageCore.Tokenizing
{
    public readonly struct SimpleToken : IPositioned
    {
        public readonly string Content;
        readonly Position position;

        public SimpleToken(string content, Position position)
        {
            this.Content = content;
            this.position = position;
        }

        public override string ToString() => Content;

        public Position Position => position;
    }
}
