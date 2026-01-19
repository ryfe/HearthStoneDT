using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HearthStoneDT.UI.Decks
{
    public sealed class DeckStore
    {
        private readonly string _path;

        public DeckStore()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HearthStoneDT");

            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "decks.json");
        }

        public List<DeckDefinition> LoadAll()
        {
            if (!File.Exists(_path))
                return new List<DeckDefinition>();

            try
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<List<DeckDefinition>>(json) ?? new List<DeckDefinition>();
            }
            catch
            {
                return new List<DeckDefinition>();
            }
        }

        public void SaveAll(List<DeckDefinition> decks)
        {
            var json = JsonSerializer.Serialize(decks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }

        public DeckDefinition AddOrReplace(DeckDefinition deck)
        {
            var decks = LoadAll();

            // 같은 이름이면 덮어쓰기
            decks = decks.Where(d => !string.Equals(d.Name, deck.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            decks.Add(deck);

            SaveAll(decks);
            return deck;
        }

        public bool DeleteByName(string name)
        {
            var decks = LoadAll();
            var before = decks.Count;

            decks = decks.Where(d => !string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

            if (decks.Count == before)
                return false;

            SaveAll(decks);
            return true;
        }
    }
}
