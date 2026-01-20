using System;
using System.IO;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// WPF 앱이라 콘솔 출력이 없어서 파일로 디버그 로그를 남긴다.
    /// </summary>
    public static class DebugLog
    {
        private static readonly object _lock = new();
        private const string BaseDir = @"C:\HearthStoneDT";
        private const string FileName = "log.txt";

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                var path = Path.Combine(BaseDir, FileName);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
                lock (_lock)
                    File.AppendAllText(path, line);
            }
            catch
            {
                // 디버그 로그 실패로 앱이 죽으면 안 된다.
            }
        }
    }
}
