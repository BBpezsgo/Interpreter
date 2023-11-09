using System;
using System.Collections.Generic;

namespace LanguageCore.BBCode.Compiler
{
    using System.Diagnostics;
    using LanguageCore.Runtime;
    using Parser.Statement;

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct CleanupItem
    {
        /// <summary>
        /// Data size on the stack
        /// </summary>
        public readonly int Size;

        public readonly bool ShouldDeallocate;

        public readonly CompiledType? Type;

        public CleanupItem(int size, bool shouldDeallocate, CompiledType? type)
        {
            Size = size;
            ShouldDeallocate = shouldDeallocate;
            Type = type;
        }

        public static CleanupItem Null => new(0, false, null);

        public override string ToString()
        {
            if (Type is null && Size == 0 && !ShouldDeallocate) return "null";
            return $"({(ShouldDeallocate ? "temp " : string.Empty)}{Type} : {Size} bytes)";
        }
        string GetDebuggerDisplay() => ToString();
    }

    public partial class CodeGenerator : CodeGeneratorBase
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
        readonly List<CompiledParameter> CompiledParameters;
        readonly List<CompiledVariable> CompiledVariables;

        readonly Stack<List<int>> ReturnInstructions;
        readonly Stack<List<int>> BreakInstructions;
        readonly Stack<bool> InMacro;

        readonly List<Instruction> GeneratedCode;

        readonly List<UndefinedFunctionOffset> UndefinedFunctionOffsets;
        readonly List<UndefinedOperatorFunctionOffset> UndefinedOperatorFunctionOffsets;
        readonly List<UndefinedGeneralFunctionOffset> UndefinedGeneralFunctionOffsets;

        readonly bool OptimizeCode;
        readonly bool CheckNullPointers;
        readonly bool TrimUnreachableCode = true;

        bool CanReturn;

        readonly List<Information> Informations;
        readonly DebugInformation GeneratedDebugInfo;
        readonly Stack<ScopeInformations> CurrentScopeDebug = new();

        /// <summary>
        /// Used for keep track of local (after base pointer) tag count that are not variables.
        /// <br/>
        /// ie.:
        /// <br/>
        /// <c>Return Flag</c>
        /// </summary>
        readonly Stack<int> TagCount;

        #endregion

        public CodeGenerator(Compiler.CompilerSettings settings) : base()
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
            this.UndefinedFunctionOffsets = new List<UndefinedFunctionOffset>();
            this.UndefinedOperatorFunctionOffsets = new List<UndefinedOperatorFunctionOffset>();
            this.UndefinedGeneralFunctionOffsets = new List<UndefinedGeneralFunctionOffset>();
            this.InMacro = new Stack<bool>();

            this.TagCount = new Stack<int>();

            this.CompiledVariables = new List<CompiledVariable>();
            this.CompiledParameters = new List<CompiledParameter>();

