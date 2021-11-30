using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpeedTest.Models;

namespace SpeedTest
{
    public class SpeedTestClient : ISpeedTestClient
    {
        private const string ConfigUrl = "http://www.speedtest.net/speedtest-config.php";
        private const string ServersUrl = "http://c.speedtest.net/speedtest-servers-static.php";
        private readonly int[] downloadSizes = { 350, 750, 1500, 4000 };
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const int MaxUploadSize = 4;
        private Settings _settings;
        private IEnumerable<Server> _orderedServers;

        public SpeedTestClient()
        {
            LoadSettings();
            LoadServers();
        }

        #region ISpeedTestClient

        /// <summary>
        /// Download speedtest.net settings
        /// </summary>
        /// <returns>speedtest.net settings</returns>
        private void LoadSettings()
        {
            using (var client = new SpeedTestWebClient())
            {
                var settings = client.GetConfig<Settings>(ConfigUrl);
                var serversConfig = client.GetConfig<ServersList>(ServersUrl);

                serversConfig.CalculateDistances(settings.Client.GeoCoordinate);
                settings.Servers = serversConfig.Servers.OrderBy(s => s.Distance).ToList();

                _settings = settings;
            }

        }

        private void LoadServers()
        {
            var servers = _settings.Servers.Where(s => s.Country.Equals(_settings.DefaultCountry)).Take(10).ToList();

            foreach (var server in servers)
            {
                server.Latency = TestServerLatency(server);
            }
            _orderedServers = servers.OrderBy(x => x.Latency);
        }

        /// <summary>
        /// Test latency (ping) to server
        /// </summary>
        /// <returns>Latency in milliseconds (ms)</returns>
        public int TestServerLatency(Server server, int retryCount = 3)
        {
            var latencyUri = CreateTestUrl(server, "latency.txt");
            var timer = new Stopwatch();

            using (var client = new SpeedTestWebClient())
            {
                for (var i = 0; i < retryCount; i++)
                {
                    string testString;
                    try
                    {
                        timer.Start();
                        testString = client.DownloadString(latencyUri);
                    }
                    catch (WebException)
                    {
                        continue;
                    }
                    finally
                    {
                        timer.Stop();    
                    }

                    if (!testString.StartsWith("test=test"))
                    {
                        throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
                    }
                }
            }

            return (int)timer.ElapsedMilliseconds / retryCount;
        }

        /// <summary>
        /// Test download speed to server
        /// </summary>
        /// <returns>Download speed in Kbps</returns>
        public double TestDownloadSpeed(int retryCount = 2, Server server = null)
        {
            server = server ?? _orderedServers.First();
            var testData = GenerateDownloadUrls(server, retryCount);

            return TestSpeed(testData, async (client, url) =>
            {
                var data = await client.DownloadDataTaskAsync(url).ConfigureAwait(false);
                return data.Length;
            }, _settings.Download.ThreadsPerUrl);
        }

        /// <summary>
        /// Test upload speed to server
        /// </summary>
        /// <returns>Upload speed in Kbps</returns>
        public double TestUploadSpeed(int retryCount = 2, Server server = null)
        {
            server = server ?? _orderedServers.First();
            var testData = GenerateUploadData(retryCount);
            return TestSpeed(testData, async (client, uploadData) =>
            {
                await client.UploadValuesTaskAsync(server.Url, uploadData).ConfigureAwait(false);
                return uploadData[0].Length;
            }, _settings.Upload.ThreadsPerUrl);
        }

        #endregion

        #region Helpers

        private static double TestSpeed<T>(IEnumerable<T> testData, Func<WebClient, T, Task<int>> doWork, int concurencyCount = 2)
        {
            var timer = new Stopwatch();
            var throttler = new SemaphoreSlim(concurencyCount);

            timer.Start();
            var downloadTasks = testData.Select(async data =>
            {
                await throttler.WaitAsync().ConfigureAwait(false);
                var client = new SpeedTestWebClient();
                try
                {
                    var size = await doWork(client, data).ConfigureAwait(false);
                    return size;
                }
                finally
                {
                    client.Dispose();
                    throttler.Release();
                }
            }).ToArray();

            Task.WaitAll(downloadTasks);
            timer.Stop();

            double totalSize = downloadTasks.Sum(task => task.Result);
            return (totalSize * 8 / 1024) / ((double)timer.ElapsedMilliseconds / 1000);
        }

        private IEnumerable<NameValueCollection> GenerateUploadData(int retryCount)
        {
            var random = new Random();
            var result = new List<NameValueCollection>();

            for (var sizeCounter = 1; sizeCounter <= MaxUploadSize; sizeCounter++)
            {
                var size = sizeCounter*1024*1024;
                var builder = new StringBuilder(size);

                for (var i = 0; i < size; ++i)
                    builder.Append(Chars[random.Next(Chars.Length)]);

                for (var i = 0; i < retryCount; i++)
                {
                    result.Add(new NameValueCollection { { string.Format("content{0}", sizeCounter), builder.ToString() } });
                }
            }

            return result;
        }

        private static string CreateTestUrl(Server server, string file)
        {
            return new Uri(new Uri(server.Url), ".").OriginalString + file;
        }

        private IEnumerable<string> GenerateDownloadUrls(Server server, int retryCount)
        {
            var downloadUriBase = CreateTestUrl(server, "random{0}x{0}.jpg?r={1}");
            foreach (var downloadSize in downloadSizes)
            {
                for (var i = 0; i < retryCount; i++)
                {
                    yield return string.Format(downloadUriBase, downloadSize, i);
                }
            }
        }

        #endregion
    }
}
