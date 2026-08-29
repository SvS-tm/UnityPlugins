using System.Collections.Concurrent;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Utilities;

public static class Il2CppUnityExtensions
{
    // Cache Il2Cpp types – avoids repeated lookups/locks
    private static readonly ConcurrentDictionary<Type, Il2CppSystem.Type> cache = new();

    private static Il2CppSystem.Type ResolveIl2CppType(Type type)
    {
        var resolve = new Func<Type, Il2CppSystem.Type>
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

    public static T_Component Il2CppAddComponent<T_Component>(this UnityEngine.GameObject host)
        where T_Component : UnityEngine.Component
    {
        var component = host.AddComponent(ResolveIl2CppType(typeof(T_Component)));
        
        if (component?.TryCast<T_Component>() is not T_Component result)
            throw new InvalidCastException($"Couldn't cast component to {typeof(T_Component).FullName}");

        return result;
    }

    public static T_Component Il2CppGetOrAddComponent<T_Component>(this UnityEngine.GameObject host)
        where T_Component : UnityEngine.Component
    {
        var type = ResolveIl2CppType(typeof(T_Component));

        var component = host.GetComponent(type);

        if (component?.TryCast<T_Component>() is not T_Component resultForGet)
        { 
            component = host.AddComponent(type);

            if (component?.TryCast<T_Component>() is not T_Component resultForAdd)
                throw new InvalidCastException($"Couldn't cast component to {typeof(T_Component).FullName}");

            return resultForAdd;
        }

        return resultForGet;
    }

    public static T_Component? Il2CppGetComponent<T_Component>(this UnityEngine.GameObject host)
        where T_Component : UnityEngine.Component
    {
        var component = host.GetComponent(ResolveIl2CppType(typeof(T_Component)));

        return component?.TryCast<T_Component>();
    }

    public static T_Component? Il2CppGetComponent<T_Component>(this UnityEngine.Component host)
    where T_Component : UnityEngine.Component
    {
        return host.GetComponent(ResolveIl2CppType(typeof(T_Component)))?.TryCast<T_Component>();
    }

    public static Il2CppArrayBase<T_Component?> IL2CppGetComponentsInChildren<T_Component>(this UnityEngine.Transform transform, bool includeInactive)
        where T_Component : UnityEngine.Component
    {
        var components = transform.GetComponentsInChildren(ResolveIl2CppType(typeof(T_Component)), includeInactive);

        var result = new Il2CppReferenceArray<T_Component?>(components.Count);

        for (var index = 0; index < components.Count; ++index)
        {
            result[index] = components[index].TryCast<T_Component>();
        }

        return result;
    }

    public static Il2CppArrayBase<T_Object?> Il2CppFindObjectsByType<T_Object>
    (
        this UnityEngine.Object target, 
        UnityEngine.FindObjectsInactive findObjectsInactive = UnityEngine.FindObjectsInactive.Exclude, 
        UnityEngine.FindObjectsSortMode findObjectsSortMode = UnityEngine.FindObjectsSortMode.None
    )
        where T_Object : UnityEngine.Object
    {
        var components = UnityEngine.Object.FindObjectsByType(ResolveIl2CppType(typeof(T_Object)), findObjectsInactive, findObjectsSortMode);

        var result = new Il2CppReferenceArray<T_Object?>(components.Count);

        for (var index = 0; index < components.Count; ++index)
        {
            result[index] = components[index].TryCast<T_Object>();
        }

        return result;
    }

    public static T_Object? Il2CppFindFirstObjectByType<T_Object>
    (
        UnityEngine.FindObjectsInactive findObjectsInactive = UnityEngine.FindObjectsInactive.Exclude
    )
        where T_Object : UnityEngine.Object
    {
        var objects = UnityEngine.Object.FindObjectsByType
        (
            ResolveIl2CppType(typeof(T_Object)),
            findObjectsInactive,
            UnityEngine.FindObjectsSortMode.None
        );

        return objects.Count > 0 ? objects[0].TryCast<T_Object>() : null;
    }
}
