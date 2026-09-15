===== Sample.Lib/Core.Data.Repositories/ChangeKind.md =====
# ChangeKind

`enum` `public`

```csharp
namespace Core.Data.Repositories;

[Flags]
public enum ChangeKind
```

Describes how an entity changed.

## Values

{% member id="added" %}
```csharp
Added = 1
```

The entity was added.

{% /member %}

{% member id="modified" %}
```csharp
Modified = 2
```

The entity was modified.

{% /member %}

{% member id="removed" %}
```csharp
Removed = 4
```

The entity was removed.

{% /member %}
===== Sample.Lib/Core.Data.Repositories/EntityChangedEventArgs-1.md =====
# EntityChangedEventArgs<TEntity>

`record` `public` `sealed`

```csharp
namespace Core.Data.Repositories;

public sealed record EntityChangedEventArgs<TEntity>
```

Provides data for entity lifecycle events.

* **TEntity**: The entity type.
* **Entity**: The entity that changed.
* **Kind**: The kind of change.

## Constructors

{% member id="ctor" %}
```csharp
public EntityChangedEventArgs(TEntity Entity, ChangeKind Kind)
```

Provides data for entity lifecycle events.

* **TEntity**: The entity type.
* **Entity**: The entity that changed.
* **Kind**: The kind of change.

{% /member %}

## Properties

{% member id="entity" %}
```csharp
public TEntity Entity { get; init; }
```

The entity that changed.

{% /member %}

{% member id="kind" %}
```csharp
public ChangeKind Kind { get; init; }
```

The kind of change.

{% /member %}
===== Sample.Lib/Core.Data.Repositories/EntityFrameworkRepository-1.md =====
# EntityFrameworkRepository<TEntity>

`class` `public`

```csharp
namespace Core.Data.Repositories;

public class EntityFrameworkRepository<TEntity> : IRepository<TEntity>
    where TEntity : class
```

A generic repository implementation using Entity Framework Core to perform standard CRUD operations. Abstracting the underlying [`DbContext`](../Core.Data/DbContext.md), this class provides a foundational data access layer that can be easily inherited or used directly.

* **TEntity**: The entity type managed by the repository.

```csharp
// Example usage in a standard .NET Dependency Injection container
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IRepository<User>, EntityFrameworkRepository<User>>();
```

## Constructors

{% member id="ctor" %}
```csharp
public EntityFrameworkRepository(DbContext dbContext)
```

Initializes a new instance of the `EntityFrameworkRepository<TEntity>` class.

* **dbContext**: The database context to use.

**Throws** `ArgumentNullException`: `dbContext` is `null`.

{% /member %}

## Delegates

{% member id="entitychangedeventhandler" %}
```csharp
public delegate void EntityChangedEventHandler(
    object sender,
    EntityChangedEventArgs<TEntity> e
);
```

Represents the method that will handle events triggered when an entity undergoes a lifecycle change.

* **sender**: The source of the event, typically the repository instance.
* **e**: An object that contains the event data.

{% /member %}

## Events

{% member id="entityadded" %}
```csharp
public event EntityChangedEventHandler? EntityAdded;
```

Occurs when a new entity is successfully added to the repository.

> **Remarks**: This event is triggered synchronously after the Add operation is performed on the DbSet, but before SaveChanges is called.

{% /member %}

## Fields

{% member id="dbcontext" %}
```csharp
protected readonly DbContext _dbContext;
```

The underlying database context instance used for data access operations.

> **Remarks**: Derived classes can access this field directly to perform operations not exposed by the generic interface.

{% /member %}

## Properties

{% member id="context" %}
```csharp
public virtual DbContext Context { get; }
```

Gets the active database context for the current scope.

**Value**: The current DbContext instance injected during repository construction.

{% /member %}

{% member id="maxpagesize" %}
```csharp
public int MaxPageSize { get; set; }
```

