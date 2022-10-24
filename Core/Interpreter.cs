using IngameCoding.BBCode;
using IngameCoding.Bytecode;
using IngameCoding.Errors;
using IngameCoding.Terminal;

using System.Diagnostics;

namespace IngameCoding.Core
{
    [Serializable]
    class Interpreter
    {
        public bool IsExecutingCode => currentlyRunningCode;

        struct InstructionOffsets
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

        readonly Dictionary<string, Compiler.BuiltinFunction> builtinFunctions = new();
        readonly Dictionary<string, Func<Stack.IStruct>> builtinStructs = new();

        bool currentlyRunningCode = false;

        BytecodeInterpeter bytecodeInterpeter;
        Action<string, TerminalInterpreter.LogType> printCallback;
        TimeSpan codeStartedTimespan;
        Action<bool> onDone;
        Action<Stack.Item> onInput = null;

        int result = 0;

        bool pauseCode = false;

        /// <summary> In ms </summary>
        float pauseCodeFor = 0f;

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

        void RunCode(Instruction[] compiledCode)
        {
            codeStartedTimespan = DateTime.Now.TimeOfDay;
            bytecodeInterpeter = new BytecodeInterpeter(compiledCode, builtinFunctions);

            this.printCallback?.Invoke("Start Code", TerminalInterpreter.LogType.Normal);
        }

        bool PrepareRunCode(Action<bool> onDone, Action<string, TerminalInterpreter.LogType> printCallback, Action<string> onNeedInput, out Action<IngameCoding.Bytecode.Stack.Item> onInput)
        {
            this.onDone = onDone;
            onInput = OnInput;

            if (currentlyRunningCode)
            {
                printCallback("Can't run the program: currently running another", TerminalInterpreter.LogType.Warning);
                return false;
            }
            this.currentlyRunningCode = true;
            this.printCallback = printCallback;

            AddBuiltins();
            AddBuiltinFunction("conin", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            {
                pauseCode = true;
                onNeedInput?.Invoke(parameters[0].ToStringValue());
            }, this.onInput);

            return true;
        }

        Instruction[] CompileCode(string code, DirectoryInfo directory, List<Warning> warnings, Action<string, TerminalInterpreter.LogType> printCallback)
        {
            var compiler = Compiler.CompileCode(
                code,
                builtinFunctions,
                builtinStructs,
                directory,
                out var compiledFunctions,
                out var compiledCode,
                out _,
                out var cg,
                out var sg,
                warnings,
                printCallback);

            foreach (var warning in warnings)
            { this.printCallback?.Invoke(warning.MessageAll, TerminalInterpreter.LogType.Warning); }

            this.printCallback?.Invoke("Initializing bytecode interpreter...", TerminalInterpreter.LogType.Debug);

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.SetGlobalVariables, sg);
            instructionOffsets.Set(InstructionOffsets.Kind.ClearGlobalVariables, cg);

            foreach (var compiledFunction in compiledFunctions)
            {
                if (compiledFunction.Value.attributes.TryGetValue("CodeEntry", out var attriute))
                {
                    if (attriute.parameters.Count != 0)
                    { throw new ParserException("Attribute 'CodeEntry' requies 0 parameter"); }
                    if (compiler.GetFunctionOffset(compiledFunction.Value.functionDefinition, out int i))
                    {
                        instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, i);
                    }
                    else
                    { throw new InternalException($"Function '{compiledFunction.Value.functionDefinition.FullName}' offset not found"); }
                }
                else if (compiledFunction.Value.attributes.TryGetValue("Catch", out attriute))
                {
                    if (attriute.parameters.Count != 1)
                    { throw new ParserException("Attribute 'Catch' requies 1 string parameter"); }
                    if (attriute.TryGetValue(0, out string value))
                    {
                        if (value == "update")
                        {
                            if (compiler.GetFunctionOffset(compiledFunction.Value.functionDefinition, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.Update, i);
                            }
                            else
                            { throw new ParserException($"Function '{compiledFunction.Value.functionDefinition.FullName}' offset not found"); }
                        }
                        else if (value == "end")
                        {
                            if (compiler.GetFunctionOffset(compiledFunction.Value.functionDefinition, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                            }
                            else
                            { throw new ParserException($"Function '{compiledFunction.Value.functionDefinition.FullName}' offset not found"); }
                        }
                        else
                        { throw new ParserException("Unknown 'Catch' event name '" + value + "'"); }
                    }
                    else
                    { throw new ParserException("Attribute 'Catch' requies 1 string parameter"); }
                }
            }

