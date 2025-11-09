using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace Utilities;

public static class InputHelpers
{
    public static IDisposable WithCooldownCallback
    (
        this InputAction action, 
        Action callback, 
        Func<int> cooldownSeconds
    )
    {
        var last = double.NegativeInfinity;

        var wrappedCallback = new Action<Il2CppSystem.Object, InputActionChange>
        (
            (sender, change) =>
            {
                if (sender.TryCast<InputAction>() is not InputAction senderAction || senderAction.id != action.id || change != InputActionChange.ActionPerformed)
                    return;

                // Cooldown gate (uses InputSystem clock)
                var now = InputState.currentTime; // same clock ctx.time uses

                if (now - last < cooldownSeconds())
                    return;

                last = now;

                callback();
            }
        );

        InputSystem.add_onActionChange(wrappedCallback);

        return new Subscription(wrappedCallback);
    }

    private class Subscription(Action<Il2CppSystem.Object, InputActionChange> Action) : IDisposable
    {
        public void Dispose()
        {
            InputSystem.remove_onActionChange(Action);
        }
    }

    public static InputAction Rebind(this InputAction action, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Hotkey string is empty.");

        action.Disable();

        // Split by '+' for chords; require every token to be a valid control path
        var parameters = value.Split('+')
            .Select(parameter => parameter.Trim())
            .Where(parameter => parameter.Length > 0)
            .ToArray();

        foreach (var part in parameters)
            ValidateControlPathOrThrow(part);

        for (var index = 0; index < action.bindings.Count; ++index)
            action.ChangeBinding(index).Erase();
        
        switch (parameters)
        {
            case [string modifier1, string modifier2, string button]:
            {
                action.AddCompositeBinding("ButtonWithTwoModifiers")
                    .With("modifier1", modifier1)
                    .With("modifier2", modifier2)
                    .With("button", button);

                break;
            }
            case [string modifier, string button]:
            {
                action.AddCompositeBinding("ButtonWithOneModifier")
                    .With("modifier", modifier)
                    .With("button", button); ;

                break;
            }
            case [string singleBinding]:
            {
                action.AddBinding(singleBinding);

                break;
            }
            default:
                throw new ArgumentException($"Unsupported chord length ({parameters.Length}). Use 1, 2, or 3 control paths.");
        }

        action.Enable();

        return action;
    }

    public static InputAction ParseAction(string name, string value)
    {
        var action = new InputAction(name: name, type: InputActionType.Button);

        return action.Rebind(value);
    }

    public static IEnumerable<string> GetAvailablePaths()
    {
        foreach (var device in InputSystem.devices)
        {
            foreach (var control in device.allControls)
            {
                yield return control.path;
            }
        }
    }

    private static void ValidateControlPathOrThrow(string path)
    {
        if (InputSystem.FindControl(path) == null)
        {
            throw new ArgumentException($"Invalid control path: '{path}'\nAvailable paths\n{string.Join("\n", GetAvailablePaths())}.");
        }
    }
}
