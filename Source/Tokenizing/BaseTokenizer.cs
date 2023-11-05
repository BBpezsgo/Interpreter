namespace LanguageCore.Tokenizing
{
    public abstract class BaseToken : IThingWithPosition
    {
        public abstract Position Position { get; }
    }
}
