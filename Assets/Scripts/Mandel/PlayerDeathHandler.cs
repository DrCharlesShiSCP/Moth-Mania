using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PlayerDeathHandler : MonoBehaviour
{
    [Header("UI / Flow")]
    [Tooltip("Optional panel to show when the player dies (e.g., Game Over UI). If null, we auto-reload after a delay.")]
    public GameObject gameOverPanel;

    [Tooltip("Pause the game (Time.timeScale=0) while the panel is shown.")]
    public bool pauseWhilePanelOpen = true;

    [Tooltip("Require input to restart when the panel is visible.")]
    public bool waitForConfirm = true;

    [Tooltip("Key to restart (legacy Input). You can also wire a UI Button to call Reload().")]
    public KeyCode restartKey = KeyCode.R;

    [Header("Fade (optional)")]
    [Tooltip("CanvasGroup that covers the screen; fade this in before reload.")]
    public CanvasGroup fade;
    public float fadeDuration = 0.5f;

    [Header("Auto Reload (when no panel)")]
    public float reloadDelay = 0.75f;

    bool _done; // prevent double reload

    public void OnPlayerDeath()
    {
        if (_done) return;
        _done = true;

        if (gameOverPanel != null)
        {
            // Show panel path
            if (pauseWhilePanelOpen) Time.timeScale = 0f;
            gameOverPanel.SetActive(true);

            // Option A: wait for user to press a key/button
            if (waitForConfirm)
            {
                // If you have a UI Button, just hook it to Reload() and skip this coroutine.
                StartCoroutine(WaitForConfirmThenReload());
            }
            // Option B: let UI Button call Reload() manually
        }
        else
        {
            // No panel: fade (optional) then reload after delay
            if (fade)
                StartCoroutine(FadeThenReload(reloadDelay));
            else
                Invoke(nameof(Reload), reloadDelay);
        }
    }

    IEnumerator WaitForConfirmThenReload()
    {
        // Optional fade-in of the panel background (if your panel has a CanvasGroup)
        var cg = gameOverPanel.GetComponent<CanvasGroup>();
        if (cg && cg.alpha < 1f)
        {
            // Unscaled because we may have paused time
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / 0.15f);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // Wait for input (unscaled while paused)
        while (true)
        {
            if (Input.GetKeyDown(restartKey))
                break;

            // If you’re using the New Input System, you can also poll an action here.
            yield return null;
        }

        // Optional screen fade before reload
        if (fade) yield return FadeRoutine(fade, fadeDuration);

        // Ensure timescale back to normal
        Time.timeScale = 1f;
        Reload();
    }

    IEnumerator FadeThenReload(float delay)
    {
        // Let any death SFX/VFX play
        float t = 0f;
        while (t < delay)
        {
            t += Time.deltaTime;
            yield return null;
        }

        yield return FadeRoutine(fade, fadeDuration);
        Reload();
    }

    static IEnumerator FadeRoutine(CanvasGroup cg, float duration)
    {
        if (!cg) yield break;
        cg.gameObject.SetActive(true);
        cg.blocksRaycasts = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    public void Reload()
    {
        // Make sure time is normal on reload
        if (Time.timeScale == 0f) Time.timeScale = 1f;

        var current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }
}
