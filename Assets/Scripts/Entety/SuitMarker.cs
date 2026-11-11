using TMPro;
using UnityEngine;

/// <summary>
/// Символ масти над головой врага (♥♦♣♠), всегда повёрнут к камере.
/// Полоска HP под символом появляется после первого попадания.
/// </summary>
public class SuitMarker : MonoBehaviour
{
    private EnemyBase enemy;
    private TextMeshPro text;
    private Transform hpBar;
    private Transform hpBack;
    private float pulse;
    private float baseSize = 3.2f;

    public static SuitMarker Create(EnemyBase enemy)
    {
        GameObject go = new GameObject("SuitMarker");
        go.transform.SetParent(enemy.transform, false);

        SuitMarker marker = go.AddComponent<SuitMarker>();
        marker.enemy = enemy;

        marker.text = go.AddComponent<TextMeshPro>();
        marker.text.alignment = TextAlignmentOptions.Center;
        marker.text.fontSize = marker.baseSize;
        marker.text.fontStyle = FontStyles.Bold;
        marker.text.outlineWidth = 0.25f;
        marker.text.outlineColor = new Color32(255, 255, 255, 255);
        marker.text.rectTransform.sizeDelta = new Vector2(2f, 1f);

        // Полоска здоровья
        marker.hpBack = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, new Color(0f, 0f, 0f), false, go.transform).transform;
        marker.hpBack.localPosition = new Vector3(0f, -0.45f, 0.01f);
        marker.hpBack.localScale = new Vector3(0.9f, 0.08f, 0.01f);

        marker.hpBar = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, new Color(0.9f, 0.1f, 0.1f), false, go.transform).transform;
        marker.hpBar.localPosition = new Vector3(0f, -0.45f, 0f);
        marker.hpBar.localScale = new Vector3(0.86f, 0.05f, 0.01f);

        marker.hpBack.gameObject.SetActive(false);
        marker.hpBar.gameObject.SetActive(false);

        marker.Refresh();

        return marker;
    }

    public void Refresh()
    {
        if (enemy == null || text == null)
            return;

        text.text = enemy.Suit.Symbol();
        text.color = enemy.Suit.IsRed() ? SuitUtil.Red : SuitUtil.Black;

        float height = enemy.AimPoint.y - enemy.transform.position.y;
        transform.localPosition = Vector3.up * (height * 2f + 0.5f) / Mathf.Max(0.01f, enemy.transform.localScale.y);
    }

    public void Pulse(bool strong)
    {
        pulse = strong ? 1f : 0.5f;

        if (hpBar != null && !hpBar.gameObject.activeSelf)
        {
            hpBar.gameObject.SetActive(true);
            hpBack.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;

        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 4f);

        if (text != null)
        {
            text.fontSize = baseSize * (1f + pulse * 0.6f);

            // Ближе к врагу — меньше, чтобы не закрывал обзор
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, transform.position);
                text.alpha = Mathf.Clamp01((d - 1.5f) / 2f);
            }
        }

        if (hpBar != null && enemy != null && hpBar.gameObject.activeSelf)
        {
            float fill = Mathf.Clamp01(enemy.Health / Mathf.Max(1f, enemy.MaxHealth));
            hpBar.localScale = new Vector3(0.86f * fill, 0.05f, 0.01f);
            hpBar.localPosition = new Vector3(-0.43f * (1f - fill), -0.45f, 0f);
        }
    }
}
