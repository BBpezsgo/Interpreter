global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Maths;

[assembly: Parallelize(Workers = 1, Scope = ExecutionScope.MethodLevel)]
