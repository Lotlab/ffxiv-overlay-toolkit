using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using HidSharp.Reports.Units;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OverlayTK
{
    public class HIDWorker
    {
        #region HidSharp Wrapper
        DeviceList deviceList => DeviceList.Local;

        Dictionary<string, HIDDeviceInfo> cachedDevice { get; } = new Dictionary<string, HIDDeviceInfo>();

        public event Action<HIDDeviceInfo, bool> DeviceChanged;

        public event Action<HIDDeviceInfo, byte[]> DeviceDataReceived;

        public HIDWorker()
        {
            deviceList.Changed += hidDeviceChanged;
        }

        ~HIDWorker()
        {
            deviceList.Changed -= hidDeviceChanged;
        }

        public JToken GetDevices()
        {
            var devs = deviceList.GetHidDevices();

            List<HIDDeviceInfo> devices = new List<HIDDeviceInfo>();
            foreach (var item in devs)
            {
                if (cachedDevice.ContainsKey(item.DevicePath))
                {
                    var cached = cachedDevice[item.DevicePath];
                    if (cached.Connected)
                    {
                        devices.Add(cached);
                        continue;
                    }
                }

                HIDDeviceInfo dev = createDeviceInfo(item);
                devices.Add(dev);
            }
            return JObject.FromObject(new {
                devices = devices.Select(item => item.GetToken()).ToArray()
            });
        }

        private HIDDeviceInfo createDeviceInfo(HidDevice item)
        {
            var dev = new HIDDeviceInfo(item);
            dev.RecvData += onRecvData;

            cachedDevice[item.DevicePath] = dev;
            return dev;
        }

        private void hidDeviceChanged(object sender, DeviceListChangedEventArgs e)
        {
            var devs = deviceList.GetHidDevices();
            HashSet<string> currentDevs = new HashSet<string>();

            List<HIDDeviceInfo> addedDevices = new List<HIDDeviceInfo>();
            List<HIDDeviceInfo> removedDevices = new List<HIDDeviceInfo>();

            foreach (var item in devs)
            {
                currentDevs.Add(item.DevicePath);
                if (cachedDevice.ContainsKey(item.DevicePath))
                {
                    var cached = cachedDevice[item.DevicePath];
                    if (cached.Connected)
                        continue;

                    addedDevices.Add(cached);
                }
                else
                {
                    var device = createDeviceInfo(item);
                    addedDevices.Add(device);
                }
            }

            foreach (var item in cachedDevice)
            {
                if (item.Value.Connected == false)
                    continue;

                if (currentDevs.Contains(item.Key))
                    continue;

                removedDevices.Add(item.Value);
                item.Value.Connected = false;
            }

            foreach (var item in addedDevices)
                onDeviceChange(item, true);
            foreach (var item in removedDevices)
                onDeviceChange(item, false);
        }

        HIDDeviceInfo getDevice(string deviceID)
        {
            if (!cachedDevice.ContainsKey(deviceID))
                throw new KeyNotFoundException($"device {deviceID} is not found");

            return cachedDevice[deviceID];
        }

        HIDDeviceInfo getOpenedDevice(string deviceID)
        {
            var device = getDevice(deviceID);
            if (!device.Opened)
                throw new InvalidOperationException($"device {deviceID} is not open");
            return device;
        }

        public void OpenDevice(string deviceID)
        {
            var device = getDevice(deviceID);
            if (device.Opened)
                return;

            if (!device.Open())
                throw new Exception($"cannot open device");
        }

        public void CloseDevice(string deviceID)
        {
            var device = getDevice(deviceID);
            if (!device.Opened)
                return;

            device.Close();
        }

        public void SendReport(string deviceID, byte[] data)
        {
            var device = getOpenedDevice(deviceID);
            device.Write(data);
        }

        public void SendFeatureReport(string deviceID, byte[] data)
        {
            var device = getOpenedDevice(deviceID);
            device.WriteFeature(data);
        }

        public byte[] ReadFeatureReport(string deviceID, byte[] data)
        {
            var device = getOpenedDevice(deviceID);
            return device.ReadFeature(data);
        }
        #endregion
        #region Overlay Handler
        public event Action<JObject> RawEvent;

        private void onRecvData(HIDDeviceInfo arg1, byte[] arg2)
        {
            DeviceDataReceived?.Invoke(arg1, arg2);

            RawEvent?.Invoke(JObject.FromObject(new { 
                type = "otk::hid::inputreport",
                instanceId = arg1.Device.DevicePath,
                data = BitConverter.ToString(arg2).Replace("-", ""),
            }));
        }

        private void onDeviceChange(HIDDeviceInfo dev, bool add)
        {
            DeviceChanged?.Invoke(dev, add);

            RawEvent?.Invoke(JObject.FromObject(new
            {
                type = "otk::hid::devicechanged",
                device = dev.GetToken(),
                add = add
            }));
        }

        public JToken Request(JObject obj)
        {
            var action = obj.GetValue("type");
            if (action == null)
                return Error("missing type");

            var instanceId = obj.GetValue("instanceId");
            try
            {
                switch (action.ToString())
                {
                    case "getDevices":
                    case "requestDevice":
                        return GetDevices();
                    case "open":
                        if (instanceId == null)
                            return Error("missing instanceId");
                        OpenDevice(instanceId.ToString());
                        return Ok();
                    case "close":
                        if (instanceId == null)
                            return Error("missing instanceId");
                        CloseDevice(instanceId.ToString());
                        return Ok();
                    case "send":
                        {
                            if (instanceId == null)
                                return Error("missing instanceId");

                            var data = StringToByteArray(obj.GetValue("data")?.ToString());
                            if (data == null)
                                return Error("missing data");

                            int? reportID = obj.GetValue("reportId")?.Value<int>();
                            data = data.Prepend((byte)(reportID ?? 0)).ToArray();

                            SendReport(instanceId.ToString(), data);
                            return Ok();
                        }
                    case "sendFeature":
                        {
                            if (instanceId == null)
                                return Error("missing instanceId");
                            var data = StringToByteArray(obj.GetValue("data")?.ToString());
                            if (data == null)
                                return Error("missing data");

                            int? reportID = obj.GetValue("reportId")?.Value<int>();
                            data = data.Prepend((byte)(reportID ?? 0)).ToArray();

                            SendFeatureReport(instanceId.ToString(), data);
                            return Ok();
                        }
                    case "recvFeature":
                        {
                            if (instanceId == null)
                                return Error("missing instanceId");

                            int? reportID = obj.GetValue("reportId")?.Value<int>();
                            if (reportID == null)
                                return Error("missing reportID");

                            byte[] data = new byte[1] { (byte)reportID };

                            SendFeatureReport(instanceId.ToString(), data);
                            return Ok();
                        }
                    default:
                        return Error($"unknown action {action}");
                }
            }
            catch (Exception e)
            {
                return Error(e);
            }
        }

        static JToken Error(string message)
        {
            return JObject.FromObject(new
            {
                error = message
            });
        }

        static JToken Error(Exception e)
        {
            return JObject.FromObject(new
            {
                error = e.Message,
                stack = e.StackTrace,
            });
        }

        static JToken Ok(object data = null)
        {
            return JObject.FromObject(new
            {
                ok = true,
                data = data
            });
        }

        static byte[] StringToByteArray(string hex)
        {
            if (hex == null)
                return null;

            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));

            return arr;
        }

        static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        #endregion
    }
    public class HIDDeviceInfo
    {
        public HidDevice Device { get; }

        public int VendorID { get; }
        public int ProductID { get; }
        public string ProductName { get; }
        public ReportDescriptor Descriptor { get; }

        public event Action<HIDDeviceInfo, byte[]> RecvData;

        HidDeviceInputReceiver Receiver { get; } = null;

        HidStream stream { get; set; } = null;

        public bool Opened => stream != null;

        public bool Connected { get; set; } = true;

        public HIDDeviceInfo(HidDevice dev)
        {
            Device = dev;
            VendorID = dev.VendorID;
            ProductID = dev.ProductID;
            try
            {
                ProductName = dev.GetProductName();
            }
            catch (Exception) { }
            try
            {
                Descriptor = dev.GetReportDescriptor();
            }
            catch (Exception) { }

            try
            {
                if (Receiver == null)
                {
                    Receiver = new HidDeviceInputReceiver(Descriptor);
                    Receiver.Received += onReceived;
                }
            }
            catch (Exception) { }
        }

        private void onReceived(object sender, EventArgs e)
        {
            byte[] buffer = new byte[1024];

            if (Receiver.TryRead(buffer, 0, out var report))
            {
                int len = report.Length;
                byte[] data = new byte[len];
                Array.Copy(buffer, data, len);

                RecvData?.Invoke(this, data);
            }
        }

        public bool Open()
        {
            if (this.stream != null)
                return false;

            if (Device.TryOpen(out var stream))
            {
                this.stream = stream;
                if (Receiver != null)
                    Receiver.Start(stream);

                return true;
            }

            return false;
        }

        public bool Close()
        {
            if (this.stream == null)
                return false;

            this.stream.Close();
            this.stream = null;
            return true;
        }

        public void Write(byte[] buffer)
        {
            if (this.stream == null)
                return;
            this.stream.Write(buffer);
        }

        public void WriteFeature(byte[] buffer)
        {
            if (this.stream == null)
                return;

            this.stream.SetFeature(buffer);
        }

        public byte[] ReadFeature(byte[] buffer)
        {
            if (this.stream == null)
                return Array.Empty<byte>();

            this.stream.GetFeature(buffer);
            throw new NotImplementedException("ReadFeature is not implemented yet.");
        }

        public JToken GetToken()
        {
            var obj = new
            {
                instanceId = Device.DevicePath,
                vendorId = VendorID,
                productId = ProductID,
                productName = ProductName,
                collections = Descriptor != null ? HIDCollectionInfo.GetToken(Descriptor) : null,
            };
            return JObject.FromObject(obj);
        }
    }

    class HIDCollectionInfo
    {
        public static JToken GetToken(ReportDescriptor dev)
        {
            uint? usage = null, usagePage = null;

            var t = dev.DeviceItems[0];
            var o = t.Usages.GetValuesFromIndex(0);
            if (o.Count() > 0)
            {
                usage = o.First() & 0xFFFF;
                usagePage = o.First() >> 16;
            }

            var obj = new
            {
                usagePage = usagePage,
                usage = usage,
                type = t.CollectionType,
                children = Array.Empty<object>(),
                inputReports = dev.InputReports.Select(item => GetToken(item)).ToArray(),
                outputReports = dev.OutputReports.Select(item => GetToken(item)).ToArray(),
                featureReports = dev.FeatureReports.Select(item => GetToken(item)).ToArray(),
            };
            return JObject.FromObject(obj);
        }

        public static object GetToken(DeviceItem item)
        {
            return new
            {
                inputReports = item.InputReports.Select(report => GetToken(report)).ToArray(),
                outputReports = item.OutputReports.Select(report => GetToken(report)).ToArray(),
                featureReports = item.FeatureReports.Select(report => GetToken(report)).ToArray(),
                usagePage = item.Usages,
            };
        }

        public static object GetToken(HidSharp.Reports.Report report)
        {
            return new
            {
                reportId = report.ReportID,
                items = report.DataItems.Select(item => GetToken(item)).ToArray(),
            };
        }

        public static JToken GetToken(HidSharp.Reports.DataItem item)
        {
            var token = JObject.FromObject(new
            {
                isAbsolute = item.IsAbsolute,
                isArray = item.IsArray,
                isBufferedBytes = item.Flags.HasFlag(HidSharp.Reports.DataItemFlags.BufferedBytes),
                isConstant = item.IsConstant,
                isLinear = !item.Flags.HasFlag(HidSharp.Reports.DataItemFlags.Nonlinear),
                isVolatile = item.Flags.HasFlag(HidSharp.Reports.DataItemFlags.Volatile),
                hasNull = item.HasNullState,
                hasPreferredState = item.HasPreferredState,
                // wrap = item.wrap,
                reportSize = item.ElementBits,
                reportCount = item.ElementCount,
                unitExponent = item.UnitExponent,
                unitSystem = ToString(item.Unit.System),
                unitFactorLengthExponent = item.Unit.LengthExponent,
                unitFactorMassExponent = item.Unit.MassExponent,
                unitFactorTimeExponent = item.Unit.TimeExponent,
                unitFactorTemperatureExponent = item.Unit.TemperatureExponent,
                unitFactorCurrentExponent = item.Unit.CurrentExponent,
                unitFactorLuminousIntensityExponent = item.Unit.LuminousIntensityExponent,
                logicalMinimum = item.LogicalMinimum,
                logicalMaximum = item.LogicalMaximum,
                physicalMinimum = item.PhysicalMinimum,
                physicalMaximum = item.PhysicalMaximum,
                // strings = item.Strings,
            });

            var usages = item.Usages.GetAllValues().ToList();
            usages.Sort();
            bool isRange = usages.Count >= 2;
            for (var i = 1; i < usages.Count; i++)
            {
                if (usages[i] - usages[i - 1] != 1)
                {
                    isRange = false;
                    break;
                }
            }
            token.Add("isRange", isRange);
            if (isRange)
            {
                token.Add("usageMinimum", usages.First());
                token.Add("usageMaximum", usages.Last());
            }
            else
            {
                token.Add("usages", JToken.FromObject(usages.ToArray()));
            }

            return token;
        }

        public static string ToString(UnitSystem unit)
        {
            switch (unit)
            {
                case UnitSystem.None:
                    return "none";
                case UnitSystem.SILinear:
                    return "si-linear";
                case UnitSystem.SIRotation:
                    return "si-rotation";
                case UnitSystem.EnglishLinear:
                    return "english-linear";
                case UnitSystem.EnglishRotation:
                    return "english-rotation";
                default:
                    return "vendor-defined";
            }
        }
    }
}