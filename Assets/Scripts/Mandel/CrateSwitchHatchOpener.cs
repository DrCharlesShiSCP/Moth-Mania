using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CrateSwitchHatchOpener : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Who can activate the switch?")]
    public string crateTag = "Crate";

    [Header("Hatch References (pivot or mesh)")]
    public Transform leftHatch;
    public Transform rightHatch;

    [Header("Scaling Axis Settings")]
    public Axis scaleAxis = Axis.X;      // <<< choose which axis to scale along
    public float closedValue = 1f;       // scale value on that axis when closed
    public float openValue = 0f;         // scale value on that axis when open

    [Header("Animation")]
    public float animationDuration = 0.5f;
    public bool toggleOnEachHit = false;

    bool _isOpen = false;
    bool _isAnimating = false;

    Vector3 _leftInitialScale;
    Vector3 _rightInitialScale;

    void Start()
    {
        if (leftHatch != null)
            _leftInitialScale = leftHatch.localScale;

        if (rightHatch != null)
            _rightInitialScale = rightHatch.localScale;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(crateTag)) return;
        if (_isAnimating) return;

        if (toggleOnEachHit)
        {
            StartCoroutine(AnimateHatches(!_isOpen));
        }
        else
        {
            if (!_isOpen)
                StartCoroutine(AnimateHatches(true));
        }
    }

    IEnumerator AnimateHatches(bool open)
    {
        _isAnimating = true;

        float startVal = open ? closedValue : openValue;
        float endVal = open ? openValue : closedValue;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / animationDuration;
            float val = Mathf.Lerp(startVal, endVal, Mathf.Clamp01(t));

            if (leftHatch != null)
                leftHatch.localScale = ApplyAxisScale(_leftInitialScale, val);

            if (rightHatch != null)
                rightHatch.localScale = ApplyAxisScale(_rightInitialScale, val);

            yield return null;
        }

        // Snap final
        if (leftHatch != null)
            leftHatch.localScale = ApplyAxisScale(_leftInitialScale, endVal);

        if (rightHatch != null)
            rightHatch.localScale = ApplyAxisScale(_rightInitialScale, endVal);

        _isOpen = open;
        _isAnimating = false;
    }

    Vector3 ApplyAxisScale(Vector3 baseScale, float axisValue)
    {
        Vector3 s = baseScale;

        switch (scaleAxis)
        {
            case Axis.X: s.x = axisValue; break;
            case Axis.Y: s.y = axisValue; break;
            case Axis.Z: s.z = axisValue; break;
        }

        return s;
    }
}
