using Microsoft.EntityFrameworkCore;

namespace Keydral.Storage.Repositories;

/// <summary>
/// Generic repository base class implementing standard CRUD operations.
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public abstract class Repository<T> : IRepository<T> where T : class
{
    /// <summary>
    /// The DbContext instance.
    /// </summary>
    protected readonly ApplicationDbContext Context;

    /// <summary>
    /// Constructor.
    /// </summary>
    protected Repository(ApplicationDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get an entity by ID.
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().FindAsync(new object[] { id }, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get all entities.
    /// </summary>
    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Add a new entity.
    /// </summary>
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await Context.Set<T>().AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Update an existing entity.
    /// </summary>
    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Context.Set<T>().Update(entity);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>
    /// Delete an entity by ID.
    /// </summary>
    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            Context.Set<T>().Remove(entity);
            await Context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Check if an entity exists by ID.
    /// </summary>
    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Context.Set<T>().AsNoTracking().FirstOrDefaultAsync(
            e => EF.Property<Guid>(e, "Id") == id, cancellationToken) != null;
    }

    /// <summary>
    /// Save changes to the database.
    /// </summary>
    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken);
    }
}
