using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UISpriteAnimator : MonoBehaviour
{
    [Header("Images")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 12f;
    [SerializeField] private bool loop = true;

    private Image image;
    private float timer;
    private int index;

    private void Awake()
    {
        image = GetComponent<Image>();

        if (frames == null || frames.Length == 0)
            Debug.LogError($"[UISpriteAnimator] '{name}' : aucune image dans Frames, l'animation ne tournera pas.", this);
    }

    private void OnEnable()
    {
        timer = 0f;
        index = 0;
        ApplyFrame();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || framesPerSecond <= 0f)
            return;

        timer += Time.unscaledDeltaTime;

        float frameDuration = 1f / framesPerSecond;

        while (timer >= frameDuration)
        {
            timer -= frameDuration;

            if (index < frames.Length - 1)
                index++;
            else if (loop)
                index = 0;
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (image != null && frames != null && frames.Length > 0)
            image.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
    }
}