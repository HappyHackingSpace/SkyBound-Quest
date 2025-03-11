using DialogueSystem.Scene;
using UnityEngine;
using UnityEngine.Serialization;

namespace DialogueSystem.Runtime.Interaction
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private Transform interactSource;
        [SerializeField] private float interactionDistance;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private PlayerController playerController; // To replace with your player controller

        private const KeyCode InteractionKey = KeyCode.Return;
        private Vector2 _rayDirection;

        private void Update()
        {
            CalculateInputDirection();
            InteractWithCharacter();
        }

        private void CalculateInputDirection()
        {
            Vector2 inputDir = playerController.InputDirection;
            _rayDirection = inputDir == Vector2.zero ? _rayDirection : inputDir;
        }

        private void InteractWithCharacter()
        {
            if (!Input.GetKeyDown(InteractionKey))
            {
                return;
            }

            // Use Physics2D.Raycast instead of Physics.Raycast
            RaycastHit2D hit = Physics2D.Raycast(interactSource.position, _rayDirection, interactionDistance, interactableLayer);

            if (hit.collider != null)
            {
                Debug.Log($"Raycast hit: {hit.collider.name} at {hit.point}");

                var interactable = hit.collider.GetComponent<IInteractable>();

                if (interactable != null && interactable.CanInteract)
                {
                    interactable.Interact();
                }
            }
            else
            {
                Debug.Log("Raycast did not hit any interactable object.");
            }
        }

        private void OnDrawGizmos()
        {
            if (interactSource == null) return;

            Vector2 rayOrigin = interactSource.position;
            Vector2 rayDirection = _rayDirection.normalized * interactionDistance;

            // Use Physics2D.Raycast for Gizmos color change
            bool hitSomething = Physics2D.Raycast(rayOrigin, _rayDirection, interactionDistance, interactableLayer);

            Gizmos.color = hitSomething ? Color.green : Color.red;
            Gizmos.DrawRay(rayOrigin, rayDirection);
        }
    }
}
