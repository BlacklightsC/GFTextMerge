using System.Collections.Generic;

namespace GFTextMerge.JsonObject
{
    public class Content
    {
        public string Name { get; set; }
        public bool Delete { get; set; }
        public List<string> Files { get; set; } = new List<string>();
        public RegexPreset Regex { get; set; } = new RegexPreset();
    }
}
