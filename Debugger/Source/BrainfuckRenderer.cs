using System.Collections.Immutable;
using System.Diagnostics;
using LanguageCore.Brainfuck;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.TUI;

public class BrainfuckRenderer
{
    readonly InterpreterCompact processor;

    DataMovement memLoad;
    readonly List<DataMovement> memStore = new();

    public BrainfuckRenderer(InterpreterCompact processor)
    {
        this.processor = processor;
    }

    enum DataMovementEdge
    {
        None,
        Start,
        Middle,
        End,
    }

    readonly struct DataMovement
    {
        public readonly int Address;
        public readonly int Size;

        public DataMovement(int address, int size)
        {
            Address = address;
            Size = size;
        }

        public DataMovementEdge At(int address)
        {
            if (Size == 0) return DataMovementEdge.None;
            if (address == Address) return DataMovementEdge.Start;
            if (address == Address + Size - 1) return DataMovementEdge.End;
            if (address >= Address && address < Address + Size) return DataMovementEdge.Middle;
            return DataMovementEdge.None;
        }
    }

    void GetDataMovementIndicators()
    {
        if (processor.CodePointer >= processor.Code.Length) return;
        CompactCodeSegment instruction = processor.Code[processor.CodePointer];

        memStore.Clear();
        memLoad = default;

        switch (instruction.OpCode)
        {
            case OpCodesCompact.NULL:
                break;
            case OpCodesCompact.PointerRight:
                break;
            case OpCodesCompact.PointerLeft:
                break;
            case OpCodesCompact.Add:
                memStore.Add(new DataMovement(processor.MemoryPointer, 1));
                break;
            case OpCodesCompact.Sub:
                memStore.Add(new DataMovement(processor.MemoryPointer, 1));
                break;
            case OpCodesCompact.BranchStart:
                memLoad = new DataMovement(processor.MemoryPointer, 1);
                break;
            case OpCodesCompact.BranchEnd:
                memLoad = new DataMovement(processor.MemoryPointer, 1);
                break;
            case OpCodesCompact.Out:
                memLoad = new DataMovement(processor.MemoryPointer, 1);
                break;
            case OpCodesCompact.In:
                memStore.Add(new DataMovement(processor.MemoryPointer, 1));
                break;
            case OpCodesCompact.Break:
                break;
            case OpCodesCompact.Clear:
                memStore.Add(new DataMovement(processor.MemoryPointer, 1));
                break;
            case OpCodesCompact.Move:
                memLoad = new DataMovement(processor.MemoryPointer, 1);
                if (instruction.Arg1 != 0) memStore.Add(new DataMovement(processor.MemoryPointer + instruction.Arg1, 1));
                if (instruction.Arg2 != 0) memStore.Add(new DataMovement(processor.MemoryPointer + instruction.Arg2, 1));
                if (instruction.Arg3 != 0) memStore.Add(new DataMovement(processor.MemoryPointer + instruction.Arg3, 1));
                if (instruction.Arg4 != 0) memStore.Add(new DataMovement(processor.MemoryPointer + instruction.Arg4, 1));
                break;
        }
    }

    static void WriteType(ref BufferWriter t, GeneralType type)
    {
        switch (type)
        {
            case AliasType v:
                t.Write(v.Identifier, AnsiColor.Green);
                break;
            case ArrayType v:
                WriteType(ref t, v.Of);
                t.Write('[');
                if (v.Length.HasValue) t.Write(v.Length.Value.ToString());
                t.Write(']');
                break;
            case BuiltinType v:
                t.Write(v.Type switch
                {
                    BasicType.Void => "void",
                    BasicType.Any => "any",
                    BasicType.U8 => "u8",
                    BasicType.I8 => "i8",
                    BasicType.U16 => "u16",
                    BasicType.I16 => "i16",
                    BasicType.U32 => "u32",
                    BasicType.I32 => "i32",
                    BasicType.U64 => "u64",
                    BasicType.I64 => "i64",
                    BasicType.F32 => "f32",
                    _ => throw new UnreachableException(),
                }, AnsiColor.Blue);
                break;
            case FunctionType v:
                WriteType(ref t, v.ReturnType);
                t.Write('(');
                for (int i = 0; i < v.Parameters.Length; i++)
                {
                    if (i > 0) t.Write(", ");
                    WriteType(ref t, v.Parameters[i]);
                }
                t.Write(')');
                break;
            case PointerType v:
                WriteType(ref t, v.To);
                t.Write('*');
                break;
            case StructType v:
                t.Write(v.Struct.Identifier.Content, AnsiColor.Green);
                if (v.Struct.Template is not null)
                {
                    t.Write('<');
                    for (int i = 0; i < v.Struct.Template.Parameters.Length; i++)
                    {
                        if (i > 0) t.Write(", ");
                        WriteType(ref t, v.TypeArguments[v.Struct.Template.Parameters[i].Content]);
                    }
                    t.Write('>');
                }
                break;
            case GenericType v:
                t.Write(v.Identifier, AnsiColor.Yellow);
                break;
            default:
                throw new UnreachableException();
        }
    }

