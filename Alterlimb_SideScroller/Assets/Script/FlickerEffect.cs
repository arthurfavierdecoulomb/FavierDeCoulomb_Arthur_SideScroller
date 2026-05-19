using UnityEngine;
using System.Collections;

/// <summary>
/// Fait apparaître ou disparaître un élément avec un effet de "flicker"
/// (clignotement façon vieil écran CRT).
/// 
/// Même esthétique que les transitions de niveau du LevelTransitionManager,
/// packagée en composant réutilisable : à poser sur n'importe quel élément
/// (titre, bouton, écran de cinématique...).
/// 
/// Le flicker agit en activant/désactivant le GameObject à intervalles
/// aléatoires, puis fixe l'état final (visible ou caché).
/// 
/// Utilisation par code :
///   flicker.FlickerIn();   // apparition
///   flicker.FlickerOut();  // disparition
/// On peut aussi attendre la fin via les coroutines FlickerInRoutine/OutRoutine.
/// </summary>
public class FlickerEffect : MonoBehaviour
{
    [Header("Cible à faire clignoter")]
    [Tooltip("L'objet à activer/désactiver. Si vide, c'est ce GameObject lui-même.")]
    [SerializeField] GameObject target;

    [Header("Durées")]
    [Tooltip("Durée du flicker d'apparition")]
    [SerializeField] float flickerInDuration = 0.5f;
    [Tooltip("Durée du flicker de disparition")]
    [SerializeField] float flickerOutDuration = 0.5f;

    [Header("Intervalle du clignotement")]
    [SerializeField] float flickerMinInterval = 0.04f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    [Header("Son (optionnel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip flickerSound;
    [Range(0f, 1f)]
    [SerializeField] float flickerSoundVolume = 0.3f;

    [Header("État de départ")]
    [Tooltip("Si coché, l'élément est caché au lancement (en attente d'un FlickerIn)")]
    [SerializeField] bool hiddenOnAwake = true;

    Coroutine flickerRoutine;

    void Awake()
    {
        if (target == null) target = gameObject;

        if (hiddenOnAwake)
            target.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════
    //  API publique
    // ════════════════════════════════════════════════════════════

    /// <summary>Fait apparaître l'élément en flicker.</summary>
    public void FlickerIn()
    {
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        flickerRoutine = StartCoroutine(FlickerInRoutine());
    }

    /// <summary>Fait disparaître l'élément en flicker.</summary>
    public void FlickerOut()
    {
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        flickerRoutine = StartCoroutine(FlickerOutRoutine());
    }

    /// <summary>
    /// Coroutine d'apparition — peut être attendue avec yield return
    /// pour enchaîner d'autres actions après.
    /// </summary>
    public IEnumerator FlickerInRoutine()
    {
        float elapsed = 0f;
        bool visible = false;

        while (elapsed < flickerInDuration)
        {
            visible = !visible;
            target.SetActive(visible);

            if (visible) PlayFlickerSound();

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        target.SetActive(true);  // état final : visible
        flickerRoutine = null;
    }

    /// <summary>
    /// Coroutine de disparition — peut être attendue avec yield return.
    /// </summary>
    public IEnumerator FlickerOutRoutine()
    {
        float elapsed = 0f;
        bool visible = true;

        while (elapsed < flickerOutDuration)
        {
            visible = !visible;
            target.SetActive(visible);

            if (!visible) PlayFlickerSound();

            float interval = Random.Range(flickerMinInterval, flickerMaxInterval);
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }

        target.SetActive(false);  // état final : caché
        flickerRoutine = null;
    }

    void PlayFlickerSound()
    {
        if (audioSource != null && flickerSound != null)
            audioSource.PlayOneShot(flickerSound, flickerSoundVolume);
    }
}