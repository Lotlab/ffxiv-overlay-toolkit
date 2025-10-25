using Advanced_Combat_Tracker;
using Lotlab.PluginCommon.FFXIV;
using Lotlab.PluginCommon.Overlay;
using Newtonsoft.Json.Linq;
using RainbowMage.OverlayPlugin;
using System;
using System.Windows.Forms;

namespace OverlayTK
{
    public class OverlayToolkit : IActPluginV1, IOverlayAddonV2
    {
        /// <summary>
        /// FFXIV 解析插件的引用
        /// </summary>
        public ACTPluginProxy FFXIV { get; set; } = null;

        /// <summary>
        /// 状态标签的引用
        /// </summary>
        Label statusLabel = null;

        /// <summary>
        /// 事件源
        /// </summary>
        OverlayToolkitES eventSource = null;

        void IActPluginV1.InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            // 设置状态标签引用方便后续使用
            statusLabel = pluginStatusText;

            // 遍历所有插件
            var plugins = ActGlobals.oFormActMain.ActPlugins;
            foreach (var item in plugins)
            {
                var obj = item?.pluginObj;
                if (obj != null && ACTPluginProxy.IsFFXIVPlugin(obj))
                {
                    FFXIV = new ACTPluginProxy(obj);
                    break;
                }
            }

            // 若解析插件不存在或不正常工作，则提醒用户，并结束初始化
            if (FFXIV == null || !FFXIV.PluginStarted)
            {
                statusLabel.Text = "FFXIV ACT Plugin 工作不正常，无法初始化。";
                return;
            }

            // 直接隐藏掉不需要显示的插件页面
            (pluginScreenSpace.Parent as TabControl).TabPages.Remove(pluginScreenSpace);

            // 更新状态标签的内容
            statusLabel.Text = "插件初始化成功，等待悬浮窗初始化。您可能需要重新加载悬浮窗插件。";
        }

        void IActPluginV1.DeInitPlugin()
        {
            FFXIV = null;
            statusLabel.Text = "插件已退出!";
        }

        void IOverlayAddonV2.Init()
        {
            var container = Registry.GetContainer();
            var registry = container.Resolve<Registry>();

            container.Resolve<ILogger>();

            // 注册事件源
            eventSource = new OverlayToolkitES(container, this);
            registry.StartEventSource(eventSource);

            statusLabel.Text = "初始化成功";
        }
    }

    public class OverlayToolkitES : EventSourceBase, IEventSource
    {
        OverlayToolkit Plugin { get; }

        IWorker[] Workers { get; }

        public OverlayToolkitES(TinyIoCContainer c, OverlayToolkit plugin) : base(c)
        {
            Plugin = plugin;

            // 设置事件源名称，必须是唯一的
            Name = "OverlayToolkitES";

            Workers = new IWorker[]
            {
                new GameVersionInfo(plugin.FFXIV),
                new FetchWorker(),
                new HIDWorker(),
                new PacketWorker(plugin.FFXIV),
            };

            // 注册事件接收器
            foreach (var worker in Workers)
            {
                worker.Init(this);

                if (!string.IsNullOrEmpty(worker.Name))
                    RegisterEventHandler(worker.Name, worker.Do);
            }
        }

        ~OverlayToolkitES()
        {
        }

        public override Control CreateConfigControl()
        {
            return null;
        }

        public override void LoadConfig(IPluginConfig config)
        {
        }

        public override void SaveConfig(IPluginConfig config)
        {
        }

        void IEventSource.RegisterEventType(string type)
        {
            RegisterEventType(type);
        }

        void IEventSource.DispatchEvent(JObject e)
        {
            DispatchEvent(e);
        }

        void IEventSource.RegisterEventHandler(string name, Func<JObject, JToken> handler)
        {
            RegisterEventHandler(name, handler);
        }

        bool IEventSource.HasSubscriber(string eventName)
        {
            return HasSubscriber(eventName);
        }
    }
}
