using System.Collections;
using UnityEngine;

public class KontenerMozgas : MonoBehaviour
{
    [Header("===== SZÁLLÍTÁS IDŐTARTAMA =====")]

    [Tooltip("Ennyi másodpercig tart a normál szállítási mozgás.")]
    [SerializeField] private float szallitasIdotartama = 60f;

    [Tooltip("A játék indulásakor automatikusan induljon el a konténer mozgása.")]
    [SerializeField] private bool automatikusInditas = true;


    [Header("===== FINOM RÁZKÓDÁS =====")]

    [Tooltip("A folyamatos apró rázkódás erőssége.")]
    [SerializeField] private float razkodasErossege = 0.015f;

    [Tooltip("Milyen gyors legyen az apró rázkódás.")]
    [SerializeField] private float razkodasSebessege = 7f;

    [Tooltip("Mennyire mozoghat oldalirányban a konténer.")]
    [SerializeField] private float oldalMozgas = 0.01f;

    [Tooltip("Mennyire mozoghat függőlegesen a konténer.")]
    [SerializeField] private float fuggolegesMozgas = 0.008f;

    [Tooltip("Mennyire mozoghat előre-hátra a konténer.")]
    [SerializeField] private float eloreHatramozgas = 0.005f;


    [Header("===== BILLEGÉS =====")]

    [Tooltip("Oldalirányú billegés maximális szöge fokban.")]
    [SerializeField] private float oldalBilleges = 0.8f;

    [Tooltip("Előre-hátra billegés maximális szöge fokban.")]
    [SerializeField] private float eloreHattraBilleges = 0.5f;

    [Tooltip("A lassú billegés sebessége.")]
    [SerializeField] private float billegesSebessege = 0.7f;


    [Header("===== VÉGSŐ NAGY RÁZKÓDÁS =====")]

    [Tooltip("A szállítás végén történő nagy rázkódás időtartama.")]
    [SerializeField] private float vegsoRazkodasIdotartama = 1.5f;

    [Tooltip("A végső rázkódás pozíciós erőssége.")]
    [SerializeField] private float vegsoRazkodasErossege = 0.08f;

    [Tooltip("A végső rázkódás forgási erőssége fokban.")]
    [SerializeField] private float vegsoForgasErossege = 4f;

    [Tooltip("A végső rázkódás sebessége.")]
    [SerializeField] private float vegsoRazkodasSebessege = 18f;


    [Header("===== LEÁLLÁS =====")]

    [Tooltip("A végén visszaálljon-e pontosan az eredeti pozícióba.")]
    [SerializeField] private bool visszaallEredetiHelyzetbe = true;


    private Vector3 eredetiPozicio;
    private Quaternion eredetiForgatas;

    private Coroutine mozgasCoroutine;

    private bool mozgasAktiv;


    private void Awake()
    {
        eredetiPozicio = transform.localPosition;
        eredetiForgatas = transform.localRotation;
    }


    private void Start()
    {
        if (automatikusInditas)
        {
            SzallitasInditasa();
        }
    }


    /// <summary>
    /// Elindítja a konténer szállítási mozgását.
    /// Meghívható más scriptből vagy Unity Eventből is.
    /// </summary>
    public void SzallitasInditasa()
    {
        if (mozgasCoroutine != null)
        {
            StopCoroutine(mozgasCoroutine);
        }

        mozgasCoroutine = StartCoroutine(Szallitas());
    }


    /// <summary>
    /// Azonnal leállítja a mozgást.
    /// </summary>
    public void SzallitasLeallitasa()
    {
        if (mozgasCoroutine != null)
        {
            StopCoroutine(mozgasCoroutine);
            mozgasCoroutine = null;
        }

        mozgasAktiv = false;

        if (visszaallEredetiHelyzetbe)
        {
            transform.localPosition = eredetiPozicio;
            transform.localRotation = eredetiForgatas;
        }
    }


