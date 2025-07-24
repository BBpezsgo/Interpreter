#pragma warning disable CS8605 // Unboxing a possibly null value.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

using System.Reflection;
using System.Reflection.Emit;

namespace LanguageCore.IL.Reflection;

class DynamicScopeTokenResolver : ITokenResolver
{
    static readonly PropertyInfo? _indexer;
    static readonly FieldInfo? _scopeFi;

    static readonly Type? _genMethodInfoType;
    static readonly FieldInfo? _genmethFi1, _genmethFi2;

    static readonly Type? _varArgMethodType;
    static readonly FieldInfo? _varargFi1;

    static readonly Type? _genFieldInfoType;
    static readonly FieldInfo? _genfieldFi1, _genfieldFi2;

    static DynamicScopeTokenResolver()
    {
        const BindingFlags s_bfInternal = BindingFlags.NonPublic | BindingFlags.Instance;

        _indexer = Type.GetType("System.Reflection.Emit.DynamicScope")?.GetProperty("Item", s_bfInternal);
        _scopeFi = Type.GetType("System.Reflection.Emit.DynamicILGenerator")?.GetField("m_scope", s_bfInternal);

        _varArgMethodType = Type.GetType("System.Reflection.Emit.VarArgMethod");
        _varargFi1 = _varArgMethodType?.GetField("m_method", s_bfInternal);

        _genMethodInfoType = Type.GetType("System.Reflection.Emit.GenericMethodInfo");
        _genmethFi1 = _genMethodInfoType?.GetField("m_methodHandle", s_bfInternal);
        _genmethFi2 = _genMethodInfoType?.GetField("m_context", s_bfInternal);

        _genFieldInfoType = Type.GetType("System.Reflection.Emit.GenericFieldInfo", false);
        if (_genFieldInfoType != null)
        {
            _genfieldFi1 = _genFieldInfoType.GetField("m_fieldHandle", s_bfInternal);
            _genfieldFi2 = _genFieldInfoType.GetField("m_context", s_bfInternal);
        }
        else
        {
            _genfieldFi1 = _genfieldFi2 = null;
        }
    }

    readonly object? m_scope;
    public object? this[int token] => _indexer?.GetValue(m_scope, new object[] { token });

    public DynamicScopeTokenResolver(DynamicMethod dm)
    {
        m_scope = _scopeFi?.GetValue(dm.GetILGenerator());
    }

    public string? AsString(int token) => this[token] as string;

    public FieldInfo AsField(int token)
    {
        if (this[token] is RuntimeFieldHandle runtimeFieldHandle)
        { return FieldInfo.GetFieldFromHandle(runtimeFieldHandle); }

        if (this[token].GetType() == _genFieldInfoType)
        {
            return FieldInfo.GetFieldFromHandle(
                (RuntimeFieldHandle)_genfieldFi1.GetValue(this[token]),
                (RuntimeTypeHandle)_genfieldFi2.GetValue(this[token])
            );
        }

        Debug.Assert(false, $"Unexpected type: {this[token].GetType()}");
        return null;
    }

    public Type? AsType(int token) => Type.GetTypeFromHandle((RuntimeTypeHandle)this[token]);

    public MethodBase? AsMethod(int token)
    {
        if (this[token] is DynamicMethod dynamicMethod)
        {
            return dynamicMethod;
        }

        if (this[token] is RuntimeMethodHandle handle)
        {
            return MethodBase.GetMethodFromHandle(handle);
        }

        if (this[token].GetType() == _genMethodInfoType)
        {
            return MethodBase.GetMethodFromHandle(
               (RuntimeMethodHandle)_genmethFi1.GetValue(this[token]),
               (RuntimeTypeHandle)_genmethFi2.GetValue(this[token])
            );
        }

        if (this[token].GetType() == _varArgMethodType)
        {
            return (MethodInfo)_varargFi1.GetValue(this[token]);
        }

        Debug.Assert(false, $"Unexpected type: {this[token].GetType()}");
        return null;
    }

    public MemberInfo? AsMember(int token)
    {
        if ((token & 0x02000000) == 0x02000000) return AsType(token);
        if ((token & 0x06000000) == 0x06000000) return AsMethod(token);
        if ((token & 0x04000000) == 0x04000000) return AsField(token);

        Debug.Assert(false, $"Unexpected token type: {token:x8}");
        return null;
    }

    public byte[]? AsSignature(int token) => this[token] as byte[];
}
