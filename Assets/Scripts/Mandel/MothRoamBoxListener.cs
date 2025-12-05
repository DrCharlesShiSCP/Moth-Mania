using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MothRoamBoxListener : MonoBehaviour
{
    bool insideRoamBox = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(MothFlockController.Instance.roamBoxTag))
        {
            insideRoamBox = true;
            MothFlockController.Instance.SetMothInsideRoamBox(transform, true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(MothFlockController.Instance.roamBoxTag))
        {
            insideRoamBox = false;
            MothFlockController.Instance.SetMothInsideRoamBox(transform, false);
        }
    }
}
