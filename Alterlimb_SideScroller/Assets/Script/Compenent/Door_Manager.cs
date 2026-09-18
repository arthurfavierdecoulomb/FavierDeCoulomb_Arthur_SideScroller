using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Door : MonoBehaviour
{
    public enum OpeningMode { Proximity, Levers, OnDroneKilled, Fuses }

    [Header("Mode d'ouverture")]
    [SerializeField] OpeningMode mode = OpeningMode.Proximity;

    [Header("Mode Proximité")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1;

    [Header("Mode Leviers")]
    [SerializeField] List<Lever> requiredLevers = new List<Lever>();

    [Header("Mode OnDroneKilled")]
    [SerializeField] DroneEnemy targetDrone;

    [Header("Comportement commun")]
    [SerializeField] bool stayOpenForever = false;
    [SerializeField] float autoCloseDelay = 3f;
    [SerializeField] string playerTag = "Player";

    [Header("Message si verrouillée")]
    [SerializeField] string lockedMessageId = "";
    [SerializeField] float lockedMessageRange = 2.5f;

    [Header("Références")]
    [SerializeField] Animator animator;
    [SerializeField] Transform playerTransform;

    [Header("Mode Fusibles")]
    [SerializeField] bool useFuseManager = true;

    [Header("Collisions physiques")]
    [SerializeField] Collider2D solidCollider;
    [SerializeField] float openColliderDelay = 0.2f;
    [SerializeField] float closeColliderDelay = 0.4f;

    [Header("Audio")]
    [SerializeField] SfxEmitter sfx;
    [SerializeField] AudioClip[] unlockClips;
    [SerializeField] AudioClip[] openClips;
    [SerializeField] AudioClip[] closeClips;
    [SerializeField] AudioClip[] lockedClips;
    [SerializeField] bool soundsFromAnimationEvents = false;
    [SerializeField] float unlockToOpenDelay = 0.35f;
    [SerializeField] float closeSoundDelay = 0f;
    [SerializeField] float lockedSoundCooldown = 0.6f;

    bool isOpen;
    bool playerWasInside;
    float closeTimer;
    bool autoCloseScheduled;
    float lockedSoundTimer;

    public event System.Action OnOpened;

    static readonly int OpenTrigger = Animator.StringToHash("Open");
    static readonly int CloseTrigger = Animator.StringToHash("Close");

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) playerTransform = p.transform;
        }

        if (solidCollider != null)
            solidCollider.enabled = true;
        else
            Debug.LogWarning($"[Door] '{name}' n'a pas de Solid Collider assigné. Le joueur pourra passer à travers.", this);

        if (sfx == null) sfx = GetComponent<SfxEmitter>();

        if (sfx == null && HasAnyClip())
            Debug.LogError($"[Door] '{name}' a des clips assignés mais aucun SfxEmitter : aucun son ne sera joué.", this);

        switch (mode)
        {
            case OpeningMode.Levers:
                foreach (Lever lever in requiredLevers)
                {
                    if (lever != null)
                        lever.OnLeverActivated += OnLeverStateChanged;
                }
                break;

            case OpeningMode.OnDroneKilled:
                if (targetDrone == null)
                    Debug.LogWarning($"[Door] '{name}' est en mode OnDroneKilled mais aucun targetDrone n'est assigné.", this);
                DroneEnemy.OnDroneDied += OnDroneDiedHandler;
                break;

            case OpeningMode.Fuses:
                FuseManager.OnAllFusesInstalledStatic += OnAllFusesHandler;
                break;
        }
    }

    void OnDestroy()
    {
        foreach (Lever lever in requiredLevers)
        {
            if (lever != null)
                lever.OnLeverActivated -= OnLeverStateChanged;
        }

        DroneEnemy.OnDroneDied -= OnDroneDiedHandler;
        FuseManager.OnAllFusesInstalledStatic -= OnAllFusesHandler;
    }

    void Update()
    {
        if (lockedSoundTimer > 0f)
            lockedSoundTimer -= Time.deltaTime;

        if (mode == OpeningMode.Proximity)
            HandleProximityMode();
        else
            HandleLockedInteraction();

        HandleAutoClose();
    }

    void HandleProximityMode()
    {
        if (playerTransform == null || isOpen) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            OpenDoor();
        }
    }

    void HandleLockedInteraction()
    {
        if (isOpen) return;
        if (playerTransform == null) return;
        if (!Input.GetKeyDown(interactionKey)) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance > lockedMessageRange) return;

        if (lockedSoundTimer <= 0f)
        {
            PlayLockedSound();
            lockedSoundTimer = lockedSoundCooldown;
        }

        if (string.IsNullOrEmpty(lockedMessageId)) return;

        if (TutorialManager.Instance != null)
            TutorialManager.Instance.ShowMessageById(lockedMessageId);
    }

    void HandleAutoClose()
    {
        if (!isOpen || !autoCloseScheduled) return;

        closeTimer -= Time.deltaTime;
        if (closeTimer <= 0f)
        {
            CloseDoor();
            autoCloseScheduled = false;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag(playerTag)) return;

        playerWasInside = true;
        autoCloseScheduled = false;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!isOpen) return;
        if (!other.CompareTag(playerTag)) return;

        if (playerWasInside)
        {
            playerWasInside = false;

            if (stayOpenForever) return;

            closeTimer = autoCloseDelay;
            autoCloseScheduled = true;
        }
    }

    void OnLeverStateChanged()
    {
        if (mode != OpeningMode.Levers) return;

        bool allActivated = true;
        foreach (Lever lever in requiredLevers)
        {
            if (lever == null || !lever.IsActivated)
            {
                allActivated = false;
                break;
            }
        }

        if (allActivated && !isOpen)
            OpenDoor();
    }

    void OnDroneDiedHandler(DroneEnemy deadDrone)
    {
        if (mode != OpeningMode.OnDroneKilled) return;
        if (deadDrone != targetDrone) return;
        if (isOpen) return;

        OpenDoor();
    }

    void OnAllFusesHandler()
    {
        if (mode != OpeningMode.Fuses) return;
        if (isOpen) return;

        OpenDoor();
    }

    public void OpenDoor()
    {
        if (isOpen) return;
        isOpen = true;

        bool wasLocked = mode != OpeningMode.Proximity;
        if (wasLocked) PlayUnlockSound();

        if (animator != null) animator.SetTrigger(OpenTrigger);

        if (!soundsFromAnimationEvents)
        {
            float delay = wasLocked ? unlockToOpenDelay : 0f;
            if (delay > 0f)
                StartCoroutine(PlayAfterDelay(openClips, delay));
            else
                PlayOpenSound();
        }

        StartCoroutine(SetColliderAfterDelay(false, openColliderDelay, () => OnOpened?.Invoke()));
    }

    public void CloseDoor()
    {
        if (!isOpen) return;
        isOpen = false;

        if (animator != null) animator.SetTrigger(CloseTrigger);

        if (!soundsFromAnimationEvents)
        {
            if (closeSoundDelay > 0f)
                StartCoroutine(PlayAfterDelay(closeClips, closeSoundDelay));
            else
                PlayCloseSound();
        }

        StartCoroutine(SetColliderAfterDelay(true, closeColliderDelay));
    }

    public void PlayUnlockSound()
    {
        if (sfx != null) sfx.Play(unlockClips);
    }

    public void PlayOpenSound()
    {
        if (sfx != null) sfx.Play(openClips);
    }

    public void PlayCloseSound()
    {
        if (sfx != null) sfx.Play(closeClips);
    }

    public void PlayLockedSound()
    {
        if (sfx != null) sfx.Play(lockedClips);
    }

    IEnumerator PlayAfterDelay(AudioClip[] clips, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sfx != null) sfx.Play(clips);
    }

    IEnumerator SetColliderAfterDelay(bool colliderEnabled, float delay, System.Action onComplete = null)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (solidCollider != null)
            solidCollider.enabled = colliderEnabled;

        onComplete?.Invoke();
    }

    bool HasAnyClip()
    {
        return (unlockClips != null && unlockClips.Length > 0)
            || (openClips != null && openClips.Length > 0)
            || (closeClips != null && closeClips.Length > 0)
            || (lockedClips != null && lockedClips.Length > 0);
    }

    public bool IsLeverRequired(Lever lever)
    {
        return mode == OpeningMode.Levers && requiredLevers.Contains(lever);
    }

    void OnDrawGizmosSelected()
    {
        switch (mode)
        {
            case OpeningMode.Proximity:
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, interactionRange);
                break;

            case OpeningMode.Levers:
                Gizmos.color = Color.yellow;
                foreach (Lever lever in requiredLevers)
                {
                    if (lever != null)
                        Gizmos.DrawLine(transform.position, lever.transform.position);
                }
                break;

            case OpeningMode.OnDroneKilled:
                if (targetDrone != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(transform.position, targetDrone.transform.position);
                    Gizmos.DrawWireSphere(targetDrone.transform.position, 0.5f);
                }
                break;
        }

        if (!string.IsNullOrEmpty(lockedMessageId))
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, lockedMessageRange);
        }
    }
}