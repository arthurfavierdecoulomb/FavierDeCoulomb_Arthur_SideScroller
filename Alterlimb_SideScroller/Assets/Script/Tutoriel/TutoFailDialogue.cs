using UnityEngine;

public class TutoFailDialogue: MonoBehaviour
{
    [Header("Détection")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool autoFindCharaController = true;
    [SerializeField] CharaController charaController;

    void Awake()
    {
        if (autoFindCharaController && charaController == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null) charaController = found.GetComponentInChildren<CharaController>();
        }

        if (charaController == null)
            Debug.LogError($"{name}: pas de characontroller assigné");
    }

    void OnEnable()
    {
        if (charaController != null)
            charaController.OnSafeRespawn += HandleSafeRespawn;
    }

    void OnDisable()
    {
        if (charaController != null)
            charaController.OnSafeRespawn -= HandleSafeRespawn;
    }

    void HandleSafeRespawn(string failDialogueId)
    {
        if (string.IsNullOrEmpty(failDialogueId)) return;
        if (OxiDialogueManager.Instance == null) return;
        if (OxiDialogueManager.Instance.IsPlaying) return;

        OxiDialogueManager.Instance.PlaySequence(failDialogueId);
    }
}