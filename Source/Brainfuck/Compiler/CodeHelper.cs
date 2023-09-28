
#nullable enable

using ProgrammingLanguage.Core;

using System;
using System.Diagnostics;
using TheProgram.Brainfuck;

namespace ProgrammingLanguage.Brainfuck
{
    readonly struct AutoPrintCodeString
    {
        readonly string v;

        public AutoPrintCodeString(string v)
        {
            this.v = v;
        }

        public static implicit operator string(AutoPrintCodeString v) => v.v;
        public static implicit operator AutoPrintCodeString(string v) => new(v);

        public static AutoPrintCodeString operator +(AutoPrintCodeString a, string b)
        {
            string newString = a.v + b;
            ProgramUtils.PrintCode(b);
            return new AutoPrintCodeString(newString);
        }
        public static AutoPrintCodeString operator +(AutoPrintCodeString a, char b) => a + b.ToString();
        public static AutoPrintCodeString operator +(AutoPrintCodeString a, AutoPrintCodeString b) => a + b.v;
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal class CompiledCode
    {
        const int HALF_BYTE = byte.MaxValue / 2;

        internal string Code;

        int indent;
        int pointer;

        public int Pointer => pointer;

        internal CompiledCode()
        {
            this.Code = "";
            this.indent = 0;
            this.pointer = 0;
        }

        #region Comments

        internal int Indent(int indent)
        {
            this.indent += indent;
            return this.indent;
        }
        internal void LineBreak()
        {
            Code += "\r\n";
            Code += new string(' ', indent);
        }
        internal void CommentLine(string text)
        {
            Code += "\r\n";
            Code += new string(' ', indent);
            Code += Utils.ReplaceCodes(text, '_');
            Code += "\r\n";
            Code += new string(' ', indent);
        }
        internal void StartBlock()
        {
            Code += "\r\n";
            Code += new string(' ', indent);
            this.Code += "{";
            this.indent += 2;
            Code += "\r\n";
            Code += new string(' ', indent);
        }
        internal void StartBlock(string label)
        {
            Code += "\r\n";
            Code += new string(' ', indent);
            this.Code += $"{Utils.ReplaceCodes(label, '_')} {{";
            this.indent += 2;
            Code += "\r\n";
            Code += new string(' ', indent);
        }
        internal void EndBlock()
        {
            this.indent -= 2;
            Code += "\r\n";
            Code += new string(' ', indent);
            this.Code += "}";
            Code += "\r\n";
            Code += new string(' ', indent);
        }
        internal CodeBlock Block()
        {
            this.StartBlock();
            return new CodeBlock(this);
        }
        internal CodeBlock Block(string label)
        {
            this.StartBlock(label);
            return new CodeBlock(this);
        }

        #endregion

        /// <summary>
        /// <b>Requires 1 more cell to the right of the <paramref name="target"/>!</b><br/>
        /// <b>Pointer:</b> <paramref name="target"/> + 1
        /// </summary>
        internal void CopyValue(int source, int target)
            => CopyValueWithTemp(source, target + 1, target);
        /// <summary>
        /// <b>Requires 1 more cell to the right of the <paramref name="targets"/>!</b><br/>
        /// <b>Pointer:</b> The last target + 1 or not modified
        /// </summary>
        internal void CopyValue(int source, params int[] targets)
        {
            if (targets.Length == 0) return;
            for (int i = 0; i < targets.Length; i++)
            { CopyValue(source, targets[i]); }
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="tempAddress"/>
        /// </summary>
        internal void CopyValueWithTemp(int source, int tempAddress, int target)
        {
            MoveValue(source, target, tempAddress);
            MoveAddValue(tempAddress, source);
        }
        /// <summary>
        /// <b>Pointer:</b> <paramref name="tempAddress"/> or not modified
        /// </summary>
        internal void CopyValueWithTemp(int source, int tempAddress, params int[] targets)
        {
            if (targets.Length == 0) return;
            for (int i = 0; i < targets.Length; i++)
            { CopyValueWithTemp(source, tempAddress, targets[i]); }
        }

        internal void SetPointer(int address) => MovePointer(address - pointer);

        internal void MovePointer(int offset)
        {
            if (offset < 0)
            {
                for (int i = 0; i < (-offset); i++)
                {
                    Code += "<";
                    pointer--;
                }
                return;
            }
            if (offset > 0)
            {
                for (int i = 0; i < offset; i++)
                {
                    Code += ">";
                    pointer++;
                }
                return;
            }
        }

        /// <summary>
        /// <b>Pointer:</b> Not modified
        /// </summary>
        internal void AddValue(int value)
        {
            if (value < 0)
            {
                for (int i = 0; i < (-value); i++)
                {
                    Code += "-";
                }
                return;
            }
            if (value > 0)
            {
                for (int i = 0; i < value; i++)
                {
                    Code += "+";
                }
                return;
            }
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="address"/>
        /// </summary>
        internal void AddValue(int address, int value)
        {
            SetPointer(address);
            AddValue(value);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="address"/>
        /// </summary>
        internal void SetValue(int address, char value)
            => SetValue(address, CharCode.GetByte(value));

        /// <summary>
        /// <b>Pointer:</b> <paramref name="address"/>
        /// </summary>
        internal void SetValue(int address, int value)
        {
            SetPointer(address);
            ClearCurrent();

            if (value > HALF_BYTE)
            {
                value -= 256;
            }

            AddValue(value);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="address"/>
        /// </summary>
        /// <exception cref="Errors.CompilerException"/>
        /// <exception cref="Errors.ImpossibleException"/>
        internal void SetValue(int address, Bytecode.DataItem value)
        {
            switch (value.Type)
            {
                case Bytecode.RuntimeType.BYTE:
                    SetValue(address, value.ValueByte);
                    return;
                case Bytecode.RuntimeType.INT:
                    SetValue(address, value.ValueInt);
                    return;
                case Bytecode.RuntimeType.FLOAT:
                    throw new Errors.NotSupportedException($"Floats not supported by brainfuck :(");
                case Bytecode.RuntimeType.CHAR:
                    SetValue(address, value.ValueChar);
                    return;
                default:
                    throw new Errors.ImpossibleException();
            }
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="address"/>
        /// </summary>
        internal void ClearValue(int address)
        {
            SetPointer(address);
            ClearCurrent();
        }

        /// <summary>
        /// <b>Pointer:</b> Last of <paramref name="addresses"/>
        /// </summary>
        internal void ClearValue(params int[] addresses)
        {
            for (int i = 0; i < addresses.Length; i++)
            { ClearValue(addresses[i]); }
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="from"/>
        /// </summary>
        internal void MoveValue(int from, params int[] to)
        {
            CommentLine($"Clear the destination ({string.Join("; ", to)}) :");
            for (int i = 0; i < to.Length; i++)
            { ClearValue(to[i]); }

            CommentLine($"Move the value (from {from}):");
            MoveAddValue(from, to);
        }
        /// <summary>
        /// <b>Pointer:</b> <paramref name="from"/>
        /// </summary>
        internal void MoveAddValue(int from, params int[] to)
        {
            this.JumpStart(from);
            this.AddValue(from, -1);

            for (int i = 0; i < to.Length; i++)
            { AddValue(to[i], 1); }

            this.JumpEnd(from);
        }
        /// <summary>
        /// <b>Pointer:</b> <paramref name="from"/>
        /// </summary>
        internal void MoveSubValue(int from, params int[] to)
        {
            this.JumpStart(from);
            this.AddValue(from, -1);

            for (int i = 0; i < to.Length; i++)
            { AddValue(to[i], -1); }

            this.JumpEnd(from);
        }
        /// <summary>
        /// <b>Pointer:</b> Not modified
        /// </summary>
        internal void ClearCurrent()
        {
            Code += "[-]";
        }

        internal void ClearRange(int start, int size)
        {
            for (int offset = 0; offset < size; offset++)
            { this.ClearValue(start + offset); }
            this.SetPointer(start);
        }

        /// <summary>
        /// <b>Pointer:</b> 0
        /// </summary>
        internal void JumpStart(int conditionAddress)
        {
            this.SetPointer(conditionAddress);
            this.Code += "[";
            this.SetPointer(0);
        }

        /// <summary>
        /// <b>Pointer:</b> 0
        /// </summary>
        internal void JumpEnd(int conditionAddress, bool clearCondition = false)
        {
            this.SetPointer(conditionAddress);
            if (clearCondition) this.ClearCurrent();
            this.Code += "]";
            this.SetPointer(0);
        }

        public static CompiledCode operator +(CompiledCode a, string b)
        {
            a.Code += b;
            return a;
        }

        public override int GetHashCode() => HashCode.Combine(Code);
        public override string ToString()
        {
            string result = Code;

            while (true)
            {
                if (result.Contains("\r\n\r\n"))
                { result = result.Replace("\r\n\r\n", "\r\n"); }

                if (result.Contains(" \r\n"))
                { result = result.Replace(" \r\n", "\r\n"); }

                else break;
            }

            return result;
        }

        /// <summary>
        /// <b>Try not to use this</b>
        /// </summary>
        internal void FixPointer(int pointer)
        {
            this.pointer = pointer;
        }

        string GetDebuggerDisplay()
            => $"{{{nameof(CompiledCode)}}}";

        /// <summary>
        /// <b>POINTER MISMATCH</b>
        /// </summary>
        internal void FindZeroRight(int step = 1)
        {
            if (step == 0) throw new ArgumentException("Must be nonzero", nameof(step));
            int _step = Math.Abs(step);
            char _code = (step < 0) ? '<' : '>';

            Code += $"[{new string(_code, _step)}]";
        }

        /// <summary>
        /// <b>POINTER MISMATCH</b>
        /// </summary>
        internal void FindZeroLeft(int step = 1)
            => FindZeroRight(-step);

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void CopyValueWithTempUnsafe(int source, int tempAddress, int target)
        {
            MoveValueUnsafe(source, target, tempAddress);
            MoveAddValueUnsafe(tempAddress, source);
        }
        /// <summary>
        /// <b>Pointer:</b> Restored to the last state or not modified
        /// </summary>
        internal void CopyValueWithTempUnsafe(int source, int tempAddress, params int[] targets)
        {
            if (targets.Length == 0) return;
            for (int i = 0; i < targets.Length; i++)
            { CopyValueWithTempUnsafe(source, tempAddress, targets[i]); }
        }

        internal void MovePointerUnsafe(int offset)
        {
            if (offset < 0)
            { Code += new string('<', -offset); }
            else if (offset > 0)
            { Code += new string('>', offset); }
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void AddValueUnsafe(int offset, int value)
        {
            MovePointerUnsafe(offset);
            AddValue(value);
            MovePointerUnsafe(-offset);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void SetValueUnsafe(int address, char value)
            => SetValueUnsafe(address, CharCode.GetByte(value));

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void SetValueUnsafe(int offset, int value)
        {
            MovePointer(offset);

            ClearCurrent();

            if (value > HALF_BYTE)
            { value -= 256; }

            AddValue(value);

            MovePointer(-offset);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void ClearValueUnsafe(int offset)
        {
            MovePointerUnsafe(offset);
            ClearCurrent();
            MovePointerUnsafe(-offset);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void ClearValueUnsafe(params int[] addresses)
        {
            for (int i = 0; i < addresses.Length; i++)
            { ClearValueUnsafe(addresses[i]); }
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void MoveValueUnsafe(int from, params int[] to)
            => MoveValueUnsafe(from, true, to);

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void MoveValueUnsafe(int from, bool clearDestination, params int[] to)
        {
            if (clearDestination)
            {
                CommentLine($"Clear the destination ({string.Join("; ", to)}) :");
                for (int i = 0; i < to.Length; i++)
                { ClearValueUnsafe(to[i]); }
            }

            CommentLine($"Move the value (from {from}):");
            MoveAddValueUnsafe(from, to);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void MoveAddValueUnsafe(int from, params int[] to)
        {
            this.JumpStartUnsafe(from);
            this.AddValueUnsafe(from, -1);

            for (int i = 0; i < to.Length; i++)
            { AddValueUnsafe(to[i], 1); }

            this.JumpEndUnsafe(from);
        }
        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void MoveSubValueUnsafe(int from, params int[] to)
        {
            this.JumpStartUnsafe(from);
            this.AddValueUnsafe(from, -1);

            for (int i = 0; i < to.Length; i++)
            { AddValueUnsafe(to[i], -1); }

            this.JumpEndUnsafe(from);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void JumpStartUnsafe(int conditionOffset)
        {
            this.MovePointerUnsafe(conditionOffset);
            this.Code += "[";
            this.MovePointerUnsafe(-conditionOffset);
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal void JumpEndUnsafe(int conditionOffset)
        {
            this.MovePointerUnsafe(conditionOffset);
            this.Code += "]";
            this.MovePointerUnsafe(-conditionOffset);
        }

        internal string GetFinalCode()
        {
            return Minifier.Minify(Utils.RemoveNoncodes(Code));
        }
    }

    internal readonly struct CodeBlock : IDisposable
    {
        readonly CompiledCode reference;

        internal CodeBlock(CompiledCode reference)
        {
            this.reference = reference;
        }

        public void Dispose()
        {
            this.reference.EndBlock();
        }
    }

    internal class StackCodeHelper
    {
        readonly CompiledCode Code;
        readonly Stack<int> TheStack;

        /// <summary>
        /// Adds up all the stack element's size
        /// </summary>
        internal int Size => TheStack.Sum();
        internal readonly int Start;
        internal readonly int MaxSize;

        internal int NextAddress => Start + TheStack.Sum();

        internal int LastAddress
        {
            get
            {
                if (TheStack.Count == 0) return Start;
                return Start + TheStack.Sum() - TheStack[^1];
            }
        }

        internal StackCodeHelper(CompiledCode code, int start, int size)
        {
            this.Code = code;
            this.TheStack = new Stack<int>();
            this.Start = start;
            this.MaxSize = size;
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal int Push(Bytecode.DataItem v)
        {
            return v.Type switch
            {
                Bytecode.RuntimeType.BYTE => Push(v.ValueByte),
                Bytecode.RuntimeType.INT => Push(v.ValueInt),
                Bytecode.RuntimeType.FLOAT => throw new Errors.NotSupportedException("Floats are not supported by the brainfuck compiler"),
                Bytecode.RuntimeType.CHAR => Push(v.ValueChar),
                _ => throw new Errors.ImpossibleException(),
            };
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal int Push(int v)
        {
            int address = PushVirtual(1);

            if (Size > MaxSize)
            { Code.PRINT(address, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Stack overflow")}\n"); }

            Code.SetValue(address, v);
            return address;
        }

        /// <summary>
        /// <b>Pointer:</b> Restored to the last state
        /// </summary>
        internal int Push(char v)
        {
            int address = PushVirtual(1);

            if (Size > MaxSize)
            { Code.PRINT(address, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Stack overflow")}\n"); }

            Code.SetValue(address, v);
            return address;
        }

        /// <summary>
        /// <b>Pointer:</b> 0
        /// </summary>
        internal int Push(string v)
        {
            if (v is null)
            { throw new ArgumentNullException(nameof(v)); }

            int size = v.Length;
            int address = PushVirtual(size);

            if (Size > MaxSize)
            { Code.PRINT(address, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Stack overflow")}\n"); }

            for (int i = 0; i < size; i++)
            {
                Code.SetValue(address + i, v[i]);
            }

            Code.SetPointer(0);
            return address;
        }

        internal int PushVirtual(int size)
        {
            int address = NextAddress;

            if (Size >= MaxSize)
            { Code.PRINT(address, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Stack overflow")}\n"); }

            TheStack.Push(size);
            return address;
        }

        /// <summary>
        /// <b>Pointer:</b> Last state or 0
        /// </summary>
        internal int PopAndStore(int target)
        {
            int size = PopVirtual();
            int address = NextAddress;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedSource = address + offset;
                int offsettedTarget = target + offset;
                Code.MoveValue(offsettedSource, offsettedTarget);
            }
            return size;
        }

        /// <summary>
        /// <b>Pointer:</b> Last state or 0
        /// </summary>
        internal int PopAndAdd(int target)
        {
            int size = PopVirtual();
            int address = NextAddress;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedSource = address + offset;
                int offsettedTarget = target + offset;
                Code.MoveAddValue(offsettedSource, offsettedTarget);
            }
            return size;
        }

        /// <summary>
        /// <b>Pointer:</b> Not modified
        /// </summary>
        internal int Pop(Action<int> onAddress)
        {
            int size = PopVirtual();
            int address = NextAddress;
            for (int offset = 0; offset < size; offset++)
            {
                int offsettedAddress = address + offset;
                onAddress?.Invoke(offsettedAddress);
            }
            return size;
        }

        /// <summary>
        /// <b>Pointer:</b> Not modified or restored to the last state
        /// </summary>
        internal void Pop()
        {
            int size = PopVirtual();
            int address = NextAddress;
            for (int offset = 0; offset < size; offset++)
            {
                Code.ClearValue(address + offset);
            }
        }

        internal int PopVirtual() => TheStack.Pop();
    }

    internal class BasicHeapCodeHelper
    {
        CompiledCode Code;

        internal readonly int Start;
        internal readonly int Size;

        int OffsettedStart => Start + BLOCK_SIZE;

        const int BLOCK_SIZE = 3;
        const int OFFSET_ADDRESS_CARRY = 0;
        const int OFFSET_VALUE_CARRY = 1;
        const int OFFSET_DATA = 2;

        public BasicHeapCodeHelper(CompiledCode code, int start, int size)
        {
            Code = code;
            Start = start;
            Size = size;
        }

        /*
         *  LAYOUT:
         *  START 0 0 (a c v) (a c v) ...
         *  a: Carrying address
         *  c: Carrying value
         *  v: Value
         */

        /// <summary>
        /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OffsettedStart</c>
        /// </summary>
        void GoBack()
        {
            // Go back
            Code += "[";
            Code.ClearCurrent();
            Code.MovePointerUnsafe(-BLOCK_SIZE);
            Code += "]";

            // Fix overshoot
            Code.MovePointerUnsafe(BLOCK_SIZE);
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OffsettedStart</c>
        /// </summary>
        void CarryBack()
        {
            // Go back
            Code += "[";
            Code.ClearCurrent();
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_VALUE_CARRY - BLOCK_SIZE);
            Code.MovePointerUnsafe(-BLOCK_SIZE);
            Code += "]";

            // Fix overshoot
            Code.MovePointerUnsafe(BLOCK_SIZE);
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// </summary>
        void GoTo()
        {
            // Condition on carrying address
            Code += "[";

            // Copy the address and leave 1 behind
            Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
            Code.AddValue(1);

            // Move to the next block
            Code.MovePointerUnsafe(BLOCK_SIZE);

            // Decrement 1 and check if zero
            //   Yes => Destination reached -> leave 1
            //   No => Repeat
            Code += "- ] +";
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// </summary>
        void CarryTo()
        {
            // Condition on carrying address
            Code += "[";

            // Copy the address and leave 1 behind
            Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
            Code.AddValue(1);

            // Copy the value
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, false, BLOCK_SIZE + OFFSET_VALUE_CARRY);

            // Move to the next block
            Code.MovePointerUnsafe(BLOCK_SIZE);

            // Decrement 1 and check if zero
            //   Yes => Destination reached -> leave 1
            //   No => Repeat
            Code += "- ] +";
        }

        internal void Init()
        {
            using (Code.Block("Initialize HEAP"))
            {
                Code.SetValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_ADDRESS_CARRY, 1);
                Code.SetPointer(0);
            }
        }

        internal void Destroy()
        {
            using (Code.Block("Destroy HEAP"))
            {
                Code.ClearValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_ADDRESS_CARRY);
                Code.SetPointer(0);
            }
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Set(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, true, OFFSET_DATA);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Add(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveAddValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Subtract(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveSubValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="resultAddress"/>
        /// </summary>
        internal void Get(int pointerAddress, int resultAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);

            Code.SetPointer(OffsettedStart);

            GoTo();

            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 0);
            Code.CopyValueWithTempUnsafe(OFFSET_DATA, OFFSET_ADDRESS_CARRY, OFFSET_VALUE_CARRY);
            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            CarryBack();

            Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
            Code.SetPointer(resultAddress);
        }
    }

    internal class HeapCodeHelper
    {
        CompiledCode Code;

        internal readonly int Start;
        internal readonly int Size;

        int OffsettedStart => Start + BLOCK_SIZE;

        const int BLOCK_SIZE = 4;
        const int OFFSET_ADDRESS_CARRY = 0;
        const int OFFSET_VALUE_CARRY = 1;
        const int OFFSET_STATUS = 2;
        const int OFFSET_DATA = 3;

        public HeapCodeHelper(CompiledCode code, int start, int size)
        {
            Code = code;
            Start = start;
            Size = size;
        }

        /*
         *  LAYOUT:
         *  START 0 0 (a c s v) (a c s v) ...
         *  a: Carrying address
         *  c: Carrying value
         *  s: Status
         *  v: Value
         */

        /// <summary>
        /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OffsettedStart</c>
        /// </summary>
        void GoBack()
        {
            // Go back
            Code += "[";
            Code.ClearCurrent();
            Code.MovePointerUnsafe(-BLOCK_SIZE);
            Code += "]";

            // Fix overshoot
            Code.MovePointerUnsafe(BLOCK_SIZE);
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OffsettedStart</c>
        /// </summary>
        void CarryBack()
        {
            // Go back
            Code += "[";
            Code.ClearCurrent();
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_VALUE_CARRY - BLOCK_SIZE);
            Code.MovePointerUnsafe(-BLOCK_SIZE);
            Code += "]";

            // Fix overshoot
            Code.MovePointerUnsafe(BLOCK_SIZE);
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// </summary>
        void GoTo()
        {
            // Condition on carrying address
            Code += "[";

            // Copy the address and leave 1 behind
            Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
            Code.AddValue(1);

            // Move to the next block
            Code.MovePointerUnsafe(BLOCK_SIZE);

            // Decrement 1 and check if zero
            //   Yes => Destination reached -> leave 1
            //   No => Repeat
            Code += "- ] +";
        }

        /// <summary>
        /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
        /// <br/>
        /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
        /// </summary>
        void CarryTo()
        {
            // Condition on carrying address
            Code += "[";

            // Copy the address and leave 1 behind
            Code.MoveValueUnsafe(OFFSET_ADDRESS_CARRY, false, BLOCK_SIZE + OFFSET_ADDRESS_CARRY);
            Code.AddValue(1);

            // Copy the value
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, false, BLOCK_SIZE + OFFSET_VALUE_CARRY);

            // Move to the next block
            Code.MovePointerUnsafe(BLOCK_SIZE);

            // Decrement 1 and check if zero
            //   Yes => Destination reached -> leave 1
            //   No => Repeat
            Code += "- ] +";
        }

        internal void Init()
        {
            using (Code.Block("Initialize HEAP"))
            {
                Code.SetValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_ADDRESS_CARRY, 1);
                Code.SetValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_STATUS, 1);
                Code.SetPointer(0);
            }
        }

        internal void Destroy()
        {
            using (Code.Block("Destroy HEAP"))
            {
                Code.ClearValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_ADDRESS_CARRY);
                Code.ClearValue(OffsettedStart + (BLOCK_SIZE * Size) + OFFSET_STATUS);
                Code.SetPointer(0);
            }
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Set(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, true, OFFSET_DATA);

            // Set status to 1
            Code.SetValueUnsafe(OFFSET_STATUS, 1);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Add(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveAddValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

            // Set status to 1
            Code.SetValueUnsafe(OFFSET_STATUS, 1);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Subtract(int pointerAddress, int valueAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);
            Code.MoveValue(valueAddress, OffsettedStart + 1);

            Code.SetPointer(OffsettedStart);

            CarryTo();

            // Copy the carried value to the address
            Code.MoveSubValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_DATA);

            // Set status to 1
            Code.SetValueUnsafe(OFFSET_STATUS, 1);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <see cref="OffsettedStart"/>
        /// </summary>
        internal void Free(int pointerAddress)
        {
            Code.ClearValue(OffsettedStart);

            Code.MoveValue(pointerAddress, OffsettedStart);

            Code.SetPointer(OffsettedStart);

            GoTo();

            // Set the value
            Code.ClearValueUnsafe(OFFSET_DATA);

            // Set status to 0
            Code.SetValueUnsafe(OFFSET_STATUS, 0);

            GoBack();
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="resultAddress"/>
        /// </summary>
        internal void Allocate(int resultAddress)
        {
            Code.SetPointer(OffsettedStart);

            Code.JumpStartUnsafe(OFFSET_STATUS);

            // Out of memory check
            Code.JumpStartUnsafe(OFFSET_ADDRESS_CARRY);
            Code.PRINT_UNSAFE(OFFSET_ADDRESS_CARRY, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Not enough of memory")}\n");
            Code.JumpEndUnsafe(OFFSET_ADDRESS_CARRY);

            // Increment address carry
            Code.AddValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            // Increment result carry & carry (bruh)
            Code.AddValueUnsafe(OFFSET_VALUE_CARRY, 1);
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_VALUE_CARRY + BLOCK_SIZE);

            Code.MovePointerUnsafe(BLOCK_SIZE);

            Code.JumpEndUnsafe(OFFSET_STATUS);

            Code.AddValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            // Set status to 1
            Code.SetValueUnsafe(OFFSET_STATUS, 1);

            CarryBack();

            Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
            Code.SetPointer(resultAddress);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="resultAddress"/>
        /// </summary>
        internal void AllocateFrom(int resultAddress, int startSearchAddress)
        {
            Code.MoveValue(startSearchAddress, OffsettedStart);

            Code.SetPointer(OffsettedStart);

            this.GoTo();

            Code.JumpStartUnsafe(OFFSET_STATUS);

            // Out of memory check
            Code.JumpStartUnsafe(OFFSET_ADDRESS_CARRY);
            Code.PRINT_UNSAFE(OFFSET_ADDRESS_CARRY, $"\n{ANSI.Generator.Generate(ANSI.ForegroundColor.RED, "Not enough of memory")}\n");
            Code.JumpEndUnsafe(OFFSET_ADDRESS_CARRY);

            // Increment address carry
            Code.AddValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            // Increment result carry & carry (bruh)
            Code.AddValueUnsafe(OFFSET_VALUE_CARRY, 1);
            Code.MoveValueUnsafe(OFFSET_VALUE_CARRY, OFFSET_VALUE_CARRY + BLOCK_SIZE);

            Code.MovePointerUnsafe(BLOCK_SIZE);

            Code.JumpEndUnsafe(OFFSET_STATUS);

            Code.AddValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            // Set status to 1
            Code.SetValueUnsafe(OFFSET_STATUS, 1);

            CarryBack();

            Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
            Code.SetPointer(resultAddress);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="resultAddress"/>
        /// </summary>
        internal void Get(int pointerAddress, int resultAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);

            Code.SetPointer(OffsettedStart);

            GoTo();

            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 0);
            Code.CopyValueWithTempUnsafe(OFFSET_DATA, OFFSET_ADDRESS_CARRY, OFFSET_VALUE_CARRY);
            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            CarryBack();

            Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
            Code.SetPointer(resultAddress);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="resultAddress"/>
        /// </summary>
        internal void GetStatus(int pointerAddress, int resultAddress)
        {
            Code.ClearValue(OffsettedStart, OffsettedStart + 1);

            Code.MoveValue(pointerAddress, OffsettedStart);

            Code.SetPointer(OffsettedStart);

            GoTo();

            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 0);
            Code.CopyValueWithTempUnsafe(OFFSET_STATUS, OFFSET_ADDRESS_CARRY, OFFSET_VALUE_CARRY);
            Code.SetValueUnsafe(OFFSET_ADDRESS_CARRY, 1);

            CarryBack();

            Code.MoveValue(Start + OFFSET_VALUE_CARRY, resultAddress);
            Code.SetPointer(resultAddress);
        }

        internal void Allocate(int resultAddress, int requiredSizeAddress, int tempAddressesStart)
        {
            /*
	            int result = Alloc();
	            int i = size;
	            while (i) {
		            i--;

		            int result2 = Alloc(result);
                    int tempAddress3 = (result2 - 1 != result);
		            if (tempAddress3) {
			            Free(result);
			            Free(result2);
			            result = result2;
			            i = size;
		            }
	            };
	            return result;
             */

            int intermediateResultAddress = tempAddressesStart + 1;
            int i = tempAddressesStart;

            tempAddressesStart += 2;

            // int result = Alloc();
            Allocate(resultAddress);

            // int i = size;
            Code.CopyValueWithTemp(requiredSizeAddress, tempAddressesStart, i);

            // i--;
            Code.AddValue(i, -1);

            // while (i) {
            Code.JumpStart(i);

            {
                int copiedResult = tempAddressesStart;
                int temp = tempAddressesStart + 1;

                // int result2 = Alloc(result);
                Code.CopyValueWithTemp(resultAddress, temp, copiedResult);
                this.AllocateFrom(intermediateResultAddress, copiedResult);

                Code.ClearValue(temp);
                Code.ClearValue(copiedResult);
            }

            {
                // if (result2 - 1 != result) {

                int copiedResult = tempAddressesStart + 1;
                int resultSub1 = tempAddressesStart; 
                int temp = tempAddressesStart + 2;

                Code.CopyValueWithTemp(resultAddress, temp, copiedResult);

                Code.CopyValueWithTemp(intermediateResultAddress, temp, resultSub1);
                Code.AddValue(resultSub1, -1);

                Code.MoveSubValue(copiedResult, resultSub1);

                Code.ClearValue(copiedResult);
                Code.ClearValue(temp);

                Code.JumpStart(resultSub1);
                Code.ClearValue(resultSub1);
            }

            {
                int temp = tempAddressesStart;

                // delete result;
                this.Free(resultAddress);

                // result = result2;
                Code.CopyValueWithTemp(intermediateResultAddress, temp, resultAddress);

                // delete result2;
                this.Free(intermediateResultAddress);

                // i = size;
                Code.CopyValueWithTemp(requiredSizeAddress, temp, i);

                Code.ClearValue(temp);
            }

            {
                int temp = tempAddressesStart;
                // }
                Code.ClearValue(temp);
                Code.JumpEnd(temp);
            }

            // i--;
            Code.AddValue(i, -1);

            // }
            Code.JumpEnd(i);
        }
    }
}
