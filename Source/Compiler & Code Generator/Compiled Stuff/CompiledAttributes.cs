global using CompiledAttributeCollection = System.Collections.Generic.Dictionary<string, LanguageCore.BBCode.Compiler.AttributeValues>;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LanguageCore.BBCode.Compiler
{
    using Tokenizing;

    public struct AttributeValues
    {
        public List<CompiledLiteral> parameters;
        public Token Identifier;

        public readonly bool TryGetValue<T>(int index, [NotNullWhen(true)] out T? value)
        {
            value = default;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            CompiledLiteralType type = Utils.ConvertType(typeof(T));
            value = type switch
            {
                CompiledLiteralType.Integer => (T)(object)parameters[index].ValueInt,
                CompiledLiteralType.Float => (T)(object)parameters[index].ValueFloat,
                CompiledLiteralType.String => (T)(object)parameters[index].ValueString,
                CompiledLiteralType.Boolean => (T)(object)parameters[index].ValueBool,
                _ => throw new ImpossibleException(),
            };
            return true;
        }

        public readonly bool TryGetValue(int index, out string value)
        {
            value = string.Empty;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == CompiledLiteralType.String)
            {
                value = parameters[index].ValueString;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out int value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == CompiledLiteralType.Integer)
            {
                value = parameters[index].ValueInt;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out float value)
        {
            value = 0;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == CompiledLiteralType.Float)
            {
                value = parameters[index].ValueFloat;
            }
            return true;
        }
        public readonly bool TryGetValue(int index, out bool value)
        {
            value = false;
            if (parameters == null) return false;
            if (parameters.Count <= index) return false;
            if (parameters[index].type == CompiledLiteralType.Boolean)
            {
                value = parameters[index].ValueBool;
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

    public readonly struct CompiledLiteral
    {
        public readonly int ValueInt;
        public readonly float ValueFloat;
        public readonly string ValueString;
        public readonly bool ValueBool;
        public readonly CompiledLiteralType type;

        public CompiledLiteral(int value)
        {
            this.type = CompiledLiteralType.Integer;

            this.ValueInt = value;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public CompiledLiteral(float value)
        {
            this.type = CompiledLiteralType.Float;

            this.ValueInt = 0;
            this.ValueFloat = value;
            this.ValueString = string.Empty;
            this.ValueBool = false;
        }
        public CompiledLiteral(string value)
        {
            this.type = CompiledLiteralType.String;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = value;
            this.ValueBool = false;
        }
        public CompiledLiteral(bool value)
        {
            this.type = CompiledLiteralType.Boolean;

            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = value;
        }
        public CompiledLiteral(object value)
        {
            this.ValueInt = 0;
            this.ValueFloat = 0;
            this.ValueString = string.Empty;
            this.ValueBool = false;

            if (value is int @int)
            {
                this.type = CompiledLiteralType.Integer;
                this.ValueInt = @int;
            }
            else if (value is float @float)
            {
                this.type = CompiledLiteralType.Float;
                this.ValueFloat = @float;
            }
            else if (value is string @string)
            {
                this.type = CompiledLiteralType.String;
                this.ValueString = @string;
            }
            else if (value is bool @bool)
            {
                this.type = CompiledLiteralType.Boolean;
                this.ValueBool = @bool;
            }
            else
            {
                throw new InternalException($"Invalid type '{value.GetType().FullName}'");
            }
        }

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
                _ => throw new ImpossibleException(),
            };
            return true;
        }
    }

    public static class CompiledAttributes
    {
        public static bool TryGetAttribute<T0, T1, T2>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0,
            [NotNullWhen(true)] out T1? value1,
            [NotNullWhen(true)] out T2? value2
            )
        {
            value0 = default;
            value1 = default;
            value2 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;
            if (!values.TryGetValue<T1>(1, out value1)) return false;
            if (!values.TryGetValue<T2>(2, out value2)) return false;

            return true;
        }

        public static bool TryGetAttribute<T0, T1>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0,
            [NotNullWhen(true)] out T1? value1
            )
        {
            value0 = default;
            value1 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;
            if (!values.TryGetValue<T1>(1, out value1)) return false;

            return true;
        }

        public static bool TryGetAttribute<T0>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            [NotNullWhen(true)] out T0? value0
            )
        {
            value0 = default;

            if (!attributes.TryGetValue(attributeName, out var values)) return false;

            if (!values.TryGetValue<T0>(0, out value0)) return false;

            return true;
        }

        public static bool TryGetAttribute(
            this CompiledAttributeCollection attributes,
            string attributeName)
            => attributes.TryGetValue(attributeName, out _);

        public static bool HasAttribute(
            this CompiledAttributeCollection attributes,
            string attributeName)
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 0) return false;

            return true;
        }

        public static bool HasAttribute<T0>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            T0 value0)
            where T0 : IEquatable<T0>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 1) return false;

            if (!values.parameters[0].TryConvert(out T0? v0) ||
                !value0.Equals(v0))
            { return false; }

            return true;
        }

        public static bool HasAttribute<T0, T1>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            T0 value0,
            T1 value1)
            where T0 : IEquatable<T0>
            where T1 : IEquatable<T1>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 2) return false;

            if (!values.parameters[0].TryConvert(out T0? v0) ||
                !value0.Equals(v0))
            { return false; }

            if (!values.parameters[1].TryConvert(out T1? v1) ||
                !value1.Equals(v1))
            { return false; }

            return true;
        }

        public static bool HasAttribute<T0, T1, T2>(
            this CompiledAttributeCollection attributes,
            string attributeName,
            T0 value0,
            T1 value1,
            T2 value2)
            where T0 : IEquatable<T0>
            where T1 : IEquatable<T1>
            where T2 : IEquatable<T2>
        {
            if (!attributes.TryGetValue(attributeName, out AttributeValues values)) return false;
            if (values.parameters.Count != 3) return false;

            if (!values.parameters[0].TryConvert(out T0? v0) ||
                !value0.Equals(v0))
            { return false; }

            if (!values.parameters[1].TryConvert(out T1? v1) ||
                !value1.Equals(v1))
            { return false; }

            if (!values.parameters[2].TryConvert(out T2? v2) ||
                !value2.Equals(v2))
            { return false; }

            return true;
        }
    }
}
