using UnityEngine;

/// <summary>
/// Простые процедурные эффекты: брызги, вспышки, лучи, примитивы.
/// Всё создаётся кодом, чтобы прототип не зависел от префабов эффектов.
/// </summary>
public static class FX
{
    private static Material unlit;
    private static Material particles;
    private static MaterialPropertyBlock block;

    public static Material UnlitMaterial
    {
        get
        {
            if (unlit == null)
            {
                GameConfig config = GameConfig.Instance;

                if (config != null && config.unlitMaterial != null)
                    unlit = config.unlitMaterial;
                else
                    unlit = new Material(FindShader("Universal Render Pipeline/Unlit", "Unlit/Color"));
            }

            return unlit;
        }
    }

    public static Material ParticleMaterial
    {
        get
        {
            if (particles == null)
            {
                GameConfig config = GameConfig.Instance;

                if (config != null && config.particleMaterial != null)
                    particles = config.particleMaterial;
                else
                    particles = new Material(FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit"));
            }

            return particles;
        }
    }

    private static Shader FindShader(string name, string fallback)
    {
        Shader shader = Shader.Find(name);

        if (shader == null)
            shader = Shader.Find(fallback);

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        return shader;
    }

    // =========================================================
    // COLOR
    // =========================================================

    public static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        if (block == null)
            block = new MaterialPropertyBlock();

        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    // =========================================================
    // PRIMITIVES
    // =========================================================

    public static GameObject Primitive(
        PrimitiveType type,
        Vector3 position,
        Vector3 scale,
        Color color,
        bool keepCollider = false,
        Transform parent = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);

        if (!keepCollider)
            Object.Destroy(go.GetComponent<Collider>());

        if (parent != null)
            go.transform.SetParent(parent, false);

        go.transform.position = position;
        go.transform.localScale = scale;

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = UnlitMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SetColor(renderer, color);

        return go;
    }

    // =========================================================
    // PARTICLES
    // =========================================================

    public static void Burst(
        Vector3 position,
        Color color,
        int count = 14,
        float speed = 5f,
        float size = 0.12f,
        float lifetime = 0.6f,
        float gravity = 1.5f)
    {
        GameObject go = new GameObject("FX_Burst");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, color * 0.6f);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count, 1);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = ParticleMaterial;
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();

        Object.Destroy(go, lifetime + 0.3f);
    }

    public static void Blood(Vector3 position, float amount = 1f)
    {
        Burst(
            position,
            new Color(0.75f, 0.02f, 0.05f),
            Mathf.RoundToInt(16 * amount),
            6f * Mathf.Sqrt(amount),
            0.14f,
            0.8f,
            2.5f
        );
    }

    public static void Sparks(Vector3 position, Color color)
    {
        Burst(position, color, 8, 4f, 0.07f, 0.35f, 0.5f);
    }

    // =========================================================
    // BEAM
    // =========================================================

    public static void Beam(Vector3 from, Vector3 to, Color color, float width = 0.15f, float duration = 0.35f)
    {
        GameObject go = new GameObject("FX_Beam");

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = width;
        line.endWidth = width * 0.4f;
        line.sharedMaterial = ParticleMaterial;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        go.AddComponent<FadeAndDie>().Init(duration);
    }

    // =========================================================
    // RING (удар по земле, взрыв)
    // =========================================================

    public static void Ring(Vector3 position, float radius, Color color, float duration = 0.35f)
    {
        GameObject go = Primitive(
            PrimitiveType.Cylinder,
            position + Vector3.up * 0.05f,
            new Vector3(0.2f, 0.02f, 0.2f),
            color
        );

        go.AddComponent<ExpandAndDie>().Init(radius * 2f, duration);
    }
}

/// <summary>Плавно уменьшает ширину LineRenderer и уничтожает объект.</summary>
public class FadeAndDie : MonoBehaviour
{
    private float duration;
    private float timer;
    private LineRenderer line;
    private float startWidth;

    public void Init(float duration)
    {
        this.duration = Mathf.Max(0.01f, duration);
        line = GetComponent<LineRenderer>();

        if (line != null)
            startWidth = line.startWidth;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;

        float t = timer / duration;

        if (line != null)
        {
            line.startWidth = Mathf.Lerp(startWidth, 0f, t);
            line.endWidth = line.startWidth * 0.4f;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }
}

/// <summary>Растягивает объект по XZ и уничтожает.</summary>
public class ExpandAndDie : MonoBehaviour
{
    private float targetSize;
    private float duration;
    private float timer;
    private Vector3 startScale;

    public void Init(float targetSize, float duration)
    {
        this.targetSize = targetSize;
        this.duration = Mathf.Max(0.01f, duration);
        startScale = transform.localScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);
        float size = Mathf.Lerp(startScale.x, targetSize, 1f - (1f - t) * (1f - t));

        transform.localScale = new Vector3(size, startScale.y * (1f - t), size);

        if (t >= 1f)
            Destroy(gameObject);
    }
}
