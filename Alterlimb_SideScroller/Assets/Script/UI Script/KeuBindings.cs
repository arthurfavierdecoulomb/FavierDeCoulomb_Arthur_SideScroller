using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameAction { MoveLeft, MoveRight, Jump, Dash, Interact, Pause }

public static class KeyBindings
{
    const string KeyPrefix = "bindings.";

    static readonly Dictionary<GameAction, KeyCode> defaults = new Dictionary<GameAction, KeyCode>
    {
        { GameAction.MoveLeft, KeyCode.Q },
        { GameAction.MoveRight, KeyCode.D },
        { GameAction.Jump, KeyCode.Space },
        { GameAction.Dash, KeyCode.LeftShift },
        { GameAction.Interact, KeyCode.E },
        { GameAction.Pause, KeyCode.Escape }
    };

    static readonly Dictionary<GameAction, KeyCode> current = new Dictionary<GameAction, KeyCode>();

    static bool loaded;

    public static event Action OnBindingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        current.Clear();
        OnBindingsChanged = null;
    }

    public static KeyCode Get(GameAction action)
    {
        EnsureLoaded();
        return current[action];
    }

    public static bool GetDown(GameAction action)
    {
        return Input.GetKeyDown(Get(action));
    }

    public static bool GetHeld(GameAction action)
    {
        return Input.GetKey(Get(action));
    }

    public static bool GetUp(GameAction action)
    {
        return Input.GetKeyUp(Get(action));
    }

    public static void Set(GameAction action, KeyCode key)
    {
        EnsureLoaded();

        KeyCode previous = current[action];
        if (previous == key) return;

        List<GameAction> conflicts = new List<GameAction>();
        foreach (KeyValuePair<GameAction, KeyCode> pair in current)
        {
            if (pair.Key != action && pair.Value == key)
                conflicts.Add(pair.Key);
        }

        for (int i = 0; i < conflicts.Count; i++)
            Assign(conflicts[i], previous);

        Assign(action, key);

        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();

        foreach (KeyValuePair<GameAction, KeyCode> pair in defaults)
            Assign(pair.Key, pair.Value);

        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
    }

    public static string GetActionName(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveLeft: return "ALLER À GAUCHE";
            case GameAction.MoveRight: return "ALLER À DROITE";
            case GameAction.Jump: return "SAUTER";
            case GameAction.Dash: return "DASH";
            case GameAction.Interact: return "INTERAGIR";
            case GameAction.Pause: return "PAUSE";
        }
        return action.ToString().ToUpperInvariant();
    }

    public static string GetDisplayName(GameAction action)
    {
        return GetDisplayName(Get(action));
    }

    public static string GetDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Space: return "ESPACE";
            case KeyCode.LeftShift: return "MAJ G";
            case KeyCode.RightShift: return "MAJ D";
            case KeyCode.LeftControl: return "CTRL G";
            case KeyCode.RightControl: return "CTRL D";
            case KeyCode.LeftAlt: return "ALT G";
            case KeyCode.RightAlt: return "ALT D";
            case KeyCode.Return: return "ENTRÉE";
            case KeyCode.KeypadEnter: return "ENTRÉE PAV";
            case KeyCode.Escape: return "ÉCHAP";
            case KeyCode.Tab: return "TAB";
            case KeyCode.Backspace: return "RETOUR";
            case KeyCode.UpArrow: return "HAUT";
            case KeyCode.DownArrow: return "BAS";
            case KeyCode.LeftArrow: return "GAUCHE";
            case KeyCode.RightArrow: return "DROITE";
            case KeyCode.Mouse0: return "SOURIS G";
            case KeyCode.Mouse1: return "SOURIS D";
            case KeyCode.Mouse2: return "SOURIS M";
        }

        string name = key.ToString();
        if (name.StartsWith("Alpha")) return name.Substring(5);
        if (name.StartsWith("Keypad")) return "PAV " + name.Substring(6);
        return name.ToUpperInvariant();
    }

    static void Assign(GameAction action, KeyCode key)
    {
        current[action] = key;
        PlayerPrefs.SetInt(KeyPrefix + action, (int)key);
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        foreach (KeyValuePair<GameAction, KeyCode> pair in defaults)
        {
            int stored = PlayerPrefs.GetInt(KeyPrefix + pair.Key, (int)pair.Value);
            current[pair.Key] = (KeyCode)stored;
        }
    }
}