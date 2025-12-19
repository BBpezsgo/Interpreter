# BBLang

[![.Net 9.0](https://img.shields.io/badge/.NET-9.0-5C2D91?style=flat-square)](#)
[![C# 11](https://img.shields.io/badge/C%23-11-239120.svg?style=flat-square)](#)

- [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)
- [Language Server](https://github.com/BBpezsgo/BBCode-LanguageServer)
- [Debugger Host](https://github.com/BBpezsgo/BBLang-DebugHost)

## About

An interpreted language for mostly scripting purposes or simulations. I use this project in my game to implement in-game programming. It can also generate Brainfuck code, because why not, and can also optimize functions into MSIL, or compile the whole script into a `DynamicMethod`.

> [!NOTE]
> Currently it doesn't support serializing, so you can only execute the script. However, you can save the generated Brainfuck code.

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

- `--heap-size size` Specifies the heap size

> [!NOTE]
> For brainfuck, if you specify zero the heap will not be initialized and wherever you try to access it, it will not compile.

> [!NOTE]
> Because of how heap represented, its size can't be larger than 126.

- `--no-nullcheck` Disables null check generation when dereferencing a pointer

## Hello World

```cs
using "https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/StandardLibrary/System.Console.bbc";

printline("hello, world");
```

Without dependencies:

```cs
[External("stdout")]
void print(char message);

void printline(temp string message)
{
    for (int i = 0; message[i]; i++)
    {
        print(message[i]);
    }
    print('\r');
    print('\n');
}

printline("hello, world");
```

## Project Structure

- `/Examples` Examples for using the project as a library.
- `/StandardLibrary` Preimplemented functions and structures and some "external function" declarations.
- `/TestFiles` Test files I use for testing.
- `/Source` The core functionality.
- `/Utility` The command line interface.
- `/Debugger` A terminal based debugger.

## Project Dependencies

### Core Library

- [System.Collections.Immutable](https://www.nuget.org/packages/System.Collections.Immutable)

> [!NOTE]
> With .NET 9 this is already installed.

### Command Line Utility

- Core
- Debugger
- [CommandLineParser](https://www.nuget.org/packages/CommandLineParser)

### Debugger

- Core

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
- You can install the necessary NuGet packages with this tool: [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) or import the dll-s manually.

## [Tests](https://github.com/BBpezsgo/Interpreter/blob/master/Tests.md)

## Troubleshooting

### api-ms-win-crt-string-l1-1-0.dll Missing Error

install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)
