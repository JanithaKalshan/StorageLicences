using StorageLicenses.Domain.Common;

namespace StorageLicenses.Domain.Entities;

/// <summary>
/// A licence granting a holder the right to store items in a unit for a fixed term.
/// Coverage is <c>[GrantDate, GrantDate + TermYears)</c> — the end date is exclusive.
/// </summary>
public class StorageLicence
{
    /// <summary>Reserved for EF Core materialization.</summary>
    protected StorageLicence()
    {
        HolderId = string.Empty;
    }

    public StorageLicence(int unitId, string holderId, DateOnly grantDate, int termYears)
    {
        if (string.IsNullOrWhiteSpace(holderId))
            throw new ArgumentException("Holder id is required.", nameof(holderId));

        if (termYears <= 0)
            throw new ArgumentOutOfRangeException(nameof(termYears), "Term years must be positive.");

        UnitId = unitId;
        HolderId = holderId;
        GrantDate = grantDate;
        TermYears = termYears;
    }

    /// <summary>
    /// Creates a licence linked via navigation property, allowing EF Core to fix up the
    /// foreign key even when <paramref name="unit"/> has not yet been persisted.
    /// </summary>
    public StorageLicence(Unit unit, string holderId, DateOnly grantDate, int termYears)
        : this(unit.Id, holderId, grantDate, termYears)
    {
        Unit = unit;
    }

    public int Id { get; private set; }

    public int UnitId { get; private set; }

    public Unit? Unit { get; private set; }

    public string HolderId { get; private set; }

    public DateOnly GrantDate { get; private set; }

    public int TermYears { get; private set; }

    public DateOnly? SurrenderedDate { get; private set; }

    /// <summary>
    /// The exclusive end date of licence coverage: <c>GrantDate + TermYears</c>.
    /// </summary>
    public DateOnly CoverageEndDateExclusive => GrantDate.AddYears(TermYears);

    public Result Surrender(DateOnly surrenderedDate)
    {
        if (surrenderedDate < GrantDate)
            return Result.Failure(new Error("INVALID_SURRENDER_DATE", "Surrender date cannot be before the grant date."));

        SurrenderedDate = surrenderedDate;
        return Result.Success();
    }

    /// <summary>
    /// Determines whether the given date falls within the licence's granted term,
    /// <c>[GrantDate, GrantDate + TermYears)</c>, ignoring any surrender.
    /// </summary>
    public bool IsWithinTerm(DateOnly date) => date >= GrantDate && date < CoverageEndDateExclusive;

    /// <summary>
    /// Determines whether this licence has been surrendered as of (on or before) the given date.
    /// </summary>
    public bool IsSurrenderedAsOf(DateOnly date) => SurrenderedDate.HasValue && date >= SurrenderedDate.Value;

    /// <summary>
    /// Determines whether this licence covers the given date, i.e. the date falls within
    /// <c>[GrantDate, GrantDate + TermYears)</c> and, if surrendered, is not on/after the surrender date.
    /// </summary>
    public bool CoversDate(DateOnly date) => IsWithinTerm(date) && !IsSurrenderedAsOf(date);
}
