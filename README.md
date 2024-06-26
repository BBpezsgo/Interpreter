# My programming language

[![.Net 8.0](https://img.shields.io/badge/.NET-8.0-5C2D91)](#)
[![C# preview](https://img.shields.io/badge/C%23-preview-239120.svg)](#)

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## [Language Server](https://github.com/BBpezsgo/BBCode-LanguageServer)

## About
It's my own programming language that can generate bytecodes (executed by a custom interpreter) and [brainfuck](https://esolangs.org/wiki/brainfuck) (with limitations).

I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great [tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF) showing how to make a parser and a compiler. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.

## Command line arguments
> 
> `BBLang.exe` is the compiled compiler program
> 
> `[stuff]` - "stuff" is an **optional** argument.
> 
> `<stuff>` - You **should** specify this argument with a value (in this example you don't have to specify "stuff" but any value that doesn't contain any whitespaces).
> 
> `stuff1|stuff2` you can use **one** of the listed arguments separated by `|` (in this example "stuff1" or "stuff2").
> 
> `stuff1;stuff2` you can use **any combination** of the listed arguments (or none) separated by `;` (in this example "stuff1" or "stuff2").

`BBLang.exe [options...] <source path>`

### Options:

**Brainfuck:**

- - `--brainfuck|-bf` Compiles and executes the code with a brainfuck interpreter.
> [!WARNING]
> Expect buggy behavior and missing features!

**Logging:**
- `--hide-debug|-hd` Hides debug logs

- `--hide-info|-hi` Hides information logs

- `--hide-warning|--hide-warnings|-hw` Hides warning logs

- `--show-progress|-sp` Prints some progress bars and labels during compilation.

- `--print-heap|-ph` Prints the HEAP. (only works with brainfuck)

**Code Generator:**
- `--print-instructions|-pi [final;commented;simplified;f;c;s]` Prints the generated instructions. For brainfuck, you can also specify which formats you want to see the generated code in:

  - `final|f` Prints the final instructions.
  - `commented|c` Prints the instructions with generated comments.
  - `simplified|s` Prints the simplified form of the instructions.

- `--no-nullcheck|-nn` Disables the check for null pointers (and throw runtime exceptions) when accessing something by a pointer (accessing a field, calling a method, etc.).

- `--dont-optimize|-do` Disables basic optimization.

- `--no-debug-info|-ndi` Disables the generation of debug informations (if you compiling into brainfuck, generating debug informations will take a lots of time).

- `--output|-o <file path>` Specified the output file path where the generated code will be saved. This option only works for brainfuck.

- `--basepath|-bp <folder path>` Sets the path where .dll ([read more](https://github.com/BBpezsgo/Interpreter/wiki/Advanced-Topics#importing-dll-files)) and other source files will be searched for `using` statements.

**Runtime:**
- `--stack-size|-ss <size>` Sets the interpreter's stack size (only valid for the default mode).

- `--heap-size|-hs <size>` Specifies the HEAP size (only valid for the default and brainfuck modes).
> [!NOTE]
> For brainfuck, if you specify zero, the HEAP will not be initialized, and wherever you try to access it, it will not compile.

> [!NOTE]
> Because of how HEAP represented in brainfuck, its size can't be larger than 126.

- `--console-gui|-cg` I use this mode for debugging. [More info](https://github.com/BBpezsgo/Interpreter/wiki/Debugger)

**Other:**
- `--throw-errors|-te` With this option, the program crashes on any exception and lets the .NET runtime handle them.

- `--no-pause|-np` With this option, the program exits at the end of execution without printing "Press any key to exit" and doesn't wait for any key press.

## Hello World:
```cs
using "https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/StandardLibrary/System.bbc";

PrintLine("hello, world");
```

## api-ms-win-crt-string-l1-1-0.dll Missing Error
This can be fixed by install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

## Build From Source

If you want to download the project and build it, there is how to do that:
1. Download this repository
2. Download the [Win32-Stuff](https://github.com/BBpezsgo/Win32-Stuff) repository
3. Extract the .zip files
4. Remove all `-main` suffixes

Now the folder structure should look like this:
```
./Interpreter/Core.csproj
./Win32-Stuff/Win32.csproj
```

5. Open `./Interpreter/Core.csproj` in a **text editor**
6. In the `ProjectReference` tags, replace `..\..` with `..` (so it will point to the existing projects you downloaded)

### Method 1: Building with Visual Studio:

7. Open `./Interpreter/Core.csproj` in Visual Studio
8. Add the `./Win32-Stuff/Win32.csproj` project to the solution
9. Now you can build it with the "Build" button

### Method 2: Building with .NET CLI:

7. Open the `./Interpreter` folder

8. Now you can use the .NET CLI to build (`dotnet build`) or run (`dotnet run`) the project

## [Tests](https://github.com/BBpezsgo/Interpreter/blob/master/Tests.md)
