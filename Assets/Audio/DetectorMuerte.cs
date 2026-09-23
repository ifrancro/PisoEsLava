using UnityEngine;

public class DetectorMuerte : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        ProcesarContacto(other.gameObject);
    }

    // Por si el collider no tiene Is Trigger activado:
    private void OnCollisionEnter(Collision collision)
    {
        ProcesarContacto(collision.gameObject);
    }

    // Por si un CharacterController interactua directamente:
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        ProcesarContacto(hit.gameObject);
    }

    private void ProcesarContacto(GameObject obj)
    {
        if (obj.CompareTag("Player") || obj.GetComponentInParent<HeatRise.PlayerController>() != null)
        {
            if (PersistentMusic.Instance != null)
            {
                PersistentMusic.Instance.ReproducirMuerte(false);
            }
        }
    }
}
