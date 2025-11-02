using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public delegate bool AttributeVerifier(IHaveAttributes context, AttributeUsage attribute, [NotNullWhen(false)] out PossibleDiagnostic? error);

public class UserDefinedAttribute
{
    public string Name { get; }
    public ImmutableArray<LiteralType> Parameters { get; }
    public CanUseOn CanUseOn { get; }
    public AttributeVerifier? Verifier { get; }

    public UserDefinedAttribute(string name, ImmutableArray<LiteralType> parameters, CanUseOn canUseOn, AttributeVerifier? verifier)
    {
        Name = name;
        Parameters = parameters;
        CanUseOn = canUseOn;
        Verifier = verifier;
    }
}
