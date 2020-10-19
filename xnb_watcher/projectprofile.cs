using System;

using Newtonsoft.Json;

namespace xnb_watcher
{
    class ProjectProfile
    {
        [JsonProperty("profilename")]
        public string profilename { get; set; }

        [JsonProperty("folders")]
        public ProfileFolder[] folders { get; set; }

        public ProjectProfile()
        {
        }
    }

    class ProfileFolder
    {
        [JsonProperty("operation")]
        public string operation { get; set; }

        [JsonProperty("path")]
        public string path { get; set; }

        public ProfileFolder()
        {
        }
    }
}
