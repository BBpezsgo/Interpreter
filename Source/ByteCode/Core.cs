using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    using System.Linq;

    #region CPU

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
                case Opcode.JUMP_BY_IF_TRUE: return JUMP_BY_IF_TRUE();
                case Opcode.JUMP_BY_IF_FALSE: return JUMP_BY_IF_FALSE();
                case Opcode.JUMP_BY: return JUMP_BY();
                case Opcode.LOAD_VALUE: return LOAD_VALUE();
                case Opcode.STORE_VALUE: return STORE_VALUE();
                case Opcode.LOAD_VALUE_BR: return LOAD_VALUE_BR();
                case Opcode.LOAD_VALUE_R: return LOAD_VALUE_R();
                case Opcode.STORE_VALUE_BR: return STORE_VALUE_BR();
                case Opcode.STORE_VALUE_R: return STORE_VALUE_R();
                case Opcode.LOAD_FIELD: return LOAD_FIELD();
                case Opcode.STORE_FIELD: return STORE_FIELD();
                case Opcode.LOAD_FIELD_R: return LOAD_FIELD_R();
                case Opcode.STORE_FIELD_R: return STORE_FIELD_R();
                case Opcode.LOAD_FIELD_BR: return LOAD_FIELD_BR();
                case Opcode.STORE_FIELD_BR: return STORE_FIELD_BR();
                case Opcode.CALL: return CALL();
                case Opcode.RETURN: return RETURN();
                case Opcode.POP_VALUE: return POP_VALUE();
                case Opcode.CALL_BUILTIN: return CALL_BUILTIN();
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
                case Opcode.LIST_INDEX: return LIST_INDEX();
                case Opcode.LIST_PUSH_ITEM: return LIST_PUSH_ITEM();
                case Opcode.LIST_SET_ITEM: return LIST_SET_ITEM();
                case Opcode.LIST_ADD_ITEM: return LIST_ADD_ITEM();
                case Opcode.LIST_PULL_ITEM: return LIST_PULL_ITEM();
                case Opcode.LIST_REMOVE_ITEM: return LIST_REMOVE_ITEM();
                case Opcode.TYPE_GET: return TYPE_GET();
                case Opcode.LOGIC_NOT: return LOGIC_NOT();
                case Opcode.HEAP_GET: return HEAP_GET();
                case Opcode.HEAP_SET: return HEAP_SET();
                case Opcode.DEBUG_SET_TAG: return DEBUG_SET_TAG();
                case Opcode.CS_PUSH: return CS_PUSH();
                case Opcode.CS_POP: return CS_POP();
                case Opcode.COPY_VALUE: return COPY_VALUE();
                case Opcode.COPY_VALUE_RECURSIVE: return COPY_VALUE_RECURSIVE();
                #endregion

                default: throw new InternalException("Unimplemented instruction " + MU.CurrentInstruction.opcode.ToString());
            }
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

        int STORE_VALUE_BR()
        {
            var index = (int)MU.CurrentInstruction.parameter + MU.BasePointer;
            var itemToSet = MU.Stack.Get(index).TrySet(MU.Stack.Last());
            MU.Stack.Set(index, itemToSet);

            MU.Stack.Pop();
            MU.Step();

            return 5;
        }
        int LOAD_VALUE_BR()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int STORE_VALUE_R()
        {
            var index = (int)MU.CurrentInstruction.parameter + MU.Stack.Count;
            var itemToSet = MU.Stack.Get(index).TrySet(MU.Stack.Last());
            MU.Stack.Set(index, itemToSet);

            MU.Stack.Pop();
            MU.Step();

            return 5;
        }
        int LOAD_VALUE_R()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.Stack.Count), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int STORE_FIELD_BR()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var valueToSet = MU.Stack.Pop();
            string fieldToSet = MU.CurrentInstruction.additionParameter;
            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer).ValueStruct;

            if (!structItem.HaveField(fieldToSet))
            { throw new RuntimeException("Field " + fieldToSet + " doesn't exists in this struct."); }

            structItem.SetField(fieldToSet, structItem.GetField(fieldToSet).TrySet(valueToSet));

            MU.Stack.Set((int)MU.CurrentInstruction.parameter + MU.BasePointer, new DataItem(structItem, null));

            MU.Step();

            return 6;
        }
        int LOAD_FIELD_BR()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var item = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer);
            var field = MU.CurrentInstruction.additionParameter;

            ProcessField(item, field);

            MU.Step();

            return 4;
        }
        void ProcessField(DataItem item, string field)
        {
            if (item.type == DataItem.Type.STRING)
            {
                var value = item.ValueString;

                if (field == "Length")
                {
                    MU.Stack.Push(value.Length, MU.CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type string does not have field " + field); }
            }
            else if (item.type == DataItem.Type.LIST)
            {
                var value = item.ValueList;

                if (field == "Length")
                {
                    MU.Stack.Push(value.items.Count, MU.CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type list does not have field " + field); }
            }
            else if (item.type == DataItem.Type.STRUCT)
            {
                var value = item.ValueStruct;

                if (!value.HaveField(field))
                { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

                MU.Stack.Push(value.GetField(field), MU.CurrentInstruction.tag);
            }
            else
            { throw new RuntimeException("Type " + item.type.ToString().ToLower() + " does not have field " + field); }

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
            MU.Stack.Set((int)MU.CurrentInstruction.parameter, MU.Stack.Last());

            MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 3;
        }
        int LOAD_VALUE()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int LOAD_FIELD_R()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var item = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.Stack.Count);
            MU.Stack.Pop();
            var field = MU.CurrentInstruction.additionParameter;

            ProcessField(item, field);

            MU.Step();

            return 4;
        }

        int STORE_FIELD_R()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            DataItem valueToSet = MU.Stack.Pop();
            string fieldToSet = MU.CurrentInstruction.additionParameter;
            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.Stack.Count).ValueStruct;

            if (!structItem.HaveField(fieldToSet))
            { throw new RuntimeException("Field " + fieldToSet + " doesn't exists in this struct."); }

            structItem.SetField(fieldToSet, structItem.GetField(fieldToSet).TrySet(valueToSet));

            MU.Stack.Set((int)MU.CurrentInstruction.parameter + MU.Stack.Count, new DataItem(structItem, null));

            MU.Step();

            return 7;
        }

        int STORE_FIELD()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            DataItem valueToSet = MU.Stack.Pop();
            string fieldToSet = MU.CurrentInstruction.additionParameter;
            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter).ValueStruct;

            if (!structItem.HaveField(fieldToSet))
            { throw new RuntimeException("Field " + fieldToSet + " doesn't exists in this struct."); }

            structItem.SetField(fieldToSet, structItem.GetField(fieldToSet).TrySet(valueToSet));

            MU.Stack.Set((int)MU.CurrentInstruction.parameter, new DataItem(structItem, null));

            MU.Step();

            return 7;
        }
        int LOAD_FIELD()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter).ValueStruct;

            if (!structItem.HaveField(MU.CurrentInstruction.additionParameter))
            { throw new RuntimeException("Field " + MU.CurrentInstruction.additionParameter + " doesn't exists in this struct."); }

            MU.Stack.Push(structItem.GetField(MU.CurrentInstruction.additionParameter), "field." + MU.CurrentInstruction.additionParameter);

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

