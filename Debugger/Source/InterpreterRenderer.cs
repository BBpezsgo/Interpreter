using System.Collections.Immutable;
using System.Diagnostics;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.TUI;

struct Window
{
    public int Size;
    public int Margin;
    public int Position;

    public Window(int size, int margin)
    {
        Size = size;
        Margin = margin;
        Position = 0;
    }

    public int Move(int newPosition)
    {
        if (newPosition <= Position + Margin)
        {
            Position = newPosition - Margin - 1;
        }
        else if (newPosition >= Position - Margin)
        {
            Position = newPosition - Margin - Size + 1;
        }
        return Position;
    }
}

public class InterpreterRenderer
{
    readonly BytecodeProcessor processor;

    DataMovement memLoad;
    DataMovement memStore;

    Register regLoad;
    Register regStore;

    public InterpreterRenderer(BytecodeProcessor processor)
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

    static (DataMovement MemLoad, DataMovement MemStore, Register RegLoad, Register RegStore) GetDataMovementIndicators(in BytecodeProcessor processor)
    {
        if (processor.Registers.CodePointer >= processor.Code.Length) return default;
        Instruction instruction = processor.Code[processor.Registers.CodePointer];

        DataMovement memLoad = default;
        DataMovement memStore = default;

        Register regLoad = 0;
        Register regStore = 0;

        switch (instruction.Opcode)
        {
            case Opcode.Push:
            {
                int size = (int)instruction.Operand1.BitWidth;
                int address = processor.Registers.StackPointer + (size * ProcessorState.StackDirection);
                memStore = new DataMovement(address, size);

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regLoad = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out address))
                { memLoad = new DataMovement(address, size); }

                break;
            }
            case Opcode.Pop8:
            {
                memLoad = new DataMovement(processor.Registers.StackPointer, 1);
                break;
            }
            case Opcode.Pop16:
            {
                memLoad = new DataMovement(processor.Registers.StackPointer, 2);
                break;
            }
            case Opcode.Pop32:
            {
                memLoad = new DataMovement(processor.Registers.StackPointer, 4);
                break;
            }
            case Opcode.Pop64:
            {
                memLoad = new DataMovement(processor.Registers.StackPointer, 8);
                break;
            }
            case Opcode.PopTo8:
            {
                int address = processor.Registers.StackPointer;
                memLoad = new DataMovement(address, 1);

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regStore = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out address))
                { memStore = new DataMovement(address, 1); }

                break;
            }
            case Opcode.PopTo16:
            {
                int address = processor.Registers.StackPointer;
                memLoad = new DataMovement(address, 2);

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regStore = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out address))
                { memStore = new DataMovement(address, 2); }

                break;
            }
            case Opcode.PopTo32:
            {
                int address = processor.Registers.StackPointer;
                memLoad = new DataMovement(address, 4);

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regStore = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out address))
                { memStore = new DataMovement(address, 4); }

                break;
            }
            case Opcode.PopTo64:
            {
                int address = processor.Registers.StackPointer;
                memLoad = new DataMovement(address, 8);

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regStore = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out address))
                { memStore = new DataMovement(address, 8); }

                break;
            }
            case Opcode.Move:
            {
                if (instruction.Operand2.Type == InstructionOperandType.Register)
                { regLoad = (Register)instruction.Operand2.Value; }
                else if (processor.ResolveAddress(instruction.Operand2, out int address))
                { memLoad = new DataMovement(address, (int)instruction.Operand2.BitWidth); }

                if (instruction.Operand1.Type == InstructionOperandType.Register)
                { regStore = (Register)instruction.Operand1.Value; }
                else if (processor.ResolveAddress(instruction.Operand1, out int address))
                { memStore = new DataMovement(address, (int)instruction.Operand1.BitWidth); }
                break;
            }
        }

        return (memLoad, memStore, regLoad, regStore);
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
                if (v.ComputedLength.HasValue) t.Write(v.ComputedLength.Value.ToString());
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

        if (!processor.DebugInformation.IsEmpty)
        {
            if (processor.DebugInformation.TryGetSourceLocation(processor.Registers.CodePointer, out SourceCodeLocation sourceLocation) &&
                processor.DebugInformation.OriginalFiles.TryGetValue(sourceLocation.Location.File, out ImmutableArray<Tokenizing.Token> tokens))
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

        static void WriteOperand(ref BufferWriter writer, in InstructionOperand operand)
        {
            switch (operand.Type)
            {
                case InstructionOperandType.Immediate8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write(operand.Byte.ToString(), AnsiColor.BrightGreen);
                    break;
                case InstructionOperandType.Immediate16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write(operand.Char.ToString(), AnsiColor.BrightGreen);
                    break;
                case InstructionOperandType.Immediate32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write(operand.Int.ToString(), AnsiColor.BrightGreen);
                    break;
                case InstructionOperandType.Immediate64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write(operand.Value.ToString(), AnsiColor.BrightGreen);
                    break;
                case InstructionOperandType.Pointer8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write(operand.Int.ToString(), AnsiColor.BrightGreen);
                    writer.Write(']');
                    break;
                case InstructionOperandType.Pointer16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write(operand.Int.ToString(), AnsiColor.BrightGreen);
                    writer.Write(']');
                    break;
                case InstructionOperandType.Pointer32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write(operand.Int.ToString(), AnsiColor.BrightGreen);
                    writer.Write(']');
                    break;
                case InstructionOperandType.Register:
                    switch ((Register)operand.Value)
                    {
                        case Register.CodePointer: writer.Write("CP", AnsiColor.BrightBlue); break;
                        case Register.StackPointer: writer.Write("SP", AnsiColor.BrightBlue); break;
                        case Register.BasePointer: writer.Write("BP", AnsiColor.BrightBlue); break;
                        case Register.EAX: writer.Write("EAX", AnsiColor.BrightBlue); break;
                        case Register.AX: writer.Write("AX", AnsiColor.BrightBlue); break;
                        case Register.AH: writer.Write("AH", AnsiColor.BrightBlue); break;
                        case Register.AL: writer.Write("AL", AnsiColor.BrightBlue); break;
                        case Register.EBX: writer.Write("EBX", AnsiColor.BrightBlue); break;
                        case Register.BX: writer.Write("BX", AnsiColor.BrightBlue); break;
                        case Register.BH: writer.Write("BH", AnsiColor.BrightBlue); break;
                        case Register.BL: writer.Write("BL", AnsiColor.BrightBlue); break;
                        case Register.ECX: writer.Write("ECX", AnsiColor.BrightBlue); break;
                        case Register.CX: writer.Write("CX", AnsiColor.BrightBlue); break;
                        case Register.CH: writer.Write("CH", AnsiColor.BrightBlue); break;
                        case Register.CL: writer.Write("CL", AnsiColor.BrightBlue); break;
                        case Register.EDX: writer.Write("EDX", AnsiColor.BrightBlue); break;
                        case Register.DX: writer.Write("DX", AnsiColor.BrightBlue); break;
                        case Register.DH: writer.Write("DH", AnsiColor.BrightBlue); break;
                        case Register.DL: writer.Write("DL", AnsiColor.BrightBlue); break;
                        case Register.RAX: writer.Write("RAX", AnsiColor.BrightBlue); break;
                        case Register.RBX: writer.Write("RBX", AnsiColor.BrightBlue); break;
                        case Register.RCX: writer.Write("RCX", AnsiColor.BrightBlue); break;
                        case Register.RDX: writer.Write("RDX", AnsiColor.BrightBlue); break;
                    }
                    break;
                case InstructionOperandType.PointerBP8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("BP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerBP16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("BP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerBP32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("BP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerBP64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("BP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerSP8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("SP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerSP16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("SP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerSP32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("SP", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEAX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEAX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEAX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEAX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEBX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEBX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEBX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEBX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerECX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("ECX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerECX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("ECX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerECX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("ECX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerECX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("ECX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEDX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEDX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEDX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerEDX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("EDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRAX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRAX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRAX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRAX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RAX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRBX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRBX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRBX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRBX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RBX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRCX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RCX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRCX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RCX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRCX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RCX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRCX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RCX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRDX8:
                    writer.Write("u8", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRDX16:
                    writer.Write("u16", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRDX32:
                    writer.Write("u32", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                case InstructionOperandType.PointerRDX64:
                    writer.Write("u64", AnsiColor.BrightBlack);
                    writer.Write(' ');
                    writer.Write('[');
                    writer.Write("RDX", AnsiColor.BrightBlue);
                    writer.Write(']');
                    break;
                default:
                    writer.Write("?", AnsiColor.BrightBlack);
                    break;
            }
        }

        int position = processor.Registers.CodePointer;
        int begin = Math.Max(0, position - (buffer.Height / 2));
        int end = Math.Min(processor.Code.Length, Math.Max(position + (buffer.Height / 2), begin + buffer.Height));
        for (int i = begin; i < end; i++)
        {
            BufferWriter t = buffer.Text(0, i - begin);
            t.Write(' ', Math.Max(0, 5 - i.ToString().Length));
            t.Write($" {i} ", background: i == position ? AnsiColor.Red : AnsiColor.Default);
            t.Write(processor.Code[i].Opcode.ToString(), foreground: AnsiColor.Yellow);
            int pc = processor.Code[i].Opcode.ParameterCount();
            if (pc >= 1)
            {
                t.Write(" ");
                WriteOperand(ref t, processor.Code[i].Operand1);
            }
            if (pc >= 2)
            {
                t.Write(" ");
                WriteOperand(ref t, processor.Code[i].Operand2);
            }
        }
    }, ElementSize.Auto());

    Element CreateRegistersElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        static string GetRegisterLabel(Register register) => register switch
        {
            Register.BasePointer => "BP",
            Register.StackPointer => "SP",
            Register.CodePointer => "CP",
            _ => register.ToString(),
        };

        void WriteRegister(ref BufferWriter t, Register register)
        {
            if ((regLoad & (Register)0b_1111) == register)
            {
                t.Write($"l{GetRegisterLabel(regLoad)}: ", AnsiColor.BrightRed);
                t.PadTo(6);
                t.Write(processor.GetState().GetData(regLoad).ToString(), AnsiColor.BrightRed);
            }
            else if ((regStore & (Register)0b_1111) == register)
            {
                t.Write($"s{GetRegisterLabel(regStore)}: ", AnsiColor.BrightRed);
                t.PadTo(6);
                t.Write(processor.GetState().GetData(regStore).ToString(), AnsiColor.BrightRed);
            }
            else
            {
                t.Write($" {GetRegisterLabel(register | Register._4)}: ");
                t.PadTo(6);
                t.Write(processor.GetState().GetData(register | Register._4).ToString(), AnsiColor.White);
            }
        }

        buffer.Clear();
        BufferWriter t;

        t = buffer.Text(0, 0);
        WriteRegister(ref t, Register._A);

        t = buffer.Text(buffer.Width / 2, 0);
        WriteRegister(ref t, Register._B);

        t = buffer.Text(0, 1);
        WriteRegister(ref t, Register._C);

        t = buffer.Text(buffer.Width / 2, 1);
        WriteRegister(ref t, Register._D);

        t = buffer.Text(0, 2);
        WriteRegister(ref t, Register.BasePointer);

        t = buffer.Text(buffer.Width / 2, 2);
        WriteRegister(ref t, Register.StackPointer);

        t = buffer.Text(0, 3);
        WriteRegister(ref t, Register.CodePointer);

        t = buffer.Text(buffer.Width / 2, 3);
        t.Write("FLAGS: ");

        if (processor.Registers.Flags.HasFlag(Flags.Overflow)) t.Write('O');
        else t.Write('-');

        if (processor.Registers.Flags.HasFlag(Flags.Sign)) t.Write('S');
        else t.Write('-');

        if (processor.Registers.Flags.HasFlag(Flags.Zero)) t.Write('Z');
        else t.Write('-');

        if (processor.Registers.Flags.HasFlag(Flags.Carry)) t.Write('C');
        else t.Write('-');
    }, ElementSize.Fixed(5));

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

        TryDrawDot(processor.Registers.StackPointer, AnsiColor.BrightBlue, buffer);
        TryDrawDot(processor.Registers.BasePointer, AnsiColor.BrightMagenta, buffer);
        for (int i = 0; i < memLoad.Size; i++)
        {
            TryDrawDot(memLoad.Address + i, AnsiColor.BrightRed, buffer);
        }
        for (int i = 0; i < memStore.Size; i++)
        {
            TryDrawDot(memStore.Address + i, AnsiColor.BrightRed, buffer);
        }
    }, ElementSize.Percentage(0.7f));

    Element CreateStackElement()
    {
        int prevPosition = 0;

        return new Panel((in AnsiBufferSlice buffer) =>
        {
            buffer.Clear();

            if (processor.Registers.StackPointer < (prevPosition + 4))
            {
                prevPosition = processor.Registers.StackPointer - 4;
            }
            else if (processor.Registers.StackPointer > (prevPosition + buffer.Height - 1 - 4))
            {
                prevPosition = processor.Registers.StackPointer - buffer.Height + 4;
            }

            CollectedScopeInfo stackDebugInfo;
            if (!processor.DebugInformation.IsEmpty)
            { stackDebugInfo = processor.DebugInformation.GetAllScopeInformation(processor.Memory, processor.Registers.BasePointer, processor.Registers.CodePointer); }
            else
            { stackDebugInfo = CollectedScopeInfo.Empty; }

            ReadOnlySpan<CallTraceItem> callTrace = DebugUtils.TraceStack(processor.Memory, processor.Registers.BasePointer, processor.DebugInformation.IsEmpty ? null : processor.DebugInformation.StackOffsets);

            int begin = Math.Max(0, prevPosition);
            int end = Math.Min(processor.Memory.Length, Math.Max(prevPosition + buffer.Height, begin + buffer.Height));
            for (int i = begin; i < end; i++)
            {
                BufferWriter t = buffer.Text(0, i - begin);

                DataMovementEdge ld = memLoad.At(i);
                DataMovementEdge st = memStore.At(i);
                if (ld == DataMovementEdge.None)
                {
                    switch (st)
                    {
                        case DataMovementEdge.None: t.Write(' '); break;
                        case DataMovementEdge.Start: t.Write("s", AnsiColor.BrightRed); break;
                        case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                        case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                    }
                }
                else
                {
                    switch (ld)
                    {
                        case DataMovementEdge.None: t.Write(' '); break;
                        case DataMovementEdge.Start: t.Write("l", AnsiColor.BrightRed); break;
                        case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                        case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                    }
                }

                if (processor.Registers.BasePointer == i) t.Write("■", AnsiColor.BrightMagenta);
                else if (callTrace.Contains(v => v.BasePointer == i)) t.Write("■", AnsiColor.BrightBlack);
                else t.Write(' ');

                if (processor.Registers.StackPointer == i) t.Write("▶", AnsiColor.BrightBlue);
                else t.Write(' ');

                t.Write($"{i,5} ");
                t.Write(processor.Memory[i].ToString(), processor.Memory[i] == 0 ? AnsiColor.BrightBlack : AnsiColor.White);

                foreach (StackElementInformation item in stackDebugInfo.Stack)
                {
                    int a = item.AbsoluteAddress(processor.Registers.BasePointer, processor.StackStart);
                    if (a == i)
                    {
                        t.PadTo(13);
                        if (item.Size == 1) t.Write(' ');
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
                    else if (i > a && i < a + item.Size - 1)
                    {
                        t.PadTo(13);
                        t.Write('│');
                        break;
                    }
                    else if (i > a && i == a + item.Size - 1)
                    {
                        t.PadTo(13);
                        t.Write('└');
                        break;
                    }
                }
            }
        }, ElementSize.Auto());
    }

    Element CreateHeapElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();

        CollectedScopeInfo stackDebugInfo;
        if (!processor.DebugInformation.IsEmpty)
        { stackDebugInfo = processor.DebugInformation.GetAllScopeInformation(processor.Memory, processor.Registers.BasePointer, processor.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        int position = 0;
        int begin = Math.Max(0, position - (buffer.Height / 2));
        int end = Math.Min(processor.Memory.Length, Math.Max(position + (buffer.Height / 2), begin + buffer.Height));
        for (int i = begin; i < end; i++)
        {
            BufferWriter t = buffer.Text(0, i - begin);

            DataMovementEdge ld = memLoad.At(i);
            DataMovementEdge st = memStore.At(i);
            if (ld == DataMovementEdge.None)
            {
                switch (st)
                {
                    case DataMovementEdge.None: t.Write(' '); break;
                    case DataMovementEdge.Start: t.Write("s", AnsiColor.BrightRed); break;
                    case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                    case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                }
            }
            else
            {
                switch (ld)
                {
                    case DataMovementEdge.None: t.Write(' '); break;
                    case DataMovementEdge.Start: t.Write("l", AnsiColor.BrightRed); break;
                    case DataMovementEdge.Middle: t.Write("╎", AnsiColor.BrightRed); break;
                    case DataMovementEdge.End: t.Write("╎", AnsiColor.BrightRed); break;
                }
            }

            t.Write($"{i,5} ");
            t.Write(processor.Memory[i].ToString(), processor.Memory[i] == 0 ? AnsiColor.BrightBlack : AnsiColor.White);

            foreach (StackElementInformation item in stackDebugInfo.Stack)
            {
                if (item.Type is not PointerType pointerType) continue;
                int pointerAddress = item.AbsoluteAddress(processor.Registers.BasePointer, processor.StackStart);
                int valueAddress = processor.Memory.AsSpan().Get<int>(pointerAddress);
                if (valueAddress <= 0) continue;

                if (!pointerType.To.GetSize(new RuntimeInfoProvider()
                {
                    PointerSize = CodeGeneratorForMain.DefaultCompilerSettings.PointerSize,
                }, out int valueSize, out _))
                {
                    valueSize = 0;
                }

                if (valueAddress == i)
                {
                    t.PadTo(13);
                    if (valueSize <= 1) t.Write(' ');
                    else t.Write('┌');
                    WriteType(ref t, pointerType.To);
                    t.Write(' ');
                    if (item.Kind == StackElementKind.Internal)
                    {
                        t.Write(item.Identifier, AnsiColor.BrightBlack);
                    }
                    else
                    {
                        t.Write(item.Identifier);
                    }
                    switch (pointerType.To.FinalValue)
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
                else if (i > valueAddress && i < valueAddress + valueSize - 1)
                {
                    t.PadTo(13);
                    t.Write('│');
                    break;
                }
                else if (i > valueAddress && i == valueAddress + valueSize - 1)
                {
                    t.PadTo(13);
                    t.Write('└');
                    break;
                }
            }

        }
    }, ElementSize.Auto());

    Element CreateStackTraceElement() => new Panel((in AnsiBufferSlice buffer) =>
    {
        buffer.Clear();

        CollectedScopeInfo stackDebugInfo;
        if (!processor.DebugInformation.IsEmpty)
        { stackDebugInfo = processor.DebugInformation.GetAllScopeInformation(processor.Memory, processor.Registers.BasePointer, processor.Registers.CodePointer); }
        else
        { stackDebugInfo = CollectedScopeInfo.Empty; }

        ReadOnlySpan<CallTraceItem> callTrace = DebugUtils.TraceStack(processor.Memory, processor.Registers.BasePointer, processor.DebugInformation.IsEmpty ? null : processor.DebugInformation.StackOffsets);

        int position = callTrace.Length - 1;
        int begin = Math.Max(0, position - (buffer.Height / 2));
        int end = Math.Min(callTrace.Length, Math.Max(position + (buffer.Height / 2), begin + buffer.Height));
        for (int i = begin; i < end; i++)
        {
            BufferWriter t = buffer.Text(0, i - begin);
            CallTraceItem frame = callTrace[i];
            FunctionInformation function = processor.DebugInformation.GetFunctionInformation(frame.InstructionPointer);
            if (!function.IsValid)
            {
                t.Write(frame.InstructionPointer.ToString());
                continue;
            }

            if (function.Function is ICompiledFunctionDefinition compiledFunction)
            {
                t.Write(function.Function.Identifier.Content, AnsiColor.Yellow);
                t.Write('(');
                for (int j = 0; j < compiledFunction.Parameters.Count; j++)
                {
                    if (j > 0) t.Write(", ");
                    for (int k = 0; k < compiledFunction.Parameters[j].Modifiers.Length; k++)
                    {
                        t.Write(compiledFunction.Parameters[j].Modifiers[k].Content, AnsiColor.Blue);
                        t.Write(' ');
                    }
                    WriteType(ref t, GeneralType.InsertTypeParameters(compiledFunction.ParameterTypes[j], function.TypeArguments) ?? compiledFunction.ParameterTypes[j]);
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
        Container container = new(FlowDirection.Horizontal, ElementSize.Percentage(1f), new Element[] {
            new Container(FlowDirection.Vertical, ElementSize.Percentage(0.3f), new Element[]
            {
                CreateSourceElement().WithBorders("Source"),
                CreateCodeElement().WithBorders("Code"),
            }),
            CreateMemoryElement().WithBorders("Memory")
            .Invisible(),
            new Container(FlowDirection.Vertical, ElementSize.Percentage(0.7f), new Element[]
            {
                CreateRegistersElement().WithBorders("Registers"),
                new Container(FlowDirection.Horizontal, ElementSize.Auto(), new Element[]
                {
                    CreateStackElement().WithBorders("Stack"),
                    CreateHeapElement().WithBorders("Heap"),
                    CreateStackTraceElement().WithBorders("Call Stack"),
                }),
            }),
        });
        Renderer renderer = new();

        bool cancel = false;
        Console.CancelKeyPress += (sender, e) =>
        {
            cancel = true;
            e.Cancel = true;
        };

        int tick = int.MaxValue;
        int freq = 100;

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

                (memLoad, memStore, regLoad, regStore) = GetDataMovementIndicators(processor);

                renderer.Render(container);

                for (int i = 0; tick > 0 && i < freq; i++)
                {
                    processor.Tick();
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
