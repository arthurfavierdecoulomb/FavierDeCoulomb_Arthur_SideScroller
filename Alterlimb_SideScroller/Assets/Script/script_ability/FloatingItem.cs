using UnityEngine;

/// <summary>
/// Anime un item pour qu'il flotte doucement de haut en bas et tourne
/// lentement sur lui-même. Idéal pour les pickups d'altermembres
/// (scie, grappin) afin de les rendre visuellement attractifs.
/// 
/// Les valeurs par défaut donnent un flottement lent et discret, comme
/// dans Hollow Knight ou Hades.
/// </summary>
public class FloatingItem : MonoBehaviour
{
    [Header("Flottement vertical")]
    [Tooltip("Amplitude du flottement haut-bas (en unités Unity)")]
    [SerializeField] float floatAmplitude = 0.2f;
    [Tooltip("Vitesse du flottement (plus haut = plus rapide)")]
    [SerializeField] float floatSpeed = 1.5f;

    [Header("Rotation (optionnelle)")]
    [Tooltip("Vitesse de rotation en degrés par seconde. 0 = pas de rotation")]
    [SerializeField] float rotationSpeed = 0f;

    [Header("Désynchronisation")]
    [Tooltip("Si activé : chaque item démarre avec un offset aléatoire pour qu'ils ne bougent pas tous en synchro")]
    [SerializeField] bool randomizePhase = true;

    Vector3 startPosition;
    float phaseOffset;

    void Awake()
    {
        // Mémorise la position initiale comme centre du flottement
        startPosition = transform.position;

        // Décalage aléatoire pour que plusieurs items soient désynchronisés
        phaseOffset = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void Update()
    {
        // Mouvement vertical en sinusoïde
        float yOffset = Mathf.Sin(Time.time * floatSpeed + phaseOffset) * floatAmplitude;
        transform.position = startPosition + new Vector3(0f, yOffset, 0f);

        // Rotation continue (si activée)
        if (rotationSpeed != 0f)
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}