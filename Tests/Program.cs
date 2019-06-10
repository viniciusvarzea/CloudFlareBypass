using System;
using System.Net;
using System.Threading.Tasks;

namespace Tests
{
    internal class Program
    {
        private static async Task Main()
        {
            using (var client = new CloudFlareBypass.CfHttpClient("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:67.0) Gecko/20100101 Firefox/67.0", null))
            {
                var res = await client.GetHtmlPage("INSERT YOUR URL TO DOWNLOAD");

                Console.WriteLine(res.Key == HttpStatusCode.SeeOther ? "Fail :(" : "OK Bypassing... check response");
            }
        }
    }
}
