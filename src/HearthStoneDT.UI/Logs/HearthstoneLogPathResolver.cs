using System;
using System.IO;
using System.Linq;
using System.Globalization;

namespace HearthStoneDT.UI.Logs
{
    public static class HearthstoneLogPathResolver
    {
        public static string[] GetRootCandidates()
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blizzard", "Hearthstone", "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Hearthstone", "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Hearthstone", "Logs"),
            };
        }

        public static (string? Root, string? LogDir) ResolveRootAndDirectory()
        {
            foreach (var root in GetRootCandidates().Where(Directory.Exists))
            {
                if (HasLogs(root))
                    return (root, root);

                var latest = Directory.EnumerateDirectories(root, "Hearthstone_*")
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .FirstOrDefault(d => HasLogs(d.FullName));

                if (latest != null)
                    return (root, latest.FullName);
            }

            return (null, null);
        }

        public static string? ResolveLogDirectory()
        {
            var (_, dir) = ResolveRootAndDirectory();
            return dir;
        }

        public static bool HasLogs(string dir)
        {
            return File.Exists(Path.Combine(dir, "Power.log"))
                && File.Exists(Path.Combine(dir, "LoadingScreen.log"));
        }

        public static string? TryGetLatestChildWithLogs(string root)
        {
            if (!Directory.Exists(root)) return null;
            if (HasLogs(root)) return root;

            // 폴더명이 Hearthstone_yyyy_MM_dd_HH_mm_ss 형태인 경우, 이름의 타임스탬프가 가장 신뢰할만하다.
            var candidates = Directory.EnumerateDirectories(root, "Hearthstone_*")
                .Select(p => new DirectoryInfo(p))
                .Select(di => new
                {
                    Dir = di,
                    Parsed = TryParseFolderTimestamp(di.Name)
                })
                .Where(x => HasLogs(x.Dir.FullName))
                .ToList();

            if (candidates.Count == 0)
                return null;

            var best = candidates
                .OrderByDescending(x => x.Parsed ?? DateTime.MinValue)
                .ThenByDescending(x => x.Dir.CreationTimeUtc)
                .First();

            return best.Dir.FullName;
        }

        private static DateTime? TryParseFolderTimestamp(string folderName)
        {
            // Hearthstone_2026_01_20_18_28_05
            const string prefix = "Hearthstone_";
            if (!folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            var s = folderName.Substring(prefix.Length);
            if (DateTime.TryParseExact(s, "yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var dt))
                return dt;

            return null;
        }
    }
}
