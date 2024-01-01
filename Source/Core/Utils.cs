﻿using System;

namespace LanguageCore
{
    using LanguageCore.Compiler;

    public interface IReadable
    {
        public string ToReadable(Func<Parser.Statement.StatementWithValue, CompiledType> typeSearch);
    }

    public interface ISimpleReadable : IReadable
    {
        public string ToReadable();
        string IReadable.ToReadable(Func<Parser.Statement.StatementWithValue, CompiledType> typeSearch) => ToReadable();
    }

    public static partial class Utils
    {
        /// <exception cref="NotImplementedException"/>
        public static CompiledLiteralType ConvertType(System.Type type)
        {
            if (type == typeof(int))
            { return CompiledLiteralType.Integer; }

            if (type == typeof(float))
            { return CompiledLiteralType.Float; }

            if (type == typeof(bool))
            { return CompiledLiteralType.Boolean; }

            if (type == typeof(string))
            { return CompiledLiteralType.String; }

            throw new NotImplementedException($"Unknown attribute type requested: \"{type.FullName}\"");
        }

        /// <exception cref="NotImplementedException"/>
        public static bool TryConvertType(System.Type type, out CompiledLiteralType result)
        {
            if (type == typeof(int))
            {
                result = CompiledLiteralType.Integer;
                return true;
            }

            if (type == typeof(float))
            {
                result = CompiledLiteralType.Float;
                return true;
            }

            if (type == typeof(bool))
            {
                result = CompiledLiteralType.Boolean;
                return true;
            }

            if (type == typeof(string))
            {
                result = CompiledLiteralType.String;
                return true;
            }

            result = default;
            return false;
        }

        public static void SetTypeParameters(CompiledType[] typeParameters, TypeArguments typeValues)
        {
            for (int i = 0; i < typeParameters.Length; i++)
            {
                if (typeParameters[i].IsGeneric)
                {
                    if (!typeValues.TryGetValue(typeParameters[i].Name, out CompiledType? eTypeParameter))
                    { throw new NotImplementedException(); }
                    typeParameters[i] = eTypeParameter;
                }
            }
        }
    }
}
