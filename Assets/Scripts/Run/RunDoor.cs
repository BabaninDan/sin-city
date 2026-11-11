using TMPro;
using UnityEngine;

/// <summary>
/// Дверь-выбор после зачистки комнаты. Показывает тип комнаты и награду,
/// игрок входит — RunManager грузит следующую комнату.
/// Собирается из примитивов (временный вид).
/// </summary>
public class RunDoor : MonoBehaviour
{
    private DoorOffer offer;
    private System.Action<DoorOffer> onEnter;
    private Transform glow;
    private bool used;

    public static RunDoor Create(Vector3 position, Vector3 facing, DoorOffer offer, System.Action<DoorOffer> onEnter, string extraLine = null)
    {
        GameObject root = new GameObject("RunDoor_" + offer.kind);
        root.transform.position = position;

        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f)
            root.transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

        RunDoor door = root.AddComponent<RunDoor>();
        door.offer = offer;
        door.onEnter = onEnter;

        Color color = offer.Color;
        Color dark = new Color(0.08f, 0.05f, 0.06f);

        // Рама
        Part(root.transform, new Vector3(-1.1f, 1.5f, 0f), new Vector3(0.25f, 3f, 0.25f), dark);
        Part(root.transform, new Vector3(1.1f, 1.5f, 0f), new Vector3(0.25f, 3f, 0.25f), dark);
        Part(root.transform, new Vector3(0f, 3.1f, 0f), new Vector3(2.45f, 0.25f, 0.25f), color);

        // Светящийся проём
        GameObject glowGo = Part(root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(1.9f, 2.9f, 0.05f), color * 0.7f);
        door.glow = glowGo.transform;

        // Подпись
        GameObject label = new GameObject("Label");
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 3.9f, 0f);

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);

        string reward = offer.RewardText;
        string text = offer.Title;

        if (!string.IsNullOrEmpty(reward))
            text += $"\n<size=60%><color=#FFD24A>{reward}</color></size>";

        if (!string.IsNullOrEmpty(extraLine))
            text += $"\n<size=50%><color=#CCCCCC>{extraLine}</color></size>";

        tmp.text = text;

        // Читается спереди: TMP смотрит в -Z, разворачиваем к игроку
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        FX.Burst(position + Vector3.up * 1.5f, color, 20, 4f, 0.15f, 0.6f, -0.3f);

        return door;
    }

    private static GameObject Part(Transform parent, Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject go = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, color, false, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        return go;
    }

    private void Update()
    {
        if (glow != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f + transform.position.x);
            glow.localScale = new Vector3(1.9f * pulse, 2.9f, 0.05f);
        }

        if (used)
            return;

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player == null || player.IsDead || GameState.InputBlocked)
            return;

        Vector3 delta = player.transform.position - (transform.position + Vector3.up);
        delta.y *= 0.5f;

        if (delta.magnitude > 1.4f)
            return;

        used = true;
        onEnter?.Invoke(offer);
    }
}
