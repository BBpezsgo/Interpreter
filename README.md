# My programming language

[![.Net 9.0](https://img.shields.io/badge/.NET-9.0-5C2D91)](#)
[![C# preview](https://img.shields.io/badge/C%23-preview-239120.svg)](#)

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## [Language Server](https://github.com/BBpezsgo/BBCode-LanguageServer)

## About
It's my own programming language that can generate bytecodes (executed by a custom interpreter) and [brainfuck](https://esolangs.org/wiki/brainfuck) (with limitations).

I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great [tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF) showing how to make a parser and a compiler. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.

## Command line arguments

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
```cs
using "https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/StandardLibrary/System.Console.bbc";

PrintLine("hello, world");
```

## api-ms-win-crt-string-l1-1-0.dll Missing Error
This can be fixed by install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

## Build From Source

1. Download this repository
2. Download the [Win32-Stuff](https://github.com/BBpezsgo/Win32-Stuff) repository
3. Download the [Maths](https://github.com/BBpezsgo/Math) repository
4. Extract the .zip files
5. Remove all `-main` suffixes

Now the folder structure should look like this:
```
./Interpreter/Core.csproj
./Win32-Stuff/Win32.csproj
./Math/Math.csproj
```

6. Open `./Interpreter/Core.csproj` in a **text editor**
7. In the `ProjectReference` tags, replace all `..\..` with `..` (so it will point to the existing projects you downloaded)

### Method 1: Building with Visual Studio:

8. Open `./Interpreter/Core.csproj` in Visual Studio
9. Add the `./Win32-Stuff/Win32.csproj` project to the solution
10. Add the `./Math/Math.csproj` project to the solution

Now you can build it with the "Build" button

### Method 2: Building with .NET CLI:

8. Open the `./Interpreter` folder

11. Now you can use the .NET CLI to build (`dotnet build`) or run (`dotnet run`) the project

## [Tests](https://github.com/BBpezsgo/Interpreter/blob/master/Tests.md)
