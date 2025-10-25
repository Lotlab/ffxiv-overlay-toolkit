using Newtonsoft.Json.Linq;
using System;

namespace OverlayTK
{
    public interface IWorker
    {
        /// <summary>
        /// Worker method name, used to act as call method name.
        /// </summary>
        /// <remarks>
        /// Set empty if you don't want to use 'Do' as call method.
        /// </remarks>
        string Name { get; }

        JToken Do(JObject req);

        void Init(IEventSource es);
    }

    public interface IEventSource
    {
        void RegisterEventType(string type);
        void DispatchEvent(JObject e);
        void RegisterEventHandler(string name, Func<JObject, JToken> handler);
        bool HasSubscriber(string eventName);
    }
}
