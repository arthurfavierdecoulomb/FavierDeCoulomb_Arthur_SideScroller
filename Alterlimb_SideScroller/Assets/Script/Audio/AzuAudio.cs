using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Tilemaps;

public class AzuAudio : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceBank
    {
        public SurfaceType surface;
        public AudioClip[] footsteps;
        public AudioClip[] stomps;
    }

    [Header("Sortie mixer")]
    [SerializeField] AudioMixerGroup sfxGroup;

    [Header("Pas par surface")]
    [SerializeField] SurfaceBank[] surfaceBanks;
    [SerializeField] SurfaceTileDatabase surfaceTiles;
    [SerializeField] SurfaceType defaultSurface = SurfaceType.Terre;
    [SerializeField] LayerMask surfaceMask;
    [SerializeField] float surfaceCheckDistance = 1.2f;
    [SerializeField] float tileProbeDepth = 0.05f;
    [SerializeField] float footstepVolume = 0.8f;
    [SerializeField] float stompVolume = 1f;

    [Header("Atterrissage")]
    [SerializeField] bool autoStompOnLanding = true;
    [SerializeField] float minFallSpeedForStomp = 6f;

    [Header("Saut et degats")]
    [SerializeField] AudioClip[] jumpClips;
    [SerializeField] float jumpVolume = 0.9f;
    [SerializeField] AudioClip[] hurtClips;
    [SerializeField] float hurtVolume = 1f;

    [Header("Changement de bras")]
    [SerializeField] AudioClip[] swapHandClips;
    [SerializeField] AudioClip[] swapSawClips;
    [SerializeField] AudioClip[] swapGrappleClips;
    [SerializeField] float swapVolume = 0.8f;

    [Header("Scie")]
    [SerializeField] AudioClip[] sawAttackClips;
    [SerializeField] float sawAttackVolume = 0.9f;

    [Header("Grappin")]
    [SerializeField] AudioClip[] grappleShootClips;
    [SerializeField] AudioClip[] grappleHookedClips;
    [SerializeField] AudioClip[] ropeClickClips;
    [SerializeField] float grappleVolume = 0.9f;
    [SerializeField] float ropeClickStep = 0.35f;
    [SerializeField] float ropeClickVolume = 0.6f;

    [Header("Alerte energie vide")]
    [SerializeField] AudioClip lowEnergyLoop;
    [SerializeField] float lowEnergyVolume = 0.5f;
    [Range(0f, 0.5f)]
    [SerializeField] float lowEnergyRatio = 0.02f;

    [Header("Variation")]
    [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Debug")]
    [SerializeField] bool logSurfaceDetection = false;

    AudioSource oneShotSource;
    AudioSource alertSource;

    AbilityManager abilityManager;
    AbilityEnergySystem energySystem;
    GrapplingHook grapple;
    Rigidbody2D rb;

    ArmAbility lastArm;
    GrapplingHook.GrappleState lastGrappleState = GrapplingHook.GrappleState.Idle;
    float lastRopeLength;
    float ropeClickAccumulator;

    bool wasGrounded = true;
    float previousFallSpeed;
    AudioClip lastPlayedClip;

    readonly Dictionary<Collider2D, Tilemap[]> tilemapCache = new Dictionary<Collider2D, Tilemap[]>();

    bool IsPaused => Time.timeScale <= 0f;

    void Awake()
    {
        abilityManager = GetComponent<AbilityManager>();
        energySystem = GetComponent<AbilityEnergySystem>();
        grapple = GetComponent<GrapplingHook>();
        rb = GetComponent<Rigidbody2D>();

        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.loop = false;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.outputAudioMixerGroup = sfxGroup;

        alertSource = gameObject.AddComponent<AudioSource>();
        alertSource.playOnAwake = false;
        alertSource.loop = true;
        alertSource.spatialBlend = 0f;
        alertSource.volume = lowEnergyVolume;
        alertSource.clip = lowEnergyLoop;
        alertSource.outputAudioMixerGroup = sfxGroup;
    }

    void Start()
    {
        if (sfxGroup == null)
            Debug.LogError("[AzuAudio] Sfx Group non assigne : les sons ne passeront pas par le mixer.");

        if (surfaceBanks == null || surfaceBanks.Length == 0)
            Debug.LogError("[AzuAudio] Aucune Surface Bank assignee : aucun bruit de pas ne sera joue.");

        if (surfaceMask.value == 0)
            Debug.LogError("[AzuAudio] Surface Mask vide : la detection de surface renverra toujours la surface par defaut.");

        if (abilityManager != null)
            lastArm = abilityManager.CurrentArm;

        if (grapple != null)
            lastGrappleState = grapple.State;
    }

    void Update()
    {
        UpdateArmSwap();
        UpdateGrapple();
        UpdateLanding();
        UpdateLowEnergyAlert();
    }

    public void Footstep()
    {
        SurfaceBank bank = GetBank(DetectSurface());
        if (bank == null) return;
        PlayRandom(bank.footsteps, footstepVolume);
    }

    public void Stomp()
    {
        SurfaceBank bank = GetBank(DetectSurface());
        if (bank == null) return;

        AudioClip[] clips = (bank.stomps != null && bank.stomps.Length > 0) ? bank.stomps : bank.footsteps;
        PlayRandom(clips, stompVolume);
    }

    public void Jump()
    {
        PlayRandom(jumpClips, jumpVolume);
    }

    public void SawAttack()
    {
        PlayRandom(sawAttackClips, sawAttackVolume);
    }

    public void Hurt()
    {
        PlayRandom(hurtClips, hurtVolume);
    }

    void UpdateArmSwap()
    {
        if (abilityManager == null) return;
        if (abilityManager.CurrentArm == lastArm) return;

        lastArm = abilityManager.CurrentArm;

        switch (lastArm)
        {
            case ArmAbility.Hand: PlayRandom(swapHandClips, swapVolume); break;
            case ArmAbility.Saw: PlayRandom(swapSawClips, swapVolume); break;
            case ArmAbility.Grapple: PlayRandom(swapGrappleClips, swapVolume); break;
        }
    }

    void UpdateGrapple()
    {
        if (grapple == null) return;

        GrapplingHook.GrappleState state = grapple.State;

        if (state != lastGrappleState)
        {
            if (state == GrapplingHook.GrappleState.Deploying)
            {
                PlayRandom(grappleShootClips, grappleVolume);
            }
            else if (state == GrapplingHook.GrappleState.Hooked)
            {
                PlayRandom(grappleHookedClips, grappleVolume);
                lastRopeLength = grapple.RopeLength;
                ropeClickAccumulator = 0f;
            }

            lastGrappleState = state;
        }

        if (state != GrapplingHook.GrappleState.Hooked) return;

        float length = grapple.RopeLength;
        ropeClickAccumulator += Mathf.Abs(length - lastRopeLength);
        lastRopeLength = length;

        if (ropeClickAccumulator >= ropeClickStep)
        {
            ropeClickAccumulator -= ropeClickStep;
            PlayRandom(ropeClickClips, ropeClickVolume);
        }
    }

    void UpdateLanding()
    {
        if (!autoStompOnLanding || rb == null) return;

        bool grounded = Physics2D.Raycast(transform.position, Vector2.down, surfaceCheckDistance, surfaceMask);

        if (grounded && !wasGrounded && previousFallSpeed <= -minFallSpeedForStomp)
            Stomp();

        wasGrounded = grounded;
        previousFallSpeed = rb.linearVelocity.y;
    }

    void UpdateLowEnergyAlert()
    {
        bool shouldPlay = false;

        if (lowEnergyLoop != null && energySystem != null && abilityManager != null && !IsPaused)
        {
            AbilityEnergySystem.AbilityEnergy watched = null;

            switch (abilityManager.CurrentArm)
            {
                case ArmAbility.Grapple: watched = energySystem.grapplingEnergy; break;
                case ArmAbility.Saw: watched = energySystem.sawEnergy; break;
            }

            if (watched != null && watched.maxEnergy > 0f)
                shouldPlay = (watched.currentEnergy / watched.maxEnergy) <= lowEnergyRatio;
        }

        if (shouldPlay && !alertSource.isPlaying)
        {
            alertSource.volume = lowEnergyVolume;
            alertSource.Play();
        }
        else if (!shouldPlay && alertSource.isPlaying)
        {
            alertSource.Stop();
        }
    }

    SurfaceType DetectSurface()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, surfaceCheckDistance, surfaceMask);
        if (hit.collider == null) return defaultSurface;

        Vector3 probePoint = (Vector3)(hit.point + Vector2.down * tileProbeDepth);
        Tilemap[] tilemaps = ResolveTilemaps(hit.collider);

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null) continue;

            TileBase tile = tilemap.GetTile(tilemap.WorldToCell(probePoint));
            if (tile == null) continue;

            if (surfaceTiles != null && surfaceTiles.TryGetSurface(tile, out SurfaceType tileSurface))
            {
                if (logSurfaceDetection)
                    Debug.Log($"[AzuAudio] Tuile {tile.name} -> {tileSurface} (database)");

                return tileSurface;
            }

            SurfaceTag tilemapTag = tilemap.GetComponent<SurfaceTag>();
            if (tilemapTag != null)
            {
                if (logSurfaceDetection)
                    Debug.Log($"[AzuAudio] Tuile {tile.name} -> {tilemapTag.surface} (tag de {tilemap.name})");

                return tilemapTag.surface;
            }

            if (logSurfaceDetection)
                Debug.LogWarning($"[AzuAudio] Tuile {tile.name} sur {tilemap.name} : ni database, ni SurfaceTag.");
        }

        SurfaceTag colliderTag = hit.collider.GetComponent<SurfaceTag>();
        if (colliderTag == null) colliderTag = hit.collider.GetComponentInParent<SurfaceTag>();

        return colliderTag != null ? colliderTag.surface : defaultSurface;
    }

    Tilemap[] ResolveTilemaps(Collider2D collider)
    {
        if (tilemapCache.TryGetValue(collider, out Tilemap[] cached)) return cached;

        Tilemap own = collider.GetComponent<Tilemap>();
        Tilemap[] found = own != null ? new Tilemap[] { own } : collider.GetComponentsInChildren<Tilemap>();

        if (found.Length == 0 && collider.transform.parent != null)
            found = collider.transform.parent.GetComponentsInChildren<Tilemap>();

        tilemapCache[collider] = found;
        return found;
    }

    SurfaceBank GetBank(SurfaceType surface)
    {
        if (surfaceBanks == null) return null;

        for (int i = 0; i < surfaceBanks.Length; i++)
        {
            if (surfaceBanks[i].surface == surface) return surfaceBanks[i];
        }

        for (int i = 0; i < surfaceBanks.Length; i++)
        {
            if (surfaceBanks[i].surface == defaultSurface) return surfaceBanks[i];
        }

        return null;
    }

    void PlayRandom(AudioClip[] clips, float volume)
    {
        if (IsPaused) return;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clips.Length > 1 && clip == lastPlayedClip)
            clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];

        if (clip == null) return;

        lastPlayedClip = clip;
        oneShotSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        oneShotSource.PlayOneShot(clip, volume);
    }
}