namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class PossibleDiagnostic
{
    public string Message;
    public readonly ImmutableArray<PossibleDiagnostic> SubErrors;
    readonly bool IsPopulated;
    readonly Position Position;
    readonly Uri? File;
    readonly bool ShouldBreak;

    public PossibleDiagnostic(string message, bool shouldBreak = true)
        : this(message, ImmutableArray<PossibleDiagnostic>.Empty, shouldBreak)
    { }

    public PossibleDiagnostic(string message, params PossibleDiagnostic[] suberrors)
        : this(message, suberrors.ToImmutableArray())
    { }

    public PossibleDiagnostic(string message, ImmutableArray<PossibleDiagnostic> suberrors, bool shouldBreak = true)
    {
        Message = message;
        IsPopulated = false;
        SubErrors = suberrors;
        ShouldBreak = shouldBreak;
    }

    public PossibleDiagnostic(string message, ILocated location)
        : this(message, location.Location.Position, location.Location.File, ImmutableArray<PossibleDiagnostic>.Empty)
    { }

    public PossibleDiagnostic(string message, ILocated location, params PossibleDiagnostic[] suberrors)
        : this(message, location.Location.Position, location.Location.File, suberrors.ToImmutableArray())
    { }

    public PossibleDiagnostic(string message, ILocated location, ImmutableArray<PossibleDiagnostic> suberrors)
        : this(message, location.Location.Position, location.Location.File, suberrors)
    { }

    PossibleDiagnostic(string message, Position position, Uri file, ImmutableArray<PossibleDiagnostic> suberrors)
    {
        Message = message;
        IsPopulated = true;
        Position = position;
        File = file;
        SubErrors = suberrors;
    }

    public void Throw()
    {
        if (IsPopulated)
        { throw new LanguageException(Message, Position, File!); }
        else
        { throw new LanguageExceptionWithoutContext(Message); }
    }

    public PossibleDiagnostic TrySetLocation(ILocated location)
    {
        if (IsPopulated) return this;
        return new(Message, location, SubErrors);
    }

    public Diagnostic ToError(IPositioned position, Uri file) =>
        IsPopulated ?
        new(DiagnosticsLevel.Error, Message, Position, File!, ShouldBreak, SubErrors.Select(v => v.ToError(position, file))) :
        new(DiagnosticsLevel.Error, Message, position.Position, file, ShouldBreak, SubErrors.Select(v => v.ToError(position, file)));

    public Diagnostic ToWarning(IPositioned position, Uri file) =>
        IsPopulated ?
        new(DiagnosticsLevel.Warning, Message, Position, File!, SubErrors.Select(v => v.ToWarning(position, file))) :
        new(DiagnosticsLevel.Warning, Message, position.Position, file, SubErrors.Select(v => v.ToWarning(position, file)));

    public Diagnostic ToError(ILocated location) =>
        IsPopulated ?
        new(DiagnosticsLevel.Error, Message, Position, File!, ShouldBreak, SubErrors.Select(v => v.ToError(location))) :
        new(DiagnosticsLevel.Error, Message, location.Location.Position, location.Location.File, ShouldBreak, SubErrors.Select(v => v.ToError(location)));

    public Diagnostic ToWarning(ILocated location) =>
        IsPopulated ?
        new(DiagnosticsLevel.Warning, Message, Position, File!, SubErrors.Select(v => v.ToWarning(location))) :
        new(DiagnosticsLevel.Warning, Message, location.Location.Position, location.Location.File, SubErrors.Select(v => v.ToWarning(location)));

    public override string ToString() => Message;
}
