using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck;

[ExcludeFromCodeCoverage]
public readonly struct RuntimeContext
{
    public readonly int MemoryPointer;
    public readonly int CodePointer;

    public RuntimeContext(int memoryPointer, int codePointer)
    {
        MemoryPointer = memoryPointer;
        CodePointer = codePointer;
    }
}

[ExcludeFromCodeCoverage]
public class BrainfuckRuntimeException : Exception
{
    public readonly RuntimeContext RuntimeContext;
    readonly Runtime.DebugInformation? DebugInfo;

    public BrainfuckRuntimeException(string message, RuntimeContext context, Runtime.DebugInformation? debugInfo) : base(message)
    {
        RuntimeContext = context;
        DebugInfo = debugInfo;
    }

    public override string ToString()
    {
        if (DebugInfo is null)
        {
            return Message;
        }

        Position position;
        if (!DebugInfo.TryGetSourceLocation(RuntimeContext.CodePointer, out SourceCodeLocation sourcePosition))
        { position = Position.UnknownPosition; }
        else
        { position = sourcePosition.SourcePosition; }

        string? arrows = null;
        if (sourcePosition.Uri is not null &&
            DebugInfo.OriginalFiles.TryGetValue(sourcePosition.Uri, out ImmutableArray<Tokenizing.Token> tokens))
        { arrows = LanguageException.GetArrows(sourcePosition.SourcePosition, tokens); }

        FunctionInformation currentFrame = DebugInfo.GetFunctionInformation(RuntimeContext.CodePointer);

        StringBuilder result = new(Message);

        result.Append(position.ToStringCool().Surround(" (at ", ")"));

        if (sourcePosition.Uri != null)
        { result.Append($" (in {sourcePosition.Uri})"); }

        result.AppendLine();

        if (arrows is not null)
        {
            result.Append(arrows);
            result.AppendLine();
            result.AppendLine();
        }

        result.AppendLine($"Code Pointer: {RuntimeContext.CodePointer}");
        result.AppendLine($"Memory Pointer: {RuntimeContext.MemoryPointer}");

        const int callTraceIndent = 1;

        result.AppendLine();
        result.Append("Call Stack:");

        result.AppendLine();
        result.Append(' ', callTraceIndent);
        result.Append(currentFrame.ToString());
        result.Append(" (current)");

        return result.ToString();
    }
}
