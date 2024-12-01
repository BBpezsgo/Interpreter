using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public class RuntimeException : LanguageExceptionWithoutContext, IDisposable
{
    const int CallStackIndent = 1;

    bool IsDisposed;
    public RuntimeContext? Context { get; set; }
    public CompiledDebugInformation DebugInformation { get; set; }
    public ReadOnlySpan<CallTraceItem> CallTrace =>
        Context is null ? ReadOnlySpan<CallTraceItem>.Empty :
        DebugInformation.IsEmpty ? ReadOnlySpan<CallTraceItem>.Empty :
        DebugUtils.TraceStack(Context.Value.Memory.AsSpan(), Context.Value.Registers.BasePointer, DebugInformation.StackOffsets);

    public RuntimeException(string message) : base(message) { }
    public RuntimeException(string message, RuntimeContext context, CompiledDebugInformation debugInformation) : base(message)
    {
        Context = context;
        DebugInformation = debugInformation;
    }
    public RuntimeException(string message, Exception inner, RuntimeContext context, CompiledDebugInformation debugInformation) : base(message, inner)
    {
        Context = context;
        DebugInformation = debugInformation;
    }

    public string ToString(bool colored)
    {
        if (!Context.HasValue) return Message + " (no context)";
        RuntimeContext context = Context.Value;

        Uri? file = null;
        string? arrows = null;
        Position position;

        if (!DebugInformation.IsEmpty && DebugInformation.TryGetSourceLocation(context.Registers.CodePointer, out SourceCodeLocation sourcePosition))
        {
            position = sourcePosition.Location.Position;
            file = sourcePosition.Location.File;
            if (sourcePosition.Location.File is not null &&
                DebugInformation.OriginalFiles.TryGetValue(sourcePosition.Location.File, out ImmutableArray<Tokenizing.Token> tokens))
            { arrows = LanguageException.GetArrows(sourcePosition.Location.Position, tokens); }
        }
        else
        { position = Position.UnknownPosition; }

        ImmutableArray<FunctionInformation> callStack = DebugInformation.IsEmpty ? ImmutableArray<FunctionInformation>.Empty : DebugInformation.GetFunctionInformation(CallTrace);

        file ??= callStack.LastOrDefault().Function?.File;

        StringBuilder result = new();

        result.Append(LanguageException.Format(Message, position, file));

        result.AppendLine();

        if (colored) result.ResetStyle();

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

                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append('[');

                    if (v.ComputedLength.HasValue)
                    {
                        if (colored) result.SetGraphics(Ansi.ForegroundWhite);
                        result.Append(v.ComputedLength.Value.ToString());
                    }
                    else if (v.Length is not null)
                    {
                        if (colored) result.SetGraphics(Ansi.ForegroundWhite);
                        result.Append('?');
                    }

                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(']');

                    break;
                case BuiltinType v:
                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlue);
                    result.Append(v.ToString());

                    break;
                case FunctionType v:
                    AppendType(v.ReturnType);

                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append('(');

                    for (int i = 0; i < v.Parameters.Length; i++)
                    {
                        if (i > 0)
                        {
                            if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                            result.Append(", ");
                        }

                        AppendType(v.Parameters[i]);
                    }

                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(')');

                    break;
                case GenericType v:
                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(v.ToString());

                    break;
                case PointerType v:
                    AppendType(v.To);

                    if (colored) result.SetGraphics(Ansi.ForegroundWhite);
                    result.Append('*');

                    break;
                case StructType v:
                    if (colored) result.SetGraphics(Ansi.BrightForegroundGreen);
                    result.Append(v.Struct.Identifier.Content);

                    if (!v.TypeArguments.IsEmpty)
                    {
                        if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append('<');
                        result.Append(string.Join(", ", v.TypeArguments.Values));
                        result.Append('>');
                    }
                    else if (v.Struct.Template is not null)
                    {
                        if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append('<');
                        result.Append(string.Join(", ", v.Struct.Template.Parameters));
                        result.Append('>');
                    }

                    break;
                case AliasType aliasType:
                    if (colored) result.SetGraphics(Ansi.ForegroundGreen);
                    result.Append(type.ToString());
                    break;
                default:
                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(type.ToString());
                    break;
            }
            if (colored) result.ResetStyle();
        }

        void AppendValue(Range<int> range, GeneralType type, int depth)
        {
            if (depth > 3)
            {
                result.Append("..."); return;
            }

            RuntimeInfoProvider runtimeInfoProvider = new()
            {
                PointerSize = 4,
            };

            if (colored) result.ResetStyle();
            if (range.Start < 0 || range.End + 1 >= context.Memory.Length)
            {
                result.Append("<invalid address>");
                return;
            }
            ReadOnlySpan<byte> value = context.Memory.AsSpan()[range.Start..(range.End + 1)];
            if (type.SameAs(BasicType.F32))
            { result.Append(value.To<float>() + "f"); }
            else if (type.SameAs(BasicType.U8))
            { result.Append(value.To<byte>()); }
            else if (type.SameAs(BasicType.I8))
            { result.Append(value.To<sbyte>()); }
            else if (type.SameAs(BasicType.U16))
            { result.Append($"'{value.To<char>().Escape()}'"); }
            else if (type.SameAs(BasicType.I16))
            { result.Append(value.To<short>()); }
            else if (type.SameAs(BasicType.U32))
            { result.Append(value.To<uint>()); }
            else if (type.SameAs(BasicType.I32))
            { result.Append(value.To<int>()); }
            else if (type.Is(out PointerType? pointerType))
            {
                if (pointerType.To is ArrayType toArrayType)
                {
                    if (toArrayType.Of.SameAs(BasicType.U16))
                    {
                        result.Append('"');
                        int i = value.To<int>();
                        while (i > 0 && i + 1 < context.Memory.Length)
                        {
                            char v = context.Memory.AsSpan()[i..].To<char>();
                            i += sizeof(char);
                            if (v == 0) break;
                            result.Append(v);
                        }
                        result.Append('"');
                    }
                    else if (!toArrayType.ComputedLength.HasValue)
                    {
                        result.Append('*');
                        result.Append(value.To<int>());
                        result.Append(" -> ");
                        result.Append("[ ? ]");
                    }
                    else
                    {
                        Range<int> pointerTo = new(value.To<int>(), value.To<int>() + pointerType.To.GetSize(runtimeInfoProvider));
                        AppendValue(pointerTo, pointerType.To, depth + 1);
                    }
                }
                else if (pointerType.To is BuiltinType toBuiltinType &&
                    toBuiltinType.SameAs(BasicType.Any))
                {
                    result.Append('*');
                    result.Append(value.To<int>());
                    result.Append(" -> ");
                    result.Append('?');
                }
                else
                {
                    Range<int> pointerTo = new(value.To<int>(), value.To<int>() + pointerType.To.GetSize(runtimeInfoProvider));
                    AppendValue(pointerTo, pointerType.To, depth + 1);
                }
            }
            else if (type.Is(out StructType? structType))
            {
                result.Append("{ ");
                if (!structType.GetFields(runtimeInfoProvider, out ImmutableDictionary<CompiledField, int>? _fields, out _))
                {
                    result.Append("<invalid type> ");
                }
                else
                {
                    KeyValuePair<CompiledField, int>[] fields = _fields.ToArray();
                    for (int i = 0; i < fields.Length; i++)
                    {
                        if (i > 0) result.Append(", ");
                        (CompiledField field, int offset) = fields[i];
                        result.Append(field.Identifier.Content);
                        result.Append(": ");
                        Range<int> fieldRange = new(range.Start + offset, range.Start + offset + field.Type.GetSize(runtimeInfoProvider));
                        AppendValue(fieldRange, field.Type, depth + 1);
                    }
                    if (fields.Length > 0) result.Append(' ');
                }
                result.Append('}');
            }
            else
            { result.AppendJoin(' ', value.ToArray()); }
        }

        void AppendScope(CallTraceItem tracedScope)
        {
            if (DebugInformation.IsEmpty) return;
            ImmutableArray<ScopeInformation> scopes = DebugInformation.GetScopes(tracedScope.InstructionPointer);
            if (scopes.IsEmpty) return;

            int scopeDepth = 0;
            foreach (ScopeInformation scope in scopes.Reverse())
            {
                result.Append(' ', CallStackIndent);
                result.Append(' ', scopeDepth);
                result.AppendLine("{");
                scopeDepth++;
                foreach (StackElementInformation item in scope.Stack)
                {
                    if (item.Kind == StackElementKind.Internal) continue;
                    if (item.Kind == StackElementKind.Parameter) continue;

                    Range<int> range = item.GetRange(tracedScope.BasePointer, context.StackStart);
                    if (range.Start > range.End)
                    { range = new Range<int>(range.End, range.Start); }
                    // if (item.BasePointerRelative)
                    // { value = context.Memory.Slice(context.Registers.BasePointer + item.Address, item.Size); }
                    // else
                    // { value = context.Memory.Slice(0 - item.Address, item.Size); }
                    result.Append(' ', CallStackIndent);
                    result.Append(' ', scopeDepth);
                    // if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    // result.Append('(');
                    // result.Append(range.Start);
                    // result.Append(')');
                    // result.Append(' ');
                    // if (colored) result.ResetStyle();
                    AppendType(item.Type);
                    result.Append(' ');
                    if (item.Kind == StackElementKind.Internal)
                    {
                        if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                        result.Append(item.Identifier);
                        if (colored) result.ResetStyle();
                    }
                    else
                    {
                        result.Append(item.Identifier);
                    }
                    if (colored) result.SetGraphics(Ansi.BrightForegroundBlack);
                    result.Append(" = ");
                    if (colored) result.ResetStyle();
                    AppendValue(range, item.Type, 0);
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

        bool AppendFrame(FunctionInformation frame, CallTraceItem callTraceItem)
        {
            if (frame.Function is null) return false;
            if (DebugInformation.IsEmpty) return false;

            ImmutableArray<ScopeInformation> scopes = DebugInformation.GetScopes(frame.Instructions.Start);

            Parser.FunctionThingDefinition function = frame.Function;
            if (colored) result.SetGraphics(Ansi.BrightForegroundYellow);
            result.Append(function.Identifier.ToString());
            if (colored) result.ResetStyle();
            result.Append('(');
            for (int j = 0; j < function.Parameters.Count; j++)
            {
                if (j > 0) result.Append(", ");
                if (function.Parameters[j].Modifiers.Length > 0)
                {
                    if (colored) result.SetGraphics(Ansi.ForegroundBlue);
                    result.AppendJoin(' ', function.Parameters[j].Modifiers);
                    if (colored) result.ResetStyle();
                    result.Append(' ');
                }

                if (function is not ICompiledFunction compiledFunction)
                {
                    result.Append(function.Parameters[j].Type.ToString());
                }
                else
                {
                    AppendType(compiledFunction.ParameterTypes[j]);
                    if (colored) result.ResetStyle();
                }

                result.Append(' ');
                result.Append(function.Parameters[j].Identifier.Content);

                bool f = false;
                foreach (ScopeInformation scope in scopes)
                {
                    if (f) break;
                    foreach (StackElementInformation item in scope.Stack)
                    {
                        if (item.Kind != StackElementKind.Parameter) continue;
                        if (item.Identifier != function.Parameters[j].Identifier.Content) continue;
                        result.Append(" = ");
                        AppendValue(item.GetRange(callTraceItem.BasePointer, context.StackStart), item.Type, 0);
                        f = true;
                        break;
                    }
                }
            }
            result.Append(')');
            result.Append(' ');

            if (DebugInformation.TryGetSourceLocation(callTraceItem.InstructionPointer, out SourceCodeLocation sourceLocation))
            {
                result.Append(LanguageException.Format(null, sourceLocation.Location));
            }
            else
            {
                result.Append(LanguageException.Format(null, function.Identifier.Position, function.File));
            }

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
        if (CallTrace.Length == 0)
        {
            result.Append(' ', CallStackIndent);
            result.AppendLine("<empty>");
        }
        else
        {
            if (!callStack.IsDefaultOrEmpty)
            {
                if (callStack.Length != CallTrace.Length)
                {
                    result.Append(' ', CallStackIndent);
                    result.Append($"Invalid stack trace");
                    result.AppendLine();
                }
                else
                {
                    for (int i = 0; i < callStack.Length; i++)
                    {
                        result.Append(' ', CallStackIndent);

                        if (!AppendFrame(callStack[i], CallTrace[i]))
                        {
                            result.Append($"<unknown> {CallTrace[i].InstructionPointer}");
                            if (DebugInformation.TryGetSourceLocation(CallTrace[i].InstructionPointer, out SourceCodeLocation sourceLocation))
                            {
                                result.Append(' ');
                                result.Append(sourceLocation.Location.ToString());
                            }
                        }

                        result.AppendLine();

                        AppendScope(CallTrace[i]);
                    }
                }
            }
        }

        FunctionInformation currentFrame = DebugInformation.IsEmpty ? default : DebugInformation.GetFunctionInformation(context.Registers.CodePointer);
        result.Append(' ', CallStackIndent);
        if (!AppendFrame(currentFrame, new CallTraceItem(context.Registers.BasePointer, context.Registers.CodePointer)))
        { result.Append($"<unknown> {context.Registers.CodePointer}"); }
        result.Append(" (current)");
        result.AppendLine();

        AppendScope(new CallTraceItem(context.Registers.BasePointer, context.Registers.CodePointer));

        return result.ToString();
    }

    public override string ToString() => ToString(false);

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        if (disposing) { }
        Context?.Dispose();
        IsDisposed = true;
    }

    ~RuntimeException() { Dispose(disposing: false); }

    void IDisposable.Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
