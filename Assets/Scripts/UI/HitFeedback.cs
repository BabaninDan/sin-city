using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Обратная связь по попаданию: всплывающие цифры урона
/// и событие для хитмаркера в HUD.
/// </summary>
public static class HitFeedback
{
    /// <summary>(урон, совпала масть, убийство)</summary>
    public static event Action<float, bool, bool> Hit;

    public static void Report(Vector3 point, float damage, bool suitMatch, bool kill)
    {
        Hit?.Invoke(damage, suitMatch, kill);
        DamageNumber.Spawn(point, damage, suitMatch, kill);
    }

    public static void Clear()
    {
        Hit = null;
    }
}

/// <summary>Цифра урона, вылетающая из точки попадания.</summary>
public class DamageNumber : MonoBehaviour
{
    private TextMeshPro text;
    private Vector3 velocity;
    private float life = 0.7f;
    private float timer;

    public static void Spawn(Vector3 point, float damage, bool suitMatch, bool kill)
    {
        GameObject go = new GameObject("DamageNumber");
        go.transform.position = point + UnityEngine.Random.insideUnitSphere * 0.2f;

        DamageNumber number = go.AddComponent<DamageNumber>();
        number.text = go.AddComponent<TextMeshPro>();
        number.text.alignment = TextAlignmentOptions.Center;
        number.text.fontStyle = FontStyles.Bold;
        number.text.fontSize = suitMatch ? 3.2f : 2.2f;
        number.text.outlineWidth = 0.2f;
        number.text.outlineColor = new Color32(0, 0, 0, 255);
        number.text.rectTransform.sizeDelta = new Vector2(3f, 1f);

        string value = Mathf.RoundToInt(damage).ToString();

        if (suitMatch)
        {
            number.text.text = value + " x2";
            number.text.color = SuitUtil.Gold;
        }
        else
        {
            number.text.text = value;
            number.text.color = Color.white;
        }

        if (kill)
            number.text.color = new Color(1f, 0.25f, 0.2f);

        number.velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f), 2.5f, UnityEngine.Random.Range(-1f, 1f));
    }

    private void LateUpdate()
    {
        timer += Time.deltaTime;

        transform.position += velocity * Time.deltaTime;
        velocity.y -= 6f * Time.deltaTime;

        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);

        if (text != null)
            text.alpha = 1f - Mathf.Clamp01((timer - life * 0.5f) / (life * 0.5f));

        if (timer >= life)
            Destroy(gameObject);
    }
}
