# Interpreter

[![.Net v6.0](https://img.shields.io/badge/.NET-v6.0-5C2D91)](#)
[![C# v10](https://img.shields.io/badge/C%23-v10.0-239120.svg)](#)

## [Download](https://drive.google.com/uc?export=download&id=1CkZ_b0OFzaiLnU6dcoRTiy-gZOiCl3jM](https://drive.google.com/uc?export=download&id=1CkZ_b0OFzaiLnU6dcoRTiy-gZOiCl3jM))

[![win-x86](https://img.shields.io/badge/win-x86-0078D6?logo=windows&logoColor=white)](#)

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## About
This is my own "programming language". This parses the given source code (text file), generates a list of instructions (opcodes with parameters) and executes it (with a bytecode interpreter).

I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great
[tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF)
showing how to make a parser and a compiler. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.

### Arguments:

`BBCodeInterpreter.exe [arguments...] <source path>`

**Logging:**
- `--hide-debug|-hd` Hides debug logs
- `--hide-info|-hi` Hides information logs
- `--hide-system|-hs` Hides system logs
- `--hide-warning|-hw` Hides warning logs

**Code Generator:**
- `--remove-unused-functions|-ruf <iterations>` Remove unused instructions iterations
- `--print-instructions|-pi` Prints the generated instructions

**Interpreter:**
- `--stack-size|-ss <size>` Sets the interpreter *stack size*
- `--heap-size|-hs <size>` Sets the heap size

**Modes:**
> Use only one of these!
- `--console-gui|-cg` I use this mode for debugging
- `--brainfuck|-bf` Compiles and executes the code with a brainfuck interpreter

**Other:**
- `--throw-errors|-te` Causes the program to crash on any syntax, parser, runtime, or internal exception.
- `--basepath|-bp <base folder path>` Sets a new path where dll and bbc files will be searched for *using* statements. If it's not there, it will look for it next to the file.

## Hello World:
```cs
// This imports the local System.bbc file along with its functions and structures.
using "../CodeFiles/System";

// Prints a message to the console
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
