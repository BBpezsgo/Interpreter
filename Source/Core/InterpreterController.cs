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

    /// <summary>
    /// This compiles and runs the code
    /// </summary>
    public class Interpreter
    {
        public enum State
        {
            Initialized,
            Destroyed,
            CallCodeEntry,
            CallUpdate,
            CallCodeEnd,
            CodeExecuted
        }

        protected abstract class Stream
        {
            internal int ID;
            internal int MemoryAddress;
            internal int BufferSize;

            public Stream(int id, int memoryAddress, int bufferSize)
            {
                ID = id;
                MemoryAddress = memoryAddress;
                BufferSize = bufferSize;
            }

            internal abstract void Dispose();

            internal abstract void Tick(IHeap heap);
        }

        protected class InputStream : Stream
        {
            internal int Length;

            internal System.IO.Stream SystemStream;

            internal bool SystemHasData => (SystemStream.Position >= SystemStream.Length);
            internal int RemaingBufferSize => (BufferSize - Length);

            public InputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
                : base(id, memoryAddress, bufferSize)
            {
                SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            internal override void Dispose()
            {
                this.SystemStream?.Close();
                this.SystemStream?.Dispose();

                Debug.WriteLine($"[STREAM {ID}]: Disposed");
            }

            internal void ClearBuffer()
            {
                this.Length = 0;

                Debug.WriteLine($"[STREAM {ID}]: Buffer cleared");
            }

            internal override void Tick(IHeap heap)
            {
                if (RemaingBufferSize == 0) return;
                if (SystemHasData) return;

                byte[] buffer = new byte[RemaingBufferSize];
                int readedCount = SystemStream.Read(buffer, 0, RemaingBufferSize);

                for (int i = 0; i < readedCount; i++)
                {
                    heap[i + MemoryAddress] = new DataItem(buffer[i]);
                }
                Length += readedCount;

                Debug.WriteLine($"[STREAM {ID}]: (AUTO) Readed {readedCount} bytes");
            }
        }

        protected class OutputStream : Stream
        {
            internal int Pointer;

            internal System.IO.Stream SystemStream;

            public OutputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
                : base(id, memoryAddress, bufferSize)
            {
                SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            internal override void Dispose()
            {
                this.SystemStream?.Close();
                this.SystemStream?.Dispose();

                Debug.WriteLine($"[STREAM {ID}]: Disposed");
            }

            internal void Flush(byte[] buffer)
            {
                this.Pointer = 0;

                this.SystemStream.Write(buffer, 0, buffer.Length);
                this.SystemStream.Flush();

                Debug.WriteLine($"[STREAM {ID}]: Write {buffer.Length} bytes");
            }

            internal override void Tick(IHeap heap) { }
        }

        public class InterpreterDetails
        {
            internal Compiler.CompilerResult CompilerResult;
            internal InstructionOffsets InstructionOffsets => interpreter.instructionOffsets;
            internal BytecodeInterpreter Interpreter => interpreter.BytecodeInterpreter;
            internal State State => interpreter.state;

            internal Instruction NextInstruction
            {
                get
                {
                    for (int cp = this.interpreter.BytecodeInterpreter.CodePointer; cp < this.CompilerResult.compiledCode.Length; cp++)
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

        /// <summary>
        /// True if the interpreter is running some code
        /// </summary>
        public bool IsExecutingCode => CurrentlyRunningCode;

        internal struct InstructionOffsets
        {
            public enum Kind
            {
                CodeEntry,
                CodeEnd,
                Update,
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

            public OnExecutedEventArgs(double elapsedMilliseconds)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public override string ToString() => $"Code executed in {ElapsedTime}";
            public string ElapsedTime => IngameCoding.Utils.GetEllapsedTime(ElapsedMilliseconds);
        }

        protected readonly Dictionary<string, BuiltinFunction> builtinFunctions = new();

        protected InterpreterDetails details;
        public InterpreterDetails Details => details;

        protected State state;

        protected bool CurrentlyRunningCode;

        protected BytecodeInterpreter BytecodeInterpreter;
        protected TimeSpan CodeStartedTimespan;

        protected bool PauseCode;
        IReturnValueConsumer ReturnValueConsumer;

        protected List<Stream> Streams;

        /// <summary> In ms </summary>
        protected float PauseCodeTime;

        protected DateTime LastTime = DateTime.Now;

        protected bool exitCalled;
        protected bool startCalled;
        protected bool HandleErrors = true;
        internal string BasePath;

        protected int waitForUpdatesCounter;
        protected Action waitForUpdatesCallback;

        void WaitForUpdates(int count, Action callback)
        {
            PauseCode = true;
            waitForUpdatesCounter = count;
            waitForUpdatesCallback = callback;
        }

        InstructionOffsets instructionOffsets;

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        /// <param name="program"></param>
        public virtual void ExecuteProgram(Instruction[] program, BytecodeInterpreterSettings settings)
        {
            CodeStartedTimespan = DateTime.Now.TimeOfDay;
            BytecodeInterpreter = new BytecodeInterpreter(program, builtinFunctions, settings);
            builtinFunctions.SetInterpreter(BytecodeInterpreter);

            state = State.Initialized;

            OnOutput?.Invoke(this, "Start code ...", LogType.Debug);
        }

        /*
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

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, 0);

            foreach (var compiledFunction in deserializedCode.CompiledFunctions)
            {
                if (compiledFunction.TryGetAttribute("Catch", out var attriute))
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
                            { throw new CompilerException($"Function '{compiledFunction.Name}' offset not found", Position.UnknownPosition); }
                        }
                        else if (value == "end")
                        {
                            if (deserializedCode.GetFunctionOffset(compiledFunction, out int i))
                            {
                                instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                            }
                            else
                            { throw new CompilerException($"Function '{compiledFunction.Name}' offset not found", Position.UnknownPosition); }
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
        */

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
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings)
        {
            OnOutput?.Invoke(this, $"Parse code: The main source code ...", LogType.Debug);

            CodeGenerator.Result codeGeneratorResult = EasyCompiler.Compile(
                file,
                builtinFunctions,
                TokenizerSettings.Default,
                parserSettings,
                compilerSettings,
                (a, b) => OnOutput?.Invoke(this, a, b),
                BasePath
                );

            Dictionary<string, int> functionOffsets = new();
            foreach (var function in codeGeneratorResult.Functions) functionOffsets.Add(function.Key, function.InstructionOffset);

            Compiler.CompilerResult compilerResult1 = new()
            {
                compiledCode = codeGeneratorResult.Code,
                debugInfo = codeGeneratorResult.DebugInfo,

                compiledStructs = codeGeneratorResult.Structs,
                compiledFunctions = codeGeneratorResult.Functions,

                functionOffsets = functionOffsets,
            };

            details.CompilerResult = compilerResult1;

            if (compilerSettings.PrintInstructions)
            { compilerResult1.WriteToConsole(); }

            OnOutput?.Invoke(this, "Initializing bytecode interpreter ...", LogType.Debug);

            instructionOffsets = new() { Offsets = new() };

            instructionOffsets.Set(InstructionOffsets.Kind.CodeEntry, 0);

            foreach (var compiledFunction in compilerResult1.compiledFunctions)
            {
                if (compiledFunction.CompiledAttributes.TryGetAttribute("Catch", out string value))
                {
                    if (value == "update")
                    {
                        if (compilerResult1.GetFunctionOffset(compiledFunction, out int i))
                        {
                            instructionOffsets.Set(InstructionOffsets.Kind.Update, i);
                        }
                        else
                        { throw new CompilerException($"Function '{compiledFunction.Identifier.Content}' offset not found", compiledFunction.Identifier); }
                    }
                    else if (value == "end")
                    {
                        if (compilerResult1.GetFunctionOffset(compiledFunction, out int i))
                        {
                            instructionOffsets.Set(InstructionOffsets.Kind.CodeEnd, i);
                        }
                        else
                        { throw new CompilerException($"Function '{compiledFunction.Identifier.Content}' offset not found", compiledFunction.Identifier); }
                    }
                    else
                    { throw new CompilerException("Unknown event '" + value + "'", Position.UnknownPosition); }
                }
            }

            return compilerResult1.compiledCode;
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
            if (CurrentlyRunningCode)
            {
                OnOutput?.Invoke(this, "Can't run the program: currently running another", LogType.Warning);
                return false;
            }
            this.CurrentlyRunningCode = true;

            if (Streams == null)
            { Streams = new List<Stream>(); }
            else
            {
                for (int i = 0; i < Streams.Count; i++)
                { Streams[i].Dispose(); }
                Streams.Clear();
            }

            AddBuiltins();

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
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings,
            bool HandleErrors = false)
        {
            this.HandleErrors = HandleErrors;

            try
            {
                return CompileCode(file, compilerSettings, parserSettings);
            }
            catch (Exception error)
            {
                OnDone?.Invoke(this, false);
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                PrintException(error);

                if (!HandleErrors) throw;
            }
            catch (System.Exception error)
            {
                OnDone?.Invoke(this, false);
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                PrintException(error);

                if (!HandleErrors) throw;
            }

            return null;
        }

        void PrintException(Exception error)
        {
            OnOutput?.Invoke(this, error.GetType().Name + ": " + error.MessageAll, LogType.Error);
            IngameCoding.Output.Debug.Debug.LogError(error);

            StackTrace stackTrace = new(error);
            var stackFrames = stackTrace.GetFrames();

            OnOutput?.Invoke(this, " Stack Trace:", LogType.Error);

            for (int i = 0; i < stackFrames.Length; i++)
            {
                var method = stackFrames[i].GetMethod();
                if (method != null)
                { OnOutput?.Invoke(this, $"  {method.Name}()", LogType.Error); }
                else
                { OnOutput?.Invoke(this, $"  <null>\r\n", LogType.Error); }
            }
        }

        void PrintException(System.Exception error)
        {
            OnOutput?.Invoke(this, $"InternalException ({error.GetType().Name}): {error.Message}", LogType.Error);
            Output.Error(error);
        }

        public void Destroy()
        {
            CurrentlyRunningCode = false;

            if (BytecodeInterpreter != null)
            {
                BytecodeInterpreter.Destroy();
                BytecodeInterpreter = null;
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
            if (ReturnValueConsumer != null)
            {
                ReturnValueConsumer.Return(new DataItem(key, "Console Input"));
                ReturnValueConsumer = null;
            }

            PauseCode = false;
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
                    OnOutput?.Invoke(this, $" Added function {newFunction.ID}", LogType.Debug);
                    functionsAdded++;
                }
            }

            OnOutput?.Invoke(this, $"DLL loaded with {functionsAdded} functions", LogType.Debug);
        }

        void AddBuiltins()
        {
            #region Console

            builtinFunctions.AddManagedBuiltinFunction("stdin", Array.Empty<BuiltinType>(), (DataItem[] parameters, ManagedBuiltinFunction function) =>
            {
                this.PauseCode = true;
                this.ReturnValueConsumer = function;
                if (this.OnNeedInput == null)
                {
                    this.OnOutput?.Invoke(this, "Event OnNeedInput does not have listeners", LogType.Warning);
                    this.OnInput('\0');
                }
                else
                {
                    this.OnNeedInput?.Invoke(this);
                }
            });

            builtinFunctions.AddBuiltinFunction("stdout",
                (char @char) =>
                {
                    OnStdOut?.Invoke(this, @char.ToString());
                });

            builtinFunctions.AddBuiltinFunction("console-set",
                (char @char, int x, int y) =>
                {
                    if (x < 0 || y < 0) return;
                    var (lx, ly) = Console.GetCursorPosition();
                    Console.SetCursorPosition(x, y);
                    Console.Write(@char);
                    Console.SetCursorPosition(lx, ly);
                });

            builtinFunctions.AddBuiltinFunction("console-clear", () =>
            {
                Console.Clear();
            });

            builtinFunctions.AddBuiltinFunction("stderr",
                (char @char) =>
                {
                    OnStdError?.Invoke(this, @char.ToString());
                });

            builtinFunctions.AddBuiltinFunction("sleep",
                (int t) =>
                {
                    PauseCodeTime = t;
                });

            #endregion

            #region Math

            builtinFunctions.AddBuiltinFunction("cos",
                (float v) =>
                { return MathF.Cos(v); });

            builtinFunctions.AddBuiltinFunction("sin",
                (float v) =>
                { return MathF.Sin(v); });

            #endregion

            #region Enviroment

            builtinFunctions.AddBuiltinFunction("time", () =>
            {
                throw new NotImplementedException();
            });

            #endregion

            #region Casts

            builtinFunctions.AddBuiltinFunction("float-to-int",
                (float @float) =>
                { return (int)@float; });
            builtinFunctions.AddBuiltinFunction("int-to-float",
                (int @int) =>
                { return (float)@int; });

            #endregion

            #region Streams

            builtinFunctions.AddBuiltinFunction("stream-c",
                (int bufferSize, int bufferMemoryAddress) =>
                {
                    int newID = 1;
                    while (true)
                    {
                        bool idIsUnique = true;
                        for (int i = 0; i < Streams.Count; i++)
                        {
                            if (Streams[i].ID == newID)
                            {
                                newID++;
                                idIsUnique = false;
                                break;
                            }
                        }
                        if (idIsUnique) break;
                    }

                    Stream newStream = new InputStream(newID, bufferMemoryAddress, bufferSize, System.IO.File.Open(@"C:\Users\bazsi\Desktop\test.txt", FileMode.OpenOrCreate));

                    Streams.Add(newStream);

                    return newID;
                });
            builtinFunctions.AddBuiltinFunction("stream-d",
                (int id) =>
                {
                    for (int i = Streams.Count - 1; i >= 0; i--)
                    {
                        if (Streams[i].ID != id) continue;

                        Stream stream = Streams[i];
                        stream.Dispose();
                        Streams.RemoveAt(i);

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            builtinFunctions.AddBuiltinFunction("stream-f",
                (int id, int count) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not OutputStream stream)
                        { throw new RuntimeException($"Stream {id} is not OutputStream"); }

                        byte[] buffer = new byte[count];
                        for (int j = 0; j < count; j++)
                        {
                            buffer[j] = ((IHeap)BytecodeInterpreter.Heap)[j + stream.MemoryAddress].Byte ?? 0;
                        }

                        stream.Flush(buffer);

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            builtinFunctions.AddBuiltinFunction("stream-l",
                (int id) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not InputStream stream)
                        { throw new RuntimeException($"Stream {id} is not InputStream"); }

                        return stream.Length;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });
            builtinFunctions.AddBuiltinFunction("stream-r",
                (int id) =>
                {
                    for (int i = 0; i < Streams.Count; i++)
                    {
                        if (Streams[i].ID != id) continue;

                        if (Streams[i] is not InputStream stream)
                        { throw new RuntimeException($"Stream {id} is not InputStream"); }

                        stream.ClearBuffer();

                        return;
                    }

                    throw new RuntimeException($"Stream {id} not found");
                });

            #endregion
        }

        protected void OnCodeExecuted()
        {
            OnDone?.Invoke(this, true);
            var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
            OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
            BytecodeInterpreter = null;
            CurrentlyRunningCode = false;

            state = State.CodeExecuted;

            for (int i = 0; i < Streams.Count; i++)
            { Streams[i].Dispose(); }
            Streams.Clear();
        }

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

            if (BytecodeInterpreter != null && BytecodeInterpreter.Heap != null)
            {
                for (int i = 0; i < Streams.Count; i++)
                { Streams[i].Tick((IHeap)BytecodeInterpreter.Heap); }
            }

            if (PauseCodeTime > 0f)
            {
                PauseCodeTime -= deltaTime;
                return;
            }

            if (waitForUpdatesCounter > 0)
            {
                waitForUpdatesCounter--;
                if (waitForUpdatesCounter <= 0)
                {
                    waitForUpdatesCallback?.Invoke();
                    PauseCode = false;
                }
                return;
            }

            if (BytecodeInterpreter == null || PauseCode) return;

            try
            { BytecodeInterpreter.Tick(); }
            catch (UserException error)
            {
                error.FeedDebugInfo(details.CompilerResult.debugInfo);

                OnOutput?.Invoke(this, "User Exception: " + error.Value.ToString() + "\r\n" + error.MessageAll, LogType.Error);

                OnDone?.Invoke(this, false);
                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                if (!HandleErrors) throw;
            }
            catch (RuntimeException error)
            {
                error.FeedDebugInfo(details.CompilerResult.debugInfo);

                OnOutput?.Invoke(this, "Runtime Exception: " + error.MessageAll, LogType.Error);

                OnDone?.Invoke(this, false);
                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                if (!HandleErrors) throw;
            }
            catch (System.Exception error)
            {
                OnOutput?.Invoke(this, "Internal Exception: " + error.Message, LogType.Error);

                OnDone?.Invoke(this, false);
                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                if (!HandleErrors) throw;
            }

            if (BytecodeInterpreter == null || BytecodeInterpreter.IsExecuting) return;

            int offset;

            if (!startCalled)
            {
                state = State.CallCodeEntry;
                OnOutput?.Invoke(this, "Call CodeEntry", LogType.Debug);

                startCalled = true;
                if (!instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEntry, out offset))
                { throw new RuntimeException("Function with attribute 'CodeEntry' not found"); }

                BytecodeInterpreter.Jump(offset);
                return;
            }

            if (instructionOffsets.TryGet(InstructionOffsets.Kind.Update, out offset))
            {
                state = State.CallUpdate;
                WaitForUpdates(10, () => BytecodeInterpreter?.Call(offset, null));
                return;
            }

            if (instructionOffsets.TryGet(InstructionOffsets.Kind.CodeEnd, out offset) && !exitCalled)
            {
                state = State.CallCodeEnd;
                OnOutput?.Invoke(this, "Call CodeEnd", LogType.Debug);

                exitCalled = true;
                BytecodeInterpreter?.Call(offset, null);
                return;
            }

            OnCodeExecuted();
        }
    }
}
