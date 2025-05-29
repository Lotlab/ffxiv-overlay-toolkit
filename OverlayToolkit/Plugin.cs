using Advanced_Combat_Tracker;
using Lotlab.PluginCommon.FFXIV;
using Lotlab.PluginCommon.FFXIV.Parser;
using Lotlab.PluginCommon.Overlay;
using Newtonsoft.Json.Linq;
using RainbowMage.OverlayPlugin;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;

namespace OverlayTK
{
    public class OverlayToolkit : IActPluginV1, IOverlayAddonV2
    {
        /// <summary>
        /// FFXIV 解析插件的引用
        /// </summary>
        ACTPluginProxy FFXIV { get; set; } = null;

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

    public class OverlayToolkitES : EventSourceBase
    {
        OverlayToolkit Plugin { get; }

        HIDWorker HID { get; }

        public OverlayToolkitES(TinyIoCContainer c, OverlayToolkit plugin) : base(c)
        {
            Plugin = plugin;

            HID = new HIDWorker();
            HID.RawEvent += (obj) => DispatchEvent(obj);

            // 设置事件源名称，必须是唯一的
            Name = "OverlayToolkitES";

            RegisterEventTypes(new List<string>()
            {
                "otk::hid::inputreport",
                "otk::hid::devicechanged",
            });


            // 注册事件接收器
            RegisterEventHandler("Fetch", fetch);
            RegisterEventHandler("otk::fetch", fetch);
            RegisterEventHandler("otk::hid", hid);
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

        JToken fetch(JObject token)
        {
            var req = new FetchWorker().Fetch(token);
            req.Wait(3000);

            if (req.Exception != null)
                return JObject.FromObject(req.Exception);

            return req.Result;
        }

        JToken hid(JObject token)
        {
            return HID.Request(token);
        }
    }
}
