using System.Collections.Generic;

namespace GFTextMerge.JsonObject
{
    public class RegexPreset
    {
        public string Search { get; set; }
        public List<int> PrimaryKey { get; set; } = new List<int>();
        public string Replace { get; set; }
        public string Empty { get; set; }
    }
}
