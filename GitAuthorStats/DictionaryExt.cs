using System.Collections.Generic;

namespace GitAuthorStats
{
	internal static class DictionaryExt
	{
		public static void AddValue(this IDictionary<string, int> mostInserted, string key, int inserted)
		{
			if (mostInserted.TryGetValue(key, out int v))
			{
				mostInserted[key] = v + inserted;
			}
			else
			{
				mostInserted[key] = inserted;
			}
		}
	}
}