namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public class RuntimeException : LanguageException
{
    const int CallStackIndent = 1;

    public RuntimeContext? Context { get; set; }
    public DebugInformation? DebugInformation { get; set; }

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

        string? arrows = null;
        if (DebugInformation?.TryGetSourceLocation(context.Registers.CodePointer, out SourceCodeLocation sourcePosition) ?? false)
        {
            Position = sourcePosition.SourcePosition;
            File = sourcePosition.Uri;
            if (sourcePosition.Uri is not null &&
                DebugInformation.OriginalFiles.TryGetValue(sourcePosition.Uri, out ImmutableArray<Tokenizing.Token> tokens))
            { arrows = GetArrows(sourcePosition.SourcePosition, tokens); }
        }
        else
        { Position = Position.UnknownPosition; }

        ImmutableArray<FunctionInformation> callStack = DebugInformation?.GetFunctionInformation(context.CallTrace).ToImmutableArray() ?? ImmutableArray<FunctionInformation>.Empty;

        File ??= callStack.LastOrDefault().File;

        StringBuilder result = new(Message);

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (File != null)
        { result.Append($" (in {File})"); }

        result.AppendLine();

        if (arrows is not null)
        {
            result.Append(arrows);
            result.AppendLine();
            result.AppendLine();
        }

        void AppendScope(int codePointer)
        {
            IEnumerable<ScopeInformation>? scopes = DebugInformation?.GetScopes(codePointer);
            if (scopes is null) return;

            int scopeDepth = 0;
            foreach (ScopeInformation scope in scopes.Reverse())
            {
                result.Append(' ', CallStackIndent);
                result.Append(' ', scopeDepth);
                result.AppendLine("{");
                scopeDepth++;
                foreach (StackElementInformation item in scope.Stack)
                {
                    Range<int> range = item.GetRange(context.Registers.BasePointer, context.StackStart);
                    if (range.Start > range.End)
                    { range = new Range<int>(range.End, range.Start); }
                    ImmutableArray<byte> value = context.Memory[range.Start..(range.End + 1)];
                    // if (item.BasePointerRelative)
                    // { value = context.Memory.Slice(context.Registers.BasePointer + item.Address, item.Size); }
                    // else
                    // { value = context.Memory.Slice(0 - item.Address, item.Size); }
                    result.Append(' ', CallStackIndent);
                    result.Append(' ', scopeDepth);
                    result.Append(item.Type.ToString());
                    result.Append(' ');
                    result.Append(item.Tag);
                    result.Append(' ');
                    if (item.Type.Equals(Compiler.BasicType.Integer))
                    { result.Append(value.To<int>()); }
                    else if (item.Type.Equals(Compiler.BasicType.Float))
                    { result.Append(value.To<float>()); }
                    else if (item.Type.Equals(Compiler.BasicType.Char))
                    { result.Append($"'{value.To<char>().Escape()}'"); }
                    else if (item.Type.Equals(Compiler.BasicType.Byte))
                    { result.Append(value.To<byte>()); }
                    else if (item.Type is Compiler.PointerType)
                    { result.Append(value.To<int>()); }
                    else
                    { result.Append(string.Join(' ', value)); }
                    result.AppendLine();
                }
            }
            for (int i = 0; i < scopeDepth; i++)
            {
                result.Append(' ', CallStackIndent);
                result.Append(' ', scopeDepth - i - 1);
                result.AppendLine("}");
            }
            result.AppendLine();
        }

        result.AppendLine();
        result.AppendLine("Call Stack (from last to recent):");
        if (context.CallTrace.Length == 0)
        {
            result.Append(' ', CallStackIndent);
            result.AppendLine("<empty>");
        }
        else
        {
            if (!callStack.IsDefaultOrEmpty)
            {
                for (int i = 0; i < callStack.Length; i++)
                {
                    result.Append(' ', CallStackIndent);
                    result.Append(callStack[i].ToString() ?? $"<unknown> {context.CallTrace[i]}");
                    result.AppendLine();

                    AppendScope(context.CallTrace[i]);
                }
            }
        }

        FunctionInformation currentFrame = DebugInformation?.GetFunctionInformation(context.Registers.CodePointer) ?? default;
        result.Append(' ', CallStackIndent);
        result.Append(currentFrame.ToString());
        result.Append(" (current)");
        result.AppendLine();

        AppendScope(context.Registers.CodePointer);

        return result.ToString();
    }
}
