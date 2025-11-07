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

    public bool EnableOptimizations { get; set; }

    readonly List<PreparationInstruction> Code = new();
    readonly List<InstructionLabel> Labels = new();

    public int Offset => Code.Count;

    public void WriteTo(StreamWriter writer)
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
    }

    bool OptimizeCodeAt(int i)
    {
        if (!EnableOptimizations) return false;

        PreparationInstruction prev0 = Code[i];

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

        if (prev0.Opcode == Opcode.Pop64)
        {
            Code[i] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                8
            );
            return true;
        }

        if (prev0.Opcode == Opcode.Pop32)
        {
            Code[i] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                4
            );
            return true;
        }

        if (prev0.Opcode == Opcode.Pop16)
        {
            Code[i] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                2
            );
            return true;
        }

        if (prev0.Opcode == Opcode.Pop8)
        {
            Code[i] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                1
            );
            return true;
        }

        if (i < 1) return false;

        if (Labels.Any(v => v.Index == i)) return false;

        PreparationInstruction prev1 = Code[i - 1];
        if (prev1.Operand1.IsLabelAddress || prev1.Operand2.IsLabelAddress) return false;

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

            && prev0.Opcode == Opcode.PopTo64)
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

            && prev0.Opcode == Opcode.PopTo32)
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

            && prev0.Opcode == Opcode.PopTo16)
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

            && prev0.Opcode == Opcode.PopTo8)
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
            && prev0.Opcode == Opcode.Pop64)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._32
            && prev0.Opcode == Opcode.Pop32)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._16
            && prev0.Opcode == Opcode.Pop16)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Value.BitWidth == BitWidth._8
            && prev0.Opcode == Opcode.Pop8)
        {
            RemoveAt(i--);
            RemoveAt(i);
            return true;
        }

        return false;
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

    InstructionOperand Compile(PreparationInstructionOperand v, int i)
    {
        if (!v.IsLabelAddress) return v.Value;
        InstructionLabel label;
        foreach (InstructionLabel _label in Labels)
        {
            if (_label == v.LabelValue.Label)
            {
                label = _label;
                goto ok;
            }
        }
        throw new UnreachableException();
    ok:
        if (label.Index == -1) throw new InternalExceptionWithoutContext($"Label is not marked");
        if (label.Index == -2) throw new InternalExceptionWithoutContext($"Label is invalid");
        if (label.Index < -2) throw new UnreachableException();
        if (v.LabelValue.IsAbsoluteLabelAddress)
        {
            return new InstructionOperand(label.Index + v.LabelValue.AdditionalLabelOffset, InstructionOperandType.Immediate32);
        }
        else
        {
            return new InstructionOperand(label.Index - i + v.LabelValue.AdditionalLabelOffset, InstructionOperandType.Immediate32);
        }
    }

    Instruction Compile(PreparationInstruction v, int i)
    {
        return new Instruction(v.Opcode, Compile(v.Operand1, i), Compile(v.Operand2, i));
    }

    public ImmutableArray<Instruction> Compile()
    {
        bool notDone = false;
        do
        {
            for (int i = Code.Count - 1; i >= 0; i--)
            {
                if (OptimizeCodeAt(i))
                {
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
