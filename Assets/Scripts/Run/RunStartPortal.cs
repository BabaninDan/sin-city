using TinyGoose.Tremble;
using TMPro;
using UnityEngine;

/// <summary>
/// Вход в забег в комнате героя (TrenchBroom: run_start).
/// Игрок входит в портал — начинается новый забег со стартовой колодой.
/// </summary>
[PointEntity("start", category: "run", colour: "1.0 0.1 0.2", size: 1f)]
public class RunStartPortal : MonoBehaviour
{
    private Transform glow;
    private bool used;

    private void Start()
    {
        Color red = new Color(0.9f, 0.1f, 0.15f);
        Color dark = new Color(0.08f, 0.05f, 0.06f);

        Part(new Vector3(-1.3f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 0.3f), dark);
        Part(new Vector3(1.3f, 1.6f, 0f), new Vector3(0.3f, 3.2f, 0.3f), dark);
        Part(new Vector3(0f, 3.3f, 0f), new Vector3(2.9f, 0.3f, 0.3f), SuitUtil.Gold);
        glow = Part(new Vector3(0f, 1.6f, 0f), new Vector3(2.3f, 3.1f, 0.05f), red).transform;

        GameObject label = new GameObject("Label");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0f, 4.2f, 0f);

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = "ЗА СТОЛ\n<size=55%><color=#FFD24A>начать забег</color></size>";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = red;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
        tmp.rectTransform.sizeDelta = new Vector2(6f, 2f);

        // Подпись видна с обеих сторон
        GameObject back = Instantiate(label, transform);
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private GameObject Part(Vector3 localPos, Vector3 scale, Color color)
    {
        GameObject go = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, color, false, transform);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        return go;
    }

    private void Update()
    {
        if (glow != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            glow.localScale = new Vector3(2.3f * pulse, 3.1f, 0.05f);
        }

        if (used || RunState.Active)
            return;

        PlayerStats player = GameManager.Instance != null ? GameManager.Instance.Player : null;

        if (player == null || player.IsDead || GameState.InputBlocked)
            return;

        Vector3 delta = player.transform.position - (transform.position + Vector3.up);
        delta.y *= 0.5f;

        if (delta.magnitude > 1.6f)
            return;

        if (RunManager.Instance == null)
            return;

        used = true;
        RunManager.Instance.StartRun();
    }
}
