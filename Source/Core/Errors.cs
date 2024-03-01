namespace LanguageCore;

using Runtime;

#region Exception

public class LanguageException : Exception
{
    public Position Position { get; }
    public Uri? Uri { get; }

    protected LanguageException(string message, Position position) : base(message)
    {
        Position = position;
        Uri = null;
    }

    protected LanguageException(string message, Position position, Uri? uri) : base(message)
    {
        Position = position;
        Uri = uri;
    }

    public LanguageException(Error error) : this(error.Message, error.Position, error.Uri) { }

    public LanguageException(string message, Exception inner) : base(message, inner) { }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool(" (at ", ")"));

        if (Uri != null)
        { result.Append($" (in {Uri})"); }

        if (InnerException != null)
        { result.Append($" {InnerException}"); }

        return result.ToString();
    }

    public string? GetArrows()
    {
        if (Uri == null) return null;
        if (!Uri.IsFile) return null;
        return GetArrows(Position, System.IO.File.ReadAllText(Uri.LocalPath));
    }

    public static string? GetArrows(Position position, string text)
    {
        if (position.AbsoluteRange == 0) return null;
        if (position == Position.UnknownPosition) return null;
        if (position.Range.Start.Line != position.Range.End.Line)
        { return null; }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        if (position.Range.Start.Line >= lines.Length)
        { return null; }

        string line = lines[position.Range.Start.Line];

        StringBuilder result = new();

        result.Append(line.Replace('\t', ' '));
        result.AppendLine();
        result.Append(' ', Math.Max(0, position.Range.Start.Character));
        result.Append('^', Math.Max(1, position.Range.End.Character - position.Range.Start.Character));
        return result.ToString();
    }
}

public class CompilerException : LanguageException
{
    public CompilerException(string message) : base(message, Position.UnknownPosition) { }
    public CompilerException(string message, Uri? uri) : base(message, Position.UnknownPosition, uri) { }
    public CompilerException(string message, Position position) : base(message, position) { }
    public CompilerException(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public CompilerException(string message, IPositioned? position) : base(message, position?.Position ?? Position.UnknownPosition) { }
    public CompilerException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}

public class NotSupportedException : CompilerException
{
    public NotSupportedException(string message) : base(message) { }
    public NotSupportedException(string message, Position position, Uri? file) : base(message, position, file) { }
    public NotSupportedException(string message, IPositioned? position, Uri? uri) : base(message, position, uri) { }
}

public class TokenizerException : LanguageException
{
    public TokenizerException(string message, Position position) : base(message, position) { }
}

public class SyntaxException : LanguageException
{
    public SyntaxException(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public SyntaxException(string message, Position? position, Uri? uri) : base(message, position ?? Position.UnknownPosition, uri) { }
    public SyntaxException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
}

public class ProcessRuntimeException : Exception
{
    public uint ExitCode { get; }

    ProcessRuntimeException(uint exitCode, string message) : base(message)
    {
        ExitCode = exitCode;
    }

