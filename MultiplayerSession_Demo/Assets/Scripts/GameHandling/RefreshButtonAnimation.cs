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
        _button = this.GetComponent<Button>();
        _rect = this.GetComponent<RectTransform>();
    }

    private void AnimateButton()
        {
            if (_animationCoroutine != null)
                return;

            _animationCoroutine = StartCoroutine(RotateButton());
        }

        private IEnumerator RotateButton()
        {
            _button.interactable = false;

            Quaternion startRotation = _rect.localRotation;
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(
                    elapsed / rotationDuration
                );

                _rect.localRotation =
                    startRotation * Quaternion.Euler(
                        0f, 0f, -360f * progress
                    );

                yield return null;
            }

            _rect.localRotation = startRotation;

            _button.interactable = true;
            _animationCoroutine = null;
        }
}
