using UnityEngine;

public class TutorialDoorNotifier : MonoBehaviour
{
    [SerializeField] Door door;

    void Awake()
    {
        if (door == null) door = GetComponent<Door>();

        if (door == null)
            Debug.LogError($"{name}: Il y a pas de porte assigné");
    }

    void OnEnable()
    {
        if (door != null) door.OnOpened += HandleDoorOpened;
    }

    void OnDisable()
    {
        if (door != null) door.OnOpened -= HandleDoorOpened;
    }

    void HandleDoorOpened()
    {
        if (OxiDialogueManager.Instance != null)
            OxiDialogueManager.Instance.NotifyExternalActionReady();
    }
}