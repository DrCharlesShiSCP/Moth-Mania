using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;

public class MothFollowerHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your existing MothFlockController here (optional, auto-finds if empty).")]
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
    [Tooltip("How often (seconds) to refresh the TOTAL count.")]
    public float totalRefreshInterval = 1.0f;

    [Header("Audio – Play on follower increase (collection)")]
    [Tooltip("AudioSource to play from. If empty, auto-finds one on the Player.")]
    public AudioSource audioSource;

    [Tooltip("Sound to play when the follower/collected count increases.")]
    public AudioClip followerIncreaseClip;

    [Range(0f, 1f)]
    public float followerIncreaseVolume = 0.8f;

    public Vector2 followerIncreasePitchRange = new Vector2(0.95f, 1.05f);

    [Header("Debug")]
    public bool logDebug = false;

    // ---------------------------
    // Level 1 Exit Direction Indicator
    // ---------------------------
    [Header("Exit Direction Indicator (Level 1)")]
    [Tooltip("Assign your ExitDirectionIndicatorUI here. It will be enabled when all mothlettes are collected.")]
    public ExitDirectionIndicatorUI exitIndicator;

    [Tooltip("Enable the exit indicator when complete.")]
    public bool enableExitIndicatorOnComplete = true;

    // ---------------------------
    // Optional Completion Popup Hint
    // ---------------------------
    [Header("Completion Hint UI (Optional)")]
    public CanvasGroup completionHintPanel;
    public TMP_Text completionHintText;

    [TextArea]
    public string completionMessage = "All mothlettes collected!\nHead back to the Exit.";

    public bool showCompletionPopupOnlyOnce = true;
    public float popupShowDelay = 0.15f;
    public float popupAutoHideAfter = 0f; // 0 = never

    // reflection into flock
    FieldInfo recalledField;
    MethodInfo getFollowersInOrderMethod;

    int cachedTotal = -1;
    float nextTotalRefreshTime = 0f;

    bool completionLatched = false;
    bool completionPopupShown = false;
    Coroutine popupRoutine;

    int lastFollowers = -1;

    void Awake()
    {
        var t = typeof(MothFlockController);
        recalledField = t.GetField("recalledMoths", BindingFlags.Instance | BindingFlags.NonPublic);
        getFollowersInOrderMethod = t.GetMethod("GetFollowersInOrder", BindingFlags.Instance | BindingFlags.NonPublic);

        // Start hidden
        if (completionHintPanel != null)
        {
            completionHintPanel.alpha = 0f;
            completionHintPanel.interactable = false;
            completionHintPanel.blocksRaycasts = false;
        }

        // Start disabled
        if (exitIndicator != null)
            exitIndicator.SetEnabled(false);
    }

    void Start()
    {
        if (flock == null)
            flock = FindAnyObjectByType<MothFlockController>();

        if (completionHintText != null)
            completionHintText.text = completionMessage;

        // Auto-find an AudioSource on the Player if not assigned
        if (audioSource == null)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                audioSource = playerGO.GetComponentInChildren<AudioSource>();
        }
    }

    void Update()
    {
        if (flock == null || counterText == null) return;

        int currentFollowers = GetCurrentFollowerCount();

        // 🔊 Play sound ONLY when follower count increases (collection moment)
        if (lastFollowers >= 0 && currentFollowers > lastFollowers)
        {
            PlayFollowerIncreaseSound();
        }

        // Refresh total immediately if follower count changes (prevents stale totals)
        if (currentFollowers != lastFollowers)
        {
            cachedTotal = GetTotalMothCount();
            nextTotalRefreshTime = Time.time + Mathf.Max(0.05f, totalRefreshInterval);
        }
        else if (Time.time >= nextTotalRefreshTime || cachedTotal < 0)
        {
            cachedTotal = GetTotalMothCount();
            nextTotalRefreshTime = Time.time + Mathf.Max(0.05f, totalRefreshInterval);
        }

        // Update lastFollowers AFTER we used it for comparisons
        lastFollowers = currentFollowers;

        int total = Mathf.Max(0, cachedTotal);

        // Update counter UI
        counterText.text = "Mothlettes:" + string.Format(displayFormat, currentFollowers, total);

        // Completion detect (guard against total=0)
        bool isComplete = (total > 0 && currentFollowers >= total);

        if (logDebug)
            Debug.Log($"[MothFollowerHUD] followers={currentFollowers} total={total} complete={isComplete} latched={completionLatched}");

        if (isComplete && !completionLatched)
        {
            completionLatched = true;
            OnAllMothlettesCollected();
        }
    }

    void PlayFollowerIncreaseSound()
    {
        // Helpful debug that will show even if clip is missing
        if (logDebug)
            Debug.Log($"[MothFollowerHUD] Follower increased -> trying SFX. audioSource={(audioSource != null)} clip={(followerIncreaseClip != null)}");

        if (audioSource == null || followerIncreaseClip == null) return;

        float prevPitch = audioSource.pitch;
        audioSource.pitch = Random.Range(followerIncreasePitchRange.x, followerIncreasePitchRange.y);
        audioSource.PlayOneShot(followerIncreaseClip, followerIncreaseVolume);
        audioSource.pitch = prevPitch;

        if (logDebug)
            Debug.Log("[MothFollowerHUD] Playing follower increase sound (PlayOneShot fired).");
    }

    void OnAllMothlettesCollected()
    {
        if (logDebug)
            Debug.Log("[MothFollowerHUD] All mothlettes collected -> enabling exit indicator + showing popup");

        // Enable Level 1 exit arrow
        if (enableExitIndicatorOnComplete && exitIndicator != null)
            exitIndicator.SetEnabled(true);

        // Optional popup
        if (completionHintPanel == null) return;
        if (showCompletionPopupOnlyOnce && completionPopupShown) return;

        completionPopupShown = true;

        if (completionHintText != null)
            completionHintText.text = completionMessage;

        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(ShowPopupRoutine());
    }

    IEnumerator ShowPopupRoutine()
    {
        if (popupShowDelay > 0f)
            yield return new WaitForSeconds(popupShowDelay);

        // Ensure active, then show
        if (!completionHintPanel.gameObject.activeSelf)
            completionHintPanel.gameObject.SetActive(true);

        completionHintPanel.alpha = 1f;
        completionHintPanel.interactable = true;
        completionHintPanel.blocksRaycasts = true;

        if (popupAutoHideAfter > 0f)
        {
            yield return new WaitForSeconds(popupAutoHideAfter);
            completionHintPanel.alpha = 0f;
            completionHintPanel.interactable = false;
            completionHintPanel.blocksRaycasts = false;
        }
    }

    int GetCurrentFollowerCount()
    {
        // Try direct field: recalledMoths
        if (recalledField != null)
        {
            var recalled = recalledField.GetValue(flock) as System.Collections.ICollection;
            if (recalled != null) return recalled.Count;
        }

        // Fallback: GetFollowersInOrder()
        if (getFollowersInOrderMethod != null)
        {
            var listObj = getFollowersInOrderMethod.Invoke(flock, null) as System.Collections.IList;
            if (listObj != null) return listObj.Count;
        }

        return 0;
    }

    int GetTotalMothCount()
    {
        // Prefer flock list if available
        if (preferFlockListForTotal && flock.mothBabies != null && flock.mothBabies.Count > 0)
            return flock.mothBabies.Count;

        // Otherwise count by tag
        if (!string.IsNullOrEmpty(babiesTag))
            return GameObject.FindGameObjectsWithTag(babiesTag).Length;

        return 0;
    }
}
