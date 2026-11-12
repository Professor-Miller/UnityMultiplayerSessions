using TMPro;
using UnityEngine;

public class RollingDotsAnimation : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float interval = 0.4f;

    private float timer;
    private int dots;

    private void OnEnable()
    {
        timer = 0f;
        dots = 0;
        text.text = "Picking Random Winner (Totally Legit)";
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer < interval)
            return;

        timer = 0f;
        dots = (dots + 1) % 4;

        text.text = "Picking Random Winner (Totally Legit)" + new string('.', dots);
    }
}
