namespace XianXia.Unity.Host
{
    /// <summary>逐字显示对话正文；View 层专用，与 Content 数据无关。</summary>
    public sealed class HostDialogueTypewriter
    {
        string _full = string.Empty;
        int _revealed;
        float _accum;

        public float CharactersPerSecond { get; set; } = 32f;

        public bool IsComplete => _revealed >= _full.Length;

        public string FullText => _full;

        public string VisibleText
        {
            get
            {
                if (_full.Length == 0)
                    return string.Empty;
                if (IsComplete)
                    return _full;
                return _full.Substring(0, _revealed);
            }
        }

        public void Begin(string fullText)
        {
            _full = fullText ?? string.Empty;
            _revealed = 0;
            _accum = 0f;
        }

        public void Skip()
        {
            _revealed = _full.Length;
            _accum = 0f;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (IsComplete || _full.Length == 0)
                return;

            var rate = CharactersPerSecond;
            if (rate <= 0f)
            {
                Skip();
                return;
            }

            _accum += unscaledDeltaTime * rate;
            while (_accum >= 1f && !IsComplete)
            {
                _revealed++;
                _accum -= 1f;
            }
        }
    }
}
