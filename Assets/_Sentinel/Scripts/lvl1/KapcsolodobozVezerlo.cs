
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class KapcsolodobozVezerlo : MonoBehaviour
{
    [Header("JATEKOS ES INTERAKCIO")]

    [Tooltip("A jatekos karakter.")]
    [SerializeField]
    private Transform jatekos;

    [Tooltip("A kapcsolodoboz elotti interakcios pont.")]
    [SerializeField]
    private Transform interakciosPont;

    [Tooltip("Milyen messzirol lehessen hasznalni a kapcsolot?")]
    [Min(0.1f)]
    [SerializeField]
    private float interakciosTavolsag = 2f;


    [Header("KAPCSOLODOBOZ AJTAJA")]

    [Tooltip("A kapcsolodoboz_ajto objektum.")]
    [SerializeField]
    private Transform kapcsolodobozAjto;

    [Tooltip("Az ajto celpozicioja a helyi Z tengelyen.")]
    [SerializeField]
    private float ajtoNyitottSzog = 57f;

    [Tooltip("Az ajto nyitasi ideje masodpercben.")]
    [Min(0.01f)]
    [SerializeField]
    private float ajtoNyitasiIdo = 1.2f;


    [Header("KAPCSOLOKAR")]

    [Tooltip("A kapcsolokar objektum.")]
    [SerializeField]
    private Transform kapcsolokar;

    [Tooltip("A kar lehuzott pozicioja a helyi X tengelyen.")]
    [SerializeField]
    private float karLentiSzog = -134f;

    [Tooltip("A kapcsolokar mozgasideje masodpercben.")]
    [Min(0.01f)]
    [SerializeField]
    private float karMozgasiIdo = 0.7f;


    [Header("HAJOLAMPAK - POINT LIGHT")]

    [SerializeField]
    private Light elsoHajolampa;

    [SerializeField]
    private Light masodikHajolampa;

    [SerializeField]
    private Light harmadikHajolampa;


    [Header("TETOKABEL")]

    [Tooltip("A tetonyilasbol lelongo elektromos kabel.")]
    [SerializeField]
    private SentinelLiveRoofCable tetoKabel;


    // Az ajto es a kar eredeti forgatasa.

    private Quaternion ajtoKezdoForgatas;

    private Quaternion karKezdoForgatas;


    // A celpoziciok.

    private Quaternion ajtoCelForgatas;

    private Quaternion karCelForgatas;


    // Az interakcio allapotai.

    private bool ajtoNyitva = false;

    private bool karLehuzva = false;

    private bool mozgasFolyamatban = false;


    // ==================================================
    // INDULAS
    // ==================================================

    private void Start()
    {
        // Jatekos automatikus megkeresese.

        if (jatekos == null)
        {
            GameObject talaltJatekos =
                GameObject.FindGameObjectWithTag("Player");


            if (talaltJatekos != null)
            {
                jatekos = talaltJatekos.transform;
            }
        }


        // Kotelezo objektumok ellenorzese.

        if (
            jatekos == null ||
            kapcsolodobozAjto == null ||
            kapcsolokar == null
        )
        {
            Debug.LogError(
                "Kapcsolodoboz: Hianyzik a jatekos, " +
                "az ajto vagy a kapcsolokar referenciaja!",
                this
            );


            enabled = false;

            return;
        }


        // FONTOS:
        // Az eredeti forgatasokat elmentjuk,
        // de a modelleket NEM forgatjuk el.

        ajtoKezdoForgatas =
            kapcsolodobozAjto.localRotation;


        karKezdoForgatas =
            kapcsolokar.localRotation;


        // Az ajto eredeti X es Y szoge megmarad.
        // Csak a Z tengely celpoziciojat modositjuk.

        Vector3 ajtoKezdoEuler =
            kapcsolodobozAjto.localEulerAngles;


        ajtoCelForgatas = Quaternion.Euler(
            ajtoKezdoEuler.x,
            ajtoKezdoEuler.y,
            ajtoNyitottSzog
        );


        // A kapcsolokar eredeti Y es Z szoge megmarad.
        // A kart az X tengelyen forgatjuk.

        Vector3 karKezdoEuler =
            kapcsolokar.localEulerAngles;


        karCelForgatas = Quaternion.Euler(
            karLentiSzog,
            karKezdoEuler.y,
            karKezdoEuler.z
        );


        // Kezdeti allapotok.

        ajtoNyitva = false;

        karLehuzva = false;

        mozgasFolyamatban = false;


        // Tetokabel megkeresese, ha nincs megadva.

        if (tetoKabel == null)
        {
            tetoKabel =
                FindAnyObjectByType<SentinelLiveRoofCable>();
        }


        if (tetoKabel == null)
        {
            Debug.LogWarning(
                "Kapcsolodoboz: A tetokabel nem talalhato. " +
                "A hajolampak ettol fuggetlenul mukodnek.",
                this
            );
        }
    }


    // ==================================================
    // INTERAKCIO
    // ==================================================

    private void Update()
    {
        // Ha a kar mar le van huzva,
        // nem lehet ujra hasznalni.

        if (karLehuzva)
        {
            return;
        }


        // Mozgas kozben nem lehet uj interakciot inditani.

        if (mozgasFolyamatban)
        {
            return;
        }


        if (Keyboard.current == null)
        {
            return;
        }


        // E billentyu ellenorzese.

        if (!Keyboard.current.eKey.wasPressedThisFrame)
        {
            return;
        }


        // Jatekos tavolsaganak ellenorzese.

        Vector3 kapcsoloPozicio =
            interakciosPont != null
                ? interakciosPont.position
                : transform.position;


        float tavolsag = Vector3.Distance(
            jatekos.position,
            kapcsoloPozicio
        );


        if (tavolsag > interakciosTavolsag)
        {
            return;
        }


        // ELSO E:
        // Ha az ajto zarva van, kinyitjuk.

        if (!ajtoNyitva)
        {
            StartCoroutine(AjtoKinyitasa());

            return;
        }


        // MASODIK E:
        // A kart csak a teljesen nyitott ajto
        // utan lehet lehuzni.

        if (ajtoNyitva && !karLehuzva)
        {
            StartCoroutine(KarLehuzasa());
        }
    }


    // ==================================================
    // AJTO KINYITASA
    // ==================================================

    private IEnumerator AjtoKinyitasa()
    {
        mozgasFolyamatban = true;


        float elteltIdo = 0f;


        while (elteltIdo < ajtoNyitasiIdo)
        {
            elteltIdo += Time.deltaTime;


            float arany = Mathf.Clamp01(
                elteltIdo / ajtoNyitasiIdo
            );


            // Finom gyorsulas es lassulas.

            float simitottArany = Mathf.SmoothStep(
                0f,
                1f,
                arany
            );


            // Az ajtot a Scene-ben beallitott
            // eredeti forgatasatol mozgatjuk
            // a kivant celpozicioig.

            kapcsolodobozAjto.localRotation =
                Quaternion.Slerp(
                    ajtoKezdoForgatas,
                    ajtoCelForgatas,
                    simitottArany
                );


            yield return null;
        }


        // Az ajto pontosan a celpozicioba kerul.

        kapcsolodobozAjto.localRotation =
            ajtoCelForgatas;


        // Csak a teljes nyitas utan engedelyezzuk
        // a kapcsolokar hasznalatat.

        ajtoNyitva = true;

        mozgasFolyamatban = false;
    }


    // ==================================================
    // KAR LEHUZASA
    // ==================================================

    private IEnumerator KarLehuzasa()
    {
        mozgasFolyamatban = true;


        float elteltIdo = 0f;


        while (elteltIdo < karMozgasiIdo)
        {
            elteltIdo += Time.deltaTime;


            float arany = Mathf.Clamp01(
                elteltIdo / karMozgasiIdo
            );


            float simitottArany = Mathf.SmoothStep(
                0f,
                1f,
                arany
            );


            // A kar az eredeti forgatasatol
            // az X tengelyen beallitott
            // celpozicioig mozdul el.

            kapcsolokar.localRotation =
                Quaternion.Slerp(
                    karKezdoForgatas,
                    karCelForgatas,
                    simitottArany
                );


            yield return null;
        }


        // A kar eleri a vegso poziciojat.

        kapcsolokar.localRotation =
            karCelForgatas;


        karLehuzva = true;


        // Hajolampak es tetokabel aramtalanitasa.

        RendszerAramtalanitasa();


        mozgasFolyamatban = false;
    }


    // ==================================================
    // TELJES ARAMTALANITAS
    // ==================================================

    private void RendszerAramtalanitasa()
    {
        // Elso hajolampa kikapcsolasa.

        if (elsoHajolampa != null)
        {
            elsoHajolampa.enabled = false;
        }


        // Masodik hajolampa kikapcsolasa.

        if (masodikHajolampa != null)
        {
            masodikHajolampa.enabled = false;
        }


        // Harmadik hajolampa kikapcsolasa.

        if (harmadikHajolampa != null)
        {
            harmadikHajolampa.enabled = false;
        }


        // Tetokabel megkeresese.

        if (tetoKabel == null)
        {
            tetoKabel =
                FindAnyObjectByType<SentinelLiveRoofCable>();
        }


        // Tetokabel aramtalanitasa.

        if (tetoKabel != null)
        {
            tetoKabel.Aramtalanitas();
        }
        else
        {
            Debug.LogWarning(
                "Kapcsolodoboz: A tetokabel nem talalhato!",
                this
            );
        }


        Debug.Log(
            "Kapcsolodoboz: A kapcsolokar lehuzva. " +
            "A hajolampak es a tetokabel aramtalanitva.",
            this
        );
    }
}