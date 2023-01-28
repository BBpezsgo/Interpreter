using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class CentralProcessingUnit
    {
        internal readonly ControlUnit CU;
        internal readonly MemoryUnit MU;

        internal Dictionary<string, BuiltinFunction> builtinFunctions;

        public int CodePointer
        {
            get { return MU.CodePointer; }
        }

        public CentralProcessingUnit(Instruction[] code, int basePointer, Dictionary<string, BuiltinFunction> builtinFunctions)
        {
            for (int i = 0; i < code.Length; i++)
            {
                code[i].index = i;
                code[i].cpu = this;
            }
            this.builtinFunctions = builtinFunctions;

            MU = new(
                new DataStack() { cpu = this },
                new HEAP(10),
                code
            )
            {
                BasePointer = basePointer,
                ReturnAddressStack = new List<int>(),
                CodePointer = code.Length
            };
            CU = new(MU, this);
        }

        public int Clock()
        {
            return CU.Process();
        }

        internal static string GetTypeText(DataItem.Type type)
        {
            return type switch
            {
                DataItem.Type.INT => "int",
                DataItem.Type.FLOAT => "float",
                DataItem.Type.STRING => "string",
                DataItem.Type.BOOLEAN => "bool",
                DataItem.Type.STRUCT => "complex",
                _ => "",
            };
        }
        internal static string GetTypeText(DataItem val) => val.type == DataItem.Type.LIST ? $"{(val.ValueList.itemTypes == DataItem.Type.LIST ? "?[]" : GetTypeText(val.ValueList.itemTypes))}[]" : GetTypeText(val.type);

        public void Destroy()
        {
            MU.Stack.Destroy();
            MU.ReturnAddressStack.Clear();
            MU.Stack = null;
            MU.ReturnAddressStack = null;
            this.builtinFunctions = null;
        }
    }

    internal class ControlUnit
    {
        readonly MemoryUnit MU;
        readonly CentralProcessingUnit CPU;

        public ControlUnit(MemoryUnit mu, CentralProcessingUnit cpu)
        {
            MU = mu;
            CPU = cpu;
        }

        internal int Process()
        {
            switch (MU.CurrentInstruction.opcode)
            {
                case Opcode.UNKNOWN: throw new InternalException("Unknown instruction");
                case Opcode.COMMENT: MU.Step(); return 1;

                #region Instructions
                case Opcode.EXIT: return EXIT();

                case Opcode.PUSH_VALUE: return PUSH_VALUE();
                case Opcode.POP_VALUE: return POP_VALUE();

                case Opcode.COPY_VALUE: return COPY_VALUE();
                case Opcode.COPY_VALUE_RECURSIVE: return COPY_VALUE_RECURSIVE();

                case Opcode.LOAD_VALUE: return LOAD_VALUE();
                case Opcode.STORE_VALUE: return STORE_VALUE();

                case Opcode.JUMP_BY_IF_TRUE: return JUMP_BY_IF_TRUE();
                case Opcode.JUMP_BY_IF_FALSE: return JUMP_BY_IF_FALSE();
                case Opcode.JUMP_BY: return JUMP_BY();
                case Opcode.CALL: return CALL();
                case Opcode.RETURN: return RETURN();

                case Opcode.LOAD_FIELD: return LOAD_FIELD();
                case Opcode.STORE_FIELD: return STORE_FIELD();

                case Opcode.CALL_BUILTIN: return CALL_BUILTIN();
                case Opcode.TYPE_GET: return TYPE_GET();

                case Opcode.MATH_ADD: return MATH_ADD();
                case Opcode.MATH_SUB: return MATH_SUB();
                case Opcode.MATH_MULT: return MATH_MULT();
                case Opcode.MATH_DIV: return MATH_DIV();
                case Opcode.MATH_MOD: return MATH_MOD();

                case Opcode.LOGIC_LT: return LOGIC_LT();
                case Opcode.LOGIC_MT: return LOGIC_MT();
                case Opcode.LOGIC_AND: return LOGIC_AND();
                case Opcode.LOGIC_OR: return LOGIC_OR();
                case Opcode.LOGIC_EQ: return LOGIC_EQ();
                case Opcode.LOGIC_NEQ: return LOGIC_NEQ();
                case Opcode.LOGIC_LTEQ: return LOGIC_LTEQ();
                case Opcode.LOGIC_MTEQ: return LOGIC_MTEQ();
                case Opcode.LOGIC_XOR: return LOGIC_XOR();
                case Opcode.LOGIC_NOT: return LOGIC_NOT();

                case Opcode.LIST_INDEX: return LIST_INDEX();
                case Opcode.LIST_PUSH_ITEM: return LIST_PUSH_ITEM();
                case Opcode.LIST_SET_ITEM: return LIST_SET_ITEM();
                case Opcode.LIST_ADD_ITEM: return LIST_ADD_ITEM();
                case Opcode.LIST_PULL_ITEM: return LIST_PULL_ITEM();
                case Opcode.LIST_REMOVE_ITEM: return LIST_REMOVE_ITEM();

                case Opcode.HEAP_GET: return HEAP_GET();
                case Opcode.HEAP_SET: return HEAP_SET();

                case Opcode.DEBUG_SET_TAG: return DEBUG_SET_TAG();
                case Opcode.CS_PUSH: return CS_PUSH();
                case Opcode.CS_POP: return CS_POP();
                #endregion

                default: throw new InternalException("Unimplemented instruction " + MU.CurrentInstruction.opcode.ToString());
            }
        }

        int Address()
        {
            AddressingMode mode = MU.CurrentInstruction.AddressingMode;
            int parameter = (int)MU.CurrentInstruction.parameter;
            return mode switch
            {
                AddressingMode.ABSOLUTE => parameter,
                AddressingMode.BASEPOINTER_RELATIVE => MU.BasePointer + parameter,
                AddressingMode.RELATIVE => MU.Stack.Count + parameter,
                AddressingMode.POP => MU.Stack.Count + parameter,
                _ => parameter,
            };
        }

        #region Instruction Methods

        int COPY_VALUE()
        {
            DataItem itemToCopy = MU.Stack.Pop();
            MU.Stack.Push(itemToCopy.Copy());
            MU.Step();

            return 2;
        }

        int COPY_VALUE_RECURSIVE()
        {
            DataItem itemToCopy = MU.Stack.Pop();
            MU.Stack.Push(itemToCopy.CopyRecursive());
            MU.Step();

            return 3;
        }

        int CS_PUSH()
        {
            MU.CallStack.Push(MU.CurrentInstruction.parameter.ToString());
            MU.Step();

            return 1;
        }

        int CS_POP()
        {
            MU.CallStack.Pop();
            MU.Step();

            return 1;
        }

        int DEBUG_SET_TAG()
        {
            var last = MU.Stack.Last();
            last.Tag = MU.CurrentInstruction.parameter.ToString();
            MU.Stack.Set(MU.Stack.Count - 1, last, true);
            MU.Step();

            return 1;
        }

        int HEAP_GET()
        {
            var v = MU.Heap[(int)MU.CurrentInstruction.parameter];
            MU.Stack.Push(v);
            MU.Step();

            return 2;
        }

        int HEAP_SET()
        {
            var v = MU.Stack.Pop();
            MU.Heap[(int)MU.CurrentInstruction.parameter] = v;
            MU.Step();

            return 2;
        }

        int LOGIC_NOT()
        {
            var v = MU.Stack.Pop();
            MU.Stack.Push(!v);
            MU.Step();

            return 3;
        }

        int TYPE_GET()
        {
            var v = MU.Stack.Pop();
            MU.Stack.Push(CentralProcessingUnit.GetTypeText(v), "type() result");
            MU.Step();

            return 3;
        }

        int LIST_PUSH_ITEM()
        {
            var newItem = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 4;
        }
        int LIST_ADD_ITEM()
        {
            var indexValue = MU.Stack.Pop();
            var newItem = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem, indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 5;
        }
        int LIST_SET_ITEM()
        {
            var indexValue = MU.Stack.Pop().ValueInt;
            var newItem = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.items[indexValue] = newItem; }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Stack.Push(listValue);
            MU.Step();

            return 3;
        }
        int LIST_PULL_ITEM()
        {
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 3;
        }
        int LIST_REMOVE_ITEM()
        {
            var indexValue = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 4;
        }

        int LIST_INDEX()
        {
            var indexValue = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();

            if (listValue.type == DataItem.Type.LIST)
            {
                if (listValue.ValueList.items.Count <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                MU.Stack.Push(listValue.ValueList.items[indexValue.ValueInt]);
            }
            else if (listValue.type == DataItem.Type.STRING)
            {
                if (listValue.ValueString.Length <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                MU.Stack.Push(listValue.ValueString[indexValue.ValueInt].ToString());
            }
            else
            { throw new RuntimeException("The variable type is not list or string!"); }
            MU.Step();

            return 4;
        }

        void OnBuiltinFunctionReturnValue(DataItem returnValue)
        {
            Output.Debug.Debug.Log(returnValue.ToString());
            MU.Stack.Push(returnValue, "return v");
        }

        int CALL_BUILTIN()
        {
            DataItem functionNameDataItem = MU.Stack.Pop();
            if (functionNameDataItem.type != DataItem.Type.STRING)
            { throw new InternalException($"Instruction CALL_BUILTIN need a STRING DataItem parameter from the stack, recived {functionNameDataItem.type} {functionNameDataItem.ToStringValue()}"); }
            string functionName = functionNameDataItem.ValueString;

            if (CPU.builtinFunctions.TryGetValue(functionName, out BuiltinFunction builtinFunction))
            {
                List<DataItem> parameters = new();
                for (int i = 0; i < (int)MU.CurrentInstruction.parameter; i++)
                { parameters.Add(MU.Stack.Pop()); }
                if (builtinFunction.ReturnSomething)
                {
                    builtinFunction.OnReturn += OnBuiltinFunctionReturnValue;
                    builtinFunction.Callback(parameters.ToArray());
                    builtinFunction.OnReturn -= OnBuiltinFunctionReturnValue;
                }
                else
                { builtinFunction.Callback(parameters.ToArray()); }
            }
            else
            { throw new RuntimeException($"Undefined function \"{functionName}\""); }

            MU.Step();

            return 15;
        }

        int LOGIC_MT()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide > rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_EQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide == rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_NEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide != rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_OR()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide | rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_XOR()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide ^ rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_LTEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide <= rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_MTEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide >= rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_AND()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide & rightSide);

            MU.Step();

            return 4;
        }

        int POP_VALUE()
        {
            if (MU.Stack.Count > 0) MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 2;
        }

        int RETURN()
        {
            int returnAddress = MU.ReturnAddressStack[^1];
            MU.ReturnAddressStack.RemoveAt(MU.ReturnAddressStack.Count - 1);
            MU.BasePointer = MU.Stack.Pop().ValueInt;
            MU.CodePointer = returnAddress;

            return 3;
        }
        int CALL()
        {
            MU.Stack.Push(MU.BasePointer, "saved base pointer");
            MU.ReturnAddressStack.Add(MU.CodePointer + 1);
            MU.BasePointer = MU.Stack.Count;
            MU.Step((int)MU.CurrentInstruction.parameter);

            return 4;
        }

        int JUMP_BY()
        {
            MU.CodePointer += (int)MU.CurrentInstruction.parameter;

            return 1;
        }
        int JUMP_BY_IF_TRUE()
        {
            var condition = MU.Stack.Pop();

            if (condition == true)
            { MU.CodePointer += (int)MU.CurrentInstruction.parameter; }
            else
            { MU.Step(); }

            return 3;
        }
        int JUMP_BY_IF_FALSE()
        {
            var condition = MU.Stack.Pop();

            if (condition == false)
            { MU.CodePointer += (int)MU.CurrentInstruction.parameter; }
            else
            { MU.Step(); }

            return 3;
        }

        int STORE_VALUE()
        {
            int address = Address();

            MU.Stack.Set(address, MU.Stack.Last());

            MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 3;
        }
        int LOAD_VALUE()
        {
            int address = Address();

            MU.Stack.Push(MU.Stack.Get(address), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int STORE_FIELD()
        {
            int address = Address();

            string field = MU.Stack.Pop().ValueString;
            if (field.Length == 0)
            { throw new InternalException("No field name given"); }
            DataItem newValue =  MU.Stack.Pop();
            IStruct item = MU.Stack.Get(address).ValueStruct;

            if (!item.HaveField(field))
            { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

            item.SetField(field, item.GetField(field).TrySet(newValue));

            MU.Stack.Set(address, new DataItem(item, null));

            MU.Step();

            return 7;
        }
        int LOAD_FIELD()
        {
            int address = Address();

            string field = MU.Stack.Pop().ValueString;
            if (field.Length == 0)
            { throw new InternalException("No field name given"); }
            DataItem item = MU.CurrentInstruction.AddressingMode == AddressingMode.POP ? MU.Stack.Pop() : MU.Stack.Get(address);

            if (item.type == DataItem.Type.STRING)
            {
                string value = item.ValueString;

                if (field == "Length")
                {
                    MU.Stack.Push(value.Length, MU.CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type string does not have field " + field); }
            }
            else if (item.type == DataItem.Type.LIST)
            {
                DataItem.List value = item.ValueList;

                if (field == "Length")
                {
                    MU.Stack.Push(value.items.Count, MU.CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type list does not have field " + field); }
            }
            else if (item.type == DataItem.Type.STRUCT)
            {
                IStruct value = item.ValueStruct;

                if (!value.HaveField(field))
                { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

                MU.Stack.Push(value.GetField(field), "field." + field);
            }
            else
            { throw new RuntimeException("Type " + item.type.ToString().ToLower() + " does not have field " + field); }

            MU.Step();

            return 4;
        }

        int LOGIC_LT()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide < rightSide);

            MU.Step();

            return 4;
        }

        int PUSH_VALUE()
        {
            if (MU.CurrentInstruction.parameter is DataItem dataItem)
            {
                MU.Stack.Push(dataItem, MU.CurrentInstruction.tag);
            }
            else
            {
                MU.Stack.Push(new DataItem(MU.CurrentInstruction.parameter, MU.CurrentInstruction.tag), MU.CurrentInstruction.tag);
            }

            MU.Step();

            return 2;
        }

        int MATH_ADD()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide + rightSide);

            MU.Step();

            return 4;
        }

        int EXIT()
        {
            MU.CodePointer = MU.Code.Length;

            return 1;
        }

        int MATH_DIV()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide / rightSide);

            MU.Step();

            return 4;
        }

        int MATH_SUB()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide - rightSide);

            MU.Step();

            return 4;
        }

        int MATH_MULT()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide * rightSide);

            MU.Step();

            return 4;
        }

        int MATH_MOD()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide % rightSide);

            MU.Step();

            return 4;
        }

        #endregion
    }

    internal class MemoryUnit
    {
        internal DataStack Stack;
        internal HEAP Heap;
        internal List<int> ReturnAddressStack;
        internal Instruction[] Code;
        internal Stack<string> CallStack;

        internal int CodePointer;
        internal int BasePointer;

        internal Instruction CurrentInstruction => Code[CodePointer];

        public MemoryUnit(DataStack stack, HEAP heap, Instruction[] code)
        {
            Stack = stack;
            Code = code;
            Heap = heap;
            CallStack = new Stack<string>();
        }

        public int End() => Code.Length;

        public void Step() => Step(1);
        public void Step(int num) => CodePointer += num;
    }
}
