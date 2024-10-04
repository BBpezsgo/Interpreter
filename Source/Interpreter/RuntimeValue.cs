using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit)]
public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    [FieldOffset(0)] public readonly float F32;

    [FieldOffset(0)] public readonly byte U8;
    [FieldOffset(0)] public readonly char U16;
    [FieldOffset(0)] public readonly uint U32;

    [FieldOffset(0)] public readonly sbyte I8;
    [FieldOffset(0)] public readonly short I16;
    [FieldOffset(0)] public readonly int I32;

    public RuntimeValue(float f32) : this() { F32 = f32; }

    public RuntimeValue(byte u8) : this() { U8 = u8; }
    public RuntimeValue(char u16) : this() { U16 = u16; }
    public RuntimeValue(ushort u16) : this() { U16 = (char)u16; }
    public RuntimeValue(uint u32) : this() { U32 = u32; }

    public RuntimeValue(sbyte i8) : this() { I8 = i8; }
    public RuntimeValue(short i16) : this() { I16 = i16; }
    public RuntimeValue(int i32) : this() { I32 = i32; }
    public RuntimeValue(bool i32) : this(i32 ? 1 : 0) { }

    public override string ToString() => I32.ToString();
    public override int GetHashCode() => I32;
    public override bool Equals(object? obj) => obj is RuntimeValue other && Equals(other);
    public bool Equals(RuntimeValue other) => I32 == other.I32;

    public static bool operator ==(RuntimeValue a, RuntimeValue b) => a.I32 == b.I32;
    public static bool operator !=(RuntimeValue a, RuntimeValue b) => a.I32 != b.I32;

    public static implicit operator RuntimeValue(int v) => new(v);
    public static implicit operator RuntimeValue(float v) => new(v);
}
