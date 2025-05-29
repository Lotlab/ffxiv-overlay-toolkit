using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace OverlayTK
{
    public class FetchWorker
    {
        static JToken Error(string message)
        {
            return JObject.FromObject(new
            {
                Error = message
            });
        }

        public async Task<JToken> Fetch(JObject req)
        {
            if (!req.TryGetValue("resource", out var resource))
                return Error("Missing resource field");

            if (resource.Type != JTokenType.String)
                return Error("Invalid resource field type, must be string");

            var msg = new HttpRequestMessage();
            msg.RequestUri = new Uri(resource.ToString());

            if (req.TryGetValue("options", out var options))
            {
                var reqOptions = new RequestInit(options);
                reqOptions.ApplyToRequest(msg);
            }

            var httpClient = new HttpClient();
            var resp = await httpClient.SendAsync(msg);
            return await FromResponse(resp);
        }

        static async Task<JToken> FromResponse(HttpResponseMessage msg)
        {
            var headersObj = new JObject();
            foreach (var header in msg.Headers)
                headersObj[header.Key] = JToken.FromObject(header.Value);

            return JObject.FromObject(new { 
                headers = headersObj,
                ok = msg.IsSuccessStatusCode,
                status = msg.StatusCode,
                statusText = msg.StatusCode.ToString(),
                type = "basic",
                url = msg.RequestMessage.RequestUri.ToString(),
                body = await msg.Content.ReadAsStringAsync()
            });
        }
        class RequestInit
        {
            string body { get; } = "";

            (string, string)[] headers { get; } = Array.Empty<(string, string)>();

            string method { get; } = "GET";

            string referrer { get; } = "";

            public RequestInit()
            {
            }

            public RequestInit(JToken token)
            {
                body = token["body"]?.ToObject<string>();
                method = token["method"]?.ToObject<string>();
                if (string.IsNullOrEmpty(method))
                    method = "GET";

                referrer = token["referrer"]?.ToObject<string>();

                List<(string, string)> headers = new List<(string, string)>();

                JObject headersObj = token["headers"] as JObject;
                if (headersObj != null)
                    foreach (var item in headersObj)
                        headers.Add((item.Key, item.Value.ToString()));

                this.headers = headers.ToArray();
            }

            public void ApplyToRequest(HttpRequestMessage req)
            {
                if (body != null)
                    req.Content = new StringContent(body);
                else if (method != "HEAD" && method != "GET")
                    req.Content = new ByteArrayContent(Array.Empty<byte>());

                req.Method = new HttpMethod(method);
                if (referrer != null)
                    req.Headers.Referrer = new Uri(referrer);

                foreach (var (name, value) in headers)
                {
                    if (name == "Content-Type")
                    {
                        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(value);
                        continue;
                    }

                    req.Headers.Add(name, value);
                }
            }
        }
    }
}
