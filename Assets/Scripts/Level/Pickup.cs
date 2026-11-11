using UnityEngine;

/// <summary>
/// Подбираемые предметы, которые создаются кодом:
/// фишки душ (валюта между смертями), патроны для пулемёта, здоровье.
/// Летят к игроку, если он рядом.
/// </summary>
public class Pickup : MonoBehaviour
{
    public enum Kind
    {
        Soul,
        ChipAmmo,
        Health
    }

    private Kind kind;
    private int amount;

    private Vector3 velocity;
    private float magnetRadius;
    private float collectRadius = 1.1f;
    private float delay = 0.35f;
    private float life = 40f;
    private float groundY;
    private bool grounded;

    public static Pickup Spawn(Kind kind, Vector3 position, int amount)
    {
        GameObject go;

        switch (kind)
        {
            case Kind.Soul:
                go = FX.Primitive(PrimitiveType.Cylinder, position, new Vector3(0.28f, 0.03f, 0.28f), SuitUtil.Gold);
                break;

            case Kind.ChipAmmo:
                go = FX.Primitive(PrimitiveType.Cylinder, position, new Vector3(0.35f, 0.15f, 0.35f), new Color(0.1f, 0.45f, 1f));
                FX.Primitive(PrimitiveType.Cylinder, position + Vector3.up * 0.18f, new Vector3(0.35f, 0.15f, 0.35f), new Color(0.95f, 0.1f, 0.15f), false, go.transform)
                    .transform.localScale = new Vector3(1f, 1f, 1f);
                go.transform.GetChild(0).localPosition = new Vector3(0f, 1.4f, 0f);
                break;

            default:
                go = FX.Primitive(PrimitiveType.Cube, position, new Vector3(0.4f, 0.4f, 0.4f), new Color(0.95f, 0.15f, 0.25f));
                FX.Primitive(PrimitiveType.Cube, position, Vector3.one, Color.white, false, go.transform)
                    .transform.localScale = new Vector3(1.05f, 0.3f, 0.3f);
                break;
        }

        go.name = "Pickup_" + kind;

        Pickup pickup = go.AddComponent<Pickup>();
        pickup.kind = kind;
        pickup.amount = amount;
        pickup.magnetRadius = kind == Kind.Soul ? 9f : 3.5f;
        pickup.velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(3f, 6f), Random.Range(-3f, 3f));

        // Пол под предметом
        pickup.groundY = position.y - 5f;
        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            pickup.groundY = hit.point.y + 0.25f;

        return pickup;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        life -= dt;
        delay -= dt;

        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player != null && !player.IsDead && delay <= 0f)
        {
            Vector3 target = player.transform.position + Vector3.up * 0.5f;
            float dist = Vector3.Distance(transform.position, target);

            if (dist <= collectRadius)
            {
                Collect(player);
                return;
            }

            if (dist <= magnetRadius && Wanted(player))
            {
                float pull = Mathf.Lerp(25f, 8f, dist / magnetRadius);
                transform.position = Vector3.MoveTowards(transform.position, target, pull * dt);
                grounded = false;
                Spin(dt);
                return;
            }
        }

        // Падение и подпрыгивание
        if (!grounded)
        {
            velocity.y -= 18f * dt;
            transform.position += velocity * dt;

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                grounded = true;
            }
        }
        else
        {
            transform.position = new Vector3(
                transform.position.x,
                groundY + Mathf.Sin(Time.time * 3f + GetInstanceID()) * 0.08f,
                transform.position.z
            );
        }

        Spin(dt);
    }

    private void Spin(float dt)
    {
        transform.Rotate(Vector3.up, 180f * dt, Space.World);
    }

    /// <summary>Не притягиваем то, что сейчас бесполезно.</summary>
    private bool Wanted(PlayerStats player)
    {
        if (kind == Kind.Health)
            return player.Health < player.MaxHealth;

        return true;
    }

    private void Collect(PlayerStats player)
    {
        switch (kind)
        {
            case Kind.Soul:
                MetaProgress.AddSouls(amount);
                break;

            case Kind.ChipAmmo:
                PlayerArsenal arsenal = player.GetComponent<PlayerArsenal>();

                if (arsenal != null && arsenal.ChipGunUnlocked)
                {
                    arsenal.AddChipAmmo(amount);
                    GameState.Say($"+{amount} фишек", new Color(0.4f, 0.7f, 1f));
                }
                else
                {
                    // Пулемёта ещё нет — патроны превращаются в души
                    MetaProgress.AddSouls(Mathf.Max(1, amount / 4));
                }

                break;

            case Kind.Health:
                if (player.Health >= player.MaxHealth)
                    return;

                player.Heal(amount);
                break;
        }

        FX.Sparks(transform.position, kind == Kind.Health ? SuitUtil.Red : SuitUtil.Gold);
        Destroy(gameObject);
    }
}
