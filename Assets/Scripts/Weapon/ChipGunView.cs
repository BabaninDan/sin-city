using UnityEngine;

/// <summary>
/// Временная модель пулемёта фишек в руке (из примитивов).
/// Заменишь на спрайт/модель — просто удали создание вида в ChipGun.Setup.
/// </summary>
public class ChipGunView : MonoBehaviour
{
    private Transform drum;
    private Transform muzzle;

    private Vector3 basePosition;
    private float kick;
    private float spin;

    public Transform Muzzle => muzzle;

    public static ChipGunView Create(Transform cameraTransform)
    {
        GameObject root = new GameObject("ChipGunView");
        root.transform.SetParent(cameraTransform, false);
        root.transform.localPosition = new Vector3(0.28f, -0.26f, 0.55f);
        root.transform.localRotation = Quaternion.identity;

        ChipGunView view = root.AddComponent<ChipGunView>();
        view.basePosition = root.transform.localPosition;

        // Корпус
        GameObject body = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, new Color(0.25f, 0.05f, 0.07f), false, root.transform);
        body.transform.localPosition = new Vector3(0f, 0f, 0f);
        body.transform.localScale = new Vector3(0.16f, 0.14f, 0.42f);

        // Золотая рамка
        GameObject trim = FX.Primitive(PrimitiveType.Cube, Vector3.zero, Vector3.one, SuitUtil.Gold, false, root.transform);
        trim.transform.localPosition = new Vector3(0f, 0.075f, 0f);
        trim.transform.localScale = new Vector3(0.17f, 0.02f, 0.43f);

        // Барабан со стопками фишек
        GameObject drumGo = new GameObject("Drum");
        drumGo.transform.SetParent(root.transform, false);
        drumGo.transform.localPosition = new Vector3(0f, 0f, 0.26f);
        view.drum = drumGo.transform;

        Color[] colors =
        {
            new Color(0.9f, 0.1f, 0.15f),
            new Color(0.1f, 0.35f, 0.9f),
            new Color(0.1f, 0.75f, 0.3f),
            new Color(0.95f, 0.95f, 0.95f),
            new Color(0.1f, 0.1f, 0.1f),
            SuitUtil.Gold
        };

        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;

            GameObject stack = FX.Primitive(PrimitiveType.Cylinder, Vector3.zero, Vector3.one, colors[i], false, drumGo.transform);
            stack.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.07f, Mathf.Sin(angle) * 0.07f, 0f);
            stack.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            stack.transform.localScale = new Vector3(0.05f, 0.09f, 0.05f);
        }

        GameObject muzzleGo = new GameObject("Muzzle");
        muzzleGo.transform.SetParent(root.transform, false);
        muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.4f);
        view.muzzle = muzzleGo.transform;

        // Не должна попадать в лучи и тени
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return view;
    }

    public void Kick()
    {
        kick = 1f;
        spin += 60f;
    }

    private void Update()
    {
        kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 12f);

        transform.localPosition = basePosition + new Vector3(0f, 0f, -0.05f * kick);

        if (drum != null)
        {
            float current = drum.localEulerAngles.z;
            float target = Mathf.MoveTowardsAngle(current, current + spin, Time.deltaTime * 900f);
            spin = Mathf.Max(0f, spin - Mathf.Abs(Mathf.DeltaAngle(current, target)));
            drum.localRotation = Quaternion.Euler(0f, 0f, target);
        }
    }
}
