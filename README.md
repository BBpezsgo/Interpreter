# Interpreter

![.Net v6.0](https://img.shields.io/badge/.NET-v6.0-5C2D91)
![C# v10](https://img.shields.io/badge/C%23-v10.0-239120.svg)

## What is this??
I was bored, so I googled how to make a programming language. I found a [youtuber](https://www.youtube.com/c/uliwitness) who made a great
[tutorial series](https://www.youtube.com/watch?v=2DTNDrdqGlo&list=PLZjGMBjt_VVAMW53XnMtNfAQowZwMviBF)
showing how to make a parser. I basically copied the code with minimal changes. When the series ended, I was left on my own, so I improved the code myself.<br>
So this project is the result of my boredom.

## What can it do?
This program parses the given source code, creates a list of instructions and executes it. The source code syntax is based on C#.

## How to run a file?
When you've created a source file, just run it with the executable. Or run the exe file with an argument where you specify the path to the source code.<br>
### Arguments:

**Logging:**
- `-hide-debug` Hides the debug logs
- `-hide-system` Hides the system logs

**Parser:**
- `-p-print-info [bool]` Prints the parser info

**Code Generator:**
- `-c-generate-comments [bool]` Generates comment instructions
- `-c-remove-unused-functions [byte]` Remove unused instructions iterations
- `-c-print-instructions [bool]` Prints the generated instructions
- `-dont-optimize` Disables basic code optimization
- `-no-debug-info` I forgot what it does
- `-bf-no-cache` Disables the external function name cache. With this option enabled, every time you perform an external call, the function name is generated on the heap

**Interpreter:**
- `-bc-clock [int]` Sets the interpreter *clock cycles/update*
- `-bc-instruction-limit [int]` Sets the interpreter *instruction limit*
- `-bc-stack-size [int]` Sets the interpreter *stack size*
- `-heap [int]` Sets the heap size

**Modes:**
> Use only one of these!
- `-test` ⚠️ Deprecated! Executes a test file.
- `-decompile` ⚠️ Deprecated! Executes a binary file.
- `-compile [string]` ⚠️ Deprecated! Compiles and writes the code to the given path
- `-console-gui`
- `-debug` ⚠️ Deprecated!
- `-il` ⚠️ Do not use this!
- `-brainfuck` Compiles and executes the code with a brainfuck interpreter

**Other:**
- `-compression none|fastest|optimal|smallest` Specifies the compression level. Only valid if you use the `-compile` argument!
- `-throw-errors` Causes the program to crash on any syntax, parser, runtime, or internal exception.
- `-basepath [string]` Sets a new path where dll and bbc files will be searched for *using* statements. If it's not there, it will look for it next to the file.

## Download from [Google Drive](https://drive.google.com/uc?export=download&id=1CkZ_b0OFzaiLnU6dcoRTiy-gZOiCl3jM)
![win-x86](https://img.shields.io/badge/win-x86-0078D6?logo=windows&logoColor=white)

[Download System.bbc](https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/CodeFiles/System.bbc)<br>
[Download System.Net.bbc](https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/CodeFiles/System.Net.bbc)
> Some predefined structs and functions.

## [Debugger](https://github.com/BBpezsgo/InterpreterDebugger) ⚠️ Deprecated

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## Hello World:
```cs
// This imports the local System.bbc file along with its functions and structures.
using "../CodeFiles/System";

// Prints a message to the console
PrintLine("hello, world");
```

## Other features
### Structs
> ⚠ **OUTDATED**

You can create a very basic struct.
A struct can only contain fields.<br>
Methods are currently not supported.
```cs
// Define the struct

struct Foo
{
  int field1;
  byte field2;
}

// Create an instance:

Foo x = new Foo;
```
### Function overloading
Yeah. It is supported.
### Method like functions
You can create a function that looks like a method.
Put the `this` keyword before the first parameter and you're done.<br>
When you call a method like the function
do not use the first argument,
instead, put before the function.
```cs
// Define the function:

int Add(this int v)
{
  return v + 5;
}

// Call the function:

13.Add();
```
### Events

You can listen to an event with the `Catch` attribute.<br>
Two events can be caught:<br>
`update` and `end`.<br>
`update` is called every tick.<br>
`end` is called when the program finishes executing.
```cs
[Catch("update")]
void Update() { }
```
> **NOTE:**
> If you listen to the `update` event, the program will run until you manually close it.

## `export` Keyword
You can use the `export` keyword to specify that the following function definition can be used in other files.
```cs
export void Foo()
{ }
```
> **NOTE:**
> The `export` keyword is only supported on functions: all structs/classes/enums can be used in other files!

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
