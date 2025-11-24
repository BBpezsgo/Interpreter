using System.IO;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public class BytecodeEmitter
{
    public readonly struct BytecodeJump : IEquatable<BytecodeJump>
    {
        public static readonly BytecodeJump Invalid = new(-1);

        public readonly int Index;

        public BytecodeJump(int index)
        {
            Index = index;
        }

        public override bool Equals(object? obj) => obj is BytecodeJump other && Index == other.Index;
        public bool Equals(BytecodeJump other) => Index == other.Index;
        public override int GetHashCode() => Index;

        public static bool operator ==(BytecodeJump left, BytecodeJump right) => left.Index == right.Index;
        public static bool operator !=(BytecodeJump left, BytecodeJump right) => left.Index != right.Index;
    }

    public GeneratorOptimizationSettings Optimizations { get; init; }
    public required DebugInformation DebugInfo { get; init; }

    readonly List<PreparationInstruction> Code = new();
    readonly List<InstructionLabel> Labels = new();

    public int Offset => Code.Count;

    public void WriteTo(StreamWriter writer, bool comments)
    {
        if (!comments)
        {
            for (int i = 0; i < Code.Count; i++)
            {
                foreach (InstructionLabel label in Labels)
                {
                    if (label.Index != i) continue;
                    writer.WriteLine($"{label}:");
                }
                writer.Write("  ");
                writer.WriteLine(Code[i].ToString());
            }

            return;
        }
        int indent = 0;

        for (int i = 0; i < Code.Count; i++)
        {
            FunctionInformation f = DebugInfo.FunctionInformation.FirstOrDefault(v => v.Instructions.Contains(i));

            int subindent = 0;

            if (f.IsValid)
            {
                subindent += 2;
                if (f.Instructions.Start == i)
                {
                    writer.WriteLine(f.Function?.ToReadable() ?? f.ReadableIdentifier);
                    writer.WriteLine('{');
                }
            }

            if (DebugInfo.CodeComments.TryGetValue(i, out List<string>? _comments))
            {
                foreach (string comment in _comments)
                {
                    foreach (char item in comment)
                    {
                        if (item == '{') indent++;
                    }
                    writer.Write(new string(' ', indent + subindent));
                    writer.WriteLine(comment);
                    foreach (char item in comment)
                    {
                        if (item == '}' && indent > 0) indent--;
                    }
                }
            }

            foreach (InstructionLabel label in Labels)
            {
                if (label.Index != i) continue;
                writer.Write(new string(' ', indent + subindent));
                writer.WriteLine($"{label}:");
            }
            writer.Write(new string(' ', indent + subindent));
            writer.Write("  ");
            writer.WriteLine(Code[i].ToString());

            if (f.IsValid)
            {
                if (f.Instructions.End == i + 1)
                {
                    writer.WriteLine('}');
                }
            }
        }
    }

    void RemoveAt(int index)
    {
        Code.RemoveAt(index);
        for (int i = 0; i < Labels.Count; i++)
        {
            InstructionLabel v = Labels[i];
            if (v.Index > index)
            {
                v.Index--;
                Labels[i] = v;
            }
        }
        DebugInfo.OffsetCodeFrom(index, -1);
    }

    static bool DoesOverlap(InstructionOperand a, InstructionOperand b)
    {
        if (a.Type.IsImmediate()) return false;
        if (b.Type.IsImmediate()) return false;

        if (a.Type is InstructionOperandType.Register) return false;
        if (b.Type is InstructionOperandType.Register) return false;

        if (a.Type.IsRegisterPointer() && b.Type.IsRegisterPointer())
        {
            if (a.Type.RegisterOfPointer() == b.Type.RegisterOfPointer())
            {
                return RangeUtils.Overlaps(
                    new Range<int>(a.Value, a.Value + (int)a.Type.BitwidthOfPointer()),
                    new Range<int>(b.Value, b.Value + (int)b.Type.BitwidthOfPointer())
                );
            }
            else
            {
                return true;
            }
        }

        if (a.Type.IsPointer() && b.Type.IsPointer())
        {
            return RangeUtils.Overlaps(
                new Range<int>(a.Value, a.Value + (int)a.Type.BitwidthOfPointer()),
                new Range<int>(b.Value, b.Value + (int)b.Type.BitwidthOfPointer())
            );
        }

        return true;
    }

    bool OptimizeCodeAt(int i)
    {
        if (!Optimizations.HasFlag(GeneratorOptimizationSettings.BytecodeLevel)) return false;

        PreparationInstruction prev0 = Code[i];

        if (prev0.Opcode
            is Opcode.Jump
            or Opcode.JumpIfEqual
            or Opcode.JumpIfGreaterS
            or Opcode.JumpIfGreaterOrEqualS
            or Opcode.JumpIfLessS
            or Opcode.JumpIfLessOrEqualS
            or Opcode.JumpIfGreaterU
            or Opcode.JumpIfGreaterOrEqualU
            or Opcode.JumpIfLessU
            or Opcode.JumpIfLessOrEqualU
            or Opcode.JumpIfNotEqual
            && prev0.Operand1.IsLabelAddress
            && prev0.Operand1.LabelValue.AdditionalLabelOffset == 0
            && prev0.Operand1.LabelValue.IsAbsoluteLabelAddress == false
            && GetLabelIndex(prev0.Operand1.LabelValue.Label) == i + 1)
        {
            RemoveAt(i);
            return true;
        }

        if (prev0.Operand1.IsLabelAddress || prev0.Operand2.IsLabelAddress) return false;

        if (prev0.Opcode == Opcode.Move
            && prev0.Operand1 == prev0.Operand2)
        {
            RemoveAt(i);
            return true;
        }

        if ((prev0.Opcode is Opcode.MathAdd or Opcode.MathSub)
            && prev0.Operand2.Value == 0)
        {
            RemoveAt(i);
            return true;
        }

        if (i < 1) return false;

        if (Labels.Any(v => v.Index == i)) return false;

        PreparationInstruction prev1 = Code[i - 1];
        if (prev1.Operand1.IsLabelAddress || prev1.Operand2.IsLabelAddress) return false;

        if (prev1.Opcode == Opcode.Move
            && prev1.Operand1.Value.Type == InstructionOperandType.Register
            && prev1.Operand2.Value.Type == InstructionOperandType.Register

            && prev0.Opcode == Opcode.Move
            && prev0.Operand1.Value == prev1.Operand1.Value
            && prev0.Operand2.Value.Type == prev1.Operand1.Value.Reg.ToPtr(prev0.Operand1.Value.BitWidth)

            && !DoesOverlap(prev0.Operand1.Value, prev1.Operand2.Value.Reg.ToPtr(prev0.Operand2.Value.Value, prev0.Operand1.Value.BitWidth)))
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Move,
                prev0.Operand1.Value,
                prev1.Operand2.Value.Reg.ToPtr(prev0.Operand2.Value.Value, prev0.Operand1.Value.BitWidth)
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.Type == InstructionOperandType.Immediate16

            && prev0.Opcode == Opcode.Push
            && prev0.Operand1.Value.Type == InstructionOperandType.Immediate16)
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Push,
                new InstructionOperand((prev0.Operand1.Value.Value) | (prev1.Operand1.Value.Value << 16), InstructionOperandType.Immediate32)
            );
            return true;
        }

        if (prev1.Opcode == Opcode.MathAdd
            && prev1.Operand2.Value.Type == InstructionOperandType.Immediate32

            && prev0.Opcode == Opcode.MathAdd
            && prev0.Operand2.Value.Type == InstructionOperandType.Immediate32
            && prev0.Operand1.Value == prev1.Operand1.Value)
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.MathAdd,
                prev0.Operand1,
                prev1.Operand2.Value.Value + prev0.Operand2.Value.Value
            );
            return true;
        }

        if (prev0.Opcode == Opcode.MathSub
            && prev0.Operand2.Value.Type == InstructionOperandType.Immediate32

            && prev1.Opcode == Opcode.MathSub
            && prev1.Operand1 == prev0.Operand1
            && prev1.Operand2.Value.Type == InstructionOperandType.Immediate32)
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.MathSub,
                prev0.Operand1,
                prev1.Operand2.Value.Value + prev0.Operand2.Value.Value
            );
            return true;
        }

        if (prev0.Opcode == Opcode.MathSub
            && prev0.Operand2.Value.Type == InstructionOperandType.Immediate32

            && prev1.Opcode == Opcode.MathAdd
            && prev1.Operand1 == prev0.Operand1
            && prev1.Operand2.Value.Type == InstructionOperandType.Immediate32)
        {
            RemoveAt(i--);

            int v = -prev0.Operand2.Value.Value + prev1.Operand2.Value.Value;
            Code[i] = new PreparationInstruction(
                v < 0 ? Opcode.MathSub : Opcode.MathAdd,
                prev0.Operand1,
                Math.Abs(v)
            );
            return true;
        }

        if (prev0.Opcode == Opcode.MathAdd
            && prev0.Operand2.Value.Type == InstructionOperandType.Immediate32

            && prev1.Opcode == Opcode.MathSub
            && prev1.Operand1 == prev0.Operand1
            && prev1.Operand2.Value.Type == InstructionOperandType.Immediate32)
        {
            RemoveAt(i--);

            int v = prev0.Operand2.Value.Value + -prev1.Operand2.Value.Value;
            Code[i] = new PreparationInstruction(
                v < 0 ? Opcode.MathSub : Opcode.MathAdd,
                prev0.Operand1,
                Math.Abs(v)
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._64

            && prev0.Opcode == Opcode.PopTo64

            && !DoesOverlap(prev0.Operand1.Value, prev1.Operand1.Value))
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Move,
                prev0.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._32

            && prev0.Opcode == Opcode.PopTo32

            && !DoesOverlap(prev0.Operand1.Value, prev1.Operand1.Value))
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Move,
                prev0.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._16

            && prev0.Opcode == Opcode.PopTo16

            && !DoesOverlap(prev0.Operand1.Value, prev1.Operand1.Value))
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Move,
                prev0.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._8

            && prev0.Opcode == Opcode.PopTo8

            && !DoesOverlap(prev0.Operand1.Value, prev1.Operand1.Value))
        {
            RemoveAt(i--);
            Code[i] = new PreparationInstruction(
                Opcode.Move,
                prev0.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._64

            && prev0.Opcode == Opcode.MathAdd
            && prev0.Operand1 == Register.StackPointer
            && prev0.Operand2 == 8)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._32

            && prev0.Opcode == Opcode.MathAdd
            && prev0.Operand1 == Register.StackPointer
            && prev0.Operand2 == 4)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._16

            && prev0.Opcode == Opcode.MathAdd
            && prev0.Operand1 == Register.StackPointer
            && prev0.Operand2 == 2)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._8

            && prev0.Opcode == Opcode.MathAdd
            && prev0.Operand1 == Register.StackPointer
            && prev0.Operand2 == 1)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        return false;
    }

    bool OptimizeCodeAtWithFinishedRegister(int i, Register finishedRegister)
    {
        if (!Optimizations.HasFlag(GeneratorOptimizationSettings.BytecodeLevel)) return false;

        PreparationInstruction prev0 = Code[i];

        if (prev0.Operand1.IsLabelAddress || prev0.Operand2.IsLabelAddress) return false;

        if (i < 1) return false;

        if (Labels.Any(v => v.Index == i)) return false;

        PreparationInstruction prev1 = Code[i - 1];
        if (prev1.Operand1.IsLabelAddress || prev1.Operand2.IsLabelAddress) return false;

        if (prev1.Opcode == Opcode.Move
            && prev1.Operand1 == finishedRegister

            && prev0.Opcode == Opcode.Push
            && prev0.Operand1 == finishedRegister)
        {
            RemoveAt(i--);
            Code[i] = new(
                Opcode.Push,
                prev1.Operand2
            );
            return true;
        }

        return false;
    }

    internal void FinishUsingRegister(Register register)
    {
        while (OptimizeCodeAt(Code.Count - 1) || OptimizeCodeAtWithFinishedRegister(Code.Count - 1, register))
        {

        }
    }

    public void Emit(PreparationInstruction instruction)
    {
        Code.Add(instruction);
        while (OptimizeCodeAt(Code.Count - 1))
        {

        }
    }

    public void Emit(
        Opcode opcode)
        => Emit(new PreparationInstruction(opcode));

    public void Emit(Opcode opcode,
        PreparationInstructionOperand operand1)
        => Emit(new PreparationInstruction(opcode, operand1));

    public void Emit(Opcode opcode,
        PreparationInstructionOperand operand1,
        PreparationInstructionOperand operand2)
        => Emit(new PreparationInstruction(opcode, operand1, operand2));

    InstructionLabel DefineLabelImpl(int offset)
    {
        int id = Labels.Count;
        while (Labels.Any(v => v == new InstructionLabel(default, id)))
        {
            id++;
        }
        InstructionLabel label = new(offset, id);
        Labels.Add(label);
        return label;
    }

    public InstructionLabel DefineLabel() => DefineLabelImpl(-1);
    public InstructionLabel MarkLabel() => DefineLabelImpl(Offset);

    public void MarkLabel(InstructionLabel label)
    {
        for (int i = 0; i < Labels.Count; i++)
        {
            InstructionLabel v = Labels[i];
            if (v != label) continue;
            v.Index = Offset;
            Labels[i] = v;
        }
    }

    int GetLabelIndex(InstructionLabel label)
    {
        foreach (InstructionLabel _label in Labels)
        {
            if (_label != label) continue;
            return _label.Index;
        }
        throw new KeyNotFoundException($"Label {label} not found");
    }

    InstructionOperand Compile(PreparationInstructionOperand v, int i)
    {
        if (!v.IsLabelAddress) return v.Value;
        int label = GetLabelIndex(v.LabelValue.Label);
        if (label == -1) throw new InternalExceptionWithoutContext($"Label is not marked");
        if (label == -2) throw new InternalExceptionWithoutContext($"Label is invalid");
        if (label < -2) throw new UnreachableException();
        if (v.LabelValue.IsAbsoluteLabelAddress)
        {
            return new InstructionOperand(label + v.LabelValue.AdditionalLabelOffset, InstructionOperandType.Immediate32);
        }
        else
        {
            return new InstructionOperand(label - i + v.LabelValue.AdditionalLabelOffset, InstructionOperandType.Immediate32);
        }
    }

    Instruction Compile(PreparationInstruction v, int i)
    {
        return new Instruction(v.Opcode, Compile(v.Operand1, i), Compile(v.Operand2, i));
    }

    void PurgeLabels()
    {
        for (int i = Labels.Count - 1; i >= 0; i--)
        {
            InstructionLabel label = Labels[i];
            if (Code.Any(v => (v.Operand1.IsLabelAddress && v.Operand1.LabelValue.Label == label) || (v.Operand2.IsLabelAddress && v.Operand2.LabelValue.Label == label)))
            { continue; }
            Labels.RemoveSwapBack(i);
        }
    }

    public ImmutableArray<Instruction> Compile()
    {
        bool notDone;
        do
        {
            notDone = false;
            for (int i = Code.Count - 1; i >= 0; i--)
            {
                if (OptimizeCodeAt(i))
                {
                    PurgeLabels();
                    notDone = true;
                    break;
                }
            }
        } while (notDone);

        ImmutableArray<Instruction>.Builder result = ImmutableArray.CreateBuilder<Instruction>(Code.Count);
        for (int i = 0; i < Code.Count; i++)
        {
            result.Add(Compile(Code[i], i));
        }
        return result.MoveToImmutable();
    }
}
