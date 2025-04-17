using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DropdownHandler : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    public void OnDropdownValue(){
        if(dropdown == null){
            Debug.LogError("No dropdown");
        }
        int idx=dropdown.value;
        // TMP_Dropdown.OptionData selectedOptionData = dropdown.options[idx];

        if(idx==0){
            // Debug.Log("placeholder selected");
            return;
        }
        TextStyler.Instance.DropdownValueChanged(dropdown);

    }

    public void OnLoadPredefinedInput()
    {
        if(dropdown == null){
            Debug.LogError("No dropdown");
        }
        if(dropdown.value==0){
            return;
        }
        TextStyler.Instance.OnLoadInput(dropdown.options[dropdown.value].text);
    }

    // //To be used when ThespeonAPI gets registered models and their languages.
    // public void addOption(string text){
    //     dropdown.options.Add(new TMP_Dropdown.OptionData(text));

    //     dropdown.RefreshShownValue();
    // }

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

    public void SetToDefaultOption()
    {
        dropdown.value = 0;
    }

    public string GetSelectedOption()
    {
        if(dropdown == null){
            Debug.LogError("No dropdown");
        }
        if(dropdown.value==0){
            return null;
        }
        return dropdown.options[dropdown.value].text;
    }
}
