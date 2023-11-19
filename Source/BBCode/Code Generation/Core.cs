using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LanguageCore.BBCode.Generator
{
    using Compiler;
    using Parser.Statement;
    using Runtime;

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct CleanupItem
    {
        public readonly int SizeOnStack;
        public readonly bool ShouldDeallocate;
        public readonly CompiledType? Type;

        public CleanupItem(int size, bool shouldDeallocate, CompiledType? type)
        {
            SizeOnStack = size;
            ShouldDeallocate = shouldDeallocate;
            Type = type;
        }

        public static CleanupItem Null => new(0, false, null);

        public override string ToString()
        {
            if (Type is null && SizeOnStack == 0 && !ShouldDeallocate) return "null";
            return $"({(ShouldDeallocate ? "temp " : string.Empty)}{Type} : {SizeOnStack} bytes)";
        }
        string GetDebuggerDisplay() => ToString();
    }

    public struct BBCodeGeneratorResult
    {
        public Instruction[] Code;
        public DebugInformation DebugInfo;

        public Hint[] Hints;
        public Information[] Informations;
        public Warning[] Warnings;
        public Error[] Errors;
                }

    public partial class CodeGeneratorForMain : CodeGenerator
    {
        /*
         *      
         *      === Stack Structure ===
         *      
         *        -- ENTRY --
         *      
         *        ? ... pointers ... (external function cache) > ExternalFunctionsCache.Count
         *      
         *        ? ... variables ... (global variables)
         *        
         *        -- CALL --
         *      
         *   -5    return value
         *      
         *   -4    ? parameter "this"    \ ParametersSize()
         *   -3    ? ... parameters ...  /
         *      
         *   -2    saved code pointer
         *   -1    saved base pointer
         *   
         *   >> 
         *   
         *   0    return flag
         *   
         *   1    ? ... variables ... (locals)
         *   
         */

        #region Fields

        readonly Dictionary<string, ExternalFunctionBase> ExternalFunctions;
        readonly Dictionary<string, int> ExternalFunctionsCache;

        readonly Stack<CleanupItem[]> CleanupStack;
        IAmInContext<CompiledClass>? CurrentContext;

        readonly Stack<List<int>> ReturnInstructions;
        readonly Stack<List<int>> BreakInstructions;
        readonly Stack<bool> InMacro;

        readonly List<Instruction> GeneratedCode;

        readonly List<UndefinedOffset<CompiledFunction>> UndefinedFunctionOffsets;
        readonly List<UndefinedOffset<CompiledOperator>> UndefinedOperatorFunctionOffsets;
        readonly List<UndefinedOffset<CompiledGeneralFunction>> UndefinedGeneralFunctionOffsets;

        readonly bool OptimizeCode;
        readonly bool CheckNullPointers;
        readonly bool TrimUnreachableCode = true;

        bool CanReturn;

        readonly List<Information> Informations;
        readonly DebugInformation GeneratedDebugInfo;
        readonly Stack<ScopeInformations> CurrentScopeDebug = new();

        #endregion

        public CodeGeneratorForMain(CompilerSettings settings) : base()
        {
            this.ExternalFunctions = new Dictionary<string, ExternalFunctionBase>();
            this.CheckNullPointers = settings.CheckNullPointers;
            this.GeneratedCode = new List<Instruction>();
            this.ExternalFunctionsCache = new Dictionary<string, int>();
            this.OptimizeCode = !settings.DontOptimize;
            this.GeneratedDebugInfo = new DebugInformation();
            this.CleanupStack = new Stack<CleanupItem[]>();
            this.ReturnInstructions = new Stack<List<int>>();
            this.BreakInstructions = new Stack<List<int>>();
            this.UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
            this.UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
            this.UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
            this.InMacro = new Stack<bool>();

            this.TagCount = new Stack<int>();

            this.Informations = new List<Information>();
        }

        BBCodeGeneratorResult GenerateCode(
            CompilerResult compilerResult,
            CompilerSettings settings,
            PrintCallback? printCallback = null,
            CompileLevel level = CompileLevel.Minimal)
        {
            base.CompiledClasses = compilerResult.Classes;
            base.CompiledStructs = compilerResult.Structs;
            this.ExternalFunctions.AddRange(compilerResult.ExternalFunctions);
            base.CompiledEnums = compilerResult.Enums;
            base.CompiledMacros = compilerResult.Macros;

            (
                this.CompiledFunctions,
                this.CompiledOperators,
                this.CompiledGeneralFunctions
            ) = UnusedFunctionManager.RemoveUnusedFunctions(
                    compilerResult,
                    settings.RemoveUnusedFunctionsMaxIterations,
                    printCallback,
                    level
                    );

            CompiledFunction? codeEntry = GetCodeEntry();

            List<string> UsedExternalFunctions = new();

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsExternal)
                { UsedExternalFunctions.Add(function.ExternalFunctionName); }
            }

            foreach (CompiledOperator @operator in this.CompiledOperators)
            {
                if (@operator.IsExternal)
                { UsedExternalFunctions.Add(@operator.ExternalFunctionName); }
            }

            if (settings.ExternalFunctionsCache)
            {
                int offset = 0;
                AddComment($"Create external functions cache {{");
                foreach (string function in UsedExternalFunctions)
                {
                    AddComment($"Create string \"{function}\" {{");

                    AddInstruction(Opcode.PUSH_VALUE, function.Length + 1);
                    AddInstruction(Opcode.HEAP_ALLOC);

                    ExternalFunctionsCache.Add(function, ExternalFunctionsCache.Count);
                    offset += function.Length;

                    {
                        // Prepare value
                        AddInstruction(Opcode.PUSH_VALUE, function.Length);

                        // Calculate pointer
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);

                        // Set value
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                    }

                    for (int i = 0; i < function.Length; i++)
                    {
                        // Prepare value
                        AddInstruction(Opcode.PUSH_VALUE, new DataItem(function[i]));

                        // Calculate pointer
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -2);
                        AddInstruction(Opcode.PUSH_VALUE, i + 1);
                        AddInstruction(Opcode.MATH_ADD);

                        // Set value
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                    }

                    AddComment("}");
                }
                AddComment("}");
            }

            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

            int entryCallInstruction = -1;
            if (codeEntry != null)
            { entryCallInstruction = Call(-1); }

            if (ExternalFunctionsCache.Count > 0)
            {
                AddComment("Clear external functions cache {");
                for (int i = 0; i < ExternalFunctionsCache.Count; i++)
                { AddInstruction(Opcode.HEAP_DEALLOC); }
                AddComment("}");
            }

            AddInstruction(Opcode.EXIT);

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                InFunction = true;
                function.InstructionOffset = GeneratedCode.Count;

                foreach (Parser.FunctionDefinition.Attribute attribute in function.Attributes)
                {
                    if (attribute.Identifier.Content != "CodeEntry") continue;
                    GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                }

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }"));
                GenerateCodeForFunction(function);
                if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

                CurrentContext = null;
                InFunction = false;
            }

            if (codeEntry != null && GeneratedCode[entryCallInstruction].ParameterInt == -1)
            { throw new InternalException($"Failed to set code entry call instruction's parameter", CurrentFile); }

            foreach (CompiledOperator function in this.CompiledOperators)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                InFunction = true;
                function.InstructionOffset = GeneratedCode.Count;

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }"));
                GenerateCodeForFunction(function);
                if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

                CurrentContext = null;
                InFunction = false;
            }

            foreach (CompiledGeneralFunction function in this.CompiledGeneralFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                InFunction = true;
                function.InstructionOffset = GeneratedCode.Count;

                AddComment(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Length > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function);

                if (function.Block != null && function.Block.Statements.Length > 0) AddComment("}");

                CurrentContext = null;
                InFunction = false;
            }

            {
                int i = 0;
                while (i < CompilableFunctions.Count)
                {
                    CompliableTemplate<CompiledFunction> function = CompilableFunctions[i];
                    i++;

                    CurrentContext = function.Function;
                    InFunction = true;
                    function.Function.InstructionOffset = GeneratedCode.Count;

                    foreach (Parser.FunctionDefinition.Attribute attribute in function.Function.Attributes)
                    {
                        if (attribute.Identifier.Content != "CodeEntry") continue;
                        GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                    }

                    SetTypeArguments(function.TypeArguments);

                    AddComment(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Length > 0) ? string.Empty : " }"));

                    GenerateCodeForFunction(function.Function);

                    if (function.Function.Block != null && function.Function.Block.Statements.Length > 0) AddComment("}");

                    CurrentContext = null;
                    InFunction = false;
                    TypeArguments.Clear();
                }
            }

            foreach (CompliableTemplate<CompiledOperator> function in this.CompilableOperators)
            {
                CurrentContext = function.Function;
                InFunction = true;
                function.Function.InstructionOffset = GeneratedCode.Count;

                SetTypeArguments(function.TypeArguments);

                AddComment(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Length > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Block != null && function.Function.Block.Statements.Length > 0) AddComment("}");

                CurrentContext = null;
                InFunction = false;
                TypeArguments.Clear();
            }

            for (int i = 0; i < CompilableGeneralFunctions.Count; i++)
            {
                CompliableTemplate<CompiledGeneralFunction> function = this.CompilableGeneralFunctions[i];
                CurrentContext = function.Function;
                InFunction = true;
                function.Function.InstructionOffset = GeneratedCode.Count;

                SetTypeArguments(function.TypeArguments);

                AddComment(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Length > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Block != null && function.Function.Block.Statements.Length > 0) AddComment("}");

                CurrentContext = null;
                InFunction = false;
                TypeArguments.Clear();
            }

            foreach (UndefinedOffset<CompiledFunction> item in UndefinedFunctionOffsets)
            {
                CompiledFunction? function = item.Function;
                if (function is null) throw new InternalException();

                bool useAbsolute;
                if (item.Caller is FunctionCall)
                { useAbsolute = false; }
                else if (item.Caller is Identifier)
                { useAbsolute = true; }
                else if (item.Caller is IndexCall)
                { useAbsolute = false; }
                else
                { throw new InternalException(); }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = useAbsolute ? function.InstructionOffset : function.InstructionOffset - item.CallInstructionIndex;
                GeneratedCode[item.CallInstructionIndex].ParameterInt = offset;
            }

            foreach (UndefinedOffset<CompiledOperator> item in UndefinedOperatorFunctionOffsets)
            {
                if (item.Function.InstructionOffset == -1)
                { throw new InternalException($"Operator {item.Function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                GeneratedCode[item.CallInstructionIndex].ParameterInt = item.Function.InstructionOffset - item.CallInstructionIndex;
            }

            foreach (UndefinedOffset<CompiledGeneralFunction> item in UndefinedGeneralFunctionOffsets)
            {
                if (item.Caller == null) { }
                else if (item.Caller is ConstructorCall constructorCall)
                {
                    if (item.Function.InstructionOffset == -1)
                    { throw new InternalException($"Constructor for type \"{constructorCall.TypeName}\" does not have instruction offset", item.CurrentFile); }
                }
                else if (item.Caller is KeywordCall functionCall)
                {
                    if (functionCall.Identifier.Content == "delete")
                    {
                        if (item.Function.InstructionOffset == -1)
                        { throw new InternalException($"Constructor for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile); }
                    }
                    else if (functionCall.Identifier.Content == "clone")
                    {
                        if (item.Function.InstructionOffset == -1)
                        { throw new InternalException($"Cloner for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile); }
                    }
                    /*
                    else if (functionCall.Identifier.Content == "out")
                    {
                        if (item.Function.InstructionOffset == -1)
                        { throw new InternalException($"Function {item.Function.ReadableID()} does not have instruction offset", item.CurrentFile); }
                    }
                    */
                    else
                    { throw new NotImplementedException(); }
                }
                else
                { throw new NotImplementedException(); }

                GeneratedCode[item.CallInstructionIndex].ParameterInt = item.Function.InstructionOffset - item.CallInstructionIndex;
            }

            return new BBCodeGeneratorResult()
            {
                Code = GeneratedCode.ToArray(),
                DebugInfo = GeneratedDebugInfo,

                Hints = Hints.ToArray(),
                Informations = Informations.ToArray(),
                Warnings = Warnings.ToArray(),
                Errors = Errors.ToArray(),
            };
        }

        public static BBCodeGeneratorResult Generate(
            CompilerResult compilerResult,
            CompilerSettings settings,
            PrintCallback? printCallback = null,
            CompileLevel level = CompileLevel.Minimal)
            => new CodeGeneratorForMain(settings).GenerateCode(compilerResult, settings, printCallback, level);
    }
}