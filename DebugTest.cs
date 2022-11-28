using Communicating;

using IngameCoding.BBCode;
using IngameCoding.Bytecode;
using IngameCoding.Core;

using System;
using System.IO;

namespace TheProgram
{
    internal static class DebugTest
    {
        public static void Run(params string[] args)
        {
            var settings = ArgumentParser.Parse(args).Value;
            settings.bytecodeInterpreterSettings.ClockCyclesPerUpdate = 1;

            var ipc = new IPC();

            var code = File.ReadAllText(settings.File.FullName);
            var interpreter = new Interpreter();

            ipc.OnRecived += (manager, message) =>
            {
                if (interpreter == null) return;

                switch (message.type)
                {
                    case "intp-update":
                        {
                            interpreter.Update();
                        }
                        break;
                    case "get-comp-res":
                        {
                            manager.Send("comp-res", new Data_CompilerResult(interpreter.Details.CompilerResult));
                        }
                        break;
                    case "get-intp-data":
                        {
                            if (interpreter.Details.Interpeter == null) return;
                            manager.Send("intp-data", new Data_BytecodeInterpeterDetails(interpreter.Details.Interpeter.Details));
                        }
                        break;
                    case "get-intp2-data":
                        {
                            manager.Send("intp2-data", new Data_CodeInterpeterDetails(interpreter.Details));
                        }
                        break;
                }
            };

            interpreter.OnOutput += (sender, message, logType) =>
            {
                ipc.Send("con-out", new Data_Log((logType.ToString(), message)));
            };

            interpreter.OnNeedInput += (sender, message) =>
            {
                Console.Write(message);
                var input = Console.ReadLine();
                sender.OnInput(input);
            };

            if (interpreter.Initialize())
            {
                var compiledCode = interpreter.CompileCode(code, settings.File.Directory, settings.compilerSettings, settings.parserSettings);

                if (compiledCode != null)
                {
                    interpreter.RunCode(compiledCode, settings.bytecodeInterpreterSettings);
                }
            }

            ipc.Start();
        }
    }

    static class Extensions
    {
        public static double ToUnix(this DateTime v) => v.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        public static TResult[] ToData<TOriginal, TResult>(this TOriginal[] v, Func<TOriginal, TResult> CreateCallback) where TResult : Data_Serializable<TOriginal>
        {
            TResult[] result = new TResult[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                result[i] = CreateCallback.Invoke(v[i]);
            }
            return result;
        }
    }

    public abstract class Data_Serializable<TOriginal>
    {
        internal Data_Serializable(TOriginal v) { }
    }

    internal class Data_CodeInterpeterDetails : Data_Serializable<Interpreter.InterpreterDetails>
    {
        internal Data_CodeInterpeterDetails(Interpreter.InterpreterDetails v) : base(v)
        {
            this.State = v.State.ToString();
        }

        public string State { get; private set; }

        public static Data_BytecodeInterpeterDetails Make(BytecodeInterpeter.InterpeterDetails v) => new(v);
    }

    internal class Data_BytecodeInterpeterDetails : Data_Serializable<BytecodeInterpeter.InterpeterDetails>
    {
        public int BasePointer { get; set; }
        public int CodePointer { get; set; }
        public Data_StackItem[] Stack { get; set; }

        internal Data_BytecodeInterpeterDetails(BytecodeInterpeter.InterpeterDetails v) : base(v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
            Stack = v.Stack.ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpeterDetails Make(BytecodeInterpeter.InterpeterDetails v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<Stack.Item>
    {
        public string Type { get; set; }
        public object Value { get; set; }
        public string Tag { get; set; }

        public Data_StackItem(Stack.Item v) : base(v)
        {
            Type = v.type.ToString();
            Value = v.Value();
            Tag = v.Tag;
        }
    }

    public class Data_CompilerResult : Data_Serializable<Compiler.CompilerResult>
    {
        public Data_Instruction[] CompiledCode { get; set; }
        public int SetGlobalVariablesInstruction { get; set; }
        public int ClearGlobalVariablesInstruction { get; set; }

        public Data_CompilerResult(Compiler.CompilerResult v) : base(v)
        {
            ClearGlobalVariablesInstruction = v.clearGlobalVariablesInstruction;
            SetGlobalVariablesInstruction = v.setGlobalVariablesInstruction;
            CompiledCode = (v.compiledCode == null) ? Array.Empty<Data_Instruction>() : v.compiledCode.ToData(v => new Data_Instruction(v));
        }
    }

    public class Data_Instruction : Data_Serializable<Instruction>
    {
        public int AdditionParameter2 { get; set; }
        public string AdditionParameter { get; set; }
        public string Tag { get; set; }
        public object Parameter { get; set; }
        public string Opcode { get; set; }
        public Data_Instruction(Instruction v) : base(v)
        {
            Opcode = v.opcode.ToString();
            Parameter = v.parameter;
            Tag = v.tag;
            AdditionParameter = v.additionParameter;
            AdditionParameter2 = v.additionParameter2;
        }
    }

    public class Data_Log : Data_Serializable<(string, string)>
    {
        public string Message { get; set; }
        public string Type { get; set; }

        public Data_Log((string, string) v) : base(v)
        {
            this.Type = v.Item1;
            this.Message = v.Item2;
        }
    }
}
