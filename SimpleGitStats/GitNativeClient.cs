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
  /// Обработчик команд Git.
  /// </summary>
  public sealed class GitNativeClient
  {
    #region Константы

    /// <summary>
    /// Максимальное количество повторений вызова команды Git.
    /// </summary>
    private const int MaxCommandExecuteAttemptsCount = 10;

    /// <summary>
    /// Задержка перед следующим вызовом команды Git.
    /// </summary>
    private const int CommandExecuteAttemptsInterval = 100;

    #endregion

    #region Поля и свойства

    /// <summary>
    /// Рабочий каталог, в котором будет исполняться git.exe.
    /// </summary>
    private readonly string repositoryPath;

    /// <summary>
    /// Путь к файлу git.exe.
    /// </summary>
    private static string gitExecutableLocation;

    /// <summary>
    /// Ид процесса git.
    /// </summary>
    private int gitProcessId;

    /// <summary>
    /// Логгер.
    /// </summary>
    private static readonly Logger log = new LoggerConfiguration().CreateLogger();

    #endregion

    #region Методы

    /// <summary>
    /// Инициализировать GitClient.
    /// </summary>
    public static void Initialize()
    {
      gitExecutableLocation = GetGitCmdFilePath();
    }

    /// <summary>
    /// Произвести поиск git.exe.
    /// </summary>
    /// <returns>Путь к исполняемому файлу git.</returns>
    private static string GetGitCmdFilePath()
    {
      var x86FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe");
      if (File.Exists(x86FilePath))
      {
        return x86FilePath;
      }

      var x64FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe");
      if (File.Exists(x64FilePath))
      {
        return x64FilePath;
      }

      // Попробуем просто запустить команду git --version.
      log.Error("Git not found in Program Files. Trying to find in PATH");

      var processStartInfo = new ProcessStartInfo
      {
        FileName = "git.exe",
        Arguments = "--version",
        CreateNoWindow = true,
        UseShellExecute = false
      };

      try
      {
        var process = Process.Start(processStartInfo);
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
    /// Выполнить команду Git.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Результат выполнения.</returns>
    public GitNativeOperationResult Execute(params string[] args)
    {
      return this.ExecuteInternal(args);
    }

    /// <summary>
    /// Выполнить команду Git и выбросить исключение при возникновении ошибки.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    public GitNativeOperationResult ExecuteAndThrowOnError(params string[] args)
    {
      var result = this.ExecuteInternal(args);
      ThrowOnError(result);
      return result;
    }

    /// <summary>
    /// Выбросить исключение при возникновении ошибки Git.
    /// </summary>
    /// <param name="result">Результат выполнения операции.</param>
    private static void ThrowOnError(GitNativeOperationResult result)
    {
      if (!result.Success)
      {
        throw new GitException(result.StandardError);
      }
    }

    /// <summary>
    /// Выполнить операцию git с указанными аргументами и потоком входных данных.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <param name="standardInput">Входной поток для процесса.</param>
    /// <returns>Результат выполнения.</returns>
    private GitNativeOperationResult ExecuteInternal(string[] args, string standardInput = null)
    {
      int retryCount = 0;
      var output = new StringBuilder();
      var error = new StringBuilder();

      while (++retryCount <= MaxCommandExecuteAttemptsCount)
      {
        output.Clear();
        error.Clear();
        using (var process = new Process())
        {
          process.StartInfo.FileName = gitExecutableLocation;
          process.StartInfo.Arguments = string.Join(" ", args);
          process.StartInfo.WorkingDirectory = this.repositoryPath;
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
                ProcessUtils.KillProcessAndChildren(this.gitProcessId);
              }
            }
          };

          process.Start();
          this.gitProcessId = process.Id;

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
            log.Error(error.ToString());
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
    /// Проверить ошибку на блокировку индекса.
    /// </summary>
    /// <param name="error">Текст ошибки.</param>
    /// <returns><c>True</c>, если произошла ошибка git index lock.</returns>
    private static bool IsIndexLocked(string error)
    {
      return error.Contains("index.lock") && error.Contains("File exists");
    }

    /// <summary>
    /// Проверить, находится ли файл в индексе Git.
    /// </summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns><c>True</c>, если индексируется.</returns>
    private bool IsFileTracked(string path)
    {
      var result = this.Execute("ls-files", path);
      return result.Success && (result.StandardOutput.Length > 0);
    }

    #endregion

    #region Конструкторы

    /// <summary>
    /// Конструктор.
    /// </summary>
    /// <param name="repositoryPath">Путь к репозиторию.</param>
    public GitNativeClient(string repositoryPath)
    {
      this.repositoryPath = repositoryPath;
    }

    /// <summary>
    /// Конструктор.
    /// </summary>
    public GitNativeClient()
    {
      this.repositoryPath = AppDomain.CurrentDomain.BaseDirectory;
    }

    #endregion


  }

  /// <summary>
  /// Результат выполнения операции Git.
  /// </summary>
  public class GitNativeOperationResult
  {
    #region Поля и свойства

    /// <summary>
    /// Вывод операции в standard error.
    /// </summary>
    public string StandardError { get; set; }

    /// <summary>
    /// Вывод операции в standard output.
    /// </summary>
    public string StandardOutput { get; set; }

    /// <summary>
    /// Признак успешности выполнения (exit code 0).
    /// </summary>
    public bool Success { get; set; }

    #endregion
  }



  /// <summary>
  /// Исключение ненахождения установленного в системе Git.
  /// </summary>
  [Serializable]
  public class GitNotFoundException : Exception
  {
    /// <summary>
    /// Конструктор.
    /// </summary>
    public GitNotFoundException() :
      base("Git not Found")
    {
    }
  }

  /// <summary>
  /// Исключение, произошедшее при операции с гитом.
  /// </summary>
  [Serializable]
  public class GitException : Exception
  {
    #region Конструкторы

    /// <summary>
    /// Конструктор.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    public GitException(string message) : base(message)
    {
    }

    /// <summary>
    /// Конструктор.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public GitException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    #endregion
  }

  /// <summary>
  /// Класс взаимодействия с процессами.
  /// </summary>
  internal static class ProcessUtils
  {
    #region Методы

    /// <summary>
    /// Завершить процесс принудительно.
    /// </summary>
    /// <param name="pid">Идентификатор процесса.</param>
    internal static void KillProcessAndChildren(int pid)
    {
      if (pid == 0)
      {
        return;
      }

      Process proc = Process.GetProcessById(pid);
      proc.Kill();
    }

    #endregion
  }

}