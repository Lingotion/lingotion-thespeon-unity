using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public TMP_Text targetText;

    // Update is called once per frame
    void Update()
    {
        float value = 1f / Time.smoothDeltaTime;
        targetText.text = value.ToString("#") + " FPS";
    }
}
