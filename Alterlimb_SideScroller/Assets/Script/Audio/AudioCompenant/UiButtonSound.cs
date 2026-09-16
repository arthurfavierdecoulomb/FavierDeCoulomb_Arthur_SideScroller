using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UiButtonSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    [Header("Type de bouton")]
    [SerializeField] UiSoundKind kind = UiSoundKind.Click;

    [Header("Survol")]
    [SerializeField] bool playOnHover = true;
    [SerializeField] bool playOnSelect = true;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(PlayClick);
    }

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(PlayClick);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playOnHover) return;
        if (button == null || !button.interactable) return;

        UiAudio.Play(UiSoundKind.Hover);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!playOnSelect) return;
        if (button == null || !button.interactable) return;

        UiAudio.Play(UiSoundKind.Hover);
    }

    void PlayClick()
    {
        UiAudio.Play(kind);
    }
}
