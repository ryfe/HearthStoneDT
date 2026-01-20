using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HearthStoneDT.UI.Logs
{
    /// <summary>
    /// HDT 스타일: 파일을 tail(오프셋 기반) 하면서 새로 추가된 "완성된 줄"만 반환.
    /// - FileShare.ReadWrite|Delete로 열어서 HS가 쓰는 중에도 읽는다.
    /// - 개행이 없는 마지막 줄은 다음 폴링으로 미룬다.
    /// - 파일 크기가 줄면(truncate/rotation) 오프셋을 0으로 리셋한다.
    /// </summary>
    public sealed class LogFileTailer
    {
        private readonly string _filePath;
        private long _offset;
        private long _lastSize = -1;

        // 마지막 미완성 라인 보관(UTF-8 가정)
        private readonly StringBuilder _carry = new();

        public LogFileTailer(string filePath)
        {
            _filePath = filePath;
        }

        public string FilePath => _filePath;

        public void ResetOffset()
        {
            _offset = 0;
            _lastSize = -1;
            _carry.Clear();
        }

        public IReadOnlyList<string> ReadNewLines(int maxLines = 5000)
        {
            var lines = new List<string>();

            if (!File.Exists(_filePath))
            {
                // 파일이 잠시 없을 수 있음
                ResetOffset();
                return lines;
            }

            var fi = new FileInfo(_filePath);

            // truncate/rotation 감지
            if (_lastSize >= 0 && fi.Length < _lastSize)
            {
                ResetOffset();
                fi = new FileInfo(_filePath);
            }

            if (_offset > fi.Length)
                ResetOffset();

            using var fs = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            fs.Seek(_offset, SeekOrigin.Begin);

            // 남은 부분 전체를 한 번에 읽는다(보통 텍스트 로그라 OK)
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: true);

            var chunk = sr.ReadToEnd();
            _offset = fs.Position;
            _lastSize = fi.Length;

            if (string.IsNullOrEmpty(chunk))
                return lines;

            _carry.Append(chunk);

            int produced = 0;
            while (produced < maxLines)
            {
                var s = _carry.ToString();
                var idx = s.IndexOf('\n');
                if (idx < 0)
                    break;

                // 한 줄 추출 (\r 제거)
                var raw = s.Substring(0, idx);
                if (raw.EndsWith("\r", StringComparison.Ordinal))
                    raw = raw.Substring(0, raw.Length - 1);

                // consume
                _carry.Clear();
                _carry.Append(s.Substring(idx + 1));

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // 정상 로그 헤더만(보통 D/W/E)
                    char c0 = raw[0];
                    if (raw.Length >= 2 && (c0 == 'D' || c0 == 'W' || c0 == 'E') && raw[1] == ' ')
                    {
                        lines.Add(raw);
                        produced++;
                    }
                }
            }

            // maxLines 초과 보호: carry가 너무 커지면 잘라낸다
            if (_carry.Length > 2_000_000)
            {
                // 마지막 200KB만 유지
                var tail = _carry.ToString();
                tail = tail.Substring(Math.Max(0, tail.Length - 200_000));
                _carry.Clear();
                _carry.Append(tail);
            }

            return lines;
        }
    }
}
