using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Utilities;

public static class Il2CppArrayExtensions
{
    public static void Il2CppCopyTo<T_Item>(this Il2CppReferenceArray<T_Item> source, Il2CppReferenceArray<T_Item> destination)
        where T_Item : Il2CppObjectBase 
    {
        var length = Math.Min(source.Length, destination.Length);

        for (var index = 0; index < length; ++index)
        {
            destination[index] = source[index];
        }
    }
}
