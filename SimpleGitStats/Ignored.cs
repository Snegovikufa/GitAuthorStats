using System;

namespace SimpleGitStats
{
	internal class Ignored
	{
		internal static string[] IgnoredExtensions =
		{
			".csproj",
			".svg",
			".json",
			"models.js",
			".xml",
			".xml",
			".testdata.js",
			"EtalonActivity.xaml",
			"EtalonActivityV3.xaml"
		};

		internal static string[] IgnoredPaths =
		{
			"CadesSigner"
		};

		public static bool IsIgnored(string filename)
		{
			foreach (string ext in IgnoredExtensions)
			{
				if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			foreach (string path in IgnoredPaths)
			{
				if (filename.StartsWith(path, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}
	}
}