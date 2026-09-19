using UnityEngine;

[DisallowMultipleComponent]
public class SentinelCameraZone : MonoBehaviour
{
    [Header("Zóna területe")]

    [InspectorName("Középpont")]
    public Vector3 kozeppont = Vector3.zero;

    [InspectorName("Méret")]
    public Vector3 meret = new Vector3(5f, 5f, 5f);

    [InspectorName("Prioritás")]
    [Tooltip(
        "Átfedõ zónák közül a nagyobb prioritás érvényesül. " +
        "Egyezésnél a kamera listájában elõbb szereplõ."
    )]
    public int prioritas;

    [Header("Kamera ezen a területen")]

    [InspectorName("Kamerabeállítások")]
    public SentinelKameraBeallitasok beallitasok =
        new SentinelKameraBeallitasok();

    public bool Tartalmazza(Vector3 vilagPozicio)
    {
        Vector3 helyi =
            transform.InverseTransformPoint(vilagPozicio) -
            kozeppont;

        Vector3 felmeret = new Vector3(
            Mathf.Abs(meret.x),
            Mathf.Abs(meret.y),
            Mathf.Abs(meret.z)
        ) * 0.5f;

        return
            Mathf.Abs(helyi.x) <= felmeret.x &&
            Mathf.Abs(helyi.y) <= felmeret.y &&
            Mathf.Abs(helyi.z) <= felmeret.z;
    }

    private void OnValidate()
    {
        meret = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(meret.x)),
            Mathf.Max(0.01f, Mathf.Abs(meret.y)),
            Mathf.Max(0.01f, Mathf.Abs(meret.z))
        );
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 elozoMatrix = Gizmos.matrix;
        Color elozoSzin = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.12f);
        Gizmos.DrawCube(kozeppont, meret);

        Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(kozeppont, meret);

        Gizmos.matrix = elozoMatrix;
        Gizmos.color = elozoSzin;
    }
}