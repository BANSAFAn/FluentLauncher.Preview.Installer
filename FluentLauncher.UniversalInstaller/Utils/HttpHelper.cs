using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace FluentLauncher.UniversalInstaller.Utils;

internal static class HttpHelper
{
    public static HttpClient _httpClient = new();

    public static async Task<FileInfo> Download(string url, string fileName, Action<double> onProgressChanged)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        using var responseMessage = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        responseMessage.EnsureSuccessStatusCode();

        FileInfo fileInfo = new (Path.Combine(Path.GetTempPath(), "FluentLauncher.UniversalInstaller", fileName));
        fileInfo.Directory.Create();
    
        using var fileStream = fileInfo.Create();
        using var contentStream = await responseMessage.Content.ReadAsStreamAsync();

        long read = 0;
        long total = responseMessage.Content.Headers.ContentLength ?? -1L;

        byte[] buffer = new byte[4096];
        int bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);

        while (bytesRead > 0)
        {
            read += bytesRead;
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);

            onProgressChanged?.Invoke((double)read / total * 100);
        }

        await fileStream.FlushAsync();

        return fileInfo;
    }
}
