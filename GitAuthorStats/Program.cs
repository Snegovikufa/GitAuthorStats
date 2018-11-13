using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Serilog;

namespace GitAuthorStats
{
	internal class Program
	{
		[Argument(0, Description = "Git repository location")]
		public string Location { get; set; }

		[Option(Description = "Since <date>. For example, 1 January, 2018")]
		public string Since { get; set; }

		[Option(Description = "Until <date>. For example, 31 December, 2018")]
		public string Until { get; set; }

		public static int Main(string[] args)
		{
			return CommandLineApplication.Execute<Program>(args);
		}

		// ReSharper disable once UnusedMember.Local
		private void OnExecute()
		{
			GitNativeClient.Initialize();

			GitNativeClient client = string.IsNullOrEmpty(this.Location) ? new GitNativeClient() : new GitNativeClient(this.Location);

			string commitPerAuthorArgs = "shortlog -sn --no-merges";
			if (!string.IsNullOrWhiteSpace(this.Since))
			{
				commitPerAuthorArgs += $" --since=\"{this.Since}\"";
			}

			if (!string.IsNullOrWhiteSpace(this.Until))
			{
				commitPerAuthorArgs += $" --until=\"{this.Until}\"";
			}

			GitNativeOperationResult commitsByAuthors = client.ExecuteAndThrowOnError(commitPerAuthorArgs);
			Console.WriteLine("# Commits per author");
			Console.WriteLine();
			Console.WriteLine(commitsByAuthors.StandardOutput);

			var authorsAndCommits = commitsByAuthors.StandardOutput.Split(Environment.NewLine);

			var authorInfos = ParseChangedByAuthors(authorsAndCommits, client);

			Console.WriteLine("# Changed lines per author");
			Console.WriteLine();
			foreach (AuthorInfo info in authorInfos.OrderByDescending(info => info.Inserted))
			{
				Console.WriteLine(info);
			}


			foreach (AuthorInfo info in authorInfos.OrderByDescending(info => info.Inserted))
			{
				Console.WriteLine($"Top 5 insertions by {info.AuthorName}:");

				var pairs = info.MostInserted.OrderByDescending(pair => pair.Value).Take(5).ToArray();
				foreach (var pair in pairs)
				{
					Console.WriteLine($"{pair.Key}\t{pair.Value}");
				}
			}

			foreach (AuthorInfo info in authorInfos.OrderByDescending(info => info.Deleted))
			{
				Console.WriteLine($"Top 5 deletions by {info.AuthorName}:");

				var pairs = info.MostDeleted.OrderByDescending(pair => pair.Value).Take(5).ToArray();
				foreach (var pair in pairs)
				{
					Console.WriteLine($"{pair.Key}\t{pair.Value}");
				}
			}

			Console.WriteLine();
		}

		private static List<AuthorInfo> ParseChangedByAuthors(string[] authorsAndCommits, GitNativeClient client)
		{
			var authorInfos = new List<AuthorInfo>();
			var mostInserted = new Dictionary<string, int>();
			var mostDeleted = new Dictionary<string, int>();

			foreach (string line in authorsAndCommits)
			{
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}

				string authorName = line.Split("\t")[1];
				GitNativeOperationResult byAuthor = client.ExecuteAndThrowOnError(
					$"log --ignore-all-space --no-merges --since=\"1 January, 2018\" --author=\"{authorName}\" --format= --numstat");

				int inserted = 0;
				int deleted = 0;

				if (byAuthor.StandardOutput.Length > 0)
				{
					Log.Logger.Debug($"PARSING STATS FOR {authorName}");

					foreach (string file in byAuthor.StandardOutput.Split(Environment.NewLine))
					{
						if (string.IsNullOrEmpty(file))
						{
							continue;
						}

						var chunks = file.Split("\t");
						int.TryParse(chunks[0], out int v1);
						int.TryParse(chunks[1], out int v2);
						string filename = chunks[2];
						Debug.Assert(chunks.Length == 3);

						if (Ignored.IsIgnored(filename))
						{
							continue;
						}

						inserted += v1;
						deleted += v2;

						if (v1 > 500)
						{
							Console.WriteLine($">>>> MAYBE YOU NEED TO IGNORE IN {nameof(Ignored)}.cs? {filename}");
						}

						mostInserted.AddValue(filename, inserted);
						mostDeleted.AddValue(filename, deleted);
					}

					authorInfos.Add(new AuthorInfo(authorName, inserted, deleted, mostInserted, mostDeleted));
				}
			}

			return authorInfos;
		}
	}
}