using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D collider)
    {
        switch (collider.gameObject.tag)
        {
            case "Hazard":
                HandleHazardCollision();
                break;
            case "SpeedBoost":
                HandleSpeedBoostCollision();
                break;
            case "FinishLine":
                HandleFinishLineCollision();
                break;
        }
    }

    void HandleHazardCollision()
    {
        Debug.Log("Player hit a hazard!");
    }

    void HandleSpeedBoostCollision()
    {
        Debug.Log("Player collected a speed boost!");
    }

    void HandleFinishLineCollision()
    {
        Debug.Log("Player reached the finish line!");
    }
}