    Element CreateSourceElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();

        if (!processor.DebugInfo.IsEmpty)
        {
            if (processor.DebugInfo.TryGetSourceLocation(processor.CodePointer, out SourceCodeLocation sourceLocation) &&
                processor.DebugInfo.OriginalFiles.TryGetValue(sourceLocation.Location.File, out ImmutableArray<Tokenizing.Token> tokens))
            {
                int firstVisibleLine = Math.Max(0, sourceLocation.Location.Position.Range.Start.Line - buffer.Height / 2);
                int lastVisibleLine = firstVisibleLine + buffer.Height - 1;

                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i].TokenType == Tokenizing.TokenType.Whitespace) continue;
                    if (tokens[i].Position.Range.Start.Line < firstVisibleLine) continue;
                    if (tokens[i].Position.Range.Start.Line > lastVisibleLine) continue;
                    AnsiColor bg = AnsiColor.Default;
                    AnsiColor fg = tokens[i].AnalyzedType switch
                    {
                        Tokenizing.TokenAnalyzedType.Attribute => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Type => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.TypeAlias => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Struct => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.Keyword => AnsiColor.BrightBlue,
                        Tokenizing.TokenAnalyzedType.FunctionName => AnsiColor.BrightYellow,
                        Tokenizing.TokenAnalyzedType.VariableName => AnsiColor.White,
                        Tokenizing.TokenAnalyzedType.FieldName => AnsiColor.White,
                        Tokenizing.TokenAnalyzedType.ParameterName => AnsiColor.White,
                        Tokenizing.TokenAnalyzedType.Statement => AnsiColor.BrightMagenta,
                        Tokenizing.TokenAnalyzedType.BuiltinType => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.TypeParameter => AnsiColor.BrightGreen,
                        Tokenizing.TokenAnalyzedType.ConstantName => AnsiColor.White,
                        _ => tokens[i].TokenType switch
                        {
                            Tokenizing.TokenType.Identifier => AnsiColor.White,
                            Tokenizing.TokenType.LiteralNumber => AnsiColor.BrightCyan,
                            Tokenizing.TokenType.LiteralHex => AnsiColor.BrightCyan,
                            Tokenizing.TokenType.LiteralBinary => AnsiColor.BrightCyan,
                            Tokenizing.TokenType.LiteralString => AnsiColor.BrightYellow,
                            Tokenizing.TokenType.LiteralCharacter => AnsiColor.BrightYellow,
                            Tokenizing.TokenType.LiteralFloat => AnsiColor.BrightCyan,
                            Tokenizing.TokenType.Operator => AnsiColor.BrightBlack,
                            Tokenizing.TokenType.PreprocessIdentifier => AnsiColor.BrightBlack,
                            Tokenizing.TokenType.PreprocessArgument => AnsiColor.BrightBlack,
                            Tokenizing.TokenType.PreprocessSkipped => AnsiColor.BrightBlack,
                            Tokenizing.TokenType.Comment => AnsiColor.BrightBlack,
                            Tokenizing.TokenType.CommentMultiline => AnsiColor.BrightBlack,
                            _ => AnsiColor.Default,
                        }
                    };

