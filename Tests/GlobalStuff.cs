global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Maths;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]
