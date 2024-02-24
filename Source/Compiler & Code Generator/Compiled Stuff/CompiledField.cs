namespace LanguageCore.Compiler
{
    using Parser;

    public class CompiledField : FieldDefinition
    {
        public new CompiledType Type;
        public CompiledStruct? Class;

        public Protection Protection
        {
            get
            {
                if (ProtectionToken == null) return Protection.Public;
                return ProtectionToken.Content switch
                {
                    "private" => Protection.Private,
                    "public" => Protection.Public,
                    _ => Protection.Public,
                };
            }
        }

        public CompiledField(CompiledType type, CompiledStruct? @struct, FieldDefinition definition) : base(definition)
        {
            Type = type;
            Class = @struct;
        }
    }
}
