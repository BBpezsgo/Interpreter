﻿using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

[ExcludeFromCodeCoverage]
public struct MainGeneratorSettings
{
    public bool GenerateComments;
    public bool PrintInstructions;
    public bool DontOptimize;
    public bool GenerateDebugInstructions;
    public bool ExternalFunctionsCache;
    public bool CheckNullPointers;
    public CompileLevel CompileLevel;
    public int PointerSize;
    public int StackSize;

    public MainGeneratorSettings(MainGeneratorSettings other)
    {
        GenerateComments = other.GenerateComments;
        PrintInstructions = other.PrintInstructions;
        DontOptimize = other.DontOptimize;
        GenerateDebugInstructions = other.GenerateDebugInstructions;
        ExternalFunctionsCache = other.ExternalFunctionsCache;
        CheckNullPointers = other.CheckNullPointers;
        CompileLevel = other.CompileLevel;
        PointerSize = other.PointerSize;
        StackSize = other.StackSize;
    }

    public static MainGeneratorSettings Default => new()
    {
        GenerateComments = true,
        PrintInstructions = false,
        DontOptimize = false,
        GenerateDebugInstructions = true,
        ExternalFunctionsCache = false,
        CheckNullPointers = true,
        CompileLevel = CompileLevel.Minimal,
        PointerSize = 4,
        StackSize = BytecodeInterpreterSettings.Default.StackSize,
    };
}
