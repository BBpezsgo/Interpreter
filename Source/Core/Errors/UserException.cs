namespace LanguageCore.Runtime;

public sealed class UserException : RuntimeException
{
    public UserException(string message) : base(message) { }

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
