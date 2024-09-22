using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public class RuntimeException : LanguageException
{
    const int CallStackIndent = 1;

    public RuntimeContext? Context { get; set; }
    public DebugInformation? DebugInformation { get; set; }
    public ImmutableArray<int> CallTrace =>
        Context is null ? ImmutableArray<int>.Empty :
        DebugInformation is null ? ImmutableArray<int>.Empty :
        ImmutableArray.Create(DebugUtils.TraceCalls(Context.Value.Memory, Context.Value.Registers.BasePointer, DebugInformation.StackOffsets));

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

        ImmutableArray<int> callTrace = CallTrace;

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

        ImmutableArray<FunctionInformation> callStack = DebugInformation?.GetFunctionInformation(callTrace).ToImmutableArray() ?? ImmutableArray<FunctionInformation>.Empty;

        File ??= callStack.LastOrDefault().Function?.File;

        StringBuilder result = new();

        result.Append(Message);

        result.Append(Position.ToStringCool().Surround(" (at ", ")"));

        if (File != null)
        { result.Append($" (in {File})"); }

        result.AppendLine();

        result.ResetStyle();

        if (arrows is not null)
        {
            result.Append(arrows);
            result.AppendLine();
            result.AppendLine();
        }

        void AppendType(GeneralType type)
        {
            switch (type)
            {
                case ArrayType v:
                    AppendType(v.Of);

                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append('[');

                    if (v.ComputedLength.HasValue)
                    {
                        result.SetGraphics(Ansi.ForegroundWhite);
                        result.Append(v.ComputedLength.Value.ToString());
                    }
                    else if (v.Length is not null)
                    {
                        result.SetGraphics(Ansi.ForegroundWhite);
                        result.Append('?');
                    }

                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(']');

                    break;
                case BuiltinType v:
                    result.SetGraphics(Ansi.BrightForegroundBlue);
                    result.Append(v.ToString());

                    break;
                case FunctionType v:
                    AppendType(v.ReturnType);

                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append('(');

                    for (int i = 0; i < v.Parameters.Length; i++)
                    {
                        if (i > 0)
                        {
                            result.SetGraphics(Ansi.BrightForegroundBlack);
                            result.Append(", ");
                        }

                        AppendType(v.Parameters[i]);
                    }

                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(')');

                    break;
                case GenericType v:
                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(v.ToString());

                    break;
                case PointerType v:
                    AppendType(v.To);

                    result.SetGraphics(Ansi.ForegroundWhite);
                    result.Append('*');

                    break;
                case StructType v:
                    result.SetGraphics(Ansi.BrightForegroundGreen);
                    result.Append(v.Struct.Identifier.Content);

                    if (!v.TypeArguments.IsEmpty)
                    {
                        result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append('<');
                        result.Append(string.Join(", ", v.TypeArguments.Values));
                        result.Append('>');
                    }
                    else if (v.Struct.Template is not null)
                    {
                        result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append('<');
                        result.Append(string.Join(", ", v.Struct.Template.Parameters));
                        result.Append('>');
                    }

                    break;
                case AliasType aliasType:
                    if (aliasType.FinalValue is BuiltinType)
                    {
                        result.SetGraphics(Ansi.BrightForegroundBlue);
                        result.Append(type.ToString());
                    }
                    else
                    {
                        result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append(type.ToString());
                    }
                    break;
                default:
                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(type.ToString());
                    break;
            }
            result.ResetStyle();
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
                    // if (item.BasePointerRelative)
                    // { value = context.Memory.Slice(context.Registers.BasePointer + item.Address, item.Size); }
                    // else
                    // { value = context.Memory.Slice(0 - item.Address, item.Size); }
                    result.Append(' ', CallStackIndent);
                    result.Append(' ', scopeDepth);
                    AppendType(item.Type);
                    // result.Append(item.Type.ToString());
                    result.Append(' ');
                    if (item.Kind == StackElementKind.Internal)
                    {
                        result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append(item.Tag);
                        result.ResetStyle();
                    }
                    else
                    {
                        result.Append(item.Tag);
                    }
                    result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(" = ");
                    result.ResetStyle();
                    if (range.Start < 0 || range.End + 1 >= context.Memory.Length)
                    {
                        result.Append("<invalid address>;");
                        result.AppendLine();
                        continue;
                    }
                    ImmutableArray<byte> value = context.Memory[range.Start..(range.End + 1)];
                    if (item.Type.Equals(BasicType.Integer))
                    { result.Append(value.To<int>()); }
                    else if (item.Type.Equals(BasicType.Float))
                    { result.Append(value.To<float>() + "f"); }
                    else if (item.Type.Equals(BasicType.Char))
                    { result.Append($"'{value.To<char>().Escape()}'"); }
                    else if (item.Type.Equals(BasicType.Byte))
                    { result.Append(value.To<byte>()); }
                    else if (item.Type is PointerType)
                    {
                        result.Append('*');
                        result.Append(value.To<int>());
                    }
                    else
                    { result.Append(string.Join(' ', value)); }
                    result.Append(';');
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

        bool AppendFrame(FunctionInformation frame)
        {
            if (frame.Function is null) return false;

            Parser.FunctionThingDefinition function = frame.Function;
            result.SetGraphics(Ansi.BrightForegroundYellow);
            result.Append(function.Identifier.ToString());
            result.ResetStyle();
            result.Append('(');
            for (int j = 0; j < function.Parameters.Count; j++)
            {
                if (j > 0) result.Append(", ");
                if (function.Parameters[j].Modifiers.Length > 0)
                {
                    result.SetGraphics(Ansi.ForegroundBlue);
                    result.AppendJoin(' ', function.Parameters[j].Modifiers);
                    result.ResetStyle();
                    result.Append(' ');
                }

                if (function is not ICompiledFunction compiledFunction)
                {
                    result.Append(function.Parameters[j].Type.ToString());
                }
                else
                {
                    AppendType(compiledFunction.ParameterTypes[j]);
                    result.ResetStyle();
                }

                result.Append(' ');
                result.Append(function.Parameters[j].Identifier.ToString());
            }
            result.Append(')');
            result.Append(' ');

            if (function.Identifier.Position != Position.UnknownPosition)
            { result.Append(function.Identifier.Position.ToStringCool().Surround(" (at ", ")")); }

            if (function.File is not null)
            { result.Append($" (in {function.File})"); }
            return true;
        }

        result.AppendLine();
        result.AppendLine($"Registers:");
        result.AppendLine($"CP: {context.Registers.CodePointer}");
        result.AppendLine($"SP: {context.Registers.StackPointer}");
        result.AppendLine($"BP: {context.Registers.BasePointer}");
        result.AppendLine($"AX: {context.Registers.AX}");
        result.AppendLine($"BX: {context.Registers.BX}");
        result.AppendLine($"CX: {context.Registers.CX}");
        result.AppendLine($"DX: {context.Registers.DX}");
        result.AppendLine($"Flags: {context.Registers.Flags}");

        result.AppendLine();
        result.AppendLine("Call Stack (from last to recent):");
        if (callTrace.Length == 0)
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
                    FunctionInformation frame = callStack[i];

                    result.Append(' ', CallStackIndent);

                    if (!AppendFrame(frame))
                    { result.Append($"<unknown> {callTrace[i]}"); }

                    result.AppendLine();

                    AppendScope(callTrace[i]);
                }
            }
        }

        FunctionInformation currentFrame = DebugInformation?.GetFunctionInformation(context.Registers.CodePointer) ?? default;
        result.Append(' ', CallStackIndent);
        if (!AppendFrame(currentFrame))
        { result.Append($"<unknown> {context.Registers.CodePointer}"); }
        result.Append(" (current)");
        result.AppendLine();

        AppendScope(context.Registers.CodePointer);

        return result.ToString();
    }
}
