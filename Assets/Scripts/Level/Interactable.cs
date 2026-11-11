using UnityEngine;

/// <summary>Объект, с которым можно взаимодействовать на E.</summary>
public interface IInteractable
{
    string Prompt { get; }
    void Interact(PlayerStats player);
}

/// <summary>
/// Ищет интерактивный объект перед камерой и вызывает его по E.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float distance = 3.2f;

    private Camera cam;
    private PlayerStats stats;

    public IInteractable Current { get; private set; }

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        Current = null;

        if (GameState.InputBlocked || GameState.PlayerDead || cam == null)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        RaycastHit[] hits = Physics.SphereCastAll(ray, 0.3f, distance, ~0, QueryTriggerInteraction.Collide);
        float best = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable == null || hit.distance >= best)
                continue;

            best = hit.distance;
            Current = interactable;
        }

        if (Current != null && Input.GetKeyDown(KeyCode.E))
            Current.Interact(stats);
    }
}
