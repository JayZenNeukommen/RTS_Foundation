using UnityEngine;

public class SplashImpactEffect : MonoBehaviour
{
    public float lifetime = 0.35f;
    public float startScaleMultiplier = 0.25f;
    public float endScaleMultiplier = 1f;

    private float timer;
    private float radius = 1f;
    private Vector3 baseScale;

    public void Initialize(float newRadius, float newLifetime)
    {
        radius = Mathf.Max(0.1f, newRadius);
        lifetime = Mathf.Max(0.05f, newLifetime);

        baseScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
        transform.localScale = baseScale * startScaleMultiplier;

        timer = lifetime;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        float t = 1f - Mathf.Clamp01(timer / lifetime);
        float scaleMultiplier = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, t);

        transform.localScale = baseScale * scaleMultiplier;

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}