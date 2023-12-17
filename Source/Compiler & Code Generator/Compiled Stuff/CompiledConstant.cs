namespace LanguageCore.Compiler
{
    using Runtime;

    public abstract class CompiledConstant : IThingWithPosition
    {
        public readonly DataItem Value;
        public abstract string Identifier { get; }
        public abstract string? FilePath { get; }
        public abstract Position Position { get; }

        protected CompiledConstant(DataItem value)
        {
            Value = value;
        }
    }
}
