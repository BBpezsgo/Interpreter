# My Programming Language

[![.Net 9.0](https://img.shields.io/badge/.NET-9.0-5C2D91)](#)
[![C# 11](https://img.shields.io/badge/C%23-11-239120.svg)](#)

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## [Language Server](https://github.com/BBpezsgo/BBCode-LanguageServer)

## About
It's my own programming language that can generate bytecodes (executed by a custom interpreter) and [brainfuck](https://esolangs.org/wiki/brainfuck) (with limitations).

I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great [tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF) showing how to make a parser and a compiler. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.

## Command Line Arguments

`BBLang [options...] source`

- `--help` Prints some information about the program

- `--verbose` Prints some information about the compilation process

- `--format format` Specifies which generator to use. Supported formats are `bytecode`, `brainfuck` and `assembly`.

> [!WARNING]
> Brainfuck sometimes aint working.

> [!WARNING]
> Assembly 100% not working.

- `--debug` Launches the debugger screen (only avaliable on Windows) [More info](https://github.com/BBpezsgo/Interpreter/wiki/Debugger)

- `--output file` Writes the generated code to the specified file (this option only works for brainfuck)

- `--throw-errors` Crashes the program whenever an exception thrown. This useful for me when debugging the compiler.

- `--print-instructions` Prints the generated instructions before execution

- `--print-memory` Prints the memory after execution

- `--basepath directory` Sets the path where source files will be searched for `using` statements

- `--dont-optimize` Disables all optimization

- `--no-debug-info` Disables debug information generation (if you compiling into brainfuck, generating debug informations will take a lots of time)

- `--stack-size size` Specifies the stack size

- `--heap-size size` Specifies the HEAP size
> [!NOTE]
> For brainfuck, if you specify zero the HEAP will not be initialized and wherever you try to access it, it will not compile.

> [!NOTE]
> Because of how HEAP represented, its size can't be larger than 126.

- `--no-nullcheck` Disables null check generation when dereferencing a pointer

## Hello World:
```
using "https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/StandardLibrary/System.Console.bbc";

PrintLine("hello, world");
```

## api-ms-win-crt-string-l1-1-0.dll Missing Error
This can be fixed by install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

## Project Structure

- `/Examples` Some basic examples for using the project. I only tested the `ExposedFunctions.cs`, `ExternalFunctions.cs` and `HelloWorld.cs` 😅.
- `/StandardLibrary` Contains some preimplemented functions and structures and some "external function" declarations.
- `/TestFiles` Contains all the test files I use for testing the project.
- `/Source` All the source code for the core functionality can be found here.
- `/Utility` This contains the command line utility and the debugger.

## Project Dependencies

### Core Library

- [System.Collections.Immutable](https://www.nuget.org/packages/System.Collections.Immutable)
> [!NOTE]
> With .NET 9 this is already installed.

### Command Line Utility

- The core library
- [Win32-Stuff](https://github.com/BBpezsgo/Win32-Stuff)
- [Maths](https://github.com/BBpezsgo/Math)

## Unity

- Import the `/Unity/package.json` to the Unity project.
- Make sure to make a symlink to the Sources directory. Run this inside the `/Unity` directory:
```sh
ln -s ../Source Runtime
```
> [!NOTE]
> If you are on Windows, move and rename the `/Source` directory to `/Unity/Runtime`.

- Inside Unity, naviage to `Edit > Project Settings... > Player > Other Settings > Scripting Define Symbols` and add the `UNITY` variable.
- If you are using the Burst compiler, add `UNITY_BURST` too.
- If you are not using the Burst compiler, remove the `Unity.Burst` reference from `/Unity/BBLang.asmdef`.
- If you want some profiler analytics, add `UNITY_PROFILER` too.
- You can install the necessary NuGet packages with this tool: [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity).

## [Tests](https://github.com/BBpezsgo/Interpreter/blob/master/Tests.md)
