using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit)]
public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    [FieldOffset(0)] readonly int _integer;
    [FieldOffset(0)] readonly float _single;

    [FieldOffset(0)] public readonly byte Byte0;
    [FieldOffset(1)] public readonly byte Byte1;
    [FieldOffset(2)] public readonly byte Byte2;
    [FieldOffset(3)] public readonly byte Byte3;

    public int Int => _integer;
    public char Char => (char)_integer;
    public byte Byte => (byte)_integer;
    public float Single => _single;

    public RuntimeValue(int value) : this() { _integer = value; }
    public RuntimeValue(byte value) : this() { _integer = value; }
    public RuntimeValue(float value) : this() { _single = value; }
    public RuntimeValue(char value) : this() { _integer = value; }
    public RuntimeValue(ushort value) : this() { _integer = value; }
    public RuntimeValue(bool value) : this(value ? 1 : 0) { }
    public RuntimeValue(byte _0, byte _1, byte _2, byte _3)
    {
        Byte0 = _0;
        Byte1 = _1;
        Byte2 = _2;
        Byte3 = _3;
    }

    public override string ToString() => _integer.ToString();
    public override int GetHashCode() => _integer;
    public override bool Equals(object? obj) => obj is RuntimeValue value && Equals(value);
    public bool Equals(RuntimeValue other) => _integer == other._integer;

    public static bool operator ==(RuntimeValue a, RuntimeValue b) => a._integer == b._integer;
    public static bool operator !=(RuntimeValue a, RuntimeValue b) => a._integer != b._integer;

    public static implicit operator RuntimeValue(int v) => new(v);

    public RuntimeValue Reverse() => new(Byte3, Byte2, Byte1, Byte0);
}
