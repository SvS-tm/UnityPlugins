// Small polyfill for Unity / old .NET profiles that don't ship these attributes.

#nullable disable

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices;
#pragma warning restore IDE0130 // Namespace does not match folder structure


// Used by the C# compiler to encode nullable reference type metadata.
[
    AttributeUsage
    (
        AttributeTargets.Class | AttributeTargets.Struct 
            | 
        AttributeTargets.Interface | AttributeTargets.Delegate 
            | 
        AttributeTargets.Method | AttributeTargets.Property 
            |
        AttributeTargets.Field | AttributeTargets.Event 
            | 
        AttributeTargets.Parameter | AttributeTargets.ReturnValue 
            | 
        AttributeTargets.GenericParameter,
        AllowMultiple = false
    )
]
public sealed class NullableAttribute : Attribute
{
    public readonly byte[] NullableFlags;

    public NullableAttribute(byte flag)
    {
        NullableFlags = [flag];
    }

    public NullableAttribute(byte[] flags)
    {
        NullableFlags = flags;
    }
}
