using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class OxiLight : MonoBehaviour
{
    public enum BreakMoment
    {
        ExplosionDuNoyau,
        FinDeTransformation
    }

    [System.Serializable]
    public class FlickerSettings
    {
        public float duration = 0.8f;
        public Vector2 intervalRange = new Vector2(0.03f, 0.09f);
        [Range(0f, 1f)] public float offChance = 0.4f;
        public Vector2 intensityRange = new Vector2(0.3f, 1.3f);
    }

    [System.Serializable]
    public class LightBreak
    {
        public string label = "Phase 2";
        public BreakMoment moment = BreakMoment.FinDeTransformation;
        public int remainingCores = 2;
        public List<Light2D> lights = new List<Light2D>();
    }

    private class LightState
    {
        public Light2D light;
        public float baseIntensity;
        public bool dead;
        public float dyingUntil;
        public float nextAgonyTime;
        public float agonyUntil;
        public float nextChangeTime;
        public float target = 1f;
        public float current = 1f;
    }

    [Header("Références")]
    [SerializeField] private OxiOCore core;
    [SerializeField] private OxiOBossDirector director;
    [SerializeField] private OxiO_Animation oxiAnimation;

    [Header("Lumières")]
    [SerializeField] private Transform lightsRoot;
    [SerializeField] private List<Light2D> lights = new List<Light2D>();

    [Header("Explosion d'un noyau")]
    [SerializeField]
    private FlickerSettings explosionFlicker = new FlickerSettings
    {
        duration = 0.9f,
        intervalRange = new Vector2(0.03f, 0.08f),
        offChance = 0.45f,
        intensityRange = new Vector2(0.2f, 1.4f)
    };

    [Header("Transformation")]
    [SerializeField]
    private FlickerSettings transformationFlicker = new FlickerSettings
    {
        duration = 0f,
        intervalRange = new Vector2(0.02f, 0.06f),
        offChance = 0.55f,
        intensityRange = new Vector2(0.1f, 1.8f)
    };

    [Header("Lumières qui crèvent")]
    [SerializeField] private int phaseCount = 2;
    [SerializeField]
    private List<LightBreak> breaks = new List<LightBreak>
    {
        new LightBreak { label = "Phase 2", moment = BreakMoment.FinDeTransformation, remainingCores = 2 },
        new LightBreak { label = "Phase finale", moment = BreakMoment.ExplosionDuNoyau, remainingCores = 1 }
    };
    [SerializeField]
    private FlickerSettings deathSputter = new FlickerSettings
    {
        duration = 1.2f,
        intervalRange = new Vector2(0.04f, 0.15f),
        offChance = 0.6f,
        intensityRange = new Vector2(0.1f, 1.1f)
    };

    [Header("Agonie des lumières mortes")]
    [SerializeField] private bool agonyAfterDeath = true;
    [SerializeField] private Vector2 agonyDelayRange = new Vector2(2f, 6f);
    [SerializeField] private float agonyFlashDuration = 0.25f;

    [Header("Retour au calme")]
    [SerializeField] private float settleSpeed = 8f;

    [Header("Diagnostic")]
    [SerializeField] private bool logDiagnostics = true;

    private readonly List<LightState> states = new List<LightState>();
    private readonly Dictionary<Light2D, LightState> stateByLight = new Dictionary<Light2D, LightState>();
    private readonly HashSet<LightBreak> triggeredBreaks = new HashSet<LightBreak>();

    private float explosionUntil;
    private bool transforming;

    private void Awake()
    {
        if (core == null)
            core = FindAnyObjectByType<OxiOCore>();

        if (director == null)
            director = FindAnyObjectByType<OxiOBossDirector>();

        if (oxiAnimation == null)
            oxiAnimation = FindAnyObjectByType<OxiO_Animation>();

        CollectLights();
        LogSetup();
    }

    private void OnEnable()
    {
        if (core != null)
            core.onCoreExplosionEvent.AddListener(HandleCoreExplosion);

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted += HandleTransformationStarted;
            oxiAnimation.OnTransformationComplete += HandleTransformationComplete;
        }
    }

    private void OnDisable()
    {
        if (core != null)
            core.onCoreExplosionEvent.RemoveListener(HandleCoreExplosion);

        if (oxiAnimation != null)
        {
            oxiAnimation.OnTransformationStarted -= HandleTransformationStarted;
            oxiAnimation.OnTransformationComplete -= HandleTransformationComplete;
        }
    }

    private void CollectLights()
    {
        if (lights.Count == 0 && lightsRoot != null)
            lights.AddRange(lightsRoot.GetComponentsInChildren<Light2D>(true));

        foreach (LightBreak entry in breaks)
        {
            if (entry == null)
                continue;

            foreach (Light2D light in entry.lights)
                if (light != null && !lights.Contains(light))
                    lights.Add(light);
        }

        foreach (Light2D light in lights)
        {
            if (light == null || stateByLight.ContainsKey(light))
                continue;

            LightState state = new LightState
            {
                light = light,
                baseIntensity = light.intensity
            };

            states.Add(state);
            stateByLight[light] = state;
        }
    }

    private void LogSetup()
    {
        if (!logDiagnostics)
            return;

        if (states.Count == 0)
            Debug.LogError($"[OxiLight] '{name}' : aucune Light2D. Remplis la liste Lights ou assigne Lights Root.", this);
        else
            Debug.Log($"[OxiLight] '{name}' : {states.Count} lumière(s) pilotée(s).", this);

        if (core == null)
            Debug.LogError($"[OxiLight] '{name}' : aucun OxiOCore trouvé, les lumières ne réagiront pas aux explosions.", this);

        if (director == null)
            Debug.LogError($"[OxiLight] '{name}' : aucun OxiOBossDirector trouvé, les lumières de la phase finale ne crèveront pas.", this);

        if (oxiAnimation == null)
            Debug.LogError($"[OxiLight] '{name}' : aucun OxiO_Animation trouvé, les lumières ne réagiront pas à la transformation.", this);

        foreach (LightBreak entry in breaks)
            if (entry != null && entry.lights.Count == 0)
                Debug.LogWarning($"[OxiLight] '{name}' : le groupe '{entry.label}' n'a aucune lumière, rien ne crèvera à ce moment-là.", this);
    }

    private void Update()
    {
        float now = Time.time;
        bool exploding = now < explosionUntil;

        foreach (LightState state in states)
        {
            if (state.light == null)
                continue;

            FlickerSettings active = ActiveFlicker(state, now, exploding);

            if (active != null)
            {
                if (now >= state.nextChangeTime)
                {
                    state.target = Sample(active);
                    state.nextChangeTime = now + Random.Range(active.intervalRange.x, active.intervalRange.y);
                }

                state.current = state.target;
            }
            else
            {
                float rest = state.dead ? 0f : 1f;
                state.target = rest;
                state.current = Mathf.MoveTowards(state.current, rest, settleSpeed * Time.deltaTime);
            }

            state.light.intensity = state.baseIntensity * state.current;
        }
    }

    private FlickerSettings ActiveFlicker(LightState state, float now, bool exploding)
    {
        if (state.dead)
        {
            if (now < state.dyingUntil || now < state.agonyUntil)
                return deathSputter;

            if (agonyAfterDeath && now >= state.nextAgonyTime)
            {
                state.agonyUntil = now + agonyFlashDuration;
                state.nextAgonyTime = state.agonyUntil + Random.Range(agonyDelayRange.x, agonyDelayRange.y);
                return deathSputter;
            }

            return null;
        }

        if (transforming)
            return transformationFlicker;

        if (exploding)
            return explosionFlicker;

        return null;
    }

    private float Sample(FlickerSettings settings)
    {
        if (Random.value < settings.offChance)
            return 0f;

        return Random.Range(settings.intensityRange.x, settings.intensityRange.y);
    }

    private void HandleCoreExplosion()
    {
        explosionUntil = Time.time + explosionFlicker.duration;

        int remaining = RemainingCores();

        if (logDiagnostics)
            Debug.Log($"[OxiLight] Explosion d'un noyau : les lumières clignotent ({remaining} noyau(x) en vie).", this);

        foreach (LightBreak entry in breaks)
            if (entry != null && entry.moment == BreakMoment.ExplosionDuNoyau && entry.remainingCores == remaining)
                BreakLights(entry);
    }

    private void HandleTransformationStarted()
    {
        transforming = true;

        if (logDiagnostics)
            Debug.Log("[OxiLight] Transformation : les lumières s'affolent.", this);
    }

    private void HandleTransformationComplete()
    {
        transforming = false;

        foreach (LightBreak entry in breaks)
            if (entry != null && entry.moment == BreakMoment.FinDeTransformation)
                BreakLights(entry);
    }

    private void BreakLights(LightBreak entry)
    {
        if (!triggeredBreaks.Add(entry))
            return;

        float now = Time.time;
        int count = 0;

        foreach (Light2D light in entry.lights)
        {
            if (light == null || !stateByLight.TryGetValue(light, out LightState state) || state.dead)
                continue;

            state.dead = true;
            state.dyingUntil = now + deathSputter.duration;
            state.nextAgonyTime = state.dyingUntil + Random.Range(agonyDelayRange.x, agonyDelayRange.y);
            state.nextChangeTime = now;
            count++;
        }

        if (logDiagnostics)
            Debug.Log($"[OxiLight] '{entry.label}' : {count} lumière(s) crèvent.", this);
    }

    private int RemainingCores()
    {
        if (director == null || core == null)
            return -1;

        int perPhase = core.CutsDoneThisPhase + core.CutsRemainingThisPhase;
        int total = phaseCount * perPhase;
        int done = (director.CurrentPhase - 1) * perPhase + core.CutsDoneThisPhase;

        return Mathf.Max(0, total - done);
    }
}
