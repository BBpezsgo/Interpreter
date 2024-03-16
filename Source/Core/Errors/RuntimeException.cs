namespace LanguageCore.Runtime;

public class RuntimeException : LanguageException
{
    public RuntimeContext? Context;
    public Position SourcePosition;
    public Uri? SourceFile;
    public ImmutableArray<FunctionInformations> CallStack;
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

        CallStack = debugInfo.GetFunctionInformations(context.CallTrace).ToImmutableArray();

        SourceFile = CallStack.Length > 0 ? CallStack[^1].File : null;
    }

    public RuntimeException(string message) : base(message, Position.UnknownPosition, null) { }
    public RuntimeException(string message, Exception inner) : base(message, inner) { }
    public RuntimeException(string message, RuntimeContext context) : base(message, Position.UnknownPosition, null)
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
            if (CallStack == default)
            { result.AppendJoin("\n   ", context.CallTrace); }
            else
            { result.AppendJoin("\n   ", CallStack); }
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
