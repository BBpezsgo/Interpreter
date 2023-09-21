using System;
using System.Collections.Generic;
using System.IO;

namespace ProgrammingLanguage.Core
{
    using BBCode;
    using BBCode.Compiler;
    using BBCode.Parser;
    using Bytecode;
    using Errors;
    using Output;

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
            internal int RemainingBufferSize => (BufferSize - Length);

            public InputStream(int id, int memoryAddress, int bufferSize, System.IO.Stream stream)
                : base(id, memoryAddress, bufferSize)
            {
                SystemStream = stream ?? throw new ArgumentNullException(nameof(stream));
            }

            internal override void Dispose()
            {
                this.SystemStream?.Close();
                this.SystemStream?.Dispose();

                Debug.Log($"[STREAM {ID}]: Disposed");
            }

            internal void ClearBuffer()
            {
                this.Length = 0;

                Debug.Log($"[STREAM {ID}]: Buffer cleared");
            }

            internal override void Tick(IHeap heap)
            {
                if (RemainingBufferSize == 0) return;
                if (SystemHasData) return;

                byte[] buffer = new byte[RemainingBufferSize];
                int readCount = SystemStream.Read(buffer, 0, RemainingBufferSize);

                for (int i = 0; i < readCount; i++)
                {
                    heap[i + MemoryAddress] = new DataItem(buffer[i]);
                }
                Length += readCount;

                Debug.Log($"[STREAM {ID}]: (AUTO) Read {readCount} bytes");
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

                Debug.Log($"[STREAM {ID}]: Disposed");
            }

            internal void Flush(byte[] buffer)
            {
                this.Pointer = 0;

                this.SystemStream.Write(buffer, 0, buffer.Length);
                this.SystemStream.Flush();

                Debug.Log($"[STREAM {ID}]: Write {buffer.Length} bytes");
            }