            this.Informations = new List<Information>();
        }

        public struct Result
        {
            public Instruction[] Code;
            public DebugInformation DebugInfo;

            public CompiledFunction[] Functions;
            public CompiledOperator[] Operators;
            public CompiledGeneralFunction[] GeneralFunctions;

            public CompiledStruct[] Structs;
            public CompiledClass[] Classes;

            public Hint[] Hints;
            public Information[] Informations;
            public Warning[] Warnings;
            public Error[] Errors;

            public readonly bool GetFunctionOffset(CompiledFunction compiledFunction, out int offset)
            {
                offset = -1;
                for (int i = 0; i < Functions.Length; i++)
                {
                    if (Functions[i].IsSame(compiledFunction))
                    {
                        if (offset != -1)
                        { throw new InternalException($"BRUH"); }
                        offset = i;
                    }
                }
                return offset != -1;
            }

            public readonly void PrintInstructions() => Result.PrintInstructions(Code);
            public static void PrintInstructions(Instruction[] code)
            {
                Console.WriteLine("\n\r === INSTRUCTIONS ===\n\r");
                int indent = 0;

                for (int i = 0; i < code.Length; i++)
                {
                    Instruction instruction = code[i];
                    /*
                    if (instruction.opcode == Opcode.COMMENT)
                    {
                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.tag}");
                        Console.ResetColor();

                        if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }
                    */

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{new string(' ', indent * 2)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.Parameter.Type == RuntimeType.SInt32)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueSInt32}");
                        Console.Write($" ");
                    }
                    else if (instruction.Parameter.Type == RuntimeType.Single)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.Parameter.ValueSingle}");
                        Console.Write($" ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{instruction.Parameter}");
                        Console.Write($" ");
                    }

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("\n\r === ===\n\r");
            }
        }

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            PrintCallback? printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
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

            foreach (var function in this.CompiledFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                foreach (var attribute in function.Attributes)
                {
                    if (attribute.Identifier.Content != "CodeEntry") continue;
                    GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                }

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Count > 0) ? string.Empty : " }"));
                GenerateCodeForFunction(function);
                if (function.Block != null && function.Block.Statements.Count > 0) AddCommentForce("}");
                CurrentContext = null;
            }

            if (codeEntry != null && GeneratedCode[entryCallInstruction].ParameterInt == -1)
            { throw new InternalException($"Failed to set code entry call instruction's parameter", CurrentFile); }

            foreach (var function in this.CompiledOperators)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Count > 0) ? string.Empty : " }"));
                GenerateCodeForFunction(function);
                if (function.Block != null && function.Block.Statements.Count > 0) AddCommentForce("}");
                CurrentContext = null;
            }

            foreach (var function in this.CompiledGeneralFunctions)
            {
                if (function.IsTemplate)
                { continue; }

                CurrentContext = function;
                function.InstructionOffset = GeneratedCode.Count;

                AddCommentForce(function.Identifier.Content + ((function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Block == null || function.Block.Statements.Count > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function);

                if (function.Block != null && function.Block.Statements.Count > 0) AddCommentForce("}");

                CurrentContext = null;
            }

            {
                int i = 0;
                while (i < CompilableFunctions.Count)
                {
                    CompliableTemplate<CompiledFunction> function = CompilableFunctions[i];
                    i++;

                    CurrentContext = function.Function;
                    function.Function.InstructionOffset = GeneratedCode.Count;

                    foreach (var attribute in function.Function.Attributes)
                    {
                        if (attribute.Identifier.Content != "CodeEntry") continue;
                        GeneratedCode[entryCallInstruction].ParameterInt = GeneratedCode.Count - entryCallInstruction;
                    }

                    SetTypeArguments(function.TypeArguments);

                    AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Count > 0) ? string.Empty : " }"));

                    GenerateCodeForFunction(function.Function);

                    if (function.Function.Block != null && function.Function.Block.Statements.Count > 0) AddCommentForce("}");

                    CurrentContext = null;
                    TypeArguments.Clear();
                }
            }

            foreach (var function in this.CompilableOperators)
            {
                CurrentContext = function.Function;
                function.Function.InstructionOffset = GeneratedCode.Count;

                SetTypeArguments(function.TypeArguments);

                AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Count > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Block != null && function.Function.Block.Statements.Count > 0) AddCommentForce("}");

                CurrentContext = null;
                TypeArguments.Clear();
            }

            foreach (var function in this.CompilableGeneralFunctions)
            {
                CurrentContext = function.Function;
                function.Function.InstructionOffset = GeneratedCode.Count;

                SetTypeArguments(function.TypeArguments);

                AddCommentForce(function.Function.Identifier.Content + ((function.Function.Parameters.Length > 0) ? "(...)" : "()") + " {" + ((function.Function.Block == null || function.Function.Block.Statements.Count > 0) ? string.Empty : " }"));

                GenerateCodeForFunction(function.Function);

                if (function.Function.Block != null && function.Function.Block.Statements.Count > 0) AddCommentForce("}");

                CurrentContext = null;
                TypeArguments.Clear();
            }

            foreach (UndefinedFunctionOffset item in UndefinedFunctionOffsets)
            {
                CompiledFunction? function = item.Function;
                if (function is null) throw new InternalException();
                bool useAbsolute;

                if (item.CallStatement != null)
                { useAbsolute = false; }
                else if (item.VariableStatement != null)
                { useAbsolute = true; }
                else if (item.IndexStatement != null)
                { useAbsolute = false; }
                else
                { throw new InternalException(); }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function {function.ReadableID()} does not have instruction offset", item.CurrentFile); }

                int offset = useAbsolute ? function.InstructionOffset : function.InstructionOffset - item.CallInstructionIndex;
                GeneratedCode[item.CallInstructionIndex].ParameterInt = offset;
            }

            foreach (UndefinedOperatorFunctionOffset item in UndefinedOperatorFunctionOffsets)
            {
                if (item.Operator.InstructionOffset == -1)
                { throw new InternalException($"Operator {item.Operator.ReadableID()} does not have instruction offset", item.CurrentFile); }

                GeneratedCode[item.CallInstructionIndex].ParameterInt = item.Operator.InstructionOffset - item.CallInstructionIndex;
            }

            foreach (UndefinedGeneralFunctionOffset item in UndefinedGeneralFunctionOffsets)
            {
                if (item.CallStatement == null) { }
                else if (item.CallStatement is ConstructorCall constructorCall)
                {
                    if (item.GeneralFunction.InstructionOffset == -1)
                    { throw new InternalException($"Constructor for type \"{constructorCall.TypeName}\" does not have instruction offset", item.CurrentFile); }
                }
                else if (item.CallStatement is KeywordCall functionCall)
                {
                    if (functionCall.Identifier.Content == "delete")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Constructor for \"{item.GeneralFunction.Context}\" does not have instruction offset", item.CurrentFile); }
                    }
                    else if (functionCall.Identifier.Content == "clone")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Cloner for \"{item.GeneralFunction.Context}\" does not have instruction offset", item.CurrentFile); }
                    }
                    /*
                    else if (functionCall.Identifier.Content == "out")
                    {
                        if (item.GeneralFunction.InstructionOffset == -1)
                        { throw new InternalException($"Function {item.GeneralFunction.ReadableID()} does not have instruction offset", item.CurrentFile); }
                    }
                    */
                    else
                    { throw new NotImplementedException(); }
                }
                else
                { throw new NotImplementedException(); }

                GeneratedCode[item.CallInstructionIndex].ParameterInt = item.GeneralFunction.InstructionOffset - item.CallInstructionIndex;
            }

            return new Result()
            {
                Code = GeneratedCode.ToArray(),
                DebugInfo = GeneratedDebugInfo,

                Functions = this.CompiledFunctions,
                Operators = this.CompiledOperators,
                GeneralFunctions = this.CompiledGeneralFunctions,
                Structs = this.CompiledStructs,
                Classes = this.CompiledClasses,

                Hints = this.Hints.ToArray(),
                Informations = this.Informations.ToArray(),
                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static Result Generate(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            PrintCallback? printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
            => new CodeGenerator(settings).GenerateCode(
            compilerResult,
            settings,
            printCallback,
            level
        );
    }
}