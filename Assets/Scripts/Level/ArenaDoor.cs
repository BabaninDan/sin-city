using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Дверь арены (браш в TrenchBroom: arena_door).
///   mode = Entrance — открыта, захлопывается при старте арены, открывается после зачистки.
///   mode = Exit     — закрыта, пока арена не зачищена.
/// Открытая дверь уезжает в пол.
/// </summary>
[BrushEntity("door", category: "arena", type: BrushType.Solid)]
public class ArenaDoor : MonoBehaviour
{
    public enum DoorMode
    {
        Entrance,
        Exit
    }

    [SerializeField] private string arena = "1";
    [SerializeField] private DoorMode mode = DoorMode.Entrance;
    [SerializeField] private float speed = 8f;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool open;
    private bool initialised;

    public string ArenaId => arena;
    public DoorMode Mode => mode;

    private void Awake()
    {
        Init();

        // Правильное состояние сразу — до постройки NavMesh
        SetOpen(mode == DoorMode.Entrance, true);
    }

    private void Init()
    {
        if (initialised)
            return;

        initialised = true;

        closedPosition = transform.position;

        float height = 3f;

        Renderer r = GetComponentInChildren<Renderer>();
        if (r != null)
            height = r.bounds.size.y;
        else
        {
            Collider c = GetComponentInChildren<Collider>();
            if (c != null)
                height = c.bounds.size.y;
        }

        openPosition = closedPosition - Vector3.up * (height + 0.05f);
    }

    public void SetOpen(bool value, bool instant = false)
    {
        Init();

        bool changed = open != value;
        open = value;

        if (instant)
        {
            transform.position = open ? openPosition : closedPosition;
            Physics.SyncTransforms();
            return;
        }

        if (changed && !open)
        {
            // Захлопнулась — эффект
            Vector3 p = closedPosition;
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null)
                p = r.bounds.center;

            FX.Burst(p, new Color(0.5f, 0.4f, 0.3f), 20, 4f, 0.2f, 0.5f, 1f);
        }
    }

    private void Update()
    {
        Vector3 target = open ? openPosition : closedPosition;

        if ((transform.position - target).sqrMagnitude < 0.0001f)
            return;

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}
