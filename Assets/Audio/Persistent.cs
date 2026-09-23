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
    public AudioClip victoryClip;
    public AudioClip cajaClip;
    public AudioClip checkpointClip;
    public AudioClip miniClip;
    public AudioClip grandeClip;
    public AudioClip botonClip;
    public AudioClip introClip;
    public AudioClip musicClip;

    [Header("Configuración")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    private bool isInitialized = false;
    private float lastDeathSoundTime = -10f;
    private float lastButtonSoundTime = -10f;
    private float nextButtonScanTime = 0f;

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

        RegistrarBotonesEscena();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextButtonScanTime)
        {
            nextButtonScanTime = Time.unscaledTime + 0.5f;
            RegistrarBotonesEscena();
        }
    }

    public void RegistrarBotonesEscena()
    {
        UnityEngine.UI.Button[] botones = FindObjectsOfType<UnityEngine.UI.Button>(true);
        foreach (UnityEngine.UI.Button btn in botones)
        {
            if (btn == null) continue;
            btn.onClick.RemoveListener(ReproducirBoton);
            btn.onClick.AddListener(ReproducirBoton);
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

        if (victoryClip == null)
        {
            victoryClip = Resources.Load<AudioClip>("sonido victoria");
        }

        if (victoryClip != null)
        {
            victoryClip.LoadAudioData();
        }

        if (cajaClip == null)
        {
            cajaClip = Resources.Load<AudioClip>("sonido caja");
        }

        if (cajaClip != null)
        {
            cajaClip.LoadAudioData();
        }

        if (checkpointClip == null)
        {
            checkpointClip = Resources.Load<AudioClip>("sonido checkpoint");
        }

        if (checkpointClip != null)
        {
            checkpointClip.LoadAudioData();
        }

        if (miniClip == null)
        {
            miniClip = Resources.Load<AudioClip>("sonido mini");
        }

        if (miniClip != null)
        {
            miniClip.LoadAudioData();
        }

        if (grandeClip == null)
        {
            grandeClip = Resources.Load<AudioClip>("sonido grande");
        }

        if (grandeClip != null)
        {
            grandeClip.LoadAudioData();
        }

        if (botonClip == null)
        {
            botonClip = Resources.Load<AudioClip>("sonido boton");
        }

        if (botonClip != null)
        {
            botonClip.LoadAudioData();
        }

        if (introClip == null)
        {
            introClip = Resources.Load<AudioClip>("sonido intro");
        }

        if (introClip != null)
        {
            introClip.LoadAudioData();
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

    public void ReproducirVictoria(bool pararMusica = true)
    {
        EnsureInitialized();

        if (pararMusica && musicSource != null)
        {
            musicSource.Stop();
        }

        if (victoryClip == null)
        {
            victoryClip = Resources.Load<AudioClip>("sonido victoria");
            if (victoryClip != null) victoryClip.LoadAudioData();
        }

        if (victoryClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(victoryClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(victoryClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(victoryClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip de victoria (victoryClip).");
        }
    }

    public void ReproducirVictoria()
    {
        ReproducirVictoria(true);
    }

    public void ReproducirCaja()
    {
        EnsureInitialized();

        if (cajaClip == null)
        {
            cajaClip = Resources.Load<AudioClip>("sonido caja");
            if (cajaClip != null) cajaClip.LoadAudioData();
        }

        if (cajaClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(cajaClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(cajaClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(cajaClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip de caja (cajaClip).");
        }
    }

    public void ReproducirCheckpoint()
    {
        EnsureInitialized();

        if (checkpointClip == null)
        {
            checkpointClip = Resources.Load<AudioClip>("sonido checkpoint");
            if (checkpointClip != null) checkpointClip.LoadAudioData();
        }

        if (checkpointClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(checkpointClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(checkpointClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(checkpointClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip de checkpoint (checkpointClip).");
        }
    }

    public void ReproducirMini()
    {
        EnsureInitialized();

        if (miniClip == null)
        {
            miniClip = Resources.Load<AudioClip>("sonido mini");
            if (miniClip != null) miniClip.LoadAudioData();
        }

        if (miniClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(miniClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(miniClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(miniClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip mini (miniClip).");
        }
    }

    public void ReproducirGrande()
    {
        EnsureInitialized();

        if (grandeClip == null)
        {
            grandeClip = Resources.Load<AudioClip>("sonido grande");
            if (grandeClip != null) grandeClip.LoadAudioData();
        }

        if (grandeClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(grandeClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(grandeClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(grandeClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
        else
        {
            Debug.LogWarning("[PersistentMusic] No se encontró el clip grande (grandeClip).");
        }
    }

    public void ReproducirBoton()
    {
        EnsureInitialized();

        if (Time.unscaledTime - lastButtonSoundTime < 0.08f) return;
        lastButtonSoundTime = Time.unscaledTime;

        if (botonClip == null)
        {
            botonClip = Resources.Load<AudioClip>("sonido boton");
            if (botonClip != null) botonClip.LoadAudioData();
        }

        if (botonClip != null)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(botonClip);
            }
            else if (musicSource != null)
            {
                musicSource.PlayOneShot(botonClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(botonClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
    }

    public void ReproducirIntro()
    {
        EnsureInitialized();

        if (introClip == null)
        {
            introClip = Resources.Load<AudioClip>("sonido intro");
            if (introClip != null) introClip.LoadAudioData();
        }

        if (musicSource != null && introClip != null)
        {
            if (musicSource.clip != introClip || !musicSource.isPlaying)
            {
                musicSource.clip = introClip;
                musicSource.volume = musicVolume;
                musicSource.loop = true;
                musicSource.Play();
            }
        }
    }

    public void ReiniciarMusicaAmbiente()
    {
        EnsureInitialized();

        if (musicClip == null)
        {
            musicClip = Resources.Load<AudioClip>("sonido juego");
            if (musicClip != null) musicClip.LoadAudioData();
        }

        if (musicSource != null && musicClip != null)
        {
            if (musicSource.clip != musicClip || !musicSource.isPlaying)
            {
                musicSource.clip = musicClip;
                musicSource.volume = musicVolume;
                musicSource.loop = true;
                musicSource.Play();
            }
        }
    }
}