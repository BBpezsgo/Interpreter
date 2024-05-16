﻿namespace LanguageCore.Runtime;

public class RuntimeException : LanguageException
{
    public RuntimeContext? Context;
    public ImmutableArray<FunctionInformations> CallStack;
    public FunctionInformations? CurrentFrame;
    string? Arrows;

    public void FeedDebugInfo(DebugInformation debugInfo)
    {
        if (!Context.HasValue) return;
        RuntimeContext context = Context.Value;

        if (!debugInfo.TryGetSourceLocation(context.CodePointer, out SourceCodeLocation sourcePosition))
        { Position = Position.UnknownPosition; }
        else
        { Position = sourcePosition.SourcePosition; }

        if (sourcePosition.Uri is not null &&
            debugInfo.OriginalFiles.TryGetValue(sourcePosition.Uri, out ImmutableArray<Tokenizing.Token> tokens))
        { Arrows = GetArrows(sourcePosition.SourcePosition, tokens); }

        CurrentFrame = debugInfo.GetFunctionInformations(context.CodePointer);

        CallStack = debugInfo.GetFunctionInformations(context.CallTrace).ToImmutableArray();

        Uri = sourcePosition.Uri;
        Uri ??= CallStack.Length > 0 ? CallStack[^1].File : null;
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

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (Uri != null)
        { result.Append($" (in {Uri})"); }

        result.AppendLine();

        if (Arrows is not null)
        {
            result.Append(Arrows);
            result.AppendLine();
            result.AppendLine();
        }

        result.Append($"Code Pointer: {context.CodePointer}");

        const int callTraceIndent = 1;

        result.AppendLine();
        result.Append("Call Stack:");
        if (context.CallTrace.Length == 0)
        { result.Append(" <empty>"); }
        else
        {
            if (CallStack.IsDefaultOrEmpty)
            {
                for (int i = 0; i < CallStack.Length; i++)
                {
                    result.AppendLine();
                    result.Append(' ', callTraceIndent);
                    result.Append($"<unknown> {context.CallTrace[i]}");
                }
            }
            else
            {
                for (int i = 0; i < CallStack.Length; i++)
                {
                    result.AppendLine();
                    result.Append(' ', callTraceIndent);
                    result.Append(CallStack[i].ToString() ?? $"<unknown> {context.CallTrace[i]}");
                }
            }
        }

        if (CurrentFrame.HasValue)
        {
            result.AppendLine();
            result.Append(' ', callTraceIndent);
            result.Append(CurrentFrame.Value.ToString());
            result.Append(" (current)");
        }

        return result.ToString();
    }
}
