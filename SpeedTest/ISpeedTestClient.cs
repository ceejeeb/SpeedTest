using SpeedTest.Models;

namespace SpeedTest
{
    public interface ISpeedTestClient
    {

        /// <summary>
        /// Test latency (ping) to server
        /// </summary>
        /// <returns>Latency in milliseconds (ms)</returns>
        int TestServerLatency(Server server, int retryCount = 3);

        /// <summary>
        /// Test download speed to server
        /// </summary>
        /// <returns>Download speed in Kbps</returns>
        double TestDownloadSpeed(int retryCount = 2, Server server = null);

        /// <summary>
        /// Test upload speed to server
        /// </summary>
        /// <returns>Upload speed in Kbps</returns>
        double TestUploadSpeed(int retryCount = 2, Server server = null);
    }
}