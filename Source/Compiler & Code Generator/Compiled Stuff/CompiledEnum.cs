using System;
using System.Diagnostics.CodeAnalysis;

namespace LanguageCore.Compiler
{
    using Parser;
    using Runtime;

    public class CompiledEnumMember : EnumMemberDefinition
    {
        public DataItem ComputedValue;
        public CompiledType Type => new(ComputedValue.Type);

        public CompiledEnumMember(EnumMemberDefinition definition) : base(definition)
        { }
    }

    public class CompiledEnum : EnumDefinition
    {
        public new CompiledEnumMember[] Members;
        public CompiledAttributeCollection CompiledAttributes;

        public CompiledType? Type
        {
            get
            {
                CompiledType? result = null;
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

        public CompiledEnum(EnumDefinition definition) : base(definition)
        {
            Members = Array.Empty<CompiledEnumMember>();
            CompiledAttributes = new CompiledAttributeCollection();
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
}