Gets or sets the maximum page size used by [`ListAsync`](#listasync).

{% /member %}

## Static Methods

{% member id="createtransient" %}
```csharp
public static EntityFrameworkRepository<TEntity> CreateTransient(
    DbContext context
)
```

Creates a transient instance of the repository using the provided context.

* **context**: The database context to use.

**Returns**: A new, non-dependency-injected instance of the repository.

> **Remarks**: Use this method sparingly; typically repositories should be resolved from the DI container.

{% /member %}

## Methods

{% member id="addasync" %}
```csharp
public async Task<TEntity> AddAsync(
    TEntity entity,
    CancellationToken cancellationToken = default
)
```

Asynchronously adds a new entity to the underlying database context. The entity will be inserted into the database upon the next save operation.

* **entity**: The entity instance to be inserted.
* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: The inserted entity with any database-generated values applied.

> **Remarks**: This method calls AddAsync on the underlying DbSet. It does not automatically call [`SaveChangesAsync`](../Core.Data/DbContext.md#savechangesasync).

{% /member %}

{% member id="getbyidasync" %}
```csharp
public async Task<TEntity?> GetByIdAsync(
    object[] keyValues,
    CancellationToken cancellationToken = default
)
```

Asynchronously finds an entity with the given primary key values. If an entity with the given primary key values exists in the context, then it is returned immediately without making a request to the store.

* **keyValues**: The values of the primary key for the entity to be found.
* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: The entity found, or null if no entity is found.

{% /member %}

{% member id="listasync" %}
```csharp
public Task<IReadOnlyList<TEntity>> ListAsync(
    ISpecification<TEntity> specification,
    CancellationToken cancellationToken = default
)
```

Evaluates the given specification and returns the matching entities as a read-only list. Useful for complex queries encapsulating filtering, sorting, and pagination.

* **specification**: The specification containing the query criteria to apply.
* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: A read-only list of entities that match the specification criteria.

> **Remarks**: Implementations should evaluate the specification criteria against the DbSet queryable.

{% /member %}

{% member id="update" %}
```csharp
public void Update(TEntity entity)
```

Begins tracking the given entity and entries reachable from the given entity using the Modified state.

* **entity**: The entity instance to update.

> **Remarks**: Since this operation only changes tracking state, it is synchronous. Changes are pushed to the database only when SaveChanges is called.

{% /member %}

{% member id="onadded" %}
```csharp
protected virtual void OnAdded(TEntity entity)
```

Called after an entity has been added.

* **entity**: The added entity.

{% /member %}
===== Sample.Lib/Core.Data.Repositories/IRepository-1.md =====
# IRepository<TEntity>

`interface` `public`

```csharp
namespace Core.Data.Repositories;

public interface IRepository<TEntity>
    where TEntity : class
```

Defines the standard data access operations for an aggregate.

* **TEntity**: The entity type managed by the repository.

## Methods

{% member id="addasync" %}
```csharp
Task<TEntity> AddAsync(
    TEntity entity,
    CancellationToken cancellationToken = default
)
```

Asynchronously adds a new entity to the repository.

* **entity**: The entity instance to be inserted.
* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: The inserted entity with any database-generated values applied.

{% /member %}

{% member id="update" %}
```csharp
void Update(TEntity entity)
```

Begins tracking the given entity using the Modified state.

* **entity**: The entity instance to update.

{% /member %}
===== Sample.Lib/Core.Data.Repositories/ISpecification-1.md =====
# ISpecification<T>

`interface` `public`

```csharp
namespace Core.Data.Repositories;

public interface ISpecification<in T>
```

Encapsulates query criteria for `T`.

* **T**: The queried entity type.

## Methods

{% member id="issatisfiedby" %}
```csharp
bool IsSatisfiedBy(T entity)
```

Determines whether the entity satisfies the specification.

* **entity**: The entity to evaluate.

**Returns**: `true` when the entity matches; otherwise `false`.

{% /member %}
===== Sample.Lib/Core.Data/DbContext.md =====
# DbContext

`class` `public` `abstract`

```csharp
namespace Core.Data;

public abstract class DbContext : IDisposable
```

A minimal unit of work that tracks entity changes.

## Constructors

{% member id="ctor" %}
```csharp
protected DbContext()
```

Initializes a new instance of the `object` class.

{% /member %}

## Methods

{% member id="savechangesasync" %}
```csharp
public abstract Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default
)
```

Persists all tracked changes to the store.

* **cancellationToken**: A token to observe for cancellation requests.

**Returns**: The number of state entries written to the store.

**Throws** `InvalidOperationException`: Thrown when the context was disposed.

{% /member %}

{% member id="dispose" %}
```csharp
public virtual void Dispose()
```

Releases the context.

{% /member %}
===== Sample.Lib/Core.Data/Money.md =====
# Money

`struct` `public` `readonly`

```csharp
namespace Core.Data;

public readonly struct Money : IEquatable<Money>
```

An immutable amount of money in a specific currency.

> **Remarks**: Arithmetic is only defined for amounts in the same currency.
>
> * Amounts are rounded to 2 decimal places.
> * Currency codes follow ISO 4217.

## Constructors

{% member id="ctor" %}
```csharp
public Money(decimal amount, string currency = "EUR")
```

Creates a new amount.

* **amount**: The amount.
* **currency**: The ISO 4217 currency code, e.g. `"EUR"`.

{% /member %}

## Fields

{% member id="zero" %}
```csharp
public static readonly Money Zero;
```

A zero amount in euros.

{% /member %}

## Properties

{% member id="amount" %}
```csharp
public decimal Amount { get; }
```

Gets the amount.

{% /member %}

{% member id="currency" %}
```csharp
public string Currency { get; init; }
```

Gets the ISO 4217 currency code.

{% /member %}

## Methods

{% member id="equals" %}
```csharp
public bool Equals(Money other)
```

Indicates whether the current object is equal to another object of the same type.

* **other**: An object to compare with this object.

**Returns**: `true` if the current object is equal to the `other` parameter; otherwise, `false`.

{% /member %}

{% member id="equals-2" %}
```csharp
public override bool Equals(object? obj)
```

Indicates whether this instance and a specified object are equal.

* **obj**: The object to compare with the current instance.

**Returns**: `true` if `obj` and this instance are the same type and represent the same value; otherwise, `false`.

{% /member %}

{% member id="gethashcode" %}
```csharp
public override int GetHashCode()
```

Returns the hash code for this instance.

**Returns**: A 32-bit signed integer that is the hash code for this instance.

{% /member %}

## Operators

{% member id="op-addition" %}
```csharp
public static Money operator +(Money left, Money right)
```

Adds two amounts.

* **left**: The first amount.
* **right**: The second amount.

**Returns**: The sum of `left` and `right`.

**Throws** `InvalidOperationException`: The currencies differ.

{% /member %}

{% member id="op-implicit" %}
```csharp
public static implicit operator Money(decimal amount)
```

Converts a decimal to an amount in euros.

* **amount**: The amount.

{% /member %}
===== Sample.Lib/Core.Data/MoneyExtensions.md =====
# MoneyExtensions

`class` `public` `static`

```csharp
namespace Core.Data;

public static class MoneyExtensions
```

Extension methods for [`Money`](Money.md).

## Extension Methods

{% member id="sum" %}
```csharp
public static Money Sum(this IEnumerable<Money> source)
```

Sums a sequence of amounts.

* **source**: The amounts to sum.

**Returns**: The total, or [`Zero`](Money.md#zero) for an empty sequence.

{% /member %}

{% member id="format" %}
```csharp
public static string Format<TFormatter>(this Money money, TFormatter formatter)
    where TFormatter : IFormatProvider, new()
```

Formats the amount for display.

* **TFormatter**: The formatter type.
* **money**: The amount.
* **formatter**: The formatter.

**Returns**: The formatted amount, e.g. `12.50 EUR`.

{% /member %}
===== Sample.Lib/index.md =====
# Sample.Lib 1.2.3

> Sample library used to test mdnet.

## Core.Data

* [DbContext](Core.Data/DbContext.md): A minimal unit of work that tracks entity changes.
* [Money](Core.Data/Money.md): An immutable amount of money in a specific currency.
* [MoneyExtensions](Core.Data/MoneyExtensions.md): Extension methods for [`Money`](Core.Data/Money.md).

## Core.Data.Repositories

* [ChangeKind](Core.Data.Repositories/ChangeKind.md): Describes how an entity changed.
* [EntityChangedEventArgs<TEntity>](Core.Data.Repositories/EntityChangedEventArgs-1.md): Provides data for entity lifecycle events.
* [EntityFrameworkRepository<TEntity>](Core.Data.Repositories/EntityFrameworkRepository-1.md): A generic repository implementation using Entity Framework Core to perform standard CRUD operations.
* [IRepository<TEntity>](Core.Data.Repositories/IRepository-1.md): Defines the standard data access operations for an aggregate.
* [ISpecification<T>](Core.Data.Repositories/ISpecification-1.md): Encapsulates query criteria for `T`.
===== Sample.Lib/mdnet.json =====
{
  "schema": 1,
  "id": "Sample.Lib",
  "version": "1.2.3",
  "files": {
    "Core.Data.Repositories/ChangeKind.md": "708bf11bdeb12eba2074e049d921e36801f071a9db95f2dcb50622827efc7817",
    "Core.Data.Repositories/EntityChangedEventArgs-1.md": "fd67eb97006508d7a299c59c8396392526cad875c64eb50b0e1179a70e58c5ae",
    "Core.Data.Repositories/EntityFrameworkRepository-1.md": "e09a8978b9a6aca8aa2dc7bf64232617bff9c7ca05dc934ecf8f33c93fc31e55",
    "Core.Data.Repositories/IRepository-1.md": "a18801602cb4629bf3addc7ccacbb3ff45271666a722aad1da17bc16838d62c8",
    "Core.Data.Repositories/ISpecification-1.md": "f00fc5619cf63f9eff124ae4a2e52a14ac9661dd949b589be47302ffcbe992e9",
    "Core.Data/DbContext.md": "f20f134ce76db34a51fa710408fb0f827c0708347539d8a95e15ee6e921758e6",
    "Core.Data/Money.md": "9c487680333cf4df11759137dfaadc589932f3f8909b6acb29ede7aad9e6a268",
    "Core.Data/MoneyExtensions.md": "dc95b4284b21f23efcf10909c80f3ac62310db908a8846c7568fb0c36bd4fdc5",
    "index.md": "f20cbecd71b2b3a9bcddd14b45cf72b2f0d0a6bf8d92e7b39872db4cc5fb55ca"
  }
}
===== index.md =====
# API documentation

* [Sample.Lib 1.2.3](Sample.Lib/index.md): Sample library used to test mdnet.
