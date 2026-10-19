using Blocks.Core;
using Blocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Blocks.EntityFrameworkCore;

/// <summary>
/// One seam per aggregate. The repository is also the unit of work (SaveChangesAsync) —
/// there is deliberately no IUnitOfWork and no IRepository with a single implementation.
/// </summary>
public abstract class RepositoryBase<TContext, TEntity>(TContext dbContext)
    where TContext : DbContext
    where TEntity : class, IEntity
{
    protected TContext Context { get; } = dbContext;
    protected DbSet<TEntity> Entities { get; } = dbContext.Set<TEntity>();

    public virtual IQueryable<TEntity> Query() => Entities;

    public virtual Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Query().SingleOrDefaultAsync(entity => entity.Id == id, ct);

    public async Task<TEntity> GetByIdOrThrowAsync(Guid id, CancellationToken ct = default) =>
        Guard.NotFound(await GetByIdAsync(id, ct));

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        Entities.AnyAsync(entity => entity.Id == id, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await Entities.AddAsync(entity, ct);

    public void Remove(TEntity entity) => Entities.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
}
