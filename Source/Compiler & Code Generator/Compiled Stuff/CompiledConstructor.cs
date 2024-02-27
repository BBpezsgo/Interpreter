using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace LanguageCore.Compiler;

using Parser;
using Parser.Statement;

public class CompiledConstructor :
    ConstructorDefinition,
    ISameCheck,
    ISameCheck<CompiledConstructor>,
    IReferenceable<KeywordCall>,
    IReferenceable<ConstructorCall>,
    IDuplicatable<CompiledConstructor>
{
    public readonly CompiledType Type;
    public readonly ImmutableArray<CompiledType> ParameterTypes;

    public readonly CompiledStruct? Context;

    public int TimesUsed;
    public int TimesUsedTotal;

    public int InstructionOffset = -1;

    readonly List<Reference<Statement>> references;
    public IReadOnlyList<Reference<Statement>> References => references;

    public override bool IsTemplate
    {
        get
        {
            if (TemplateInfo is not null) return true;
            if (Context != null && Context.TemplateInfo != null) return true;
            return false;
        }
    }

    public CompiledConstructor(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledStruct? context, ConstructorDefinition functionDefinition) : base(functionDefinition)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = context;
        this.references = new List<Reference<Statement>>();
    }

    public CompiledConstructor(CompiledType type, IEnumerable<CompiledType> parameterTypes, CompiledConstructor other) : base(other)
    {
        this.Type = type;
        this.ParameterTypes = parameterTypes.ToImmutableArray();

        this.Context = other.Context;
        this.references = new List<Reference<Statement>>(other.references);
        this.TimesUsed = other.TimesUsed;
        this.TimesUsedTotal = other.TimesUsedTotal;
    }

    public void AddReference(KeywordCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void AddReference(ConstructorCall referencedBy, Uri? file) => references.Add(new Reference<Statement>(referencedBy, file));
    public void ClearReferences() => references.Clear();

    public bool IsSame(CompiledConstructor other)
    {
        if (this.Type != other.Type) return false;
        if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
        for (int i = 0; i < this.ParameterTypes.Length; i++)
        { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

        return true;
    }
    public bool IsSame(ISameCheck? other) => other is CompiledConstructor other2 && IsSame(other2);

    public CompiledConstructor Duplicate() => new(Type, ParameterTypes, Context, this)
    {
        TimesUsed = TimesUsed,
        TimesUsedTotal = TimesUsedTotal,
    };

    public override string ToString()
    {
        StringBuilder result = new();

        if (IsExport)
        { result.Append("export "); }

        result.Append(Type);

        result.Append('(');
        if (this.ParameterTypes.Length > 0)
        {
            for (int i = 0; i < ParameterTypes.Length; i++)
            {
                if (i > 0) result.Append(", ");
                result.Append(ParameterTypes[i].ToString());
            }
        }
        result.Append(')');

        result.Append(Block?.ToString() ?? ";");

        return result.ToString();
    }
}