            return compiledCode;
        }

        internal void RunCode_BBCode(string code, DirectoryInfo directory, Action<string, TerminalInterpreter.LogType> printCallback, Action<bool> onDone, Action<string> onNeedInput, out Action<IngameCoding.Bytecode.Stack.Item> onInput, bool HandleErrors = true)
        {
            if (!PrepareRunCode(onDone, printCallback, onNeedInput, out onInput)) return;

            if (HandleErrors)
            {
                List<Warning> warnings = new();
                try
                {
                    var compiledCode = CompileCode(code, directory, warnings, printCallback);
                    RunCode(compiledCode);
                }
                catch (Errors.Exception error)
                {
                    this.onDone(false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { this.printCallback?.Invoke(warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    this.printCallback?.Invoke(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                    Debug.Debug.LogError(error);

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
                    printCallback?.Invoke("Stack Trace:\n" + StackTraceString, TerminalInterpreter.LogType.Error);
                    printCallback?.Invoke($"Code cannot be compiled", TerminalInterpreter.LogType.Error);
                }
                catch (System.Exception error)
                {
                    this.onDone(false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { this.printCallback?.Invoke(warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    this.printCallback?.Invoke($"InternalException ({error.GetType().Name}): {error.Message}", TerminalInterpreter.LogType.Error);
                    Terminal.Output.LogError(error);

                    printCallback?.Invoke($"Code cannot be compiled", TerminalInterpreter.LogType.Error);
                }
            }
            else
            {
                List<Warning> warnings = new();
                var compiledCode = CompileCode(code, directory, warnings, printCallback);
                RunCode(compiledCode);
            }
        }

#if false

        public void RunCode_Bytecode(string code, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone, System.Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Stack.Item> onInput, VmWindow window)
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
            }, (Stack.Item[] parameters) =>
            {
                pauseCode = true;
                onNeedInput(parameters[0].ToStringValue());
            }, this.onInput);

            try
            {
                this.printCallback?.Invoke("Parsing Code...", TerminalInterpreter.LogType.Debug);

                var tokenizer = new IngameCoding.AssemblerLike.Tokenizer();
                var parser = new IngameCoding.AssemblerLike.Parser();

                parser.Parse(tokenizer.Parse(code));

                this.printCallback?.Invoke("Generate bytecodes...", TerminalInterpreter.LogType.Debug);

                var instructions = parser.GenerateCode();

                this.printCallback?.Invoke("Initializing bytecode interpreter...", TerminalInterpreter.LogType.Debug);

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
                Debug.LogException(error);
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
        }

#if false

        public void RunExe(string Code, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone, System.Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Stack.Item> onInput)
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
            }, (IngameCoding.Bytecode.Stack.Item[] parameters) =>
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
            }, (Stack.Item[] parameters) =>
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

        void AddBuiltins()
        {
            #region Console

            AddBuiltinFunction("conlog", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            {
                if (parameters[0].type == Stack.Item.Type.LIST)
                {
                    var list = parameters[0].ValueList;
                    this.printCallback($"[ {string.Join(", ", list.items)} ]", TerminalInterpreter.LogType.Normal);
                }
                else
                {
                    this.printCallback(parameters[0].ToStringValue(), TerminalInterpreter.LogType.Normal);
                }
            });
            AddBuiltinFunction("conerr", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            {
                this.printCallback(parameters[0].ToStringValue(), TerminalInterpreter.LogType.Error);
            });
            AddBuiltinFunction("conwarn", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            {
                this.printCallback(parameters[0].ToStringValue(), TerminalInterpreter.LogType.Warning);
            });
            AddBuiltinFunction("sleep", new BBCode.Type[] {
                new BBCode.Type("any", BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            {
                pauseCodeFor = parameters[0].ValueInt;
            });

            #endregion

            #region Enviroment

            AddBuiltinFunction("tmnw", () =>
            {
                return new Stack.Item(DateTime.Now.ToString("HH:mm:ss"), "tmnw() result");
            });

            #endregion

            #region Other

            AddBuiltinFunction("splitstring", new BBCode.Type[] {
                new BBCode.Type("string", BuiltinType.STRING),
                new BBCode.Type("string", BuiltinType.STRING)
            }, (Stack.Item[] parameters) =>
            {
                var splitCharacter = parameters[0].ValueString;
                var stringToSplit = parameters[1].ValueString;

                var splitResult = stringToSplit.Split(splitCharacter, StringSplitOptions.None);

                var newList = new Stack.Item.List(Stack.Item.Type.STRING);
                foreach (var item in splitResult)
                {
                    newList.Add(new Stack.Item(item, ""));
                }
                return new Stack.Item(newList, "");
            });

            #endregion

            #region Structs

            #endregion
        }

        void OnInput(Stack.Item x)
        {
            bytecodeInterpeter.AddValueToStack(x);
            pauseCode = false;
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
                return GetGoodNumber(val).ToString() + " ms";
            }

            if (val > 50)
            {
                val /= 50;
            }
            else
            {
                return GetGoodNumber(val).ToString() + " sec";
            }

            return GetGoodNumber(val).ToString() + " min";
        }

        void OnCodeExecuted(int result)
        {
            onDone(true);
            var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
            printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of " + result.ToString(), TerminalInterpreter.LogType.Normal);
            bytecodeInterpeter = null;
            currentlyRunningCode = false;
        }

        bool exitCalled = false;
        bool globalVariablesCreated = false;
        bool startCalled = false;
        bool globalVariablesDisposed = false;

        public void Update()
        {
            Update((float)(DateTime.Now - LastTime).TotalMilliseconds);
        }

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
                try
                {
                    bytecodeInterpeter.Tick();
                }
                catch (RuntimeException error)
                {
                    printCallback("Runtime Error: " + error.MessageAll, TerminalInterpreter.LogType.Error);

                    onDone(false);
                    var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -1", TerminalInterpreter.LogType.Normal);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    Terminal.Output.LogError(error);
                }
                catch (System.Exception error)
                {
                    printCallback("Internal Error: " + error.Message, TerminalInterpreter.LogType.Error);

                    onDone(false);
                    var elapsedMilliseconds = (DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -1", TerminalInterpreter.LogType.Normal);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    Terminal.Output.LogError(error);
                }

                if (bytecodeInterpeter != null && !bytecodeInterpeter.IsRunning)
                {
                    if (!globalVariablesCreated)
                    {
                        this.printCallback?.Invoke("Set Global Variables", TerminalInterpreter.LogType.Debug);

                        globalVariablesCreated = true;
                        bytecodeInterpeter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.SetGlobalVariables));
                    }
                    else if (!startCalled)
                    {
                        this.printCallback?.Invoke("Call CodeEntry", TerminalInterpreter.LogType.Debug);

                        startCalled = true;
                        if (!instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEntry, out int offset))
                        { result = -1; throw new RuntimeException("Function with attribute 'CodeEntry' not found"); }

                        bytecodeInterpeter.Call(offset);
                    }
                    else if (instructionOffsets.TryGet(InstructionOffsets.Kind.Update, out int offset))
                    {
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
                        this.printCallback?.Invoke("Call CodeEnd", TerminalInterpreter.LogType.Debug);

                        exitCalled = true;
                        if (bytecodeInterpeter != null)
                        {
                            bytecodeInterpeter.Call(offset);
                        }
                    }
                    else if (!globalVariablesDisposed)
                    {
                        this.printCallback?.Invoke("Dispose Global Variables", TerminalInterpreter.LogType.Debug);

                        globalVariablesDisposed = true;
                        bytecodeInterpeter.Jump(instructionOffsets.Get(InstructionOffsets.Kind.ClearGlobalVariables));
                    }
                    else
                    {
                        OnCodeExecuted(result);
                    }
                }
            }
        }

        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, Action<Stack.Item[]> callback, Action<IngameCoding.Bytecode.Stack.Item> returnCallback)
        {
            Compiler.BuiltinFunction function = new(callback, parameterTypes, true);

            returnCallback += new Action<Stack.Item>((result) =>
            {
                function.RaiseReturnEvent(result);
            });

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Terminal.Output.LogWarning($"Builtin function '{name}'() already defined");
            }
        }

        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, Func<Stack.Item[], Stack.Item> callback)
        {
            Compiler.BuiltinFunction function = new(new Action<Stack.Item[]>((p) =>
            {
                var x = callback(p);
                this.bytecodeInterpeter.AddValueToStack(x);
            }), parameterTypes, true);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Terminal.Output.LogWarning($"Builtin function '{name}'() already defined");
            }
        }

        void AddBuiltinFunction(string name, Func<Stack.Item> callback)
        {
            Compiler.BuiltinFunction function = new(new Action<Stack.Item[]>((_) =>
            {
                var x = callback();
                this.bytecodeInterpeter.AddValueToStack(x);
            }), Array.Empty<BBCode.Type>(), true);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Terminal.Output.LogWarning($"Builtin function '{name}'() already defined");
            }
        }
        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, Action<Stack.Item[]> callback)
        {
            Compiler.BuiltinFunction function = new((parameters) => { callback(parameters); }, parameterTypes);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Terminal.Output.LogWarning($"Builtin function '{name}'() already defined");
            }
        }
    }

    class EasyInterpreter
    {
        public static void Run(string path, bool HandleErrors = true)
        {
            if (!File.Exists(path))
            {
                Terminal.Output.LogError("File does not exists!");
                return;
            }

            var file = new FileInfo(path);
            Terminal.Output.LogDebug($"Run file '{file.FullName}'");
            var code = File.ReadAllText(file.FullName);
            var codeInterpreter = new Core.Interpreter();
            Action<Bytecode.Stack.Item> onInputDone = null;
            codeInterpreter.RunCode_BBCode(code, file.Directory, (message, logType) =>
            {
                switch (logType)
                {
                    case Terminal.TerminalInterpreter.LogType.Normal:
                        Terminal.Output.Log(message);
                        break;
                    case Terminal.TerminalInterpreter.LogType.Warning:
                        Terminal.Output.LogWarning(message);
                        break;
                    case Terminal.TerminalInterpreter.LogType.Error:
                        Terminal.Output.LogError(message);
                        break;
                    case Terminal.TerminalInterpreter.LogType.Debug:
                        Terminal.Output.LogDebug(message);
                        break;
                }
            }, (success) =>
            {
                if (!success)
                {
                    Terminal.Output.LogError("Failed to execute the code");
                }
            }, (msg) =>
            {
                Console.WriteLine(msg);
                var input = Console.ReadLine();
                onInputDone?.Invoke(new Bytecode.Stack.Item(msg, "Console Input"));
            }, out onInputDone, HandleErrors);

            while (codeInterpreter.IsExecutingCode)
            {
                if (HandleErrors)
                {
                    try
                    {
                        codeInterpreter.Update();
                    }
                    catch (ParserException error)
                    {
                        Terminal.Output.LogError($"ParserException: {error.MessageAll}");
                    }
                    catch (RuntimeException error)
                    {
                        Terminal.Output.LogError($"RuntimeException: {error.MessageAll}");
                    }
                    catch (EndlessLoopException)
                    {
                        Terminal.Output.LogError($"Endless loop!!!");
                    }
                    catch (InternalException error)
                    {
                        Terminal.Output.LogError($"InternalException: {error.Message}");
                    }
                }
                else
                {
                    codeInterpreter.Update();
                }
            }
        }

        public static void Run(params string[] args)
        {
            if (args.Length == 0)
            {
                Output.LogError("Wrong number of arguments was passed!");
                return;
            }

            bool ThrowErrors = args.Contains("-throw-errors");
            string File = args.Last();

            Run(File, !ThrowErrors);
        }
    }
}
