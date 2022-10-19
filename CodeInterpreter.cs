using IngameCoding.BBCode;
using IngameCoding.Bytecode;
using IngameCoding.Terminal;

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace IngameCoding
{
    public struct InstructionOffsets
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


    [System.Serializable]
    public class CodeInterpeter
    {
        readonly Dictionary<string, Compiler.BuiltinFunction> builtinFunctions = new();
        private readonly Dictionary<string, System.Func<Stack.IStruct>> builtinStructs = new();

        public bool currentlyRunningCode = false;

        BytecodeInterpeter bytecodeInterpeter;
        System.Action<string, TerminalInterpreter.LogType> printCallback;
        System.TimeSpan codeStartedTimespan;
        System.Action<bool> onDone;
        System.Action<Stack.Item> onInput = null;

        bool pauseCode = false;

        /// <summary> In ms </summary>
        float pauseCodeFor = 0f;

        int waitForUpdatesCounter;
        System.Action waitForUpdatesCallback;
        void WaitForUpdates(int count, System.Action callback)
        {
            pauseCode = true;
            waitForUpdatesCounter = count;
            waitForUpdatesCallback = callback;
        }

        InstructionOffsets instructionOffsets;

        void RunCode(Instruction[] compiledCode, System.Action<string, TerminalInterpreter.LogType> printCallback)
        {
            codeStartedTimespan = System.DateTime.Now.TimeOfDay;
            bytecodeInterpeter = new BytecodeInterpeter(compiledCode, builtinFunctions);

            this.printCallback?.Invoke("Start Code", TerminalInterpreter.LogType.Normal);
        }

        bool PrepareRunCode(Action<bool> onDone, Action<string, TerminalInterpreter.LogType> printCallback, Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Stack.Item> onInput)
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

        Instruction[] CompileCode(string code, List<Warning> warnings, System.Action<string, TerminalInterpreter.LogType> printCallback)
        {
            var compiler = Compiler.CompileCode(
                code,
                builtinFunctions,
                builtinStructs,
                new DirectoryInfo("D:\\Program Files\\BBCodeProject\\BBCode\\TestFiles"),
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

        public void RunCode_BBCode(string code, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone, System.Action<string> onNeedInput, out System.Action<IngameCoding.Bytecode.Stack.Item> onInput, bool HandleErrors = true)
        {
            if (!PrepareRunCode(onDone, printCallback, onNeedInput, out onInput)) return;

            if (HandleErrors)
            {
                List<Warning> warnings = new();
                try
                {
                    var compiledCode = CompileCode(code, warnings, printCallback);
                    RunCode(compiledCode, this.printCallback);
                }
                catch (Exception error)
                {
                    this.onDone(false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { this.printCallback?.Invoke(warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    this.printCallback?.Invoke(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                    Debug.LogException(error);

                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(error);
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
                    printCallback?.Invoke("Stack Trace:\n" + StackTraceString, TerminalInterpreter.LogType.DebugError);
                    printCallback?.Invoke($"Code cannot be compiled", TerminalInterpreter.LogType.Debug);
                }
                catch (System.Exception error)
                {
                    this.onDone(false);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    foreach (var warning in warnings)
                    { this.printCallback?.Invoke(warning.MessageAll, TerminalInterpreter.LogType.Warning); }

                    this.printCallback?.Invoke($"InternalException ({error.GetType().Name}): {error.Message}", TerminalInterpreter.LogType.Error);
                    Debug.LogException(error);

                    printCallback?.Invoke($"Code cannot be compiled", TerminalInterpreter.LogType.Debug);
                }
            }
            else
            {
                List<Warning> warnings = new();
                var compiledCode = CompileCode(code, warnings, printCallback);
                RunCode(compiledCode, this.printCallback);
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

        /*

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
            AddBuiltinFunction("conin", new IngameCoding.BBCode.Type[] {
                new IngameCoding.BBCode.Type("any", IngameCoding.BBCode.BuiltinType.ANY)
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
                Debug.LogException(error);
            }
            catch (System.Exception error)
            {
                this.onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                this.printCallback(error.GetType().Name + ": " + error.Message, TerminalInterpreter.LogType.Error);
                Debug.LogException(error);
            }
        }

        public void CompileExe(string Code, FileSystem.File file, System.Action<string, TerminalInterpreter.LogType> printCallback, System.Action<bool> onDone)
        {
            AddBuiltins();
            AddBuiltinFunction("conin", new IngameCoding.BBCode.Type[] {
                new IngameCoding.BBCode.Type("any", IngameCoding.BBCode.BuiltinType.ANY)
            }, (Stack.Item[] parameters) =>
            { }, (_) => { });

            try
            {
                IngameCoding.BBCode.Compiler.CompileCode(Code, builtinFunctions, os.drivers[0].Folder.GetFolder("Namespaces"), out var compiledFunctions, out var compiledCode, out var compiledStructs);
                IngameCoding.BBCode.Compiler.SaveExe(file, compiledFunctions, compiledStructs, compiledCode);
                onDone(true);
            }
            catch (IngameCoding.Exception error)
            {
                onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                printCallback(error.GetType().Name + ": " + error.MessageAll, TerminalInterpreter.LogType.Error);
                Debug.LogException(error);
            }
            catch (System.Exception error)
            {
                onDone(false);
                bytecodeInterpeter = null;
                currentlyRunningCode = false;

                printCallback(error.GetType().Name + ": " + error.Message, TerminalInterpreter.LogType.Error);
                Debug.LogException(error);
            }
        }

        */

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
                return new Stack.Item(System.DateTime.Now.ToString("HH:mm:ss"), "tmnw() result");
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

        double GetGoodNumber(double val) => System.Math.Round(val * 100) / 100;

        string GetEllapsedTime(double ms)
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
            var elapsedMilliseconds = (System.DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
            printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of " + result.ToString(), TerminalInterpreter.LogType.Normal);
            bytecodeInterpeter = null;
            currentlyRunningCode = false;
        }

        bool exitCalled = false;
        bool globalVariablesCreated = false;
        bool startCalled = false;
        bool globalVariablesDisposed = false;

        public void Update(float deltaTime)
        {
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
                    var elapsedMilliseconds = (System.DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -1", TerminalInterpreter.LogType.Normal);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    Debug.LogException(error);
                }
                catch (System.Exception error)
                {
                    printCallback("Internal Error: " + error.Message, TerminalInterpreter.LogType.Error);

                    onDone(false);
                    var elapsedMilliseconds = (System.DateTime.Now.TimeOfDay - codeStartedTimespan).TotalMilliseconds;
                    printCallback("Code executed in " + GetEllapsedTime(elapsedMilliseconds) + " with result of -2", TerminalInterpreter.LogType.Normal);
                    bytecodeInterpeter = null;
                    currentlyRunningCode = false;

                    Debug.LogException(error);
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
                        { throw new ParserException("Function with attribute 'CodeEntry' not found"); }

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
                        OnCodeExecuted(0);
                    }
                }
            }
        }

        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, System.Action<Stack.Item[]> callback, System.Action<IngameCoding.Bytecode.Stack.Item> returnCallback)
        {
            Compiler.BuiltinFunction function = new(callback, parameterTypes, true);

            returnCallback += new System.Action<Stack.Item>((result) =>
            {
                function.RaiseReturnEvent(result);
            });

            if (!builtinFunctions.Keys.Contains(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Debug.LogWarning($"Builtin function '{name}'() already defined");
            }
        }
        void AddBuiltinFunction(string name, System.Action callback, System.Action<Stack.Item> returnCallback)
        { AddBuiltinFunction(name, new BBCode.Type[0], (p) => { callback?.Invoke(); }, returnCallback); }

        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, System.Func<Stack.Item[], Stack.Item> callback)
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
                Debug.LogWarning($"Builtin function '{name}'() already defined");
            }
        }

        void AddBuiltinFunction(string name, System.Func<Stack.Item> callback)
        {
            Compiler.BuiltinFunction function = new(new System.Action<Stack.Item[]>((_) =>
            {
                var x = callback();
                this.bytecodeInterpeter.AddValueToStack(x);
            }), new BBCode.Type[0], true);

            if (!builtinFunctions.Keys.Contains(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Debug.LogWarning($"Builtin function '{name}'() already defined");
            }
        }
        void AddBuiltinFunction(string name, BBCode.Type[] parameterTypes, System.Action<Stack.Item[]> callback)
        {
            Compiler.BuiltinFunction function = new((parameters) => { callback(parameters); }, parameterTypes);

            if (!builtinFunctions.Keys.Contains(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Debug.LogWarning($"Builtin function '{name}'() already defined");
            }
        }

        void AddBuiltinFunction(string name, System.Action callback)
        { AddBuiltinFunction(name, new BBCode.Type[0], (p) => { callback?.Invoke(); }); }
    }
}
