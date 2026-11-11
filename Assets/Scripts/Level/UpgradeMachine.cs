using TinyGoose.Tremble;
using TMPro;
using UnityEngine;

/// <summary>
/// Игровой автомат в комнате героя: тратишь фишки душ на постоянные улучшения.
/// TrenchBroom: prop_UpgradeMachine.
/// </summary>
[PrefabEntity(category: "prop")]
public class UpgradeMachine : MonoBehaviour, IInteractable
{
    [SerializeField, NoTremble] private Renderer screen;

    public string Prompt => $"[E] Автомат душ   <color=#FFD24A>● {MetaProgress.Souls}</color>";

    private void Awake()
    {
        CreateLabels();
    }

    /// <summary>Надписи на четырёх экранах (создаются в игре, не в префабе).</summary>
    private void CreateLabels()
    {
        for (int i = 0; i < 4; i++)
        {
            Quaternion rot = Quaternion.Euler(0f, i * 90f, 0f);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = rot * new Vector3(0f, 0f, 0.635f) + Vector3.up * 1.35f;
            label.transform.localRotation = rot * Quaternion.Euler(0f, 180f, 0f);

            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.text = "АВТОМАТ\nДУШ\n<size=60%>[E]</size>";
            tmp.fontSize = 1.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.3f, 0.02f, 0.02f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.rectTransform.sizeDelta = new Vector2(0.8f, 0.55f);
        }
    }

    public void Interact(PlayerStats player)
    {
        if (GameHUD.Instance != null)
            GameHUD.Instance.OpenUpgradeMenu();
    }

    private void Update()
    {
        if (screen == null)
            return;

        // Мигающий экран
        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        FX.SetColor(screen, Color.Lerp(new Color(1f, 0.6f, 0.1f), new Color(1f, 0.95f, 0.5f), t));
    }
}