    private IEnumerator Szallitas()
    {
        mozgasAktiv = true;

        float elteltIdo = 0f;

        // Véletlenszerű Perlin Noise indulási pontok,
        // hogy ne legyen minden alkalommal teljesen ugyanolyan.
        float zajX = Random.Range(0f, 1000f);
        float zajY = Random.Range(0f, 1000f);
        float zajZ = Random.Range(0f, 1000f);


        // =====================================================
        // NORMÁL SZÁLLÍTÁSI MOZGÁS
        // =====================================================

        while (elteltIdo < szallitasIdotartama)
        {
            elteltIdo += Time.deltaTime;

            float ido = Time.time;


            // -------------------------
            // FINOM POZÍCIÓS RÁZKÓDÁS
            // -------------------------

            float perlinX =
                Mathf.PerlinNoise(zajX, ido * razkodasSebessege) * 2f - 1f;

            float perlinY =
                Mathf.PerlinNoise(zajY, ido * razkodasSebessege) * 2f - 1f;

            float perlinZ =
                Mathf.PerlinNoise(zajZ, ido * razkodasSebessege) * 2f - 1f;


            Vector3 pozicioEltolas = new Vector3(
                perlinX * oldalMozgas,
                perlinY * fuggolegesMozgas,
                perlinZ * eloreHatramozgas
            );

            pozicioEltolas *= razkodasErossege * 100f;


            // -------------------------
            // LASSÚ BILLEGÉS
            // -------------------------

            float billegesX =
                Mathf.Sin(ido * billegesSebessege)
                * eloreHattraBilleges;

            float billegesZ =
                Mathf.Sin(
                    ido * billegesSebessege * 0.73f + 1.4f
                )
                * oldalBilleges;


            // Egy kis véletlenszerű forgási vibráció
            float randomForgasX = perlinX * razkodasErossege * 3f;
            float randomForgasZ = perlinZ * razkodasErossege * 3f;


            Quaternion forgatasEltolas =
                Quaternion.Euler(
                    billegesX + randomForgasX,
                    0f,
                    billegesZ + randomForgasZ
                );


            // -------------------------
            // MOZGÁS ALKALMAZÁSA
            // -------------------------

            transform.localPosition =
                eredetiPozicio + pozicioEltolas;

            transform.localRotation =
                eredetiForgatas * forgatasEltolas;


            yield return null;
        }


        // =====================================================
        // SZÁLLÍTÁS VÉGE - NAGY RÁZKÓDÁS
        // =====================================================

        yield return StartCoroutine(VegsoRazkodas());


        // =====================================================
        // LEÁLLÁS
        // =====================================================

        mozgasAktiv = false;

        if (visszaallEredetiHelyzetbe)
        {
            transform.localPosition = eredetiPozicio;
            transform.localRotation = eredetiForgatas;
        }

        mozgasCoroutine = null;
    }


    private IEnumerator VegsoRazkodas()
    {
        float elteltIdo = 0f;

        Vector3 kezdoPozicio = transform.localPosition;
        Quaternion kezdoForgatas = transform.localRotation;


        while (elteltIdo < vegsoRazkodasIdotartama)
        {
            elteltIdo += Time.deltaTime;

            float arany =
                elteltIdo / vegsoRazkodasIdotartama;


            // A rázkódás fokozatosan gyengül.
            float csillapitas = 1f - arany;


            float x =
                (Mathf.PerlinNoise(
                    Time.time * vegsoRazkodasSebessege,
                    0f
                ) * 2f - 1f)
                * vegsoRazkodasErossege
                * csillapitas;


            float y =
                (Mathf.PerlinNoise(
                    0f,
                    Time.time * vegsoRazkodasSebessege
                ) * 2f - 1f)
                * vegsoRazkodasErossege
                * csillapitas;


            float z =
                Random.Range(-1f, 1f)
                * vegsoRazkodasErossege
                * 0.5f
                * csillapitas;


            Vector3 pozicioRazkodas =
                new Vector3(x, y, z);


            Vector3 forgasiRazkodas =
                new Vector3(
                    Random.Range(
                        -vegsoForgasErossege,
                        vegsoForgasErossege
                    ),
                    Random.Range(
                        -vegsoForgasErossege * 0.25f,
                        vegsoForgasErossege * 0.25f
                    ),
                    Random.Range(
                        -vegsoForgasErossege,
                        vegsoForgasErossege
                    )
                )
                * csillapitas;


            transform.localPosition =
                kezdoPozicio + pozicioRazkodas;

            transform.localRotation =
                kezdoForgatas
                * Quaternion.Euler(forgasiRazkodas);


            yield return null;
        }
    }


    public bool MozgasAktiv()
    {
        return mozgasAktiv;
    }
}