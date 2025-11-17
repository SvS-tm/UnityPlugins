// Small polyfill for Unity / old .NET profiles that don't ship these attributes.

#nullable disable

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130 // Namespace does not match folder structure

// Specifies the nullability context for contained code.
[
    AttributeUsage
    (
        AttributeTargets.Module | AttributeTargets.Class 
            | 
        AttributeTargets.Struct | AttributeTargets.Interface 
            | 
        AttributeTargets.Delegate | AttributeTargets.Method, 
        AllowMultiple = false
    )
]
public sealed class NullableContextAttribute(byte flag) : Attribute
{
    public readonly byte Flag = flag;
}
