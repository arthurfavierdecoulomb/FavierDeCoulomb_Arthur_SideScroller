using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class BossDialogueManager : MonoBehaviour
{
    public static BossDialogueManager Instance { get; private set; }

    public enum Speaker { Azu, OxiO }

    [System.Serializable]
    public class DialogueLine
    {
        public Speaker speaker;
        public Sprite azuExpression;
        [TextArea(2, 5)] public string text;
        public AudioClip voice;
        public bool matchTypewriterToVoice = true;
        public UnityEvent onLineStart;
        public UnityEvent onLineEnd;
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string label;
        public UnityEvent onSelected;
    }

    [System.Serializable]
    public class DialogueSequence
    {
        public string id;
        public List<DialogueLine> lines = new List<DialogueLine>();
        public bool endsWithChoice;
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public UnityEvent onSequenceEnd;
    }

    [Header("Séquences")]
    [SerializeField] List<DialogueSequence> sequences = new List<DialogueSequence>();

    [Header("UI")]
    [SerializeField] GameObject dialoguePanel;
    [SerializeField] GameObject dialogueBackground;
    [SerializeField] TextMeshProUGUI speakerNameText;
    [SerializeField] TextMeshProUGUI dialogueText;
    [SerializeField] GameObject azuPortraitRoot;
    [SerializeField] Image azuPortrait;

    [Header("Invite de continuation")]
    [SerializeField] GameObject continueHint;
    [SerializeField] RectTransform continueHintRect;
    [SerializeField] float hintPopDuration = 0.22f;
    [SerializeField] float hintPopOvershoot = 1.35f;
    [SerializeField] bool hintPulse = true;
    [SerializeField] float hintPulseScale = 1.12f;
    [SerializeField] float hintPulseDuration = 0.9f;
    [SerializeField] TextMeshProUGUI continueHintLabel;
    [SerializeField] string continueHintFormat = "[ {0} ] pour continuer";

    [Header("Choix")]
    [SerializeField] GameObject choicePanel;
    [SerializeField] Button[] choiceButtons;
    [SerializeField] TextMeshProUGUI[] choiceLabels;

    [Header("Noms affichés")]
    [SerializeField] string azuName = "Azu";
    [SerializeField] string oxiName = "Oxi-O";

    [Header("Références")]
    [SerializeField] OxiOAnimation oxiAnimation;
    [SerializeField] MonoBehaviour playerController;

    [Header("Blocage du joueur")]
    [SerializeField] bool ignoreInputWhilePaused = true;
    [SerializeField] string playerTag = "Player";
    [SerializeField] Rigidbody2D playerBody;
    [SerializeField] bool freezePlayerBody = true;
    [SerializeField] bool keepGravityWhileFrozen = true;

    [Header("Doublage")]
    [SerializeField] AudioSource voiceSource;
    [SerializeField] bool stopVoiceOnAdvance = true;
    [Range(0f, 1f)]
    [SerializeField] float voiceVolume = 1f;

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.035f;
    [SerializeField] float minTypewriterDelay = 0.012f;
    [SerializeField] KeyCode advanceKey = KeyCode.E;

    [Header("Animation du panneau")]
    [SerializeField] float hideOffsetY = -400f;
    [SerializeField] float slideInDuration = 0.45f;
    [SerializeField] float slideOutDuration = 0.25f;
    [SerializeField] float bounceAmplitude = 40f;
    [Range(1, 4)]
    [SerializeField] int bounceCount = 2;
    [Range(0.1f, 0.9f)]
    [SerializeField] float bounceDamping = 0.45f;

    public bool IsPlaying => isPlaying;
    public event System.Action<string> OnSequenceFinished;

    RectTransform panelRect;
    CanvasGroup canvasGroup;
    Vector2 shownPosition;
    Vector2 hiddenPosition;
    Vector3 hintBaseScale = Vector3.one;
    Coroutine hintRoutine;
    bool isPlaying;
    bool skipRequested;
    Coroutine freezeRoutine;
    int chosenIndex = -1;

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
        SetInteractable(false);

        if (continueHintRect == null && continueHint != null)
            continueHintRect = continueHint.GetComponent<RectTransform>();

        if (continueHintRect != null)
            hintBaseScale = continueHintRect.localScale;

        if (dialogueBackground != null) dialogueBackground.SetActive(false);
        dialoguePanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (continueHint != null) continueHint.SetActive(false);
        if (speakerNameText != null) speakerNameText.text = "";
        if (dialogueText != null) dialogueText.text = "";
        if (azuPortraitRoot != null) azuPortraitRoot.SetActive(false);

        if (continueHintLabel != null)
            continueHintLabel.text = string.Format(continueHintFormat, advanceKey);

        ResolvePlayerReferences();
    }

    void ResolvePlayerReferences()
    {
        GameObject found = null;

        if (playerBody == null || playerController == null)
            found = GameObject.FindGameObjectWithTag(playerTag);

        if (playerBody == null && found != null)
            playerBody = found.GetComponentInChildren<Rigidbody2D>();

        if (playerController == null)
            Debug.LogError($"[BossDialogueManager] '{name}' : le champ Player Controller est vide, Azu pourra se déplacer pendant les dialogues. Assigne son CharaController.", this);

        if (playerBody == null && freezePlayerBody)
            Debug.LogWarning($"[BossDialogueManager] '{name}' : aucun Rigidbody2D trouvé sur l'objet taggé '{playerTag}', Azu gardera son élan pendant les dialogues.", this);
    }

    void SetPlayerFrozen(bool frozen)
    {
        if (playerController != null)
            playerController.enabled = !frozen;

        if (!freezePlayerBody || playerBody == null)
            return;

        if (frozen)
        {
            if (freezeRoutine != null)
                StopCoroutine(freezeRoutine);

            freezeRoutine = StartCoroutine(FreezeBodyRoutine());
        }
        else if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            freezeRoutine = null;
        }
    }

    IEnumerator FreezeBodyRoutine()
    {
        while (true)
        {
            if (playerBody != null)
                playerBody.linearVelocity = keepGravityWhileFrozen
                    ? new Vector2(0f, Mathf.Min(0f, playerBody.linearVelocity.y))
                    : Vector2.zero;

            yield return new WaitForFixedUpdate();
        }
    }

    public void PlaySequence(string id)
    {
        if (isPlaying)
        {
            Debug.LogWarning($"[BossDialogueManager] Séquence '{id}' ignorée : un dialogue est déjà en cours.");
            return;
        }

        DialogueSequence sequence = sequences.Find(s => s.id == id);

        if (sequence == null)
        {
            Debug.LogWarning($"[BossDialogueManager] Aucune séquence avec l'id '{id}'.");
            return;
        }

        StartCoroutine(SequenceRoutine(sequence));
    }

    IEnumerator SequenceRoutine(DialogueSequence sequence)
    {
        isPlaying = true;

        SetPlayerFrozen(true);

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.Suppress(true);

        yield return StartCoroutine(SlideInRoutine());

        foreach (DialogueLine line in sequence.lines)
        {
            line.onLineStart?.Invoke();
            ApplySpeaker(line);

            HideContinueHint();

            PlayVoice(line);

            yield return StartCoroutine(TypeLineRoutine(line.text, DelayForLine(line)));

            if (line.speaker == Speaker.OxiO && oxiAnimation != null)
                oxiAnimation.StopTalking();

            ShowContinueHint();

            yield return StartCoroutine(WaitForAdvanceRoutine());

            if (stopVoiceOnAdvance && voiceSource != null)
                voiceSource.Stop();

            line.onLineEnd?.Invoke();
        }

        HideContinueHint();

        if (sequence.endsWithChoice && sequence.choices.Count > 0)
            yield return StartCoroutine(ChoiceRoutine(sequence));

        yield return StartCoroutine(SlideOutRoutine());

        SetPlayerFrozen(false);

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.Suppress(false);

        isPlaying = false;
        sequence.onSequenceEnd?.Invoke();
        OnSequenceFinished?.Invoke(sequence.id);
    }

    void ShowContinueHint()
    {
        if (continueHint == null)
            return;

        StopHintRoutine();

        continueHint.SetActive(true);
        hintRoutine = StartCoroutine(HintRoutine());
    }

    void HideContinueHint()
    {
        StopHintRoutine();

        if (continueHintRect != null)
            continueHintRect.localScale = hintBaseScale;

        if (continueHint != null)
            continueHint.SetActive(false);
    }

    void StopHintRoutine()
    {
        if (hintRoutine == null)
            return;

        StopCoroutine(hintRoutine);
        hintRoutine = null;
    }

    IEnumerator HintRoutine()
    {
        if (continueHintRect == null)
            yield break;

        float elapsed = 0f;
        float pop = Mathf.Max(0.01f, hintPopDuration);

        while (elapsed < pop)
        {
            float t = elapsed / pop;
            float scale = EaseOutBack(t, hintPopOvershoot);

            continueHintRect.localScale = hintBaseScale * scale;

            elapsed += Time.deltaTime;
            yield return null;
        }

        continueHintRect.localScale = hintBaseScale;

        if (!hintPulse)
            yield break;

        float time = 0f;
        float period = Mathf.Max(0.05f, hintPulseDuration);

        while (true)
        {
            time += Time.deltaTime;

            float wave = (Mathf.Sin(time * Mathf.PI * 2f / period) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, hintPulseScale, wave);

            continueHintRect.localScale = hintBaseScale * scale;

            yield return null;
        }
    }

    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * (p * p * p) + c1 * (p * p);
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

        if (oxiAnimation != null)
        {
            if (isAzu) oxiAnimation.StopTalking();
            else oxiAnimation.StartTalking();
        }
    }

    void PlayVoice(DialogueLine line)
    {
        if (line.voice == null) return;

        if (voiceSource == null)
        {
            Debug.LogWarning($"[BossDialogueManager] Le doublage '{line.voice.name}' ne peut pas être joué : le champ voiceSource est vide.");
            return;
        }

        if (!voiceSource.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[BossDialogueManager] Le doublage '{line.voice.name}' ne peut pas être joué : l'objet portant l'AudioSource est désactivé.");
            return;
        }

        voiceSource.Stop();
        voiceSource.clip = line.voice;
        voiceSource.volume = voiceVolume;
        voiceSource.Play();
    }

    float DelayForLine(DialogueLine line)
    {
        if (!line.matchTypewriterToVoice) return typewriterDelay;
        if (line.voice == null) return typewriterDelay;
        if (string.IsNullOrEmpty(line.text)) return typewriterDelay;

        float computed = line.voice.length / line.text.Length;
        return Mathf.Max(minTypewriterDelay, computed);
    }

    IEnumerator TypeLineRoutine(string text, float delay)
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

            while (elapsed < delay)
            {
                if (AdvancePressed()) skipRequested = true;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }

    bool AdvancePressed()
    {
        if (ignoreInputWhilePaused && Time.timeScale == 0f)
            return false;

        return Input.GetKeyDown(advanceKey);
    }

    IEnumerator WaitForAdvanceRoutine()
    {
        yield return null;

        while (!AdvancePressed())
            yield return null;

        yield return null;
    }

    IEnumerator ChoiceRoutine(DialogueSequence sequence)
    {
        chosenIndex = -1;

        if (choicePanel != null) choicePanel.SetActive(true);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool used = i < sequence.choices.Count;

            if (choiceButtons[i] == null) continue;

            choiceButtons[i].gameObject.SetActive(used);

            if (!used) continue;

            if (i < choiceLabels.Length && choiceLabels[i] != null)
                choiceLabels[i].text = sequence.choices[i].label;

            int index = i;
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].onClick.AddListener(() => chosenIndex = index);
        }

        SetInteractable(true);

        while (chosenIndex < 0)
            yield return null;

        if (choicePanel != null) choicePanel.SetActive(false);

        sequence.choices[chosenIndex].onSelected?.Invoke();
    }

    void SetInteractable(bool interactable)
    {
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    IEnumerator SlideInRoutine()
    {
        dialoguePanel.SetActive(true);
        if (dialogueBackground != null) dialogueBackground.SetActive(true);

        canvasGroup.alpha = 1f;

        float elapsed = 0f;
        Vector2 startPos = hiddenPosition;
        panelRect.anchoredPosition = startPos;

        while (elapsed < slideInDuration)
        {
            float t = elapsed / slideInDuration;
            Vector2 basePos = Vector2.Lerp(startPos, shownPosition, t);
            float dampingCurve = Mathf.Pow(1f - t, 1f - bounceDamping);
            float oscillation = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);

            panelRect.anchoredPosition = basePos + Vector2.up * (oscillation * dampingCurve * bounceAmplitude);

            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = shownPosition;
    }

    IEnumerator SlideOutRoutine()
    {
        SetInteractable(false);

        float elapsed = 0f;
        Vector2 startPos = panelRect.anchoredPosition;

        while (elapsed < slideOutDuration)
        {
            float t = elapsed / slideOutDuration;
            panelRect.anchoredPosition = Vector2.Lerp(startPos, hiddenPosition, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = hiddenPosition;
        canvasGroup.alpha = 0f;

        if (dialogueText != null) dialogueText.text = "";
        if (speakerNameText != null) speakerNameText.text = "";
        if (azuPortraitRoot != null) azuPortraitRoot.SetActive(false);

        HideContinueHint();

        if (dialogueBackground != null) dialogueBackground.SetActive(false);
        dialoguePanel.SetActive(false);
    }
}