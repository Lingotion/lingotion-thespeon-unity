using UnityEngine;
using UnityEditor;
using Lingotion.Thespeon.CurvesToUI;

[CustomEditor(typeof(CurvesToUI))]
public class CurvesToUIEditor : Editor
{
    // private CurvesToUI script;
    private SerializedProperty speedCurveProp;
    private SerializedProperty loudnessCurveProp;

    private void OnEnable()
    {
        // script = (CurvesToUI)target;
        speedCurveProp = serializedObject.FindProperty("speedCurve");
        loudnessCurveProp = serializedObject.FindProperty("loudnessCurve");
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();

        serializedObject.Update();

        EditorGUILayout.LabelField("Custom Curve Editor", EditorStyles.boldLabel);

        // Speed Curve
        // EditorGUILayout.LabelField("Speed Curve", EditorStyles.boldLabel);
        Rect speedCurveRect = GUILayoutUtility.GetRect(100, 50);
        EditorGUI.CurveField(speedCurveRect, speedCurveProp, Color.red, new Rect(0, 0, 1, 2));

        // Loudness Curve
        // EditorGUILayout.LabelField("Loudness Curve", EditorStyles.boldLabel);
        Rect loudnessCurveRect = GUILayoutUtility.GetRect(100, 50);
        EditorGUI.CurveField(loudnessCurveRect, loudnessCurveProp, Color.blue, new Rect(0, 0, 1, 2));

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }    
    }
}
