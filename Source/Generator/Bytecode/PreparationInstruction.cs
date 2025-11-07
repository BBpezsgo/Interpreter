using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public readonly struct LabelInstructionOperand : IEquatable<LabelInstructionOperand>
{
    public readonly bool IsAbsoluteLabelAddress;
    public readonly int AdditionalLabelOffset;
    public readonly InstructionLabel Label;

    public LabelInstructionOperand(InstructionLabel label, bool isAbsoluteLabelAddress, int additionalLabelOffset)
    {
        Label = label;
        IsAbsoluteLabelAddress = isAbsoluteLabelAddress;
        AdditionalLabelOffset = additionalLabelOffset;
    }

    public override bool Equals(object? obj) => obj is LabelInstructionOperand other && Equals(other);
    public bool Equals(LabelInstructionOperand other) =>
        IsAbsoluteLabelAddress == other.IsAbsoluteLabelAddress
        && AdditionalLabelOffset == other.AdditionalLabelOffset
        && Label.Equals(other.Label);
    public override int GetHashCode() => HashCode.Combine(IsAbsoluteLabelAddress, AdditionalLabelOffset, Label);

    public static bool operator ==(LabelInstructionOperand left, LabelInstructionOperand right) => left.Equals(right);
    public static bool operator !=(LabelInstructionOperand left, LabelInstructionOperand right) => !left.Equals(right);

    public override string ToString()
    {
        StringBuilder result = new();
        if (IsAbsoluteLabelAddress) result.Append('<');
        result.Append('<');
        result.Append(Label.ToString(   ));
        if (AdditionalLabelOffset > 0)
        {
            result.Append('+');
            result.Append(AdditionalLabelOffset);
        }
        else
        {
            result.Append(AdditionalLabelOffset);
        }
        result.Append('>');
        if (IsAbsoluteLabelAddress) result.Append('>');
        return result.ToString();
    }
}

public struct PreparationInstructionOperand : IEquatable<PreparationInstructionOperand>
{
    public bool IsLabelAddress;
    public InstructionOperand Value;
    public LabelInstructionOperand LabelValue;

    public PreparationInstructionOperand(InstructionOperand value) : this()
    {
        IsLabelAddress = false;
        Value = value;
    }

    public PreparationInstructionOperand(InstructionLabel label, bool isAbsolute, int additionalOffset = 0) : this()
    {
        IsLabelAddress = true;
        LabelValue = new(label, isAbsolute, additionalOffset);
    }

    public override readonly bool Equals(object? obj) => obj is PreparationInstructionOperand other && Equals(other);
    public readonly bool Equals(PreparationInstructionOperand other) =>
        IsLabelAddress == other.IsLabelAddress
        && Value.Equals(other.Value)
        && LabelValue.Equals(other.LabelValue);
    public override int GetHashCode() => IsLabelAddress ? HashCode.Combine(IsLabelAddress, LabelValue) : HashCode.Combine(IsLabelAddress, Value);

    public static bool operator ==(PreparationInstructionOperand left, PreparationInstructionOperand right) => left.Equals(right);
    public static bool operator !=(PreparationInstructionOperand left, PreparationInstructionOperand right) => !left.Equals(right);

    public static implicit operator PreparationInstructionOperand(int value) => new(new InstructionOperand(value, InstructionOperandType.Immediate32));
    public static implicit operator PreparationInstructionOperand(InstructionOperand value) => new(value);
    public static implicit operator PreparationInstructionOperand(Register register) => new(new InstructionOperand((int)register, InstructionOperandType.Register));
    public static explicit operator PreparationInstructionOperand(AddressRegisterPointer address) => new(address.Register.ToPtr(0, BitWidth._32));
    public static explicit operator PreparationInstructionOperand(AddressOffset address) => address.Base switch
    {
        AddressRegisterPointer registerPointer => new(registerPointer.Register.ToPtr(address.Offset * ProcessorState.StackDirection, BitWidth._32)),
        _ => throw new NotImplementedException()
    };
    public static explicit operator PreparationInstructionOperand(Address address) => address switch
    {
        AddressRegisterPointer registerPointer => (PreparationInstructionOperand)registerPointer,
        AddressOffset addressOffset => (PreparationInstructionOperand)addressOffset,
        _ => throw new NotImplementedException()
    };

    public override string ToString() => IsLabelAddress ? LabelValue.ToString() : Value.ToString();
}

public class PreparationInstruction
{
    public Opcode Opcode;
    public PreparationInstructionOperand Operand1;
    public PreparationInstructionOperand Operand2;

    public PreparationInstruction(
        Opcode opcode)
    {
        Opcode = opcode;
        Operand1 = default;
        Operand2 = default;
    }

    public PreparationInstruction(
        Opcode opcode,
        PreparationInstructionOperand operand1)
    {
        Opcode = opcode;
        Operand1 = operand1;
        Operand2 = default;
    }

    public PreparationInstruction(
        Opcode opcode,
        PreparationInstructionOperand operand1,
        PreparationInstructionOperand operand2)
    {
        Opcode = opcode;
        Operand1 = operand1;
        Operand2 = operand2;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        StringBuilder result = new();

        result.Append(Opcode.ToString());

        int parameterCount = Opcode.ParameterCount();

        if (parameterCount >= 1)
        {
            result.Append(' ');
            result.Append(Operand1.ToString());
        }

        if (parameterCount >= 2)
        {
            result.Append(' ');
            result.Append(Operand2.ToString());
        }

        return result.ToString();
    }
}
