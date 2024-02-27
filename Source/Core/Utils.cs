using System;
using System.IO;

namespace LanguageCore;

using Compiler;
using Parser.Statement;

public interface IReadable
{
    public string ToReadable(Func<StatementWithValue, CompiledType> typeSearch);
}

public interface ISimpleReadable : IReadable
{
    public string ToReadable();
    string IReadable.ToReadable(Func<StatementWithValue, CompiledType> typeSearch) => ToReadable();
}

[Flags]
public enum ToReadableFlags
{
    None = 0b_0000,
    ParameterIdentifiers = 0b_0001,
    Modifiers = 0b_0010,
}

public static partial class Utils
{
    internal static void GenerateTestFiles(string directoryPath, int n)
    {
        for (int i = 1; i <= n; i++)
        {
            string name = $"{n.ToString().PadLeft(2, '0')}.bbc";
            string path = Path.Combine(directoryPath, name);
            if (File.Exists(path)) continue;
            File.WriteAllText(path, string.Empty);
        }
    }

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

    /// <exception cref="NotImplementedException"/>
    public static void SetTypeParameters(CompiledType[] typeParameters, TypeArguments typeValues)
    {
        for (int i = 0; i < typeParameters.Length; i++)
        { Utils.SetTypeParameters(ref typeParameters[i], typeValues); }
    }

    /// <exception cref="NotImplementedException"/>
    public static void SetTypeParameters(ref CompiledType typeParameter, TypeArguments typeValues)
    {
        if (!typeParameter.IsGeneric)
        { return; }

        if (!typeValues.TryGetValue(typeParameter.Name, out CompiledType? eTypeParameter))
        { throw new NotImplementedException(); }

        typeParameter = eTypeParameter;
    }
}
