using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float moveSpeed = 15f;
    public float dodgeDistance = 1.5f;

    void Update()
    {
        if (!IsOwner) return;

        MovePlayer();
        HandleDodge();
    }

    void MovePlayer()
    {
        Vector3 moveDirection = new Vector3(1, -1, 0).normalized;
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
    }

    void HandleDodge()
    {
        if (Input.GetKeyDown(KeyCode.A))
            Dodge(new Vector3(-1, -1, 0));

        if (Input.GetKeyDown(KeyCode.D))
            Dodge(new Vector3(1, 1, 0));
    }

    void Dodge(Vector3 direction)
    {
        transform.Translate(direction.normalized * dodgeDistance);
    }
}