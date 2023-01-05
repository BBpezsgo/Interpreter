using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IngameCoding.Core
{
    using BBCode;
    using Bytecode;
    using Errors;
    using IngameCoding.BBCode.Compiler;
    using Terminal;

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
            internal BytecodeInterpeter Interpeter => interpreter.bytecodeInterpeter;
            internal State State => interpreter.state;

            readonly Interpreter interpreter;

            public InterpreterDetails(Interpreter interpreter) => this.interpreter = interpreter;
        }

        public delegate void OnDoneEventHandler(Interpreter sender, bool success);
        /// <summary>
        /// Will be invoked when the code is completed
        /// </summary>
        public event OnDoneEventHandler OnDone;

        public delegate void OnOutputEventHandler(Interpreter sender, string message, TerminalInterpreter.LogType logType);
        /// <summary>
        /// Will be invoked when the code has output
        /// </summary>
        public event OnOutputEventHandler OnOutput;

        public delegate void OnInputEventHandler(Interpreter sender, string message);
        /// <summary>
        /// Will be invoked when the code needs input<br/>
        /// Call <see cref="OnInput(string)"/> after this invoked
        /// </summary>
        public event OnInputEventHandler OnNeedInput;

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

        readonly Dictionary<string, BuiltinFunction> builtinFunctions = new();
        readonly Dictionary<string, Func<IStruct>> builtinStructs = new();

        InterpreterDetails details;
        public InterpreterDetails Details => details;

        State state;

        bool currentlyRunningCode;

        BytecodeInterpeter bytecodeInterpeter;
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
            bytecodeInterpeter = new BytecodeInterpeter(compiledCode, builtinFunctions, bytecodeInterpreterSettings);
            builtinFunctions.SetInterpeter(bytecodeInterpeter);

            state = State.Initialized;

            OnOutput?.Invoke(this, "Start code ...", TerminalInterpreter.LogType.Debug);
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
        public Instruction[] CompileCode(
            string sourceCode,
            FileInfo file,
            List<Warning> warnings,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings)
        {
            OnOutput?.Invoke(this, $"Parse code: The main source code ...", TerminalInterpreter.LogType.Debug);

            List<Error> errors = new();

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
                (a, b) => OnOutput?.Invoke(this, a, b));

            if (errors.Count > 0)
            { throw new System.Exception("Failed to compile", errors[0].ToException()); }

            details.CompilerResult = compilerResult;

            if (compilerSettings.PrintInstructions)
            { compilerResult.WriteToConsole(); }

            List<string> printedWarnings = new();
            foreach (var warning in warnings)
            {
                if (printedWarnings.Contains(warning.MessageAll)) continue;
                printedWarnings.Add(warning.MessageAll);
                OnOutput?.Invoke(this, warning.MessageAll, TerminalInterpreter.LogType.Warning);
            }

            OnOutput?.Invoke(this, "Initializing bytecode interpreter ...", TerminalInterpreter.LogType.Debug);

            OnlyHaveCode = false;

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.SetGlobalVariables, compilerResult.setGlobalVariablesInstruction);
            instructionOffsets.Set(InstructionOffsets.Kind.ClearGlobalVariables, compilerResult.clearGlobalVariablesInstruction);

            foreach (var compiledFunction in compilerResult.compiledFunctions)
            {
                if (compiledFunction.Value.CompiledAttributes.TryGetValue("CodeEntry", out var attriute))
                {
                    if (attriute.parameters.Count != 0)
                    { throw new CompilerException("Attribute 'CodeEntry' requies 0 parameter", attriute.NameToken); }
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
                    { throw new CompilerException("Attribute 'Catch' requies 1 string parameter", attriute.NameToken); }
                    if (attriute.TryGetValue(0, out string value))
                    {
                        if (value == "update")
                        {
                            if (compilerResult.GetFunctionOffset(compiledFunction.Value, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.Update, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.Value.FullName}' offset not found", compiledFunction.Value.Name); }
                        }
                        else if (value == "end")
                        {
                            if (compilerResult.GetFunctionOffset(compiledFunction.Value, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.Value.FullName}' offset not found", compiledFunction.Value.Name); }
                        }
                        else
                        { throw new CompilerException("Unknown event '" + value + "'", attriute.NameToken); }
                    }
                    else
                    { throw new CompilerException("Attribute requies 1 string parameter", attriute.NameToken); }
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
                OnOutput?.Invoke(this, "Can't run the program: currently running another", TerminalInterpreter.LogType.Warning);
                return false;
            }
            this.currentlyRunningCode = true;

            AddBuiltins();
            AddBuiltinFunction("conin", new BBCode.TypeToken[] {
                new TypeToken("any", BuiltinType.ANY, null)
            }, (DataItem[] parameters) =>
            {
                pauseCode = true;
                if (OnNeedInput == null)
                {
                    OnOutput?.Invoke(this, "Event OnNeedInput does not have listeners", TerminalInterpreter.LogType.Warning);
                    OnInput("");
                }
                else
                {
                    OnNeedInput?.Invoke(this, parameters[0].ToStringValue());
                }
            }, true);

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
        /// Throw or print exceptions?
        /// </param>
        /// <returns>
        /// A list of instructions
        /// </returns>
        public Instruction[] CompileCode(
            string sourceCode,
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings,
            bool HandleErrors = true)
        {
            this.HandleErrors = HandleErrors;
            if (this.HandleErrors)
            {
                List<Warning> warnings = new();
                try
                {
                    return CompileCode(sourceCode, file, warnings, compilerSettings, parserSettings);
                }
                catch (Exception error)
                {
                    OnDone?.Invoke(this, false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { OnOutput?.Invoke(this, warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    OnOutput?.Invoke(this, error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                    Output.Debug.Debug.LogError(error);

                    StackTrace stackTrace = new(error);
                    var stackFrames = stackTrace.GetFrames();

                    string StackTraceString = "";
                    foreach (var frame in stackFrames)
                    {
                        var method = frame.GetMethod();
                        if (method != null)
                        {
                            StackTraceString += "  " + method.Name + "()\n";
                        }
                    }
                    OnOutput?.Invoke(this, "Stack Trace:\n" + StackTraceString, TerminalInterpreter.LogType.Error);
                    OnOutput?.Invoke(this, $"Code cannot be compiled", TerminalInterpreter.LogType.Error);
                    return null;
                }
                catch (System.Exception error)
                {
                    OnDone?.Invoke(this, false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { OnOutput?.Invoke(this, warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    OnOutput?.Invoke(this, $"InternalException ({error.GetType().Name}): {error.Message}", TerminalInterpreter.LogType.Error);
                    Output.Output.Error(error);

                    OnOutput?.Invoke(this, $"Code cannot be compiled", TerminalInterpreter.LogType.Error);
                    return null;
                }
            }
            else
            {
                List<Warning> warnings = new();
                return CompileCode(sourceCode, file, warnings, compilerSettings, parserSettings);
            }
        }

#if false

        public void RunCode_Bytecode(string code, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone, System.Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Item> onInput, VmWindow window)
        {
            this.onDone = onDone;
            onInput = OnInput;

            this.windowForm = window;
            this.windowForm.OnClose += Window_OnClose;

            if (currentlyRunningCode)
            {
                printCallback("Can't run the program: currently running another", TerminalInterpreter.LogType.Warning);
                return;
            }
            this.currentlyRunningCode = true;
            this.printCallback = printCallback;

            AddBuiltins();
            AddBuiltinFunction("conin", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Item[] parameters) =>
            {
                pauseCode = true;
                onNeedInput(parameters[0].ToStringValue());
            }, this.onInput);

            try
            {
                this.printCallback?.Invoke("Parsing Code ...", TerminalInterpreter.LogType.Debug);

                var tokenizer = new IngameCoding.AssemblerLike.Tokenizer();
                var parser = new IngameCoding.AssemblerLike.Parser();

                parser.Parse(tokenizer.Parse(code));

                this.printCallback?.Invoke("Generate bytecodes ...", TerminalInterpreter.LogType.Debug);

                var instructions = parser.GenerateCode();

                this.printCallback?.Invoke("Initializing bytecode interpreter ...", TerminalInterpreter.LogType.Debug);

                instructionOffsets = new() { Offsets = new() };
                instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, 0);

                RunCode(instructions, this.printCallback);
            }
            catch (Exception error)
            {
                this.onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                this.printCallback(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                Debug.LogError(error.ToString());
            }
        }

#endif

        public void Destroy()
        {
            currentlyRunningCode = false;

            if (bytecodeInterpeter != null)
            {
                bytecodeInterpeter.Destroy();
                bytecodeInterpeter = null;
            }

            state = State.Destroyed;
        }

#if false

        public void RunExe(string Code, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone, System.Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Item> onInput)
        {
            this.onDone = onDone;
            onInput = OnInput;

            if (currentlyRunningCode)
            {
                printCallback("Can't run the program: currently running another", TerminalInterpreter.LogType.Warning);
                return;
            }
            this.currentlyRunningCode = true;
            this.printCallback = printCallback;

            AddBuiltins();
            AddBuiltinFunction("conin", new IngameCoding.Code.Type[] {
                new IngameCoding.Code.Type("any", IngameCoding.Code.BuiltinType.ANY)
            }, (IngameCoding.Bytecode.Item[] parameters) =>
            {
                pauseCode = true;
                onNeedInput(parameters[0].ToStringValue());
            }, this.onInput);

            try
            {
                IngameCoding.ExeLike.Tokenizer tokenizer = new();
                var exe = IngameCoding.ExeLike.Parser.LoadExe(Code);
                throw new System.NotImplementedException();
                //RunCode_CLike(exe.compiledFunctions, exe.compiledCode.ToArray(), this.printCallback);
            }
            catch (Exception error)
            {
                this.onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                this.printCallback(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                Debug.LogError(error);
            }
            catch (System.Exception error)
            {
                this.onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                this.printCallback(error.GetType().Name + ": " + error.Message, TerminalInterpreter.LogType.Error);
                Debug.LogError(error);
            }
        }

        public void CompileExe(string Code, FileSystem.File file, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone)
        {
            AddBuiltins();
            AddBuiltinFunction("conin", new IngameCoding.Code.Type[] {
                new IngameCoding.Code.Type("any", IngameCoding.Code.BuiltinType.ANY)
            }, (Item[] parameters) =>
            { }, (_) => { });

            try
            {
                IngameCoding.Code.Compiler.CompileCode(Code, builtinFunctions, os.drivers[0].Folder.GetFolder("Namespaces"), out var compiledFunctions, out var compiledCode, out var compiledStructs);
                IngameCoding.Code.Compiler.SaveExe(file, compiledFunctions, compiledStructs, compiledCode);
                onDone(true);
            }
            catch (IngameCoding.Exception error)
            {
                onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                printCallback(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                Debug.LogError(error);
            }
            catch (System.Exception error)
            {
                onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                printCallback(error.GetType().Name + ": " + error.Message, TerminalInterpreter.LogType.Error);
                Debug.LogError(error);
            }
        }

#endif

        /// <summary>
        /// Provides input to the interpreter<br/>
        /// <lv>WARNING:</lv> Call it only after <see cref="OnNeedInput"/> invoked!
        /// </summary>
        /// <param name="inputValue">
        /// The input value
        /// </param>
        public void OnInput(string inputValue)
        {
            bytecodeInterpeter.AddValueToStack(new DataItem(inputValue, "Console Input"));
            pauseCode = false;
        }

        public void LoadDLL(string path)
        {
            var dll = System.Reflection.Assembly.LoadFile(path);
            var exportedTypes = dll.GetExportedTypes();

            foreach (Type type in exportedTypes)
            {
                Debug.WriteLine(type.FullName);
            }
        }

        void AddBuiltins()
        {
            #region Console

            builtinFunctions.AddBuiltinFunction("conlog", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                if (parameters[0].type == DataItem.Type.LIST)
                {
                    var list = parameters[0].ValueList;
                    OnOutput?.Invoke(this, $"[ {string.Join(", ", list.items)} ]", TerminalInterpreter.LogType.Normal);
                }
                else
                {
                    OnOutput?.Invoke(this, parameters[0].ToStringValue(), TerminalInterpreter.LogType.Normal);
                }
            });
            builtinFunctions.AddBuiltinFunction("conerr", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                OnOutput?.Invoke(this, parameters[0].ToStringValue(), TerminalInterpreter.LogType.Error);
            });
            builtinFunctions.AddBuiltinFunction("conwarn", new TypeToken[] {
                TypeToken.CreateAnonymous("any", BuiltinType.ANY)
            }, (DataItem[] parameters) =>
            {
                OnOutput?.Invoke(this, parameters[0].ToStringValue(), TerminalInterpreter.LogType.Warning);
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

                var newList = new DataItem.List(DataItem.Type.STRING);
                foreach (var item in splitResult)
                {
                    newList.Add(new DataItem(item, ""));
                }
                return new DataItem(newList, "");
            });

            #endregion

            #region Net.Http

            this.builtinFunctions.AddBuiltinFunction<string>("http-get", (url) =>
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
            OnOutput?.Invoke(this, "Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of " + result.ToString(), TerminalInterpreter.LogType.System);
            bytecodeInterpeter = null;
            currentlyRunningCode = false;

            state = State.CodeExecuted;
        }

        bool exitCalled;
        bool globalVariablesCreated;
        bool startCalled;
        bool globalVariablesDisposed;
        bool HandleErrors = true;

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

            if (bytecodeInterpeter != null && !pauseCode)
            {
                if (HandleErrors)
                {
                    try
                    {
                        bytecodeInterpeter.Tick();
                    }
                    catch (RuntimeException error)
                    {
                        OnOutput?.Invoke(this, "Runtime Error: " + error.MessageAll, TerminalInterpreter.LogType.Error);

                        OnDone?.Invoke(this, false);
                        var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                        OnOutput?.Invoke(this, "Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -1", TerminalInterpreter.LogType.System);
                        bytecodeInterpeter = null;
                        currentlyRunningCode = false;

                        Output.Output.Error(error);
                    }
                    catch (System.Exception error)
                    {
                        OnOutput?.Invoke(this, "Internal Error: " + error.Message, TerminalInterpreter.LogType.Error);

                        OnDone?.Invoke(this, false);
                        var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                        OnOutput?.Invoke(this, "Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -1", TerminalInterpreter.LogType.System);
                        bytecodeInterpeter = null;
                        currentlyRunningCode = false;

                        Output.Output.Error(error);
                    }
                }
                else
                {
                    bytecodeInterpeter.Tick();
                }

                if (bytecodeInterpeter != null && !bytecodeInterpeter.IsRunning)
                {
                    if (OnlyHaveCode)
                    {
                        OnCodeExecuted(result);
                    }
                    else
                    {
                        if (!globalVariablesCreated)
                        {
                            bytecodeInterpeter.CallStackPush("state: SetGlobalVariables");
                            state = State.SetGlobalVariables;
                            OnOutput?.Invoke(this, "Set Global Variables", TerminalInterpreter.LogType.Debug);

                            globalVariablesCreated = true;
                            bytecodeInterpeter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.SetGlobalVariables));
                        }
                        else if (!startCalled)
                        {
                            bytecodeInterpeter.CallStackPop();
                            bytecodeInterpeter.CallStackPush("state: Call CodeEntry");
                            state = State.CallCodeEntry;
                            OnOutput?.Invoke(this, "Call CodeEntry", TerminalInterpreter.LogType.Debug);

                            startCalled = true;
                            if (!instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEntry, out int offset))
                            { result = -1; throw new RuntimeException("Function with attribute 'CodeEntry' not found"); }

                            bytecodeInterpeter.Call(offset);
                        }
                        else if (instructionOffsets.TryGet(InstructionOffsets.Kind.Update, out int offset))
                        {
                            bytecodeInterpeter.CallStackPop();
                            bytecodeInterpeter.CallStackPush("event: Update");
                            state = State.CallUpdate;
                            WaitForUpdates(10, () =>
                            {
                                if (bytecodeInterpeter != null)
                                {
                                    bytecodeInterpeter.Call(offset);
                                }
                            });
                        }
                        else if (instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEnd, out offset) && !exitCalled)
                        {
                            bytecodeInterpeter.CallStackPop();
                            bytecodeInterpeter.CallStackPush("event: CodeEnd");
                            state = State.CallCodeEnd;
                            OnOutput?.Invoke(this, "Call CodeEnd", TerminalInterpreter.LogType.Debug);

                            exitCalled = true;
                            if (bytecodeInterpeter != null)
                            {
                                bytecodeInterpeter.Call(offset);
                            }
                        }
                        else if (!globalVariablesDisposed)
                        {
                            bytecodeInterpeter.CallStackPop();
                            bytecodeInterpeter.CallStackPush("state: DisposeGlobalVariables");
                            state = State.DisposeGlobalVariables;
                            OnOutput?.Invoke(this, "Dispose Global Variables", TerminalInterpreter.LogType.Debug);

                            globalVariablesDisposed = true;
                            bytecodeInterpeter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.ClearGlobalVariables));
                        }
                        else
                        {
                            bytecodeInterpeter.CallStackPop();
                            OnCodeExecuted(result);
                        }
                    }
                }
            }
        }

        #region AddBuiltinFunction()

        void AddBuiltinFunction(string name, TypeToken[] parameterTypes, Action<DataItem[]> callback, bool ReturnSomething = false)
        {
            BuiltinFunction function = new(callback, parameterTypes, ReturnSomething);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }

        #endregion
    }

    static class StackItemExtension
    {
        public static object Value(this DataItem item) => item.type switch
        {
            DataItem.Type.INT => item.ValueInt,
            DataItem.Type.FLOAT => item.ValueFloat,
            DataItem.Type.STRING => item.ValueString,
            DataItem.Type.BOOLEAN => item.ValueBoolean,
            _ => null,
        };

        public static bool EqualType(this DataItem item, BuiltinType type)
        {
            switch (item.type)
            {
                case DataItem.Type.INT:
                    if (type == BuiltinType.INT) return true;
                    break;
                case DataItem.Type.FLOAT:
                    if (type == BuiltinType.FLOAT) return true;
                    break;
                case DataItem.Type.STRING:
                    if (type == BuiltinType.STRING) return true;
                    break;
                case DataItem.Type.BOOLEAN:
                    if (type == BuiltinType.BOOLEAN) return true;
                    break;
            }
            return false;
        }
    }

    /// <summary>
    /// A simpler form of <see cref="Interpreter"/><br/>
    /// Just call <see cref="Run(string, bool)"/> and that's it
    /// </summary>
    class EasyInterpreter
    {
        public static void Run(TheProgram.ArgumentParser.Settings settings) => Run(settings.File, settings.parserSettings, settings.compilerSettings, settings.bytecodeInterpreterSettings, settings.LogDebugs, settings.LogSystem, !settings.ThrowErrors);

        /// <summary>
        /// Compiles and interprets source code
        /// </summary>
        /// <param name="path">
        /// The path to the source code file
        /// </param>
        /// <param name="HandleErrors">
        /// Throw or print exceptions?
        /// </param>
        public static void Run(
            FileInfo file,
            BBCode.Parser.ParserSettings parserSettings,
            Compiler.CompilerSettings compilerSettings,
            BytecodeInterpreterSettings bytecodeInterpreterSettings,
            bool LogDebug = true,
            bool LogSystem = true,
            bool HandleErrors = true
            )
        {
            if (LogDebug) Output.Output.Debug($"Run file '{file.FullName}'");
            var code = File.ReadAllText(file.FullName);
            var codeInterpreter = new Interpreter();

            codeInterpreter.OnOutput += (sender, message, logType) =>
            {
                switch (logType)
                {
                    case TerminalInterpreter.LogType.System:
                        if (LogSystem) Output.Output.Log(message);
                        break;
                    case TerminalInterpreter.LogType.Normal:
                        Output.Output.Log(message);
                        break;
                    case TerminalInterpreter.LogType.Warning:
                        Output.Output.Warning(message);
                        break;
                    case TerminalInterpreter.LogType.Error:
                        Output.Output.Error(message);
                        break;
                    case TerminalInterpreter.LogType.Debug:
                        if (LogDebug) Output.Output.Debug(message);
                        break;
                }
            };

            codeInterpreter.OnNeedInput += (sender, message) =>
            {
                Console.Write(message);
                var input = Console.ReadLine();
                sender.OnInput(input);
            };

            if (codeInterpreter.Initialize())
            {
                Instruction[] compiledCode;

                if (file.Extension.ToLower() == ".bcc")
                {
                    var tokens = BCCode.Tokenizer.Parse(code);

                    var parser = new BCCode.Parser();
                    var (statements, labels) = parser.Parse(tokens);

                    compiledCode = BCCode.Parser.GenerateCode(statements, labels);
                }
                else
                {
                    compiledCode = codeInterpreter.CompileCode(code, file, compilerSettings, parserSettings, HandleErrors);
                }

                if (compiledCode != null)
                { codeInterpreter.RunCode(compiledCode, bytecodeInterpreterSettings); }
            }

            while (codeInterpreter.IsExecutingCode)
            {
                if (HandleErrors)
                {
                    try
                    {
                        codeInterpreter.Update();
                    }
                    catch (CompilerException error)
                    {
                        Output.Output.Error($"CompilerException: {error.MessageAll}");
                    }
                    catch (RuntimeException error)
                    {
                        Output.Output.Error($"RuntimeException: {error.MessageAll}");
                    }
                    catch (EndlessLoopException)
                    {
                        Output.Output.Error($"Endless loop!!!");
                    }
                    catch (InternalException error)
                    {
                        Output.Output.Error($"InternalException: {error.Message}");
                    }
                }
                else
                {
                    codeInterpreter.Update();
                }
            }
        }
    }
}
