using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mdnet.Signatures;

/// <summary>Renders symbols as C# declarations without bodies.</summary>
public static class SignatureWriter
{
    private const int WrapWidth = 80;

    /// <summary>Short type references: <c>Task&lt;IReadOnlyList&lt;TEntity&gt;?&gt;</c>.</summary>
    public static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
            | SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays
    );

    /// <summary>Type names in headings and links: <c>Outer.Inner&lt;T&gt;</c>.</summary>
    public static readonly SymbolDisplayFormat TypeNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    private static readonly SymbolDisplayFormat VarianceTypeParametersFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance
    );

    // ---- types ------------------------------------------------------------------------------

    public static string TypeKind(INamedTypeSymbol type) =>
        type.TypeKind switch
        {
            Microsoft.CodeAnalysis.TypeKind.Interface => "interface",
            Microsoft.CodeAnalysis.TypeKind.Enum => "enum",
            Microsoft.CodeAnalysis.TypeKind.Delegate => "delegate",
            Microsoft.CodeAnalysis.TypeKind.Struct when type.IsRecord => "record struct",
            Microsoft.CodeAnalysis.TypeKind.Struct => "struct",
            _ when type.IsRecord => "record",
            _ => "class",
        };

    public static IReadOnlyList<string> TypeModifiers(INamedTypeSymbol type)
    {
        var modifiers = new List<string>();
        if (type.IsStatic)
        {
            modifiers.Add("static");
        }
        else if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Class)
        {
            if (type.IsAbstract)
            {
                modifiers.Add("abstract");
            }
            else if (type.IsSealed)
            {
                modifiers.Add("sealed");
            }
        }

        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Struct)
        {
            if (type.IsReadOnly)
            {
                modifiers.Add("readonly");
            }

            if (type.IsRefLikeType)
            {
                modifiers.Add("ref");
            }
        }

        return modifiers;
    }

    public static string TypeDeclaration(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        AppendAttributes(sb, type);

        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Delegate && type.DelegateInvokeMethod is { } invoke)
        {
            sb.Append(Accessibility(type.DeclaredAccessibility)).Append(" delegate ");
            AppendReturnType(sb, invoke);
            sb.Append(' ').Append(TypeParameterList(type.Name, type.TypeParameters));
            AppendParameters(sb, invoke.Parameters, isExtension: false, open: "(", close: ")");
            AppendConstraints(sb, type.TypeParameters);
            return sb.Append(';').ToString();
        }

        sb.Append(Accessibility(type.DeclaredAccessibility));
        foreach (var modifier in TypeModifiers(type))
        {
            sb.Append(' ').Append(modifier);
        }

        sb.Append(' ').Append(TypeKind(type)).Append(' ');
        sb.Append(type.ToDisplayString(VarianceTypeParametersFormat));

        var bases = new List<string>();
        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            if (type.EnumUnderlyingType is { SpecialType: not SpecialType.System_Int32 } underlying)
            {
                bases.Add(underlying.ToDisplayString(TypeFormat));
            }
        }
        else
        {
            if (
                type.BaseType is { } baseType
                && baseType.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType)
            )
            {
                bases.Add(baseType.ToDisplayString(TypeFormat));
            }

            bases.AddRange(
                type.Interfaces.Where(i => !(type.IsRecord && IsRecordEquatable(type, i)))
                    .Select(i => i.ToDisplayString(TypeFormat))
            );
        }

        if (bases.Count > 0)
        {
            sb.Append(" : ").Append(string.Join(", ", bases));
        }

        AppendConstraints(sb, type.TypeParameters);
        return sb.ToString();
    }

    private static bool IsRecordEquatable(INamedTypeSymbol record, INamedTypeSymbol iface) =>
        iface is { Name: "IEquatable", TypeArguments.Length: 1 }
        && SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], record);

    // ---- members ----------------------------------------------------------------------------

    public static string MemberSignature(ISymbol member, Visibility visibility)
    {
        var signature = member switch
        {
            IMethodSymbol method => Method(method),
            IPropertySymbol property => Property(property, visibility),
            IFieldSymbol field => Field(field),
            IEventSymbol @event => Event(@event),
            INamedTypeSymbol type => TypeDeclaration(type),
            _ => member.ToDisplayString(),
        };

        // Inside a type, its nested types need no qualification: EntityChangedEventHandler, not Repo<T>.EntityChangedEventHandler.
        for (var containing = member.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            signature = signature.Replace(containing.ToDisplayString(TypeFormat) + ".", "", StringComparison.Ordinal);
        }

        return signature;
    }

    private static string Method(IMethodSymbol method)
    {
        var sb = new StringBuilder();
        AppendAttributes(sb, method);
        AppendMemberModifiers(sb, method);

        switch (method.MethodKind)
        {
            case MethodKind.Constructor:
                sb.Append(method.ContainingType.Name);
                break;
            case MethodKind.Conversion:
                sb.Append(method.Name.Contains("Explicit", StringComparison.Ordinal) ? "explicit" : "implicit");
                sb.Append(" operator ");
                sb.Append(method.ReturnType.ToDisplayString(TypeFormat));
                break;
            case MethodKind.UserDefinedOperator:
                AppendReturnType(sb, method);
                sb.Append(" operator ").Append(OperatorToken(method.Name));
                break;
            default:
                AppendReturnType(sb, method);
                sb.Append(' ').Append(TypeParameterList(Identifier(method.Name), method.TypeParameters));
                break;
        }

        AppendParameters(sb, method.Parameters, method.IsExtensionMethod, "(", ")");
        AppendConstraints(sb, method.TypeParameters);
        return sb.ToString();
    }

    private static string Property(IPropertySymbol property, Visibility visibility)
    {
        var sb = new StringBuilder();
        AppendAttributes(sb, property);
        AppendMemberModifiers(sb, property);
        if (property.ReturnsByRefReadonly)
        {
            sb.Append("ref readonly ");
        }
        else if (property.ReturnsByRef)
        {
            sb.Append("ref ");
        }

        sb.Append(property.Type.ToDisplayString(TypeFormat)).Append(' ');
        if (property.IsIndexer)
        {
            sb.Append("this");
            AppendParameters(sb, property.Parameters, isExtension: false, "[", "]");
        }
        else
        {
            sb.Append(Identifier(property.Name));
        }

        sb.Append(" {");
        AppendAccessor(sb, property, property.GetMethod, "get", visibility);
        AppendAccessor(sb, property, property.SetMethod, property.SetMethod?.IsInitOnly == true ? "init" : "set", visibility);
        return sb.Append(" }").ToString();
    }

    private static void AppendAccessor(StringBuilder sb, IPropertySymbol property, IMethodSymbol? accessor, string keyword, Visibility visibility)
    {
        if (accessor is null || !visibility.Includes(accessor.DeclaredAccessibility))
        {
            return;
        }

        sb.Append(' ');
        if (accessor.DeclaredAccessibility != property.DeclaredAccessibility)
        {
            sb.Append(Accessibility(accessor.DeclaredAccessibility)).Append(' ');
        }

        sb.Append(keyword).Append(';');
    }

    private static string Field(IFieldSymbol field)
    {
        var sb = new StringBuilder();
        AppendAttributes(sb, field);
        if (field.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum)
        {
            sb.Append(Identifier(field.Name));
            if (field.HasConstantValue)
            {
                sb.Append(" = ").Append(Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        AppendMemberModifiers(sb, field);
        sb.Append(field.Type.ToDisplayString(TypeFormat)).Append(' ').Append(Identifier(field.Name));
        if (field.IsConst && field.HasConstantValue)
        {
            sb.Append(" = ").Append(Literal(field.ConstantValue, field.Type));
        }

        return sb.Append(';').ToString();
    }

    private static string Event(IEventSymbol @event)
    {
        var sb = new StringBuilder();
        AppendAttributes(sb, @event);
        AppendMemberModifiers(sb, @event);
        sb.Append("event ").Append(@event.Type.ToDisplayString(TypeFormat)).Append(' ').Append(Identifier(@event.Name));
        return sb.Append(';').ToString();
    }

    // ---- pieces -----------------------------------------------------------------------------

    private static void AppendMemberModifiers(StringBuilder sb, ISymbol member)
    {
        var inInterface = member.ContainingType.TypeKind == Microsoft.CodeAnalysis.TypeKind.Interface;
        var modifiers = new List<string>();
        if (!inInterface || member.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public)
        {
            modifiers.Add(Accessibility(member.DeclaredAccessibility));
        }

        if (member is IFieldSymbol { IsConst: true })
        {
            modifiers.Add("const");
        }
        else if (member.IsStatic)
        {
            modifiers.Add("static");
        }

        if (member.IsAbstract && !inInterface)
        {
            modifiers.Add("abstract");
        }
        else if (member.IsAbstract && member.IsStatic)
        {
            modifiers.Add("abstract");
        }

        if (member.IsVirtual && !(inInterface && !member.IsStatic))
        {
            modifiers.Add("virtual");
        }

        if (member.IsOverride)
        {
            modifiers.Add(member.IsSealed ? "sealed override" : "override");
        }

        if (member is IFieldSymbol { IsReadOnly: true })
        {
            modifiers.Add("readonly");
        }

        if (member is IFieldSymbol { IsRequired: true } or IPropertySymbol { IsRequired: true })
        {
            modifiers.Add("required");
        }

        if (member is IMethodSymbol { IsReadOnly: true } && !member.ContainingType.IsReadOnly)
        {
            modifiers.Add("readonly");
        }

        if (member is IMethodSymbol { IsAsync: true })
        {
            modifiers.Add("async");
        }

        foreach (var modifier in modifiers.Where(m => m.Length > 0))
        {
            sb.Append(modifier).Append(' ');
        }
    }

    private static void AppendReturnType(StringBuilder sb, IMethodSymbol method)
    {
        if (method.ReturnsByRefReadonly)
        {
            sb.Append("ref readonly ");
        }
        else if (method.ReturnsByRef)
        {
            sb.Append("ref ");
        }

        sb.Append(method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(TypeFormat));
    }

    private static void AppendParameters(
        StringBuilder sb,
        IReadOnlyList<IParameterSymbol> parameters,
        bool isExtension,
        string open,
        string close
    )
    {
        var rendered = parameters.Select((p, i) => Parameter(p, isExtension && i == 0)).ToList();
        var lastLineLength = sb.Length - (sb.ToString().LastIndexOf('\n') + 1);
        var oneLine = string.Join(", ", rendered);
        if (lastLineLength + oneLine.Length + 2 <= WrapWidth || rendered.Count == 0)
        {
            sb.Append(open).Append(oneLine).Append(close);
            return;
        }

        sb.Append(open).Append('\n');
        sb.Append(string.Join(",\n", rendered.Select(r => "    " + r)));
        sb.Append('\n').Append(close);
    }

    private static string Parameter(IParameterSymbol parameter, bool isThis)
    {
        var sb = new StringBuilder();
        if (parameter.IsParams)
        {
            sb.Append("params ");
        }

        if (isThis)
        {
            sb.Append("this ");
        }

        sb.Append(
            parameter.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                RefKind.In => "in ",
                RefKind.RefReadOnlyParameter => "ref readonly ",
                _ => "",
            }
        );
        sb.Append(parameter.Type.ToDisplayString(TypeFormat)).Append(' ').Append(Identifier(parameter.Name));
        if (parameter.HasExplicitDefaultValue)
        {
            sb.Append(" = ").Append(Literal(parameter.ExplicitDefaultValue, parameter.Type));
        }

        return sb.ToString();
    }

    private static void AppendConstraints(StringBuilder sb, IReadOnlyList<ITypeParameterSymbol> typeParameters)
    {
        foreach (var tp in typeParameters)
        {
            var constraints = new List<string>();
            if (tp.HasReferenceTypeConstraint)
            {
                constraints.Add(
                    tp.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class"
                );
            }
            else if (tp.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }
            else if (tp.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (tp.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            constraints.AddRange(
                tp.ConstraintTypes.Where(t => !(tp.HasValueTypeConstraint && t.SpecialType == SpecialType.System_ValueType))
                    .Select(t => t.ToDisplayString(TypeFormat))
            );
            if (tp.HasConstructorConstraint && !tp.HasValueTypeConstraint)
            {
                constraints.Add("new()");
            }

            if (tp.AllowsRefLikeType)
            {
                constraints.Add("allows ref struct");
            }

            if (constraints.Count > 0)
            {
                sb.Append("\n    where ").Append(tp.Name).Append(" : ").Append(string.Join(", ", constraints));
            }
        }
    }

    private static void AppendAttributes(StringBuilder sb, ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            switch (name)
            {
                case "System.FlagsAttribute":
                    sb.Append("[Flags]\n");
                    break;
                case "System.ObsoleteAttribute":
                    var message = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                    sb.Append(message is null ? "[Obsolete]\n" : $"[Obsolete({SymbolDisplay.FormatLiteral(message, true)})]\n");
                    break;
                case "System.Diagnostics.CodeAnalysis.ExperimentalAttribute":
                    var id = attribute.ConstructorArguments.FirstOrDefault().Value as string;
                    sb.Append($"[Experimental({SymbolDisplay.FormatLiteral(id ?? "", true)})]\n");
                    break;
            }
        }
    }

    private static string TypeParameterList(string name, IReadOnlyList<ITypeParameterSymbol> typeParameters) =>
        typeParameters.Count == 0
            ? name
            : $"{name}<{string.Join(", ", typeParameters.Select(tp => tp.Variance switch
            {
                VarianceKind.In => "in " + tp.Name,
                VarianceKind.Out => "out " + tp.Name,
                _ => tp.Name,
            }))}>";

    private static string Literal(object? value, ITypeSymbol type)
    {
        if (type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Enum && value is not null)
        {
            var field = type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));
            return field is null
                ? $"({type.ToDisplayString(TypeFormat)}){Convert.ToString(value, CultureInfo.InvariantCulture)}"
                : $"{type.ToDisplayString(TypeFormat)}.{field.Name}";
        }

        return value switch
        {
            null => type.IsReferenceType || type.NullableAnnotation == NullableAnnotation.Annotated || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? "null" : "default",
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            bool b => b ? "true" : "false",
            float f => f.ToString("R", CultureInfo.InvariantCulture) + "f",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "default",
        };
    }

    private static string OperatorToken(string metadataName)
    {
        var kind = SyntaxFacts.GetOperatorKind(metadataName);
        var token = kind == SyntaxKind.None ? metadataName : SyntaxFacts.GetText(kind);
        return metadataName.StartsWith("op_Checked", StringComparison.Ordinal) ? "checked " + token : token;
    }

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    public static string Accessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Microsoft.CodeAnalysis.Accessibility.Public => "public",
            Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
            Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
            Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
            Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
            Microsoft.CodeAnalysis.Accessibility.Private => "private",
            _ => "",
        };
}
