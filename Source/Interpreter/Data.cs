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
                    DataType.INT => 4,
                    DataType.FLOAT => 4,
                    DataType.STRING => 4 + System.Text.Encoding.ASCII.GetByteCount(item.ValueString),
                    DataType.BOOLEAN => 1,
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
        public void Push(string value, string tag = null) => Push(new DataItem(value, tag));
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
                if (i == BBCode.Utils.NULL_POINTER) throw new RuntimeException($"Null pointer!");
                return heap[i];
            }
            set
            {
                if (i == BBCode.Utils.NULL_POINTER) throw new RuntimeException($"Null pointer!");
                heap[i] = value;
            }
        }

        internal DataItem[] ToArray() => heap.ToList().ToArray();
    }

    public enum DataType
    {
        BYTE,
        INT,
        FLOAT,
        STRING,
        BOOLEAN,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public struct DataItem
    {
        public static DataItem Null => new() { };

        public readonly DataType type;
        internal DataStack stack;
        internal HEAP heap;

        #region Value Fields

        byte? valueByte;
        int? valueInt;
        float? valueFloat;
        string valueString;
        bool? valueBoolean;

        #endregion

        public bool IsNull
        {
            get
            {
                if (valueByte.HasValue) return false;
                if (valueInt.HasValue) return false;
                if (valueFloat.HasValue) return false;
                if (valueBoolean.HasValue) return false;
                if (valueString != null) return false;
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
                if (type == DataType.BYTE)
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
                if (type == DataType.INT)
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
                if (type == DataType.FLOAT)
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
        public string ValueString
        {
            get
            {
                if (type == DataType.STRING)
                {
                    return valueString;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + " to string");
            }
            set
            {
                valueString = value;
            }
        }
        public bool ValueBoolean
        {
            get
            {
                if (type == DataType.BOOLEAN)
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

        #endregion

        #region Constructors

        DataItem(DataType type, string tag)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        DataItem(DataType type)
        {
            this.type = type;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;

            this.stack = null;
            this.heap = null;

            this.Tag = null;
        }

        public DataItem(int value, string tag) : this(DataType.INT, tag)
        {
            this.valueInt = value;
        }
        public DataItem(byte value, string tag) : this(DataType.BYTE, tag)
        { this.valueByte = value; }
        public DataItem(float value, string tag) : this(DataType.FLOAT, tag)
        { this.valueFloat = value; }
        public DataItem(string value, string tag) : this(DataType.STRING, tag)
        { this.valueString = value; }
        public DataItem(bool value, string tag) : this(DataType.BOOLEAN, tag)
        { this.valueBoolean = value; }

        public DataItem(int value) : this(DataType.INT)
        {
            this.valueInt = value;
        }
        public DataItem(byte value) : this(DataType.BYTE)
        { this.valueByte = value; }
        public DataItem(float value) : this(DataType.FLOAT)
        { this.valueFloat = value; }
        public DataItem(string value) : this(DataType.STRING)
        { this.valueString = value; }
        public DataItem(bool value) : this(DataType.BOOLEAN)
        { this.valueBoolean = value; }

        public DataItem(object value, string tag) : this(DataType.BYTE, tag)
        {
            if (value == null)
            {
                throw new RuntimeException($"Unknown type null");
            }

            if (value is int a)
            {
                this.type = DataType.INT;
                this.valueInt = a;
            }
            else if (value is float b)
            {
                this.type = DataType.FLOAT;
                this.valueFloat = b;
            }
            else if (value is string c)
            {
                this.type = DataType.STRING;
                this.valueString = c;
            }
            else if (value is bool d)
            {
                this.type = DataType.BOOLEAN;
                this.valueBoolean = d;
            }
            else if (value is byte g)
            {
                this.type = DataType.BYTE;
                this.valueByte = g;
            }
            else
            {
                throw new RuntimeException($"Unknown type {value.GetType().FullName}");
            }
        }

        #endregion

        #region Operators

        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.BYTE:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueByte + rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueByte + rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueByte + rightSide.ValueFloat, null);
                        case DataType.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueFloat, null);
                        case DataType.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueFloat, null);
                        case DataType.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case DataType.STRING:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case DataType.STRING:
                            return new DataItem(leftSide.ValueString + rightSide.ValueString, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do + operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.BYTE:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueByte - rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueByte - rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueByte - rightSide.ValueFloat, null);
                    }
                    break;
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueFloat, null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static DataItem BitshiftLeft(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.BYTE:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueByte << rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueByte << rightSide.ValueInt, null);
                    }
                    break;
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueInt << rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt << rightSide.ValueInt, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do << operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem BitshiftRight(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.BYTE:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueByte >> rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueByte >> rightSide.ValueInt, null);
                    }
                    break;
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(leftSide.ValueInt >> rightSide.ValueByte, null);
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt >> rightSide.ValueInt, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do >> operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueFloat, null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueFloat * rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueFloat * rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do * operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueFloat, null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueFloat / rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueFloat / rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do / operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueFloat, null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return new DataItem(leftSide.ValueFloat % rightSide.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(leftSide.ValueFloat % rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do % operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator <(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt < rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt < rightSide.ValueFloat;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat < rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat < rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do < operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator >(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt > rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt > rightSide.ValueFloat;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat > rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat > rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do > operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator <=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt <= rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt <= rightSide.ValueFloat;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat <= rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat <= rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do <= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator >=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt >= rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt >= rightSide.ValueFloat;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat >= rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat >= rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do >= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator ==(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt == rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt == rightSide.ValueFloat;
                        case DataType.STRING:
                            return leftSide.ValueInt.ToString() == rightSide.ValueString;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat == rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat == rightSide.ValueFloat;
                        case DataType.STRING:
                            return leftSide.ValueFloat.ToString() == rightSide.ValueString;
                    }
                    break;
                case DataType.STRING:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueString == rightSide.ValueInt.ToString();
                        case DataType.FLOAT:
                            return leftSide.ValueString == rightSide.ValueFloat.ToString();
                        case DataType.STRING:
                            return leftSide.ValueString == rightSide.ValueString;
                    }
                    break;
                case DataType.BOOLEAN:
                    if (rightSide.type == DataType.BOOLEAN)
                    {
                        return leftSide.ValueBoolean == rightSide.ValueBoolean;
                    }
                    break;
            }

            throw new RuntimeException("Can't do == operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator !=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case DataType.INT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueInt != rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueInt != rightSide.ValueFloat;
                        case DataType.STRING:
                            return leftSide.ValueInt.ToString() != rightSide.ValueString;
                    }
                    break;
                case DataType.FLOAT:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueFloat != rightSide.ValueInt;
                        case DataType.FLOAT:
                            return leftSide.ValueFloat != rightSide.ValueFloat;
                        case DataType.STRING:
                            return leftSide.ValueFloat.ToString() != rightSide.ValueString;
                    }
                    break;
                case DataType.STRING:
                    switch (rightSide.type)
                    {
                        case DataType.INT:
                            return leftSide.ValueString != rightSide.ValueInt.ToString();
                        case DataType.FLOAT:
                            return leftSide.ValueString != rightSide.ValueFloat.ToString();
                        case DataType.STRING:
                            return leftSide.ValueString != rightSide.ValueString;
                    }
                    break;
                case DataType.BOOLEAN:
                    if (rightSide.type == DataType.BOOLEAN)
                    {
                        return leftSide.ValueBoolean != rightSide.ValueBoolean;
                    }
                    break;
            }

            throw new RuntimeException("Can't do != operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator ==(DataItem leftSide, bool rightSide) => (leftSide == new DataItem(rightSide, null));
        public static bool operator !=(DataItem leftSide, bool rightSide) => (leftSide != new DataItem(rightSide, null));

        public static DataItem operator !(DataItem leftSide)
        {
            if (leftSide.type == DataType.BOOLEAN)
            {
                return new DataItem(!leftSide.ValueBoolean, leftSide.Tag);
            }
            throw new RuntimeException($"Can't do ! operation with type {leftSide.GetTypeText()}");
        }
        public static DataItem operator |(DataItem leftSide, DataItem rightSide)
        {
            try
            {
                var (a, b) = IntoSimilarTypes(leftSide, rightSide);

                if (a.type == DataType.BOOLEAN && b.type == DataType.BOOLEAN)
                { return new DataItem(a.ValueBoolean | b.ValueBoolean, a.Tag); }
                if (a.type == DataType.BYTE && b.type == DataType.BYTE)
                { return new DataItem(a.ValueByte | b.ValueByte, a.Tag); }
                if (a.type == DataType.INT && b.type == DataType.INT)
                { return new DataItem(a.ValueInt | b.ValueInt, a.Tag); }
            }
            catch (NotImplementedException)
            { }

            throw new RuntimeException($"Can't do | operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        public static DataItem operator &(DataItem leftSide, DataItem rightSide)
        {
            try
            {
                var (a, b) = IntoSimilarTypes(leftSide, rightSide);

                if (a.type == DataType.BOOLEAN && b.type == DataType.BOOLEAN)
                { return new DataItem(a.ValueBoolean & b.ValueBoolean, a.Tag); }
                if (a.type == DataType.BYTE && b.type == DataType.BYTE)
                { return new DataItem(a.ValueByte & b.ValueByte, a.Tag); }
                if (a.type == DataType.INT && b.type == DataType.INT)
                { return new DataItem(a.ValueInt & b.ValueInt, a.Tag); }
            }
            catch (NotImplementedException)
            { }

            throw new RuntimeException($"Can't do & operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }
        public static DataItem operator ^(DataItem leftSide, DataItem rightSide)
        {
            try
            {
                var (a, b) = IntoSimilarTypes(leftSide, rightSide);

                if (a.type == DataType.BOOLEAN && b.type == DataType.BOOLEAN)
                { return new DataItem(a.ValueBoolean ^ b.ValueBoolean, a.Tag); }
                if (a.type == DataType.BYTE && b.type == DataType.BYTE)
                { return new DataItem(a.ValueByte ^ b.ValueByte, a.Tag); }
                if (a.type == DataType.INT && b.type == DataType.INT)
                { return new DataItem(a.ValueInt ^ b.ValueInt, a.Tag); }
            }
            catch (NotImplementedException)
            { }

            throw new RuntimeException($"Can't do ^ operation with type {leftSide.GetTypeText()} and {rightSide.GetTypeText()}");
        }

        public static bool operator true(DataItem leftSide)
        {
            if (leftSide.type == DataType.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }
        public static bool operator false(DataItem leftSide)
        {
            if (leftSide.type == DataType.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }

        #endregion

        public string ToStringValue() => type switch
        {
            DataType.INT => ValueInt.ToString(),
            DataType.BYTE => ValueByte.ToString(),
            DataType.FLOAT => ValueFloat.ToString().Replace(',', '.'),
            DataType.STRING => ValueString,
            DataType.BOOLEAN => ValueBoolean.ToString(),
            _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
        };

        public override string ToString()
        {
            if (IsNull) return null;
            string retStr = type switch
            {
                DataType.INT => ValueInt.ToString(),
                DataType.BYTE => ValueByte.ToString(),
                DataType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                DataType.STRING => $"\"{ValueString}\"",
                DataType.BOOLEAN => ValueBoolean.ToString(),
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
                case DataType.BYTE:
                    hash.Add(valueByte);
                    break;
                case DataType.INT:
                    hash.Add(valueInt);
                    break;
                case DataType.FLOAT:
                    hash.Add(valueFloat);
                    break;
                case DataType.STRING:
                    hash.Add(valueString);
                    break;
                case DataType.BOOLEAN:
                    hash.Add(valueBoolean);
                    break;
                default:
                    break;
            }
            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
            => obj is DataItem value &&
            this.type == value.type &&
            this.type switch
            {
                DataType.BYTE => valueByte == value.valueByte,
                DataType.INT => valueInt == value.valueInt,
                DataType.FLOAT => valueFloat == value.valueFloat,
                DataType.STRING => valueString == value.valueString,
                DataType.BOOLEAN => valueBoolean == value.valueBoolean,
                _ => throw new NotImplementedException(),
            };

        public static (DataItem, DataItem) IntoSimilarTypes(DataItem a, DataItem b)
        {
            switch (a.type)
            {
                case DataType.BYTE:
                    {
                        switch (b.type)
                        {
                            case DataType.BYTE: return (a, b);
                            case DataType.INT: return (new DataItem((int)a.ValueByte, a.Tag), b);
                            case DataType.FLOAT: return (new DataItem((float)a.ValueByte, a.Tag), b);
                            case DataType.STRING: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.BOOLEAN:
                            default:
                                break;
                        }
                        break;
                    }
                case DataType.INT:
                    {
                        switch (b.type)
                        {
                            case DataType.BYTE: return (a, new DataItem((byte)b.ValueByte, b.Tag));
                            case DataType.INT: return (a, b);
                            case DataType.FLOAT: return (new DataItem((float)a.ValueFloat, a.Tag), b);
                            case DataType.STRING: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.BOOLEAN:
                            default:
                                break;
                        }
                        break;
                    }
                case DataType.FLOAT:
                    {
                        switch (b.type)
                        {
                            case DataType.BYTE: return (a, new DataItem((float)b.ValueFloat, b.Tag));
                            case DataType.INT: return (a, new DataItem((float)b.ValueFloat, b.Tag));
                            case DataType.FLOAT: return (a, b);
                            case DataType.STRING: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.BOOLEAN:
                            default:
                                break;
                        }
                        break;
                    }
                case DataType.STRING:
                    {
                        switch (b.type)
                        {
                            case DataType.BYTE: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.INT: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.FLOAT: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            case DataType.STRING: return (a, b);
                            case DataType.BOOLEAN: return (new DataItem(a.ToStringValue(), a.Tag), b);
                            default:
                                break;
                        }
                        break;
                    }
                case DataType.BOOLEAN:
                    {
                        switch (b.type)
                        {
                            case DataType.STRING: return (a, new DataItem(b.ToStringValue(), b.Tag));
                            case DataType.BOOLEAN: return (a, b);
                            case DataType.BYTE:
                            case DataType.INT:
                            case DataType.FLOAT:
                            default:
                                break;
                        }
                        break;
                    }
                default:
                    break;
            }
            throw new NotImplementedException();
        }
    }
}
