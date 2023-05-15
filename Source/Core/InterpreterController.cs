using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace IngameCoding.Core
{
    using BBCode;

    using Bytecode;

    using Errors;

    using IngameCoding.BBCode.Compiler;
    using IngameCoding.Output;

    using System.Linq;

    /// <summary>
    /// This compiles and runs the code
    /// </summary>
    [Serializable]
    public class Interpreter
    {
        internal enum State
        {
            Initialized,
            Destroyed,
            SetGlobalVariables,
            CallCodeEntry,
            CallUpdate,
            CallCodeEnd,
            DisposeGlobalVariables,
            CodeExecuted
        }

        public class InterpreterDetails
        {
            internal Compiler.CompilerResult CompilerResult;
            internal InstructionOffsets InstructionOffsets => interpreter.instructionOffsets;
            internal BytecodeInterpreter Interpreter => interpreter.bytecodeInterpreter;
            internal State State => interpreter.state;

            internal Instruction NextInstruction
            {
                get
                {
                    for (int cp = this.interpreter.bytecodeInterpreter.CodePointer; cp < this.CompilerResult.compiledCode.Length; cp++)
                    {
                        if (cp < 0 || cp >= this.CompilerResult.compiledCode.Length) return null;
                        Instruction result = this.CompilerResult.compiledCode[cp];
                        if (result.opcode == Opcode.COMMENT) continue;
                        return result;
                    }
                    return null;
                }
            }

            readonly Interpreter interpreter;

            public InterpreterDetails(Interpreter interpreter) => this.interpreter = interpreter;
        }

        public delegate void OnDoneEventHandler(Interpreter sender, bool success);
        /// <summary>
        /// Will be invoked when the code is completed
        /// </summary>
        public event OnDoneEventHandler OnDone;

        public delegate void OnOutputEventHandler(Interpreter sender, string message, LogType logType);
        /// <summary>
        /// Will be invoked when the interpeter has output
        /// </summary>
        public event OnOutputEventHandler OnOutput;

        public delegate void OnStdOutEventHandler(Interpreter sender, string data);
        /// <summary>
        /// Will be invoked when the code has standard output data
        /// </summary>
        public event OnStdOutEventHandler OnStdOut;

        public delegate void OnStdErrorEventHandler(Interpreter sender, string data);
        /// <summary>
        /// Will be invoked when the code has standard error data
        /// </summary>
        public event OnStdErrorEventHandler OnStdError;

        public delegate void OnInputEventHandler(Interpreter sender);
        /// <summary>
        /// Will be invoked when the code needs input<br/>
        /// Call <see cref="OnInput(char)"/> after this invoked
        /// </summary>
        public event OnInputEventHandler OnNeedInput;

        public delegate void OnExecutedEventHandler(Interpreter sender, OnExecutedEventArgs e);
        public event OnExecutedEventHandler OnExecuted;

        bool OnlyHaveCode = true;
        /// <summary>
        /// True if the interpreter is running some code
        /// </summary>
        public bool IsExecutingCode => currentlyRunningCode;

        internal struct InstructionOffsets
        {
            public enum Kind
            {
                CodeEntry,
                CodeEnd,
                Update,
                ClearGlobalVariables,
                SetGlobalVariables,
            }

            public Dictionary<Kind, int> Offsets;

            public int Get(Kind kind) => Offsets[kind];
            public bool TryGet(Kind kind) => TryGet(kind, out _);
            public bool TryGet(Kind kind, out int offset)
            {
                offset = -1;
                if (Offsets.TryGetValue(kind, out int offset_))
                {
                    if (offset_ > -1)
                    {
                        offset = offset_;
                        return true;
                    }
                    return false;
                }
                return false;
            }
            public void Set(Kind kind, int offset)
            {
                if (Offsets.ContainsKey(kind))
                {
                    Offsets[kind] = offset;
                }
                else
                {
                    Offsets.Add(kind, offset);
                }
            }
        }

        public struct OnExecutedEventArgs
        {
            public double ElapsedMilliseconds;
            public int ExitCode;

            public OnExecutedEventArgs(double elapsedMilliseconds, int exitCode)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
                ExitCode = exitCode;
            }

            public override string ToString() => $"Code executed in {ElapsedTime} with exit code {ExitCode}";
            public string ElapsedTime => GetEllapsedTime(ElapsedMilliseconds);
        }

        readonly Dictionary<string, BuiltinFunction> builtinFunctions = new();
        readonly Dictionary<string, Func<IStruct>> builtinStructs = new();

        InterpreterDetails details;
        public InterpreterDetails Details => details;

        State state;

        bool currentlyRunningCode;

        BytecodeInterpreter bytecodeInterpreter;
        TimeSpan codeStartedTimespan;

        int result;

        bool pauseCode;

        /// <summary> In ms </summary>
        float pauseCodeFor;

        DateTime LastTime = DateTime.Now;

        int waitForUpdatesCounter;
        Action waitForUpdatesCallback;
        void WaitForUpdates(int count, Action callback)
        {
            pauseCode = true;
            waitForUpdatesCounter = count;
            waitForUpdatesCallback = callback;
        }

        InstructionOffsets instructionOffsets;

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        /// <param name="compiledCode"></param>
        public void RunCode(Instruction[] compiledCode, BytecodeInterpreterSettings bytecodeInterpreterSettings)
        {
            codeStartedTimespan = DateTime.Now.TimeOfDay;
            bytecodeInterpreter = new BytecodeInterpreter(compiledCode, builtinFunctions, bytecodeInterpreterSettings);
            builtinFunctions.SetInterpreter(bytecodeInterpreter);

            state = State.Initialized;

            OnOutput?.Invoke(this, "Start code ...", LogType.Debug);
        }

        public Instruction[] Read(byte[] code)
        {
            CompileIntoFile.SerializableCode deserializedCode = CompileIntoFile.Decompile(code);
            return ReadRaw(deserializedCode);
        }

        public Instruction[] Read(string code)
        {
            CompileIntoFile.SerializableCode deserializedCode = CompileIntoFile.Decompile(code);
            return ReadRaw(deserializedCode);
        }

        Instruction[] ReadRaw(CompileIntoFile.SerializableCode deserializedCode)
        {
            List<Error> errors = new();

            details = new InterpreterDetails(this);

            OnOutput?.Invoke(this, "Initializing bytecode interpreter ...", LogType.Debug);

            OnlyHaveCode = false;

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.SetGlobalVariables, deserializedCode.OffsetSetGlobalVariables);
            instructionOffsets.Set(InstructionOffsets.Kind.ClearGlobalVariables, deserializedCode.OffsetClearGlobalVariables);

            foreach (var compiledFunction in deserializedCode.CompiledFunctions)
            {
                if (compiledFunction.TryGetAttribute("CodeEntry", out var attriute))
                {
                    if (attriute.parameters.Length != 0)
                    { throw new CompilerException("Attribute 'CodeEntry' requies 0 parameter", Position.UnknownPosition); }
                    if (deserializedCode.GetFunctionOffset(compiledFunction, out int i))
                    {
                        instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, i);
                    }
                    else
                    { throw new InternalException($"Function '{compiledFunction.FullName}' offset not found"); }
                }
                else if (compiledFunction.TryGetAttribute("Catch", out attriute))
                {
                    if (attriute.parameters.Length != 1)
                    { throw new CompilerException("Attribute 'Catch' requies 1 string parameter", Position.UnknownPosition); }
                    if (attriute.TryGetValue(0, out string value))
                    {
                        if (value == "update")
                        {
                            if (deserializedCode.GetFunctionOffset(compiledFunction, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.Update, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.FullName}' offset not found", Position.UnknownPosition); }
                        }
                        else if (value == "end")
                        {
                            if (deserializedCode.GetFunctionOffset(compiledFunction, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.FullName}' offset not found", Position.UnknownPosition); }
                        }
                        else
                        { throw new CompilerException("Unknown event '" + value + "'", Position.UnknownPosition); }
                    }
                    else
                    { throw new CompilerException("Attribute requies 1 string parameter", Position.UnknownPosition); }
                }
            }

            return deserializedCode.Instructions;
        }

        /// <summary>
        /// Compiles the source code into msg list of instructions<br/>
        /// <lv>WARNING:</lv> This will throw exceptions when they occur! To automatically print errors, use <see cref="CompileCode(string, DirectoryInfo, bool)"/> instead.
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <param name="file">
        /// The source code file
        /// </param>
        /// <param name="warnings">
        /// A list that the compiler can fill with warnings
        /// </param>
        /// <returns>
        /// A list of instructions
        /// </returns>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="Exception"></exception>
        /// <exception cref="System.Exception"></exception>
        public Instruction[] CompileCode(
            string sourceCode,
            FileInfo file,
            List<Warning> warnings,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings,
            out List<Error> errors)
        {
            OnOutput?.Invoke(this, $"Parse code: The main source code ...", LogType.Debug);

            errors = new();

            var parserResult = BBCode.Parser.Parser.Parse(sourceCode, warnings, (msg, lv) => OnOutput?.Invoke(this, $"  {msg}", lv));
            parserResult.SetFile(file.FullName);
            var compilerResult = Compiler.CompileCode(
                parserResult,
                builtinFunctions,
                builtinStructs,
                file,
                warnings,
                errors,
                compilerSettings,
                parserSettings,
                (a, b) => OnOutput?.Invoke(this, a, b),
                BasePath ?? "");

            if (errors.Count > 0)
            {
                var firstError = errors[0].ToException();
                errors.Clear();
                throw new System.Exception("Failed to compile", firstError);
            }

            details.CompilerResult = compilerResult;

            if (compilerSettings.PrintInstructions)
            { compilerResult.WriteToConsole(); }

            List<string> printedWarnings = new();
            foreach (var warning in warnings)
            {
                if (printedWarnings.Contains(warning.MessageAll)) continue;
                printedWarnings.Add(warning.MessageAll);
                OnOutput?.Invoke(this, warning.MessageAll, LogType.Warning);
            }

            OnOutput?.Invoke(this, "Initializing bytecode interpreter ...", LogType.Debug);

            OnlyHaveCode = false;

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.SetGlobalVariables, compilerResult.setGlobalVariablesInstruction);
            instructionOffsets.Set(InstructionOffsets.Kind.ClearGlobalVariables, compilerResult.clearGlobalVariablesInstruction);

            foreach (var compiledFunction in compilerResult.compiledFunctions)
            {
                if (compiledFunction.Value.CompiledAttributes.TryGetValue("CodeEntry", out var attriute))
                {
                    if (attriute.parameters.Count != 0)
                    { throw new CompilerException("Attribute 'CodeEntry' requies 0 parameter", attriute.Identifier); }
                    if (compilerResult.GetFunctionOffset(compiledFunction.Value, out int i))
                    {
                        instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, i);
                    }
                    else
                    { throw new InternalException($"Function '{compiledFunction.Value.FullName}' offset not found"); }
                }
                else if (compiledFunction.Value.CompiledAttributes.TryGetValue("Catch", out attriute))
                {
                    if (attriute.parameters.Count != 1)
                    { throw new CompilerException("Attribute 'Catch' requies 1 string parameter", attriute.Identifier); }
                    if (attriute.TryGetValue(0, out string value))
                    {
                        if (value == "update")
                        {
                            if (compilerResult.GetFunctionOffset(compiledFunction.Value, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.Update, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.Value.FullName}' offset not found", compiledFunction.Value.Identifier); }
                        }
                        else if (value == "end")
                        {
                            if (compilerResult.GetFunctionOffset(compiledFunction.Value, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.Value.FullName}' offset not found", compiledFunction.Value.Identifier); }
                        }
                        else
                        { throw new CompilerException("Unknown event '" + value + "'", attriute.Identifier); }
                    }
                    else
                    { throw new CompilerException("Attribute requies 1 string parameter", attriute.Identifier); }
                }
            }

            return compilerResult.compiledCode;
        }

        /// <summary>
        /// Initializes the compiler
        /// <list type="bullet">
        /// <item>It checks if any code is running</item>
        /// <item>Adds built-in functions</item>
        /// </list>
        /// </summary>
        /// <returns>
        /// True, if you can run some code
        /// </returns>
        public bool Initialize()
        {
            if (currentlyRunningCode)
            {
                OnOutput?.Invoke(this, "Can't run the program: currently running another", LogType.Warning);
                return false;
            }
            this.currentlyRunningCode = true;

            AddBuiltins();
            AddBuiltinFunction("stdin", Array.Empty<TypeToken>(), (DataItem[] parameters) =>
            {
                pauseCode = true;
                if (OnNeedInput == null)
                {
                    OnOutput?.Invoke(this, "Event OnNeedInput does not have listeners", LogType.Warning);
                    OnInput('\0');
                }
                else
                {
                    OnNeedInput?.Invoke(this);
                }
            }, true);
            builtinFunctions.AddBuiltinFunction("test", () =>
            {
                var d = bytecodeInterpreter.Stack.ToByteArray((v) => v.ToByteArray());
                var de = new StepList<byte>(d);
                List<DataItem> r = new();
                de.Next();
                foreach (var item in bytecodeInterpreter.Stack)
                {
                    r.Add(de.GetDataItem(item.Tag));
                }
            });

            details = new InterpreterDetails(this);

            state = State.Initialized;

            return true;
        }

        /// <summary>
        /// Compiles the source code into msg list of instructions
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <param name="file">
        /// The source code file
        /// </param>
        /// <param name="HandleErrors">
        /// Throw exceptions?
        /// </param>
        /// <returns>
        /// A list of instructions
        /// </returns>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="Exception"></exception>
        /// <exception cref="System.Exception"></exception>
        public Instruction[] CompileCode(
            string sourceCode,
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings,
            bool HandleErrors = false)
        {
            this.HandleErrors = HandleErrors;

            List<Warning> warnings = new();
            List<Error> errors = new();
            try
            {
                return CompileCode(sourceCode, file, warnings, compilerSettings, parserSettings, out errors);
            }
            catch (Exception error)
            {
                OnDone?.Invoke(this, false);
                bytecodeInterpreter = null;
                currentlyRunningCode = false;

                OnOutput?.Invoke(this, error.GetType().Name + ": " + error.MessageAll, LogType.Error);
                IngameCoding.Output.Debug.Debug.LogError(error);

                StackTrace stackTrace = new(error);
                var stackFrames = stackTrace.GetFrames();

                string StackTraceString = "";
                foreach (var frame in stackFrames)
                {
                    var method = frame.GetMethod();
                    if (method != null)
                    {
                        StackTraceString += "  " + method.Name + "()\r\n";
                    }
                }
                OnOutput?.Invoke(this, " Stack Trace:\r\n" + StackTraceString, LogType.Error);

                foreach (var warning in warnings)
                { OnOutput?.Invoke(this, warning.MessageAll + "\r\n", LogType.Warning); }

                foreach (var error_ in errors)
                { OnOutput?.Invoke(this, error_.MessageAll + "\r\n", LogType.Error); }

                OnOutput?.Invoke(this, $"Code cannot be compiled", LogType.Error);

                if (!HandleErrors) throw;
            }
            catch (System.Exception error)
            {
                OnDone?.Invoke(this, false);
                bytecodeInterpreter = null;
                currentlyRunningCode = false;

                OnOutput?.Invoke(this, $"InternalException ({error.GetType().Name}): {error.Message}", LogType.Error);
                Output.Error(error);

                foreach (var warning in warnings)
                { OnOutput?.Invoke(this, warning.MessageAll + "\r\n", LogType.Warning); }

                foreach (var error_ in errors)
                { OnOutput?.Invoke(this, error_.MessageAll + "\r\n", LogType.Error); }

                OnOutput?.Invoke(this, $"Code cannot be compiled", LogType.Error);

                if (!HandleErrors) throw;
            }

            return null;
        }

        public void Destroy()
        {
            currentlyRunningCode = false;

            if (bytecodeInterpreter != null)
            {
                bytecodeInterpreter.Destroy();
                bytecodeInterpreter = null;
            }

            state = State.Destroyed;
        }

        /// <summary>
        /// Provides input to the interpreter<br/>
        /// <lv>WARNING:</lv> Call it only after <see cref="OnNeedInput"/> invoked!
        /// </summary>
        /// <param name="key">
        /// The input value
        /// </param>
        public void OnInput(char key)
        {
            bytecodeInterpreter.AddValueToStack(new DataItem(key.ToString(), "Console Input"));
            pauseCode = false;
        }

        public void LoadDLL(string path)
        {
            OnOutput?.Invoke(this, $"Load DLL \"{path}\" ...", LogType.Debug);
            var dll = System.Reflection.Assembly.LoadFile(path);
            var exportedTypes = dll.GetExportedTypes();
            int functionsAdded = 0;

            foreach (Type type in exportedTypes)
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                foreach (var method in methods)
                {
                    var newFunction = builtinFunctions.AddBuiltinFunction(method);
                    OnOutput?.Invoke(this, $" Added function {newFunction.ReadableID()}", LogType.Debug);
                    functionsAdded++;
                }
            }

            OnOutput?.Invoke(this, $"DLL loaded with {functionsAdded} functions", LogType.Debug);
        }

        void AddBuiltins()
        {
            #region Console

            builtinFunctions.AddBuiltinFunction("stdout", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                OnStdOut?.Invoke(this, parameters[0].ToStringValue());
            });
            builtinFunctions.AddBuiltinFunction("stderr", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                OnStdError?.Invoke(this, parameters[0].ToStringValue());
            });
            builtinFunctions.AddBuiltinFunction("sleep", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                pauseCodeFor = parameters[0].ValueInt;
            });

            #endregion

            #region Enviroment

            builtinFunctions.AddBuiltinFunction("tmnw", () =>
            {
                return new DataItem(DateTime.Now.ToString("HH:mm:ss"), "tmnw() result");
            });

            #endregion

            #region Other

            this.builtinFunctions.AddBuiltinFunction("splitstring", new TypeToken[] {
                TypeToken.CreateAnonymous("string", BuiltinType.STRING),
                TypeToken.CreateAnonymous("string", BuiltinType.STRING)
            }, (DataItem[] parameters) =>
            {
                var splitCharacter = parameters[0].ValueString;
                var stringToSplit = parameters[1].ValueString;

                var splitResult = stringToSplit.Split(splitCharacter, StringSplitOptions.None);

                var newList = new DataItem.List(DataType.STRING);
                foreach (var item in splitResult)
                {
                    newList.Add(new DataItem(item, ""));
                }
                return new DataItem(newList, "");
            });

            #endregion

            #region Net.Http

            this.builtinFunctions.AddBuiltinFunction<string>("http_get", (url) =>
            {
                System.Net.Http.HttpClient httpClient = new();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
                System.Net.Http.HttpResponseMessage result = httpClient.GetAsync(url).Result;
                string res = result.Content.ReadAsStringAsync().Result;
                return res;
            });

            #endregion

            #region Structs

            #endregion
        }

        static double GetGoodNumber(double val) => Math.Round(val * 100) / 100;

        static string GetEllapsedTime(double ms)
        {
            var val = ms;

            if (val > 750)
            {
                val /= 1000;
            }
            else
            {
                return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms";
            }

            if (val > 50)
            {
                val /= 50;
            }
            else
            {
                return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " sec";
            }

            return GetGoodNumber(val).ToString(System.Globalization.CultureInfo.InvariantCulture) + " min";
        }

        void OnCodeExecuted(int result)
        {
            OnDone?.Invoke(this, true);
            var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
            OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds, result));
            bytecodeInterpreter = null;
            currentlyRunningCode = false;

            state = State.CodeExecuted;
        }

        bool exitCalled;
        bool globalVariablesCreated;
        bool startCalled;
        bool globalVariablesDisposed;
        bool HandleErrors = true;
        internal string BasePath;

        /// <summary>
        /// Interpret the next instructions
        /// </summary>
        public void Update() => Update((float)(DateTime.Now - LastTime).TotalMilliseconds);
        /// <summary>
        /// Interpret the next instructions
        /// </summary>
        /// <param name="deltaTime">
        /// Time since last <c>Update()</c>
        /// </param>
        /// <exception cref="RuntimeException"></exception>
        /// <exception cref="System.Exception"></exception>
        public void Update(float deltaTime)
        {
            LastTime = DateTime.Now;

            if (pauseCodeFor > 0f)
            {
                pauseCodeFor -= deltaTime;
                return;
            }

            if (waitForUpdatesCounter > 0)
            {
                waitForUpdatesCounter--;
                if (waitForUpdatesCounter <= 0)
                {
                    waitForUpdatesCallback?.Invoke();
                    pauseCode = false;
                }
                return;
            }

            if (bytecodeInterpreter != null && !pauseCode)
            {
                try
                { bytecodeInterpreter.Tick(); }
                catch (RuntimeException error)
                {
                    error.FeedDebugInfo(details.CompilerResult.debugInfo);

                    OnOutput?.Invoke(this, "Runtime Exception: " + error.MessageAll, LogType.Error);

                    OnDone?.Invoke(this, false);
                    var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds, -1));
                    bytecodeInterpreter = null;
                    currentlyRunningCode = false;

                    if (!HandleErrors) throw;
                }
                catch (System.Exception error)
                {
                    OnOutput?.Invoke(this, "Internal Exception: " + error.Message, LogType.Error);

                    OnDone?.Invoke(this, false);
                    var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds, -1));
                    bytecodeInterpreter = null;
                    currentlyRunningCode = false;

                    if (!HandleErrors) throw;
                }

                if (bytecodeInterpreter != null && !bytecodeInterpreter.IsRunning)
                {
                    if (OnlyHaveCode)
                    {
                        OnCodeExecuted(result);
                    }
                    else
                    {
                        if (!globalVariablesCreated)
                        {
                            state = State.SetGlobalVariables;
                            OnOutput?.Invoke(this, "Set Global Variables", LogType.Debug);

                            globalVariablesCreated = true;
                            bytecodeInterpreter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.SetGlobalVariables));
                        }
                        else if (!startCalled)
                        {
                            state = State.CallCodeEntry;
                            OnOutput?.Invoke(this, "Call CodeEntry", LogType.Debug);

                            startCalled = true;
                            if (!instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEntry, out int offset))
                            { result = -1; throw new RuntimeException("Function with attribute 'CodeEntry' not found"); }

                            bytecodeInterpreter.Call(offset);
                        }
                        else if (instructionOffsets.TryGet(InstructionOffsets.Kind.Update, out int offset))
                        {
                            state = State.CallUpdate;
                            WaitForUpdates(10, () => bytecodeInterpreter?.Call(offset));
                        }
                        else if (instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEnd, out offset) && !exitCalled)
                        {
                            state = State.CallCodeEnd;
                            OnOutput?.Invoke(this, "Call CodeEnd", LogType.Debug);

                            exitCalled = true;
                            bytecodeInterpreter?.Call(offset);
                        }
                        else if (!globalVariablesDisposed)
                        {
                            state = State.DisposeGlobalVariables;
                            OnOutput?.Invoke(this, "Dispose Global Variables", LogType.Debug);

                            globalVariablesDisposed = true;
                            bytecodeInterpreter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.ClearGlobalVariables));
                        }
                        else
                        {
                            OnCodeExecuted(result);
                        }
                    }
                }
            }
        }

        #region AddBuiltinFunction()

        void AddBuiltinFunction(string name, TypeToken[] parameterTypes, Action<DataItem[]> callback, bool ReturnSomething = false)
        {
            BuiltinFunction function = new(callback, name, parameterTypes, ReturnSomething);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }

        #endregion
    }
}
