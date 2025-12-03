using UnityEngine;

public class TrapDamage : MonoBehaviour
{
    public int thisdeals2damageAmount = 1; // how many lives to remove

    private void OnCollisionEnter(Collision collision)
    {
        PlayerLives lives = collision.collider.GetComponent<PlayerLives>();

        if (lives != null)
        {
            // Deal 2 lives worth of damage at once
            lives.ApplyDamage(thisdeals2damageAmount, false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerLives lives = other.GetComponent<PlayerLives>();

        if (lives != null)
        {
            // Deal 2 lives worth of damage at once
            lives.ApplyDamage(thisdeals2damageAmount, false);
        }
    }
}
