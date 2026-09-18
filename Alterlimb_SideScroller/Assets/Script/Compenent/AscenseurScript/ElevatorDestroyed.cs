using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class FlickerLight
{
    public SpriteRenderer spriteRenderer;
    public Sprite[] onFrames;
    public Sprite offSprite;
    public Light2D light2D;
    public float onIntensity = 1f;
    public float offIntensity = 0f;
}

public class ElevatorDestroyed : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite bodySprite;

    [Header("Lights")]
    [SerializeField] private FlickerLight[] lights;

    [Header("Flicker Settings")]
    [SerializeField] private float minFlickerDelay = 0.05f;
    [SerializeField] private float maxFlickerDelay = 0.3f;
    [SerializeField] private float offChance = 0.3f;

    [Header("Power Surge Settings")]
    [SerializeField] private float minSurgeDelay = 2f;
    [SerializeField] private float maxSurgeDelay = 6f;
    [SerializeField] private float minSurgeDuration = 0.5f;
    [SerializeField] private float maxSurgeDuration = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] flickerClips;
    [Range(0f, 1f)]
    [SerializeField] private float flickerVolume = 1f;

    private Coroutine[] flickerRoutines;

    private void Awake()
    {
        if (bodyRenderer != null && bodySprite != null)
        {
            bodyRenderer.sprite = bodySprite;
        }

        if (lights == null || lights.Length == 0)
        {
            Debug.LogError($"{name}: ElevatorDestroyed has no lights configured");
            enabled = false;
            return;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].spriteRenderer == null || lights[i].onFrames == null || lights[i].onFrames.Length == 0)
            {
                Debug.LogError($"{name}: light index {i} is missing its SpriteRenderer or onFrames");
            }
        }

        flickerRoutines = new Coroutine[lights.Length];
    }

    private void OnEnable()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            flickerRoutines[i] = StartCoroutine(FlickerLoop(i));
        }
    }

    private void OnDisable()
    {
        for (int i = 0; i < flickerRoutines.Length; i++)
        {
            if (flickerRoutines[i] != null)
            {
                StopCoroutine(flickerRoutines[i]);
                flickerRoutines[i] = null;
            }
        }
    }

    private IEnumerator FlickerLoop(int index)
    {
        FlickerLight light = lights[index];
        float nextSurgeTime = Time.time + Random.Range(minSurgeDelay, maxSurgeDelay);

        while (true)
        {
            if (Time.time >= nextSurgeTime)
            {
                yield return StartCoroutine(PowerSurge(light));
                nextSurgeTime = Time.time + Random.Range(minSurgeDelay, maxSurgeDelay);
            }
            else
            {
                bool goingOff = Random.value < offChance;

                if (goingOff)
                {
                    light.spriteRenderer.sprite = light.offSprite;
                    if (light.light2D != null) light.light2D.intensity = light.offIntensity;
                }
                else
                {
                    light.spriteRenderer.sprite = light.onFrames[Random.Range(0, light.onFrames.Length)];
                    if (light.light2D != null) light.light2D.intensity = light.onIntensity;
                }

                PlayFlickerSound();

                yield return new WaitForSeconds(Random.Range(minFlickerDelay, maxFlickerDelay));
            }
        }
    }

    private IEnumerator PowerSurge(FlickerLight light)
    {
        float duration = Random.Range(minSurgeDuration, maxSurgeDuration);
        float elapsed = 0f;
        light.spriteRenderer.sprite = light.offSprite;
        if (light.light2D != null) light.light2D.intensity = light.offIntensity;
        PlayFlickerSound();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void PlayFlickerSound()
    {
        if (audioSource == null || flickerClips == null || flickerClips.Length == 0) return;

        audioSource.PlayOneShot(flickerClips[Random.Range(0, flickerClips.Length)], flickerVolume);
    }
}