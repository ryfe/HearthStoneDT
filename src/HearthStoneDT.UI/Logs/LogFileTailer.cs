using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HearthStoneDT.UI.Logs
{
    public sealed class LogFileTailer
    {
        private readonly string _path;
        private long _offset;

        public LogFileTailer(string path)
        {
            _path = path;
        }

        public void StartFromEnd()
        {
            if (!File.Exists(_path)) { _offset = 0; return; }
            _offset = new FileInfo(_path).Length;
        }

        public void StartFromBeginning() => _offset = 0;

        public IEnumerable<(string line, bool reset)> ReadNewLines(bool detectTruncate)
        {
            if (!File.Exists(_path))
                yield break;

            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            if (detectTruncate && fs.Length < _offset)
            {
                _offset = 0;
                yield return ("", true);
            }

            fs.Seek(_offset, SeekOrigin.Begin);

            using var sr = new StreamReader(fs, Encoding.UTF8);

            string? line;
            while ((line = sr.ReadLine()) != null)
                yield return (line, false);

            _offset = fs.Position;
        }
    }
}
