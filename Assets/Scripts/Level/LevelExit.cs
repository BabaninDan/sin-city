using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Выход с уровня (конец прототипа / главы).
/// TrenchBroom: level_exit_trigger.
/// </summary>
[BrushEntity("exit_trigger", category: "level", type: BrushType.Trigger)]
public class LevelExit : MonoBehaviour
{
    private Bounds bounds;
    private bool done;

    private void Start()
    {
        Collider col = GetComponentInChildren<Collider>();

        if (col != null)
        {
            bounds = col.bounds;
            ArenaTrigger.MakeTrigger(col);
        }
        else
        {
            bounds = new Bounds(transform.position, Vector3.one * 2f);
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }

    private void Update()
    {
        if (done)
            return;

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player == null || player.IsDead)
            return;

        Bounds grown = bounds;
        grown.Expand(new Vector3(0f, 2f, 0f));

        if (!grown.Contains(player.transform.position))
            return;

        done = true;
        GameManager.Instance.Win();
    }
}
