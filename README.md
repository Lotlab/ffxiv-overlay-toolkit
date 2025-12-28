# Overlay Toolkit

一个悬浮窗增强插件，提供了一些常用的接口。可以搭配 [https://www.npmjs.com/package/overlay-toolkit](overlay-toolkit) 库使用。

当前支持的增强方法：

- fetch: 代替浏览器的 fetch 方法，实现跨域访问；
- game_ver: 获取游戏客户端的版本；
- hid: WebHID 支持，令你的悬浮窗可以与任何 HID 外设交互;
- packet: 游戏原始数据包监听，仅悬浮窗即可实现获取/解析网络数据包功能;

具体用法请参考上述 overlay-toolkit 中封装后的方法。

# Contribution Guide

如果你觉得缺少什么功能，欢迎开启 Pull Request。