using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Keeps the former presentation hook so existing scene bindings remain valid.
    /// Interaction spots stay in HostInteractSpots; their debug discs are not formal art.
    /// </summary>
    public sealed class HostInteractSpotPresenter : MonoBehaviour
    {
        Transform _root;

        public void Bind(PlayableHostBootstrap host) { }

        public void Rebuild() => Clear();

        public void Clear()
        {
            if (_root == null)
                _root = transform.Find("InteractSpots");
            if (_root == null) return;
            if (Application.isPlaying) Destroy(_root.gameObject);
            else DestroyImmediate(_root.gameObject);
            _root = null;
        }
    }
}
