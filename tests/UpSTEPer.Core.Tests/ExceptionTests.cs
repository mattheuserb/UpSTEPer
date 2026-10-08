namespace UpSTEPer.Core.Tests;

public class ExceptionTests
{
    public static TheoryData<Func<Exception>, string?, bool> Constructors => new()
    {
        { () => new StepImportException(), null, false },
        { () => new StepImportException("import"), "import", false },
        { () => new StepImportException("import", new IOException()), "import", true },
        { () => new GeometryOperationException(), null, false },
        { () => new GeometryOperationException("geometry"), "geometry", false },
        { () => new GeometryOperationException("geometry", new IOException()), "geometry", true },
    };

    [Theory]
    [MemberData(nameof(Constructors))]
    public void Constructors_SetMessageAndInnerException(Func<Exception> create, string? message, bool hasInner)
    {
        ArgumentNullException.ThrowIfNull(create);

        var exception = create();

        if (message is not null)
        {
            Assert.Equal(message, exception.Message);
        }

        Assert.Equal(hasInner, exception.InnerException is IOException);
    }
}
