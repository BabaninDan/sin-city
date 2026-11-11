using TinyGoose.Tremble;
using UnityEngine;

/// <summary>
/// Описание шаблона комнаты (TrenchBroom: room_info, одна на карту).
/// kinds — какие комнаты можно собрать из шаблона: "combat", "boss", "small"
///   (small = магазин, бар, рулетка); через пробел или запятую.
/// chapters — в каких главах встречается: "123".
/// </summary>
[PointEntity("info", category: "room", colour: "1.0 0.8 0.2", size: 0.5f)]
public class RoomInfo : MonoBehaviour
{
    [SerializeField] private string kinds = "combat";
    [SerializeField] private string chapters = "123";

    public bool HasKind(string kind)
    {
        return !string.IsNullOrEmpty(kinds) &&
               kinds.ToLowerInvariant().Contains(kind.ToLowerInvariant());
    }

    public bool InChapter(int chapter)
    {
        return string.IsNullOrEmpty(chapters) || chapters.Contains(chapter.ToString());
    }
}
