using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class KontenerFenyMozgas : MonoBehaviour
{
    [Header("===== KAPCSOLAT A KONTÉNER MOZGÁSÁVAL =====")]

    [Tooltip("A KontenerMozgas script, amit figyelünk.")]
    [SerializeField] private KontenerMozgas kontenerMozgas;


    [Header("===== FÉNYEK SZÁMA =====")]

    [Tooltip("Ennyi mozgó fény haladjon el a konténer fölött.")]
    [Range(1, 10)]
    [SerializeField] private int fenyekSzama = 3;

    [Tooltip("A fények közötti távolság.")]
    [SerializeField] private float fenyekKozottiTavolsag = 8f;


    [Header("===== MOZGÁSI TERÜLET =====")]

    [Tooltip("Innen indulnak a fények a jobb oldalon.")]
    [SerializeField] private float jobbOldaliPozicio = 15f;

    [Tooltip("Eddig haladnak a fények balra.")]
    [SerializeField] private float balOldaliPozicio = -15f;

    [Tooltip("Milyen magasan legyenek a fények a konténer fölött.")]
    [SerializeField] private float fenyMagassag = 6f;

    [Tooltip("Mennyire legyenek előrébb vagy hátrébb a konténerhez képest.")]
    [SerializeField] private float melysegEltolas = 0f;


    [Header("===== MOZGÁS =====")]

    [Tooltip("Milyen gyorsan haladjanak a fények jobbról balra.")]
    [SerializeField] private float mozgasSebesseg = 6f;

    [Tooltip("Kis véletlenszerű sebességeltérés a fények között.")]
    [SerializeField] private float sebessegElteres = 0.5f;


    [Header("===== FÉNY BEÁLLÍTÁSOK =====")]

    [Tooltip("A fény intenzitása.")]
    [SerializeField] private float fenyIntenzitas = 8000f;

    [Tooltip("A fény hatótávolsága.")]
    [SerializeField] private float fenyHatar = 20f;

    [Tooltip("A Spot Light külső szöge.")]
    [Range(1f, 179f)]
    [SerializeField] private float spotSzog = 45f;

    [Tooltip("A fény színe.")]
    [SerializeField] private Color fenySzin = new Color(1f, 0.92f, 0.75f);

    [Tooltip("Bekapcsoljuk-e az árnyékokat.")]
    [SerializeField] private bool arnyekok = true;


    [Header("===== FÉNY IRÁNYA =====")]

    [Tooltip("A fény X tengely körüli dőlése.")]
    [SerializeField] private float fenyForgatasX = 65f;

    [Tooltip("A fény Y tengely körüli dőlése.")]
    [SerializeField] private float fenyForgatasY = -15f;

    [Tooltip("A fény Z tengely körüli dőlése.")]
    [SerializeField] private float fenyForgatasZ = 0f;


    [Header("===== HDRP VOLUMETRIKUS FÉNY =====")]

    [Tooltip("A fény hasson-e a volumetrikus ködre.")]
    [SerializeField] private bool volumetrikusFeny = true;

    [Tooltip("A volumetrikus fénycsóva erőssége.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumetrikusDimmer = 1f;

    [Tooltip("A volumetrikus árnyék erőssége.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumetrikusArnyekDimmer = 1f;


    [Header("===== PULZÁLÁS =====")]

    [Tooltip("Finom fényerő pulzálás a természetesebb hatásért.")]
    [SerializeField] private bool pulzalas = true;

    [Tooltip("A pulzálás mértéke.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float pulzalasErossege = 0.05f;

    [Tooltip("A pulzálás sebessége.")]
    [SerializeField] private float pulzalasSebessege = 2f;


    [Header("===== LEÁLLÁS =====")]

    [Tooltip("Ha a konténer megáll, a fények azonnal kapcsoljanak ki.")]
    [SerializeField] private bool azonnaliKikapcsolas = true;


    private class MozgoFeny
    {
        public GameObject objektum;
        public Light feny;
        public HDAdditionalLightData hdFeny;
        public float sebesseg;
        public float pulzalasEltolas;
    }


    private readonly List<MozgoFeny> fenyek =
        new List<MozgoFeny>();


    private void Start()
    {
        FenyekLetrehozasa();
    }


    private void Update()
    {
        if (kontenerMozgas == null)
            return;

        bool mozog =
            kontenerMozgas.MozgasAktiv();

        if (mozog)
        {
            FenyekBekapcsolasa();
            FenyekMozgatasa();
        }
        else
        {
            if (azonnaliKikapcsolas)
            {
                FenyekKikapcsolasa();
            }
        }
    }


    private void FenyekLetrehozasa()
    {
        for (int i = 0; i < fenyekSzama; i++)
        {
            GameObject ujFeny =
                new GameObject(
                    "Mozgo volumetrikus feny " + (i + 1)
                );

            ujFeny.transform.SetParent(transform);


            float kezdoX =
                jobbOldaliPozicio +
                (i * fenyekKozottiTavolsag);


            ujFeny.transform.localPosition =
                new Vector3(
                    kezdoX,
                    fenyMagassag,
                    melysegEltolas
                );


            ujFeny.transform.localRotation =
                Quaternion.Euler(
                    fenyForgatasX,
                    fenyForgatasY,
                    fenyForgatasZ
                );


            // =====================================================
            // UNITY SPOT LIGHT
            // =====================================================

            Light light =
                ujFeny.AddComponent<Light>();

            light.type = LightType.Spot;
            light.intensity = fenyIntenzitas;
            light.range = fenyHatar;
            light.spotAngle = spotSzog;
            light.color = fenySzin;

            light.shadows =
                arnyekok
                    ? LightShadows.Soft
                    : LightShadows.None;


            // =====================================================
            // HDRP VOLUMETRIKUS BEÁLLÍTÁS
            // =====================================================

            HDAdditionalLightData hdLight =
                ujFeny.GetComponent<HDAdditionalLightData>();

            if (hdLight == null)
            {
                hdLight =
                    ujFeny.AddComponent<HDAdditionalLightData>();
            }


            hdLight.affectsVolumetric =
                volumetrikusFeny;

            hdLight.volumetricDimmer =
                volumetrikusDimmer;

            hdLight.volumetricShadowDimmer =
                volumetrikusArnyekDimmer;


            // =====================================================
            // FÉNY ADATAINAK ELTÁROLÁSA
            // =====================================================

            MozgoFeny adat =
                new MozgoFeny
                {
                    objektum = ujFeny,

                    feny = light,

                    hdFeny = hdLight,

                    sebesseg =
                        mozgasSebesseg +
                        Random.Range(
                            -sebessegElteres,
                            sebessegElteres
                        ),

                    pulzalasEltolas =
                        Random.Range(0f, 10f)
                };


            fenyek.Add(adat);
        }
    }


    private void FenyekMozgatasa()
    {
        foreach (MozgoFeny adat in fenyek)
        {
            Vector3 pozicio =
                adat.objektum.transform.localPosition;


            pozicio.x -=
                adat.sebesseg *
                Time.deltaTime;


            // =====================================================
            // HA ELÉRTE A BAL OLDALT
            // =====================================================

            if (pozicio.x < balOldaliPozicio)
            {
                float legtavolabbiX =
                    LegtavolabbiJobbOldaliFeny();


                pozicio.x =
                    legtavolabbiX +
                    fenyekKozottiTavolsag;
            }


            adat.objektum.transform.localPosition =
                pozicio;


            // =====================================================
            // FINOM FÉNYERŐ PULZÁLÁS
            // =====================================================

            if (pulzalas)
            {
                float szorzo =
                    1f +
                    Mathf.Sin(
                        Time.time *
                        pulzalasSebessege +
                        adat.pulzalasEltolas
                    )
                    *
                    pulzalasErossege;


                adat.feny.intensity =
                    fenyIntenzitas *
                    szorzo;
            }
            else
            {
                adat.feny.intensity =
                    fenyIntenzitas;
            }
        }
    }


    private float LegtavolabbiJobbOldaliFeny()
    {
        float maximum =
            jobbOldaliPozicio;


        foreach (MozgoFeny adat in fenyek)
        {
            float x =
                adat.objektum.transform.localPosition.x;


            if (x > maximum)
            {
                maximum = x;
            }
        }


        return maximum;
    }


    private void FenyekBekapcsolasa()
    {
        foreach (MozgoFeny adat in fenyek)
        {
            if (!adat.objektum.activeSelf)
            {
                adat.objektum.SetActive(true);
            }
        }
    }


    private void FenyekKikapcsolasa()
    {
        foreach (MozgoFeny adat in fenyek)
        {
            if (adat.objektum.activeSelf)
            {
                adat.objektum.SetActive(false);
            }
        }
    }
}