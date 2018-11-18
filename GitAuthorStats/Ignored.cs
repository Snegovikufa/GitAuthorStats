﻿using System;

namespace GitAuthorStats
{
	internal class Ignored
	{
		internal static string[] IgnoredExtensions =
		{
			".csproj",
			".g.cs",
			".g.i.cs",
			".svg",
			".json",
			"models.js",
			".xml",
			".xml",
			".lref",
			".cache",
			".xshd",
			".xsd",
			".uml",
			".classdiagram",
			".testdata.js",
			"EtalonActivity.xaml",
			"EtalonActivityV3.xaml",
			".Designer.cs",
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