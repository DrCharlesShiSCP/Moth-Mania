using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class MothFollowerGoalChecker : MonoBehaviour
{
    [Header("References")]
    public MothFlockController flock;         // assign in Inspector
    public EndDoor doorScript;

    [Header("Requirement")]
    [Tooltip("Trigger when follower count >= this value.")]
    public float requiredFollowers = 5f;
    [Tooltip("If true, trigger only once.")]
    public bool checkOnce = true;
    [Tooltip("0 = check every frame. >0 = check on an interval (seconds).")]
    public float pollIntervalSeconds = 0f;

    [Header("Events")]
    public UnityEvent onPassed;                // optional UnityEvent to hook in Inspector

    // internal
    bool hasPassed;
    float nextPollTime;

    private void Start()
    {
        flock = FindAnyObjectByType<MothFlockController>();
    }
    void Update()
    {
        //right now I wrote it to constantly check for amount of moths following the player. This will waste a huge amount of performance. Rewrite later to optimize lol.
        if (flock == null) return;
        if (checkOnce && hasPassed) return;

        // throttle if using interval
        if (pollIntervalSeconds > 0f && Time.time < nextPollTime) return;

        int currentFollowers = TryGetFollowerCount(flock);

        if (currentFollowers >= Mathf.CeilToInt(requiredFollowers))
        {
            if (!hasPassed)
            {
                // fire once
                hasPassed = true;
                CheckPassed();
            }
        }else
        {
            hasPassed = false;
            doorScript.EnoughMoths = false;
        }

        if (pollIntervalSeconds > 0f)
            nextPollTime = Time.time + pollIntervalSeconds;
    }

    /// <summary>
    /// Calls local event then (optionally) calls CheckPassed() on the assigned target via SendMessage.
    /// </summary>
    public void CheckPassed()
    {
        // UnityEvent (Inspector hookups)
        onPassed?.Invoke();
        doorScript.EnoughMoths = true;
        //  Or do anything else here (win condition, open door, etc.)
        Debug.Log("[MothFollowerGoalChecker] CheckPassed triggered.");
    }

    /// <summary>
    /// Attempts to read the current follower count from MothFlockController.
    /// Prefers non-public GetFollowersInOrder() if present, else counts the private 'recalledMoths' HashSet.
    /// Falls back to 0 if neither is available.
    /// </summary>
    int TryGetFollowerCount(MothFlockController controller)
    {
        try
        {
            // Try private method: List<Transform> GetFollowersInOrder()
            MethodInfo getFollowersMethod = typeof(MothFlockController)
                .GetMethod("GetFollowersInOrder", BindingFlags.Instance | BindingFlags.NonPublic);
            if (getFollowersMethod != null)
            {
                var listObj = getFollowersMethod.Invoke(controller, null) as IList;
                if (listObj != null) return listObj.Count;
            }

            // Try private field: HashSet<Transform> recalledMoths
            FieldInfo recalledField = typeof(MothFlockController)
                .GetField("recalledMoths", BindingFlags.Instance | BindingFlags.NonPublic);
            if (recalledField != null)
            {
                var recalled = recalledField.GetValue(controller) as System.Collections.ICollection;
                if (recalled != null) return recalled.Count;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MothFollowerGoalChecker] Reflection read failed: {e.Message}");
        }

        // Couldn¡¯t read ¨C safest fallback
        return 0;
    }
}
