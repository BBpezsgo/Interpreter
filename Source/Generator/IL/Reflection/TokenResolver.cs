using System.Reflection;

namespace LanguageCore.IL.Reflection;

public interface ITokenResolver
{
    MethodBase? AsMethod(int token);
    FieldInfo? AsField(int token);
    Type? AsType(int token);
    string? AsString(int token);
    MemberInfo? AsMember(int token);
    byte[]? AsSignature(int token);
}
