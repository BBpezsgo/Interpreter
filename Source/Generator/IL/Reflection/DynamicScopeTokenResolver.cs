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
        _genfieldFi1 = _genFieldInfoType?.GetField("m_fieldHandle", s_bfInternal);
        _genfieldFi2 = _genFieldInfoType?.GetField("m_context", s_bfInternal);
    }

    readonly object? m_scope;

    public object? this[int token] => _indexer?.GetValue(m_scope, new object[] { token });

    public DynamicScopeTokenResolver(DynamicMethod dm)
    {
        m_scope = _scopeFi?.GetValue(dm.GetILGenerator());
    }

    public string? AsString(int token)
    {
        return this[token] as string;
    }

    public FieldInfo AsField(int token)
    {
        object? t = this[token];

        if (t is RuntimeFieldHandle runtimeFieldHandle)
        { return FieldInfo.GetFieldFromHandle(runtimeFieldHandle); }

        if (t is null)
        {
            Debug.Assert(false);
            return null;
        }

        if (t.GetType().Equals(_genFieldInfoType))
        {
            if (_genfieldFi1?.GetValue(t) is not RuntimeFieldHandle v1 || _genfieldFi2?.GetValue(t) is not RuntimeTypeHandle v2)
            {
                Debug.Assert(false);
                return null;
            }

            return FieldInfo.GetFieldFromHandle(v1, v2);
        }

        Debug.Assert(false, $"Unexpected type: {t.GetType()}");
        return null;
    }

    public Type? AsType(int token)
    {
        if (this[token] is not RuntimeTypeHandle v)
        {
            Debug.Assert(false);
            return null;
        }

        return Type.GetTypeFromHandle(v);
    }

    public MethodBase? AsMethod(int token)
    {
        object? t = this[token];

        if (t is null)
        {
            Debug.Assert(false);
            return null;
        }

        if (t is DynamicMethod dynamicMethod)
        {
            return dynamicMethod;
        }

        if (t is RuntimeMethodHandle handle)
        {
            return MethodBase.GetMethodFromHandle(handle);
        }

        if (t.GetType() == _genMethodInfoType)
        {
            if (_genmethFi1?.GetValue(t) is not RuntimeMethodHandle v1 || _genmethFi2?.GetValue(t) is not RuntimeTypeHandle v2)
            {
                Debug.Assert(false);
                return null;
            }

            return MethodBase.GetMethodFromHandle(v1, v2);
        }

        if (t.GetType() == _varArgMethodType)
        {
            return (MethodInfo?)_varargFi1?.GetValue(t);
        }

        Debug.Assert(false, $"Unexpected type: {t.GetType()}");
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

    public byte[]? AsSignature(int token)
    {
        return this[token] as byte[];
    }
}
