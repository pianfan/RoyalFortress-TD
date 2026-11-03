using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class FortressButtonSound : MonoBehaviour
{
    private Button button;
    private void Awake() { button = GetComponent<Button>(); button.onClick.AddListener(Click); }
    private void Click() { FortressAudio.Play(FortressSound.Click); }
    private void OnDestroy() { if (button) button.onClick.RemoveListener(Click); }
}
