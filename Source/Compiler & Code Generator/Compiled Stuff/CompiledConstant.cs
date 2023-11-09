using LanguageCore.Runtime;

namespace LanguageCore.BBCode.Compiler
{
    public abstract class CompiledConstant : IThingWithPosition
    {
        public readonly DataItem Value;
        public abstract string Identifier { get; }
        public abstract string? FilePath { get; }
        public abstract Position Position { get; }

        public CompiledConstant(DataItem value)
        {
            Value = value;
        }
    }
}
