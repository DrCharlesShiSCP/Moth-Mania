using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class ProceduralGlowSweepTexture : MonoBehaviour
{
    [Header("Texture")]
    [SerializeField] private int width = 512;
    [SerializeField] private int height = 64;

    [Header("Glow Band")]
    [Tooltip("Where the bright band is centered (0..1 across the texture).")]
    [Range(0f, 1f)][SerializeField] private float bandCenter01 = 0.5f;

    [Tooltip("Band width as fraction of texture width (0..1).")]
    [Range(0.01f, 0.5f)][SerializeField] private float bandWidth01 = 0.12f;

    [Tooltip("Softness of the falloff (0..1). Higher = softer edges.")]
    [Range(0.01f, 1f)][SerializeField] private float softness01 = 0.35f;

    [Tooltip("Overall brightness of the band.")]
    [Range(0f, 10f)][SerializeField] private float intensity = 2.0f;

    [Header("Material Property")]
    [SerializeField] private string textureProperty = "_MainTex";

    private Texture2D _tex;
    private Renderer _r;
    private Material _matInstance;

    void OnEnable()
    {
        _r = GetComponent<Renderer>();
        // Instance material so we don't overwrite shared material asset
        _matInstance = Application.isPlaying ? _r.material : _r.sharedMaterial;

        Rebuild();
    }

    void OnDisable()
    {
        if (Application.isPlaying)
        {
            if (_tex != null) Destroy(_tex);
        }
        else
        {
            if (_tex != null) DestroyImmediate(_tex);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Rebuild();
    }
#endif

    [ContextMenu("Rebuild Texture")]
    public void Rebuild()
    {
        if (_matInstance == null) return;

        width = Mathf.Clamp(width, 32, 4096);
        height = Mathf.Clamp(height, 4, 1024);

        if (_tex == null || _tex.width != width || _tex.height != height)
        {
            if (_tex != null)
            {
                if (Application.isPlaying) Destroy(_tex);
                else DestroyImmediate(_tex);
            }

            _tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            _tex.name = "ProceduralGlowSweep";
            _tex.wrapMode = TextureWrapMode.Repeat;   // IMPORTANT for looping
            _tex.filterMode = FilterMode.Bilinear;
        }

        // Build a horizontal 1D glow band and copy vertically
        // We use smoothstep falloff for soft edges.
        float centerPx = bandCenter01 * (width - 1);
        float halfWidthPx = Mathf.Max(1f, bandWidth01 * width * 0.5f);

        // softness controls how wide the falloff region is relative to band half-width
        float falloffPx = Mathf.Max(1f, softness01 * halfWidthPx);

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs(x - centerPx);

                // Core band: inside halfWidthPx is bright; outside fades to 0 over falloffPx
                float t = Mathf.InverseLerp(halfWidthPx + falloffPx, halfWidthPx, d);
                // Smoothstep
                t = t * t * (3f - 2f * t);

                float a = Mathf.Clamp01(t) * Mathf.Clamp(intensity, 0f, 10f);

                // White band with alpha = strength (best for Additive blending)
                pixels[y * width + x] = new Color(a, a, a, a);
            }
        }

        _tex.SetPixels(pixels);
        _tex.Apply(false, false);

        _matInstance.SetTexture(textureProperty, _tex);
    }
}
