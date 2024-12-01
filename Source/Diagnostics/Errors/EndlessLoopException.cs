namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class EndlessLoopException : InternalExceptionWithoutContext
{
    public EndlessLoopException() : base("Endless loop") { }
}
