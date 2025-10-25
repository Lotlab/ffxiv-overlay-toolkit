using Lotlab.PluginCommon.FFXIV;
using Newtonsoft.Json.Linq;

namespace OverlayTK
{
    public class GameVersionInfo : IWorker
    {
        public ACTPluginProxy FFXIV { get; }
        public GameVersionInfo(ACTPluginProxy ffxiv)
        {
            FFXIV = ffxiv;
        }
        
        public string Name => "otk::game_ver";

        public JToken Do(JObject req)
        {
            return JObject.FromObject(new
            {
                version = FFXIV.DataRepository.GetGameVersion(),
                lang = FFXIV.DataRepository.GetSelectedLanguageID(),
            });
        }

        public void Init(IEventSource es)
        {
        }
    }
}
