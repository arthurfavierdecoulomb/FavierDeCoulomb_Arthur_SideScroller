using UnityEngine;
using System;
using System.Collections;

public class Lever : MonoBehaviour
{
    [Header("Référence")]
    [SerializeField] Transform handle;

    [Header("Rotation")]
    [SerializeField] float restAngle = -49f;
    [SerializeField] float activeAngle = 49f;
    [SerializeField] float fakeAngle = 0f;

    [Header("Durées d'animation")]
    [SerializeField] float activationDuration = 0.6f;
    [SerializeField] float fakeDownDuration = 0.3f;
    [SerializeField] float fakeUpDuration = 0.5f;

    [Header("Bounce")]
    [Range(0f, 3f)]
    [SerializeField] float bounceStrength = 1.7f;

    [Header("Interaction joueur")]
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] KeyCode interactionKey = KeyCode.Mouse1;
    [SerializeField] string playerTag = "Player";

    [Header("Liaison porte (laisser vide = faux levier)")]
    [SerializeField] Door connectedDoor;

    [Header("Message si faux levier")]
    [SerializeField] string brokenMessageId = "";

    [Header("Audio")]
    [SerializeField] SfxEmitter sfx;
    [SerializeField] AudioClip[] pullClips;
    [SerializeField] AudioClip[] lockInClips;
    [SerializeField] AudioClip[] wrongLeverClips;
    [SerializeField] float wrongLeverSoundDelay = 0.05f;

    Transform playerTransform;
    bool isAnimating;
    bool isActivated;

    public bool IsActivated => isActivated;

    public event Action OnLeverActivated;

    void Awake()
    {
        if (handle == null) handle = transform;

        SetHandleAngle(restAngle);

        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) playerTransform = p.transform;

        if (sfx == null) sfx = GetComponent<SfxEmitter>();

        if (sfx == null && HasAnyClip())
            Debug.LogError($"[Lever] '{name}' a des clips assignés mais aucun SfxEmitter : aucun son ne sera joué.", this);
    }

    void Update()
    {
        if (isActivated || isAnimating || playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            TryActivate();
        }
    }

    void TryActivate()
    {
        bool isRealLever = connectedDoor != null && connectedDoor.IsLeverRequired(this);

        if (isRealLever)
            StartCoroutine(ActivateRoutine());
        else
            StartCoroutine(FakeActivateRoutine());
    }

    IEnumerator ActivateRoutine()
    {
        isAnimating = true;

        PlayPullSound();

        yield return RotateHandle(restAngle, activeAngle, activationDuration, useBounce: true);

        PlayLockInSound();

        isActivated = true;
        isAnimating = false;

        OnLeverActivated?.Invoke();
    }

    IEnumerator FakeActivateRoutine()
    {
        isAnimating = true;

        if (!string.IsNullOrEmpty(brokenMessageId) && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ShowMessageById(brokenMessageId);
        }

        PlayPullSound();

        yield return RotateHandle(restAngle, fakeAngle, fakeDownDuration, useBounce: false);

        if (wrongLeverSoundDelay > 0f)
            yield return new WaitForSeconds(wrongLeverSoundDelay);

        PlayWrongLeverSound();

        yield return new WaitForSeconds(0.1f);

        yield return RotateHandle(fakeAngle, restAngle, fakeUpDuration, useBounce: true);

        isAnimating = false;
    }

    public void PlayPullSound()
    {
        if (sfx != null) sfx.Play(pullClips);
    }

    public void PlayLockInSound()
    {
        if (sfx != null) sfx.Play(lockInClips);
    }

    public void PlayWrongLeverSound()
    {
        if (sfx != null) sfx.Play(wrongLeverClips);
    }

    bool HasAnyClip()
    {
        return (pullClips != null && pullClips.Length > 0)
            || (lockInClips != null && lockInClips.Length > 0)
            || (wrongLeverClips != null && wrongLeverClips.Length > 0);
    }

    IEnumerator RotateHandle(float fromAngle, float toAngle, float duration, bool useBounce)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float easedT = useBounce ? EaseOutBack(t, bounceStrength) : EaseOutQuad(t);
            float angle = Mathf.LerpUnclamped(fromAngle, toAngle, easedT);

            SetHandleAngle(angle);
            yield return null;
        }

        SetHandleAngle(toAngle);
    }

    void SetHandleAngle(float angle)
    {
        handle.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    static float EaseOutBack(float t, float strength)
    {
        float c1 = strength;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        if (connectedDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, connectedDoor.transform.position);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
        }
    }
}