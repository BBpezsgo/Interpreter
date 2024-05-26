namespace LanguageCore.Runtime;

[Flags]
public enum Flags
{
    _ = 0b_0000000000000000,
    Carry = 0b_0000000000000001,
    // Parity = 0b_0000000000000100,
    // AuxiliaryCarry = 0b_0000000000010000,
    Zero = 0b_0000000001000000,
    Sign = 0b_0000000010000000,

    // Trap = 0b_0000000100000000,
    // InterruptEnable = 0b_0000001000000000,
    // Direction = 0b_0000010000000000,
    Overflow = 0b_0000100000000000,
}
