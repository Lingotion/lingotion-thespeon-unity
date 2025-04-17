using UnityEngine;
using TMPro;

[ExecuteAlways]
public class GraphTextSync : MonoBehaviour
{
    [Header("Graph UI Elements")]
    public RectTransform graphContainer;  // The graph panel
    public TextMeshProUGUI mirroredText;  // The text mirrored along the X-axis
    public TMP_InputField textInput;      // The original input field

    [Header("Text Stretching")]
    public float padding = 10f; // Space to prevent text overflow
    public float minFontWidth = 0.5f; // Minimum horizontal scaling when text is too long
    public float defaultFontWidth = 1f; // Normal font width (default)



    private void Start()
    {
        if (textInput != null)
        {
            textInput.onValueChanged.AddListener(UpdateMirroredText);
            UpdateMirroredText(textInput.text); // Initialize text
        }
    }

    void UpdateMirroredText(string newText)
    {
        if (mirroredText == null || graphContainer == null) return;

        float graphWidth = graphContainer.rect.width - padding;
        float textPreferredWidth = mirroredText.GetPreferredValues(newText).x;

        if (textPreferredWidth < graphWidth)
        {
            float totalSpacing = graphWidth - textPreferredWidth;
            float spacingPerCharacter = totalSpacing / (newText.Length - 1);
            mirroredText.text = InsertCspaceTags(newText, spacingPerCharacter);
            mirroredText.rectTransform.localScale = Vector3.one;
        }
        else
        {
            mirroredText.text = newText;
            float scaleFactor = graphWidth / textPreferredWidth;
            mirroredText.rectTransform.localScale = new Vector3(scaleFactor, 1, 1);
        }
    }



    string InsertCspaceTags(string input, float spacing)
    {
        string cspaceTag = $"<cspace={spacing}>";
        return string.Join(cspaceTag, input.ToCharArray());
    }
}
