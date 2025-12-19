using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public class RuntimeException : LanguageExceptionWithoutContext
{
    const int CallStackIndent = 1;

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

                    if (v.Length.HasValue)
                    {
                        if (colored) result.SetGraphics(Ansi.ForegroundWhite);
                        result.Append(v.Length.Value.ToString());
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
            else if (type.SameAs(BasicType.U64))
            { result.Append(value.To<ulong>()); }
            else if (type.SameAs(BasicType.I64))
            { result.Append(value.To<long>()); }
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
                    else if (!toArrayType.Length.HasValue)
                    {
                        result.Append('*');
                        result.Append(value.To<int>());
                        result.Append(" -> ");
                        result.Append("[ ? ]");
                    }
                    else if (StatementCompiler.FindSize(pointerType.To, out int _s, out _, runtimeInfoProvider))
                    {
                        Range<int> pointerTo = new(value.To<int>(), value.To<int>() + _s);
                        AppendValue(pointerTo, pointerType.To, depth + 1);
                    }
                    else
                    {
                        result.Append('?');
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
                else if (StatementCompiler.FindSize(pointerType.To, out int _s, out _, runtimeInfoProvider))
                {
                    Range<int> pointerTo = new(value.To<int>(), value.To<int>() + _s);
                    AppendValue(pointerTo, pointerType.To, depth + 1);
                }
                else
                {
                    result.Append('?');
                }
            }
            else if (type.Is(out StructType? structType))
            {
                result.Append("{ ");
                int offset = 0;
                bool comma = true;
                foreach (CompiledField field in structType.Struct.Fields)
                {
                    if (comma) comma = false;
                    else result.Append(", ");
                    result.Append(field.Identifier.Content);
                    result.Append(": ");
                    GeneralType fieldType = structType.ReplaceType(field.Type, out _);

                    if (StatementCompiler.FindSize(fieldType, out int _s, out _, runtimeInfoProvider))
                    {
                        Range<int> fieldRange = new(range.Start + offset, range.Start + offset + _s);
                        AppendValue(fieldRange, fieldType, depth + 1);
                        offset += _s;
                    }
                    else
                    {
                        result.Append('?');
                        break;
                    }
                }
                if (structType.Struct.Fields.Length > 0) result.Append(' ');
                result.Append('}');
            }
            else if (type.Is(out ArrayType? arrayType))
            {
                result.Append('[');
                if (arrayType.Length.HasValue && StatementCompiler.FindSize(arrayType.Of, out int elementSize, out _, runtimeInfoProvider))
                {
                    for (int i = 0; i < arrayType.Length.Value; i++)
                    {
                        if (i > 0) result.Append(',');
                        result.Append(' ');
                        int offset = range.Start + (i * elementSize);
                        AppendValue(new Range<int>(offset, offset + elementSize), arrayType.Of, depth);
                    }
                }
                else
                {
                    result.Append(" ...");
                }
                result.Append(' ');
                result.Append(']');
            }
            else if (type.Is(out FunctionType? functionType))
            {
                if (functionType.HasClosure)
                {
                    result.Append(" => ");
                    if (value.To<int>() == 0)
                    {
                        result.Append("NULL");
                    }
                    else if (StatementCompiler.FindSize(PointerType.Any, out int _s, out _, runtimeInfoProvider))
                    {
                        Range<int> pointerTo = new(value.To<int>(), value.To<int>() + _s);
                        AppendValue(pointerTo, new FunctionType(functionType.ReturnType, functionType.Parameters, false), depth);
                    }
                    else
                    {
                        result.Append('?');
                    }
                }
                else
                {
                    result.Append('#');
                    if (value.To<int>() < 0)
                    {
                        result.Append("INVALID");
                    }
                    else
                    {
                        result.Append(value.To<int>());
                    }
                }
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

            ICompiledFunctionDefinition function = frame.Function;
            if (colored) result.SetGraphics(Ansi.BrightForegroundYellow);
            if (function is CompiledFunctionDefinition compiledFunctionDefinition1)
            {
                result.Append(compiledFunctionDefinition1.Identifier.ToString());
            }
            else if (function is CompiledLambda)
            {
                result.Append("<lambda>");
            }
            else
            {
                result.Append("<unknown function>");
            }
            if (colored) result.ResetStyle();
            result.Append('(');
            for (int j = 0; j < function.Parameters.Length; j++)
            {
                if (j > 0) result.Append(", ");
                if (function.Parameters[j].Modifiers.Length > 0)
                {
                    if (colored) result.SetGraphics(Ansi.ForegroundBlue);
                    result.AppendJoin(' ', function.Parameters[j].Modifiers);
                    if (colored) result.ResetStyle();
                    result.Append(' ');
                }

                if (function is not ICompiledFunctionDefinition compiledFunction)
                {
                    result.Append(function.Parameters[j].Type.ToString());
                }
                else
                {
                    AppendType(compiledFunction.Parameters[j].Type);
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
            else if (function is CompiledFunctionDefinition compiledFunctionDefinition2)
            {
                result.Append(LanguageException.Format(null, compiledFunctionDefinition2.Identifier.Position, function.File));
            }
            else
            {
                result.Append(LanguageException.Format(null, Position.UnknownPosition, function.File));
            }

            return true;
        }

        // result.AppendLine();
        // result.AppendLine($"Registers:");
        // result.AppendLine($"CP: {context.Registers.CodePointer}");
        // result.AppendLine($"SP: {context.Registers.StackPointer}");
        // result.AppendLine($"BP: {context.Registers.BasePointer}");
        // result.AppendLine($"AX: {context.Registers.AX}");
        // result.AppendLine($"BX: {context.Registers.BX}");
        // result.AppendLine($"CX: {context.Registers.CX}");
        // result.AppendLine($"DX: {context.Registers.DX}");
        // result.AppendLine($"Flags: {context.Registers.Flags}");

        result.AppendLine();
        result.AppendLine("Call Stack (from oldest to recent):");
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
}
