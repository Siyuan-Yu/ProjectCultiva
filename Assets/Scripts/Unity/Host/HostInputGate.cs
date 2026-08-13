namespace XianXia.Unity.Host
{
    /// <summary>
    /// 模态 UI（任务日志等）打开时阻断相机缩放／平移与 RTS 世界操作。
    /// </summary>
    public static class HostInputGate
    {
        public static bool BlockWorldCamera { get; set; }

        public static bool BlockWorldInteraction { get; set; }

        public static void Clear()
        {
            BlockWorldCamera = false;
            BlockWorldInteraction = false;
        }
    }
}
