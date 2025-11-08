using BepInEx.Configuration;
using UnityEngine;

namespace UnlimitedRestockers;

public class Setting
{
	public KeyCode KEY_HIRE;

	public bool KEY_HIRE_SHIFT = false;

	public bool KEY_HIRE_CTRL = false;

	public bool KEY_HIRE_ALT = false;

	public KeyCode KEY_FIRE;

	public bool KEY_FIRE_SHIFT = false;

	public bool KEY_FIRE_CTRL = false;

	public bool KEY_FIRE_ALT = false;

	public ConfigEntry<int> COST_HIRE;

	public ConfigEntry<int> RESTOCKER_ID;

	public ConfigEntry<int> COOLDOWN_HIRE;

	public Setting(ConfigFile configuration)
	{
		var addRestockersBinding = configuration.Bind("UnlimitedRestockers", "add_restockers_key", "H", "Use this key to hire additional restockers.");
		var removeRestockersBinding = configuration.Bind("UnlimitedRestockers", "remove_restockers_key", "F", "Use this key to fire additional restockers.");
		KEY_HIRE = GetKeyCode(0, addRestockersBinding.Value, KeyCode.H);
		KEY_FIRE = GetKeyCode(1, removeRestockersBinding.Value, KeyCode.F);
		COST_HIRE = configuration.Bind("UnlimitedRestockers", "hire_cost", 150, "Cost (in dollars) to hire a cloned restocker.");
		COOLDOWN_HIRE = configuration.Bind("UnlimitedRestockers", "hire_cooldown", 1, "Cool-down time (seconds) for preventing rapid inputs.");
	}

	private KeyCode GetKeyCode(int type, string str, KeyCode defaultKey)
	{
		string text = str.ToUpperInvariant();
		if (text.Contains("SHIFT+"))
		{
			switch (type)
			{
			case 0:
				KEY_HIRE_SHIFT = true;
				break;
			case 1:
				KEY_FIRE_SHIFT = true;
				break;
			}
			text = text.Replace("SHIFT+", "");
		}
		if (text.Contains("CTRL+"))
		{
			switch (type)
			{
			case 0:
				KEY_HIRE_CTRL = true;
				break;
			case 1:
				KEY_FIRE_CTRL = true;
				break;
			}
			text = text.Replace("CTRL+", "");
		}
		if (text.Contains("ALT+"))
		{
			switch (type)
			{
			case 0:
				KEY_HIRE_ALT = true;
				break;
			case 1:
				KEY_FIRE_ALT = true;
				break;
			}
			text = text.Replace("ALT+", "");
		}
		return (KeyCode)(text switch
		{
			"A" => 97, 
			"B" => 98, 
			"C" => 99, 
			"D" => 100, 
			"E" => 101, 
			"F" => 102, 
			"G" => 103, 
			"H" => 104, 
			"I" => 105, 
			"J" => 106, 
			"K" => 107, 
			"L" => 108, 
			"M" => 109, 
			"N" => 110, 
			"O" => 111, 
			"P" => 112, 
			"Q" => 113, 
			"R" => 114, 
			"S" => 115, 
			"T" => 116, 
			"U" => 117, 
			"V" => 118, 
			"W" => 119, 
			"X" => 120, 
			"Y" => 121, 
			"Z" => 122, 
			"0" => 48, 
			"1" => 49, 
			"2" => 50, 
			"3" => 51, 
			"4" => 52, 
			"5" => 53, 
			"6" => 54, 
			"7" => 55, 
			"8" => 56, 
			"9" => 57, 
			"RETURN" => 13, 
			"TAB" => 9, 
			"INSERT" => 277, 
			"DELETE" => 127, 
			"PAGEUP" => 280, 
			"PAGEDOWN" => 281, 
			"HOME" => 278, 
			"END" => 279, 
			"NUMLOCK" => 300, 
			_ => (int)defaultKey, 
		});
	}
}
