using System;

namespace XianXia.Unity.Host
{
    public interface IHostDialogueView
    {
        void Draw(
            HostDialogueModel model,
            Action<int> onChoiceSelected,
            Action onDismissFallback);
    }
}
