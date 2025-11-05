using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using TMPro;

public class MothFollowerHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your existing MothFlockController here.")]
    public MothFlockController flock;

    [Tooltip("TMP Text in the scene to display 'current/total'.")]
    public TMP_Text counterText;

    [Header("Display")]
    [Tooltip("Text format. {0}=current followers, {1}=total moths.")]
    public string displayFormat = "{0}/{1}";

    [Header("Totals")]
    [Tooltip("If true, use flock.mothBabies.Count for total when available; otherwise scan by tag.")]
    public bool preferFlockListForTotal = true;

    [Tooltip("Fallback tag to count total moths if flock list is empty or not used.")]
    public string babiesTag = "babies";

    [Header("Performance")]
    [Tooltip("How often (seconds) to refresh the TOTAL count (per frame).")]
    public float totalRefreshInterval = 10.0f;

    // cache for reflection
    FieldInfo recalledField;
    MethodInfo getFollowersInOrderMethod;

    int cachedTotal = -1;
    float nextTotalRefreshTime = 0f;

    void Awake()
    {
        // Prepare reflection once
        var t = typeof(MothFlockController);
        // Prefer direct access to recalledMoths (followers set)
        recalledField = t.GetField("recalledMoths", BindingFlags.Instance | BindingFlags.NonPublic);
        // Keep a backup to call GetFollowersInOrder() if field not found
        getFollowersInOrderMethod = t.GetMethod("GetFollowersInOrder", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void Start()
    {
        flock = FindAnyObjectByType<MothFlockController>();

    }
    void Update()
    {
        if (flock == null || counterText == null) return;

        // 1) Current followers (count the set of followers actively following player)
        int currentFollowers = GetCurrentFollowerCount();

        // 2) Total moths (from flock list if available, else a tag scan)
        if (Time.time >= nextTotalRefreshTime || cachedTotal < 0)
        {
            cachedTotal = GetTotalMothCount();
            nextTotalRefreshTime = Time.time + Mathf.Max(0.05f, totalRefreshInterval);
        }

        // 3) Update UI
        counterText.text = "Mothlettes:" + string.Format(displayFormat, currentFollowers, Mathf.Max(0, cachedTotal));
    }

    int GetCurrentFollowerCount()
    {
        // Try direct field: HashSet<Transform> recalledMoths
        if (recalledField != null)
        {
            var recalled = recalledField.GetValue(flock) as System.Collections.ICollection;
            if (recalled != null) return recalled.Count;
        }

        // Fallback: private List<Transform> GetFollowersInOrder()
        if (getFollowersInOrderMethod != null)
        {
            var listObj = getFollowersInOrderMethod.Invoke(flock, null) as System.Collections.IList;
            if (listObj != null) return listObj.Count;
        }

        // Couldn¡¯t read ¨C safest fallback
        return 0;
    }

    int GetTotalMothCount()
    {
        // Prefer flock list if requested and available
        if (preferFlockListForTotal && flock.mothBabies != null && flock.mothBabies.Count > 0)
            return flock.mothBabies.Count;

        // Otherwise count by tag in scene
        if (!string.IsNullOrEmpty(babiesTag))
            return GameObject.FindGameObjectsWithTag(babiesTag).Length;

        return 0;
    }
}
