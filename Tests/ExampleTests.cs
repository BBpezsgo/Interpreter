using System.Text;

namespace Tests;

[TestClass, TestCategory("Examples"), DoNotParallelize]
public class ExampleTests
{
    const int Timeout = 5000;

    [TestMethod, Timeout(Timeout)]
    public void HelloWorld()
    {
        StringBuilder output = new();
        Console.SetOut(new StringWriter(output));

        Examples.HelloWorld.Run();

        Assert.AreEqual(
            "Hello world!\r\n",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void ExternalFunctions()
    {
        StringBuilder output = new();
        Console.SetOut(new StringWriter(output));

        Examples.ExternalFunctions.Run();

        Assert.AreEqual(
            "This was called from the script!!!\n" +
            "This was called with these arguments: -3, 8\n" +
            "This was called with these arguments: 76, 4 and the result is 80\n",
            output.ToString());
    }

    [TestMethod, Timeout(Timeout)]
    public void ExposedFunctions()
    {
        StringBuilder output = new();
        Console.SetOut(new StringWriter(output));

        Examples.ExposedFunctions.Run();

        Assert.AreEqual(
            "This was called from C#!!!\r\n" +
            "This was called with these arguments: 4, -17\r\n" +
            "This was called with these arguments: 64, 2 and the result is 66\r\n" +
            "Function \"with_return_value\" returned 66\n",
            output.ToString());
    }
}
