using UnityEngine;
using UnityEngine.UI;

public class HeartsUI : MonoBehaviour
{
    [Header("References")]
    public PlayerLives playerLives;     // assign your player
    public Image[] heartImages;         // size = 3 (or more if you want)
    public Sprite heartFull;
    public Sprite heartEmpty;

    void OnEnable()
    {
        RefreshAll();
        if (playerLives)
        {
            playerLives.onLifeLost.AddListener(RefreshAll);
            playerLives.onLivesRefreshed.AddListener(RefreshAll);
        }
    }

    void OnDisable()
    {
        if (playerLives)
        {
            playerLives.onLifeLost.RemoveListener(RefreshAll);
            playerLives.onLivesRefreshed.RemoveListener(RefreshAll);
        }
    }

    public void RefreshAll()
    {
        if (!playerLives || heartImages == null) return;

        int lives = Mathf.Clamp(playerLives.currentLives, 0, heartImages.Length);
        for (int i = 0; i < heartImages.Length; i++)
        {
            bool filled = i < lives;
            if (heartImages[i])
                heartImages[i].sprite = filled ? heartFull : heartEmpty;
        }
    }
}
