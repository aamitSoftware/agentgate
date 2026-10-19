using Blocks.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.EntityFrameworkCore.Tests;

/// <summary>
/// The fix for the course's dual-write bug: whatever a domain-event handler adds to the DbContext
/// (in production: MassTransit outbox rows; here: an OutboxRecord stand-in) commits or rolls back
/// together with the change that raised the event.
/// </summary>
public sealed class DispatchDomainEventsInterceptorTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync(); // in-memory SQLite lives as long as this connection

        _services = new ServiceCollection()
            .AddDomainEventDispatch(typeof(DispatchDomainEventsInterceptorTests).Assembly)
            .AddDbContext<TestDbContext>((sp, options) => options
                .UseSqlite(_connection)
                .AddInterceptors(sp.GetRequiredService<DispatchDomainEventsInterceptor>()))
            .BuildServiceProvider();

        await InScope(db => db.Database.EnsureCreatedAsync());
    }

    [Fact]
    public async Task Handler_side_effects_commit_with_the_triggering_change()
    {
        var order = Order.Place("first");

        await InScope(async db =>
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync(); // a single save — no second save for the "outbox" row
        });

        await InScope(async db =>
        {
            Assert.Equal(1, await db.Orders.CountAsync());
            Assert.Equal(order.Id, (await db.OutboxRecords.SingleAsync()).OrderId);
        });
    }

    [Fact]
    public async Task Handler_side_effects_roll_back_when_the_save_fails()
    {
        await InScope(async db =>
        {
            db.Orders.Add(Order.Place("duplicate"));
            await db.SaveChangesAsync();
        });

        await InScope(async db =>
        {
            db.Orders.Add(Order.Place("duplicate")); // violates the unique index on Name
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });

        await InScope(async db =>
        {
            Assert.Equal(1, await db.Orders.CountAsync());
            Assert.Equal(1, await db.OutboxRecords.CountAsync()); // only the first order's record survived
        });
    }

    [Fact]
    public async Task Sync_save_with_pending_events_is_rejected_instead_of_dropping_them()
    {
        await InScope(db =>
        {
            db.Orders.Add(Order.Place("sync"));
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            return Task.CompletedTask;
        });
    }

    private async Task InScope(Func<TestDbContext, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed class Order : AggregateRoot
{
    public string Name { get; private init; } = default!;

    public static Order Place(string name)
    {
        var order = new Order { Name = name, CreatedBy = "test", CreatedOn = DateTime.UtcNow };
        order.AddDomainEvent(new OrderPlaced(order));
        return order;
    }
}

public sealed record OrderPlaced(Order Order) : IDomainEvent;

public sealed class OutboxRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OrderId { get; init; }
}

public sealed class WriteOutboxRecordOnOrderPlaced(TestDbContext db) : IDomainEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced domainEvent, CancellationToken ct)
    {
        db.OutboxRecords.Add(new OutboxRecord { OrderId = domainEvent.Order.Id });
        return Task.CompletedTask;
    }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxRecord> OutboxRecords => Set<OutboxRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.Entity<OutboxRecord>().HasKey(record => record.Id);
    }
}

public sealed class OrderConfiguration : AuditedEntityConfiguration<Order>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);
        builder.HasIndex(order => order.Name).IsUnique();
    }
}
