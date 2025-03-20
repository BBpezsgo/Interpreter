using System.Runtime.InteropServices;

namespace LanguageCore;

public static class PackedValues
{
    public static PackedValues<T0> Create<T0>(T0 v0)
        where T0 : unmanaged
        => new(v0);
    public static PackedValues<T0, T1> Create<T0, T1>(T0 v0, T1 v1)
        where T0 : unmanaged where T1 : unmanaged
        => new(v0, v1);
    public static PackedValues<T0, T1, T2> Create<T0, T1, T2>(T0 v0, T1 v1, T2 v2)
        where T0 : unmanaged where T1 : unmanaged where T2 : unmanaged
        => new(v0, v1, v2);
    public static PackedValues<T0, T1, T2, T3> Create<T0, T1, T2, T3>(T0 v0, T1 v1, T2 v2, T3 v3)
        where T0 : unmanaged where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
        => new(v0, v1, v2, v3);
    public static PackedValues<T0, T1, T2, T3, T4> Create<T0, T1, T2, T3, T4>(T0 v0, T1 v1, T2 v2, T3 v3, T4 v4)
        where T0 : unmanaged where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
        => new(v0, v1, v2, v3, v4);
    public static PackedValues<T0, T1, T2, T3, T4, T5> Create<T0, T1, T2, T3, T4, T5>(T0 v0, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5)
        where T0 : unmanaged where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        => new(v0, v1, v2, v3, v4, v5);
    public static PackedValues<T0, T1, T2, T3, T4, T5, T6> Create<T0, T1, T2, T3, T4, T5, T6>(T0 v0, T1 v1, T2 v2, T3 v3, T4 v4, T5 v5, T6 v6)
        where T0 : unmanaged where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged where T6 : unmanaged
        => new(v0, v1, v2, v3, v4, v5, v6);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0>
    where T0 : unmanaged
{
    public T0 _0;

    public PackedValues(T0 _0) => this._0 = _0;

}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1>
    where T0 : unmanaged
    where T1 : unmanaged
{
    public T0 _0;
    public T1 _1;

    public PackedValues(T0 _0, T1 _1)
    {
        this._0 = _0;
        this._1 = _1;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1, T2>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
{
    public T0 _0;
    public T1 _1;
    public T2 _2;

    public PackedValues(T0 _0, T1 _1, T2 _2)
    {
        this._0 = _0;
        this._1 = _1;
        this._2 = _2;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1, T2, T3>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
{
    public T0 _0;
    public T1 _1;
    public T2 _2;
    public T3 _3;

    public PackedValues(T0 _0, T1 _1, T2 _2, T3 _3)
    {
        this._0 = _0;
        this._1 = _1;
        this._2 = _2;
        this._3 = _3;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1, T2, T3, T4>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
{
    public T0 _0;
    public T1 _1;
    public T2 _2;
    public T3 _3;
    public T4 _4;

    public PackedValues(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4)
    {
        this._0 = _0;
        this._1 = _1;
        this._2 = _2;
        this._3 = _3;
        this._4 = _4;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1, T2, T3, T4, T5>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
{
    public T0 _0;
    public T1 _1;
    public T2 _2;
    public T3 _3;
    public T4 _4;
    public T5 _5;

    public PackedValues(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5)
    {
        this._0 = _0;
        this._1 = _1;
        this._2 = _2;
        this._3 = _3;
        this._4 = _4;
        this._5 = _5;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PackedValues<T0, T1, T2, T3, T4, T5, T6>
    where T0 : unmanaged
    where T1 : unmanaged
    where T2 : unmanaged
    where T3 : unmanaged
    where T4 : unmanaged
    where T5 : unmanaged
    where T6 : unmanaged
{
    public T0 _0;
    public T1 _1;
    public T2 _2;
    public T3 _3;
    public T4 _4;
    public T5 _5;
    public T6 _6;

    public PackedValues(T0 _0, T1 _1, T2 _2, T3 _3, T4 _4, T5 _5, T6 _6)
    {
        this._0 = _0;
        this._1 = _1;
        this._2 = _2;
        this._3 = _3;
        this._4 = _4;
        this._5 = _5;
        this._6 = _6;
    }
}
