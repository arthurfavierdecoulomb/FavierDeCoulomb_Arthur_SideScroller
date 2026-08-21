using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class BossDialogueTrigger : MonoBehaviour
{
    [Header("Déclenchement")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] string sequenceId = "boss_intro";
    [SerializeField] bool triggerOnce = true;

    [Header("Caméra")]
    [SerializeField] Camera targetCamera;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField] Transform cameraTarget;
    [SerializeField] float targetOrthographicSize = 12f;
    [SerializeField] float cameraMoveDuration = 1.5f;
    [SerializeField] AnimationCurve cameraCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Après le dialogue")]
    [SerializeField] bool resumeFollowAfterDialogue = true;
    [SerializeField] bool restoreCameraAfterDialogue = false;
    [SerializeField] float cameraReturnDuration = 1f;

    bool triggered;

    void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (cameraFollow == null && targetCamera != null)
            cameraFollow = targetCamera.GetComponent<CameraFollow>();

        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce) return;
        if (!other.CompareTag(playerTag)) return;

        triggered = true;
        StartCoroutine(SequenceRoutine());
    }

    IEnumerator SequenceRoutine()
    {
        Vector3 originalPosition = targetCamera.transform.position;
        float originalSize = targetCamera.orthographicSize;

        if (cameraFollow == null)
            Debug.LogWarning("[BossDialogueTrigger] Aucun CameraFollow trouvé : la caméra risque d'être reprise pendant le dialogue.");
        else
            cameraFollow.Suspend();

        yield return StartCoroutine(MoveCamera(cameraTarget != null ? cameraTarget.position : originalPosition,
                                               targetOrthographicSize,
                                               cameraMoveDuration));

        if (BossDialogueManager.Instance == null)
        {
            Debug.LogWarning("[BossDialogueTrigger] Aucun BossDialogueManager dans la scène.");
            if (cameraFollow != null) cameraFollow.Resume();
            yield break;
        }

        BossDialogueManager.Instance.PlaySequence(sequenceId);
        yield return null;

        if (!BossDialogueManager.Instance.IsPlaying)
            Debug.LogWarning($"[BossDialogueTrigger] La séquence '{sequenceId}' n'a pas démarré.");

        while (BossDialogueManager.Instance.IsPlaying)
            yield return null;

        if (restoreCameraAfterDialogue)
            yield return StartCoroutine(MoveCamera(originalPosition, originalSize, cameraReturnDuration));

        if (resumeFollowAfterDialogue && cameraFollow != null)
        {
            cameraFollow.ResetZoom();
            cameraFollow.Resume();
        }
    }

    IEnumerator MoveCamera(Vector3 targetPosition, float targetSize, float duration)
    {
        if (targetCamera == null) yield break;

        Transform cam = targetCamera.transform;
        Vector3 startPos = cam.position;
        float startSize = targetCamera.orthographicSize;
        Vector3 destination = new Vector3(targetPosition.x, targetPosition.y, startPos.z);

        if (duration <= 0f)
        {
            cam.position = destination;
            targetCamera.orthographicSize = targetSize;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = cameraCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            cam.position = Vector3.LerpUnclamped(startPos, destination, t);
            targetCamera.orthographicSize = Mathf.LerpUnclamped(startSize, targetSize, t);
            yield return null;
        }

        cam.position = destination;
        targetCamera.orthographicSize = targetSize;
    }
}