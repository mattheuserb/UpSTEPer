namespace UpSTEPer.Core.Tests;

public class FaceIdTests
{
    [Fact]
    public void ToString_ShowsIndex()
    {
        Assert.Equal("Face#42", new FaceId(42).ToString());
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new FaceId(3), new FaceId(3));
        Assert.NotEqual(new FaceId(3), new FaceId(4));
    }
}
