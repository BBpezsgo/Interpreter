using System;
using System.IO;

namespace TheProgram
{
    using Communicating;

    using IngameCoding.BBCode.Compiler;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;

    internal class Debugger
    {
        readonly InterProcessCommunication Ipc;
        readonly string SourceCode;
        Interpreter Interpreter;

        int CurrentLine
        {
            get
            {
                int result = -1;

                for (int i = 0; i < Interpreter.Details.CompilerResult.debugInfo.Length; i++)
                {
                    DebugInfo item = Interpreter.Details.CompilerResult.debugInfo[i];
                    if (item.InstructionStart > Interpreter.Details.Interpreter.CodePointer) continue;
                    if (item.InstructionEnd < Interpreter.Details.Interpreter.CodePointer) continue;

                    result = item.Position.Start.Line;
                }

                return result;
            }
        }

        private bool NeedStdin;

        internal Debugger(ArgumentParser.Settings settings_)
        {
            Ipc = new InterProcessCommunication();
            Ipc.OnRecived += (manager, message) => { if (Interpreter == null) return; OnMessage(message); };

            ArgumentParser.Settings settings = ModifySettings(settings_);
            SourceCode = File.ReadAllText(settings.File.FullName);
            NeedStdin = false;
            InitInterpreter(settings);
            Ipc.Start();
        }

        void InitInterpreter(ArgumentParser.Settings settings)
        {
            Interpreter?.Destroy();
            Interpreter = new Interpreter();

            Interpreter.OnOutput += (sender, message, logType) => Ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(sender.Details)));
            Interpreter.OnStdOut += (_, message) => Ipc.Send("stdout", message);
            Interpreter.OnStdError += (_, message) => Ipc.Send("stderr", message);
            Interpreter.OnNeedInput += _ => NeedStdin = true;

            if (!Interpreter.Initialize()) return;

            Instruction[] compiledCode = Interpreter.CompileCode(settings.File, settings.compilerSettings, settings.parserSettings, settings.HandleErrors);
            if (compiledCode == null) return;

            Interpreter.ExecuteProgram(compiledCode, settings.bytecodeInterpreterSettings);
        }

        static ArgumentParser.Settings ModifySettings(ArgumentParser.Settings settings)
        {
            ArgumentParser.Settings result = settings;
            result.bytecodeInterpreterSettings.ClockCyclesPerUpdate = 1;
            return result;
        }

        void OnMessage(IPCMessage<object> message)
        {
            switch (message.type)
            {
                case "intp/stepline":
                    {
                        int startedLine = CurrentLine;
                        int endlessSafe = 8;
                        while (Interpreter.IsExecutingCode && startedLine == CurrentLine)
                        {
                            if (endlessSafe-- <= 0) break;
                            Interpreter.Update();
                        }
                        Ipc.Reply("intp/updated", "null", message.id);
                    }
                    break;
                case "intp/step":
                    {
                        Interpreter.Update();
                        Ipc.Reply("intp/updated", "null", message.id);
                    }
                    break;
                case "comp/debuginfo":
                    {
                        Ipc.Reply("comp/debuginfo",
                            (Interpreter.Details.CompilerResult.debugInfo == null) ?
                                Array.Empty<Data_DebugInfo>() :
                                Interpreter.Details.CompilerResult.debugInfo.ToData(v => new Data_DebugInfo(v)),
                            message.id);
                    }
                    break;
                case "comp/code":
                    {
                        Ipc.Reply("comp/code",
                            (Interpreter.Details.CompilerResult.compiledCode == null) ?
                                Array.Empty<Data_Instruction>() :
                                Interpreter.Details.CompilerResult.compiledCode.ToData(v => new Data_Instruction(v)),
                            message.id);
                    }
                    break;
                case "get-intp-data":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("intp-data",
                            new Data_BytecodeInterpreterDetails(Interpreter.Details.Interpreter),
                            message.id);
                    }
                    break;
                case "intp/pointers":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("intp/pointers",
                            new BytecodeProcessorPointers(Interpreter.Details.Interpreter),
                            message.id);
                    }
                    break;
                case "intp/stack":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("intp/stack",
                            new Stack_(Interpreter.Details.Interpreter),
                            message.id);
                    }
                    break;
                case "eval":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                    }
                    break;
                case "intp/state":
                    {
                        Ipc.Reply("intp/state",
                            Interpreter.Details.State.ToString(),
                            message.id);
                    }
                    break;
                case "intp/callstack":
                    {
                        Ipc.Reply("intp/callstack",
                            Interpreter.Details.Interpreter.CallStack,
                            message.id);
                    }
                    break;
                case "stdin":
                    if (!NeedStdin) break;
                    Interpreter.OnInput(message.data.ToString()[0]);
                    NeedStdin = false;
                    break;
            }
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
#pragma warning disable IDE0060 // Remove unused parameter
        internal Data_Serializable(TOriginal v) { }
