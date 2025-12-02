using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.Tests;

static class MsilRunner
{
    class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext() : base(isCollectible: true) { }

        protected override System.Reflection.Assembly? Load(AssemblyName assemblyName) => null;
    }

    public static int Run(string file, string input)
    {
        (WeakReference context, int result) = RunWrapper(file, input);
        for (int i = 0; context.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if (context.IsAlive) throw new Exception("AssemblyLoadContext was not collected. Memory may leak between tests.");

        return result;
    }

    static (WeakReference, int) RunWrapper(string file, string input)
    {
        CollectibleAssemblyLoadContext context = new();

        int result = RunImpl(file, input);

        WeakReference weakRef = new(context, trackResurrection: true);
        context.Unload();
        return (weakRef, result);
    }

    static int RunImpl(string file, string input)
    {
        CollectibleAssemblyLoadContext context = new();

        DiagnosticsCollection diagnostics = new();

        CompilerResult compiled = StatementCompiler.CompileFile(file, new CompilerSettings(Utils.GetCompilerSettings(IL.Generator.CodeGeneratorForIL.DefaultCompilerSettings))
        {
            ExternalFunctions = BytecodeProcessor.GetExternalFunctions(new FixedIO(input)).ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.IL,
        }, diagnostics);

        Func<int> generatedCode = IL.Generator.CodeGeneratorForIL.Generate(compiled, diagnostics, new()
        {
            AllowCrash = true,
            AllowHeap = true,
            AllowPointers = true,
        });

        diagnostics.Throw();

        return generatedCode.Invoke();
    }
}
