using System;

namespace SimpleGitStats
{
	internal class Ignored
	{
		public static string[] IgnoredExtensions = new string[]
		{
			".csproj",
			".svg",
			".json",
			"models.js",
			".xml",
			".xml",
			".testdata.js",
			"EtalonActivity.xaml",
			"EtalonActivityV3.xaml",
		};

		public static string[] IgnoredPaths = new string[]
		{
			"CadesSigner",
		};

		public static bool IsIgnored(string filename)
		{
			foreach (var ext in Ignored.IgnoredExtensions)
				if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
					return true;

			foreach (var path in Ignored.IgnoredPaths)
				if (filename.StartsWith(path, StringComparison.OrdinalIgnoreCase))
					return true;

			return false;
		}
	}
}