using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class StepList<T>
    {
        int Position = 0;
        readonly T[] Values = null;

        internal StepList(T[] values, int startIndex)
        {
            this.Values = values;
            this.Position = startIndex;
        }
        internal StepList(List<T> values, int startIndex) : this(values.ToArray(), startIndex) { }

        internal StepList(T[] values) : this(values, 0) { }
        internal StepList(List<T> values) : this(values.ToArray(), 0) { }

        internal T Next() => Values[Position++];
        internal T[] Next(int n)
        {
            T[] result = new T[n];
            for (int i = 0; i < n; i++) result[i] = Next();
            return result;
        }
        internal bool End() => Position >= Values.Length;
        internal void Reset() => Position = 0;
    }

    internal class DataStack : Stack<DataItem>
    {
        internal int UsedVirtualMemory
        {
            get
            {
                static int CalculateItemSize(DataItem item) => item.type switch
                {
                    RuntimeType.INT => 4,
                    RuntimeType.FLOAT => 4,
                    RuntimeType.BOOLEAN => 1,
                    _ => throw new NotImplementedException(),
                };

                int result = 0;
                for (int i = 0; i < Count; i++)
                { result += CalculateItemSize(this[i]); }
                return result;
            }
        }

        internal BytecodeProcessor processor;

        public DataStack(BytecodeProcessor processor) => this.processor = processor;

        public void Destroy() => base.Clear();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public override DataItem Pop()
        {
            DataItem val = this[^1];
            RemoveAt(Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public override void Push(DataItem value)
        {
            var item = value;
            item.stack = this;
            item.heap = this.processor.Memory.Heap;
            base.Push(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(DataItem value, string tag)
        {
            var item = value;
            item.stack = this;
            item.heap = this.processor.Memory.Heap;
            item.Tag = tag;
            base.Push(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(int value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(float value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(bool value, string tag = null) => Push(new DataItem(value, tag));
        /// <summary>Adds a list to the end</summary>
        public override void PushRange(List<DataItem> list) => PushRange(list.ToArray());
        /// <summary>Adds an array to the end</summary>
        public override void PushRange(DataItem[] list)
        { foreach (DataItem item in list) Push(item); }
        /// <summary>Adds a list to the end</summary>
        public void PushRange(DataItem[] list, string tag)
        {
            DataItem[] newList = new DataItem[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                DataItem item = list[i];
                item.Tag = tag ?? item.Tag;
                newList[i] = item;
            }
            PushRange(newList);
        }
        /// <summary>Sets a specific item's value</summary>
        public void Set(int index, DataItem val, bool overrideTag = false)
        {
            DataItem item = val;
            item.stack = this;
            item.heap = this.processor.Memory.Heap;
            if (!overrideTag)
            {
                item.Tag = this[index].Tag;
            }
            this[index] = item;
        }
    }
    internal class HEAP
    {
        readonly DataItem[] heap;

        internal HEAP(int size = 0)
        {
            this.heap = new DataItem[size];
        }

        internal int Size => this.heap.Length;
        internal DataItem this[int i]
        {
            get
            {
                if (i < 0) throw new RuntimeException($"Null pointer!");
                if (i >= heap.Length) throw new RuntimeException($"Pointer points ouf of memory bounds. Possibly out of HEAP memory.");
                return heap[i];
            }
            set
            {
                if (i < 0) throw new RuntimeException($"Null pointer!");
                if (i >= heap.Length) return; // throw new RuntimeException($"Pointer points ouf of memory bounds. Possibly out of HEAP memory.");
                heap[i] = value;
            }
        }

        internal DataItem[] ToArray() => heap.ToList().ToArray();

        internal string GetString(int start, int length)
        {
            int end = start + length;
            string result = "";
            for (int i = start; i < end; i++)
            {
                if (this[i].type != RuntimeType.CHAR)
                {
                    throw new InternalException($"Unexpected data type {this[i].type}, expected {nameof(RuntimeType.CHAR)}");
                }
                result += this[i].ValueChar;
            }
            return result;
        }
        internal string GetStringByPointer(int pointer)
        {
            int subpointer = this[pointer].ValueInt;
            int length = this[pointer + 1].ValueInt;
            return GetString(subpointer, length);
        }
    }

    public enum RuntimeType
    {
        BYTE,
        INT,
        FLOAT,
        STRING,
        BOOLEAN,
        CHAR,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct DataItem
    {
        public static DataItem Null => new() { };

        public readonly RuntimeType type;
        internal DataStack stack;
        internal HEAP heap;

        #region Value Fields

        byte? valueByte;
        int? valueInt;
        float? valueFloat;
        bool? valueBoolean;
        char? valueChar;

        #endregion

        public bool IsNull
        {
            get
            {
                if (valueByte.HasValue) return false;
                if (valueInt.HasValue) return false;
                if (valueFloat.HasValue) return false;
                if (valueBoolean.HasValue) return false;
                if (valueChar.HasValue) return false;
                return true;
            }
        }
        /// <summary><b>Only for debugging!</b></summary>
        public string Tag { get; internal set; }

        #region Value Properties

        public byte ValueByte
        {
            get
            {
                if (type == RuntimeType.BYTE)
                { return valueByte.Value; }

                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to byte");
            }
            set
            {
                valueByte = value;
            }
        }
        public int ValueInt
        {
            get
            {
                if (type == RuntimeType.INT)
                { return valueInt.Value; }

                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to integer");
            }
            set
            {
                valueInt = value;
            }
        }
        public float ValueFloat
        {
            get
            {
                if (type == RuntimeType.FLOAT)
                {
                    return valueFloat.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to float");
            }
            set
            {
                valueFloat = value;
            }
        }
        public bool ValueBoolean
        {
            get
            {
                if (type == RuntimeType.BOOLEAN)
                {
                    return valueBoolean.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to boolean");
            }
            set
            {
                valueBoolean = value;
            }
        }
        public char ValueChar
        {
            get
            {
                if (type == RuntimeType.CHAR)
                {
                    return valueChar.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to char");
            }
            set
            {
                valueChar = value;
            }
        }

        #endregion

        #region Constructors

        DataItem(RuntimeType type, string tag)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueBoolean = null;
            this.valueChar = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        DataItem(RuntimeType type)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueBoolean = null;
            this.valueChar = null;

            this.stack = null;
            this.heap = null;

            this.Tag = null;
        }

        public DataItem(int value, string tag) : this(RuntimeType.INT, tag)
        { this.valueInt = value; }
        public DataItem(byte value, string tag) : this(RuntimeType.BYTE, tag)
        { this.valueByte = value; }
        public DataItem(float value, string tag) : this(RuntimeType.FLOAT, tag)
        { this.valueFloat = value; }
        public DataItem(bool value, string tag) : this(RuntimeType.BOOLEAN, tag)
        { this.valueBoolean = value; }
        public DataItem(char value, string tag) : this(RuntimeType.CHAR, tag)
        { this.valueChar = value; }

        public DataItem(int value) : this(RuntimeType.INT)
        { this.valueInt = value; }
        public DataItem(byte value) : this(RuntimeType.BYTE)
        { this.valueByte = value; }
        public DataItem(float value) : this(RuntimeType.FLOAT)
        { this.valueFloat = value; }
        public DataItem(bool value) : this(RuntimeType.BOOLEAN)
        { this.valueBoolean = value; }
        public DataItem(char value) : this(RuntimeType.CHAR)
        { this.valueChar = value; }

        /// <exception cref="RuntimeException"/>
        public DataItem(object value, string tag) : this(RuntimeType.BYTE, tag)
        {
            if (value == null)
            {
                throw new RuntimeException($"Unknown type null");
            }

            if (value is int a)
            {
                this.type = RuntimeType.INT;
                this.valueInt = a;
            }
            else if (value is float b)
            {
                this.type = RuntimeType.FLOAT;
                this.valueFloat = b;
            }
            else if (value is bool d)
            {
                this.type = RuntimeType.BOOLEAN;
                this.valueBoolean = d;
            }
            else if (value is byte g)
            {
                this.type = RuntimeType.BYTE;
                this.valueByte = g;
            }
            else if (value is char h)
            {
                this.type = RuntimeType.CHAR;
                this.valueChar = h;
            }
            else
            {
                throw new RuntimeException($"Unknown type {value.GetType().FullName}");
            }
        }

        #endregion

        #region Operators

        /// <exception cref="RuntimeException"/>
        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value + right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value + right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do + operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value - right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value - right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftLeft(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value << right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value << right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do << operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem BitshiftRight(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value >> right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value >> right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do >> operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value * right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value * right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do * operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value / right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value / right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do / operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value % right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value, leftSide.Tag); }
            }

            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value % right.Value, leftSide.Tag); }
            }

            throw new RuntimeException("Can't do % operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator <(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value < right.Value; }
            }

            throw new RuntimeException("Can't do < operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator >(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value > right.Value; }
            }

            throw new RuntimeException("Can't do > operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator <=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide < rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do <= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator >=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return (leftSide > rightSide) || (leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do >= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
        }

        /// <exception cref="RuntimeException"/>
        public static bool operator ==(DataItem leftSide, DataItem rightSide)
        {
            {
                float? left = leftSide.Float;
                float? right = rightSide.Float;

                if (left.HasValue && right.HasValue)
                { return left.Value == right.Value; }
            }

            throw new RuntimeException("Can't do == operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        /// <exception cref="RuntimeException"/>
        public static bool operator !=(DataItem leftSide, DataItem rightSide)
        {
            try
            { return !(leftSide == rightSide); }
            catch (RuntimeException ex)
            { throw new RuntimeException("Can't do != operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString(), ex); }
        }

        /// <exception cref="RuntimeException"/>
        public static DataItem operator !(DataItem leftSide)
        {
            if (leftSide.type == RuntimeType.BOOLEAN)
            {
                return new DataItem(!leftSide.ValueBoolean, leftSide.Tag);
            }
            throw new RuntimeException($"Can't do ! operation with type {leftSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator |(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value | right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value | right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do | operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator &(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value & right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value & right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do & operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        /// <exception cref="RuntimeException"/>
        public static DataItem operator ^(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == RuntimeType.BYTE && rightSide.type == RuntimeType.BYTE)
            {
                byte? left = leftSide.Byte;
                byte? right = rightSide.Byte;

                if (left.HasValue && right.HasValue)
                {
                    int r = left.Value ^ right.Value;
                    if (r < byte.MinValue || r > byte.MaxValue)
                    { return new DataItem(r, leftSide.Tag); }
                    else
                    { return new DataItem((byte)r, leftSide.Tag); }
                }
            }

            {
                int? left = leftSide.Integer;
                int? right = rightSide.Integer;

                if (left.HasValue && right.HasValue)
                { return new DataItem(left.Value ^ right.Value, leftSide.Tag); }
            }

            throw new RuntimeException($"Can't do ^ operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }

        public bool IsFalsy() => this.type switch
        {
            RuntimeType.BYTE => this.valueByte == 0,
            RuntimeType.INT => this.valueInt.Value == 0,
            RuntimeType.FLOAT => this.valueFloat.Value == 0f,
            RuntimeType.STRING => false,
            RuntimeType.BOOLEAN => !this.valueBoolean.Value,
            RuntimeType.CHAR => (int)this.valueChar == 0,
            _ => false,
        };

        #endregion

        public int? Integer => type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.BOOLEAN => this.ValueBoolean ? 1 : 0,
            RuntimeType.CHAR => this.ValueChar,
            _ => null,
        };
        public byte? Byte
        {
            get
            {
                int? integer_ = this.Integer;
                return (!integer_.HasValue || integer_.Value < byte.MinValue || integer_.Value > byte.MaxValue) ?
                    null :
                    (byte)integer_.Value;
            }
        }
        public float? Float => type switch
        {
            RuntimeType.BYTE => this.ValueByte,
            RuntimeType.INT => this.ValueInt,
            RuntimeType.FLOAT => this.ValueFloat,
            RuntimeType.BOOLEAN => this.ValueBoolean ? 1f : 0f,
            RuntimeType.CHAR => this.ValueChar,
            _ => null,
        };

        /// <exception cref="RuntimeException"/>
        public override string ToString() => this.IsNull ? "null" : type switch
        {
            RuntimeType.INT => ValueInt.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.BYTE => ValueByte.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.FLOAT => ValueFloat.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.CHAR => ValueChar.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RuntimeType.BOOLEAN => ValueBoolean.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
        };

        /// <exception cref="RuntimeException"/>
        public string GetDebuggerDisplay()
        {
            if (IsNull) return null;
            string retStr = type switch
            {
                RuntimeType.INT => ValueInt.ToString(),
                RuntimeType.BYTE => ValueByte.ToString(),
                RuntimeType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                RuntimeType.CHAR => $"'{ValueChar.Escape()}'",
                RuntimeType.BOOLEAN => ValueBoolean.ToString(),
                _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
            };
            if (!string.IsNullOrEmpty(this.Tag))
            {
                retStr = retStr + " #" + this.Tag;
            }
            return retStr;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(type);
            switch (type)
            {
                case RuntimeType.BYTE:
                    hash.Add(valueByte);
                    break;
                case RuntimeType.INT:
                    hash.Add(valueInt);
                    break;
                case RuntimeType.FLOAT:
                    hash.Add(valueFloat);
                    break;
                case RuntimeType.BOOLEAN:
                    hash.Add(valueBoolean);
                    break;
                case RuntimeType.CHAR:
                    hash.Add(valueChar);
                    break;
                default:
                    break;
            }
            return hash.ToHashCode();
        }

        /// <exception cref="NotImplementedException"/>
        public override bool Equals(object obj)
            => obj is DataItem value &&
            this.type == value.type &&
            this.type switch
            {
                RuntimeType.BYTE => valueByte == value.valueByte,
                RuntimeType.INT => valueInt == value.valueInt,
                RuntimeType.FLOAT => valueFloat == value.valueFloat,
                RuntimeType.BOOLEAN => valueBoolean == value.valueBoolean,
                RuntimeType.CHAR => valueChar == value.valueChar,
                _ => throw new NotImplementedException(),
            };
    }
}
