#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using XianXia.Unity.Content;
using XianXia.Unity.Host;

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

            var appearanceErrors = HostStepAArt.ValidateAppearanceMappings();
            if (appearanceErrors.Count > 0)
            {
                var failure = string.Join("; ", appearanceErrors);
                Debug.LogError("[ContentValidate] 外观映射无效：" + failure);
                EditorUtility.DisplayDialog("Content Validate", "FAILED\n" + failure, "OK");
                return;
            }
            try
            {
                PlayerBuildProfiles.ReadFormalOpening(Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "Content", "BaseGame")));
            }
            catch (Exception error)
            {
                Debug.LogError("[ContentValidate] 正式开局无效：" + error.Message);
                EditorUtility.DisplayDialog("Content Validate", "FAILED\n" + error.Message, "OK");
                return;
            }

            Debug.Log("[ContentValidate] " + message);
            EditorUtility.DisplayDialog("Content Validate", message, "OK");
        }
    }
}
#endif
