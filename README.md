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

**Interpreter:**
- `-bc-clock [int]` Sets the interpreter *clock cycles/update*
- `-bc-instruction-limit [int]` Sets the interpreter *instruction limit*
- `-bc-stack-size [int]` Sets the interpreter *stack size*

**Modes:**
> Use only one of these!
- `-test` Executes a test file.
- `-decompile` Executes a binary file.
- `-compile [string]` Compiles and writes the code to the given path

**Other:**
- `-compression none|fastest|optimal|smallest` Specifies the compression level. Only valid if you use the `-compile` argument!
- `-throw-errors` Causes the program to crash on any syntax, parser, runtime, or internal exception.
- `-basepath [string]` Sets a new path where dll and bbc files will be searched for *using* statements. If it's not there, it will look for it next to the file.

## Download from [Google Drive](https://drive.google.com/uc?export=download&id=1SBDsPYvKi7P0UVTI9bW9rxLWqf8hb_4y) or [Mega](https://mega.nz/file/CNpUFR5S#80hrb8ofv5RNCy7uIMmxxeA43dAl5I5jbfs7t_OBanU)
![win-x86](https://img.shields.io/badge/win-x86-0078D6?logo=windows&logoColor=white)

[Download System.bbc](https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/CodeFiles/System.bbc)<br>
[Download System.Net.bbc](https://raw.githubusercontent.com/BBpezsgo/Interpreter/master/CodeFiles/System.Net.bbc)
> Some predefined structs and functions.

## [Debugger](https://github.com/BBpezsgo/InterpreterDebugger)

## [VSCode Extension](https://github.com/BBpezsgo/InterpreterVSCodeExtension)

## Hello World:
```
// This imports the local System.bbc file along with its functions and structures.
using "../CodeFiles/System";

// Namespaces can only be used to organize code
namespace Program
{
    // The program needs a function with the [CodeEntry] attribute. This function will be executed when the program is started.
    [CodeEntry]
    void Main()
    {
        // Print a message to the console
        Console.Log("Hello world");
    }
}
```

## Other features
### Structs
You can create a very basic struct.
A struct can only contain fields.<br>
Methods are currently not supported.
```
// Define the struct

struct Foo
{
  int field1;
  string field2;
}

// Create an instance:

Foo x = new Foo;
```
> If `Console.Log` is called with a parameter of type `Struct`, it will print `{ ... }`
### Lists
There are also lists... What should I say?
```
// Define a list:
int[] var0;

// Define a list literal:
[0, 1, 2, 3];

// Combined: (Define a list with an initial value)
int[] var2 = [0, 1, 2, 3];
```
> The `Console.Log` function prints all items in the list
### Function overloading
Well, this is a test feature, so I don't recommend using function overloading.
### Method like functions
You can create a function that looks like a method.
Put the `this` keyword before the first parameter and you're done.<br>
When you call a method like the function
do not use the first argument,
instead, put before the function.
```
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
```
[Catch("update")]
void Update() { }
```
> **NOTE:**
> If you listen to the `update` event, the program will run until you manually close it.

## `export` Keyword
You can use the `export` keyword to specify that the following function definition can be used in other files.
```
export void Foo()
{ }
```
> **NOTE:**
> The `export` keyword is not supported on structs: all structs can be used in other files!

## Predefined Functions

### System.bbc

`Console.Log(message)` Prints `message` to the console<br>
`Console.LogWarning(message)` Prints `message` in yellow on the console<br>
`Console.LogError(message)` Prints `message` in red on the console<br>
`Console.Input(promt)` Prints the "prompt" to the console and waits for user input<br>

`Sleep(ms)` Pauses the code execution for `ms` milliseconds<br>

`Time.Now()` Returns the current time<br>

`<string>.Reverse()` Returns the reverse of the string<br>
`<string>.Substring(start)` Returns the string after `start`<br>
`<string>.Substring(start, length)` Returns the `length` long strings after `start`<br>
`<string>.Split(sep)` Splits the text at the specified (`sep`) separators<br>

`Math.Pow(a, b)` Returns `a` to the power of `b`<br>
`Math.Abs(v)` Returns the absolute value of `v`<br>
`Math.Min(a, b)` Returns the smaller of `a` and `b`<br>
`Math.Max(a, b)` Returns the larger of `a` and `b`<br>

### System.Net.bbc

`Http.Get(url)` Sends a HTTP GET request to `url`

## api-ms-win-crt-string-l1-1-0.dll Missing Error
This can be fixed by install [this](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170).

## Known issules

The file `/TestFiles/test-matrix.bbc` doesn't work for some reason :(
