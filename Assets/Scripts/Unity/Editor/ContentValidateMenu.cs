#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XianXia.Unity.Content;

namespace XianXia.Unity.EditorTools
{
    /// <summary>Chapter Production Toolkit: validate BaseGame cross-references.</summary>
    public static class ContentValidateMenu
    {
        [MenuItem("XianXia/Content/Validate BaseGame Package")]
        public static void ValidateBaseGame()
        {
            if (!ContentPackageValidationFacade.TryValidateBaseGameFromDataPath(Application.dataPath, out var message))
            {
                Debug.LogError("[ContentValidate] FAIL: " + message);
                EditorUtility.DisplayDialog("Content Validate", "FAILED\n" + message, "OK");
                return;
            }

            Debug.Log("[ContentValidate] " + message);
            EditorUtility.DisplayDialog("Content Validate", message, "OK");
        }
    }
}
#endif
