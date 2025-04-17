using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputConfig", menuName = "Input/InputConfig")]
public class InputConfig : ScriptableObject
{
    public InputActionAsset inputActions;
}