                    buffer.Text(tokens[i].Position.Range.Start.Character, tokens[i].Position.Range.Start.Line - firstVisibleLine, tokens[i].ToOriginalString(), fg, bg);
                }

                for (int l = sourceLocation.Location.Position.Range.Start.Line; l <= sourceLocation.Location.Position.Range.End.Line; l++)
                {
                    if (l < firstVisibleLine) continue;
                    if (l > lastVisibleLine) continue;
                    int xStart = l == sourceLocation.Location.Position.Range.Start.Line ? sourceLocation.Location.Position.Range.Start.Character : 0;
                    int xEnd = l == sourceLocation.Location.Position.Range.End.Line ? sourceLocation.Location.Position.Range.End.Character : buffer.Width - 1;
                    for (int x = xStart; x < xEnd; x++)
                    {
                        if (x < 0) continue;
                        if (x >= buffer.Width) continue;
                        char c = buffer[x, l - firstVisibleLine].Char;
                        buffer[x, l - firstVisibleLine] = new(c == default ? ' ' : c, AnsiColor.Black, AnsiColor.Red);
                    }
                }
            }
        }
    }, ElementSize.Auto());

    Element CreateCodeElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();

        int begin = Math.Max(0, processor.CodePointer - 20);
        int end = Math.Min(processor.Code.Length - 1, begin + buffer.Width);
        BufferWriter t = buffer.Text(0, 0);
        for (int i = begin; i < end && t.Offset < buffer.Width; i++)
        {
            CompactCodeSegment p = processor.Code[i];
            AnsiColor bg = i == processor.CodePointer ? AnsiColor.Red : AnsiColor.Default;
            AnsiColor? fg = i == processor.CodePointer ? AnsiColor.Black : null;
            switch (p.OpCode)
            {
                case OpCodesCompact.PointerRight:
                    t.Write('>', fg ?? AnsiColor.BrightBlack, bg);
                    break;
                case OpCodesCompact.PointerLeft:
                    t.Write('<', fg ?? AnsiColor.BrightBlack, bg);
                    break;
                case OpCodesCompact.Add:
                    t.Write('+', fg ?? AnsiColor.Blue, bg);
                    break;
                case OpCodesCompact.Sub:
                    t.Write('-', fg ?? AnsiColor.Blue, bg);
                    break;
                case OpCodesCompact.BranchStart:
                    t.Write('[', fg ?? AnsiColor.Magenta, bg);
                    break;
                case OpCodesCompact.BranchEnd:
                    t.Write(']', fg ?? AnsiColor.Magenta, bg);
                    break;
                case OpCodesCompact.Out:
                    t.Write('.', fg ?? AnsiColor.Yellow, bg);
                    break;
                case OpCodesCompact.In:
                    t.Write(',', fg ?? AnsiColor.Yellow, bg);
                    break;
                case OpCodesCompact.Clear:
                    t.Write('[', fg ?? AnsiColor.Magenta, bg);
                    t.Write("-", fg ?? AnsiColor.Blue, bg);
                    t.Write(']', fg ?? AnsiColor.Magenta, bg);
                    break;
                case OpCodesCompact.Move:
                    t.Write('(', fg ?? AnsiColor.White, bg);
                    t.Write('M', fg ?? AnsiColor.Blue, bg);

                    if (p.Arg1 != 0) t.Write($"{p.Arg1};", fg ?? AnsiColor.White, bg);
                    if (p.Arg2 != 0) t.Write($"{p.Arg2};", fg ?? AnsiColor.White, bg);
                    if (p.Arg3 != 0) t.Write($"{p.Arg3};", fg ?? AnsiColor.White, bg);
                    if (p.Arg4 != 0) t.Write($"{p.Arg4};", fg ?? AnsiColor.White, bg);

                    t.Write(')', fg ?? AnsiColor.White, bg);
                    break;
                default:
                    throw new UnreachableException();
            }
            if (p.Count > 1) t.Write(p.Count.ToString(), fg ?? AnsiColor.Default, bg);
        }
    }, ElementSize.Fixed(3));

    Element CreateRegistersElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();
        BufferWriter t;

        t = buffer.Text(0, 0);
        t.Write($" CP: ");
        t.PadTo(6);
        t.Write(processor.CodePointer.ToString(), AnsiColor.White);

        t = buffer.Text(buffer.Width / 2, 0);
        t.Write($" MP: ");
        t.PadTo(6);
        t.Write(processor.MemoryPointer.ToString(), AnsiColor.White);
    }, ElementSize.Fixed(3));

    Element CreateMemoryElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();
        byte[] memb = new byte[Math.Min(buffer.Width * buffer.Height, processor.Memory.Length / 4)];
        for (int i = 0; i < processor.Memory.Length; i++)
        {
            if (i / 4 >= memb.Length) break;
            if (processor.Memory[i] == 0) continue;
            memb[i / 4] |= (byte)(1 << (i % 4));
        }

        for (int i = 0; i < memb.Length; i++)
        {
            int x = i % buffer.Width;
            int y = i / buffer.Width;
            buffer[x, y] = new(" ▘▝▀▖▌▞▛▗▚▐▜▄▙▟█"[memb[i]]);
        }

        void TryDrawDot(int address, AnsiColor color, in AnsiBufferSlice buffer)
        {
            int i = address / 4;
            if (i < memb.Length)
            {
                int c = memb[i] | (1 << (address % 4));
                int x = i % buffer.Width;
                int y = i / buffer.Width;
                buffer[x, y] = new(" ▘▝▀▖▌▞▛▗▚▐▜▄▙▟█"[c], color);
            }
        }

        TryDrawDot(processor.MemoryPointer, AnsiColor.BrightBlue, buffer);
    }, ElementSize.Percentage(0.7f));

    Element CreateTapeElement()
    {
        int prevPosition = 0;

        return new Panel((in AnsiBufferSlice buffer) =>
        {
            buffer.Clear();

            if (processor.MemoryPointer < (prevPosition + 4))
            {
                prevPosition = processor.MemoryPointer - 4;
            }
            else if (processor.MemoryPointer > (prevPosition + buffer.Height - 1 - 4))
            {
                prevPosition = processor.MemoryPointer - buffer.Height + 4;
            }

            ImmutableArray<ScopeInformation> stackDebugInfo;
            if (!processor.DebugInfo.IsEmpty)
            { stackDebugInfo = processor.DebugInfo.GetScopes(processor.CodePointer); }
            else
            { stackDebugInfo = ImmutableArray<ScopeInformation>.Empty; }

            int begin = Math.Max(0, prevPosition);
            int end = Math.Min(processor.Memory.Length, Math.Max(prevPosition + buffer.Height, begin + buffer.Height));
            for (int i = begin; i < end; i++)
            {
                BufferWriter t = buffer.Text(0, i - begin);

                DataMovementEdge ld = memLoad.At(i);
                if (ld != DataMovementEdge.None)
                {
                    switch (ld)
                    {
                        case DataMovementEdge.None: t.Write(' '); break;
                        case DataMovementEdge.Start: t.Write("l", AnsiColor.BrightRed); break;
                        case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                        case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                    }
                }
                else
                {
                    foreach (DataMovement item in memStore)
                    {
                        switch (item.At(i))
                        {
                            case DataMovementEdge.None: continue;
                            case DataMovementEdge.Start: t.Write("s", AnsiColor.BrightRed); break;
                            case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                            case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                        }
                        goto e;
                    }
                    t.Write(' ');
                e:;
                }

                if (processor.MemoryPointer == i) t.Write("▶", AnsiColor.BrightBlue);
                else t.Write(' ');

                t.Write($"{i,5} ");
                t.Write(processor.Memory[i].ToString(), processor.Memory[i] == 0 ? AnsiColor.BrightBlack : AnsiColor.White);

                foreach (ScopeInformation stack in stackDebugInfo)
                {
                    foreach (StackElementInformation item in stack.Stack)
                    {
                        if (item.Address == i)
                        {
                            t.PadTo(13);
                            if (item.Size <= 1) t.Write(' ');
                            else t.Write('┌');
                            WriteType(ref t, item.Type);
                            t.Write(' ');
                            if (item.Kind == StackElementKind.Internal)
                            {
                                t.Write(item.Identifier, AnsiColor.BrightBlack);
                            }
                            else
                            {
                                t.Write(item.Identifier);
                            }
                            switch (item.Type.FinalValue)
                            {
                                case BuiltinType v:
                                    switch (v.Type)
                                    {
                                        case BasicType.U8: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<byte>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.I8: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<sbyte>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.U16: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<ushort>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.I16: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<short>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.U32: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<uint>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.I32: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<int>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.U64: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<ulong>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.I64: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<long>(i).ToString(), AnsiColor.BrightBlue); break;
                                        case BasicType.F32: t.Write(" = "); t.Write(processor.Memory.AsSpan().Get<float>(i).ToString(), AnsiColor.BrightBlue); break;
                                    }
                                    break;
                            }
                            break;
                        }
                        else if (i > item.Address && i < item.Address + item.Size - 1)
                        {
                            t.PadTo(13);
                            t.Write('│');
                            break;
                        }
                        else if (i > item.Address && i == item.Address + item.Size - 1)
                        {
                            t.PadTo(13);
                            t.Write('└');
                            break;
                        }
                    }
                }
            }
        }, ElementSize.Auto());
    }

    Element CreateStackTraceElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();

        ImmutableArray<FunctionInformation> callTrace = processor.DebugInfo.GetFunctionInformationNested(processor.CodePointer);

        int position = callTrace.Length - 1;
        int begin = Math.Max(0, position - (buffer.Height / 2));
        int end = Math.Min(callTrace.Length, Math.Max(position + (buffer.Height / 2), begin + buffer.Height));
        for (int i = begin; i < end; i++)
        {
            BufferWriter t = buffer.Text(0, i - begin);
            FunctionInformation function = callTrace[i];
            if (!function.IsValid)
            {
                t.Write("?");
                continue;
            }

            if (function.Function is ICompiledFunctionDefinition compiledFunction)
            {
                t.Write(function.Function.Identifier.Content, AnsiColor.Yellow);
                t.Write('(');
                for (int j = 0; j < compiledFunction.Parameters.Length; j++)
                {
                    if (j > 0) t.Write(", ");
                    for (int k = 0; k < compiledFunction.Parameters[j].Modifiers.Length; k++)
                    {
                        t.Write(compiledFunction.Parameters[j].Modifiers[k].Content, AnsiColor.Blue);
                        t.Write(' ');
                    }
                    WriteType(ref t, GeneralType.InsertTypeParameters(compiledFunction.Parameters[j].Type, function.TypeArguments) ?? compiledFunction.Parameters[j].Type);
                    t.Write(' ');
                    t.Write(compiledFunction.Parameters[j].Identifier.Content);
                }
                t.Write(')');
            }
            else if (function.Function is not null)
            {
                t.Write(function.Function.Identifier.Content, AnsiColor.Yellow);
                t.Write('(');
                for (int j = 0; j < function.Function.Parameters.Count; j++)
                {
                    if (j > 0) t.Write(", ");
                    for (int k = 0; k < function.Function.Parameters[j].Modifiers.Length; k++)
                    {
                        t.Write(function.Function.Parameters[j].Modifiers[k].Content, AnsiColor.Blue);
                        t.Write(' ');
                    }
                    t.Write(function.Function.Parameters[j].Type.ToString(function.TypeArguments), AnsiColor.Green);
                    t.Write(' ');
                    t.Write(function.Function.Parameters[j].Identifier.Content);
                }
                t.Write(')');
            }
            else
            {
                t.Write(function.Function?.GetType().Name ?? "?");
            }
        }
    }, ElementSize.Auto());

    public void Run()
    {
        Container container = new(FlowDirection.Vertical, ElementSize.Percentage(1f), new Element[] {
            CreateCodeElement().WithBorders("Code"),
            new Container(FlowDirection.Horizontal, ElementSize.Auto(), new Element[]
            {
                CreateTapeElement().WithBorders("Tape"),
                CreateStackTraceElement().WithBorders("Call Stack"),
            })
        });
        Renderer renderer = new();

        bool cancel = false;
        Console.CancelKeyPress += (sender, e) =>
        {
            cancel = true;
            e.Cancel = true;
        };

        int tick = int.MaxValue;
        int freq = 10;

        try
        {
            Console.CursorVisible = false;
            while (!cancel)
            {
                while (!Console.IsOutputRedirected && Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Spacebar)
                    {
                        if (tick != int.MaxValue)
                        {
                            tick = int.MaxValue;
                        }
                        else
                        {
                            tick = 0;
                        }
                    }
                    else if (key.Key == ConsoleKey.Enter)
                    {
                        if (tick == int.MaxValue) tick = 1;
                        else tick++;
                    }
                }

                GetDataMovementIndicators();

                renderer.Render(container);

                if (processor.IsDone) break;

                for (int i = 0; tick > 0 && i < freq; i++)
                {
                    processor.Step();
                    if (tick != int.MaxValue) tick--;
                }

                Thread.Sleep(50);
            }
        }
        finally
        {
            Console.ResetColor();
            Console.CursorVisible = true;
        }
    }
}
