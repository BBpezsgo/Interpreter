namespace LanguageCore.Compiler;

using Parser;
using Runtime;

public class CompiledEnumMember : EnumMemberDefinition,
    IHaveCompiledType,
    IInContext<CompiledEnum>
{
    public DataItem ComputedValue { get; set; }
    public GeneralType Type => new BuiltinType(ComputedValue.Type);
    [NotNull] public new CompiledEnum? Context { get; set; }

    public CompiledEnumMember(EnumMemberDefinition definition) : base(definition) { }
}

public class CompiledEnum : EnumDefinition, IProbablyHaveCompiledType
{
    public new ImmutableArray<CompiledEnumMember> Members { get; }
    public GeneralType? Type
    {
        get
        {
            GeneralType? result = null;
            for (int i = 0; i < Members.Length; i++)
            {
                CompiledEnumMember member = Members[i];
                if (result is null)
                { result = member.Type; }
                else if (result != member.Type)
                { return null; }
            }
            return result;
        }
    }

    public CompiledEnum(IEnumerable<CompiledEnumMember> members, EnumDefinition definition) : base(definition)
    {
        foreach (CompiledEnumMember member in members) member.Context = this;
        Members = members.ToImmutableArray();
    }

    public bool GetValue(string identifier, out DataItem memberValue)
    {
        if (GetMember(identifier, out CompiledEnumMember? member))
        {
            memberValue = member.ComputedValue;
            return true;
        }
        else
        {
            memberValue = default;
            return false;
        }
    }

    public bool GetMember(string identifier, [NotNullWhen(true)] out CompiledEnumMember? member)
    {
        for (int i = 0; i < Members.Length; i++)
        {
            if (Members[i].Identifier.Content == identifier)
            {
                member = Members[i];
                return true;
            }
        }
        member = null;
        return false;
    }

    public override string ToString() => $"enum {Identifier} : {Type}";
}
