using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledEnumMember : EnumMemberDefinition, IHaveKey<string>
    {
        public DataItem ComputedValue;

        public CompiledEnumMember(EnumMemberDefinition definition)
            : base(definition.Identifier, definition.Value)
        { }
    }

    public class CompiledEnum : EnumDefinition, ITypeDefinition, IHaveKey<string>
    {
        public new CompiledEnumMember[] Members;
        public Dictionary<string, AttributeValues> CompiledAttributes;

        public CompiledEnum(EnumDefinition definition) : base(definition.Identifier, definition.Attributes, definition.Members)
        {
            Members = Array.Empty<CompiledEnumMember>();
            CompiledAttributes = new Dictionary<string, AttributeValues>();
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
    }
}