    public static bool TryGetFromExitCode(int exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
        => ProcessRuntimeException.TryGetFromExitCode(unchecked((uint)exitCode), out processRuntimeException);

    public static bool TryGetFromExitCode(uint exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
    {
        processRuntimeException = exitCode switch
        {
            0xC0000094 => new ProcessRuntimeException(exitCode, "Integer division by zero"),
            _ => null,
        };
        return processRuntimeException != null;
    }
}

public class RuntimeException : LanguageException
{
    public RuntimeContext? Context;
    public Position SourcePosition;
    public Uri? SourceFile;
    public FunctionInformations[]? CallStack;
    public FunctionInformations? CurrentFrame;

    public void FeedDebugInfo(DebugInformation debugInfo)
    {
        if (!Context.HasValue) return;
        RuntimeContext context = Context.Value;

        if (!debugInfo.TryGetSourceLocation(context.CodePointer, out SourceCodeLocation sourcePosition))
        { SourcePosition = Position.UnknownPosition; }
        else
        { SourcePosition = sourcePosition.SourcePosition; }

        CurrentFrame = debugInfo.GetFunctionInformations(context.CodePointer);

        CallStack = debugInfo.GetFunctionInformations(context.CallTrace);

        SourceFile = CallStack.Length > 0 ? CallStack[^1].File : null;
    }

    public RuntimeException(string message) : base(message, Position.UnknownPosition) { }
    public RuntimeException(string message, Exception inner) : base(message, inner) { }
    public RuntimeException(string message, RuntimeContext context) : base(message, Position.UnknownPosition)
    {
        Context = context;
    }
    public RuntimeException(string message, Exception inner, RuntimeContext context) : this(message, inner)
    {
        Context = context;
    }

    public override string ToString()
    {
        if (!Context.HasValue) return Message + " (no context)";
        RuntimeContext context = Context.Value;

        StringBuilder result = new(Message);

        result.Append(SourcePosition.ToStringCool(" (at ", ")"));

        if (SourceFile != null)
        { result.Append($" (in {SourceFile})"); }

        result.Append(Environment.NewLine);
        result.Append($"Code Pointer: ");
        result.Append(context.CodePointer);

        result.Append(Environment.NewLine);
        result.Append("Call Stack:");
        if (context.CallTrace.Length == 0)
        { result.Append(" (CallTrace is empty)"); }
        else
        {
            result.AppendLine();
            result.Append('\t');
            result.Append(' ');
            if (CallStack == null)
            { result.Append(string.Join("\n   ", context.CallTrace)); }
            else
            { result.Append(string.Join("\n   ", CallStack)); }
        }

        if (CurrentFrame.HasValue)
        {
            result.AppendLine();
            result.Append('\t');
            result.Append(' ');
            result.Append(CurrentFrame.Value.ToString());
            result.Append(" (current)");
        }

        return result.ToString();
    }
}

public class UserException : RuntimeException
{
    public UserException(string message) : base(message)
    { }

    public override string ToString()
    {
        if (!Context.HasValue) return Message + " (no context)";
        RuntimeContext context = Context.Value;

        StringBuilder result = new(Message);

        result.Append(SourcePosition.ToStringCool(" (at ", ")"));

        if (SourceFile != null)
        { result.Append($" (in {SourceFile})"); }

        if (context.CallTrace.Length != 0)
        {
            result.AppendLine();
            result.Append('\t');
            result.Append(' ');
            if (CallStack == null)
            { result.Append(string.Join("\n   ", context.CallTrace)); }
            else
            { result.Append(string.Join("\n   ", CallStack)); }
        }

        if (CurrentFrame.HasValue)
        {
            result.AppendLine();
            result.Append('\t');
            result.Append(' ');
            result.Append(CurrentFrame.Value.ToString());
            result.Append(" (current)");
        }

        return result.ToString();
    }
}

#endregion

#region InternalException

/// <summary> If this exception raised, it's a <b>big</b> problem. </summary>
public class InternalException : LanguageException
{
    public InternalException() : base(string.Empty, Position.UnknownPosition) { }
    public InternalException(string message) : base(message, Position.UnknownPosition) { }
    public InternalException(string message, Uri? uri) : base(message, Position.UnknownPosition, uri) { }
    public InternalException(string message, IPositioned position, Uri? uri) : base(message, position.Position, uri) { }
}

/// <inheritdoc/>
public class EndlessLoopException : InternalException
{
    public EndlessLoopException() : base("Endless loop") { }
}

#endregion

#region NotExceptionBut

public class NotExceptionBut
{
    public string Message { get; }
    public Position Position { get; }
    public Uri? Uri { get; }

    protected NotExceptionBut(string message, Position position, Uri? file)
    {
        Message = message;
        Position = position;
        Uri = file;
    }

    public string? GetArrows()
    {
        if (Uri == null) return null;
        if (!Uri.IsFile) return null;
        return LanguageException.GetArrows(Position, System.IO.File.ReadAllText(Uri.LocalPath));
    }

    public override string ToString()
    {
        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool(" (at ", ")"));
        if (Uri != null)
        { result.Append($" (in {Uri})"); }

        return result.ToString();
    }
}

public class Error : NotExceptionBut
{
    public Error(string message, Position position, Uri? uri) : base(message, position, uri)
    {
#if DEBUG
        Debugger.Break();
#endif
    }

    public Error(string message, Position? position, Uri? uri) : this(message, position ?? Position.UnknownPosition, uri) { }
    public Error(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }

    public LanguageException ToException() => new(this);
}

public class Warning : NotExceptionBut
{
    public Warning(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Warning(string message, Position? position, Uri? uri) : this(message, position ?? Position.UnknownPosition, uri) { }
    public Warning(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }
}

public class Information : NotExceptionBut
{
    public Information(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Information(string message, Position? position, Uri? uri) : this(message, position ?? Position.UnknownPosition, uri) { }
    public Information(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }
}

public class Hint : NotExceptionBut
{
    public Hint(string message, Position position, Uri? uri) : base(message, position, uri) { }
    public Hint(string message, Position? position, Uri? uri) : this(message, position ?? Position.UnknownPosition, uri) { }
    public Hint(string message, IPositioned? position, Uri? uri) : this(message, position?.Position ?? Position.UnknownPosition, uri) { }
}

#endregion
