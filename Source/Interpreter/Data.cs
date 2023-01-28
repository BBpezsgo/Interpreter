using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    using System.Linq;

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
                        case DataItem.Type.INT:
                            return 4;
                        case DataItem.Type.FLOAT:
                            return 4;
                        case DataItem.Type.STRING:
                            return 4 + System.Text.ASCIIEncoding.ASCII.GetByteCount(item.ValueString);
                        case DataItem.Type.BOOLEAN:
                            return 1;
                        case DataItem.Type.STRUCT:
                            if (item.ValueStruct is DataItem.Struct valStruct)
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
                        case DataItem.Type.LIST:
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
                for (int i = 0; i < stack.Count; i++)
                { result += CalculateItemSize(stack[i]); }
                return result;
            }
        }

        internal BytecodeProcessor cpu;

        public void Destroy() => stack.Clear();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public override DataItem Pop()
        {
            DataItem val = this.stack[^1];
            this.stack.RemoveAt(this.stack.Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public override void Push(DataItem value)
        {
            var item = value;
            item.stack = this;
            item.heap = this.cpu.Memory.Heap;
            this.stack.Add(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(DataItem value, string tag)
        {
            var item = value;
            item.stack = this;
            item.heap = this.cpu.Memory.Heap;
            item.Tag = tag;
            this.stack.Add(item);
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
            item.heap = this.cpu.Memory.Heap;
            if (!overrideTag)
            {
                item.Tag = stack[index].Tag;
            }
            this.stack[index] = item;
        }
        /// <returns>A specific item</returns>
        public DataItem Get(int index) => this.stack[index];
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

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public struct DataItem
    {
        public class UnassignedStruct : IStruct
        {
            public string Name => throw new RuntimeException("Struct is null");
            public bool HaveField(string field) => throw new RuntimeException("Struct is null");
            public void SetField(string field, DataItem value) => throw new RuntimeException("Struct is null");
            public DataItem GetField(string field) => throw new RuntimeException("Struct is null");
            public IStruct Copy() => new UnassignedStruct();
            public IStruct CopyRecursive() => new UnassignedStruct();

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
        }

        public class List
        {
            public Type itemTypes;
            public List<DataItem> items = new();

            public List(Type type)
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
                    throw new RuntimeException($"Wrong type ({newItem.type.ToString().ToLower()}) of item pushed to the list {(itemTypes == Type.LIST ? "?[]" : itemTypes.ToString().ToLower()) + "[]"}");
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
                return $"[{(int)itemTypes}]";
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

        public enum Type
        {
            INT,
            FLOAT,
            STRING,
            BOOLEAN,
            STRUCT,
            LIST,
        }

        public Type type;

        #region Value Fields

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

        public int ValueInt
        {
            get
            {
                if (type == Type.INT)
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
                if (type == Type.FLOAT)
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
                if (type == Type.STRING)
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
                if (type == Type.BOOLEAN)
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
                if (type == Type.STRUCT)
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
                if (type == Type.LIST)
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

        public DataItem(int value, string tag, bool isHeapAddress = false)
        {
            this.type = Type.INT;

            this.IsHeapAddress = isHeapAddress;

            this.valueInt = value;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(float value, string tag)
        {
            this.type = Type.FLOAT;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = value;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(string value, string tag)
        {
            this.type = Type.STRING;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = value;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(bool value, string tag)
        {
            this.type = Type.BOOLEAN;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = value;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(IStruct value, string tag)
        {
            this.type = Type.STRUCT;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = value;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(List value, string tag)
        {
            this.type = Type.LIST;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = value;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(BBCode.TypeToken type1, string tag)
        {
            this.type = Type.INT;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            switch (type1.typeName)
            {
                case BBCode.BuiltinType.INT:
                    this.type = Type.INT;
                    this.valueInt = 0;
                    break;
                case BBCode.BuiltinType.FLOAT:
                    this.type = Type.FLOAT;
                    this.valueFloat = 0f;
                    break;
                case BBCode.BuiltinType.STRING:
                    this.type = Type.STRING;
                    this.valueString = "";
                    break;
                case BBCode.BuiltinType.BOOLEAN:
                    this.type = Type.BOOLEAN;
                    this.valueBoolean = false;
                    break;
                case BBCode.BuiltinType.STRUCT:
                    // TODO: Ezt tesztelni:
                    this.type = Type.STRUCT;
                    this.valueStruct = new UnassignedStruct();
                    break;
            }

            this.stack = null;
            this.heap = null;

            this.Tag = tag;
        }
        public DataItem(object value, string tag)
        {
            if (value == null)
            {
                throw new RuntimeException($"Unknown type null");
            }

            this.type = Type.INT;

            this.IsHeapAddress = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;

            this.stack = null;
            this.heap = null;

            this.Tag = tag;

            if (value is int a)
            {
                this.type = Type.INT;
                this.valueInt = a;
            }
            else if (value is float b)
            {
                this.type = Type.FLOAT;
                this.valueFloat = b;
            }
            else if (value is string c)
            {
                this.type = Type.STRING;
                this.valueString = c;
            }
            else if (value is bool d)
            {
                this.type = Type.BOOLEAN;
                this.valueBoolean = d;
            }
            else if (value is IStruct e)
            {
                this.type = Type.STRUCT;
                this.valueStruct = e;
            }
            else if (value is List f)
            {
                this.type = Type.LIST;
                this.ValueList = f;
            }
            else
            {
                throw new RuntimeException($"Unknown type {value.GetType().FullName}");
            }
        }

        #endregion

        #region TrySet()

        public DataItem TrySet(DataItem value)
        {
            switch (type)
            {
                case Type.INT:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(Math.Round(value.ValueFloat), null);
                        case Type.STRING:
                            return new DataItem(int.Parse(value.ValueString), null);
                    }
                    break;
                case Type.FLOAT:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(value.ValueFloat, null);
                    }
                    break;
                case Type.STRING:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt.ToString(), null);
                        case Type.FLOAT:
                            return new DataItem(value.ValueFloat.ToString(), null);
                        case Type.STRING:
                            return new DataItem(value.ValueString, null);
                        case Type.BOOLEAN:
                            return new DataItem(value.ValueBoolean.ToString(), null);
                    }
                    break;
                case Type.BOOLEAN:
                    switch (value.type)
                    {
                        case Type.BOOLEAN:
                            return new DataItem(value.ValueBoolean, null);
                    }
                    break;
                case Type.STRUCT:
                    switch (value.type)
                    {
                        case Type.STRUCT:
                            return new DataItem(value.ValueStruct, null);
                    }
                    break;
                case Type.LIST:
                    switch (value.type)
                    {
                        case Type.LIST:
                            return new DataItem(value.ValueList, null);
                    }
                    break;
            }
            throw new RuntimeException("Can't cast from " + value.type.ToString() + " to " + type.ToString());
        }
        public DataItem TrySet(int value)
        {
            return type switch
            {
                Type.INT => new DataItem(value, null),
                Type.FLOAT => new DataItem(value, null),
                Type.STRING => new DataItem(value.ToString(), null),
                _ => throw new RuntimeException("Can't cast from " + "INT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(float value)
        {
            return type switch
            {
                Type.INT => new DataItem(Math.Round(value), null),
                Type.FLOAT => new DataItem(value, null),
                Type.STRING => new DataItem(value.ToString(), null),
                _ => throw new RuntimeException("Can't cast from " + "FLOAT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(bool value)
        {
            return type switch
            {
                Type.STRING => new DataItem(value.ToString(), null),
                Type.BOOLEAN => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "BOOLEAN" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(string value)
        {
            return type switch
            {
                Type.STRING => new DataItem(value, null),
                Type.INT => new DataItem(int.Parse(value), null),
                _ => throw new RuntimeException("Can't cast from " + "STRING" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(IStruct value)
        {
            return type switch
            {
                Type.STRUCT => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "STRUCT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(List value)
        {
            return type switch
            {
                Type.LIST => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "LIST" + " to " + type.ToString()),
            };
        }

        #endregion

        #region Operators

        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueFloat, null);
                        case Type.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueFloat, null);
                        case Type.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case Type.STRING:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat * rightSide.ValueInt, null);
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat / rightSide.ValueInt, null);
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat % rightSide.ValueInt, null);
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt < rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt < rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat < rightSide.ValueInt;
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt > rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt > rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat > rightSide.ValueInt;
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt <= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt <= rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat <= rightSide.ValueInt;
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt >= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt >= rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat >= rightSide.ValueInt;
                        case Type.FLOAT:
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt == rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt == rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueInt.ToString() == rightSide.ValueString;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat == rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat == rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueFloat.ToString() == rightSide.ValueString;
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueString == rightSide.ValueInt.ToString();
                        case Type.FLOAT:
                            return leftSide.ValueString == rightSide.ValueFloat.ToString();
                        case Type.STRING:
                            return leftSide.ValueString == rightSide.ValueString;
                    }
                    break;
                case Type.BOOLEAN:
                    if (rightSide.type == Type.BOOLEAN)
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
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt != rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt != rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueInt.ToString() != rightSide.ValueString;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat != rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat != rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueFloat.ToString() != rightSide.ValueString;
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueString != rightSide.ValueInt.ToString();
                        case Type.FLOAT:
                            return leftSide.ValueString != rightSide.ValueFloat.ToString();
                        case Type.STRING:
                            return leftSide.ValueString != rightSide.ValueString;
                    }
                    break;
                case Type.BOOLEAN:
                    if (rightSide.type == Type.BOOLEAN)
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

        public static bool operator !(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return !leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do ! operation with type " + leftSide.type.ToString());
        }
        public static bool operator |(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean | rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do | operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }
        public static bool operator &(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean & rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do & operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }
        public static bool operator ^(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean ^ rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do ^ operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }

        public static bool operator true(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }
        public static bool operator false(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }

        #endregion

        public string ToStringValue()
        {
            string retStr = type switch
            {
                Type.INT => ValueInt.ToString(),
                Type.FLOAT => ValueFloat.ToString().Replace(',', '.'),
                Type.STRING => ValueString,
                Type.BOOLEAN => ValueBoolean.ToString(),
                Type.STRUCT => "{ ... }",
                Type.LIST => "[ ... ]",
                _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
            };
            return retStr;
        }

        public override string ToString()
        {
            string retStr = type switch
            {
                Type.INT => ValueInt.ToString(),
                Type.FLOAT => ValueFloat.ToString().Replace(',', '.') + "f",
                Type.STRING => $"\"{ValueString}\"",
                Type.BOOLEAN => ValueBoolean.ToString(),
                Type.STRUCT => $"{ValueStruct.GetType().Name} {{...}}",
                Type.LIST => "[...]",
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
            hash.Add(IsHeapAddress);
            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DataItem item &&
                   type == item.type &&
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
            Type.INT => new DataItem(valueInt, Tag),
            Type.FLOAT => new DataItem(valueFloat, Tag),
            Type.STRING => new DataItem(valueString, Tag),
            Type.BOOLEAN => new DataItem(valueBoolean, Tag),
            Type.STRUCT => new DataItem(valueStruct.Copy(), Tag),
            Type.LIST => new DataItem(valueList.Copy(), Tag),
            _ => throw new InternalException($"Unknown type {type}"),
        };
        public DataItem CopyRecursive() => type switch
        {
            Type.INT => new DataItem(valueInt, Tag),
            Type.FLOAT => new DataItem(valueFloat, Tag),
            Type.STRING => new DataItem(valueString, Tag),
            Type.BOOLEAN => new DataItem(valueBoolean, Tag),
            Type.STRUCT => new DataItem(valueStruct.CopyRecursive(), Tag),
            Type.LIST => new DataItem(valueList.CopyRecursive(), Tag),
            _ => throw new InternalException($"Unknown type {type}"),
        };
    }
    public interface IStruct
    {
        public string Name { get; }
        public bool HaveField(string field);
        public void SetField(string field, DataItem value);
        public DataItem GetField(string field);
        public IStruct Copy();
        public IStruct CopyRecursive();
    }
}
