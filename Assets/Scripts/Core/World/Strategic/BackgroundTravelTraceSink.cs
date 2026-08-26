using System;
using System.Diagnostics;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Background Travel Trace 输出（Core 侧不引用 UnityEngine；Host 可注入 LogInfo）。
    /// </summary>
    public static class BackgroundTravelTraceSink
    {
        public static Action<string> LogInfo { get; set; }

        public static void Emit(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            Debug.WriteLine(line);
            LogInfo?.Invoke(line);
        }
    }
}
