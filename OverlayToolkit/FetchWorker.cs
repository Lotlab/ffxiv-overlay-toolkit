using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace OverlayTK
{
    public static class JsonHelper
    {
        public static JToken Error(string message)
        {
            return JObject.FromObject(new
            {
                Error = message
            });
        }
    }

    public class FetchWorker : IWorker
    {
        public string Name => "otk::fetch";

        public async Task<JToken> Fetch(JObject req)
        {
            var request = req.ToObject<FetchRequest>();
            if (string.IsNullOrEmpty(request.resource))
                return JsonHelper.Error("Missing resource field");

            var msg = new HttpRequestMessage();
            msg.RequestUri = new Uri(request.resource);

            if (request.options != null)
                request.options.Value.ApplyToRequest(msg);

            var httpClient = new HttpClient();
            var resp = await httpClient.SendAsync(msg);
            return await FromResponse(resp);
        }

        static async Task<JToken> FromResponse(HttpResponseMessage msg)
        {
            var headersObj = new JObject();
            foreach (var header in msg.Headers)
                headersObj[header.Key] = JToken.FromObject(header.Value);

            return JObject.FromObject(new FetchResponse(msg, await msg.Content.ReadAsStringAsync()));
        }

        public JToken Do(JObject token)
        {
            var req = Fetch(token);
            req.Wait(3000);

            if (req.Exception != null)
                return JObject.FromObject(req.Exception);

            return req.Result;
        }

        public void Init(IEventSource es)
        {
            es.RegisterEventHandler("Fetch", Do);
        }

        class FetchRequest
        {
            public string resource;
            public RequestInit? options;
        }

        struct RequestInit
        {
            public string body;

            public Dictionary<string, string> headers;

            public string method;

            public string referrer;

            public void ApplyToRequest(HttpRequestMessage req)
            {
                if (string.IsNullOrEmpty(method))
                    method = "GET";

                req.Method = new HttpMethod(method);

                if (body != null)
                    req.Content = new StringContent(body);
                else if (method != "HEAD" && method != "GET")
                    req.Content = new ByteArrayContent(Array.Empty<byte>());

                if (!string.IsNullOrEmpty(referrer))
                    req.Headers.Referrer = new Uri(referrer);

                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        if (item.Key == "Content-Type")
                        {
                            req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(item.Value);
                            continue;
                        }

                        req.Headers.Add(item.Key, item.Value);
                    }
                }
            }
        }
    
        class FetchResponse
        {
            public Dictionary<string, string> headers;
            public bool ok;
            public int status;
            public string statusText;
            public string type;
            public string url;
            public string body;

            public FetchResponse(HttpResponseMessage msg, string body)
            {
                headers = new Dictionary<string, string>();
                foreach (var item in msg.Headers)
                {
                    foreach (var value in item.Value)
                    {
                        headers.Add(item.Key, value);
                    }
                }

                ok = msg.IsSuccessStatusCode;
                status = (int)msg.StatusCode;
                statusText = msg.StatusCode.ToString();
                type = "basic";
                url = msg.RequestMessage.RequestUri.ToString();
                this.body = body;
            }
        }
    }
}
