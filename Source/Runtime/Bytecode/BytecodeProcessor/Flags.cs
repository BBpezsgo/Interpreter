namespace LanguageCore.Runtime;

[Flags]
public enum Flags
{
    _ = 0b_0000_0000_0000_0000,
    Carry = 0b_0000_0000_0000_0001,
    // Parity = 0b_0000_0000_0000_0100,
    // AuxiliaryCarry = 0b_0000_0000_0001_0000,
    Zero = 0b_0000_0000_0100_0000,
    Sign = 0b_0000_0000_1000_0000,
    // Trap = 0b_0000_0001_0000_0000,
    // InterruptEnable = 0b_0000_0010_0000_0000,
    // Direction = 0b_0000_0100_0000_0000,
    Overflow = 0b_0000_1000_0000_0000,
}
