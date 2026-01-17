using UnityEngine;

public class SimpleCameraShake : MonoBehaviour
{
    public static SimpleCameraShake Instance;

    [Header("Tuning")]
    public float frequency = 25f; // how “jittery” the shake is

    Vector3 _originalLocalPos;
    float _timeLeft;
    float _amplitude;

    void Awake()
    {
        Instance = this;
        _originalLocalPos = transform.localPosition;
    }

    void LateUpdate()
    {
        if (_timeLeft > 0f)
        {
            _timeLeft -= Time.deltaTime;

            float t = Time.time * frequency;
            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f,
                0f
            ) * _amplitude;

            transform.localPosition = _originalLocalPos + offset;
        }
        else
        {
            transform.localPosition = _originalLocalPos;
        }
    }

    public void Shake(float amplitude, float duration)
    {
        // If multiple shakes overlap, keep the stronger/longer one
        _amplitude = Mathf.Max(_amplitude, amplitude);
        _timeLeft = Mathf.Max(_timeLeft, duration);
    }
}
