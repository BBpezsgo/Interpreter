namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class EndlessLoopException : InternalException
{
    public EndlessLoopException() : base("Endless loop", Position.UnknownPosition, null) { }
}
