/*
This demonstrates a simple usage of using custom source providers.
*/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using LanguageCore;
using LanguageCore.BBLang.Generator;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace Examples;

public static class CustomSourceProvider
{
    class SourceProvider : ISourceProviderSync
    {
        readonly Dictionary<string, string> Sources;

        public SourceProvider(Dictionary<string, string> sources)
        {
            Sources = sources;
        }

        // This function will tries to load the requested file.
        // The `requestedFile` is what after the `using` statement.
        //    For example:
        //      `using System` will produce "System"
        //      `using "~/StandardLibrary/System.bbc"` will produce "~/StandardLibrary/System.bbc"
        //      `using "http://localhost/System"` will produce "http://localhost/System"
        // The `currentFile` is the location of the script where the `using` statement is,
        // or `null` if it is the entry point of the application.
        public SourceProviderResultSync TryLoad(string requestedFile, Uri? currentFile)
        {
            // You can also do `requestedFile.StartsWith("local:///")` but Uri is more reliable.
            if (!Uri.TryCreate(currentFile, requestedFile, out Uri? uri))
            {
                return SourceProviderResultSync.NextHandler();
            }

            if (uri.Scheme != "local")
            {
                // If this is not the correct "provider" for the Uri, use the next one.
                // This is the same as "NotFound", but this has a more intuitive name.
                // (maybe in the future I will make it better)
                return SourceProviderResultSync.NextHandler();
            }

            if (!Sources.TryGetValue(uri.AbsolutePath, out string? source))
            {
                // The passed Uri was not found.
                return SourceProviderResultSync.NotFound(uri);
            }

            // On success, return the content of the requested script.
            // There are overloads for this "Success" function that accepts a "byte[]" or "string",
            // but those only avaliable on "sync providers".
            // For "async providers", you can only return a "Stream".
            return SourceProviderResultSync.Success(uri, new MemoryStream(Encoding.UTF8.GetBytes(source)));
        }
    }

    public static void Run()
    {
        Dictionary<string, string> sources = new()
        {
            {
                // Virtual filename
                "/main",

                // Content
                """
                using "./stdlib/stdout";

                u16[] message = "Hello";
                print(&message);
                """
            },

            {
                // Virtual filename
                "/stdlib/stdout",

                // Content
                """
                [External("stdout")]
                void print(u16 c);

                export void print(u16[]* message)
                {
                    for (i32 i = 0; message[i]; i++)
                    {
                        print(message[i]);
                    }
                }
                """
            },
        };

        List<IExternalFunction> externalFunctions = BytecodeProcessor.GetExternalFunctions(StandardIO.Instance);

        DiagnosticsCollection diagnostics = new();

        // Here you can specify what script to load.
        // This is similar to the "file" scheme.
        // The "///" will produce an empty "hostname" in the Uri.
        CompilerResult compiled = StatementCompiler.CompileFile("local:///main", new CompilerSettings(CodeGeneratorForMain.DefaultCompilerSettings)
        {
            ExternalFunctions = externalFunctions.ToImmutableArray(),
            PreprocessorVariables = PreprocessorVariables.Normal,
            SourceProviders = ImmutableArray.Create<ISourceProvider>(
            //  Here you can pass the "source provider"
            //  The "source providers" will used in-order as specified here.
                new SourceProvider(sources)
            ),
        }, diagnostics);

        BBLangGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, MainGeneratorSettings.Default, null, diagnostics);
        diagnostics.Print();
        diagnostics.Throw();

        BytecodeProcessor interpreter = new(
            BytecodeInterpreterSettings.Default,
            generatedCode.Code,
            null,
            generatedCode.DebugInfo,
            externalFunctions,
            generatedCode.GeneratedUnmanagedFunctions
        );

        interpreter.RunUntilCompletion();
    }
}
