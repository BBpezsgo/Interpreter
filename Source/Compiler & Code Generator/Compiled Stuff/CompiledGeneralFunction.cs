using System;
using System.Collections.Generic;

namespace LanguageCore.Compiler
{
    using Parser;
    using Parser.Statement;

    public class CompiledGeneralFunction :
        GeneralFunctionDefinition,
        ICanBeSame,
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

        public IReadOnlyList<(Statement Statement, string? File)> References => references;
        readonly List<(Statement Statement, string? File)> references = new();

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

        public CompiledGeneralFunction(CompiledType type, CompiledType[] parameterTypes, GeneralFunctionDefinition functionDefinition) : base(functionDefinition.Identifier, functionDefinition.Modifiers)
        {
            this.Type = type;
            this.ParameterTypes = parameterTypes;

            base.Parameters = functionDefinition.Parameters;
            base.Block = functionDefinition.Block;

            base.FilePath = functionDefinition.FilePath;
        }

        public void AddReference(KeywordCall statement, string? file) => references.Add((statement, file));
        public void AddReference(ConstructorCall statement, string? file) => references.Add((statement, file));
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
        public bool IsSame(ICanBeSame? other) => other is CompiledGeneralFunction other2 && IsSame(other2);

        public CompiledGeneralFunction Duplicate() => new(Type, ParameterTypes, this)
        {
            context = this.context,
            Modifiers = this.Modifiers,
            TimesUsed = this.TimesUsed,
            TimesUsedTotal = this.TimesUsedTotal,
        };

        public override string ToString()
        {
            string result = string.Empty;
            if (IsExport)
            {
                result += "export ";
            }
            result += this.Identifier.Content;

            result += '(';
            if (this.ParameterTypes.Length > 0)
            {
                for (int i = 0; i < ParameterTypes.Length; i++)
                {
                    if (i > 0) result += ", ";
                    result += ParameterTypes[i].ToString();
                }
            }
            result += ')';

            result += Block?.ToString() ?? ";";

            return result;
        }

        public CompiledGeneralFunctionTemplateInstance InstantiateTemplate(TypeArguments typeParameters)
        {
            CompiledGeneralFunctionTemplateInstance result = new(Type, ParameterTypes, this, this)
            {
                Modifiers = this.Modifiers,
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
