# Sample.Lib

A tiny data-access library used to exercise every part of mdnet.

## Getting started

Register the context and resolve repositories. See the [design notes](../../docs/design.md) or the [Markdoc docs](https://markdoc.dev).

```csharp
# not a heading
var repository = new EntityFrameworkRepository<Order>(context);
```
