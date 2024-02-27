using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LanguageCore.Compiler;

public class CompiledAttribute : Parser.AttributeUsage
{
    public new readonly ImmutableArray<CompiledLiteral> Parameters;

    public CompiledAttribute(IEnumerable<CompiledLiteral> parameters, Parser.AttributeUsage definition) : base(definition.Identifier, definition.Parameters)
    {
        Parameters = parameters.ToImmutableArray();
    }

    public bool TryGetValue<T>(int index, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (Parameters == null) return false;
        if (Parameters.Length <= index) return false;
        CompiledLiteralType type = Utils.ConvertType(typeof(T));
        value = type switch
        {
            CompiledLiteralType.Integer => (T)(object)Parameters[index].ValueInt,
            CompiledLiteralType.Float => (T)(object)Parameters[index].ValueFloat,
            CompiledLiteralType.String => (T)(object)Parameters[index].ValueString,
            CompiledLiteralType.Boolean => (T)(object)Parameters[index].ValueBool,
            _ => throw new UnreachableException(),
        };
        return true;
    }

    public bool TryGetValue(int index, out string value)
    {
        value = string.Empty;
        if (Parameters == null) return false;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].type == CompiledLiteralType.String)
        {
            value = Parameters[index].ValueString;
        }
        return true;
    }

    public bool TryGetValue(int index, out int value)
    {
        value = 0;
        if (Parameters == null) return false;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].type == CompiledLiteralType.Integer)
        {
            value = Parameters[index].ValueInt;
        }
        return true;
    }

    public bool TryGetValue(int index, out float value)
    {
        value = 0;
        if (Parameters == null) return false;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].type == CompiledLiteralType.Float)
        {
            value = Parameters[index].ValueFloat;
        }
        return true;
    }

    public bool TryGetValue(int index, out bool value)
    {
        value = false;
        if (Parameters == null) return false;
        if (Parameters.Length <= index) return false;
        if (Parameters[index].type == CompiledLiteralType.Boolean)
        {
            value = Parameters[index].ValueBool;
        }
        return true;
    }
}

public enum CompiledLiteralType
{
    Integer,
    Float,
    String,
    Boolean,
}

public readonly struct CompiledLiteral : IPositioned
{
    public readonly int ValueInt;
    public readonly float ValueFloat;
    public readonly string ValueString;
    public readonly bool ValueBool;
    public readonly CompiledLiteralType type;

    readonly Parser.Statement.Literal _literal;

    public CompiledLiteral(Parser.Statement.Literal value)
    {
        this.ValueInt = 0;
        this.ValueFloat = 0;
        this.ValueString = string.Empty;
        this.ValueBool = false;
        this._literal = value;

        switch (value.Type)
        {
            case Parser.LiteralType.Integer:
                type = CompiledLiteralType.Integer;
                ValueInt = value.GetInt();
                break;
            case Parser.LiteralType.Float:
                type = CompiledLiteralType.Float;
                ValueFloat = value.GetFloat();
                break;
            case Parser.LiteralType.String:
                type = CompiledLiteralType.String;
                ValueString = value.Value;
                break;
            default:
                throw new InternalException($"Invalid type \"{value.Type}\"");
        }
    }

    public Position Position => _literal.Position;

    public readonly bool TryConvert<T>([NotNullWhen(true)] out T? value)
    {
        if (!Utils.TryConvertType(typeof(T), out CompiledLiteralType type))
        {
            value = default;
            return false;
        }

        if (type != this.type)
        {
            value = default;
            return false;
        }

        value = type switch
        {
            CompiledLiteralType.Integer => (T)(object)ValueInt,
            CompiledLiteralType.Float => (T)(object)ValueFloat,
            CompiledLiteralType.String => (T)(object)ValueString,
            CompiledLiteralType.Boolean => (T)(object)ValueBool,
            _ => throw new UnreachableException(),
        };
        return true;
    }
}

public static class CompiledAttributes
{
    public static bool TryGetAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1
        )
    {
        value0 = default;
        value1 = default;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0
        )
    {
        value0 = default;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out T2? value2,
        [NotNullWhen(true)] out CompiledAttribute? attribute
        )
    {
        value0 = default;
        value1 = default;
        value2 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;
        if (!values.TryGetValue<T2>(2, out value2)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0, T1>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out T1? value1,
        [NotNullWhen(true)] out CompiledAttribute? attribute
        )
    {
        value0 = default;
        value1 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;
        if (!values.TryGetValue<T1>(1, out value1)) return false;

        return true;
    }

    public static bool TryGetAttribute<T0>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        [NotNullWhen(true)] out T0? value0,
        [NotNullWhen(true)] out CompiledAttribute? attribute
        )
    {
        value0 = default;
        attribute = null;

        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        attribute = values;

        if (!values.TryGetValue<T0>(0, out value0)) return false;

        return true;
    }

    public static bool TryGetAttribute(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName)
        => attributes.TryGetValue(attributeName, out _);

    public static bool HasAttribute(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName)
    {
        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        if (values.Parameters.Length != 0) return false;

        return true;
    }

    public static bool HasAttribute<T0>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        T0 value0)
        where T0 : IEquatable<T0>
    {
        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        if (values.Parameters.Length != 1) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        T0 value0,
        T1 value1)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
    {
        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        if (values.Parameters.Length != 2) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        return true;
    }

    public static bool HasAttribute<T0, T1, T2>(
        this IReadOnlyDictionary<string, CompiledAttribute> attributes,
        string attributeName,
        T0 value0,
        T1 value1,
        T2 value2)
        where T0 : IEquatable<T0>
        where T1 : IEquatable<T1>
        where T2 : IEquatable<T2>
    {
        if (!attributes.TryGetValue(attributeName, out CompiledAttribute? values)) return false;
        if (values.Parameters.Length != 3) return false;

        if (!values.Parameters[0].TryConvert(out T0? v0) ||
            !value0.Equals(v0))
        { return false; }

        if (!values.Parameters[1].TryConvert(out T1? v1) ||
            !value1.Equals(v1))
        { return false; }

        if (!values.Parameters[2].TryConvert(out T2? v2) ||
            !value2.Equals(v2))
        { return false; }

        return true;
    }
}
