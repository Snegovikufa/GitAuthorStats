using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GitAuthorStats
{
	interface IWriter: IDisposable
	{
		void WriteLine();
		void WriteLine(string line);
	}

	internal class ConsoleWriter : IWriter
	{
		public void WriteLine()
		{
			Console.WriteLine();
		}

		public void WriteLine(string line)
		{
			Console.WriteLine(line);
		}

		public void Dispose()
		{
		}
	}

	internal class FileWriter: IWriter, IDisposable
	{
		protected internal StreamWriter _stream;

		public FileWriter(string filename)
		{
			_stream = new StreamWriter(File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read));
		}

		public void WriteLine()
		{
			_stream.WriteLine();
		}

		public void WriteLine(string line)
		{
			_stream.WriteLine(line);
		}

		public void Dispose()
		{
			_stream?.Dispose();
		}
	}
}
