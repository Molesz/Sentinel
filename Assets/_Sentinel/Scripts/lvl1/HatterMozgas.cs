using UnityEngine;

public class HatterMozgas : MonoBehaviour
{
    [Header("===== HÁTTÉR MOZGÁS =====")]

    [Tooltip("A konténer mozgását vezérlõ script.")]
    [SerializeField] private KontenerMozgas kontenerMozgas;

    [Tooltip("Milyen lassan mozogjon balra a háttér.")]
    [SerializeField] private float mozgasSebesseg = 0.08f;

    private void Update()
    {
        if (kontenerMozgas == null)
            return;

        if (!kontenerMozgas.MozgasAktiv())
            return;

        transform.position += Vector3.left * mozgasSebesseg * Time.deltaTime;
    }
}