using System.Diagnostics;
using LanguageCore.Parser;

namespace LanguageCore.BBCode.Compiler
{
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public class CompiledParameter : ParameterDefinition
    {
        public new CompiledType Type;

        readonly int currentParamsSize;

        public readonly int Index;
        public int RealIndex => -(currentParamsSize + 1 + CodeGeneratorForMain.TagsBeforeBasePointer);
        public bool IsRef => Modifiers.Contains("ref");

        public CompiledParameter(int index, int currentParamsSize, CompiledType type, ParameterDefinition definition) : base(definition.Modifiers, definition.Type, definition.Identifier)
        {
            this.Index = index;
            this.currentParamsSize = currentParamsSize;
            this.Type = type;
        }

        public CompiledParameter(CompiledType type, ParameterDefinition definition)
            : this(-1, -1, type, definition)
        {
        }

        public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} RealIndex: {RealIndex} }}";
    }
}
