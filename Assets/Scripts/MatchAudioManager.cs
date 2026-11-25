
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MatchAudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource effectsSource;

    [Header("Game Start")]
    [SerializeField] private AudioClip gameStartSound;
    [SerializeField, Range(0f, 1f)] private float gameStartVolume = 1f;
    [SerializeField] private float gameStartDelay = 0f;

    [Header("Background Music")]
    [SerializeField] private AudioClip backgroundSound;
    [SerializeField, Range(0f, 1f)] private float backgroundVolume = 0.5f;

    [Header("Enemy Death")]
    [SerializeField] private AudioClip enemyDeathSound;
    [SerializeField, Range(0f, 1f)] private float enemyDeathVolume = 1f;
    [SerializeField] private float enemyDeathDelay = 0f;

    [Header("Player Dash")]
    [SerializeField] private AudioClip dashSound;
    [SerializeField, Range(0f, 1f)] private float dashVolume = 1f;
    [SerializeField] private float dashDelay = 0f;

    [Header("Sword Swing")]
    [SerializeField] private AudioClip swordSwingSound;
    [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 1f;
    [SerializeField] private float swordSwingDelay = 0f;

    [Header("Player Hit")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;
    [SerializeField] private float hitDelay = 0f;

    [Header("Enemy Hit")]
    [SerializeField] private AudioClip enemyHitSound;
    [SerializeField, Range(0f, 1f)] private float enemyHitVolume = 1f;
    [SerializeField] private float enemyHitDelay = 0f;

    [Header("Detection")]
    [SerializeField] private float scanInterval = 0.2f;

    private class HealthRegistration
    {
        public System.Action<float, float> healthChangedHandler;
        public System.Action deathHandler;
    }

    private readonly Dictionary<MechaHealth, HealthRegistration> registeredHealth = new();
    private readonly Dictionary<MechaMovement, bool> dashStates = new();
    private readonly HashSet<CrystalSwordAttack> registeredAttacks = new();

    private readonly List<MechaMovement> dashSnapshot = new();

    private float nextScanTime;
    private bool backgroundStarted;
    private bool deathSoundPlaying;

    private void Awake()
    {
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        if (effectsSource == null)
            effectsSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = false;

        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
    }

    private void Start()
    {
        StartCoroutine(PlayGameStartSequence());
        ScanObjects();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + scanInterval;
            ScanObjects();
        }

        UpdateDashDetection();
        UpdateSwordSwing();
    }

    private IEnumerator PlayGameStartSequence()
    {
        if (gameStartDelay > 0f)
            yield return new WaitForSecondsRealtime(gameStartDelay);

        if (gameStartSound != null)
        {
            musicSource.PlayOneShot(gameStartSound, gameStartVolume);
            yield return new WaitForSecondsRealtime(gameStartSound.length);
        }

        StartBackgroundMusic();
    }

    private void StartBackgroundMusic()
    {
        if (backgroundStarted || backgroundSound == null)
            return;

        backgroundStarted = true;

        musicSource.Stop();
        musicSource.clip = backgroundSound;
        musicSource.volume = backgroundVolume;
        musicSource.loop = true;
        musicSource.Play();
    }

    private void ScanObjects()
    {
        MechaHealth[] healthObjects = FindObjectsByType<MechaHealth>(FindObjectsSortMode.None);

        foreach (MechaHealth health in healthObjects)
        {
            if (health != null && !registeredHealth.ContainsKey(health))
                RegisterHealth(health);
        }

        MechaMovement[] movements = FindObjectsByType<MechaMovement>(FindObjectsSortMode.None);

        foreach (MechaMovement movement in movements)
        {
            if (movement != null && !dashStates.ContainsKey(movement))
                dashStates.Add(movement, movement.IsDashing);
        }

        CrystalSwordAttack[] attacks = FindObjectsByType<CrystalSwordAttack>(FindObjectsSortMode.None);

        foreach (CrystalSwordAttack attack in attacks)
        {
            if (attack != null)
                registeredAttacks.Add(attack);
        }

        RemoveDestroyedObjects();
    }

    private void RegisterHealth(MechaHealth health)
    {
        HealthRegistration registration = new HealthRegistration();

        registration.healthChangedHandler = (previous, current) =>
        {
            HandleHealthChanged(health, previous, current);
        };

        registration.deathHandler = () =>
        {
            HandleDeath(health);
        };

        health.HealthChanged += registration.healthChangedHandler;
        health.Death += registration.deathHandler;

        registeredHealth.Add(health, registration);
    }

    private void HandleHealthChanged(MechaHealth health, float previous, float current)
    {
        if (health == null || !health.CompareTag("Enemy"))
            return;

        if (current < previous)
        {
            PlayHit();
            PlayEnemyHit();
        }
    }

    private void HandleDeath(MechaHealth health)
    {
        if (health == null || !health.CompareTag("Enemy"))
            return;

        PlayEnemyDeath();
    }

    private void UpdateDashDetection()
    {
        dashSnapshot.Clear();

        foreach (MechaMovement movement in dashStates.Keys)
            dashSnapshot.Add(movement);

        for (int i = 0; i < dashSnapshot.Count; i++)
        {
            MechaMovement movement = dashSnapshot[i];

            if (!dashStates.ContainsKey(movement))
                continue;

            if (movement == null)
            {
                dashStates.Remove(movement);
                continue;
            }

            bool currentlyDashing = movement.IsDashing;
            bool wasDashing = dashStates[movement];

            if (currentlyDashing && !wasDashing && movement.IsOwner)
                PlayDash();

            if (dashStates.ContainsKey(movement))
                dashStates[movement] = currentlyDashing;
        }
    }

    private void UpdateSwordSwing()
    {
        if (Mouse.current == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        foreach (CrystalSwordAttack attack in registeredAttacks)
        {
            if (attack != null && attack.IsOwner)
            {
                PlaySwordSwing();
                return;
            }
        }
    }

    private void RemoveDestroyedObjects()
    {
        List<MechaHealth> healthToRemove = new();

        foreach (var entry in registeredHealth)
        {
            if (entry.Key == null)
                healthToRemove.Add(entry.Key);
        }

        foreach (MechaHealth health in healthToRemove)
            registeredHealth.Remove(health);

        List<MechaMovement> movementsToRemove = new();

        foreach (var entry in dashStates)
        {
            if (entry.Key == null)
                movementsToRemove.Add(entry.Key);
        }

        foreach (MechaMovement movement in movementsToRemove)
            dashStates.Remove(movement);

        registeredAttacks.RemoveWhere(attack => attack == null);
    }

    public void PlayEnemyDeath()
    {
        if (deathSoundPlaying || enemyDeathSound == null)
            return;

        deathSoundPlaying = true;
        StartCoroutine(PlayEnemyDeathDelayed());
    }

    private IEnumerator PlayEnemyDeathDelayed()
    {
        if (enemyDeathDelay > 0f)
            yield return new WaitForSecondsRealtime(enemyDeathDelay);

        PlayEffect(enemyDeathSound, enemyDeathVolume);

        yield return new WaitForSecondsRealtime(enemyDeathSound.length);

        deathSoundPlaying = false;
    }

    public void PlayDash()
    {
        PlayDelayed(dashSound, dashVolume, dashDelay);
    }

    public void PlaySwordSwing()
    {
        PlayDelayed(swordSwingSound, swordSwingVolume, swordSwingDelay);
    }

    public void PlayHit()
    {
        PlayDelayed(hitSound, hitVolume, hitDelay);
    }

    public void PlayEnemyHit()
    {
        PlayDelayed(enemyHitSound, enemyHitVolume, enemyHitDelay);
    }

    private void PlayDelayed(AudioClip clip, float volume, float delay)
    {
        if (clip == null)
            return;

        if (delay <= 0f)
        {
            PlayEffect(clip, volume);
            return;
        }

        StartCoroutine(PlayDelayedCoroutine(clip, volume, delay));
    }

    private IEnumerator PlayDelayedCoroutine(AudioClip clip, float volume, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (clip != null)
            PlayEffect(clip, volume);
    }

    private void PlayEffect(AudioClip clip, float volume)
    {
        if (clip == null || effectsSource == null)
            return;

        effectsSource.PlayOneShot(clip, volume);
    }

    public void SetBackgroundVolume(float volume)
    {
        backgroundVolume = Mathf.Clamp01(volume);

        if (musicSource != null && backgroundStarted)
            musicSource.volume = backgroundVolume;
    }

    public void StopBackground()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void ResumeBackground()
    {
        if (musicSource != null && backgroundStarted && backgroundSound != null)
        {
            musicSource.volume = backgroundVolume;
            musicSource.Play();
        }
    }

    private void OnDestroy()
    {
        foreach (var entry in registeredHealth)
        {
            if (entry.Key == null)
                continue;

            entry.Key.HealthChanged -= entry.Value.healthChangedHandler;
            entry.Key.Death -= entry.Value.deathHandler;
        }

        registeredHealth.Clear();
        dashStates.Clear();
        registeredAttacks.Clear();
        dashSnapshot.Clear();
    }
}
