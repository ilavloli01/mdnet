namespace Core.Data.Repositories;

/// <summary>Defines the standard data access operations for an aggregate.</summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
public interface IRepository<TEntity>
    where TEntity : class
{
    /// <summary>Asynchronously adds a new entity to the repository.</summary>
    /// <param name="entity">The entity instance to be inserted.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The inserted entity with any database-generated values applied.</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Begins tracking the given entity using the Modified state.</summary>
    /// <param name="entity">The entity instance to update.</param>
    void Update(TEntity entity);
}

/// <summary>Encapsulates query criteria for <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The queried entity type.</typeparam>
public interface ISpecification<in T>
{
    /// <summary>Determines whether the entity satisfies the specification.</summary>
    /// <param name="entity">The entity to evaluate.</param>
    /// <returns><see langword="true"/> when the entity matches; otherwise <see langword="false"/>.</returns>
    bool IsSatisfiedBy(T entity);
}

/// <summary>Provides data for entity lifecycle events.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="Entity">The entity that changed.</param>
/// <param name="Kind">The kind of change.</param>
public sealed record EntityChangedEventArgs<TEntity>(TEntity Entity, ChangeKind Kind);

/// <summary>Describes how an entity changed.</summary>
[Flags]
public enum ChangeKind
{
    /// <summary>The entity was added.</summary>
    Added = 1,

    /// <summary>The entity was modified.</summary>
    Modified = 2,

    /// <summary>The entity was removed.</summary>
    Removed = 4,
}
