namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class LanguageExceptionWithoutContext : Exception
{
    public LanguageExceptionWithoutContext(string message) : base(message) { }
    public LanguageExceptionWithoutContext(string message, Exception inner) : base(message, inner) { }
}
