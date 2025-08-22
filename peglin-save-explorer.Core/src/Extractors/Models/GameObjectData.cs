using System.Collections.Generic;

namespace peglin_save_explorer.Extractors.Models
{
    /// <summary>
    /// Represents GameObject data extracted from Unity assets
    /// </summary>
    public class GameObjectData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public long PathID { get; set; }
        public List<ComponentData> Components { get; set; } = new();
        public Dictionary<string, object> RawData { get; set; } = new();
    }
}
