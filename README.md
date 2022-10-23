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
The argument syntax looks like this: `[-throw-errors] <path>`<br>
The `-throw-errors` argument causes the program to crash on any syntax, parser, runtime, or internal exception.
