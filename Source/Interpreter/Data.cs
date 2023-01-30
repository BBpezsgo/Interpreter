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
                static int CalculateItemSize(DataItem item)
                {
                    switch (item.type)
                    {
                        case DataType.INT:
                            return 4;
                        case DataType.FLOAT:
                            return 4;
                        case DataType.STRING:
                            return 4 + System.Text.ASCIIEncoding.ASCII.GetByteCount(item.ValueString);
                        case DataType.BOOLEAN:
                            return 1;
                        case DataType.STRUCT:
                            if (item.ValueStruct is Struct valStruct)
                            {
                                int result = 0;
                                foreach (var field in valStruct.fields)
                                {
                                    result += System.Text.ASCIIEncoding.ASCII.GetByteCount(field.Key);
                                    result += CalculateItemSize(field.Value);
                                }
                                result += 4;
                                return result;
                            }
                            break;
                        case DataType.LIST:
                            {
                                var result = 0;
                                foreach (var element in item.ValueList.items)
                                { result += CalculateItemSize(element); }
                                result += 4;
                                return result;
                            }
                    }
                    return 0;
                }

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
        /// <returns>Adds a new item to the end</returns>
        public void Push(IStruct value, string tag = null) => Push(new DataItem(value, tag));
        /// <summary>Adds a list to the end</summary>
        public override void PushRange(List<DataItem> list) => PushRange(list.ToArray());
        /// <summary>Adds a list to the end</summary>
        public void PushRange(List<int> list)
        {
            var newList = new List<DataItem>();
            for (int i = 0; i < list.Count; i++)
            {
                newList.Add(new DataItem(list[i], null));
            }
            PushRange(newList);
        }
        /// <summary>Adds an array to the end</summary>
        public override void PushRange(DataItem[] list)
        { foreach (DataItem item in list) Push(item); }
        /// <summary>Adds a list to the end</summary>
        public void PushRange(int[] list, string tag = "")
        {
            var newList = new List<DataItem>();
            for (int i = 0; i < list.Length; i++)
            {
                newList.Add(new DataItem(list[i], (tag.Length > 0) ? tag : null));
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
            get => heap[i];
            set => heap[i] = value;
        }

        internal void Set(int address, int v) => heap[address].ValueInt = v;
        internal void Set(int address, float v) => heap[address].ValueFloat = v;
        internal void Set(int address, bool v) => heap[address].ValueBoolean = v;
        internal void Set(int address, string v) => heap[address].ValueString = v;
        internal void Set(int address, IStruct v) => heap[address].ValueStruct = v;
        internal void Set(int address, DataItem.List v) => heap[address].ValueList = v;

        internal DataItem[] ToArray() => heap.ToList().ToArray();
    }

    public enum DataType
    {
        BYTE,
        INT,
        FLOAT,
        STRING,
        BOOLEAN,
        STRUCT,
        LIST,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public struct DataItem
    {
        [System.Diagnostics.DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
        public class List
        {
            public DataType itemTypes;
            public List<DataItem> items = new();

            public List(DataType type)
            {
                this.itemTypes = type;
            }

            internal void Add(DataItem newItem)
            {
                if (itemTypes == newItem.type)
                {
                    items.Add(newItem);
                }
                else
                {
                    throw new RuntimeException($"Wrong type ({newItem.type.ToString().ToLower()}) of item pushed to the list {(itemTypes == DataType.LIST ? "?[]" : itemTypes.ToString().ToLower()) + "[]"}");
                }
            }

            internal void Add(DataItem newItem, int i)
            {
                if (itemTypes == newItem.type)
                {
                    items.Insert(i, newItem);
                }
                else
                {
                    throw new RuntimeException($"Wrong type ({newItem.type}) of item added to the list with type {itemTypes}");
                }
            }

            internal void Remove()
            {
                if (items.Count > 0)
                {
                    items.RemoveAt(items.Count - 1);
                }
            }

            internal void Remove(int i)
            {
                items.RemoveAt(i);
            }

            public override string ToString()
            {
                return $"{itemTypes.GetTypeText()}[]";
            }

            internal List Copy()
            {
                List listCopy = new(itemTypes);
                foreach (var item in items)
                { listCopy.Add(item); }
                return listCopy;
            }
            internal List CopyRecursive()
            {
                List listCopy = new(itemTypes);
                foreach (var item in items)
                { listCopy.Add(item.CopyRecursive()); }
                return listCopy;
            }
        }

        public readonly DataType type;

        #region Value Fields

        byte? valueByte;
        int? valueInt;
        float? valueFloat;
        string valueString;
        bool? valueBoolean;
        IStruct valueStruct;
        List valueList;

        #endregion

        internal bool IsHeapAddress;

        internal DataStack stack;
        internal HEAP heap;

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
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueInt = value;
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
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueFloat = value;
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
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueString = value;
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
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueBoolean = value;
            }
        }
        public IStruct ValueStruct
        {
            get
            {
                if (type == DataType.STRUCT)
                {
                    return valueStruct;
                }
                throw new RuntimeException($"Can't cast {type.ToString().ToLower()} to {(valueStruct != null ? valueStruct is not UnassignedStruct ? valueStruct.Name ?? "struct" : "struct" : "struct")}");
            }
            set
            {
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueStruct = value;
            }
        }
        public List ValueList
        {
            get
            {
                if (type == DataType.LIST)
                {
                    return valueList;
                }
                throw new RuntimeException("Can't cast " + type.ToString().ToLower() + $" to {(valueList != null ? $"{valueList.ToString().ToString().ToLower()}[]" : "list")}");
            }
            set
            {
                if (IsHeapAddress) heap.Set(valueInt.Value, value);
                else valueList = value;
            }
        }

        #endregion

        /// <summary>Only for debugging</summary>
        public string Tag { get; internal set; }

        #region Constructors

        DataItem(DataType type, string tag)
        {
            this.type = type;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueByte = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }

        public DataItem(int value, string tag, bool isHeapAddress = false) : this(DataType.INT, tag)
        {
            this.IsHeapAddress = isHeapAddress;
            this.valueInt = value;
        }
        public DataItem(byte value, string tag) : this(DataType.BYTE, tag)
        {
            this.valueByte = value;
        }
        public DataItem(float value, string tag) : this(DataType.FLOAT, tag)
        {
            this.valueFloat = value;
        }
        public DataItem(string value, string tag) : this(DataType.STRING, tag)
        {
            this.valueString = value;
        }
        public DataItem(bool value, string tag) : this(DataType.BOOLEAN, tag)
        {
            this.valueBoolean = value;
        }
        public DataItem(IStruct value, string tag) : this(DataType.STRUCT, tag)
        {
            this.valueStruct = value;
        }
        public DataItem(List value, string tag) : this(DataType.LIST, tag)
        {
            this.valueList = value;
        }
        public DataItem(BBCode.TypeToken type1, string tag) : this(DataType.BYTE, tag)
        {
            switch (type1.typeName)
            {
                case BBCode.BuiltinType.INT:
                    this.type = DataType.INT;
                    this.valueInt = 0;
                    break;
                case BBCode.BuiltinType.FLOAT:
                    this.type = DataType.FLOAT;
                    this.valueFloat = 0f;
                    break;
                case BBCode.BuiltinType.STRING:
                    this.type = DataType.STRING;
                    this.valueString = "";
                    break;
                case BBCode.BuiltinType.BOOLEAN:
                    this.type = DataType.BOOLEAN;
                    this.valueBoolean = false;
                    break;
                case BBCode.BuiltinType.STRUCT:
                    this.type = DataType.STRUCT;
                    this.valueStruct = new UnassignedStruct();
                    break;
            }
        }
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
            else if (value is IStruct e)
            {
                this.type = DataType.STRUCT;
                this.valueStruct = e;
            }
            else if (value is List f)
            {
                this.type = DataType.LIST;
                this.ValueList = f;
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

        public DataItem TrySet(DataItem value)
        {
            switch (type)
            {
                case DataType.BYTE:
                    switch (value.type)
                    {
                        case DataType.BYTE:
                            return new DataItem(value.ValueByte, null);
                    }
                    break;
                case DataType.INT:
                    switch (value.type)
                    {
                        case DataType.INT:
                            return new DataItem(value.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(Math.Round(value.ValueFloat), null);
                        case DataType.STRING:
                            return new DataItem(int.Parse(value.ValueString), null);
                    }
                    break;
                case DataType.FLOAT:
                    switch (value.type)
                    {
                        case DataType.INT:
                            return new DataItem(value.ValueInt, null);
                        case DataType.FLOAT:
                            return new DataItem(value.ValueFloat, null);
                    }
                    break;
                case DataType.STRING:
                    switch (value.type)
                    {
                        case DataType.INT:
                            return new DataItem(value.ValueInt.ToString(), null);
                        case DataType.FLOAT:
                            return new DataItem(value.ValueFloat.ToString(), null);
                        case DataType.STRING:
                            return new DataItem(value.ValueString, null);
                        case DataType.BOOLEAN:
                            return new DataItem(value.ValueBoolean.ToString(), null);
                    }
                    break;
                case DataType.BOOLEAN:
                    switch (value.type)
                    {
                        case DataType.BOOLEAN:
                            return new DataItem(value.ValueBoolean, null);
                    }
                    break;
                case DataType.STRUCT:
                    switch (value.type)
                    {
                        case DataType.STRUCT:
                            return new DataItem(value.ValueStruct, null);
                    }
                    break;
                case DataType.LIST:
                    switch (value.type)
                    {
                        case DataType.LIST:
                            return new DataItem(value.ValueList, null);
                    }
                    break;
            }
            throw new RuntimeException("Can't cast from " + value.type.ToString() + " to " + type.ToString());
        }

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

        public static bool operator ==(DataItem leftSide, int rightSide) => (leftSide == new DataItem(rightSide, null));
        public static bool operator !=(DataItem leftSide, int rightSide) => (leftSide != new DataItem(rightSide, null));

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
            DataType.STRUCT => "{ ... }",
            DataType.LIST => "[ ... ]",
            _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
        };

        public override string ToString()
        {
            string retStr = type switch
            {
                DataType.INT => ValueInt.ToString(),
                DataType.BYTE => ValueByte.ToString(),
                DataType.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                DataType.STRING => $"\"{ValueString}\"",
                DataType.BOOLEAN => ValueBoolean.ToString(),
                DataType.STRUCT => $"{ValueStruct.GetType().Name} {{...}}",
                DataType.LIST => "[...]",
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
            hash.Add(valueInt);
            hash.Add(valueFloat);
            hash.Add(valueString);
            hash.Add(valueBoolean);
            hash.Add(valueStruct);
            hash.Add(valueList);
            hash.Add(valueByte);
            hash.Add(IsHeapAddress);
            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DataItem item &&
                   type == item.type &&
                   valueByte == item.valueByte &&
                   valueInt == item.valueInt &&
                   valueFloat == item.valueFloat &&
                   valueString == item.valueString &&
                   valueBoolean == item.valueBoolean &&
                   EqualityComparer<IStruct>.Default.Equals(valueStruct, item.valueStruct) &&
                   EqualityComparer<List>.Default.Equals(valueList, item.valueList) &&
                   IsHeapAddress == item.IsHeapAddress;
        }

        public DataItem Copy() => type switch
        {
            DataType.BYTE => new DataItem(valueByte, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.INT => new DataItem(valueInt, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.FLOAT => new DataItem(valueFloat, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.STRING => new DataItem(valueString, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.BOOLEAN => new DataItem(valueBoolean, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.STRUCT => new DataItem(valueStruct.Copy(), Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.LIST => new DataItem(valueList.Copy(), Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            _ => throw new InternalException($"Unknown type {type}"),
        };
        public DataItem CopyRecursive() => type switch
        {
            DataType.BYTE => new DataItem(valueByte, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.INT => new DataItem(valueInt, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.FLOAT => new DataItem(valueFloat, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.STRING => new DataItem(valueString, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.BOOLEAN => new DataItem(valueBoolean, Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.STRUCT => new DataItem(valueStruct.CopyRecursive(), Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            DataType.LIST => new DataItem(valueList.CopyRecursive(), Tag) { heap = heap, IsHeapAddress = IsHeapAddress, stack = stack },
            _ => throw new InternalException($"Unknown type {type}"),
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
                            case DataType.STRUCT:
                            case DataType.LIST:
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
                            case DataType.STRUCT:
                            case DataType.LIST:
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
                            case DataType.STRUCT:
                            case DataType.LIST:
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
                            case DataType.STRUCT:
                            case DataType.LIST:
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
                            case DataType.STRUCT:
                            case DataType.LIST:
                            case DataType.BYTE:
                            case DataType.INT:
                            case DataType.FLOAT:
                            default:
                                break;
                        }
                        break;
                    }
                case DataType.STRUCT:
                case DataType.LIST:
                default:
                    break;
            }
            throw new NotImplementedException();
        }
    }

    public class UnassignedStruct : IStruct
    {
        public string Name => throw new RuntimeException("Struct is null");
        public bool HaveField(string field) => throw new RuntimeException("Struct is null");
        public void SetField(string field, DataItem value) => throw new RuntimeException("Struct is null");
        public DataItem GetField(string field) => throw new RuntimeException("Struct is null");
        public IStruct Copy() => new UnassignedStruct();
        public IStruct CopyRecursive() => new UnassignedStruct();
        public string[] GetFields() => Array.Empty<string>();

        public override string ToString() => "struct {null}";
    }

    public class Struct : IStruct
    {
        internal readonly Dictionary<string, DataItem> fields = new();

        readonly string name;
        public string Name => name;

        public Struct(Dictionary<string, DataItem> fields, string name)
        { this.fields = fields; this.name = name; }

        public bool HaveField(string field) => fields.ContainsKey(field);
        public void SetField(string field, DataItem value) => fields[field] = value;
        public DataItem GetField(string field) => fields[field];
        public IStruct Copy()
        {
            Dictionary<string, DataItem> fieldsClone = new();

            foreach (var field in this.fields)
            { fieldsClone.Add(field.Key, field.Value); }

            return new Struct(fieldsClone, name);
        }
        public IStruct CopyRecursive()
        {
            Dictionary<string, DataItem> fieldsClone = new();

            foreach (var field in this.fields)
            { fieldsClone.Add(field.Key, field.Value.Copy()); }

            return new Struct(fieldsClone, name);
        }

        public override string ToString() => "struct {...}";

        public string[] GetFields()
        {
            string[] result = new string[fields.Count];
            for (int i = 0; i < result.Length; i++) result[i] = fields.ElementAt(i).Key;
            return result;
        }
    }

    public interface IStruct
    {
        public string Name { get; }
        public bool HaveField(string field);
        public void SetField(string field, DataItem value);
        public DataItem GetField(string field);
        public IStruct Copy();
        public IStruct CopyRecursive();
        string[] GetFields();
    }
}
