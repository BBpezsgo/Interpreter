using System.Reflection;
using System.Reflection.Emit;

namespace LanguageCore.IL.Reflection;

public static class DynamicMethodILProvider
{
    public static byte[]? GetByteArray(DynamicMethod method)
    {
        object? resolver = typeof(DynamicMethod).GetField("_resolver", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(method);
        if (resolver is null)
        {
            return GetByteArray(method.GetILGenerator());
        }
        return resolver.GetType().GetField("m_code", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(resolver) as byte[] ?? Array.Empty<byte>();
    }

    public static byte[]? GetByteArray(ILGenerator ilgen)
    {
        Type? type = ilgen.GetType();
        FieldInfo? fiBytes;
        FieldInfo? fiLength;

        while (true)
        {
            fiBytes = type.GetField("m_ILStream", BindingFlags.Instance | BindingFlags.NonPublic);
            fiLength = type.GetField("m_length", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fiBytes is not null && fiLength is not null) break;
            type = type.BaseType;
            if (type is null) return null;
        }

        byte[]? bytes = (byte[]?)fiBytes.GetValue(ilgen);
        int? count = (int?)fiLength.GetValue(ilgen);

        if (bytes is null || count is null) return null;

        return new ArraySegment<byte>(bytes, 0, count.Value).ToArray();
    }
}
