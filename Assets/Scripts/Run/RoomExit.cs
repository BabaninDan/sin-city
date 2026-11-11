using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Место двери-выбора (TrenchBroom: room_exit). Поле slot = 0, 1, 2.
/// Сколько выходов в шаблоне — столько дверей максимум.
/// </summary>
[PointEntity("exit", category: "room", colour: "0.2 0.6 1.0", size: 0.5f)]
public class RoomExit : MonoBehaviour
{
    [SerializeField] private int slot = 0;

    public int Slot => slot;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(2f, 3f, 0.3f));
    }
}
