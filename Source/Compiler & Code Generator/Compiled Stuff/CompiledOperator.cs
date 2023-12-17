using System;
using System.Collections.Generic;

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;

    public class CompiledOperator :
        FunctionDefinition,
        ICanBeSame,
        IAmInContext<CompiledClass>,
        IReferenceable<OperatorCall>,
        IDuplicatable<CompiledOperator>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset = -1;

        public CompiledAttributeCollection CompiledAttributes;

        public IReadOnlyList<(OperatorCall Statement, string? File)> ReferencesOperator => references;
        readonly List<(OperatorCall Statement, string? File)> references = new();

        public new CompiledType Type;
        public TypeInstance TypeToken => base.Type;

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo != null) return true;
                if (Context != null && Context.TemplateInfo != null) return true;
                return false;
            }
        }

        public bool IsExternal => CompiledAttributes.ContainsKey("External");
        public string ExternalFunctionName => CompiledAttributes.TryGetAttribute("External", out string? name) ? name : string.Empty;

        public CompiledClass? Context { get; set; }

        public CompiledOperator(CompiledType type, CompiledType[] parameterTypes, FunctionDefinition functionDefinition) : base(functionDefinition.Attributes, functionDefinition.Modifiers, functionDefinition.Type, functionDefinition.Identifier, functionDefinition.Parameters, functionDefinition.TemplateInfo)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;
            this.CompiledAttributes = new();

            base.Block = functionDefinition.Block;
            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(OperatorCall referencedBy, string? file) => references.Add((referencedBy, file));
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledOperator other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }
        public bool IsSame(ICanBeSame? other) => other is CompiledOperator other2 && IsSame(other2);

        public CompiledOperator Duplicate() => new(this.Type, new List<CompiledType>(this.ParameterTypes).ToArray(), this)
        {
            CompiledAttributes = this.CompiledAttributes,
            Modifiers = this.Modifiers,
            TimesUsed = TimesUsed,
            TimesUsedTotal = TimesUsedTotal,
        };
        public CompiledOperatorTemplateInstance InstantiateTemplate(TypeArguments typeParameters)
        {
            CompiledOperatorTemplateInstance result = new(Type, ParameterTypes, this, this)
            {
                CompiledAttributes = this.CompiledAttributes,
                Modifiers = this.Modifiers,
                TimesUsed = TimesUsed,
                TimesUsedTotal = TimesUsedTotal,
            };

            Utils.SetTypeParameters(result.ParameterTypes, typeParameters);

            if (result.Type.IsGeneric)
            {
                if (!typeParameters.TryGetValue(result.Type.Name, out CompiledType? typeParameter))
                { throw new NotImplementedException(); }
                result.Type = typeParameter;
            }

            return result;
        }
    }
}
