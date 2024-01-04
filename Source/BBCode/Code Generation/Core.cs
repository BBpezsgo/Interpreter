using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LanguageCore.BBCode.Generator
{
    using Compiler;
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

        readonly ExternalFunctionReadonlyCollection ExternalFunctions;
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

        readonly bool TrimUnreachableCode = true;

        bool CanReturn;

        readonly DebugInformation GeneratedDebugInfo;
        readonly Stack<ScopeInformations> CurrentScopeDebug = new();

        #endregion

        public CodeGeneratorForMain(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection) : base(compilerResult, settings, analysisCollection)
        {
            this.ExternalFunctions = compilerResult.ExternalFunctions;
            this.GeneratedCode = new List<Instruction>();
            this.ExternalFunctionsCache = new Dictionary<string, int>();
            this.GeneratedDebugInfo = new DebugInformation();
            this.CleanupStack = new Stack<CleanupItem[]>();
            this.ReturnInstructions = new Stack<List<int>>();
            this.BreakInstructions = new Stack<List<int>>();
            this.UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
            this.UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
            this.UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
            this.InMacro = new Stack<bool>();

            this.TagCount = new Stack<int>();
        }

        BBCodeGeneratorResult GenerateCode(
            CompilerResult compilerResult,
            GeneratorSettings settings,
            PrintCallback? printCallback = null)
        {
            List<string> usedExternalFunctions = new();

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsExternal)
                { usedExternalFunctions.Add(function.ExternalFunctionName); }
            }

            foreach (CompiledOperator @operator in this.CompiledOperators)
            {
                if (@operator.IsExternal)
                { usedExternalFunctions.Add(@operator.ExternalFunctionName); }
            }

            AddInstruction(Opcode.PUSH_VALUE, new DataItem(0));

            if (settings.ExternalFunctionsCache)
            {
                int offset = 0;
                AddComment($"Create external functions cache {{");
                foreach (string function in usedExternalFunctions)
                {
                    AddComment($"Create string \"{function}\" {{");

                    AddInstruction(Opcode.PUSH_VALUE, function.Length + 1);
                    AddInstruction(Opcode.HEAP_ALLOC);

                    ExternalFunctionsCache.Add(function, ExternalFunctionsCache.Count + 1);
                    offset += function.Length;

                    // Prepare value
                    AddInstruction(Opcode.PUSH_VALUE, function.Length);

                    // Calculate pointer
                    AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);

                    // Set value
                    AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);

                    for (int i = 0; i < function.Length; i++)
                    {
                        // Prepare value
                        AddInstruction(Opcode.PUSH_VALUE, new DataItem(function[i]));

                        // Calculate pointer
                        AddInstruction(Opcode.LOAD_VALUE, AddressingMode.StackRelative, -2);
                        AddInstruction(Opcode.PUSH_VALUE, i + 1);
                        AddInstruction(Opcode.MATH_ADD);

                        // Set value
                        AddInstruction(Opcode.HEAP_SET, AddressingMode.Runtime);
                    }

                    AddComment("}");
                }
                AddComment("}");
            }

            CurrentFile = compilerResult.File?.FullName;
            if (CurrentFile == null)
            { AnalysisCollection?.Warnings.Add(new Warning($"{nameof(CurrentFile)} is null", null, null)); }
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

            if (ExternalFunctionsCache.Count > 0)
            {
                AddComment("Clear external functions cache {");
                for (int i = 0; i < ExternalFunctionsCache.Count; i++)
                { AddInstruction(Opcode.HEAP_FREE); }
                AddComment("}");
            }

            AddInstruction(Opcode.EXIT);

            foreach (CompiledFunction function in this.CompiledFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                function.InstructionOffset = GeneratedCode.Count;
                GenerateCodeForFunction(function);
            }

            foreach (CompiledOperator function in this.CompiledOperators)
            {
                if (function.IsTemplate)
                { continue; }

                function.InstructionOffset = GeneratedCode.Count;
                GenerateCodeForFunction(function);
            }

            foreach (CompiledGeneralFunction function in this.CompiledGeneralFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                function.InstructionOffset = GeneratedCode.Count;
                GenerateCodeForFunction(function);
            }

            {
                int i = 0;
                while (i < CompilableFunctions.Count)
                {
                    CompliableTemplate<CompiledFunction> function = CompilableFunctions[i];
                    i++;

                    function.Function.InstructionOffset = GeneratedCode.Count;
                    GenerateCodeForCompilableFunction(function);
                }
            }

            foreach (CompliableTemplate<CompiledOperator> function in this.CompilableOperators)
            {
                function.Function.InstructionOffset = GeneratedCode.Count;
                GenerateCodeForCompilableFunction(function);
            }

            foreach (CompliableTemplate<CompiledGeneralFunction> function in CompilableGeneralFunctions)
            {
                function.Function.InstructionOffset = GeneratedCode.Count;
                GenerateCodeForCompilableFunction(function);
            }

            foreach (UndefinedOffset<CompiledFunction> item in UndefinedFunctionOffsets)
            {
                if (item.Called.InstructionOffset == -1)
                { throw new InternalException($"Function {item.Called.ToReadable()} does not have instruction offset", item.CurrentFile); }

                int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
            }

            foreach (UndefinedOffset<CompiledOperator> item in UndefinedOperatorFunctionOffsets)
            {
                if (item.Called.InstructionOffset == -1)
                { throw new InternalException($"Operator {item.Called.ToReadable()} does not have instruction offset", item.CurrentFile); }

                int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
            }

            foreach (UndefinedOffset<CompiledGeneralFunction> item in UndefinedGeneralFunctionOffsets)
            {
                if (item.Called.InstructionOffset == -1)
                {
                    throw item.Called.Identifier.Content switch
                    {
                        BuiltinFunctionNames.Cloner => new InternalException($"Cloner for \"{item.Called.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.Constructor => new InternalException($"Constructor for \"{item.Called.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.Destructor => new InternalException($"Destructor for \"{item.Called.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.IndexerGet => new InternalException($"Index getter for \"{item.Called.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.IndexerSet => new InternalException($"Index setter for \"{item.Called.Context}\" does not have instruction offset", item.CurrentFile),
                        _ => new NotImplementedException(),
                    };
                }

                int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
            }

            return new BBCodeGeneratorResult()
            {
                Code = GeneratedCode.ToArray(),
                DebugInfo = GeneratedDebugInfo,
            };
        }

        public static BBCodeGeneratorResult Generate(
            CompilerResult compilerResult,
            GeneratorSettings settings,
            PrintCallback? printCallback = null,
            AnalysisCollection? analysisCollection = null)
        {
            UnusedFunctionManager.RemoveUnusedFunctions(
                ref compilerResult,
                settings.RemoveUnusedFunctionsMaxIterations,
                printCallback,
                settings.CompileLevel);

            return new CodeGeneratorForMain(compilerResult, settings, analysisCollection).GenerateCode(compilerResult, settings, printCallback);
        }
    }
}