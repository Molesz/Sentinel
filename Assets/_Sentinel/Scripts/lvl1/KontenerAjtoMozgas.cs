
using UnityEngine;

public class KontenerAjtoMozgas : MonoBehaviour
{
    [Header("KONTÉNER")]
    [Tooltip("Az a konténer objektum, amelynek a mozgását figyeljük.")]
    [SerializeField] private Transform kontenerAlap;

    [Header("AJTÓ MOZGÁSA")]
    [Tooltip("Az ajtó legkisebb Y elfordulása.")]
    [SerializeField, Range(-90f, 0f)]
    private float minimumSzog = -10f;

    [Tooltip("Az ajtó legnagyobb Y elfordulása.")]
    [SerializeField, Range(0f, 90f)]
    private float maximumSzog = 10f;

    [Tooltip("Az ajtó lengésének sebessége.")]
    [SerializeField, Range(0.1f, 10f)]
    private float lengesSebesseg = 1.5f;

    [Header("MOZGÁSÉRZÉKELÉS")]
    [Tooltip("A konténer legkisebb érzékelhetõ elmozdulása.")]
    [SerializeField]
    private float mozgasKuszob = 0.00001f;

    [Tooltip("A konténer legkisebb érzékelhetõ elfordulása.")]
    [SerializeField]
    private float forgatasKuszob = 0.0001f;

    private Vector3 elozoPozicio;
    private Quaternion elozoForgatas;

    private float lengesIdo;
    private Vector3 eredetiForgatas;

    private bool inicializalva;

    private void Start()
    {
        if (kontenerAlap == null)
        {
            Debug.LogError(
                "KontenerAjtoMozgas: Nincs megadva a konténer alap!",
                this
            );

            enabled = false;
            return;
        }

        elozoPozicio = kontenerAlap.position;
        elozoForgatas = kontenerAlap.rotation;

        eredetiForgatas = transform.localEulerAngles;

        // Az induló szöghöz igazítjuk a lengést.
        float kezdoSzog =
            Mathf.DeltaAngle(0f, eredetiForgatas.y);

        kezdoSzog = Mathf.Clamp(
            kezdoSzog,
            minimumSzog,
            maximumSzog
        );

        float kozep = (minimumSzog + maximumSzog) * 0.5f;

        float amplitudo =
            (maximumSzog - minimumSzog) * 0.5f;

        if (amplitudo > 0f)
        {
            lengesIdo = Mathf.Asin(
                Mathf.Clamp(
                    (kezdoSzog - kozep) / amplitudo,
                    -1f,
                    1f
                )
            ) / lengesSebesseg;
        }

        inicializalva = true;
    }

    private void LateUpdate()
    {
        if (!inicializalva)
            return;

        // Megnézzük, elmozdult-e a konténer.
        float elmozdulas =
            Vector3.Distance(
                kontenerAlap.position,
                elozoPozicio
            );

        // Megnézzük, elfordult-e a konténer.
        float elfordulas =
            Quaternion.Angle(
                kontenerAlap.rotation,
                elozoForgatas
            );

        bool kontenerMozog =
            elmozdulas > mozgasKuszob ||
            elfordulas > forgatasKuszob;

        // Elmentjük az aktuális állapotot.
        elozoPozicio = kontenerAlap.position;
        elozoForgatas = kontenerAlap.rotation;

        // Ha a konténer áll, az ajtó sem mozog.
        if (!kontenerMozog)
            return;

        // Lengés idõzítése.
        lengesIdo += Time.deltaTime * lengesSebesseg;

        float kozep =
            (minimumSzog + maximumSzog) * 0.5f;

        float amplitudo =
            (maximumSzog - minimumSzog) * 0.5f;

        // Folyamatos lengés a két szög között.
        float aktualisSzog =
            kozep + Mathf.Sin(lengesIdo) * amplitudo;

        // Csak az ajtó Y tengelyét forgatjuk.
        transform.localRotation = Quaternion.Euler(
            eredetiForgatas.x,
            aktualisSzog,
            eredetiForgatas.z
        );
    }
}