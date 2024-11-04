namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class PossibleDiagnostic
{
    public string Message { get; }

    readonly bool _isPopulated;
    readonly Position _position;
    readonly Uri? _file;

    public PossibleDiagnostic(string message)
    {
        Message = message;
        _isPopulated = false;
    }

    public PossibleDiagnostic(string message, ILocated location)
        : this(message, location.Location.Position, location.Location.File)
    { }

    public PossibleDiagnostic(string message, IPositioned position, Uri file)
        : this(message, position.Position, file)
    { }

    PossibleDiagnostic(string message, Position position, Uri file)
    {
        Message = message;
        _isPopulated = true;
        _position = position;
        _file = file;
    }

    public void Throw()
    {
        if (_isPopulated)
        { throw new LanguageException(Message, _position, _file!); }
        else
        { throw new LanguageExceptionWithoutContext(Message); }
    }

    public Diagnostic ToError(IPositioned position, Uri file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file!) :
        new(DiagnosticsLevel.Error, Message, position?.Position ?? Position.UnknownPosition, file);

    public Diagnostic ToWarning(IPositioned position, Uri file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file!) :
        new(DiagnosticsLevel.Warning, Message, position.Position, file);

    public Diagnostic ToError(ILocated location) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file!) :
        new(DiagnosticsLevel.Error, Message, location.Location.Position, location.Location.File);

    public Diagnostic ToWarning(ILocated location) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file!) :
        new(DiagnosticsLevel.Warning, Message, location.Location.Position, location.Location.File);

    public override string ToString() => Message;
}
