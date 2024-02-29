using System.IO;

namespace LanguageCore;

using Compiler;
using LanguageCore.Parser;
using Parser.Statement;

public interface IReadable
{
    public string ToReadable(Func<StatementWithValue, GeneralType> typeSearch);
}

public interface ISimpleReadable : IReadable
{
    public string ToReadable();
    string IReadable.ToReadable(Func<StatementWithValue, GeneralType> typeSearch) => ToReadable();
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
    public static bool TryConvertType(Type type, out LiteralType result)
    {
        if (type == typeof(int))
        {
            result = LiteralType.Integer;
            return true;
        }

        if (type == typeof(float))
        {
            result = LiteralType.Float;
            return true;
        }

        if (type == typeof(string))
        {
            result = LiteralType.String;
            return true;
        }

        result = default;
        return false;
    }

    /// <exception cref="NotImplementedException"/>
    public static void SetTypeParameters(GeneralType[] typeParameters, Dictionary<string, GeneralType> typeValues)
    {
        for (int i = 0; i < typeParameters.Length; i++)
        { Utils.SetTypeParameters(ref typeParameters[i], typeValues); }
    }

    /// <exception cref="NotImplementedException"/>
    public static void SetTypeParameters(ref GeneralType typeParameter, Dictionary<string, GeneralType> typeValues)
    {
        if (typeParameter is not GenericType genericType)
        { return; }

        if (!typeValues.TryGetValue(genericType.Identifier, out GeneralType? eTypeParameter))
        { throw new NotImplementedException(); }

        typeParameter = eTypeParameter;
    }
}
