using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TestApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var t1 = doFetch();
            t1.Wait();
            var t2 = doHIDTest();
            t2.Wait();

            Console.ReadLine();
        }
    
        static async Task doFetch()
        {
            var worker = new OverlayTK.FetchWorker();
            var result = await worker.Fetch(new JObject() {
                { "resource", "https://example.org" },
                { "options", new JObject() {
                    { "headers", new JObject() {
                        { "User-Agent", "OverlayTK-TestApp/1.0" }
                    } }
                } }
            });
            Console.WriteLine(result.ToString());
        }

        static async Task doHIDTest()
        {
            var worker = new OverlayTK.HIDWorker();
            var devices = worker.GetDevices();
            Console.WriteLine(devices.ToString());
        }
    }
}
