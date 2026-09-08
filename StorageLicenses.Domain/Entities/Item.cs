namespace StorageLicenses.Domain.Entities;

/// <summary>
/// An item that can be placed into a storage unit. Only <see cref="IntakeDate"/> affects
/// scheduling rules.
/// </summary>
public class Item
{
    /// <summary>Reserved for EF Core materialization.</summary>
    protected Item()
    {
        Reference = string.Empty;
        Description = string.Empty;
    }

    public Item(string reference, string description, DateOnly? intakeDate = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        Reference = reference;
        Description = description;
        IntakeDate = intakeDate;
    }

    public int Id { get; private set; }

    public string Reference { get; private set; }

    public string Description { get; private set; }

    public DateOnly? IntakeDate { get; private set; }

    public void RecordIntake(DateOnly intakeDate) => IntakeDate = intakeDate;
}
