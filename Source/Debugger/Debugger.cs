﻿#if false
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Communicating;
using LanguageCore;
using LanguageCore.BBCode.Generator;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members

#nullable disable

namespace TheProgram
{
    [RequiresDynamicCode("Uses System.Text.Json.JsonSerializer")]
    [RequiresUnreferencedCode("Uses System.Text.Json.JsonSerializer")]
    public class Debugger
    {
        readonly InterProcessCommunication Ipc;
        readonly string SourceCode;
        InterpreterDebuggabble Interpreter;

        int CurrentLine
        {
            get
            {
                if (Interpreter.CompilerResult.DebugInfo.TryGetSourceLocation(Interpreter.BytecodeInterpreter.CodePointer, out SourceCodeLocation sourceLocation))
                { return sourceLocation.SourcePosition.Range.Start.Line; }

                return -1;
            }
        }

        private bool NeedStdin;

        public Debugger(ProgramArguments settings)
        {
            Ipc = new InterProcessCommunication();
            Ipc.OnReceived += (manager, message) => { if (Interpreter == null) return; OnMessage(message); };

            SourceCode = File.ReadAllText(settings.File.FullName);
            NeedStdin = false;
            InitInterpreter(settings);
            Ipc.Start();
        }

        void InitInterpreter(ProgramArguments settings)
        {
            Interpreter?.Dispose();
            Interpreter = new InterpreterDebuggabble();

            Interpreter.OnOutput += (sender, message, logType) => Ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(sender)));
            Interpreter.OnStdOut += (_, message) => Ipc.Send("stdout", message);
            Interpreter.OnStdError += (_, message) => Ipc.Send("stderr", message);
            Interpreter.OnNeedInput += _ => NeedStdin = true;

            if (Interpreter.IsExecutingCode) return;

