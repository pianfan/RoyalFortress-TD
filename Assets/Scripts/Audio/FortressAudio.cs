using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FortressSound
{
    Click, CardPick, Invalid, Build, Arrow, Mage, Stone, ArrowHit, MagicHit, StoneHit,
    EnemyDeath, Leak, Wave, WaveClear, Victory, Defeat, Boss, Frost, Pulse, Seal
}

// Scene-independent audio. World sounds follow pause; UI and quiet music remain available.
public sealed class FortressAudio : MonoBehaviour
{
    public static FortressAudio Instance { get; private set; }
    public float MusicVolume { get; private set; }
    public float EffectsVolume { get; private set; }
    public string CurrentMusic => music[musicChannel].clip ? music[musicChannel].clip.name : "";
    public int PlayedEffects { get; private set; }
    public int ActiveWorldVoices { get { int count = 0; foreach (var source in world) if (source.isPlaying) count++; return count; } }
    public bool WorldPaused { get; private set; }
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> tracks = new Dictionary<string, AudioClip>();
    private readonly Dictionary<FortressSound, float> nextSound = new Dictionary<FortressSound, float>();
    private readonly AudioSource[] music = new AudioSource[2], ui = new AudioSource[3], world = new AudioSource[12];
    private readonly float[] voiceGain = new float[12];
    private readonly float[] uiGain = new float[3];
    private AudioSource ambience;
    private AudioListener fallbackListener;
    private WaveGameController game;
    private RoyalFortress.ForestSanctuary.ForestSanctuaryGameplay forest;
    private RoyalFortress.GateOfDead.GateOfDeadEncounter gate;
    private int musicChannel, wave, cleared;
    private bool settled, bossPresent, settingsDirty;
    private float blend, scanAt, saveAt, ambienceFade;
    private string desiredMusic;
    private string pendingAmbience;
    private FortressWeatherKind lastWeather;
    private bool hasFocus = true;
    private static readonly string[] ClipNames = { "ui-click", "card-pick", "invalid", "build", "arrow", "mage", "stone", "arrow-hit", "magic-hit", "stone-hit", "enemy-death", "leak", "wave", "wave-clear", "victory", "defeat", "boss", "frost", "pulse", "seal" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() { Instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!Instance) new GameObject("Fortress Audio").AddComponent<FortressAudio>();
    }
    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fortress.Audio.Music", .45f));
        EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Fortress.Audio.Effects", .70f));
        foreach (var clip in Resources.LoadAll<AudioClip>("FortressAudio/Sfx")) clips[clip.name] = clip;
        foreach (var clip in Resources.LoadAll<AudioClip>("FortressAudio/Music")) tracks[clip.name] = clip;
        for (int i = 0; i < music.Length; i++) music[i] = Source("Music " + i, true);
        for (int i = 0; i < ui.Length; i++) ui[i] = Source("UI " + i, false);
        for (int i = 0; i < world.Length; i++) world[i] = Source("World " + i, false);
        ambience = Source("Weather", true);
        fallbackListener = gameObject.AddComponent<AudioListener>();
        fallbackListener.enabled = false;
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private AudioSource Source(string title, bool loop)
    {
        var node = new GameObject(title); node.transform.SetParent(transform, false);
        var source = node.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
        source.volume = 0; source.priority = loop ? 64 : 128;
        return source;
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        if (game) game.DebugWaveJumped -= ResetForDebug;
        game = FindFirstObjectByType<WaveGameController>();
        if (game) game.DebugWaveJumped += ResetForDebug;
        forest = FindFirstObjectByType<RoyalFortress.ForestSanctuary.ForestSanctuaryGameplay>();
        gate = FindFirstObjectByType<RoyalFortress.GateOfDead.GateOfDeadEncounter>();
        wave = cleared = 0; settled = bossPresent = false;
        WorldPaused = false; lastWeather = FortressWeatherKind.Clear;
        foreach (var source in world) source.Stop();
        ambience.Stop(); ambience.clip = null; ambienceFade = 0; pendingAmbience = null;
        nextSound.Clear(); scanAt = 0;
        RefreshListener();
    }
    private void ResetForDebug()
    {
        wave = game.CurrentWave; cleared = game.CompletedWaves;
        settled = bossPresent = false;
        foreach (var source in world) source.Stop();
        ambience.Stop(); ambience.clip = null;
        pendingAmbience = null; ambienceFade = 0; lastWeather = FortressWeatherKind.Clear;
        nextSound.Clear();
    }
    private void RefreshListener()
    {
        bool hasOther = false;
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener != fallbackListener && listener.isActiveAndEnabled) { hasOther = true; break; }
        fallbackListener.enabled = !hasOther;
    }
    private void Update()
    {
        bool paused = game && game.IsPaused;
        if (paused != WorldPaused)
        {
            WorldPaused = paused;
            // Stop short combat transients; keep the loop's playback position when paused.
            if (paused) { foreach (var source in world) source.Stop(); ambience.Pause(); }
            else if (hasFocus) ambience.UnPause();
        }
        if (Time.unscaledTime >= scanAt)
        {
            scanAt = Time.unscaledTime + .5f;
            RefreshListener();
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!button.GetComponent<FortressButtonSound>()) button.gameObject.AddComponent<FortressButtonSound>();
        }
        bool boss = game && FortressWeatherController.Instance && FortressWeatherController.Instance.CurrentBoss;
        if (game && !paused)
        {
            if (game.CurrentWave > wave) { wave = game.CurrentWave; Play(FortressSound.Wave); }
            if (game.CompletedWaves > cleared)
            {
                cleared = game.CompletedWaves;
                if (!game.AllWavesFinished) Play(FortressSound.WaveClear);
            }
            if (boss && !bossPresent) Play(FortressSound.Boss);
            bossPresent = boss;
            bool victory = game.AllWavesFinished && (!forest || forest.VictoryFinished) && (!gate || gate.VictoryFinished);
            if (!settled && (game.IsGameOver || victory))
            {
                settled = true; Play(game.IsGameOver ? FortressSound.Defeat : FortressSound.Victory);
            }
        }
        string track = !game || !game.IsWaveActive || game.IsGameOver ? "menu" : boss ? "boss" : "battle";
        ChangeMusic(track);
        blend = Mathf.MoveTowards(blend, 1, Time.unscaledDeltaTime * .8f);
        float musicGain = MusicVolume * (paused ? .35f : 1) * (hasFocus ? 1 : 0) * .65f;
        music[musicChannel].volume = musicGain * blend;
        music[1-musicChannel].volume = musicGain * (1-blend);
        if (blend >= 1 && music[1-musicChannel].isPlaying) music[1-musicChannel].Stop();
        var weather = game && !game.IsGameOver && FortressWeatherController.Instance
            ? FortressWeatherController.Instance.Weather : FortressWeatherKind.Clear;
        if (weather != lastWeather)
        {
            lastWeather = weather;
            if (weather == FortressWeatherKind.Snow) Play(FortressSound.Frost);
            pendingAmbience = weather == FortressWeatherKind.Snow ? "snow-wind" : weather == FortressWeatherKind.Sandstorm ? "sand-wind" : null;
        }
        UpdateAmbience(paused);
        if (settingsDirty && Time.unscaledTime >= saveAt) SaveSettings();
    }
    private void ChangeMusic(string name)
    {
        if (desiredMusic == name) return;
        desiredMusic = name;
        if (!tracks.TryGetValue(name, out var clip)) return;
        musicChannel = 1-musicChannel; blend = 0;
        music[musicChannel].clip = clip; music[musicChannel].volume = 0;
        music[musicChannel].Play();
    }
    private void UpdateAmbience(bool paused)
    {
        string playing = ambience.clip ? ambience.clip.name : null;
        bool same = playing == pendingAmbience;
        ambienceFade = Mathf.MoveTowards(ambienceFade, same && pendingAmbience != null ? 1 : 0, Time.unscaledDeltaTime * 1.5f);
        if (!same && ambienceFade <= 0)
        {
            ambience.Stop(); ambience.clip = null;
            if (pendingAmbience != null && clips.TryGetValue(pendingAmbience, out var clip))
            {
                ambience.clip = clip;
                ambience.Play();
                if (paused || !hasFocus) ambience.Pause();
            }
        }
        ambience.volume = EffectsVolume * ambienceFade * .27f;
    }
    public static void Play(FortressSound sound, Vector3? position = null)
    {
        if (Instance) Instance.PlayEffect(sound, position);
    }
    private void PlayEffect(FortressSound sound, Vector3? position)
    {
        bool isUI = sound == FortressSound.Click || sound == FortressSound.Invalid || sound == FortressSound.CardPick;
        if (!hasFocus || EffectsVolume <= 0 || (!isUI && game && game.IsPaused)) return;
        float now = Time.unscaledTime;
        if (nextSound.TryGetValue(sound, out var next) && now < next) return;
        float cooldown = sound >= FortressSound.Arrow && sound <= FortressSound.StoneHit ? .09f : sound == FortressSound.EnemyDeath ? .16f : .08f;
        nextSound[sound] = now + cooldown;
        if (!clips.TryGetValue(ClipNames[(int)sound], out var clip)) return;
        var pool = isUI ? ui : world;
        int index = -1;
        for (int i = 0; i < pool.Length; i++) if (!pool[i].isPlaying) { index = i; break; }
        if (index < 0)
        {
            if (isUI || sound < FortressSound.Leak) return;
            index = 0; pool[index].Stop();
        }
        float gain = isUI ? .28f : sound >= FortressSound.Arrow && sound <= FortressSound.EnemyDeath ? .20f : .48f;
        float pan = 0;
        if (position.HasValue && Camera.main)
        {
            var point = Camera.main.WorldToViewportPoint(position.Value);
            pan = Mathf.Clamp((point.x-.5f)*1.2f, -.65f, .65f);
            if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) gain *= .35f;
        }
        if (isUI) uiGain[index] = gain; else voiceGain[index] = gain;
        var source = pool[index]; source.clip = clip; source.volume = EffectsVolume * gain;
        source.panStereo = pan;
        source.pitch = sound >= FortressSound.Arrow && sound <= FortressSound.EnemyDeath ? Random.Range(.94f, 1.06f) : 1;
        source.Play(); PlayedEffects++;
    }
    public void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); SettingsChanged(); }
    public void SetEffectsVolume(float value)
    {
        EffectsVolume = Mathf.Clamp01(value);
        for (int i = 0; i < world.Length; i++) world[i].volume = EffectsVolume * voiceGain[i];
        for (int i = 0; i < ui.Length; i++) ui[i].volume = EffectsVolume * uiGain[i];
        ambience.volume = EffectsVolume * ambienceFade * .27f;
        SettingsChanged();
    }
    private void SettingsChanged()
    {
        PlayerPrefs.SetFloat("Fortress.Audio.Music", MusicVolume);
        PlayerPrefs.SetFloat("Fortress.Audio.Effects", EffectsVolume);
        settingsDirty = true; saveAt = Time.unscaledTime + .5f;
    }
    private void SaveSettings() { if (settingsDirty) PlayerPrefs.Save(); settingsDirty = false; }
    private void OnApplicationFocus(bool focus)
    {
        hasFocus = focus;
        if (!focus) { foreach (var source in world) source.Stop(); foreach (var source in ui) source.Stop(); ambience.Pause(); SaveSettings(); }
        else if (!WorldPaused) ambience.UnPause();
    }
    private void OnApplicationQuit() { SaveSettings(); }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (game) game.DebugWaveJumped -= ResetForDebug;
        SaveSettings();
        if (Instance == this) Instance = null;
    }
}
