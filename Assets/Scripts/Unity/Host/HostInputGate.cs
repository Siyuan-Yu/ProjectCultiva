namespace XianXia.Unity.Host
{
    /// <summary>
    /// 每个模态界面只持有自己的输入锁；关闭界面不得解除其它界面的锁。
    /// </summary>
    public static class HostInputGate
    {
        /// <summary>Full modal owners block both camera and world interaction.</summary>
        static readonly System.Collections.Generic.HashSet<string> _owners = new System.Collections.Generic.HashSet<string>();
        static readonly System.Collections.Generic.HashSet<string> _interactionOwners = new System.Collections.Generic.HashSet<string>();
        static readonly System.Collections.Generic.HashSet<string> _textInputOwners = new System.Collections.Generic.HashSet<string>();
        public static void Acquire(string owner) { if (!string.IsNullOrEmpty(owner)) _owners.Add(owner); }
        public static void Release(string owner) { if (!string.IsNullOrEmpty(owner)) _owners.Remove(owner); }
        public static void AcquireInteraction(string owner)
        {
            if (!string.IsNullOrEmpty(owner)) _interactionOwners.Add(owner);
        }
        public static void ReleaseInteraction(string owner)
        {
            if (!string.IsNullOrEmpty(owner)) _interactionOwners.Remove(owner);
        }
        public static void AcquireTextInput(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return;
            _textInputOwners.Add(owner);
            Acquire(owner);
        }
        public static void ReleaseTextInput(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return;
            _textInputOwners.Remove(owner);
            Release(owner);
        }
        public static bool TextInputFocused => _textInputOwners.Count > 0;
        public static bool EncounterModalLock
        {
            get => _owners.Contains("CharacterEncounterUI");
            set { if (value) Acquire("CharacterEncounterUI"); else Release("CharacterEncounterUI"); }
        }

        public static bool BlockWorldCamera
        {
            get => _owners.Count > 0;
        }

        public static bool BlockWorldInteraction
        {
            get => _owners.Count > 0 || _interactionOwners.Count > 0;
        }

        // Only the explicit session boundary may clear all owners.
        public static void ResetSession()
        {
            _owners.Clear();
            _interactionOwners.Clear();
            _textInputOwners.Clear();
        }
    }
}
