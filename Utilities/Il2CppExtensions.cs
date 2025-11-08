using System.Collections.Concurrent;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Utilities;

public static class Il2CppExtensions
{
    // Cache Il2Cpp types – avoids repeated lookups/locks
    private static readonly ConcurrentDictionary<System.Type, Il2CppSystem.Type> cache = new();

    private static Il2CppSystem.Type ResolveIl2CppType(System.Type type)
    {
        var resolve = new System.Func<System.Type, Il2CppSystem.Type>
        (
            (type) =>
            {
                // Fast path: if already known to il2cpp, this succeeds
                try
                {
                    return Il2CppType.From(type);
                }
                catch
                {
                    // Only our own classes should be registered
                    if (type.Assembly != System.Reflection.Assembly.GetCallingAssembly())
                    {
                        throw new System.MissingMemberException
                        (
                            $"Type {type.FullName} is not an IL2CPP type and does not belong to this plugin. "
                        );
                    }

                    if (!ClassInjector.IsTypeRegisteredInIl2Cpp(type))
                    {
                        ClassInjector.RegisterTypeInIl2Cpp(type);
                    }

                    return Il2CppType.From(type);
                }
            }
        );

        var resolved = cache.GetOrAdd(type, resolve);

        return resolved;
    }

    public static T_Component? Il2CppAddComponent<T_Component>(this GameObject host)
        where T_Component : Component
    {
        var component = host.AddComponent(ResolveIl2CppType(typeof(T_Component)));

        return component?.TryCast<T_Component>();
    }

    public static T_Component? Il2CppGetComponent<T_Component>(this GameObject host)
        where T_Component : Component
    {
        var component = host.GetComponent(ResolveIl2CppType(typeof(T_Component)));

        return component?.TryCast<T_Component>();
    }

    public static T_Component? Il2CppGetComponent<T_Component>(this Component host)
    where T_Component : Component
    {
        return host.GetComponent(ResolveIl2CppType(typeof(T_Component)))?.TryCast<T_Component>();
    }

    public static Il2CppArrayBase<T_Component?> IL2CppGetComponentsInChildren<T_Component>(this Transform transform, bool includeInactive)
        where T_Component : Component
    {
        var components = transform.GetComponentsInChildren(ResolveIl2CppType(typeof(T_Component)), includeInactive);

        var result = new Il2CppReferenceArray<T_Component?>(components.Length);

        for (var index = 0; index < components.Length; ++index)
        {
            result[index] = components[index].TryCast<T_Component>();
        }

        return result;
    }

    public static Il2CppArrayBase<T_Object?> Il2CppFindObjectsByType<T_Object>
    (
        this Object target, 
        FindObjectsInactive findObjectsInactive = FindObjectsInactive.Exclude, 
        FindObjectsSortMode findObjectsSortMode = FindObjectsSortMode.None
    )
        where T_Object : Object
    {
        var components = Object.FindObjectsByType(ResolveIl2CppType(typeof(T_Object)), findObjectsInactive, findObjectsSortMode);

        var result = new Il2CppReferenceArray<T_Object?>(components.Length);

        for (var index = 0; index < components.Length; ++index)
        {
            result[index] = components[index].TryCast<T_Object>();
        }

        return result;
    }
}
