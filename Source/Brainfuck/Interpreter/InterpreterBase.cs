using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LanguageCore.Brainfuck.Generator;

namespace LanguageCore.Brainfuck
{
    public delegate void OutputCallback(byte data);
    public delegate byte InputCallback();

    public abstract class InterpreterBase
    {
        public const int MEMORY_SIZE = 1024;

        public byte[] Memory;

        protected OutputCallback OnOutput;
        protected InputCallback OnInput;

        protected int codePointer;
        protected int memoryPointer;

        public int CodePointer => codePointer;
        public int MemoryPointer => memoryPointer;

        protected bool isPaused;

        public bool IsPaused => isPaused;

        public RuntimeContext CurrentContext => new(memoryPointer, codePointer);

        public static void OnDefaultOutput(byte data) => Console.Write(CharCode.GetChar(data));
        public static byte OnDefaultInput() => CharCode.GetByte(Console.ReadKey(true).KeyChar);

        public InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null)
        {
            Memory = new byte[MEMORY_SIZE];

            OnOutput = onOutput ?? OnDefaultOutput;
            OnInput = onInput ?? OnDefaultInput;

            codePointer = 0;
            memoryPointer = 0;
            isPaused = false;
        }

        public abstract bool Step();

        public void Run()
        { while (Step()) ; }

        public void Run(int stepsBeforeSleep)
        {
            int step = 0;
            while (true)
            {
                if (!Step()) break;
                step++;
                if (step >= stepsBeforeSleep)
                {
                    step = 0;
                    Thread.Sleep(10);
                }
            }
        }

        public void Reset()
        {
            codePointer = 0;
            memoryPointer = 0;
            Array.Clear(Memory);
        }

        public byte[] GetRawHeap(BrainfuckGeneratorSettings settings)
        {
            int heapStart = BasicHeapCodeHelper.GetOffsettedStart(settings.HeapStart);
            // int heapEnd = brainfuckGeneratorSettings.HeapStart + brainfuckGeneratorSettings.HeapSize * BasicHeapCodeHelper.BLOCK_SIZE;

            byte[] result = new byte[(Memory.Length - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE];

            for (int i = heapStart; i < Memory.Length; i += BasicHeapCodeHelper.BLOCK_SIZE)
            {
                // byte addressCarry = Memory[i + BasicHeapCodeHelper.OFFSET_ADDRESS_CARRY];
                // byte valueCarry = Memory[i + BasicHeapCodeHelper.OFFSET_VALUE_CARRY];
                byte data = Memory[i + BasicHeapCodeHelper.OFFSET_DATA];

                int heapAddress = (i - heapStart) / BasicHeapCodeHelper.BLOCK_SIZE;
                result[heapAddress] = data;
            }

            return result;
        }
    }

    public abstract class InterpreterBase<TCode> : InterpreterBase
    {
        protected TCode[] Code;

        public bool OutOfCode => codePointer >= Code.Length || codePointer < 0;

        public InterpreterBase(Uri uri, OutputCallback? onOutput = null, InputCallback? onInput = null)
            : this(onOutput, onInput)
        {
            using System.Net.Http.HttpClient client = new();
            client.GetStringAsync(uri).ContinueWith((code) =>
            {
                Code = ParseCode(code.Result);
            }, System.Threading.Tasks.TaskScheduler.Default).Wait();
        }

        public InterpreterBase(FileInfo file, OutputCallback? onOutput = null, InputCallback? onInput = null)
            : this(File.ReadAllText(file.FullName), onOutput, onInput)
        {

        }

        public InterpreterBase(string code, OutputCallback? onOutput = null, InputCallback? onInput = null)
            : this(onOutput, onInput)
        {
            Code = ParseCode(code);
        }

        public InterpreterBase(OutputCallback? onOutput = null, InputCallback? onInput = null) : base(onOutput, onInput)
        {
            Code = Array.Empty<TCode>();
        }

        protected abstract TCode[] ParseCode(string code);

        public override bool Step()
        {
            if (OutOfCode) return false;

            Evaluate(Code[codePointer]);

            codePointer++;
            return !OutOfCode;
        }

        protected abstract void Evaluate(TCode instruction);
    }
}
