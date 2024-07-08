global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using Maths;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
