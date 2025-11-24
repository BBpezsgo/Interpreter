using System;

namespace Examples;

static class Program
{
    static void Main()
    {
        Console.WriteLine(" === BBL Examples ===");

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(HelloWorld)} ---");
        Console.WriteLine();
        HelloWorld.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(Strings)} ---");
        Console.WriteLine();
        Strings.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(ExternalFunctions)} ---");
        Console.WriteLine();
        ExternalFunctions.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(ExternalConstants)} ---");
        Console.WriteLine();
        ExternalConstants.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(ExposedFunctions)} ---");
        Console.WriteLine();
        ExposedFunctions.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(ExecutionManager)} ---");
        Console.WriteLine();
        ExecutionManager.Run();

        Console.WriteLine();
        Console.WriteLine($" --- {nameof(CustomSourceProvider)} ---");
        Console.WriteLine();
        CustomSourceProvider.Run();
    }
}
