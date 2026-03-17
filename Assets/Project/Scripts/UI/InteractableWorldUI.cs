using UnityEngine;
using TMPro;
using System.Collections;

public class InteractableWorldUI : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string keyString = "E";
    [SerializeField] private string descriptionString = "Interact";
    [Tooltip("Offset relative to the player to ensure it doesn't block the front view.")]
    [SerializeField] private Vector3 offsetFromPlayer = new Vector3(1f, 1.5f, -0.5f); 
    [SerializeField] private float floatingSpeed = 2f;
    [SerializeField] private float floatingHeight = 0.1f;
    
    [Header("UI References")]
    [Tooltip("The parent GameObject of the WorldSpace Canvas. Leave empty to use this gameObject.")]
    [SerializeField] private GameObject uiCanvasObject;
    [Tooltip("TextMeshPro component for the individual key press (e.g. 'E').")]
    [SerializeField] private TMP_Text keyText; 
    [Tooltip("TextMeshPro component for the description (e.g. 'Interact').")]
    [SerializeField] private TMP_Text descriptionText;
    
    private Transform playerTransform;
    private Camera mainCamera;
    private bool isPlayerInRange = false;

    private Vector3 currentVelocity = Vector3.zero;
    [SerializeField] private float uiSmoothTime = 0.15f;

    private void Start()
    {
        mainCamera = Camera.main;
        
        // If not assigned, assume this script is attached directly to the UI Canvas object
        if (uiCanvasObject == null)
            uiCanvasObject = this.gameObject;
            
        uiCanvasObject.SetActive(false);
    }

    private void Update()
    {
        if (isPlayerInRange && playerTransform != null && uiCanvasObject != null)
        {
            // 1. Calculate dynamic position around the player (e.g., right and slightly behind to not block view)
            Vector3 targetPosition = playerTransform.position 
                + (playerTransform.right * offsetFromPlayer.x) 
                + (playerTransform.up * offsetFromPlayer.y) 
                + (playerTransform.forward * offsetFromPlayer.z);

            // Add a simple floating up and down animation
            float floatOffset = Mathf.Sin(Time.time * floatingSpeed) * floatingHeight;
            targetPosition.y += floatOffset;

            // 2. Smoothly move the UI to the target position
            uiCanvasObject.transform.position = Vector3.SmoothDamp(uiCanvasObject.transform.position, targetPosition, ref currentVelocity, uiSmoothTime);

            // 3. Billboard: Make UI always face the camera
            if (mainCamera != null)
            {
                uiCanvasObject.transform.rotation = Quaternion.LookRotation(uiCanvasObject.transform.position - mainCamera.transform.position);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ensure the interacting object is the Player
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered range");
            if (!isPlayerInRange)
            {
                playerTransform = other.transform;
                isPlayerInRange = true;
                
                if (keyText != null)
                {
                    keyText.text = keyString;
                }

                if (descriptionText != null)
                {
                    descriptionText.text = descriptionString;
                }

                if (uiCanvasObject != null)
                {
                    uiCanvasObject.SetActive(true);
                    // Snap to starting position immediately when appearing so it doesn't fly from far away
                    uiCanvasObject.transform.position = playerTransform.position + offsetFromPlayer;
                    
                    // Scale pop-in animation
                    StopAllCoroutines();
                    StartCoroutine(ScaleAnimation(0f, 1f, 0.25f));
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Debug.Log("Player is out of range");
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            
            if (uiCanvasObject != null)
            {
                // Scale pop-out animation, then deactivate
                StopAllCoroutines();
                StartCoroutine(ScaleAnimation(uiCanvasObject.transform.localScale.x, 0f, 0.15f, () => uiCanvasObject.SetActive(false)));
            }
            playerTransform = null;
        }
    }

    private IEnumerator ScaleAnimation(float startScale, float endScale, float duration, System.Action onComplete = null)
    {
        float timer = 0f;
        Vector3 initialScale = Vector3.one * startScale;
        Vector3 targetScale = Vector3.one * endScale;

        uiCanvasObject.transform.localScale = initialScale;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            
            // Ease-out cubic curve for snappy animation
            float curve = 1f - Mathf.Pow(1f - t, 3f);
            
            uiCanvasObject.transform.localScale = Vector3.Lerp(initialScale, targetScale, curve);
            yield return null;
        }

        uiCanvasObject.transform.localScale = targetScale;
        onComplete?.Invoke();
    }
}
