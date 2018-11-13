using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Serilog;
using Serilog.Core;

namespace SimpleGitStats
{
	/// <summary>
	///   Git parser.
	/// </summary>
	public sealed class GitNativeClient
	{
		private const int MaxCommandExecuteAttemptsCount = 10;

		private const int CommandExecuteAttemptsInterval = 100;

		private static string _gitExecutableLocation;

		private static readonly Logger Log = new LoggerConfiguration().CreateLogger();

		private readonly string _repositoryPath;

		private int _gitProcessId;


		/// <summary>
		///   Creates new instance of <seealso cref="GitNativeClient"/>.
		/// </summary>
		/// <param name="repositoryPath">Repository path.</param>
		public GitNativeClient(string repositoryPath)
		{
			this._repositoryPath = repositoryPath;
		}

		/// <summary>
		///   Creates new instance of <seealso cref="GitNativeClient"/>.
		/// </summary>
		public GitNativeClient()
		{
			this._repositoryPath = AppDomain.CurrentDomain.BaseDirectory;
		}

		/// <summary>
		///   Initialize GitClient.
		/// </summary>
		public static void Initialize()
		{
			_gitExecutableLocation = GetGitCmdFilePath();
		}

		/// <summary>
		///   Search for git.exe.
		/// </summary>
		/// <returns>Path to git executable.</returns>
		private static string GetGitCmdFilePath()
		{
			string x86FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe");
			if (File.Exists(x86FilePath))
			{
				return x86FilePath;
			}

			string x64FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe");
			if (File.Exists(x64FilePath))
			{
				return x64FilePath;
			}

			// Попробуем просто запустить команду git --version.
			Log.Error("Git not found in Program Files. Trying to find in PATH");

			ProcessStartInfo processStartInfo = new ProcessStartInfo
			{
				FileName = "git.exe",
				Arguments = "--version",
				CreateNoWindow = true,
				UseShellExecute = false
			};

			try
			{
				Process process = Process.Start(processStartInfo);
				process.WaitForExit();

				if (process.ExitCode == 0)
				{
					return "git.exe";
				}
			}
			catch
			{
				throw new GitNotFoundException();
			}

			throw new GitNotFoundException();
		}

		/// <summary>
		///   Executes Git command.
		/// </summary>
		/// <param name="args">Commandline arguments.</param>
		/// <returns>Result.</returns>
		public GitNativeOperationResult Execute(params string[] args)
		{
			return this.ExecuteInternal(args);
		}

		/// <summary>
		///   Executes Git command and throws on error.
		/// </summary>
		/// <param name="args">Commandline arguments.</param>
		public GitNativeOperationResult ExecuteAndThrowOnError(params string[] args)
		{
			GitNativeOperationResult result = this.ExecuteInternal(args);
			ThrowOnError(result);
			return result;
		}

		/// <summary>
		///   Throws if error occurred with git command.
		/// </summary>
		/// <param name="result">Git result.</param>
		private static void ThrowOnError(GitNativeOperationResult result)
		{
			if (!result.Success)
			{
				throw new GitException(result.StandardError);
			}
		}

		/// <summary>
		///   Executes git operation with arguments and standard input.
		/// </summary>
		/// <param name="args">Commandline arguments.</param>
		/// <param name="standardInput">Standard input for process.</param>
		/// <returns>Git result.</returns>
		private GitNativeOperationResult ExecuteInternal(string[] args, string standardInput = null)
		{
			int retryCount = 0;
			StringBuilder output = new StringBuilder();
			StringBuilder error = new StringBuilder();

			while (++retryCount <= MaxCommandExecuteAttemptsCount)
			{
				output.Clear();
				error.Clear();
				using (Process process = new Process())
				{
					process.StartInfo.FileName = _gitExecutableLocation;
					process.StartInfo.Arguments = string.Join(" ", args);
					process.StartInfo.WorkingDirectory = this._repositoryPath;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
					process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

					if (!string.IsNullOrEmpty(standardInput))
					{
						process.StartInfo.RedirectStandardInput = true;
					}

					process.OutputDataReceived += (sender, e) =>
					{
						if (e.Data != null)
						{
							output.AppendLine(e.Data);
						}
					};
					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data != null)
						{
							error.AppendLine(e.Data);

							if (e.Data.Contains("Logon failed"))
							{
								ProcessUtils.KillProcessAndChildren(this._gitProcessId);
							}
						}
					};

					process.Start();
					this._gitProcessId = process.Id;

					if (!string.IsNullOrEmpty(standardInput))
					{
						var bytes = Encoding.UTF8.GetBytes(standardInput);
						process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
						process.StandardInput.Close();
					}

					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					process.WaitForExit();

					string allErrors = error.ToString();

					if (process.ExitCode != 0)
					{
						Log.Error(error.ToString());
						if (IsIndexLocked(allErrors))
						{
							Thread.Sleep(CommandExecuteAttemptsInterval);
							continue;
						}

						return new GitNativeOperationResult
						{
							StandardOutput = output.ToString(),
							StandardError = allErrors,
							Success = false
						};
					}

					return new GitNativeOperationResult
					{
						StandardOutput = output.ToString(),
						StandardError = allErrors,
						Success = true
					};
				}
			}

			return new GitNativeOperationResult
			{
				StandardOutput = output.ToString(),
				StandardError = error.ToString(),
				Success = true
			};
		}

		/// <summary>
		///   Checks if Git index is locked.
		/// </summary>
		/// <param name="error">Git error.</param>
		/// <returns><c>True</c> if git index is locked.</returns>
		private static bool IsIndexLocked(string error)
		{
			return error.Contains("index.lock") && error.Contains("File exists");
		}

		/// <summary>
		///   Checks if file is in Git index.
		/// </summary>
		/// <param name="path">Path to file.</param>
		/// <returns><c>True</c> if git indexes file.</returns>
		private bool IsFileTracked(string path)
		{
			GitNativeOperationResult result = this.Execute("ls-files", path);
			return result.Success && result.StandardOutput.Length > 0;
		}
	}

	/// <summary>
	///   Результат выполнения операции Git.
	/// </summary>
	public class GitNativeOperationResult
	{
		/// <summary>
		///   Standard error.
		/// </summary>
		public string StandardError { get; set; }

		/// <summary>
		///   Standard output.
		/// </summary>
		public string StandardOutput { get; set; }

		/// <summary>
		///   Exit code 0 or not.
		/// </summary>
		public bool Success { get; set; }
	}


	/// <summary>
	///   Git was not found.
	/// </summary>
	[Serializable]
	public class GitNotFoundException : Exception
	{
		public GitNotFoundException() :
			base("Git not Found")
		{
		}
	}

	/// <summary>
	///   Git exception.
	/// </summary>
	[Serializable]
	public class GitException : Exception
	{
		public GitException(string message) : base(message)
		{
		}

		public GitException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}

	/// <summary>
	///   Helps to kill process.
	/// </summary>
	internal static class ProcessUtils
	{
		/// <summary>
		///   Kill process and childs.
		/// </summary>
		/// <param name="pid">Process PID.</param>
		/// <remarks>For .Net Core there is no standard way to get parent or child process.</remarks>
		internal static void KillProcessAndChildren(int pid)
		{
			if (pid == 0)
			{
				return;
			}

			Process proc = Process.GetProcessById(pid);
			proc.Kill();
		}
	}
}