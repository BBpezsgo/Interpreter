using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public class CompiledRegisterAccess : CompiledAccessExpression
{
    public required Register Register { get; init; }

    public override string Stringify(int depth = 0) => $"{Register}";
    public override string ToString() => $"{Register}";
}
