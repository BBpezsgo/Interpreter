using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Core;
using IngameCoding.Errors;
using DataUtilities.Serializer;
using DataUtilities.ReadableFileFormat;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace IngameCoding
{
    internal class CompileIntoFile
    {
        static byte[] Compress(byte[] data, CompressionLevel compressionLevel)
        {
            MemoryStream output = new();
            using (DeflateStream dstream = new(output, compressionLevel))
            { dstream.Write(data, 0, data.Length); }
            return output.ToArray();
        }

        static byte[] Decompress(byte[] data)
        {
            MemoryStream input = new(data);
            MemoryStream output = new();
            using (DeflateStream dstream = new(input, CompressionMode.Decompress))
            { dstream.CopyTo(output); }
            return output.ToArray();
        }

        static void WriteFile(string output, byte[] data, CompressionLevel compressionLevel = CompressionLevel.Optimal, bool LogDebugs = false)
        {
            List<byte> dataToWrite = new();
            switch (compressionLevel)
            {
                case CompressionLevel.NoCompression:
                    dataToWrite.Add(0);
                    dataToWrite.AddRange(data);
                    break;
                case CompressionLevel.Fastest:
                    dataToWrite.Add(1);
                    dataToWrite.AddRange(Compress(data, compressionLevel));
                    break;
                case CompressionLevel.Optimal:
                    dataToWrite.Add(2);
                    dataToWrite.AddRange(Compress(data, compressionLevel));
                    break;
                case CompressionLevel.SmallestSize:
                    dataToWrite.Add(3);
                    dataToWrite.AddRange(Compress(data, compressionLevel));
                    break;
            }
            byte[] dataToWriteArray = dataToWrite.ToArray();
            File.WriteAllBytes(output, dataToWriteArray);

            var compressionRatio = data.LongLength / (float)dataToWriteArray.LongLength;

            if (LogDebugs) Output.Output.Debug($"Done");
            if (LogDebugs) Output.Output.Debug($" Compression Ratio: -{Math.Round((1f - compressionRatio) * 100f)}%");
            if (LogDebugs) Output.Output.Debug($" Size: {dataToWriteArray.LongLength} bytes");
        }
        internal static byte[] ReadFile(string input)
        {
            List<byte> data = File.ReadAllBytes(input).ToList();
            byte compressionLevelI = data[0];
            data.RemoveAt(0);
            var compressedData = data.ToArray();
            return compressionLevelI switch
            {
                0 => compressedData,
                1 => Decompress(compressedData),
                2 => Decompress(compressedData),
                3 => Decompress(compressedData),
                _ => throw new InternalException($"Unknown compression level {compressionLevelI}"),
            };
        }
        internal static string ReadReadableFile(string input) => File.ReadAllText(input);

        internal static void Compile(TheProgram.ArgumentParser.Settings settings)
        {
            var file = settings.File;
            var compilerSettings = settings.compilerSettings;

            compilerSettings.GenerateComments = false;
            compilerSettings.RemoveUnusedFunctionsMaxIterations = 0;
            compilerSettings.GenerateDebugInstructions = false;

            if (settings.LogDebugs) Output.Output.Debug($"Compile file \"{file.FullName}\" ...");

            var code = File.ReadAllText(file.FullName);
            var compilerResult = CompileCode(code, file, new List<Warning>(), compilerSettings, settings.parserSettings);

            switch (settings.CompileToFileType)
            {
                case TheProgram.ArgumentParser.FileType.Binary:
                    CompileToBinary(settings, compilerResult);
                    break;
                case TheProgram.ArgumentParser.FileType.Readable:
                    CompileToReadable(settings, compilerResult);
                    break;
            }

            if (settings.LogDebugs) Output.Output.Debug($"Done");
        }

        static void CompileToBinary(TheProgram.ArgumentParser.Settings settings, Compiler.CompilerResult compilerResult)
        {
            if (settings.LogDebugs) Output.Output.Debug($"Serialize code ...");

            Serializer serializer = new();
            serializer.Serialize(new SerializableCode(compilerResult));

            var output = settings.CompileOutput;
            CompressionLevel compressionLevel = settings.CompressionLevel;

            if (settings.LogDebugs) Output.Output.Debug($"Write binary to \"{output}\" ...");
            if (settings.LogDebugs) Output.Output.Debug($" Compression Level: {compressionLevel}");
            WriteFile(output, serializer.Result, compressionLevel, settings.LogDebugs);
        }

        static void CompileToReadable(TheProgram.ArgumentParser.Settings settings, Compiler.CompilerResult compilerResult)
        {
            if (settings.LogDebugs) Output.Output.Debug($"Serialize code ...");

            Value data = (new SerializableCode(compilerResult)).SerializeText();

            var output = settings.CompileOutput;
            if (settings.LogDebugs) Output.Output.Debug($"Write text to \"{output}\" ...");
            File.WriteAllText(output, data.ToSDF(false));
        }

        internal static SerializableCode Decompile(byte[] data)
        {
            Deserializer serializer = new(data);
            return (SerializableCode)serializer.DeserializeObject<SerializableCode>();
        }

        internal static SerializableCode Decompile(string raw)
        {
            var data = Parser.Parse(raw);
            var serializableCode = new SerializableCode();
            serializableCode.DeserializeText(data);
            return serializableCode;
        }

        internal class SerializableLiteral : ISerializable<SerializableLiteral>, ISerializableText, IDeserializableText
        {
            internal Literal.Type Type;
            internal bool ValueBool;
            internal float ValueFloat;
            internal int ValueInt;
            internal string ValueString;

            public static SerializableLiteral Create(Literal v) => new()
            {
                Type = v.type,
                ValueBool = v.ValueBool,
                ValueFloat = v.ValueFloat,
                ValueInt = v.ValueInt,
                ValueString = v.ValueString,
            };

            public void Deserialize(Deserializer deserializer)
            {
                this.Type = (Literal.Type)deserializer.DeserializeInt32();
                switch (this.Type)
                {
                    case Literal.Type.Integer:
                        ValueInt = deserializer.DeserializeInt32();
                        break;
                    case Literal.Type.Float:
                        ValueFloat = deserializer.DeserializeFloat();
                        break;
                    case Literal.Type.String:
                        ValueString = deserializer.DeserializeString();
                        break;
                    case Literal.Type.Boolean:
                        ValueBool = deserializer.DeserializeBoolean();
                        break;
                }
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize((int)this.Type);
                switch (this.Type)
                {
                    case Literal.Type.Integer:
                        serializer.Serialize(ValueInt);
                        break;
                    case Literal.Type.Float:
                        serializer.Serialize(ValueFloat);
                        break;
                    case Literal.Type.String:
                        serializer.Serialize(ValueString);
                        break;
                    case Literal.Type.Boolean:
                        serializer.Serialize(ValueBool);
                        break;
                }
            }

            Value ISerializableText.SerializeText()
            {
                Value result = Value.Object();
                result["Type"] = Value.Literal((int)Type);
                switch (Type)
                {
                    case Literal.Type.Integer:
                        result["Value"] = Value.Literal(ValueInt);
                        break;
                    case Literal.Type.Float:
                        result["Value"] = Value.Literal(ValueFloat);
                        break;
                    case Literal.Type.String:
                        result["Value"] = Value.Literal(ValueString);
                        break;
                    case Literal.Type.Boolean:
                        result["Value"] = Value.Literal(ValueBool);
                        break;
                }
                return result;
            }

            public void DeserializeText(Value data)
            {
                Type = (Literal.Type)data["Type"].Int;
                switch (Type)
                {
                    case Literal.Type.Integer:
                        ValueInt = (int)data["Value"].Int;
                        break;
                    case Literal.Type.Float:
                        ValueFloat = (float)data["Value"].Float;
                        break;
                    case Literal.Type.String:
                        ValueString = data["Value"].String;
                        break;
                    case Literal.Type.Boolean:
                        ValueBool = (bool)data["Value"].Bool;
                        break;
                    default:
                        break;
                }
            }
        }

        internal class SerializableAttribute : ISerializable<SerializableAttribute>, ISerializableText, IDeserializableText
        {
            internal string Name;
            internal SerializableLiteral[] parameters;

            public static SerializableAttribute Create(AttributeValues v)
            {
                SerializableAttribute result = new()
                {
                    Name = v.Identifier.Content,
                    parameters = new SerializableLiteral[v.parameters.Count],
                };
                for (int i = 0; i < v.parameters.Count; i++)
                { result.parameters[i] = SerializableLiteral.Create(v.parameters[i]); }
                return result;
            }

            public void Deserialize(Deserializer deserializer)
            {
                this.Name = deserializer.DeserializeString();
                this.parameters = deserializer.DeserializeObjectArray<SerializableLiteral>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(this.Name);
                serializer.Serialize(this.parameters);
            }

            Value ISerializableText.SerializeText()
            {
                Value result = Value.Object();
                result["Name"] = Value.Literal(Name);
                result["Parameters"] = Value.Object(parameters);
                return result;
            }
            public void DeserializeText(Value data)
            {
                Name = data["Name"].String;
                parameters = data["Parameters"].Array.Convert<SerializableLiteral>();
            }

            public bool TryGetValue(int index, out string value)
            {
                value = string.Empty;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.String)
                {
                    value = parameters[index].ValueString;
                }
                return true;
            }
            public bool TryGetValue(int index, out int value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Integer)
                {
                    value = parameters[index].ValueInt;
                }
                return true;
            }
            public bool TryGetValue(int index, out float value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Float)
                {
                    value = parameters[index].ValueFloat;
                }
                return true;
            }
            public bool TryGetValue(int index, out bool value)
            {
                value = false;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Boolean)
                {
                    value = parameters[index].ValueBool;
                }
                return true;
            }
        }

        internal class SerializableFunctionDef : ISerializable<SerializableFunctionDef>, ISerializableText, IDeserializableText
        {
            internal string Name;
            internal SerializableAttribute[] Attributes;

            public SerializableFunctionDef() { }

            public SerializableFunctionDef(CompiledFunction funcDef)
            {
                this.Name = funcDef.Identifier.Content;
                this.Attributes = new SerializableAttribute[funcDef.CompiledAttributes.Count];
                for (int i = 0; i < Attributes.Length; i++)
                {
                    Attributes[i] = SerializableAttribute.Create(funcDef.CompiledAttributes.ElementAt(i).Value);
                }
            }

            Value ISerializableText.SerializeText()
            {
                Value result = Value.Object();
                result["Name"] = Value.Literal(Name);
                result["Attributes"] = Value.Object(Attributes);
                return result;
            }

            public void DeserializeText(Value data)
            {
                this.Name = data["Name"].String;
                this.Attributes = data["Attributes"].Array.Convert<SerializableAttribute>();
            }

            public void Deserialize(Deserializer deserializer)
            {
                this.Name = deserializer.DeserializeString();
                this.Attributes = deserializer.DeserializeObjectArray<SerializableAttribute>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(this.Name);
                serializer.Serialize(this.Attributes);
            }

            internal bool TryGetAttribute(string name, out SerializableAttribute attriute)
            {
                attriute = null;
                foreach (var item in Attributes)
                {
                    if (item.Name == name)
                    {
                        attriute = item;
                        return true;
                    }
                }
                return false;
            }
        }

        internal class SerializableCode : ISerializable<SerializableCode>, ISerializableText, IDeserializableText
        {
            internal Instruction[] Instructions;
            internal Dictionary<string, int> FunctionOffsets;
            internal SerializableFunctionDef[] CompiledFunctions;

            public SerializableCode() { }

            public SerializableCode(Compiler.CompilerResult compilerResult)
            {
                Instructions = compilerResult.compiledCode;
                FunctionOffsets = compilerResult.functionOffsets;
                CompiledFunctions = new SerializableFunctionDef[compilerResult.compiledFunctions.Length];
                for (int i = 0; i < compilerResult.compiledFunctions.Length; i++)
                {
                    CompiledFunctions[i] = new SerializableFunctionDef(compilerResult.compiledFunctions[i]);
                }
            }

            public void Deserialize(Deserializer deserializer)
            {
                FunctionOffsets = deserializer.DeserializeDictionary<string, int>();
                Instructions = deserializer.DeserializeObjectArray<Instruction>();
                CompiledFunctions = deserializer.DeserializeObjectArray<SerializableFunctionDef>();
            }

            public void DeserializeText(Value data)
            {
                FunctionOffsets = data["FunctionOffsets"].Dictionary().Convert(v => v.Int ?? -1);
                Instructions = data["Instructions"].Array.Convert<Instruction>();
                CompiledFunctions = data["CompiledFunctions"].Array.Convert<SerializableFunctionDef>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(FunctionOffsets);
                serializer.Serialize(Instructions);
                serializer.Serialize(CompiledFunctions);
            }

            public Value SerializeText()
            {
                Value result = Value.Object();

                result["CompiledFunctions"] = Value.Object(CompiledFunctions);
                result["FunctionOffsets"] = Value.Object(FunctionOffsets);
                result["Instructions"] = Value.Object(Instructions);

                return result;
            }


            internal bool GetFunctionOffset(SerializableFunctionDef compiledFunction, out int functionOffset)
                => FunctionOffsets.TryGetValue(compiledFunction.Name, out functionOffset);
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
            var compilerResult = Compiler.Compile(
                parserResult,
                new Dictionary<string, BuiltinFunction>(),
                file,
                warnings,
                errors,
                parserSettings);

            var codeGeneratorResult = CodeGenerator.Generate(
                compilerResult,
                parserResult.GlobalVariables,
                parserResult.TopLevelStatements,
                compilerSettings
                );

            if (errors.Count > 0)
            { throw new System.Exception("Failed to compile", errors[0].ToException()); }

            return new Compiler.CompilerResult()
            {
                compiledCode = codeGeneratorResult.Code,
                compiledFunctions = codeGeneratorResult.Functions,
                compiledStructs = codeGeneratorResult.Structs,
                compiledVariables = new Dictionary<string, CompiledVariable>(),
                debugInfo = codeGeneratorResult.DebugInfo,
                functionOffsets = new Dictionary<string, int>(),
            };
        }
    }
}