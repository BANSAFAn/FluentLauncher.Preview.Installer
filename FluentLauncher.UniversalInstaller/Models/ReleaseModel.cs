using System.Text.Json.Serialization;

namespace FluentLauncher.UniversalInstaller.Models;

public class AssetModel
{
    [JsonPropertyName("name")]
    public string Name { set; get; }

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { set; get; }
}

public class ReleaseModel
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; }

    [JsonPropertyName("published_at")]
    public string PublishedAt { set; get; }

    [JsonPropertyName("prerelease")]
    public bool IsPreRelease { get; set; }

    [JsonPropertyName("body")]
    public string Body { set; get; }

    [JsonPropertyName("assets")]
    public AssetModel[] Assets { get; set; }
}
