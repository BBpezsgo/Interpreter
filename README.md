# Interpreter

[![.Net 7.0](https://img.shields.io/badge/.NET-7.0-5C2D91)](#)
[![C# preview](https://img.shields.io/badge/C%23-preview-239120.svg)](#)

## Download

[Download win-x64 JIT](https://drive.google.com/uc?export=download&id=1tlonja46cQfgcAPrV-6hxesONnVzdW2o)

[Download win-x64 AOT](https://drive.google.com/uc?export=download&id=1eEvYsDmbwt2s5ND8-DStJG1WFoc7WTXZ) Google says it's a virus ☹

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## About
It's my own programming language with three different output formats: bytecodes (executed by a custom interpreter), x86-64 assembly (that can be assembled by [nasm](https://www.nasm.us/) (WIP)), and [brainfuck](https://esolangs.org/wiki/brainfuck) (with limitations).

I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great [tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF) showing how to make a parser and a compiler. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.

## Command line arguments

> `BBCodeInterpreter.exe` is the compiled compiler program
> 
> `[stuff]` - "stuff" is an **optional** argument
> 
> `<stuff>` - "stuff" is a **required** argument
> 
> `stuff1|stuff2` you can use **one** of the listed arguments separated by `|` (in this example "stuff1" or "stuff2")

`BBCodeInterpreter.exe [options...] <source path>`

### Options:

**Logging:**
- `--hide-debug|-hd` Hides debug logs
- `--hide-info|-hi` Hides information logs
- `--hide-system|-hs` Hides system logs
- `--hide-warning|-hw` Hides warning logs

**Code Generator:**
- `--remove-unused-functions|-ruf <iterations>` Sets the unused function removal's max iteration count
- `--print-instructions|-pi` Prints the generated instructions
- `--no-nullcheck|-nn` Check for null pointers (and throw runtime exceptions) when accessing something by a pointer (accessing a field, calling a method, etc.)
- `--dont-optimize|-do` Disables basic optimization
- `--no-debug-info|-ndi` Disables the generation of debug informations (if you compiling into brainfuck, generating debug informations will take a lots of time)

**Interpreter:**
- `--stack-size|-ss <size>` Sets the interpreter's stack size (only valid with the default mode)
- `--heap-size|-hs <size>` Sets the interpreter's HEAP size (only valid with the default mode)

**Modes:**
> Use only one of these!
- `--brainfuck|-bf` Compiles and executes the code with a brainfuck interpreter. ⚠ **Expect buggy behavior and missing features!**
- `--asm` Generates an assembly file, assemble it with nasm and execute the result exe file. ⚠ **Expect buggy behavior and missing features!**
- The default mode is custom bytecodes that the interpreter can execute (this will automatically execute after the compiling)

**Other:**
- `--throw-errors|-te` With this option, the program crashes on any exception and lets the .NET runtime handle them.
- `--basepath|-bp <base folder path>` Sets a path where .dll and other source files will be searched for `using` statements. If it's not there, it will look for them in the directory where the input file is.
- `--console-gui|-cg` I use this mode for debugging
- `--no-pause|-np` With this option, the program exits at the end of execution without printing "Press any key to exit" and doesn't wait for any key press

## Hello World:
```cs
using "../StandardLibrary/System";

PrintLine("hello, world");
```

## Default external functions

### "stdin"
Reads a key from the console. This will block the code execution until a key is pressed.
- Parameters: none
- Return value: `char`

### "stdout"
Writes a character to the standard output.
- Parameters: `char` character
- Return value: `void`

### "stderr"
Writes a character to the standard error.
- Parameters: none
- Return value: `void`

### "console-set"
Sets a character on the console.
- Parameters: `char` character, `int` x, `int` y
- Return value: `void`

### "console-clear"
Clears the console.
- Parameters: none
- Return value: `void`

### "sleep"
Pauses the code execution for `t` millisecs.
- Parameters: `int` t
- Return value: `void`

### "sin"
Returns the sine of `v` angle.
- Parameters: `float` v
- Return value: `float`

### "cos"
Returns the cosine of `v` angle.
- Parameters: `float` v
- Return value: `float`

## api-ms-win-crt-string-l1-1-0.dll Missing Error
This can be fixed by install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

## Build From Source

If you want to download the project and build it, there is how to do that:
1. Download this repository
2. Download the [Win32-Stuff](https://github.com/BBpezsgo/Win32-Stuff) repository
3. Download the [DataUtilities](https://github.com/BBpezsgo/DataUtilities) repository
4. Extract all three .zip files
5. Remove all `-main` suffixes

Now the folder structure should look like this:
```
/Interpreter/BBCodeInterpreter.csproj
/Win32-Stuff/Win32.csproj
/DataUtilities/DataUtilities.csproj
```

6. Open `/Interpreter/BBCodeInterpreter.csproj` in a **text editor**
7. In the two `ProjectReference` tags, replace `C:\Users\bazsi\source\repos` with `..` (so it will point to the existing projects you downloaded)

### Method 1: Building with Visual Studio:

8. Open `/Interpreter/BBCodeInterpreter.csproj` in Visual Studio
9. Add the two existing projects to the solution 
   ( `/Win32-Stuff/Win32.csproj` and `/DataUtilities/DataUtilities.csproj` )

Done

### Method 2: Building with .NET CLI:

8. Open the `/Interpreter` folder

Done, you can now use the .NET CLI to build the project (`dotnet build`) or run it (`dotnet run`)