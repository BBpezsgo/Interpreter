using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace TheProgram
{
    class SomeTests
    {
        static readonly Action<string> Write = Console.WriteLine;

        public static void Main()
        {
            string code = @"
            using System;
            namespace RoslynCompileSample
            {
                public class Writer
                {
                    public void Write(string message)
                    {
                        Console.WriteLine($""you said '{message}!'"");
                    }
                }
            }";

            Compile(code, out var assembly);
            Write(assembly.GetType("RoslynCompileSample.Writer").GetMember("Write")[0].Name);
        }

        static CSharpCompilation Compile(string code)
        {
            Write("Parsing ...");
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            Write("Create references ...");

            var refPaths = new[] {
                typeof(System.Object).GetTypeInfo().Assembly.Location,
                typeof(Console).GetTypeInfo().Assembly.Location,
                Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll")
            };

            MetadataReference[] references = refPaths.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

            Write("Adding the following references:");
            foreach (var r in refPaths)
                Write(r);

            Write("Compiling ...");
            CSharpCompilation compilation = CSharpCompilation.Create(
                "out.dll",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation;
        }

        static bool CompileTo(string code, string outputPath)
        {
            using var ms = new MemoryStream();

            EmitResult result = Compile(code).Emit(ms);

            if (!result.Success)
            {
                Write("Compilation failed!");
                foreach (var item in result.Diagnostics)
                { Write($"  {item.Severity} {item.Id}:  {item.GetMessage()}"); }

                return false;
            }
            else
            {
                Write("Compilation successful! ");

                Write("Write to file ...");
                ms.Seek(0, SeekOrigin.Begin);

                File.WriteAllText(outputPath, null);

                var fileStream = File.OpenWrite(outputPath);
                ms.WriteTo(fileStream);
                fileStream.Close();

                return true;
            }
        }

        static bool CompileTo(string code, string outputPath, out Assembly assembly)
        {
            using var ms = new MemoryStream();

            EmitResult result = Compile(code).Emit(ms);

            if (!result.Success)
            {
                Write("Compilation failed!");
                foreach (var item in result.Diagnostics)
                { Write($"  {item.Severity} {item.Id}:  {item.GetMessage()}"); }

                assembly = null;
                return false;
            }
            else
            {
                Write("Compilation successful! ");

                Write("Write to file ...");
                ms.Seek(0, SeekOrigin.Begin);

                File.WriteAllText(outputPath, null);

                var fileStream = File.OpenWrite(outputPath);
                ms.WriteTo(fileStream);
                fileStream.Close();

                assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                return true;
            }
        }

        static bool Compile(string code, out Assembly assembly)
        {
            using var ms = new MemoryStream();

            EmitResult result = Compile(code).Emit(ms);

            if (!result.Success)
            {
                Write("Compilation failed!");
                foreach (var item in result.Diagnostics)
                { Write($"  {item.Severity} {item.Id}:  {item.GetMessage()}"); }

                assembly = null;
                return false;
            }
            else
            {
                Write("Compilation successful! ");

                Write("Read assembly ...");
                ms.Seek(0, SeekOrigin.Begin);

                assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                return true;
            }
        }
    }
}
