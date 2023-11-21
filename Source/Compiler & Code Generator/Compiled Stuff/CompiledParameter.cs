using System.Diagnostics;

namespace LanguageCore.BBCode.Generator
{
    using LanguageCore.Compiler;
    using Parser;

    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public class CompiledParameter : ParameterDefinition
    {
        public new CompiledType Type;

        public readonly int CurrentParamsSize;
        public readonly int Index;
        public readonly int RealIndex;
        public bool IsRef => Modifiers.Contains("ref");

        public CompiledParameter(int index, int currentParamsSize, CompiledType type, ParameterDefinition definition) : base(definition.Modifiers, definition.Type, definition.Identifier)
        {
            this.Index = index;
            this.CurrentParamsSize = currentParamsSize;
            this.Type = type;
            this.RealIndex = -(currentParamsSize + 1 + CodeGeneratorForMain.TagsBeforeBasePointer);
        }

        public CompiledParameter(CompiledType type, ParameterDefinition definition)
            : this(-1, -1, type, definition)
        {
        }

        public override string ToString() => $"{(IsRef ? "ref " : string.Empty)}{Type} {Identifier} {{ Index: {Index} RealIndex: {RealIndex} }}";
    }
}
