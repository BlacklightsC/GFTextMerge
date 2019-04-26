using System.Collections.Generic;

using Newtonsoft.Json;

namespace GFTextMerge.JsonObject
{
    public class Settings
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        [JsonProperty(PropertyName = "Record mismatch contents")]
        public bool MismatchLog { get; set; }
        [JsonProperty(PropertyName = "Remove dummy-data as possible")]
        public bool RemoveDummy { get; set; }
        [JsonProperty(PropertyName = "Override usage")]
        public bool UseOverride { get; set; }
        public List<Content> Contents { get; set; } = new List<Content>();
        public List<LocaleContent> Locales { get; set; } = new List<LocaleContent>();
    }
}
