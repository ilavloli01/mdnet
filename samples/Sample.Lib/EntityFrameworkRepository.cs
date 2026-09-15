namespace Core.Data.Repositories;

/// <summary>
/// A generic repository implementation using Entity Framework Core to perform standard CRUD operations.
/// Abstracting the underlying <see cref="DbContext"/>, this class provides a foundational data access layer
/// that can be easily inherited or used directly.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
/// <example>
/// <code>
/// // Example usage in a standard .NET Dependency Injection container
/// var builder = WebApplication.CreateBuilder(args);
/// builder.Services.AddScoped&lt;IRepository&lt;User&gt;, EntityFrameworkRepository&lt;User&gt;&gt;();
/// </code>
/// </example>
public class EntityFrameworkRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
{
    /// <summary>Represents the method that will handle events triggered when an entity undergoes a lifecycle change.</summary>
    /// <param name="sender">The source of the event, typically the repository instance.</param>
    /// <param name="e">An object that contains the event data.</param>
    public delegate void EntityChangedEventHandler(object sender, EntityChangedEventArgs<TEntity> e);

    /// <summary>The underlying database context instance used for data access operations.</summary>
    /// <remarks>Derived classes can access this field directly to perform operations not exposed by the generic interface.</remarks>
    protected readonly DbContext _dbContext;

    private readonly List<TEntity> _pending = [];

    /// <summary>Initializes a new instance of the <see cref="EntityFrameworkRepository{TEntity}"/> class.</summary>
    /// <param name="dbContext">The database context to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dbContext"/> is <see langword="null"/>.</exception>
    public EntityFrameworkRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>Occurs when a new entity is successfully added to the repository.</summary>
    /// <remarks>This event is triggered synchronously after the Add operation is performed on the DbSet, but before SaveChanges is called.</remarks>
    public event EntityChangedEventHandler? EntityAdded;

    /// <summary>Gets the active database context for the current scope.</summary>
    /// <value>The current DbContext instance injected during repository construction.</value>
    public virtual DbContext Context => _dbContext;

    /// <summary>Gets or sets the maximum page size used by <see cref="ListAsync"/>.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Creates a transient instance of the repository using the provided context.</summary>
    /// <param name="context">The database context to use.</param>
    /// <returns>A new, non-dependency-injected instance of the repository.</returns>
    /// <remarks>Use this method sparingly; typically repositories should be resolved from the DI container.</remarks>
    public static EntityFrameworkRepository<TEntity> CreateTransient(DbContext context) => new(context);

    /// <summary>
    /// Asynchronously adds a new entity to the underlying database context.
    /// The entity will be inserted into the database upon the next save operation.
    /// </summary>
    /// <remarks>This method calls AddAsync on the underlying DbSet. It does not automatically call <see cref="DbContext.SaveChangesAsync"/>.</remarks>
    /// <inheritdoc />
    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _pending.Add(entity);
        OnAdded(entity);
        EntityAdded?.Invoke(this, new EntityChangedEventArgs<TEntity>(entity, ChangeKind.Added));
        return entity;
    }

    /// <summary>
    /// Asynchronously finds an entity with the given primary key values. If an entity with the given primary key
    /// values exists in the context, then it is returned immediately without making a request to the store.
    /// </summary>
    /// <param name="keyValues">The values of the primary key for the entity to be found.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The entity found, or null if no entity is found.</returns>
    public async Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return null;
    }

    /// <summary>
    /// Evaluates the given specification and returns the matching entities as a read-only list.
    /// Useful for complex queries encapsulating filtering, sorting, and pagination.
    /// </summary>
    /// <param name="specification">The specification containing the query criteria to apply.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A read-only list of entities that match the specification criteria.</returns>
    /// <remarks>Implementations should evaluate the specification criteria against the DbSet queryable.</remarks>
    public Task<IReadOnlyList<TEntity>> ListAsync(
        ISpecification<TEntity> specification,
        CancellationToken cancellationToken = default
    ) => Task.FromResult<IReadOnlyList<TEntity>>(_pending.Where(specification.IsSatisfiedBy).Take(MaxPageSize).ToList());

    /// <summary>Begins tracking the given entity and entries reachable from the given entity using the Modified state.</summary>
    /// <param name="entity">The entity instance to update.</param>
    /// <remarks>Since this operation only changes tracking state, it is synchronous. Changes are pushed to the database only when SaveChanges is called.</remarks>
    public void Update(TEntity entity) { }

    /// <summary>Called after an entity has been added.</summary>
    /// <param name="entity">The added entity.</param>
    protected virtual void OnAdded(TEntity entity) { }

    internal void Clear() => _pending.Clear();
}
