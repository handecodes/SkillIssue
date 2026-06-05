using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SkillIssue.Data;

namespace SkillIssue.Tests.Helpers;

/// <summary>
/// Creates an in-memory SQLite AppDbContext for unit tests.
/// The connection is kept open for the lifetime of the factory so the
/// in-memory database persists across CreateDbContext calls.
/// </summary>
public sealed class SqliteDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new AppDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());

    public void Dispose() => _connection.Dispose();
}
