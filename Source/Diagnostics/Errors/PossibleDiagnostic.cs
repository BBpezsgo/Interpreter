namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class PossibleDiagnostic
{
    public string Message { get; }

    readonly bool _isPopulated;
    readonly Position _position;
    readonly Uri? _file;
    readonly PossibleDiagnostic[] _suberrors;

    public PossibleDiagnostic(string message, params PossibleDiagnostic[] suberrors)
    {
        Message = message;
        _isPopulated = false;
        _suberrors = suberrors;
    }

    public PossibleDiagnostic(string message, ILocated location, params PossibleDiagnostic[] suberrors)
        : this(message, location.Location.Position, location.Location.File, suberrors)
    { }

    public PossibleDiagnostic(string message, IPositioned position, Uri file, params PossibleDiagnostic[] suberrors)
        : this(message, position.Position, file, suberrors)
    { }

    PossibleDiagnostic(string message, Position position, Uri file, params PossibleDiagnostic[] suberrors)
    {
        Message = message;
        _isPopulated = true;
        _position = position;
        _file = file;
        _suberrors = suberrors;
    }

    public void Throw()
    {
        if (_isPopulated)
        { throw new LanguageException(Message, _position, _file!); }
        else
        { throw new LanguageExceptionWithoutContext(Message); }
    }

    public PossibleDiagnostic TrySetLocation(ILocated location)
    {
        if (_isPopulated) return this;
        return new(Message, location, _suberrors);
    }

    public Diagnostic ToError(IPositioned position, Uri file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file!, _suberrors.Select(v => v.ToError(position, file))) :
        new(DiagnosticsLevel.Error, Message, position.Position, file, _suberrors.Select(v => v.ToError(position, file)));

    public Diagnostic ToWarning(IPositioned position, Uri file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file!, _suberrors.Select(v => v.ToWarning(position, file))) :
        new(DiagnosticsLevel.Warning, Message, position.Position, file, _suberrors.Select(v => v.ToWarning(position, file)));

    public Diagnostic ToError(ILocated location) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file!, _suberrors.Select(v => v.ToError(location))) :
        new(DiagnosticsLevel.Error, Message, location.Location.Position, location.Location.File, _suberrors.Select(v => v.ToError(location)));

    public Diagnostic ToWarning(ILocated location) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file!, _suberrors.Select(v => v.ToWarning(location))) :
        new(DiagnosticsLevel.Warning, Message, location.Location.Position, location.Location.File, _suberrors.Select(v => v.ToWarning(location)));

    public override string ToString() => Message;
}
