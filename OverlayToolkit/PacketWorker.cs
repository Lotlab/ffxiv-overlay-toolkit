using Lotlab.PluginCommon.FFXIV;
using Lotlab.PluginCommon.FFXIV.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace OverlayTK
{
    public class PacketWorker : IWorker
    {
        public string Name => "otk::packet";

        NetworkParser Parser { get; } = new NetworkParser();

        public PacketWorker(ACTPluginProxy ffxiv)
        {
            ffxiv.DataSubscription.NetworkReceived += onNetworkReceived;
            ffxiv.DataSubscription.NetworkSent += onNetworkSent;
        }

        IEventSource EventSource { get; set; }

        Dictionary<string, EventListener> Listeners { get; } = new Dictionary<string, EventListener>();

        private void onNetworkSent(string connection, long epoch, byte[] message)
        {
            handlePacket(true, connection, epoch, message);
        }

        private void onNetworkReceived(string connection, long epoch, byte[] message)
        {
            handlePacket(false, connection, epoch, message);
        }

        void handlePacket(bool isSent, string connection, long epoch, byte[] message)
        {
            if (message.Length < 32)
                return;

            var header = Parser.ParseIPCHeader(message);
            if (header == null)
                return;

            var segment = header.Value.segmentHeader;
            bool isSourceEqualTarget = segment.source_actor == segment.target_actor;

            foreach (var item in Listeners)
            {
                bool matched = item.Value.Filters.Length == 0;

                foreach (var filter in item.Value.Filters)
                {
                    if (filter.Match(isSent, message.Length, header.Value.type, isSourceEqualTarget))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                    dispatchPacket(item.Value, isSent, connection, epoch, message, header.Value);
            }
        }

        void dispatchPacket(EventListener listener, bool isSent, string connection, long epoch, byte[] message, IPCHeader header)
        {
            if (EventSource == null)
                return;

            // Old Behavior: using global type
            var eventType = "otk::packet";

            // New Behavior: using custom event name if provided
            if (!string.IsNullOrEmpty(listener.EventName))
            {
                eventType = listener.EventName;
            }

            EventSource.DispatchEvent(JObject.FromObject(new
            {
                type = eventType,
                name = listener.Name,
                dir = isSent,
                conn = connection,
                epoch = epoch,
                length = message.Length,
                opcode = header.type,
                msg = Convert.ToBase64String(message)
            }));
        }

        public JToken Subscribe(JObject req)
        {
            var listener = req.ToObject<EventListener>();
            if (string.IsNullOrEmpty(listener.Name))
                return JsonHelper.Error("missing 'name' field");

            Listeners[listener.Name] = listener;

            // Register event type if provided
            if (!string.IsNullOrEmpty(listener.EventName))
                EventSource.RegisterEventType(listener.EventName);

            return new JObject()
            {
                { "name", listener.Name },
                { "evt_name", listener.EventName }
            };
        }

        public JToken Unsubscribe(JObject req)
        {
            var name = req.Value<string>("name");
            if (string.IsNullOrEmpty(name))
                return JsonHelper.Error("missing 'name' field");

            if (!Listeners.ContainsKey(name))
                return JsonHelper.Error($"event {name} is not listen yet");

            Listeners.Remove(name);
            return new JObject();
        }

        public JToken Do(JObject req)
        {
            var action = req.Value<string>("action");
            if (string.IsNullOrEmpty(action))
                return JsonHelper.Error("missing 'action' field");

            switch (action.ToLower())
            {
                case "subscribe":
                    return Subscribe(req);
                case "unsubscribe":
                    return Unsubscribe(req);
                default:
                    return JsonHelper.Error($"unknown action {action}");
            }
        }

        public void Init(IEventSource es)
        {
            EventSource = es;
            es.RegisterEventType("otk::packet");
        }

        struct EventListener
        {
            [JsonProperty("name")]
            public string Name;

            [JsonProperty("evt_name")]
            public string EventName;
            
            [JsonProperty("filters")]
            public Filter[] Filters;
        }

        struct Filter
        {
            [JsonProperty("direction")]
            public bool? IsSent;
            [JsonProperty("length")]
            public int? Length;
            [JsonProperty("opcode")]
            public int? Opcode;
            [JsonProperty("self_actor")]
            public bool? SelfActor;

            public bool Match(bool isSent, int length, int opcode, bool actorSelf)
            {
                if (IsSent.HasValue && IsSent.Value != isSent)
                    return false;
                if (Length.HasValue && Length.Value != length)
                    return false;
                if (Opcode.HasValue && Opcode.Value != opcode)
                    return false;
                if (SelfActor.HasValue && SelfActor.Value != actorSelf)
                    return false;

                return true;
            }

            public static Filter FromJson(JObject token)
            {
                var f = new Filter();
                if (token.TryGetValue("direction", out var direction))
                {
                    if (direction.Type == JTokenType.String)
                    {
                        var dirStr = direction.ToString().ToLower();
                        if (dirStr == "send" || dirStr == "sent")
                            f.IsSent = true;
                        else if (dirStr == "recv" || dirStr == "received")
                            f.IsSent = false;
                    }
                    else if (direction.Type == JTokenType.Boolean)
                    {
                        f.IsSent = direction.ToObject<bool>();
                    }
                }
                if (token.TryGetValue("length", out var length) && length.Type == JTokenType.Integer)
                {
                    f.Length = length.ToObject<int>();
                }
                if (token.TryGetValue("opcode", out var opcode) && opcode.Type == JTokenType.Integer)
                {
                    f.Opcode = opcode.ToObject<int>();
                }
                return f;
            }
        }
    }
}
