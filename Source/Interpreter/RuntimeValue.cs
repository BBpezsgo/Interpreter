using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[StructLayout(LayoutKind.Explicit)]
public readonly struct RuntimeValue : IEquatable<RuntimeValue>
{
    public static RuntimeValue Null => default;

    [FieldOffset(0)] readonly int _integer;
    [FieldOffset(0)] readonly float _single;

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

    public override string ToString() => _integer.ToString();
    public override int GetHashCode() => _integer;
    public override bool Equals(object? obj) => obj is RuntimeValue value && Equals(value);
    public bool Equals(RuntimeValue other) => _integer == other._integer;

    public static bool operator ==(RuntimeValue a, RuntimeValue b) => a._integer == b._integer;
    public static bool operator !=(RuntimeValue a, RuntimeValue b) => a._integer != b._integer;

    public static implicit operator RuntimeValue(int v) => new(v);
}
