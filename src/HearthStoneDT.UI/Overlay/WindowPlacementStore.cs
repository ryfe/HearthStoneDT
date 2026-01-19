using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace HearthStoneDT.UI.Overlay
{
    internal static class WindowPlacementStore
    {
        private sealed class Placement
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        private static string GetPath(string key)
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HearthStoneDT");

            Directory.CreateDirectory(dir);

            return Path.Combine(dir, $"{key}.json");
        }

        public static void Save(Window window, string key)
        {
            if (window.WindowState != WindowState.Normal)
                return;

            var p = new Placement
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height
            };

            var json = JsonSerializer.Serialize(p, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetPath(key), json);
        }

        public static bool TryLoad(Window window, string key)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
                return false;

            try
            {
                var json = File.ReadAllText(path);
                var p = JsonSerializer.Deserialize<Placement>(json);
                if (p == null) return false;

                // 화면 밖으로 나가는 것 방지
                var area = SystemParameters.WorkArea;

                var w = p.Width > 50 ? p.Width : window.Width;
                var h = p.Height > 50 ? p.Height : window.Height;

                var left = p.Left;
                var top = p.Top;

                // 최소/최대 클램프
                if (left < area.Left) left = area.Left;
                if (top < area.Top) top = area.Top;
                if (left + w > area.Right) left = Math.Max(area.Left, area.Right - w);
                if (top + h > area.Bottom) top = Math.Max(area.Top, area.Bottom - h);

                window.Width = w;
                window.Height = h;
                window.Left = left;
                window.Top = top;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
