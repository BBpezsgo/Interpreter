namespace Tests;

public class ExpectedExceptionWithMessageAttribute : ExpectedExceptionBaseAttribute
{
    public Type ExceptionType { get; set; }
    public string ExpectedMessage { get; set; }

    public ExpectedExceptionWithMessageAttribute(Type exceptionType, string expectedMessage)
    {
        ExceptionType = exceptionType;
        ExpectedMessage = expectedMessage;
    }

    protected override void Verify(Exception exception)
    {
        if (exception.GetType() != ExceptionType)
        {
            RethrowIfAssertException(exception);

            Assert.Fail(
                "ExpectedExceptionWithMessage failed." +
                $"Expected exception type: {ExceptionType.FullName}." +
                $"Actual exception type: {exception.GetType().FullName}." +
                $"Exception message: {exception.Message}");
        }

        if (exception.Message != ExpectedMessage)
        {
            Assert.Fail(
                "ExpectedExceptionWithMessage failed." +
                $"Expected exception message: {ExceptionType.FullName}." +
                $"Actual exception message: {exception.GetType().FullName}.");
        }
    }
}
