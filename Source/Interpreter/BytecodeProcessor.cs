using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class BytecodeProcessor
    {
        internal readonly Memory Memory;

        internal Dictionary<string, BuiltinFunction> builtinFunctions;

        internal int CodePointer;
        internal int BasePointer;

        internal Instruction CurrentInstruction => Memory.Code[CodePointer];

        public BytecodeProcessor(Instruction[] code, int basePointer, Dictionary<string, BuiltinFunction> builtinFunctions)
        {
            for (int i = 0; i < code.Length; i++)
            {
                code[i].index = i;
                code[i].cpu = this;
            }
            this.builtinFunctions = builtinFunctions;

            BasePointer = basePointer;
            CodePointer = code.Length;

            Memory = new(
                10,
                code,
                this
            );
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

        public int End() => Memory.Code.Length;

        public void Step() => Step(1);
        public void Step(int num) => CodePointer += num;

        public void Destroy()
        {
            Memory.Stack.Destroy();
            Memory.ReturnAddressStack.Clear();
            Memory.Stack = null;
            Memory.ReturnAddressStack = null;
            this.builtinFunctions = null;
        }

        public int Clock()
        {
            switch (CurrentInstruction.opcode)
            {
                case Opcode.UNKNOWN: throw new InternalException("Unknown instruction");
                case Opcode.COMMENT: Step(); return 1;

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

                default: throw new InternalException("Unimplemented instruction " + CurrentInstruction.opcode.ToString());
            }
        }

        int GetAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => (int)CurrentInstruction.parameter,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + (int)CurrentInstruction.parameter,
            AddressingMode.RELATIVE => Memory.Stack.Count + (int)CurrentInstruction.parameter,
            AddressingMode.POP => Memory.Stack.Count - 1,
            _ => (int)CurrentInstruction.parameter,
        };

        #region Instruction Methods

        int COPY_VALUE()
        {
            DataItem itemToCopy = Memory.Stack.Pop();
            Memory.Stack.Push(itemToCopy.Copy());
            Step();

            return 2;
        }

        int COPY_VALUE_RECURSIVE()
        {
            DataItem itemToCopy = Memory.Stack.Pop();
            Memory.Stack.Push(itemToCopy.CopyRecursive());
            Step();

            return 3;
        }

        int CS_PUSH()
        {
            Memory.CallStack.Push(CurrentInstruction.parameter.ToString());
            Step();

            return 1;
        }

        int CS_POP()
        {
            Memory.CallStack.Pop();
            Step();

            return 1;
        }

        int DEBUG_SET_TAG()
        {
            var last = Memory.Stack.Last();
            last.Tag = CurrentInstruction.parameter.ToString();
            Memory.Stack.Set(Memory.Stack.Count - 1, last, true);
            Step();

            return 1;
        }

        int HEAP_GET()
        {
            var v = Memory.Heap[(int)CurrentInstruction.parameter];
            Memory.Stack.Push(v);
            Step();

            return 2;
        }

        int HEAP_SET()
        {
            var v = Memory.Stack.Pop();
            Memory.Heap[(int)CurrentInstruction.parameter] = v;
            Step();

            return 2;
        }

        int LOGIC_NOT()
        {
            var v = Memory.Stack.Pop();
            Memory.Stack.Push(!v);
            Step();

            return 3;
        }

        int TYPE_GET()
        {
            var v = Memory.Stack.Pop();
            Memory.Stack.Push(BytecodeProcessor.GetTypeText(v), "type() result");
            Step();

            return 3;
        }

        int LIST_PUSH_ITEM()
        {
            var newItem = Memory.Stack.Pop();
            var listValue = Memory.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            Step();

            return 4;
        }
        int LIST_ADD_ITEM()
        {
            var indexValue = Memory.Stack.Pop();
            var newItem = Memory.Stack.Pop();
            var listValue = Memory.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem, indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            Step();

            return 5;
        }
        int LIST_SET_ITEM()
        {
            var indexValue = Memory.Stack.Pop().ValueInt;
            var newItem = Memory.Stack.Pop();
            var listValue = Memory.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.items[indexValue] = newItem; }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            Memory.Stack.Push(listValue);
            Step();

            return 3;
        }
        int LIST_PULL_ITEM()
        {
            var listValue = Memory.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            Step();

            return 3;
        }
        int LIST_REMOVE_ITEM()
        {
            var indexValue = Memory.Stack.Pop();
            var listValue = Memory.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            Step();

            return 4;
        }

        int LIST_INDEX()
        {
            var indexValue = Memory.Stack.Pop();
            var listValue = Memory.Stack.Pop();

            if (listValue.type == DataItem.Type.LIST)
            {
                if (listValue.ValueList.items.Count <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                Memory.Stack.Push(listValue.ValueList.items[indexValue.ValueInt]);
            }
            else if (listValue.type == DataItem.Type.STRING)
            {
                if (listValue.ValueString.Length <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                Memory.Stack.Push(listValue.ValueString[indexValue.ValueInt].ToString());
            }
            else
            { throw new RuntimeException("The variable type is not list or string!"); }
            Step();

            return 4;
        }

        void OnBuiltinFunctionReturnValue(DataItem returnValue)
        {
            Output.Debug.Debug.Log(returnValue.ToString());
            Memory.Stack.Push(returnValue, "return v");
        }

        int CALL_BUILTIN()
        {
            DataItem functionNameDataItem = Memory.Stack.Pop();
            if (functionNameDataItem.type != DataItem.Type.STRING)
            { throw new InternalException($"Instruction CALL_BUILTIN need a STRING DataItem parameter from the stack, recived {functionNameDataItem.type} {functionNameDataItem.ToStringValue()}"); }
            string functionName = functionNameDataItem.ValueString;

            if (builtinFunctions.TryGetValue(functionName, out BuiltinFunction builtinFunction))
            {
                List<DataItem> parameters = new();
                for (int i = 0; i < (int)CurrentInstruction.parameter; i++)
                { parameters.Add(Memory.Stack.Pop()); }
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

            Step();

            return 15;
        }

        int LOGIC_MT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide > rightSide);

            Step();

            return 4;
        }
        int LOGIC_EQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide == rightSide);

            Step();

            return 4;
        }
        int LOGIC_NEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide != rightSide);

            Step();

            return 4;
        }
        int LOGIC_OR()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide | rightSide);

            Step();

            return 4;
        }
        int LOGIC_XOR()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide ^ rightSide);

            Step();

            return 4;
        }
        int LOGIC_LTEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide <= rightSide);

            Step();

            return 4;
        }
        int LOGIC_MTEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide >= rightSide);

            Step();

            return 4;
        }
        int LOGIC_AND()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide & rightSide);

            Step();

            return 4;
        }

        int POP_VALUE()
        {
            if (Memory.Stack.Count > 0) Memory.Stack.RemoveAt(Memory.Stack.Count - 1);
            Step();

            return 2;
        }

        int RETURN()
        {
            int returnAddress = Memory.ReturnAddressStack[^1];
            Memory.ReturnAddressStack.RemoveAt(Memory.ReturnAddressStack.Count - 1);
            BasePointer = Memory.Stack.Pop().ValueInt;
            CodePointer = returnAddress;

            return 3;
        }
        int CALL()
        {
            Memory.Stack.Push(BasePointer, "saved base pointer");
            Memory.ReturnAddressStack.Add(CodePointer + 1);
            BasePointer = Memory.Stack.Count;
            Step((int)CurrentInstruction.parameter);

            return 4;
        }

        int JUMP_BY()
        {
            CodePointer += (int)CurrentInstruction.parameter;

            return 1;
        }
        int JUMP_BY_IF_TRUE()
        {
            var condition = Memory.Stack.Pop();

            if (condition == true)
            { CodePointer += (int)CurrentInstruction.parameter; }
            else
            { Step(); }

            return 3;
        }
        int JUMP_BY_IF_FALSE()
        {
            var condition = Memory.Stack.Pop();

            if (condition == false)
            { CodePointer += (int)CurrentInstruction.parameter; }
            else
            { Step(); }

            return 3;
        }

        int STORE_VALUE()
        {
            int address = GetAddress();

            Memory.Stack.Set(address, Memory.Stack.Last());

            Memory.Stack.RemoveAt(Memory.Stack.Count - 1);
            Step();

            return 3;
        }
        int LOAD_VALUE()
        {
            int address = GetAddress();

            Memory.Stack.Push(Memory.Stack.Get(address), CurrentInstruction.tag);

            Step();

            return 3;
        }

        int STORE_FIELD()
        {
            int address = GetAddress();

            string field = Memory.Stack.Pop().ValueString;
            if (field.Length == 0)
            { throw new InternalException("No field name given"); }
            DataItem newValue = Memory.Stack.Pop();
            IStruct item = Memory.Stack.Get(address).ValueStruct;

            if (!item.HaveField(field))
            { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

            item.SetField(field, item.GetField(field).TrySet(newValue));

            Memory.Stack.Set(address, new DataItem(item, null));

            Step();

            return 7;
        }
        int LOAD_FIELD()
        {
            int address = GetAddress();

            string field = Memory.Stack.Pop().ValueString;
            if (field.Length == 0)
            { throw new InternalException("No field name given"); }
            DataItem item = CurrentInstruction.AddressingMode == AddressingMode.POP ? Memory.Stack.Pop() : Memory.Stack.Get(address);

            if (item.type == DataItem.Type.STRING)
            {
                string value = item.ValueString;

                if (field == "Length")
                {
                    Memory.Stack.Push(value.Length, CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type string does not have field " + field); }
            }
            else if (item.type == DataItem.Type.LIST)
            {
                DataItem.List value = item.ValueList;

                if (field == "Length")
                {
                    Memory.Stack.Push(value.items.Count, CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type list does not have field " + field); }
            }
            else if (item.type == DataItem.Type.STRUCT)
            {
                IStruct value = item.ValueStruct;

                if (!value.HaveField(field))
                { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

                Memory.Stack.Push(value.GetField(field), "field." + field);
            }
            else
            { throw new RuntimeException("Type " + item.type.ToString().ToLower() + " does not have field " + field); }

            Step();

            return 4;
        }

        int LOGIC_LT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide < rightSide);

            Step();

            return 4;
        }

        int PUSH_VALUE()
        {
            if (CurrentInstruction.parameter is DataItem dataItem)
            {
                Memory.Stack.Push(dataItem, CurrentInstruction.tag);
            }
            else
            {
                Memory.Stack.Push(new DataItem(CurrentInstruction.parameter, CurrentInstruction.tag), CurrentInstruction.tag);
            }

            Step();

            return 2;
        }

        int MATH_ADD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide + rightSide);

            Step();

            return 4;
        }

        int EXIT()
        {
            CodePointer = Memory.Code.Length;

            return 1;
        }

        int MATH_DIV()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide / rightSide);

            Step();

            return 4;
        }

        int MATH_SUB()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide - rightSide);

            Step();

            return 4;
        }

        int MATH_MULT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide * rightSide);

            Step();

            return 4;
        }

        int MATH_MOD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide % rightSide);

            Step();

            return 4;
        }

        #endregion
    }

    internal class Memory
    {
        internal DataStack Stack;
        internal HEAP Heap;
        internal List<int> ReturnAddressStack;
        internal Instruction[] Code;
        internal Stack<string> CallStack;

        public Memory(int heapSize, Instruction[] code, BytecodeProcessor processor)
        {
            Code = code;

            Stack = new DataStack(processor);
            Heap = new HEAP(heapSize);

            CallStack = new Stack<string>();
            ReturnAddressStack = new List<int>();
        }
    }
}
