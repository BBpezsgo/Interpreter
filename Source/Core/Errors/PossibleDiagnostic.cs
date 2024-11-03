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

    public PossibleDiagnostic(string message, IPositioned position, Uri file)
        : this(message, position.Position, file)
    { }

    public PossibleDiagnostic(string message, Position position, Uri file)
    {
        Message = message;
        _isPopulated = true;
        _position = position;
        _file = file;
    }

    public Diagnostic InstantiateError(Position position, Uri? file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file) :
        new(DiagnosticsLevel.Error, Message, position, file);

    public Diagnostic InstantiateWarning(Position position, Uri? file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file) :
        new(DiagnosticsLevel.Warning, Message, position, file);

    public Diagnostic InstantiateError(IPositioned? position, Uri? file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Error, Message, _position, _file) :
        new(DiagnosticsLevel.Error, Message, position?.Position ?? Position.UnknownPosition, file);

    public Diagnostic InstantiateWarning(IPositioned position, Uri? file) =>
        _isPopulated ?
        new(DiagnosticsLevel.Warning, Message, _position, _file) :
        new(DiagnosticsLevel.Warning, Message, position.Position, file);

    public override string ToString() => Message;
}
