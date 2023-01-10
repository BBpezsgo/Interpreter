using System;
using System.Collections.Generic;
using System.IO;
using IngameCoding.BBCode;
using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Core;
using IngameCoding.Serialization;
using IngameCoding.Errors;

namespace IngameCoding
{
    internal class CompileIntoFile
    {
        internal static void Compile(TheProgram.ArgumentParser.Settings settings)
        {
            var output = settings.CompileOutput;
            var file = settings.File;

            var code = File.ReadAllText(file.FullName);
            var compilerResult = CompileCode(code, file, new List<Warning>(), settings.compilerSettings, settings.parserSettings);
            
            IngameCoding.Serialization.Serializer serializer = new();
            serializer.Serialize<Compiler.CompilerResult>(compilerResult);
            File.WriteAllBytes(output, serializer.Result);
        }

        internal static Compiler.CompilerResult Decompile(TheProgram.ArgumentParser.Settings settings)
        {            
            IngameCoding.Serialization.Deserializer serializer = new(File.ReadAllBytes(settings.File.FullName));
            return (Compiler.CompilerResult)serializer.DeserializeObject<Compiler.CompilerResult>();
        }

        static Compiler.CompilerResult CompileCode(
            string sourceCode,
            FileInfo file,
            List<Warning> warnings,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings)
        {
            List<Error> errors = new();

            var parserResult = BBCode.Parser.Parser.Parse(sourceCode, warnings);
            parserResult.SetFile(file.FullName);
            var compilerResult = Compiler.CompileCode(
                parserResult,
                new Dictionary<string, BuiltinFunction>(),
                new Dictionary<string, Func<IStruct>>(),
                file,
                warnings,
                errors,
                compilerSettings,
                parserSettings);

            if (errors.Count > 0)
            { throw new System.Exception("Failed to compile", errors[0].ToException()); }

            return compilerResult;
        }

        static Dictionary<string, BuiltinFunction> GetBuiltinFunctions()
        {
            Dictionary<string, BuiltinFunction> builtinFunctions = new();

            builtinFunctions.AddBuiltinFunction("stdout", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) => { });
            builtinFunctions.AddBuiltinFunction("stderr", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) => { });
            builtinFunctions.AddBuiltinFunction("sleep", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) => { });

            builtinFunctions.AddBuiltinFunction("tmnw", () => { });

            builtinFunctions.AddBuiltinFunction("splitstring", new TypeToken[] {
                TypeToken.CreateAnonymous("string", BuiltinType.STRING),
                TypeToken.CreateAnonymous("string", BuiltinType.STRING)
            }, (DataItem[] parameters) => { });

            builtinFunctions.AddBuiltinFunction<string>("http-get", (url) => { });

            return builtinFunctions;
        }

    }
}