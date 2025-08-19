using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    public TMP_Text targetText;

    void Update()
    {
        float value = 1f / Time.smoothDeltaTime;
        targetText.text = value.ToString("#") + " FPS";
    }
}
