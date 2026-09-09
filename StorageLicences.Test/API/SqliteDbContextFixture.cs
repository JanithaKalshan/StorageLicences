using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StorageLicenses.Infastructure.Repositories;

namespace StorageLicences.Test.API;

/// <summary>
/// Provides a real <see cref="ApplicationDbContext"/> backed by an in-memory SQLite database so
/// Infrastructure services (which rely on relational features such as explicit transactions,
/// unsupported by the EF Core InMemory provider) can be exercised deterministically without a
/// real SQL Server instance.
/// </summary>
public sealed class SqliteDbContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Context { get; }

    public SqliteDbContextFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
