namespace LanguageCore.Compiler;

using Parser;
using Runtime;

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

    public bool IsSameType(GeneralType type)
    {
        if (type is not BuiltinType builtinType) return false;
        RuntimeType runtimeType;
        try
        { runtimeType = builtinType.RuntimeType; }
        catch (NotImplementedException)
        { return false; }

        for (int i = 0; i < Members.Length; i++)
        {
            if (Members[i].ComputedValue.Type != runtimeType)
            { return false; }
        }

        return true;
    }

    public override string ToString() => $"enum {Identifier} : {Type}";
}
