using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public partial class CodeGeneratorForMain
{
    bool AllowInstructionLevelOptimizations => !Settings.DontOptimize;

    bool OptimizeCode()
    {
        if (!AllowInstructionLevelOptimizations) return false;
        if (GeneratedCode.Count == 0) return false;

        PreparationInstruction instruction = GeneratedCode[^1];
        if (IsBytecodeLabeled(GeneratedCode.Count - 1)) return false;

        // WHYYYYY
        //if ((instruction.Opcode is
        //    Opcode.Jump or
        //    Opcode.JumpIfEqual or
        //    Opcode.JumpIfGreater or
        //    Opcode.JumpIfGreaterOrEqual or
        //    Opcode.JumpIfLess or
        //    Opcode.JumpIfLessOrEqual or
        //    Opcode.JumpIfNotEqual) &&
        //    instruction.Operand1 == 1)
        //{
        //    GeneratedCode.Pop();
        //    return true;
        //}

        if (instruction.Opcode == Opcode.Move
            && instruction.Operand1 == instruction.Operand2)
        {
            GeneratedCode.Pop();
            return true;
        }

        if ((instruction.Opcode is Opcode.MathAdd or Opcode.MathSub)
            && instruction.Operand2 == 0)
        {
            GeneratedCode.Pop();
            return true;
        }

        if (GeneratedCode.Count < 2) return false;

        PreparationInstruction prev1 = GeneratedCode[^2];
        if (IsBytecodeLabeled(GeneratedCode.Count - 2)) return false;

        //if (prev1.Opcode == Opcode.MathAdd
        //    && prev1.Operand1.Type == InstructionOperandType.Register && prev1.Operand1.Reg.IsGeneralPurpose()
        //    && prev1.Operand2.Type == InstructionOperandType.Immediate32
        //    && instruction.Opcode == Opcode.Move
        //    && instruction.Operand2.Type == prev1.Operand1.Reg.ToPtr(instruction.Operand1.BitWidth))
        //{
        //    GeneratedCode.Pop();
        //    GeneratedCode[^1] = new(
        //        Opcode.Move,
        //        instruction.Operand1,
        //        prev1.Operand1.Reg.ToPtr(prev1.Operand2.Value, instruction.Operand1.BitWidth)
        //    );
        //}

        //if (prev1.Opcode == Opcode.MathAdd
        //    && prev1.Operand1.Type == InstructionOperandType.Register && prev1.Operand1.Reg.IsGeneralPurpose()
        //    && prev1.Operand2.Type == InstructionOperandType.Immediate32
        //    && instruction.Opcode == Opcode.PopTo32
        //    && instruction.Operand1.Type == prev1.Operand1.Reg.ToPtr(BitWidth._32))
        //{
        //    GeneratedCode.Pop();
        //    GeneratedCode[^1] = new(
        //        Opcode.PopTo32,
        //        prev1.Operand1.Reg.ToPtr(prev1.Operand2.Value, BitWidth._32)
        //    );
        //}

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.Type == InstructionOperandType.Immediate16
            && instruction.Opcode == Opcode.Push
            && instruction.Operand1.Type == InstructionOperandType.Immediate16)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.Push,
                new InstructionOperand((instruction.Operand1.Value) | (prev1.Operand1.Value << 16), InstructionOperandType.Immediate32)
            );
            return true;
        }

        // FIXME: the two moves can overlap
        if (prev1.Opcode == Opcode.Move
            && instruction.Opcode == Opcode.Move
            && instruction.Operand1 == prev1.Operand1
            && !(
                prev1.Operand1.Type == InstructionOperandType.Register
                && instruction.Operand2.Type == prev1.Operand1.Reg.ToPtr(instruction.Operand1.BitWidth)
            ))
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = instruction;
            return true;
        }

        if (instruction.Opcode == Opcode.MathAdd
            && instruction.Operand2.Type == InstructionOperandType.Immediate32
            && prev1.Opcode == Opcode.MathAdd
            && prev1.Operand1 == instruction.Operand1
            && prev1.Operand2.Type == InstructionOperandType.Immediate32)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.MathAdd,
                instruction.Operand1,
                prev1.Operand2.Value + instruction.Operand2.Value
            );
            return true;
        }

        if (instruction.Opcode == Opcode.MathSub
            && instruction.Operand2.Type == InstructionOperandType.Immediate32
            && prev1.Opcode == Opcode.MathSub
            && prev1.Operand1 == instruction.Operand1
            && prev1.Operand2.Type == InstructionOperandType.Immediate32)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.MathSub,
                instruction.Operand1,
                prev1.Operand2.Value + instruction.Operand2.Value
            );
            return true;
        }

        if (instruction.Opcode == Opcode.MathSub
            && instruction.Operand2.Type == InstructionOperandType.Immediate32
            && prev1.Opcode == Opcode.MathAdd
            && prev1.Operand1 == instruction.Operand1
            && prev1.Operand2.Type == InstructionOperandType.Immediate32)
        {
            GeneratedCode.Pop();

            int v = -instruction.Operand2.Value + prev1.Operand2.Value;
            GeneratedCode[^1] = new PreparationInstruction(
                v < 0 ? Opcode.MathSub : Opcode.MathAdd,
                instruction.Operand1,
                Math.Abs(v)
            );
            return true;
        }

        if (instruction.Opcode == Opcode.MathAdd
            && instruction.Operand2.Type == InstructionOperandType.Immediate32
            && prev1.Opcode == Opcode.MathSub
            && prev1.Operand1 == instruction.Operand1
            && prev1.Operand2.Type == InstructionOperandType.Immediate32)
        {
            GeneratedCode.Pop();

            int v = instruction.Operand2.Value + -prev1.Operand2.Value;
            GeneratedCode[^1] = new PreparationInstruction(
                v < 0 ? Opcode.MathSub : Opcode.MathAdd,
                instruction.Operand1,
                Math.Abs(v)
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._64
            && instruction.Opcode == Opcode.PopTo64)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.Move,
                instruction.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._32
            && instruction.Opcode == Opcode.PopTo32)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.Move,
                instruction.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._16
            && instruction.Opcode == Opcode.PopTo16)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.Move,
                instruction.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._8
            && instruction.Opcode == Opcode.PopTo8)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.Move,
                instruction.Operand1,
                prev1.Operand1
            );
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._64
            && instruction.Opcode == Opcode.Pop64)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(Opcode.NOP);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._32
            && instruction.Opcode == Opcode.Pop32)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(Opcode.NOP);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._16
            && instruction.Opcode == Opcode.Pop16)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(Opcode.NOP);
            return true;
        }

        if (prev1.Opcode == Opcode.Push
            && prev1.Operand1.BitWidth == BitWidth._8
            && instruction.Opcode == Opcode.Pop8)
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(Opcode.NOP);
            return true;
        }

        if (prev1.Opcode == Opcode.Move
            && prev1.Operand1.Type == InstructionOperandType.Register && ((Register)prev1.Operand1.Value).IsGeneralPurpose()
            && prev1.Operand2.Type == InstructionOperandType.Register

            && instruction.Opcode == Opcode.Move
            && instruction.Operand1.Type == InstructionOperandType.Register
            && instruction.Operand2.Type == ((Register)prev1.Operand1.Value).ToPtr(((Register)instruction.Operand1.Value).BitWidth()))
        {
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(Opcode.Move, instruction.Operand1, ((Register)prev1.Operand2.Value).ToPtr(instruction.Operand2.Value, ((Register)instruction.Operand1.Value).BitWidth()));
            return true;
        }

        if (prev1.Opcode is Opcode.Pop64 or Opcode.Pop32 or Opcode.Pop16 or Opcode.Pop8
            && instruction.Opcode is Opcode.Pop64 or Opcode.Pop32 or Opcode.Pop16 or Opcode.Pop8)
        {
            int a = prev1.Opcode switch
            {
                Opcode.Pop64 => 8,
                Opcode.Pop32 => 4,
                Opcode.Pop16 => 2,
                Opcode.Pop8 => 1,
                _ => throw new UnreachableException(),
            };
            int b = instruction.Opcode switch
            {
                Opcode.Pop64 => 8,
                Opcode.Pop32 => 4,
                Opcode.Pop16 => 2,
                Opcode.Pop8 => 1,
                _ => throw new UnreachableException(),
            };
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                a + b
            );
            return true;
        }

        if (prev1.Opcode == Opcode.MathAdd
            && prev1.Operand1 == Register.StackPointer
            && prev1.Operand2.Type == InstructionOperandType.Immediate32
            && instruction.Opcode is Opcode.Pop64 or Opcode.Pop32 or Opcode.Pop16 or Opcode.Pop8)
        {
            int a = instruction.Opcode switch
            {
                Opcode.Pop64 => 8,
                Opcode.Pop32 => 4,
                Opcode.Pop16 => 2,
                Opcode.Pop8 => 1,
                _ => throw new UnreachableException(),
            };
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                a + prev1.Operand2.Value
            );
            return true;
        }

        if (prev1.Opcode is Opcode.Pop64 or Opcode.Pop32 or Opcode.Pop16 or Opcode.Pop8
            && instruction.Opcode == Opcode.MathAdd
            && instruction.Operand1 == Register.StackPointer
            && instruction.Operand2.Type == InstructionOperandType.Immediate32)
        {
            int a = prev1.Opcode switch
            {
                Opcode.Pop64 => 8,
                Opcode.Pop32 => 4,
                Opcode.Pop16 => 2,
                Opcode.Pop8 => 1,
                _ => throw new UnreachableException(),
            };
            GeneratedCode.Pop();
            GeneratedCode[^1] = new PreparationInstruction(
                Opcode.MathAdd,
                Register.StackPointer,
                a + instruction.Operand2.Value
            );
            return true;
        }

        return false;
    }

    void AddInstruction(PreparationInstruction instruction)
    {
        GeneratedCode.Add(instruction);
        while (OptimizeCode())
        {
            _statistics.InstructionLevelOptimizations++;
        }
    }

    void AddInstruction(
        Opcode opcode)
        => AddInstruction(new PreparationInstruction(opcode));

    void AddInstruction(
        Opcode opcode,
        int operand)
        => AddInstruction(new PreparationInstruction(opcode, new InstructionOperand(new CompiledValue(operand))));

    void AddInstruction(Opcode opcode,
        InstructionOperand operand1)
        => AddInstruction(new PreparationInstruction(opcode, operand1));

    void AddInstruction(Opcode opcode,
        InstructionOperand operand1,
        InstructionOperand operand2)
        => AddInstruction(new PreparationInstruction(opcode, operand1, operand2));

    void AddComment(string comment)
    {
        if (DebugInfo is null) return;
        if (DebugInfo.CodeComments.TryGetValue(GeneratedCode.Count, out List<string>? comments))
        { comments.Add(comment); }
        else
        { DebugInfo.CodeComments.Add(GeneratedCode.Count, new List<string>() { comment }); }
    }
}

