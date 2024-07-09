using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit)]
public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    [FieldOffset(0)] public readonly int I32;
    [FieldOffset(0)] public readonly float F32;

    [FieldOffset(0)] public readonly char U16;

    [FieldOffset(0)] public readonly byte U8;

    public RuntimeValue(int value) : this() { I32 = value; }
    public RuntimeValue(byte value) : this() { I32 = value; }
    public RuntimeValue(float value) : this() { F32 = value; }
    public RuntimeValue(char value) : this() { I32 = value; }
    public RuntimeValue(ushort value) : this() { I32 = value; }
    public RuntimeValue(bool value) : this(value ? 1 : 0) { }

    public override string ToString() => I32.ToString();
    public override int GetHashCode() => I32;
    public override bool Equals(object? obj) => obj is RuntimeValue other && Equals(other);
    public bool Equals(RuntimeValue other) => I32 == other.I32;

    public static bool operator ==(RuntimeValue a, RuntimeValue b) => a.I32 == b.I32;
    public static bool operator !=(RuntimeValue a, RuntimeValue b) => a.I32 != b.I32;

    public static implicit operator RuntimeValue(int v) => new(v);
    public static implicit operator RuntimeValue(float v) => new(v);
}
