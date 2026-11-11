using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Пулемёт фишек на постаменте. Подобрал — открыт навсегда (даже после смерти).
/// Если уже открыт — даёт патроны.
/// TrenchBroom: prop_ChipGunPickup.
/// </summary>
[PrefabEntity(category: "prop")]
public class ChipGunPickup : MonoBehaviour
{
    [SerializeField] private int ammo = 80;
    [SerializeField, NoTremble] private Transform spinner;

    private bool taken;

    private void Update()
    {
        if (spinner != null)
        {
            spinner.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            spinner.localPosition = new Vector3(0f, 1.2f + Mathf.Sin(Time.time * 2f) * 0.1f, 0f);
        }

        if (taken)
            return;

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player == null || player.IsDead)
            return;

        Vector3 delta = player.transform.position - transform.position;
        delta.y *= 0.5f;

        if (delta.magnitude > 1.6f)
            return;

        PlayerArsenal arsenal = player.GetComponent<PlayerArsenal>();

        if (arsenal == null)
            return;

        taken = true;
        arsenal.UnlockChipGun(ammo);

        FX.Burst(transform.position + Vector3.up * 1.2f, SuitUtil.Gold, 40, 7f, 0.2f, 0.8f, 0.5f);

        if (spinner != null)
            spinner.gameObject.SetActive(false);
    }
}