#pragma warning restore IDE0060 // Remove unused parameter
    }

    public struct BytecodeProcessorPointers
    {
        public int BasePointer { get; set; }
        public int CodePointer { get; set; }

        internal BytecodeProcessorPointers(BytecodeInterpreter v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
        }
    }

    public struct Stack_
    {
        public int StackMemorySize { get; set; }
        public Data_StackItem[] Stack { get; set; }

        internal Stack_(BytecodeInterpreter v)
        {
            StackMemorySize = v.StackMemorySize;
            Stack = v.Stack.ToArray().ToData(v => new Data_StackItem(v));
        }
    }

    internal class Data_BytecodeInterpreterDetails : Data_Serializable<BytecodeInterpreter>
    {
        public Data_StackItem[] Heap { get; set; }

        internal Data_BytecodeInterpreterDetails(BytecodeInterpreter v) : base(v)
        {
            Heap = v.Heap.ToArray().ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<DataItem>
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Tag { get; set; }
        public bool IsHeapAddress { get; set; }

        public Data_StackItem(DataItem v) : base(v)
        {
            var v_v = v.Value();
            Type = v.type.ToString();
            Value = v_v == null ? "null" : v_v.ToString();
            Tag = v.Tag;
        }
    }

    public class Data_Function : Data_Serializable<CompiledFunction>
    {
        public string FullName { get; set; }
        public Data_Position Position { get; set; }

        public Data_Function(CompiledFunction v) : base(v)
        {
            this.FullName = v.Identifier.Content;
            this.Position = new Data_Position(v.Identifier.GetPosition());
        }
    }

    public class Data_Position : Data_Serializable<Position>
    {
        public int StartLine { get; set; }
        public int StartChar { get; set; }
        public int EndLine { get; set; }
        public int EndChar { get; set; }
        public int StartTotal { get; set; }
        public int EndTotal { get; set; }

        public Data_Position(Position v) : base(v)
        {
            StartLine = v.Start.Line;
            StartChar = v.Start.Character;

            EndLine = v.End.Line;
            EndChar = v.End.Character;

            StartTotal = v.AbsolutePosition.Start;
            EndTotal = v.AbsolutePosition.End;
        }
    }

    public class Data_DebugInfo : Data_Serializable<DebugInfo>
    {
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public Data_Position Position { get; set; }

        public Data_DebugInfo(DebugInfo v) : base(v)
        {
            StartOffset = v.InstructionStart;
            EndOffset = v.InstructionEnd;
            Position = new Data_Position(v.Position);
        }
    }

    public class Data_Instruction : Data_Serializable<Instruction>
    {
        public string Tag { get; set; }
        public object Parameter { get; set; }
        public bool ParameterIsComplicated { get; set; }
        public string Opcode { get; set; }
        public Data_Instruction(Instruction v) : base(v)
        {
            Opcode = v.opcode.ToString();
            if (v.Parameter is DataItem v2)
            {
                switch (v2.type)
                {
                    case RuntimeType.INT:
                        Parameter = "INT";
                        break;
                    case RuntimeType.FLOAT:
                        Parameter = "FLOAT";
                        break;
                    case RuntimeType.STRING:
                        Parameter = "STRING";
                        break;
                    case RuntimeType.BOOLEAN:
                        Parameter = "BOOLEAN";
                        break;
                    default: throw new NotImplementedException();
                }
                ParameterIsComplicated = true;
            }
            else
            {
                Parameter = v.Parameter;
                ParameterIsComplicated = false;
            }
            Tag = v.tag;
        }
    }

    public class Data_Log
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public Data_Context Context { get; set; }

        public Data_Log(IngameCoding.Output.LogType type, string message, Data_Context context)
        {
            Type = type.ToString();
            Message = message;
            Context = context;
        }
    }

    public class Data_Context : Data_Serializable<Interpreter.InterpreterDetails>
    {
        public int CodePointer { get; set; } = -1;
        public string[] CallStack { get; set; } = Array.Empty<string>();

        public Data_Context(Interpreter.InterpreterDetails v) : base(v)
        {
            try
            {
                if (v == null) return;
                if (v.Interpreter == null) return;
            }
            catch (NullReferenceException)
            {
                return;
            }

            CodePointer = v.Interpreter.CodePointer;
            CallStack = v.Interpreter.CallStack;
        }
    }
}