#if false
    internal static class ArithmeticLogicUnit
    {
        internal enum OpCode
        {
            Add,
            Subtract,
            Negate,
            Increment,
            Decrement,
            And,
            Or,
            Xor,
            Not,
        }

        internal static int Process(OpCode opCode, int A, int B)
        {
            return opCode switch
            {
                OpCode.Add => A + B,
                OpCode.Subtract => A - B,
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                OpCode.And => A & B,
                OpCode.Or => A | B,
                OpCode.Xor => A ^ B,
                OpCode.Not => ~A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static bool Process(OpCode opCode, bool A)
        {
            return opCode switch
            {
                OpCode.Not => !A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static bool Process(OpCode opCode, bool A, bool B)
        {
            return opCode switch
            {
                OpCode.And => A & B,
                OpCode.Or => A | B,
                OpCode.Xor => A ^ B,
                OpCode.Not => !A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static float Process(OpCode opCode, float A, float B)
        {
            return opCode switch
            {
                OpCode.Add => A + B,
                OpCode.Subtract => A - B,
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static int Process(OpCode opCode, int A)
        {
            return opCode switch
            {
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                OpCode.Not => ~A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }
    }
#endif

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

    #endregion

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

        internal CentralProcessingUnit cpu;

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
            item.heap = this.cpu.MU.Heap;
            this.stack.Add(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(DataItem value, string tag)
        {
            var item = value;
            item.stack = this;
            item.heap = this.cpu.MU.Heap;
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
            item.heap = this.cpu.MU.Heap;
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
            RUNTIME,
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
                case Type.RUNTIME:
                    return value;
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
                Type.RUNTIME => "<RUNTIME>",
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
            Type.RUNTIME => throw new InternalException($"Unknown type {type}"),
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
            Type.RUNTIME => throw new InternalException($"Unknown type {type}"),
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
