// This code and software are protected by intellectual property law and is the property of Lingotion AB, reg. no. 559341-4138, Sweden. The code and software may only be used and distributed according to the Terms of Service and Use found at www.lingotion.com.
#if UNITY_EDITOR
using UnityEditor;
using Lingotion.Thespeon.Inputs;

namespace Lingotion.Thespeon.EditorTools
{
    [CustomEditor(typeof(ThespeonCharacterAsset))]
    public class ThespeonCharacterAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // Everything but the version stays editable so manually created assets remain usable.
            DrawPropertiesExcluding(serializedObject, "m_Script", "moduleVersion");
            serializedObject.ApplyModifiedProperties();

            var asset = (ThespeonCharacterAsset)target;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Module Version", string.IsNullOrEmpty(asset.moduleVersion) ? "Unknown" : asset.moduleVersion);
            }
        }
    }
}
#endif
