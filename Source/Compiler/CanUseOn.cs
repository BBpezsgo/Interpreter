using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

[Flags]
public enum CanUseOn
{
    Function,
    Struct,
    Field,
    TypeAlias,
}
