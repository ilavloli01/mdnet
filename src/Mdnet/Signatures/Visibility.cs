using Microsoft.CodeAnalysis;

namespace Mdnet.Signatures;

public enum VisibilityLevel
{
    Public,
    Protected,
    Internal,
}

/// <summary>Decides which declared accessibilities are documented.</summary>
public sealed record Visibility(VisibilityLevel Level)
{
    public bool Includes(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => true,
            Accessibility.Protected or Accessibility.ProtectedOrInternal => Level >= VisibilityLevel.Protected,
            Accessibility.Internal or Accessibility.ProtectedAndInternal => Level >= VisibilityLevel.Internal,
            _ => false,
        };

    /// <summary>The symbol and all its containing types are documented.</summary>
    public bool IsVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            if (!Includes(current.DeclaredAccessibility))
            {
                return false;
            }
        }

        return true;
    }
}
