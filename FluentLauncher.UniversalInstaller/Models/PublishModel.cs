using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FluentLauncher.UniversalInstaller.Models;

//{
//  "commit": "a9519ac",
//  "build": 5,
//  "releaseTime": "2025-04-24T01:33:09",
//  "currentPreviewVersion": "2.3.5.5",
//  "previousStableVersion": "2.3.5.0",
//  "hashes": {
//    "updatePackage-x64.zip": "75abfb0555ec1dd1e45e63b800f8f41f",
//    "updatePackage-arm64.zip": "c5806840885e2351e872d9f9066a5550"
//  }
//}

internal class PublishModel
{
    [JsonPropertyName("commit")]
    public string Commit { get; set; }

    [JsonPropertyName("build")]
    public int Build { get; set; }

    [JsonPropertyName("releaseTime")]
    public DateTime ReleaseTime { get; set; }

    [JsonPropertyName("currentPreviewVersion")]
    public string CurrentPreviewVersion { get; set; }

    [JsonPropertyName("previousStableVersion")]
    public string PreviousPreviewVersion { get; set; }

    [JsonPropertyName("enableLoadExtensions")]
    public bool EnableLoadExtensions { get; set; }

    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; }
}
