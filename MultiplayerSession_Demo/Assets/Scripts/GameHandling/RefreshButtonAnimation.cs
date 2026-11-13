using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(RectTransform))]
public class RefreshButtonAnimation : MonoBehaviour
{
    [SerializeField] private float rotationDuration = 0.75f;

    private Button _button;
    private RectTransform _rect;
    private Coroutine _animationCoroutine;

    void OnEnable() => GameManager.OnRefreshBrowse += AnimateButton;
    void OnDisable() => GameManager.OnRefreshBrowse -= AnimateButton;

    void Start()
    {
        // Cache the references to the Button and the RectTransform component
        _button = this.GetComponent<Button>();
        _rect = this.GetComponent<RectTransform>();
    }

    private void AnimateButton()
    {
        // PREVENT OVERLAPPING ANIMATIONS
        //
        // If _animationCoroutine is not null,
        // an animation has already been started.
        //
        // Return immediately instead of starting another.
        if (_animationCoroutine != null) return;

        // Start the coroutine and return a Coroutine reference,
        // which we store to track the animation.
        _animationCoroutine = StartCoroutine(RotateButton());
    }

    private IEnumerator RotateButton()
    {
        // Disable the button interaction
        _button.interactable = false;

        // Store the starting rotation
        Quaternion startRotation = _rect.localRotation;

        // Tracks how many seconds have passed
        float elapsed = 0f;

        // Continue animationg until the elapsed time
        // reaches the speciied rotation duration
        while (elapsed < rotationDuration)
        {
            // Time.unscaledDeltaTime measures frame time
            // without being affected by Time.timeScale.
            //
            // This allows the animation to continue
            // even if gameplay is paused.
            elapsed += Time.unscaledDeltaTime;

            // Calculate the animation progress
            float progress = Mathf.Clamp01(elapsed / rotationDuration);

            // Calculate the current rotation
            _rect.localRotation = startRotation * Quaternion.Euler(0f, 0f, -360f * progress);

            // Wait until the next frame
            yield return null;
        }

        // Restore the original rotation
        _rect.localRotation = startRotation;

        // Re-enable the button
        _button.interactable = true;

        // Clear the coroutine reference so we can start a new one
        _animationCoroutine = null;
    }
}
