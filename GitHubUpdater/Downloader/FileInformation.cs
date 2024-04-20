using System.Text.Json.Serialization;

namespace GitHubUpdater.Downloader
{
    public class FileInformation
    {
        public string Path { get; set; }

        public string Sha { get; set; }

        [JsonIgnore]
        public FileState FileState { get; set; } = FileState.SAME;

        public string Url { get; set; }

        [JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FileType FileType { get; set; }
    }
}
