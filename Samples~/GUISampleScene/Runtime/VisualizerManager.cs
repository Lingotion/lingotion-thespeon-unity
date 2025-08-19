using UnityEngine;
using TMPro;

public class VisualizerManager : MonoBehaviour
{
    public TMP_Text jsonVisualizer;

    public void UpdateVisualizer(string json)
    {
        if (jsonVisualizer != null)
        {
            jsonVisualizer.text = json;
        }
        else
        {
            Lingotion.Thespeon.Core.LingotionLogger.Error("jsonVisualizer is not assigned in the Inspector.");
        }
    }
    

    public void CopyTextToClipboard()
    {
        GUIUtility.systemCopyBuffer = jsonVisualizer.text;
        Lingotion.Thespeon.Core.LingotionLogger.Info("Text copied to clipboard: \n" + jsonVisualizer.text);
    }
    
}
