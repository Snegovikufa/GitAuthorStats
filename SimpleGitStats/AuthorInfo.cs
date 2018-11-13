using System.Collections.Generic;

namespace SimpleGitStats
{
	internal struct AuthorInfo
	{
		public string AuthorName { get; }
		public int Inserted { get; }
		public int Deleted { get; }
		public Dictionary<string, int> MostInserted { get; }
		public Dictionary<string, int> MostDeleted { get; }

		public AuthorInfo(string authorName,
			int inserted,
			int deleted,
			Dictionary<string, int> mostInserted,
			Dictionary<string, int> mostDeleted)
		{
			AuthorName = authorName;
			Inserted = inserted;
			Deleted = deleted;
			MostInserted = mostInserted;
			MostDeleted = mostDeleted;
		}

		public override string ToString()
		{
			return $"{AuthorName}\tINSERTED: {Inserted}\tDELETED: {Deleted}";
		}
	}
}