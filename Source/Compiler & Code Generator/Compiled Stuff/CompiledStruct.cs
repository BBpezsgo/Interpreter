using System.Collections.Generic;
using LanguageCore.Parser;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledStruct : StructDefinition, ITypeDefinition, IDataStructure, IHaveKey<string>
    {
        public new readonly CompiledField[] Fields;
        public CompiledAttributeCollection CompiledAttributes;
        public readonly List<DefinitionReference> References;
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
        public int Size
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

        public CompiledStruct(Dictionary<string, AttributeValues> compiledAttributes, CompiledField[] fields, StructDefinition definition) : base(definition.Name, definition.BracketStart, definition.BracketEnd, definition.Attributes, definition.Fields, definition.Methods, definition.Modifiers)
        {
            this.CompiledAttributes = compiledAttributes;
            this.Fields = fields;

            base.FilePath = definition.FilePath;

            base.Statements.Clear();
            base.Statements.AddRange(definition.Statements);

            this.References = new List<DefinitionReference>();
        }
    }
}