            try
            {
                CompilerResult compiled = Compiler.Compile(Parser.ParseFile(settings.File.FullName), Interpreter.GenerateExternalFunctions(), settings.File, settings.compilerSettings.BasePath, (message, logType) => Ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(Interpreter))));
                BBCodeGeneratorResult generatedCode = CodeGeneratorForMain.Generate(compiled, settings.compilerSettings, (message, logType) => Ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(Interpreter))));

                Interpreter.CompilerResult = generatedCode;
                Interpreter.Initialize(generatedCode.Code, settings.bytecodeInterpreterSettings);
            }
            catch (Exception ex)
            {
                Ipc.Send("console/out", new Data_Log(LogType.Error, ex.ToString(), new Data_Context(Interpreter)));
                return;
            }
        }

        void OnMessage(IPCMessage<object> message)
        {
            switch (message.Type)
            {
                case "debug/step":
                {
                    Ipc.Log($"Abs Breakpoint: {Interpreter.AbsoluteBreakpoint}");
                    Ipc.Log($"Breakpoint: {Interpreter.Breakpoint}");

                    Interpreter.Breakpoint = Math.Max(1, Interpreter.Breakpoint + 1);

                    Ipc.Log($"Abs Breakpoint: {Interpreter.AbsoluteBreakpoint}");
                    Ipc.Log($"Breakpoint: {Interpreter.Breakpoint}");

                    try
                    { Interpreter.Continue(2); }
                    catch (EndlessLoopException) { }
                    Ipc.Reply("interpreter/updated", "null", message);
                }
                break;
                case "debug/stepinto":
                {
                    Interpreter.StepInto();
                    Ipc.Reply("interpreter/updated", "null", message);
                }
                break;

                case "interpreter/tick":
                {
                    Interpreter.Update();
                    Ipc.Reply("interpreter/updated", "null", message);
                }
                break;

                case "compiler/debuginfo":
                    throw new NotImplementedException();
                case "compiler/code":
                {
                    Ipc.Reply("compiler/code",
                        (Interpreter.CompilerResult.Code == null) ?
                            Array.Empty<Data_Instruction>() :
                            Interpreter.CompilerResult.Code.ToData(v => new Data_Instruction(v)),
                        message);
                }
                break;

                case "interpreter/details":
                {
                    if (Interpreter.BytecodeInterpreter == null) return;
                    Ipc.Reply("interpreter/details", new Data_BytecodeInterpreterDetails(Interpreter.BytecodeInterpreter), message);
                }
                break;
                case "interpreter/registers":
                {
                    if (Interpreter.BytecodeInterpreter == null) return;
                    Ipc.Reply("interpreter/registers", new BytecodeProcessorRegisters(Interpreter.BytecodeInterpreter), message);
                }
                break;
                case "interpreter/stack":
                {
                    if (Interpreter.BytecodeInterpreter == null) return;
                    Ipc.Reply("interpreter/stack", new Stack_(Interpreter.BytecodeInterpreter), message);
                }
                break;
                case "interpreter/state":
                {
                    Ipc.Reply("interpreter/state", Interpreter.State.ToString(), message);
                }
                break;
                case "interpreter/callstack":
                {
                    Ipc.Reply("interpreter/callstack", Interpreter.BytecodeInterpreter.TraceCalls(), message);
                }
                break;

                case "eval":
                {
                    if (Interpreter.BytecodeInterpreter == null) return;
                }
                break;
                case "stdin":
                    if (!NeedStdin) break;
                    Interpreter.OnInput(message.Data.ToString()[0]);
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
        public Data_Serializable(TOriginal v) { }
#pragma warning restore IDE0060 // Remove unused parameter
    }

    public struct BytecodeProcessorRegisters
    {
        public int BasePointer { get; set; }
        public int CodePointer { get; set; }

        public BytecodeProcessorRegisters(BytecodeInterpreter v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
        }
    }

    public struct Stack_
    {
        public Data_StackItem[] Stack { get; set; }

        public Stack_(BytecodeInterpreter v)
        {
            Stack = v.Memory.Stack.ToArray().ToData(v => new Data_StackItem(v));
        }
    }

    public class Data_BytecodeInterpreterDetails : Data_Serializable<BytecodeInterpreter>
    {
        public Data_StackItem[] Heap { get; set; }

        public Data_BytecodeInterpreterDetails(BytecodeInterpreter v) : base(v)
        {
            Heap = v.Memory.Heap.ToArray().ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<DataItem>
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public bool IsHeapAddress { get; set; }

        public Data_StackItem(DataItem data) : base(data)
        {
            if (data.IsNull)
            {
                Type = "BYTE";
                Value = "null";
            }
            else
            {
                object value = data.GetValue();
                Type = data.Type.ToString();
                Value = value == null ? "null" : value.ToString();
            }
        }
    }

    public class Data_Function : Data_Serializable<CompiledFunction>
    {
        public string FullName { get; set; }
        public Data_Position Position { get; set; }

        public Data_Function(CompiledFunction v) : base(v)
        {
            this.FullName = v.Identifier.Content;
            this.Position = new Data_Position(v.Identifier.Position);
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
            StartLine = v.Range.Start.Line;
            StartChar = v.Range.Start.Character;

            EndLine = v.Range.End.Line;
            EndChar = v.Range.End.Character;

            StartTotal = v.AbsoluteRange.Start;
            EndTotal = v.AbsoluteRange.End;
        }
    }

    public class Data_Instruction : Data_Serializable<Instruction>
    {
        public string Opcode { get; set; }
        public Data_StackItem Parameter { get; set; }

        public Data_Instruction(Instruction v) : base(v)
        {
            Opcode = v.opcode.ToString();
            Parameter = new Data_StackItem(v.Parameter);
        }
    }

    public class Data_Log
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public Data_Context Context { get; set; }

        public Data_Log(LogType type, string message, Data_Context context)
        {
            Type = type.ToString();
            Message = message;
            Context = context;
        }
    }

    public class Data_Context : Data_Serializable<Interpreter>
    {
        public int CodePointer { get; set; } = -1;
        public int[] CallStack { get; set; } = Array.Empty<int>();

        public Data_Context(Interpreter v) : base(v)
        {
            try
            {
                if (v == null) return;
                if (v.BytecodeInterpreter == null) return;
            }
            catch (NullReferenceException)
            {
                return;
            }

            CodePointer = v.BytecodeInterpreter.CodePointer;
            CallStack = v.BytecodeInterpreter.TraceCalls();
        }
    }
}

#endif
