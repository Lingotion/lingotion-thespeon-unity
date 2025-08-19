using UnityEngine;
using TMPro;

[ExecuteAlways]
public class GraphTextSync : MonoBehaviour
{
    [Header("Graph UI Elements")]
    public RectTransform graphContainer;
    public TextMeshProUGUI mirroredText;
    public TMP_InputField textInput;
    [Header("Text Stretching")]
    private readonly float padding = 10f;


    private void Start()
    {
        if (textInput != null)
        {
            textInput.onValueChanged.AddListener(UpdateMirroredText);
            UpdateMirroredText(textInput.text);
        }
    }

    private void UpdateMirroredText(string newText)
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

    private string InsertCspaceTags(string input, float spacing)
    {
        string cspaceTag = $"<cspace={spacing}>";
        return string.Join(cspaceTag, input.ToCharArray());
    }
}
