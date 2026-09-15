namespace Core.Data;

/// <summary>A minimal unit of work that tracks entity changes.</summary>
public abstract class DbContext : IDisposable
{
    /// <summary>Persists all tracked changes to the store.</summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The number of state entries written to the store.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the context was disposed.</exception>
    public abstract Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Releases the context.</summary>
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
