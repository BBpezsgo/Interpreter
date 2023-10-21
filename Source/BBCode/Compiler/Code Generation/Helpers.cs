using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.BBCode.Compiler
{
    using System.Diagnostics.CodeAnalysis;
    using LanguageCore.Runtime;
    using Parser;
    using Parser.Statement;

    public partial class CodeGenerator : CodeGeneratorBase
    {
        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        readonly struct ValueAddress
        {
            public readonly int Address;
            public readonly bool BasepointerRelative;
            public readonly bool IsReference;
            public readonly bool InHeap;
            public AddressingMode AddressingMode => BasepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE;

            public ValueAddress(int address, bool basepointerRelative, bool isReference, bool inHeap)
            {
                Address = address;
                BasepointerRelative = basepointerRelative;
                IsReference = isReference;
                InHeap = inHeap;
            }

            public ValueAddress(CompiledVariable variable)
            {
                Address = variable.MemoryAddress;
                BasepointerRelative = !variable.IsGlobal;
                IsReference = false;
                InHeap = false;
            }

            public ValueAddress(CompiledParameter parameter, int address)
            {
                Address = address;
                BasepointerRelative = true;
                IsReference = parameter.IsRef;
                InHeap = false;
            }

            public static ValueAddress operator +(ValueAddress address, int offset) => new(address.Address + offset, address.BasepointerRelative, address.IsReference, address.InHeap);

            public override string ToString()
            {
                StringBuilder result = new();
                result.Append('(');
                result.Append($"{Address}");
                if (BasepointerRelative)
                { result.Append(" (BPR)"); }
                else
                { result.Append(" (ABS)"); }
                if (IsReference)
                { result.Append(" | IsRef"); }
                if (InHeap)
                { result.Append(" | InHeap"); }
                result.Append(')');
                return result.ToString();
            }
            string GetDebuggerDisplay() => ToString();
        }

        #region Helper Functions

        int CallRuntime(CompiledVariable address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            if (address.Type.InHEAP)
            { AddInstruction(Opcode.HEAP_GET, AddressingMode.ABSOLUTE, address.MemoryAddress); }
            else
            { StackLoad(new ValueAddress(address), address.Type.SizeOnStack); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(CompiledParameter address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            ValueAddress offset = GetBaseAddress(address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset.Address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(StatementWithValue address)
        {
            CompiledType addressType = FindStatementType(address);

            if (addressType != Type.INT && !addressType.IsFunction)
            { throw new CompilerException($"This should be an \"int\" or function pointer and not \"{addressType}\"", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0); // Saved code pointer

            AddInstruction(Opcode.GET_BASEPOINTER); // Saved base pointer

            GenerateCodeForStatement(address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);
            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int Call(int absoluteAddress)
        {
            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.ABSOLUTE, absoluteAddress - GeneratedCode.Count);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        void Return()
        {
            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RUNTIME, 0);
            AddInstruction(Opcode.SET_CODEPOINTER, AddressingMode.RUNTIME);
        }

        protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out CompiledType? type)
        {
            if (GetVariable(symbolName, out CompiledVariable? variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter? parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledVariable? compiledVariable)
            => CompiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, [NotNullWhen(true)] out CompiledParameter? parameter)
            => CompiledParameters.TryGetValue(parameterName, out parameter);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(TypeInstance type)
        {
            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out Type builtinType))
            {
                AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(builtinType));
                return 1;
            }

            CompiledType instanceType = FindType(type);

            if (instanceType.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in instanceType.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type));
                }
                return size;
            }

            if (instanceType.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER);
                return 1;
            }

            if (instanceType.IsEnum)
            {
                if (instanceType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{instanceType.Enum.Identifier.Content}\" initial value: enum has no members", instanceType.Enum.Identifier, instanceType.Enum.FilePath); }

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Enum.Members[0].Value);
                return 1;
            }

            if (instanceType.IsFunction)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER);
                return 1;
            }

            throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", type.Identifier, CurrentFile);
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size += GenerateInitialValue(field.Type);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER);
                return 1;
            }

            if (type.IsStackArray)
            {
                int stackSize = type.StackArraySize;

                int size = 0;
                for (int i = 0; i < stackSize; i++)
                {
                    size += GenerateInitialValue(type.StackArrayOf);
                }
                return size;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type));
            return 1;
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, Action<int> afterValue)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type));
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER);
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type));
            afterValue?.Invoke(0);
            return 1;
        }

        int ParametersSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledParameters.Count; i++)
                {
                    sum += CompiledParameters[i].Type.SizeOnStack;
                }

                return sum;
            }
        }
        int ParametersSizeBefore(int beforeThis)
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                if (CompiledParameters[i].Index < beforeThis) continue;

                sum += CompiledParameters[i].Type.SizeOnStack;
            }

            return sum;
        }

        #endregion

        #region Memory Helpers

        void StackStore(ValueAddress address, int size)
        {
            for (int i = size - 1; i >= 0; i--)
            {
                StackStore(address + i);
                // AddInstruction(Opcode.STORE_VALUE, address.AddressingMode, address.Address + i);
            }
        }
        void StackLoad(ValueAddress address, int size)
        {
            AddComment($"Load from stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");
                StackLoad(address + currentOffset);
            }
            AddComment("}");
        }

        void StackLoad(ValueAddress address)
        {
            if (address.IsReference)
            {
                AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME);
                throw new NotImplementedException();
            }

            if (address.InHeap)
            {
                throw new NotImplementedException();
            }

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                case AddressingMode.BASEPOINTER_RELATIVE:
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                    break;

                case AddressingMode.POP:
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode);
                    break;

                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode);
                    break;
                default: throw new ImpossibleException();
            }
        }
        void StackStore(ValueAddress address)
        {
            if (address.IsReference)
            {
                throw new NotImplementedException();
            }

            if (address.InHeap)
            {
                throw new NotImplementedException();
            }

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.ABSOLUTE, address.Address);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, address.Address);
                    break;
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, address.Address);
                    break;
                case AddressingMode.POP:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RUNTIME);
                    break;
                default: throw new ImpossibleException();
            }
        }

        void BoxData(int to, int size)
        {
            AddComment($"Box: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, (-currentReversedOffset) - 1);

                AddInstruction(Opcode.PUSH_VALUE, to + size);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }
        void UnboxData(int from, int size)
        {
            AddComment($"Unbox: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }

        void HeapLoad(ValueAddress pointerAddress, int offset)
        {
            StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.BasepointerRelative, pointerAddress.IsReference, false));
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void HeapStore(ValueAddress pointerAddress, int offset)
        {
            StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.BasepointerRelative, pointerAddress.IsReference, false));
            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }

        #endregion

        #region Addressing Helpers

        public const int TagsBeforeBasePointer = 2;

        public int ReturnValueOffset => -(ParametersSize + 1 + TagsBeforeBasePointer);
        public const int ReturnFlagOffset = 0;
        public const int SavedBasePointerOffset = -1;
        public const int SavedCodePointerOffset = -2;

        ValueAddress GetDataAddress(StatementWithValue value)
        {
            if (value is IndexCall indexCall)
            { return GetDataAddress(indexCall); }

            if (value is Identifier identifier)
            { return GetDataAddress(identifier); }

            if (value is Field field)
            { return GetDataAddress(field); }

            throw new NotImplementedException();
        }
        ValueAddress GetDataAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable? val))
            {
                return new ValueAddress(val);
            }

            throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
        }
        ValueAddress GetDataAddress(Field field)
        {
            ValueAddress address = GetBaseAddress(field);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int offset = GetDataOffset(field);
            return new ValueAddress(address.Address + offset, address.BasepointerRelative, address.IsReference, address.InHeap);
        }
        ValueAddress GetDataAddress(IndexCall indexCall)
        {
            ValueAddress address = GetBaseAddress(indexCall.PrevStatement!);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int currentOffset = GetDataOffset(indexCall);
            return new ValueAddress(address.Address + currentOffset, address.BasepointerRelative, address.IsReference, address.InHeap);
        }

        int GetDataOffset(StatementWithValue value)
        {
            if (value is IndexCall indexCall)
            { return GetDataOffset(indexCall); }

            if (value is Field field)
            { return GetDataOffset(field); }

            if (value is Identifier)
            { return 0; }

            throw new NotImplementedException();
        }
        int GetDataOffset(Field field)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            IReadOnlyDictionary<string, int> fieldOffsets;

            if (prevType.IsStruct)
            {
                fieldOffsets = prevType.Struct.FieldOffsets;
            }
            else if (prevType.IsClass)
            {
                prevType.Class.AddTypeArguments(TypeArguments);
                prevType.Class.AddTypeArguments(prevType.TypeParameters);

                fieldOffsets = prevType.Class.FieldOffsets;

                prevType.Class.ClearTypeArguments();
            }
            else
            { throw new NotImplementedException(); }

            if (!fieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
            { throw new InternalException($"Field \"{field.FieldName}\" does not have an offset value", CurrentFile); }

            int prevOffset = GetDataOffset(field.PrevStatement);
            return prevOffset + fieldOffset;
        }
        int GetDataOffset(IndexCall indexCall)
        {
            CompiledType prevType = FindStatementType(indexCall.PrevStatement);

            if (!prevType.IsStackArray)
            { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

            if (!TryCompute(indexCall.Expression, RuntimeType.INT, out DataItem index))
            { throw new CompilerException($"Can't compute the index value", indexCall.Expression, CurrentFile); }

            int prevOffset = GetDataOffset(indexCall.PrevStatement!);
            int offset = index.ValueInt * prevType.StackArrayOf.SizeOnStack;
            return prevOffset + offset;
        }

        ValueAddress GetBaseAddress(StatementWithValue statement)
        {
            if (statement is Identifier identifier)
            { return GetBaseAddress(identifier); }

            if (statement is Field field)
            { return GetBaseAddress(field); }

            if (statement is IndexCall indexCall)
            { return GetBaseAddress(indexCall); }

            throw new NotImplementedException();
        }
        ValueAddress GetBaseAddress(CompiledParameter parameter)
        {
            int address = -(ParametersSizeBefore(parameter.Index) + TagsBeforeBasePointer);
            return new ValueAddress(parameter, address);
        }
        ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
        {
            int address = -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer);
            return new ValueAddress(parameter, address);
        }
        ValueAddress GetBaseAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable? val))
            {
                return new ValueAddress(val);
            }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        ValueAddress GetBaseAddress(Field statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).InHEAP;
            return new ValueAddress(address.Address, address.BasepointerRelative, address.IsReference, inHeap);
        }
        ValueAddress GetBaseAddress(IndexCall statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement!);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).InHEAP;
            return new ValueAddress(address.Address, address.BasepointerRelative, address.IsReference, inHeap);
        }

        bool IsItInHeap(StatementWithValue value)
        {
            if (value is Identifier)
            { return false; }

            if (value is Field field)
            { return IsItInHeap(field); }

            if (value is IndexCall indexCall)
            { return IsItInHeap(indexCall); }

            throw new NotImplementedException();
        }
        bool IsItInHeap(IndexCall indexCall)
        {
            return IsItInHeap(indexCall.PrevStatement!) || FindStatementType(indexCall.PrevStatement).InHEAP;
        }
        bool IsItInHeap(Field field)
        {
            return IsItInHeap(field.PrevStatement) || FindStatementType(field.PrevStatement).InHEAP;
        }

        #endregion
    }
}