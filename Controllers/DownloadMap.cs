using Newtonsoft.Json;
using System.IO.Compression;

namespace RatingAPI.Controllers
{
    public class Downloader
    {
        public string maps_dir = "D:\\maps";

        public string Map(string hash)
        {
            string mapDir = Path.Combine(maps_dir, hash);

            if (Directory.Exists(mapDir))
            {
                return "";
            }

            string beatsaverUrl = $"https://beatsaver.com/api/maps/hash/{hash}";
            using var httpClient = new HttpClient();
            var response = httpClient.GetStringAsync(beatsaverUrl).Result;
            dynamic beatsaverData = JsonConvert.DeserializeObject(response);
            string downloadURL = string.Empty;

            foreach (var version in beatsaverData.versions)
            {
                if (version.hash.ToString().ToLower() == hash.ToLower())
                {
                    downloadURL = version.downloadURL;
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadURL))
            {
                throw new Exception("Map download URL not found.");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; BeatSaverDownloader/1.0)");
            var data = client.GetByteArrayAsync(downloadURL);

            using var zipStream = new MemoryStream(data.Result);
            using var zipArchive = new ZipArchive(zipStream);
            Directory.CreateDirectory(mapDir);
            zipArchive.ExtractToDirectory(mapDir);

            string[] extractedFiles = Directory.GetFiles(mapDir);
            foreach (string extractedFile in extractedFiles)
            {
                if (!extractedFile.EndsWith(".dat"))
                {
                    try
                    {
                        File.Delete(extractedFile);
                    }
                    catch
                    {
                        // Handle exceptions if required or continue
                    }
                }
            }

            return mapDir;
        }
    }
}
