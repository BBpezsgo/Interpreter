using System.Collections.Generic;

namespace LanguageCore.Compiler
{
    using Parser;

    public class CompiledStruct : StructDefinition,
        IReferenceable<TypeInstance>
    {
        public new readonly CompiledField[] Fields;
        public CompiledAttributeCollection CompiledAttributes;

        public IReadOnlyList<Reference<TypeInstance>> References => references;
        readonly List<Reference<TypeInstance>> references;

        public IReadOnlyDictionary<string, int> FieldOffsets
        {
            get
            {
                Dictionary<string, int> result = new();
                int currentOffset = 0;
                foreach (CompiledField field in Fields)
                {
                    result.Add(field.Identifier.Content, currentOffset);
                    currentOffset += field.Type.SizeOnStack;
                }
                return result;
            }
        }

        public int SizeOnStack
        {
            get
            {
                int size = 0;
                for (int i = 0; i < Fields.Length; i++)
                {
                    CompiledField field = Fields[i];
                    size += field.Type.SizeOnStack;
                }
                return size;
            }
        }

        public CompiledStruct(CompiledAttributeCollection compiledAttributes, CompiledField[] fields, StructDefinition definition) : base(definition.Name, definition.BracketStart, definition.BracketEnd, definition.Attributes, definition.Fields, definition.Methods, definition.Modifiers)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

            base.FilePath = definition.FilePath;

            base.Statements.Clear();
            base.Statements.AddRange(definition.Statements);

            this.references = new List<Reference<TypeInstance>>();
        }

        public void AddReference(TypeInstance referencedBy, string? file) => references.Add(new Reference<TypeInstance>(referencedBy, file));
        public void ClearReferences() => references.Clear();
    }
}
