using CommandLine;

namespace LanguageCore;

public class CommandLineOptions
{
    [Value(0,
        Required = false,
        HelpText = "The input file to compile. If not specified, the \"interactive\" will be launched instead.")]
    public Uri? Source { get; set; }

    [Option("verbose",
        Required = false,
        HelpText = "Prints some information")]
    public bool Verbose { get; set; }

    [Option('f', "format",
        Required = false,
        Default = "bytecode",
        HelpText = "Sets the format of the generated code")]
    public string? Format { get; set; }

    [Option('d', "debug",
        Required = false,
        HelpText = "Launches the debug screen")]
    public bool Debug { get; set; }

    [Option('o', "output",
        Required = false,
        HelpText = "Writes the generated code to the specified file (this option only works for brainfuck)")]
    public string? Output { get; set; }

    [Option("throw-errors",
        Required = false,
        HelpText = "Whenever an exception occurs, the program crashes. This is useful when debugging the compiler")]
    public bool ThrowErrors { get; set; }

    [Option("basepath",
        Required = false,
        HelpText = "Sets the path where source files will be searched for \"{DeclarationKeywords.Using}\"")]
    public string? BasePath { get; set; }

    [Option("dont-optimize",
        Required = false,
        HelpText = "Disable all optimization")]
    public bool? DontOptimize { get; set; }

    [Option("no-debug-info",
        Required = false,
        HelpText = "Do not generate any debug information (if you compiling into brainfuck, generating debug informations will take a lots of time)")]
    public bool? NoDebugInfo { get; set; }

    [Option("stack-size",
        Required = false,
        HelpText = "Specifies the stack size in bytes")]
    public int? StackSize { get; set; }

    [Option("heap-size",
        Required = false,
        HelpText = "Specifies the heap size in bytes")]
    public int? HeapSize { get; set; }

    [Option("no-nullcheck",
        Required = false,
        HelpText = "Do not generate null-checks when dereferencing a pointer")]
    public bool? NoNullcheck { get; set; }

    [Option("print-instructions",
        Required = false,
        HelpText = "Prints the generated instructions")]
    public bool? PrintInstructions { get; set; }

    [Option("print-memory",
        Required = false,
        HelpText = "Prints the memory after execution completed")]
    public bool? PrintMemory { get; set; }
}