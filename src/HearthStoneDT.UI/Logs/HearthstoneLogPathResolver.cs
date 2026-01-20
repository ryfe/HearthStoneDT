using System;
using System.IO;
using System.Linq;

namespace HearthStoneDT.UI.Logs
{
    public static class HearthstoneLogPathResolver
    {
        public static string? ResolveLogDirectory()
        {
            // 1) 흔한 후보들
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Blizzard", "Hearthstone", "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Hearthstone", "Logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Hearthstone", "Logs"),
            };

            foreach (var root in roots.Where(Directory.Exists))
            {
                // A) 루트에 바로 Power.log가 있는 경우
                if (HasLogs(root))
                    return root;

                // B) 루트 아래 Hearthstone_YYYY... 하위 폴더들 중 최신 선택
                var latest = Directory.EnumerateDirectories(root, "Hearthstone_*")
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(d => d.CreationTimeUtc)
                    .FirstOrDefault(d => HasLogs(d.FullName));

                if (latest != null)
                    return latest.FullName;
            }

            return null;
        }

        private static bool HasLogs(string dir)
        {
            return File.Exists(Path.Combine(dir, "Power.log"))
                && File.Exists(Path.Combine(dir, "LoadingScreen.log"));
        }
    }
}
