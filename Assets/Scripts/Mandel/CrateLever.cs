using UnityEngine;
using System.Collections;

public class CrateLever : MonoBehaviour
{


    public enum Axis { X, Y, Z }


    [Header("Roam Box Control")]
    public GameObject roamBoxObject;    // assign the MothRoamBox object here
    public bool toggleRoamBox = true;   // use toggle behavior
    public bool initialState = true;    // optional: what state should it start in


    [Header("Activation")]
    public string crateTag = "Crate";
    public bool allowRetrigger = false;

    [Header("Lever Rotation")]
    public Axis rotationAxis = Axis.X;  // <<< choose X, Y, or Z
    public float rotationAngle = 60f;   // degrees
    public float animationTime = 0.3f;

    private bool _activated = false;
    private bool _isAnimating = false;

    private Quaternion _initialRot;
    private Quaternion _targetRot;

    void Start()
    {
        _initialRot = transform.localRotation;
        _targetRot = GetTargetRotation();

        if (roamBoxObject != null)
            roamBoxObject.SetActive(initialState);
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.collider.CompareTag(crateTag))
            TriggerLever();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(crateTag))
            TriggerLever();
    }

    void TriggerLever()
    {
        if (_activated && !allowRetrigger) return;
        if (_isAnimating) return;

        _activated = true;

        // NEW: control the RoamBox
        if (roamBoxObject != null)
        {
            if (toggleRoamBox)
            {
                // toggle ON/OFF each time lever is hit
                roamBoxObject.SetActive(!roamBoxObject.activeSelf);
            }
            else
            {
                // one-shot: turn it off when lever is activated
                roamBoxObject.SetActive(false);
            }
        }

        StartCoroutine(RotateLever());
    }



    Quaternion GetTargetRotation()
    {
        Vector3 euler = transform.localEulerAngles;

        switch (rotationAxis)
        {
            case Axis.X: euler.x += rotationAngle; break;
            case Axis.Y: euler.y += rotationAngle; break;
            case Axis.Z: euler.z += rotationAngle; break;
        }

        return Quaternion.Euler(euler);
    }

    IEnumerator RotateLever()
    {
        _isAnimating = true;

        Quaternion start = transform.localRotation;
        Quaternion end = _targetRot;

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / animationTime;
            transform.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        transform.localRotation = end;

        _isAnimating = false;
    }
}
