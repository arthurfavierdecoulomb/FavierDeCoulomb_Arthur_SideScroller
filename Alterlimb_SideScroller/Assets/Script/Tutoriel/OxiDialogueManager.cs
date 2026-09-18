using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OxiDialogueManager : MonoBehaviour
{
    public static OxiDialogueManager Instance { get; private set; }

    public enum Speaker { Azu, OxiO }

    [System.Serializable]
    public class DialogueLine
    {
        public enum RequiredAction { None, Move, Jump }

        public Speaker speaker;
        public Sprite azuExpression;
        public Sprite oxiExpression;
        [TextArea(2, 5)] public string text;
        public AudioClip voice;
        public RequiredAction requiredAction = RequiredAction.None;
    }

    [System.Serializable]
    public class DialogueSequence
    {
        public string id;
        public List<DialogueLine> lines = new List<DialogueLine>();
        public UnityEvent onSequenceEnd;
    }

    [Header("Séquences")]
    [SerializeField] List<DialogueSequence> sequences = new List<DialogueSequence>();

    [Header("UI")]
    [SerializeField] GameObject dialoguePanel;
    [SerializeField] TextMeshProUGUI speakerNameText;
    [SerializeField] TextMeshProUGUI dialogueText;
    [SerializeField] GameObject azuPortraitRoot;
    [SerializeField] Image azuPortrait;
    [SerializeField] GameObject oxiPortraitRoot;
    [SerializeField] Image oxiPortrait;

    [Header("Invite de continuation")]
    [SerializeField] GameObject continueHint;
    [SerializeField] TextMeshProUGUI continueHintLabel;
    [SerializeField] string continueHintFormat = "[ {0} ] pour continuer";

    [Header("Noms affichés")]
    [SerializeField] string azuName = "Azu";
    [SerializeField] string oxiName = "Oxi-O";

    [Header("Références")]
    [SerializeField] OxiO_Animation oxiAnimation;

    [Header("Blocage du joueur")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool autoFindCharaController = true;
    [SerializeField] CharaController charaController;

    [Header("Doublage")]
    [SerializeField] AudioSource voiceSource;
    [Range(0f, 1f)]
    [SerializeField] float voiceVolume = 1f;

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.035f;
    [SerializeField] KeyCode advanceKey = KeyCode.Return;

    [Header("Animation du panneau")]
    [SerializeField] float hideOffsetY = -400f;
    [SerializeField] float slideInDuration = 0.35f;
    [SerializeField] float slideOutDuration = 0.2f;

    public bool IsPlaying => isPlaying;
    public event System.Action<string> OnSequenceFinished;

    RectTransform panelRect;
    CanvasGroup canvasGroup;
    Vector2 shownPosition;
    Vector2 hiddenPosition;
    bool isPlaying;
    bool skipRequested;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        panelRect = dialoguePanel.GetComponent<RectTransform>();
        canvasGroup = dialoguePanel.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = dialoguePanel.AddComponent<CanvasGroup>();

        shownPosition = panelRect.anchoredPosition;
        hiddenPosition = shownPosition + Vector2.up * hideOffsetY;
        panelRect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        dialoguePanel.SetActive(false);
        if (continueHint != null) continueHint.SetActive(false);
        if (speakerNameText != null) speakerNameText.text = "";
        if (dialogueText != null) dialogueText.text = "";
        if (azuPortraitRoot != null) azuPortraitRoot.SetActive(false);
        if (oxiPortraitRoot != null) oxiPortraitRoot.SetActive(false);

        if (continueHintLabel != null)
            continueHintLabel.text = string.Format(continueHintFormat, advanceKey);

        if (autoFindCharaController && charaController == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null) charaController = found.GetComponentInChildren<CharaController>();
        }

        if (charaController == null)
            Debug.LogError($"{name}: OxiDialogueManager has no CharaController assigned");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlaySequence(string id)
    {
        DialogueSequence sequence = FindSequence(id);
        if (sequence == null)
        {
            Debug.LogError($"{name}: no dialogue sequence found for id '{id}'");
            return;
        }

        StartCoroutine(PlaySequenceRoutine(sequence));
    }

    DialogueSequence FindSequence(string id)
    {
        foreach (DialogueSequence sequence in sequences)
            if (sequence.id == id)
                return sequence;

        return null;
    }

    IEnumerator PlaySequenceRoutine(DialogueSequence sequence)
    {
        isPlaying = true;
        SetPlayerFrozen(true);

        yield return StartCoroutine(SlideInRoutine());

        foreach (DialogueLine line in sequence.lines)
        {
            ApplySpeaker(line);
            PlayVoice(line);

            yield return StartCoroutine(TypeLineRoutine(line.text));

            if (line.requiredAction != DialogueLine.RequiredAction.None)
            {
                yield return StartCoroutine(WaitForActionRoutine(line.requiredAction));
            }
            else
            {
                ShowContinueHint();
                yield return StartCoroutine(WaitForAdvanceRoutine());
                HideContinueHint();
            }
        }

        yield return StartCoroutine(SlideOutRoutine());

        SetPlayerFrozen(false);
        isPlaying = false;

        sequence.onSequenceEnd?.Invoke();
        OnSequenceFinished?.Invoke(sequence.id);
    }

    void SetPlayerFrozen(bool frozen)
    {
        if (charaController != null)
            charaController.SetControlLocked(frozen);
    }

    void ApplySpeaker(DialogueLine line)
    {
        bool isAzu = line.speaker == Speaker.Azu;

        if (speakerNameText != null)
            speakerNameText.text = isAzu ? azuName : oxiName;

        if (azuPortraitRoot != null)
            azuPortraitRoot.SetActive(isAzu && line.azuExpression != null);

        if (isAzu && azuPortrait != null && line.azuExpression != null)
            azuPortrait.sprite = line.azuExpression;

        if (oxiPortraitRoot != null)
            oxiPortraitRoot.SetActive(!isAzu && line.oxiExpression != null);

        if (!isAzu && oxiPortrait != null && line.oxiExpression != null)
            oxiPortrait.sprite = line.oxiExpression;

        if (oxiAnimation != null)
        {
            if (isAzu) oxiAnimation.StopTalking();
            else oxiAnimation.StartTalking();
        }
    }

    void PlayVoice(DialogueLine line)
    {
        if (line.voice == null || voiceSource == null) return;

        voiceSource.Stop();
        voiceSource.clip = line.voice;
        voiceSource.volume = voiceVolume;
        voiceSource.Play();
    }

    IEnumerator TypeLineRoutine(string text)
    {
        if (dialogueText == null) yield break;

        dialogueText.text = "";
        skipRequested = false;

        foreach (char c in text)
        {
            if (skipRequested)
            {
                dialogueText.text = text;
                yield break;
            }

            dialogueText.text += c;

            float elapsed = 0f;
            while (elapsed < typewriterDelay)
            {
                if (AdvancePressed())
                {
                    skipRequested = true;
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    IEnumerator WaitForActionRoutine(DialogueLine.RequiredAction action)
    {
        if (charaController != null)
            charaController.SetControlLocked(false);

        bool movedLeft = false;
        bool movedRight = false;

        while (true)
        {
            if (action == DialogueLine.RequiredAction.Move)
            {
                float h = Input.GetAxisRaw("Horizontal");
                if (h < -0.1f) movedLeft = true;
                if (h > 0.1f) movedRight = true;

                if (movedLeft && movedRight) break;
            }
            else if (action == DialogueLine.RequiredAction.Jump)
            {
                if (Input.GetButtonDown("Jump")) break;
            }

            yield return null;
        }
    }

    bool AdvancePressed()
    {
        return Input.GetKeyDown(advanceKey);
    }

    IEnumerator WaitForAdvanceRoutine()
    {
        yield return null;

        while (!AdvancePressed())
            yield return null;

        yield return null;
    }

    void ShowContinueHint()
    {
        if (continueHint != null) continueHint.SetActive(true);
    }

    void HideContinueHint()
    {
        if (continueHint != null) continueHint.SetActive(false);
    }

    IEnumerator SlideInRoutine()
    {
        dialoguePanel.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            float t = elapsed / slideInDuration;
            panelRect.anchoredPosition = Vector2.Lerp(hiddenPosition, shownPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = shownPosition;
    }

    IEnumerator SlideOutRoutine()
    {
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        Vector2 startPos = panelRect.anchoredPosition;

        while (elapsed < slideOutDuration)
        {
            float t = elapsed / slideOutDuration;
            panelRect.anchoredPosition = Vector2.Lerp(startPos, hiddenPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;

        if (dialogueText != null) dialogueText.text = "";
        if (speakerNameText != null) speakerNameText.text = "";
        if (azuPortraitRoot != null) azuPortraitRoot.SetActive(false);
        if (oxiPortraitRoot != null) oxiPortraitRoot.SetActive(false);

        dialoguePanel.SetActive(false);
    }
}