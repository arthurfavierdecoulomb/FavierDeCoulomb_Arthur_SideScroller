using System.Collections;
using UnityEngine;

public class SpikeWarningUI : MonoBehaviour
{
    [Header("Panneaux")]
    [SerializeField] private GameObject leftPanel;
    [SerializeField] private GameObject rightPanel;

    [Header("Clignotement")]
    [SerializeField] private float blinkInterval = 0.18f;
    [SerializeField] private float blinkAcceleration = 0.82f;
    [SerializeField] private float minBlinkInterval = 0.05f;

    [Header("Pulsation de l'icône")]
    [SerializeField] private RectTransform leftIcon;
    [SerializeField] private RectTransform rightIcon;
    [SerializeField] private bool pulseIcon = true;
    [SerializeField] private float iconPulseScale = 1.25f;
    [SerializeField] private float iconPulseDuration = 0.3f;

    [Header("Son")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip blipSound;
    [Range(0f, 1f)]
    [SerializeField] private float blipVolume = 0.5f;

    [Header("Diagnostic")]
    [SerializeField] private bool logSetupWarnings = true;

    private Vector3 leftIconBaseScale = Vector3.one;
    private Vector3 rightIconBaseScale = Vector3.one;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        if (leftIcon != null) leftIconBaseScale = leftIcon.localScale;
        if (rightIcon != null) rightIconBaseScale = rightIcon.localScale;

        HideAll();
        LogSetup();
    }

    private void LogSetup()
    {
        if (!logSetupWarnings)
            return;

        if (leftPanel == null)
            Debug.LogError($"[SpikeWarningUI] '{name}' : Left Panel non assigné, aucun avertissement à gauche.", this);

        if (rightPanel == null)
            Debug.LogError($"[SpikeWarningUI] '{name}' : Right Panel non assigné, aucun avertissement à droite.", this);
    }

    public IEnumerator Warn(bool fromLeft, float duration)
    {
        GameObject panel = fromLeft ? leftPanel : rightPanel;
        RectTransform icon = fromLeft ? leftIcon : rightIcon;
        Vector3 baseScale = fromLeft ? leftIconBaseScale : rightIconBaseScale;

        if (panel == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        if (pulseIcon && icon != null)
        {
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseRoutine(icon, baseScale));
        }

        float elapsed = 0f;
        float interval = blinkInterval;
        bool visible = true;

        while (elapsed < duration)
        {
            panel.SetActive(visible);

            if (visible && audioSource != null && blipSound != null)
                audioSource.PlayOneShot(blipSound, blipVolume);

            visible = !visible;

            float wait = Mathf.Min(interval, duration - elapsed);
            yield return new WaitForSeconds(wait);

            elapsed += wait;
            interval = Mathf.Max(minBlinkInterval, interval * blinkAcceleration);
        }

        HidePanel(panel, icon, baseScale);
    }

    public void HideAll()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        HidePanel(leftPanel, leftIcon, leftIconBaseScale);
        HidePanel(rightPanel, rightIcon, rightIconBaseScale);
    }

    private void HidePanel(GameObject panel, RectTransform icon, Vector3 baseScale)
    {
        if (icon != null)
            icon.localScale = baseScale;

        if (panel != null && panel.activeSelf)
            panel.SetActive(false);
    }

    private IEnumerator PulseRoutine(RectTransform icon, Vector3 baseScale)
    {
        float time = 0f;
        float period = Mathf.Max(0.05f, iconPulseDuration);

        while (true)
        {
            time += Time.deltaTime;
            float wave = (Mathf.Sin(time * Mathf.PI * 2f / period) + 1f) * 0.5f;
            icon.localScale = baseScale * Mathf.Lerp(1f, iconPulseScale, wave);
            yield return null;
        }
    }
}