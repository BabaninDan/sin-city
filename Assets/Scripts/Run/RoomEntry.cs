using TinyGoose.Tremble;
using UnityEngine;

/// <summary>Точка появления игрока в комнате (TrenchBroom: room_entry).</summary>
[PointEntity("entry", category: "room", colour: "0.2 1.0 0.2", size: 0.5f)]
public class RoomEntry : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
    }
}
