using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HearthStoneDT.UI.Decks
{
    public sealed class CardInfo
    {
        public int DbfId { get; set; }
        public string CardId { get; set; } = "";
        public string NameKo { get; set; } = "";
        public int Cost { get; set; }
        public string Rarity{ get; set; } = "COMMON";
    }

    public sealed class CardDb
    {
        private static readonly HttpClient _http = new HttpClient();

        private readonly string _path;
        private Dictionary<int, CardInfo>? _byDbfId;

        public CardDb()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HearthStoneDT");

            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "cards.koKR.collectible.json");
        }

        public async Task EnsureLoadedAsync()
        {
            if (_byDbfId != null)
                return;

            await EnsureFileAsync();

            using var fs = File.OpenRead(_path);
            using var doc = await JsonDocument.ParseAsync(fs);

            var map = new Dictionary<int, CardInfo>(capacity: 5000);

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // HearthstoneJSON 카드 필드: id, dbfId, name, cost ...
                if (!el.TryGetProperty("dbfId", out var dbfEl))
                    continue;

                int dbfId = dbfEl.GetInt32();

                string cardId = el.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? "") : "";
                string name = el.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "") : "";
                int cost = el.TryGetProperty("cost", out var costEl) ? costEl.GetInt32() : 0;
                string rarity = el.TryGetProperty("rarity", out var rarEl) ? (rarEl.GetString() ?? "COMMON") : "COMMON";


                if (string.IsNullOrWhiteSpace(cardId))
                    continue;

                map[dbfId] = new CardInfo
                {
                    DbfId = dbfId,
                    CardId = cardId,
                    NameKo = name,
                    Cost = cost,
                    Rarity = rarity
                };
            }

            _byDbfId = map;
        }

        public bool TryGet(int dbfId, out CardInfo info)
        {
            info = default!;
            return _byDbfId != null && _byDbfId.TryGetValue(dbfId, out info);
        }

        private async Task EnsureFileAsync()
        {
            // 7일 이내면 재사용
            if (File.Exists(_path))
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(_path);
                if (age < TimeSpan.FromDays(7))
                    return;
            }

            // collectible만 받자(용량 줄임)
            // HearthstoneJSON는 /v1/latest/{locale}/cards.collectible.json 제공
            var url = "https://api.hearthstonejson.com/v1/latest/koKR/cards.collectible.json";

            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(_path, bytes);
        }
    }
}
