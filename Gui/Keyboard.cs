﻿using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public static class Keyboard
	{
		private static Dictionary<Keys, bool> downStates = new Dictionary<Keys, bool>();

		public static bool IsKeyDown(Keys key)
		{
			if (downStates.ContainsKey(key))
			{
				return downStates[key];
			}

			return false;
		}

		public static void SetKeyDownState(Keys key, bool down)
		{
			if (downStates.ContainsKey(key))
			{
				downStates[key] = down;
			}
			else
			{
				downStates.Add(key, down);
			}
		}
	}
}