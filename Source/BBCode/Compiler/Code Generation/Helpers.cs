using System;

#pragma warning disable IDE0051 // Remove unused private members

namespace LanguageCore.BBCode.Compiler
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
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
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            if (address.Type.InHEAP)
            { AddInstruction(Opcode.HEAP_GET, AddressingMode.ABSOLUTE, address.MemoryAddress); }
            else
            { LoadFromStack(address.MemoryAddress, address.Type.Size, !address.IsGlobal); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

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
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            ValueAddress offset = GetBaseAddress(address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset.Address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(StatementWithValue address)
        {
            if (FindStatementType(address) != Type.INT)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            GenerateCodeForStatement(address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int Call(int absoluteAddress)
        {
            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

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

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
        {
            if (GetVariable(symbolName, out CompiledVariable variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        bool GetVariable(string variableName, out CompiledVariable compiledVariable)
            => CompiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameter)
            => CompiledParameters.TryGetValue(parameterName, out parameter);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(TypeInstance type, string tag = "")
        {
            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out Type builtinType))
            {
                AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(builtinType), tag);
                return 1;
            }

            CompiledType instanceType = FindType(type);

            if (instanceType.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in instanceType.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (instanceType.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (instanceType.IsEnum)
            {
                if (instanceType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{instanceType.Enum.Identifier.Content}\" initial value: enum has no members", instanceType.Enum.Identifier, instanceType.Enum.FilePath); }

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Enum.Members[0].Value, tag);
                return 1;
            }

            if (instanceType.IsFunction)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, tag);
                return 1;
            }

            throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", type.Identifier, CurrentFile);
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (type.IsStackArray)
            {
                int stackSize = type.StackArraySize;

                int size = 0;
                for (int i = 0; i < stackSize; i++)
                {
                    size += GenerateInitialValue(type.StackArrayOf, $"tag[{i}]");
                }
                return size;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            return 1;
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, Action<int> afterValue, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
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

        #region HEAP Helpers

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

        void StoreToStack(int destination, int size, bool basepointerRelative)
        {
            for (int i = size - 1; i >= 0; i--)
            {
                AddInstruction(Opcode.STORE_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, destination + i);
            }
        }
        void CopyToStack(int to, int size, bool basepointerRelative)
        {
            // TODO: Optimize this

            AddComment($"Copy to stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                int loadFrom = (-currentReversedOffset) - 1;
                int storeTo = to + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, loadFrom);

                AddInstruction(Opcode.STORE_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, storeTo);
            }
            AddComment("}");
        }
        void LoadFromStack(int from, int size, bool basepointerRelative)
        {
            AddComment($"Load from stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                int loadFrom = from + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, loadFrom);
            }
            AddComment("}");
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
        static ValueAddress GetDataAddress(CompiledVariable val)
            => new(val.MemoryAddress, !val.IsGlobal, false, false);
        ValueAddress GetDataAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            {
                return new ValueAddress(val.MemoryAddress, !val.IsGlobal, false, false);
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
            ValueAddress address = GetBaseAddress(indexCall.PrevStatement);
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

            int prevOffset = GetDataOffset(indexCall.PrevStatement);
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
            return new ValueAddress(address, true, parameter.IsRef, false);
        }
        ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
        {
            int address = -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer);
            return new ValueAddress(address, true, parameter.IsRef, false);
        }
        ValueAddress GetBaseAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            {
                return new ValueAddress(val.MemoryAddress, !val.IsGlobal, false, false);
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
            ValueAddress address = GetBaseAddress(statement.PrevStatement);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).InHEAP;
            return new ValueAddress(address.Address, address.BasepointerRelative, address.IsReference, inHeap);
        }

        bool IsItInHeap(StatementWithValue value)
        {
            if (value is Identifier identifier)
            { return false; }

            if (value is Field field)
            { return IsItInHeap(field); }

            if (value is IndexCall indexCall)
            { return IsItInHeap(indexCall); }

            throw new NotImplementedException();
        }
        bool IsItInHeap(IndexCall indexCall)
        {
            return IsItInHeap(indexCall.PrevStatement) || FindStatementType(indexCall.PrevStatement).InHEAP;
        }
        bool IsItInHeap(Field field)
        {
            return IsItInHeap(field.PrevStatement) || FindStatementType(field.PrevStatement).InHEAP;
        }

        #endregion
    }
}