using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

public class OxiDialogueManager : MonoBehaviour
{
    public static OxiDialogueManager Instance { get; private set; }

    public enum Speaker { Azu, OxiO }

    [System.Serializable]
    public class DialogueLine
    {
        public enum RequiredAction { None, Move, Jump, External }

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
    [SerializeField] RectTransform continueHintRect;
    [SerializeField] float hintPopDuration = 0.22f;
    [SerializeField] float hintPopOvershoot = 1.35f;
    [SerializeField] bool hintPulse = true;
    [SerializeField] float hintPulseScale = 1.12f;
    [SerializeField] float hintPulseDuration = 0.9f;
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

    [Header("Sortie audio des bruitages")]
    [SerializeField] AudioMixerGroup sfxOutput;

    [Header("Bruitages — machine à écrire")]
    [SerializeField] AudioClip[] typewriterClips;
    [SerializeField] bool skipTypewriterWhenVoiced = true;
    [SerializeField] float typewriterMinInterval = 0.05f;
    [Range(0f, 1f)]
    [SerializeField] float typewriterVolume = 0.45f;
    [SerializeField] Vector2 typewriterPitchRange = new Vector2(0.94f, 1.06f);

    [Header("Bruitages — panneau")]
    [SerializeField] AudioClip[] panelInClips;
    [SerializeField] AudioClip[] panelOutClips;
    [Range(0f, 1f)]
    [SerializeField] float panelVolume = 0.8f;

    [Header("Bruitages — validation")]
    [SerializeField] AudioClip[] advanceClips;
    [Range(0f, 1f)]
    [SerializeField] float advanceVolume = 0.7f;

    [Header("Machine à écrire")]
    [SerializeField] float typewriterDelay = 0.035f;
    [SerializeField] bool matchTypewriterToVoice = true;
    [SerializeField] KeyCode advanceKey = KeyCode.Return;

    [Header("Animation du panneau")]
    [SerializeField] float hideOffsetY = -400f;
    [SerializeField] float slideInDuration = 0.35f;
    [SerializeField] float slideOutDuration = 0.2f;
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
    bool externalActionReady;

    AudioSource sfxSource;
    AudioClip lastSfxClip;
    float lastTypewriterTime = -999f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.outputAudioMixerGroup = sfxOutput;

        if (sfxOutput == null)
            Debug.LogWarning($"{name}: OxiDialogueManager has no sfxOutput assigned — typewriter/panel sounds will play on Master instead of Sfx");

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

        if (continueHintRect == null && continueHint != null)
            continueHintRect = continueHint.GetComponent<RectTransform>();

        if (continueHintRect != null)
            hintBaseScale = continueHintRect.localScale;

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
        if (isPlaying)
        {
            Debug.LogWarning($"{name}: PlaySequence('{id}') ignorée — une séquence est déjà en cours");
            return;
        }

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

            yield return StartCoroutine(TypeLineRoutine(line));

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

    bool ShouldClick(DialogueLine line)
    {
        if (typewriterClips == null || typewriterClips.Length == 0) return false;
        if (skipTypewriterWhenVoiced && line.voice != null) return false;

        return true;
    }

    void PlayTypewriterClick()
    {
        if (Time.unscaledTime - lastTypewriterTime < typewriterMinInterval) return;
        lastTypewriterTime = Time.unscaledTime;

        PlaySfx(typewriterClips, typewriterVolume, Random.Range(typewriterPitchRange.x, typewriterPitchRange.y));
    }

    void PlaySfx(AudioClip[] clips, float volume, float pitch)
    {
        if (sfxSource == null) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastSfxClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastSfxClip = clip;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    IEnumerator TypeLineRoutine(DialogueLine line)
    {
        if (dialogueText == null) yield break;

        dialogueText.text = "";
        skipRequested = false;
        bool playClicks = ShouldClick(line);

        float charDelay = typewriterDelay;
        if (matchTypewriterToVoice && line.voice != null && line.text.Length > 0)
            charDelay = line.voice.length / line.text.Length;

        foreach (char c in line.text)
        {
            if (skipRequested)
            {
                dialogueText.text = line.text;
                yield break;
            }

            dialogueText.text += c;

            if (playClicks && !char.IsWhiteSpace(c))
                PlayTypewriterClick();

            float elapsed = 0f;
            while (elapsed < charDelay)
            {
                if (AdvancePressed())
                {
                    skipRequested = true;
                    PlaySfx(advanceClips, advanceVolume, 1f);
                    voiceSource?.Stop();
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
        {
            charaController.SetControlLocked(false);

            switch (action)
            {
                case DialogueLine.RequiredAction.Move:
                    charaController.SetMoveEnabled(true);
                    break;
                case DialogueLine.RequiredAction.Jump:
                    charaController.SetJumpEnabled(true);
                    break;
                case DialogueLine.RequiredAction.External:
                    charaController.SetInteractEnabled(true);
                    break;
            }
        }

        bool movedLeft = false;
        bool movedRight = false;
        externalActionReady = false;

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
            else if (action == DialogueLine.RequiredAction.External)
            {
                if (externalActionReady) break;
            }

            yield return null;
        }
    }

    public void NotifyExternalActionReady()
    {
        externalActionReady = true;
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

        PlaySfx(advanceClips, advanceVolume, 1f);

        yield return null;
    }

    void ShowContinueHint()
    {
        if (continueHint == null) return;

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
        if (hintRoutine == null) return;

        StopCoroutine(hintRoutine);
        hintRoutine = null;
    }

    IEnumerator HintRoutine()
    {
        if (continueHintRect == null) yield break;

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

        if (!hintPulse) yield break;

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

    IEnumerator SlideInRoutine()
    {
        dialoguePanel.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        PlaySfx(panelInClips, panelVolume, 1f);

        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            float t = elapsed / slideInDuration;
            Vector2 basePos = Vector2.Lerp(hiddenPosition, shownPosition, t);
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
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        PlaySfx(panelOutClips, panelVolume, 1f);

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