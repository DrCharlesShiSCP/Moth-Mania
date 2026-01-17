using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CollectedMoth : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip collectClip;
    [Range(0f, 1f)] public float volume = 0.8f;
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    bool initialized = false;

    public void Init()
    {
        if (initialized) return;
        initialized = true;

        PlayCollectSound();
    }

    void PlayCollectSound()
    {
        AudioSource source = FindPlayerAudioSource();
        if (source == null || collectClip == null) return;

        float prevPitch = source.pitch;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(collectClip, volume);
        source.pitch = prevPitch;
    }

    AudioSource FindPlayerAudioSource()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return null;

        return player.GetComponentInChildren<AudioSource>();
    }
}
