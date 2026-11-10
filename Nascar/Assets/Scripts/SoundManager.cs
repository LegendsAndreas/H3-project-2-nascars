using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public AudioSource MusicSource;
    public AudioSource SfxSource;
    public AudioClip BackgroundMusic;
    public AudioClip CarStartUpSound;
    [Range(0f, 1f)] public float MusicVolume = 0.5f;
    [Range(0f, 1f)] public float SfxVolume = 1f;
    
    void Start()
    {
        MusicSource.clip = BackgroundMusic;
        MusicSource.loop = true;
    }
    
    void Update()
    {
        MusicSource.volume = MusicVolume;
        SfxSource.volume = SfxVolume;
        
        if (MusicSource.isPlaying)
        {
            return;
        }

        MusicSource.Play();
    }
    
    public void PlayCarStartUpSound()
    {
        Debug.Log("Playing car startup sound.");
        SfxSource.PlayOneShot(CarStartUpSound);
    }
}
