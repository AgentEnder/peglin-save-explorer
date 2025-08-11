using System.Collections.Generic;

namespace peglin_save_explorer.Extractors.Models
{
    /// <summary>
    /// Represents component data extracted from Unity GameObjects
    /// </summary>
    public class ComponentData
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public long PathID { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
