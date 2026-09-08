using StorageLicenses.Domain.Common;

namespace StorageLicences.Test.Domain;

public class ResultTests
{
    private static readonly Error ErrorA = new("ERROR_A", "First error.");
    private static readonly Error ErrorB = new("ERROR_B", "Second error.");

    [Fact]
    public void Success_HasNoErrors()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_SingleError_ExposesErrorAndErrors()
    {
        var result = Result.Failure(ErrorA);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorA, result.Error);
        Assert.Single(result.Errors);
        Assert.Contains(ErrorA, result.Errors);
    }

    [Fact]
    public void Failure_MultipleErrors_ExposesAllErrorsAndFirstAsError()
    {
        var result = Result.Failure([ErrorA, ErrorB]);

        Assert.True(result.IsFailure);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(ErrorA, result.Errors);
        Assert.Contains(ErrorB, result.Errors);
        Assert.Equal(ErrorA, result.Error);
    }
}