            internal override void Tick(IHeap heap) { }
        }

        public class InterpreterDetails
        {
            internal CodeGenerator.Result CompilerResult;
            internal InstructionOffsets InstructionOffsets => interpreter.instructionOffsets;
            internal BytecodeInterpreter Interpreter => interpreter.BytecodeInterpreter;
            internal State State => interpreter.state;

            internal Instruction NextInstruction
            {
                get
                {
                    if (this.interpreter == null) return null;
                    if (this.interpreter.BytecodeInterpreter == null) return null;
                    for (int cp = this.interpreter.BytecodeInterpreter.CodePointer; cp < this.CompilerResult.Code.Length; cp++)
                    {
                        if (cp < 0 || cp >= this.CompilerResult.Code.Length) return null;
                        Instruction result = this.CompilerResult.Code[cp];
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
        /// Will be invoked when the interpreter has output
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
            public int CodeEnd;
            public int Update;

            public static InstructionOffsets None => new()
            {
                CodeEnd = -1,
                Update = -1,
            };
        }

        public struct OnExecutedEventArgs
        {
            public double ElapsedMilliseconds;

            public OnExecutedEventArgs(double elapsedMilliseconds)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public override readonly string ToString() => $"Code executed in {ElapsedTime}";
            public readonly string ElapsedTime => ProgrammingLanguage.Utils.GetElapsedTime(ElapsedMilliseconds);
        }

        protected readonly Dictionary<string, ExternalFunctionBase> externalFunctions = new();

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
            BytecodeInterpreter = new BytecodeInterpreter(program, externalFunctions, settings);
            externalFunctions.SetInterpreter(BytecodeInterpreter);

            state = State.Initialized;

            OnOutput?.Invoke(this, "Start code ...", LogType.Debug);
        }

        static InstructionOffsets GetInstructionOffsets(CodeGenerator.Result compilerResult)
        {
            InstructionOffsets result = InstructionOffsets.None;

            foreach (CompiledFunction compiledFunction in compilerResult.Functions)
            {
                if (compiledFunction.CompiledAttributes.TryGetAttribute("Catch", out string value))
                {
                    if (value == "update")
                    {
                        if (!compilerResult.GetFunctionOffset(compiledFunction, out int offset))
                        { throw new CompilerException($"Function '{compiledFunction.Identifier.Content}' offset not found", compiledFunction.Identifier, compiledFunction.FilePath); }

                        result.Update = offset;
                    }
                    else if (value == "end")
                    {
                        if (!compilerResult.GetFunctionOffset(compiledFunction, out int offset))
                        { throw new CompilerException($"Function '{compiledFunction.Identifier.Content}' offset not found", compiledFunction.Identifier, compiledFunction.FilePath); }

                        result.CodeEnd = offset;
                    }
                    else
                    { throw new CompilerException("Unknown event '" + value + "'", (FunctionDefinition.Attribute)compiledFunction.Attributes.Get("Catch"), compiledFunction.FilePath); }
                }
            }

            return result;
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
            FileInfo file,
            Compiler.CompilerSettings compilerSettings,
            ParserSettings parserSettings)
        {
            OnOutput?.Invoke(this, $"Parse code: The main source code ...", LogType.Debug);

            CodeGenerator.Result codeGeneratorResult = EasyCompiler.Compile(
                file,
                externalFunctions,
                TokenizerSettings.Default,
                parserSettings,
                compilerSettings,
                (a, b) => OnOutput?.Invoke(this, a, b),
                BasePath
                ).CodeGeneratorResult;

            details.CompilerResult = codeGeneratorResult;

            if (compilerSettings.PrintInstructions)
            { codeGeneratorResult.PrintInstructions(); }

            OnOutput?.Invoke(this, "Initializing bytecode interpreter ...", LogType.Debug);

            instructionOffsets = GetInstructionOffsets(codeGeneratorResult);

            return codeGeneratorResult.Code;
        }

        /// <summary>
        /// Initializes the compiler
        /// <list type="bullet">
        /// <item>It checks if any code is running</item>
        /// <item>Adds external functions</item>
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
                { Streams[i]?.Dispose(); }
                Streams.Clear();
            }

            AddExternalFunctions();

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
            ParserSettings parserSettings,
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
            OnOutput?.Invoke(this, $"{error.GetType().Name}: {error}", LogType.Error);
            Debug.LogError(error);
        }

        void PrintException(System.Exception error)
        {
            OnOutput?.Invoke(this, $"InternalException ({error.GetType().Name}): {error.Message}", LogType.Error);
            Debug.LogError(error);
        }

        public void Destroy()
        {
            CurrentlyRunningCode = false;

            if (Streams != null)
            {
                for (int i = 0; i < Streams.Count; i++)
                {
                    Streams[i].Dispose();
                }
                Streams.Clear();
            }

            BytecodeInterpreter = null;
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
            System.Reflection.Assembly dll = System.Reflection.Assembly.LoadFile(path);
            System.Type[] exportedTypes = dll.GetExportedTypes();
            int functionsAdded = 0;

            foreach (System.Type type in exportedTypes)
            {
                System.Reflection.MethodInfo[] methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                foreach (System.Reflection.MethodInfo method in methods)
                {
                    ExternalFunctionSimple newFunction = externalFunctions.AddExternalFunction(method);
                    OnOutput?.Invoke(this, $" Added function {newFunction.ID}", LogType.Debug);
                    functionsAdded++;
                }
            }

            OnOutput?.Invoke(this, $"DLL loaded with {functionsAdded} functions", LogType.Debug);
        }

        void AddExternalFunctions()
        {
            #region Console

            externalFunctions.AddManagedExternalFunction("stdin", Array.Empty<Type>(), (DataItem[] parameters, ExternalFunctionManaged function) =>
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

            externalFunctions.AddExternalFunction("stdout", (char @char) => OnStdOut?.Invoke(this, @char.ToString()));

            externalFunctions.AddExternalFunction("console-set",
                (char @char, int x, int y) =>
                {
                    if (x < 0 || y < 0) return;
                    var (lx, ly) = Console.GetCursorPosition();
                    Console.SetCursorPosition(x, y);
                    Console.Write(@char);
                    Console.SetCursorPosition(lx, ly);
                });

            externalFunctions.AddExternalFunction("console-clear", Console.Clear);

            externalFunctions.AddExternalFunction("stderr", (char @char) => OnStdError?.Invoke(this, @char.ToString()));

            externalFunctions.AddExternalFunction("sleep", (int t) => { PauseCodeTime = t; });

            #endregion

            #region Math

            externalFunctions.AddExternalFunction("cos", (float v) => MathF.Cos(v));

            externalFunctions.AddExternalFunction("sin", (float v) => MathF.Sin(v));

            #endregion

            #region Enviroment

            externalFunctions.AddExternalFunction("time", () =>
            {
                throw new NotImplementedException();
            });

            #endregion

            #region Casts

            externalFunctions.AddExternalFunction("float-to-int",
                (float @float) =>
                { return (int)@float; });
            externalFunctions.AddExternalFunction("int-to-float",
                (int @int) =>
                { return (float)@int; });

            #endregion

            #region Streams

            externalFunctions.AddExternalFunction("stream-c",
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
            externalFunctions.AddExternalFunction("stream-d",
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
            externalFunctions.AddExternalFunction("stream-f",
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
            externalFunctions.AddExternalFunction("stream-l",
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
            externalFunctions.AddExternalFunction("stream-r",
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

            #region Win32

            externalFunctions.AddExternalFunction<int, string, string, uint, Windows.Win32.MessageBoxResult>("MessageBox", Windows.Win32.MessageBoxW);

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
            {
                bool didSomething = BytecodeInterpreter.Tick();
                if (didSomething) return;
            }
            catch (UserException error)
            {
                error.FeedDebugInfo(details.CompilerResult.DebugInfo);

                OnOutput?.Invoke(this, "User Exception: " + error.Value.ToString() + "\r\n" + error.ToString(), LogType.Error);

                OnDone?.Invoke(this, false);
                var elapsedMilliseconds = (DateTime.Now.TimeOfDay - CodeStartedTimespan).TotalMilliseconds;
                OnExecuted?.Invoke(this, new OnExecutedEventArgs(elapsedMilliseconds));
                BytecodeInterpreter = null;
                CurrentlyRunningCode = false;

                if (!HandleErrors) throw;
            }
            catch (RuntimeException error)
            {
                error.FeedDebugInfo(details.CompilerResult.DebugInfo);

                OnOutput?.Invoke(this, "Runtime Exception: " + error.ToString(), LogType.Error);

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

            if (BytecodeInterpreter == null || !BytecodeInterpreter.IsDone) return;

            if (instructionOffsets.Update != -1)
            {
                state = State.CallUpdate;
                WaitForUpdates(10, () => BytecodeInterpreter?.Call(instructionOffsets.Update, null));
                return;
            }

            if (instructionOffsets.CodeEnd != -1 && !exitCalled)
            {
                state = State.CallCodeEnd;
                OnOutput?.Invoke(this, "Call CodeEnd", LogType.Debug);

                exitCalled = true;
                BytecodeInterpreter?.Call(instructionOffsets.CodeEnd, null);
                return;
            }

            OnCodeExecuted();
        }
    }
}
