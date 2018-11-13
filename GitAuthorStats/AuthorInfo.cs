using System.Collections.Generic;

namespace GitAuthorStats
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
			this.AuthorName = authorName;
			this.Inserted = inserted;
			this.Deleted = deleted;
			this.MostInserted = mostInserted;
			this.MostDeleted = mostDeleted;
		}

		public override string ToString()
		{
			return $"{this.AuthorName}\tINSERTED: {this.Inserted}\tDELETED: {this.Deleted}";
		}
	}
}