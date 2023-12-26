using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;

    public class CompiledGeneralFunction :
        GeneralFunctionDefinition,
        ISameCheck,
        ISameCheck<CompiledGeneralFunction>,
        IAmInContext<CompiledClass>,
        IReferenceable<KeywordCall>,
        IReferenceable<ConstructorCall>,
        IDuplicatable<CompiledGeneralFunction>
    {
        public CompiledType[] ParameterTypes;

        public int TimesUsed;
        public int TimesUsedTotal;

        public int InstructionOffset = -1;

        public bool ReturnSomething => this.Type.BuiltinType != LanguageCore.Compiler.Type.Void;

        public IReadOnlyList<Reference<Statement>> References => references;
        readonly List<Reference<Statement>> references = new();

        public override bool IsTemplate
        {
            get
            {
                if (TemplateInfo is not null) return true;
                if (context != null && context.TemplateInfo != null) return true;
                return false;
            }
        }

        public CompiledType Type;

        CompiledClass? context;
        public CompiledClass? Context
        {
            get => context;
            set => context = value;
        }

        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers, functionDefinition.Parameters)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;

            base.Block = functionDefinition.Block;

            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(KeywordCall referencedBy, string? file) => references.Add(new Reference<Statement>(referencedBy, file));
        public void AddReference(ConstructorCall referencedBy, string? file) => references.Add(new Reference<Statement>(referencedBy, file));
        public void ClearReferences() => references.Clear();

        public bool IsSame(CompiledGeneralFunction other)
        {
            if (this.Type != other.Type) return false;
            if (this.Identifier.Content != other.Identifier.Content) return false;
            if (this.ParameterTypes.Length != other.ParameterTypes.Length) return false;
            for (int i = 0; i < this.ParameterTypes.Length; i++)
            { if (this.ParameterTypes[i] != other.ParameterTypes[i]) return false; }

            return true;
        }
        public bool IsSame(ISameCheck? other) => other is CompiledGeneralFunction other2 && IsSame(other2);

        public CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, this)
        {
            context = this.context,
            TimesUsed = this.TimesUsed,
            TimesUsedTotal = this.TimesUsedTotal,
        };

        public override string ToString()
        {
            StringBuilder result = new();

            if (IsExport)
            { result.Append("export "); }

            result.Append(this.Identifier.Content);

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

        public CompiledGeneralFunctionTemplateInstance InstantiateTemplate(TypeArguments typeParameters)
        {
            CompiledGeneralFunctionTemplateInstance result = new(Type, ParameterTypes, this, this)
            {
                TimesUsed = this.TimesUsed,
                TimesUsedTotal = this.TimesUsedTotal,
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
