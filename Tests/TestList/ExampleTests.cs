using System.Text;

namespace LanguageCore.Tests;

[TestClass, TestCategory("Examples"), DoNotParallelize]
public class ExampleTests
{
    const int Timeout = 5000;

    [TestMethod, Timeout(Timeout)]
    public void HelloWorld()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.HelloWorld.Run();

        Assert.AreEqual(
            "Hello world!\r\n",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void ExternalFunctions()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.ExternalFunctions.Run();

        Assert.AreEqual(
            $"This was called from the script!!!{writer.NewLine}" +
            $"This was called with these arguments: -3, 8{writer.NewLine}" +
            $"This was called with these arguments: 76, 4 and the result is 80{writer.NewLine}",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void ExposedFunctions()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.ExposedFunctions.Run();

        Assert.AreEqual(
            "This was called from C#!!!\r\n" +
            "This was called with these arguments: 4, -17\r\n" +
            "This was called with these arguments: 64, 2 and the result is 66\r\n" +
            $"Function \"with_return_value\" returned 66{writer.NewLine}",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void CustomSourceProvider()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.CustomSourceProvider.Run();

        Assert.AreEqual(
            "Hello",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void ExternalConstantsProvider()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.ExternalConstants.Run();
    }
    [TestMethod, Timeout(Timeout)]
    public void ExecutionManager()
    {
        StringBuilder output = new();
        using StringWriter writer = new(output);
        Console.SetOut(writer);

        Examples.ExecutionManager.Run();

        Assert.AreEqual(
            ".\r\n" +
            "init\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "tick\r\n" +
            "end\r\n",
            output.ToString());
    }
}
