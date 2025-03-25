using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

public class ExternalConstant
{
    public string Name;
    public CompiledValue Value;

    public ExternalConstant(string name, CompiledValue value)
    {
        Name = name;
        Value = value;
    }
}
