using FluentLauncher.CommandLineInstaller.Models;
using System.Text.Json.Serialization;

namespace FluentLauncher.CommandLineInstaller;

[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(AssetModel))]
[JsonSerializable(typeof(AssetModel[]))]
[JsonSerializable(typeof(ReleaseModel))]
[JsonSerializable(typeof(ReleaseModel[]))]
internal partial class SerializerContext : JsonSerializerContext { }
