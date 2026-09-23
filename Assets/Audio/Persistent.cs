using UnityEngine;

public class PersistentMusic : MonoBehaviour
{
    private static PersistentMusic instance;
    public static PersistentMusic Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PersistentMusic>();
                if (instance == null)
                {
                    GameObject go = new GameObject("Audio");
                    instance = go.AddComponent<PersistentMusic>();
                }
            }
            instance.EnsureInitialized();
            return instance;
        }
        set
        {
            instance = value;
        }
    }

    [Header("Referencias de Audio")]
    public AudioSource musicSource;
    public AudioSource sfxSource;
    public AudioClip deathClip;
    public AudioClip musicClip;

    [Header("Configuración")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    private bool isInitialized = false;
    private float lastDeathSoundTime = -10f;

    void Awake()
    {
        transform.parent = null;

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }
    }

    public void EnsureInitialized()
    {
        if (isInitialized && musicSource != null && sfxSource != null) return;

        AudioSource[] sources = GetComponents<AudioSource>();

        // 1. Configurar Fuente de Música de fondo
        if (musicSource == null)
        {
            if (sources.Length > 0)
            {
                musicSource = sources[0];
            }
            else
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        musicSource.spatialBlend = 0f; // Audio 2D global
        musicSource.loop = true;

        // 2. Configurar Fuente de Efectos (SFX)
        if (sfxSource == null)
        {
            sources = GetComponents<AudioSource>();
            foreach (AudioSource src in sources)
            {
                if (src != musicSource)
                {
                    sfxSource = src;
                    break;
                }
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D global para escucharse con nitidez en cualquier posición
        sfxSource.ignoreListenerPause = true; // Permite sonar si el listener o juego pausa
        sfxSource.volume = sfxVolume;
        sfxSource.pitch = 1f;
        sfxSource.mute = false;

        // 3. Cargar clips si faltan
        if (deathClip == null)
        {
            deathClip = Resources.Load<AudioClip>("sonido muerte");
        }

        if (deathClip != null)
        {
            deathClip.LoadAudioData();
        }

        if (musicClip == null && musicSource.clip == null)
        {
            musicClip = Resources.Load<AudioClip>("sonido juego");
            if (musicClip != null)
            {
                musicSource.clip = musicClip;
            }
        }

        if (musicSource.clip != null)
        {
            musicSource.clip.LoadAudioData();
        }

        isInitialized = true;
    }

    public void ReproducirMuerte(bool pararMusica = true)
    {
        EnsureInitialized();

        // Evitar múltiples reproducciones consecutivas en menos de 200 ms (debounce)
        if (Time.unscaledTime - lastDeathSoundTime < 0.2f) return;
        lastDeathSoundTime = Time.unscaledTime;

        if (pararMusica && musicSource != null)
        {
            musicSource.Stop();
        }

        if (deathClip == null)
        {
            deathClip = Resources.Load<AudioClip>("sonido muerte");
            if (deathClip != null) deathClip.LoadAudioData();
        }

        if (deathClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(deathClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(deathClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(deathClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip de muerte (deathClip).");
        }
    }

    public void ReproducirMuerte()
    {
        ReproducirMuerte(true);
    }

    public void ReiniciarMusicaAmbiente()
    {
        EnsureInitialized();

        if (musicSource != null && !musicSource.isPlaying)
        {
            if (musicSource.clip == null && musicClip != null)
            {
                musicSource.clip = musicClip;
            }
            musicSource.volume = musicVolume;
            musicSource.loop = true;
            musicSource.Play();
        }
    }
}