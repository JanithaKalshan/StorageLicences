using StorageLicenses.Domain.Entities;

namespace StorageLicences.Test.Domain;

public class StorageLicenceTests
{
    private static Unit CreateUnit() => new(palletCapacity: 3, boxCapacity: 8);

    [Fact]
    public void CoversDate_DateWithinTerm_ReturnsTrue()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2016, 3, 10), 10);

        Assert.True(licence.CoversDate(new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public void CoversDate_DateOnExclusiveEndDate_ReturnsFalse()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2016, 3, 10), 10);

        Assert.False(licence.CoversDate(new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void CoversDate_DateBeforeGrantDate_ReturnsFalse()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2016, 3, 10), 10);

        Assert.False(licence.CoversDate(new DateOnly(2016, 3, 9)));
    }

    [Fact]
    public void CoversDate_DateOnOrAfterSurrenderDate_ReturnsFalse()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2020, 1, 1), 10);
        licence.Surrender(new DateOnly(2023, 6, 1));

        Assert.False(licence.CoversDate(new DateOnly(2023, 6, 1)));
        Assert.True(licence.CoversDate(new DateOnly(2023, 5, 31)));
    }

    [Fact]
    public void Surrender_DateBeforeGrantDate_ReturnsFailureResult()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2020, 1, 1), 10);

        var result = licence.Surrender(new DateOnly(2019, 12, 31));

        Assert.True(result.IsFailure);
        Assert.Null(licence.SurrenderedDate);
    }

    [Fact]
    public void Surrender_ValidDate_ReturnsSuccessAndSetsSurrenderedDate()
    {
        var unit = CreateUnit();
        var licence = new StorageLicence(unit, "COMPANY-ABC", new DateOnly(2020, 1, 1), 10);

        var result = licence.Surrender(new DateOnly(2023, 6, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2023, 6, 1), licence.SurrenderedDate);
    }
}
