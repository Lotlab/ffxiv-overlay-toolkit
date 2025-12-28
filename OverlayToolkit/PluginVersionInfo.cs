using Newtonsoft.Json.Linq;
using System.Reflection;

namespace OverlayTK
{
    public class PluginVersionInfo : IWorker
    {
        public PluginVersionInfo()
        {
        }
        
        public string Name => "otk::plugin_ver";
        public JToken Do(JObject req)
        {
            return JObject.FromObject(new
            {
                version = Assembly.GetExecutingAssembly().GetName().Version.ToString(),
            });
        }
        public void Init(IEventSource es)
        {
        }
    }
}
