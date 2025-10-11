using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
    public static readonly ImmutableDictionary<string, (Register Register, BuiltinType Type)> RegisterKeywords = new Dictionary<string, (Register Register, BuiltinType Type)>()
    {
        { "IP", (Register.CodePointer, BuiltinType.I32) },
        { "SP", (Register.CodePointer, BuiltinType.I32) },
        { "BP", (Register.CodePointer, BuiltinType.I32) },

        { "EAX", (Register.EAX, BuiltinType.I32) },
        { "EBX", (Register.EBX, BuiltinType.I32) },
        { "ECX", (Register.ECX, BuiltinType.I32) },
        { "EDX", (Register.EDX, BuiltinType.I32) },

        { "AX", (Register.AX, BuiltinType.I16) },
        { "BX", (Register.BX, BuiltinType.I16) },
        { "CX", (Register.CX, BuiltinType.I16) },
        { "DX", (Register.DX, BuiltinType.I16) },

        { "AH", (Register.AH, BuiltinType.I8) },
        { "BH", (Register.BH, BuiltinType.I8) },
        { "CH", (Register.CH, BuiltinType.I8) },
        { "DH", (Register.DH, BuiltinType.I8) },

        { "AL", (Register.AL, BuiltinType.I8) },
        { "BL", (Register.BL, BuiltinType.I8) },
        { "CL", (Register.CL, BuiltinType.I8) },
        { "DL", (Register.DL, BuiltinType.I8) },
    }.ToImmutableDictionary();

    public StatementCompiler(CompilerSettings settings, DiagnosticsCollection diagnostics, PrintCallback? print)
    {
        Frames = new();

        CompilableFunctions = new();
        CompilableOperators = new();
        CompilableGeneralFunctions = new();

        Diagnostics = diagnostics;
        Settings = settings;

        ExternalFunctions = settings.ExternalFunctions;
        ExternalConstants = settings.ExternalConstants;
        PreprocessorVariables = settings.PreprocessorVariables ?? Enumerable.Empty<string>();
        UserDefinedAttributes = (settings.UserDefinedAttributes ?? Enumerable.Empty<UserDefinedAttribute>()).ToImmutableArray();
    }
}
