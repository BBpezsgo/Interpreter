global using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
