namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class InternalExceptionWithoutContext : LanguageExceptionWithoutContext
{
    public InternalExceptionWithoutContext() : base(string.Empty) { }
    public InternalExceptionWithoutContext(string message) : base(message) { }
}
