using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;


public class DropdownHandler : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;
    public event UnityAction<string> DropdownValueChanged;

    public void OnDropdownValue()
    {
        if (dropdown == null)
        {
            Lingotion.Thespeon.Core.LingotionLogger.Error("No dropdown");
        }
        int idx = dropdown.value;

        if (idx == 0)
        {
            return;
        }
        DropdownValueChanged?.Invoke(dropdown.options[idx].text);
    }
    public void SetOptions(List<string> options)
    {
        dropdown.options = new List<TMP_Dropdown.OptionData> { dropdown.options[0] };
        foreach (string option in options)
        {
            dropdown.options.Add(new TMP_Dropdown.OptionData(option));
        }
        dropdown.RefreshShownValue();
    }


    public List<string> GetNonDefaultOptions()
    {
        List<string> options = new List<string>();
        for (int i = 1; i < dropdown.options.Count; i++)
        {
            options.Add(dropdown.options[i].text);
        }
        return options;
    }

    public string GetSelectedOption()
    {
        if (dropdown == null)
        {
            Lingotion.Thespeon.Core.LingotionLogger.Error("No dropdown");
            return null;
        }
        if (dropdown.value == 0)
        {
            return null;
        }
        return dropdown.options[dropdown.value].text;
    }
}
