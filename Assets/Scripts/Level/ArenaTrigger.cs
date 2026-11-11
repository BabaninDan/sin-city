using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Невидимый объём: когда игрок в него входит — арена стартует.
/// В TrenchBroom: arena_trigger, поле arena = ID арены (например "1").
/// </summary>
[BrushEntity("trigger", category: "arena", type: BrushType.Trigger)]
public class ArenaTrigger : MonoBehaviour
{
    [SerializeField] private string arena = "1";

    private Bounds bounds;
    private float checkTimer;
    private bool fired;

    public string ArenaId => arena;

    private void Start()
    {
        Collider col = GetComponent<Collider>();

        if (col == null)
            col = GetComponentInChildren<Collider>();

        if (col != null)
        {
            bounds = col.bounds;
            MakeTrigger(col);
        }
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            bounds = r != null ? r.bounds : new Bounds(transform.position, Vector3.one * 2f);
        }

        // Триггер не должен быть видимым
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }

    /// <summary>Объём не должен мешать ходить (на случай, если импорт сделал его твёрдым).</summary>
    public static void MakeTrigger(Collider col)
    {
        if (col.isTrigger)
            return;

        if (col is MeshCollider mesh)
            mesh.convex = true;

        col.isTrigger = true;
    }

    private void Update()
    {
        if (fired)
            return;

        checkTimer -= Time.deltaTime;

        if (checkTimer > 0f)
            return;

        checkTimer = 0.1f;

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player == null || player.IsDead)
            return;

        // Проверяем по объёму — работает без Rigidbody и OnTriggerEnter
        Bounds grown = bounds;
        grown.Expand(new Vector3(0f, 2f, 0f));

        if (!grown.Contains(player.transform.position))
            return;

        if (ArenaManager.Instance == null)
            return;

        fired = true;
        ArenaManager.Instance.StartArena(arena);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);

        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
