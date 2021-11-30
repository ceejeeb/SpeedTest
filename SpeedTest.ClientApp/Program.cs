using System;

namespace SpeedTest.ClientApp
{
    class Program
    {
        private static SpeedTestClient client;

        static void Main()
        {
            Console.WriteLine("Getting speedtest.net settings and server list...");
            client = new SpeedTestClient();

            Console.WriteLine("Testing download speed...");
            var downloadSpeed = client.TestDownloadSpeed(4);
            PrintSpeed("Download", downloadSpeed);

            Console.WriteLine("Testing upload speed...");
            var uploadSpeed = client.TestUploadSpeed(2);
            PrintSpeed("Upload", uploadSpeed);        
        }

        

        private static void PrintSpeed(string type, double speed)
        {
            if (speed > 1024)
            {
                Console.WriteLine("{0} speed: {1} Mbps", type, Math.Round(speed / 1024, 2));
            }
            else
            {
                Console.WriteLine("{0} speed: {1} Kbps", type, Math.Round(speed, 2));
            }
        }
    }
}
