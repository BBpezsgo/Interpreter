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

        /// <exception cref="LanguageException"/>
        public readonly void ThrowErrors()
        {
            if (Errors.Length <= 0) return;
            throw Errors[0].ToException();
        }

        public readonly void Print(PrintCallback callback)
        {
            for (int i = 0; i < Errors.Length; i++)
            { callback.Invoke(Errors[i].ToString(), LogType.Error); }

            for (int i = 0; i < Warnings.Length; i++)
            { callback.Invoke(Warnings[i].ToString(), LogType.Warning); }

            for (int i = 0; i < Informations.Length; i++)
            { callback.Invoke(Informations[i].ToString(), LogType.Normal); }

            for (int i = 0; i < Hints.Length; i++)
            { callback.Invoke(Hints[i].ToString(), LogType.Normal); }
        }
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

        readonly ExternalFunctionCollection ExternalFunctions;
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
            this.ExternalFunctions = new ExternalFunctionCollection();
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
            { Warnings.Add(new Warning($"{nameof(CurrentFile)} is null", null, null)); }
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

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
                if (item.Function.InstructionOffset == -1)
                { throw new InternalException($"Function {item.Function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = item.IsAbsoluteAddress ? item.Function.InstructionOffset : item.Function.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
            }

            foreach (UndefinedOffset<CompiledOperator> item in UndefinedOperatorFunctionOffsets)
            {
                if (item.Function.InstructionOffset == -1)
                { throw new InternalException($"Operator {item.Function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = item.IsAbsoluteAddress ? item.Function.InstructionOffset : item.Function.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
            }

            foreach (UndefinedOffset<CompiledGeneralFunction> item in UndefinedGeneralFunctionOffsets)
            {
                if (item.Function.InstructionOffset == -1)
                {
                    throw item.Function.Identifier.Content switch
                    {
                        BuiltinFunctionNames.Cloner => new InternalException($"Cloner for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.Constructor => new InternalException($"Constructor for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.Destructor => new InternalException($"Destructor for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.IndexerGet => new InternalException($"Index getter for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile),
                        BuiltinFunctionNames.IndexerSet => new InternalException($"Index setter for \"{item.Function.Context}\" does not have instruction offset", item.CurrentFile),
                        _ => new NotImplementedException(),
                    };
                }

                int offset = item.IsAbsoluteAddress ? item.Function.InstructionOffset : item.Function.InstructionOffset - item.InstructionIndex;
                GeneratedCode[item.InstructionIndex].Parameter = offset;
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