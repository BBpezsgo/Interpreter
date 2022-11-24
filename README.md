# Interpreter

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
- `-throw-errors` Causes the program to crash on any syntax, parser, runtime, or internal exception.
- `-hide-debug` Hides the debug logs
- `-hide-system` Hides the system logs
- `-p-print-info [bool]` Prints the parser info
- `-c-generate-comments [bool]` Generates comment instructions
- `-c-remove-unused-functions [byte]` Remove unused instructions iterations
- `-c-print-instructions [bool]` Prints the generated instructions
- `-bc-clock [int]` Sets the interperter *clock cycles/update*
- `-bc-instruction-limit [int]` Sets the interperter *instruction limit*
- `-bc-stack-size [int]` Sets the interperter *stack size*
- `-debug` <b>(Experimental feature)</b> This will start another thing that you might be able to use for debugging. You must manually jump to the next instruction with the TAB key.

### [Download .EXE](https://onedrive.live.com/download?cid=6AEB0DA011C539BF&resid=6AEB0DA011C539BF%2153979&authkey=AB-_RBd-SC-FnC8)

## Hello World program:
https://github.com/BBpezsgo/Interpreter/blob/32a5cb93a041a326dbf46774ca7dceb3945ad24d/TestFiles/helloworld.bbc#L1-L14

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

> **NOTE:**
> If you listen to the `update` event, the program will run until you manually close it.

You can listen to an event with the `Catch` attribute.<br>
Two events can be caught:<br>
`update` and `end`.<br>
`update` is called every tick.<br>
`end` is called when the program finishes executing.
```
[Catch("update")]
void Update() { }
```