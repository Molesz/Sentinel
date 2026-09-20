using UnityEngine;

[System.Serializable]
public class SentinelKameraBeallitasok
{
    [Header("Képkivágás")]

    [InspectorName("Távolság")]
    [Min(0.1f)]
    public float tavolsag = 6f;

    [InspectorName("Látószög")]
    [Range(15f, 80f)]
    public float latoszog = 35f;

    [InspectorName("Célpont eltolása")]
    [Tooltip("Világkoordinátában értendő. Y: a kép középpontjának magassága.")]
    public Vector3 celpontEltolas = new Vector3(0f, 0.3f, 0f);

    [Header("Követés")]

    [InspectorName("Vízszintes simítás")]
    [Tooltip("Másodperc. Nagyobb érték: lassabb, lágyabb követés.")]
    [Min(0.01f)]
    public float vizszintesSimítás = 0.35f;

    [InspectorName("Függőleges simítás")]
    [Min(0.01f)]
    public float fuggolegesSimítás = 0.8f;

    [InspectorName("Mélységi simítás")]
    [Min(0.01f)]
    public float melysegiSimítás = 0.5f;

    [InspectorName("Vízszintes holtzóna")]
    [Tooltip("A karakter ennyit mozdulhat oldalra kamerakövetés nélkül.")]
    [Min(0f)]
    public float vizszintesHoltzona = 0.2f;

    [InspectorName("Függőleges holtzóna")]
    [Tooltip("Az apró függőleges mozgásokat és kisebb ugrásokat tompítja.")]
    [Min(0f)]
    public float fuggolegesHoltzona = 0.35f;

    [Header("Előretekintés")]

    [InspectorName("Előretekintés távolsága")]
    [Min(0f)]
    public float eloretekintes = 0.65f;

    [InspectorName("Teljes előretekintés sebessége")]
    [Tooltip("Ekkora karaktersebességnél éri el a teljes előretekintést.")]
    [Min(0.01f)]
    public float teljesEloretekintesSebessege = 2.5f;
}

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class SentinelFollowCamera : MonoBehaviour
{
    [Header("Karakter")]

    [InspectorName("Követett karakter")]
    public Transform karakter;

    [InspectorName("Mozgási referencia")]
    [Tooltip(
        "Opcionális: a ténylegesen mozgó konténer vagy platform. " +
        "Így annak mozgása nem számít a karakter saját előretekintésébe."
    )]
    public Transform mozgasiReferencia;

    [Header("Alapbeállítások")]

    [InspectorName("Alap kamerabeállítások")]
    public SentinelKameraBeallitasok alap =
        new SentinelKameraBeallitasok();

    [InspectorName("Kamera szöge")]
    [Tooltip(
        "(0,0,0): negatív Z felől néz +Z felé. " +
        "(5,0,0): enyhén felülről néz."
    )]
    public Vector3 kameraSzoge = new Vector3(5f, 0f, 0f);

    [Header("Filmes átmenetek")]

    [InspectorName("Zónaváltás simítása")]
    [Min(0.01f)]
    public float zonavaltasSimítása = 1.2f;

    [InspectorName("Előretekintés simítása")]
    [Min(0.01f)]
    public float eloretekintesSimítása = 0.65f;

    [InspectorName("Sebességmérés simítása")]
    [Min(0.01f)]
    public float sebessegSimítása = 0.15f;

    [InspectorName("Megállás érzékelési küszöbe")]
    [Min(0f)]
    public float megallasKuszob = 0.08f;

    [InspectorName("Előretekintés megtartása megálláskor")]
    public bool eloretekintesMegtartasa = true;

    [Header("Kamerazónák")]

    [InspectorName("Zónák")]
    public SentinelCameraZone[] zonak;

    [Header("Követési középpont határai")]

    [InspectorName("Határok használata")]
    public bool hatarokHasznalata;

    [InspectorName("Határok referenciája")]
    [Tooltip(
        "Üresen világkoordináták. Mozgó konténer esetén add meg " +
        "a konténert, és a határok annak helyi koordinátáiban értendők."
    )]
    public Transform hatarReferencia;

    [InspectorName("Bal és jobb határ")]
    public Vector2 vizszintesHatar = new Vector2(-10f, 10f);

    [InspectorName("Alsó és felső határ")]
    public Vector2 fuggolegesHatar = new Vector2(-5f, 5f);

    [Header("Teljes konténer-képkivágás")]
    [Tooltip("A képkivágás a nyitott oldal keretén belül marad. A referencia +Z tengelye felé néz.")]
    public bool teljesKepHatarolasa;
    public Vector2 melysegiHatar = new Vector2(-1f, 1f);
    [Min(0f)] public float kepBiztonsagiSzegely = 0.04f;

    private Camera kamera;

    private bool inicializalt;
    private Transform elozoKarakter;
    private Transform elozoReferencia;

    private Vector3 elozoHelyiPozicio;
    private Vector3 kovetesiPont;
    private Vector3 kovetesiSebesseg;
    private Vector3 eltolas;
    private Vector3 eltolasSebesseg;

    private float mertSebesseg;
    private float mertSebessegValtozas;

    private float eloreCel;
    private float eloreAktualis;
    private float eloreSebesseg;

    private float tavolsag;
    private float tavolsagSebesseg;
    private float latoszog;
    private float latoszogSebesseg;

    private float vizSim;
    private float fuggSim;
    private float melySim;
    private float vizHolt;
    private float fuggHolt;

    private void Awake()
    {
        kamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        inicializalt = false;
    }

    // Teleportálás vagy újraéledés után is meghívható.
    [ContextMenu("Kamera azonnal a karakterhez")]
    public void AzonnaliIgazitas()
    {
        inicializalt = false;
    }

    private SentinelKameraBeallitasok AktivBeallitasok()
    {
        SentinelCameraZone nyertes = null;

        if (zonak != null)
        {
            foreach (SentinelCameraZone zona in zonak)
            {
                if (zona == null || !zona.isActiveAndEnabled)
                    continue;

                if (!zona.Tartalmazza(karakter.position))
                    continue;

                if (nyertes == null ||
                    zona.prioritas > nyertes.prioritas)
                {
                    nyertes = zona;
                }
            }
        }

        return nyertes != null ? nyertes.beallitasok : alap;
    }

    private Vector3 MozgasiPozicio()
    {
        if (mozgasiReferencia == null)
            return karakter.position;

        return mozgasiReferencia.InverseTransformPoint(
            karakter.position
        );
    }

    private static float HoltZonaCel(
        float jelenlegi,
        float cel,
        float felmeret)
    {
        float kulonbseg = cel - jelenlegi;

        if (Mathf.Abs(kulonbseg) <= felmeret)
            return jelenlegi;

        return cel - Mathf.Sign(kulonbseg) * felmeret;
    }

    private Vector3 HatarozottPont(Vector3 pont)
    {
        if (!hatarokHasznalata)
            return pont;

        if (hatarReferencia != null)
            pont = hatarReferencia.InverseTransformPoint(pont);

        pont.x = Mathf.Clamp(
            pont.x,
            Mathf.Min(vizszintesHatar.x, vizszintesHatar.y),
            Mathf.Max(vizszintesHatar.x, vizszintesHatar.y)
        );

        pont.y = Mathf.Clamp(
            pont.y,
            Mathf.Min(fuggolegesHatar.x, fuggolegesHatar.y),
            Mathf.Max(fuggolegesHatar.x, fuggolegesHatar.y)
        );

        if (hatarReferencia != null)
            pont = hatarReferencia.TransformPoint(pont);

        return pont;
    }

    private void Inicializalas(SentinelKameraBeallitasok b)
    {
        kovetesiPont = karakter.position;
        elozoHelyiPozicio = MozgasiPozicio();

        eltolas = b.celpontEltolas;
        tavolsag = Mathf.Max(0.1f, b.tavolsag);
        latoszog = Mathf.Clamp(b.latoszog, 15f, 80f);

        vizSim = b.vizszintesSimítás;
        fuggSim = b.fuggolegesSimítás;
        melySim = b.melysegiSimítás;
        vizHolt = b.vizszintesHoltzona;
        fuggHolt = b.fuggolegesHoltzona;

        kovetesiSebesseg = Vector3.zero;
        eltolasSebesseg = Vector3.zero;

        mertSebesseg = 0f;
        mertSebessegValtozas = 0f;
        eloreCel = 0f;
        eloreAktualis = 0f;
        eloreSebesseg = 0f;
        tavolsagSebesseg = 0f;
        latoszogSebesseg = 0f;

        elozoKarakter = karakter;
        elozoReferencia = mozgasiReferencia;
        inicializalt = true;
    }

    private void LateUpdate()
    {
        if (karakter == null)
        {
            inicializalt = false;
            return;
        }

        if (kamera == null)
            kamera = GetComponent<Camera>();

        SentinelKameraBeallitasok b = AktivBeallitasok();
        float dt = Time.deltaTime;

        if (!inicializalt ||
            elozoKarakter != karakter ||
            elozoReferencia != mozgasiReferencia)
        {
            Inicializalas(b);
        }
        else if (dt > 0f)
        {
            KovetesFrissitese(b, dt);
        }

        // Szünet alatt se keletkezzen hamis mozgássebesség.
        elozoHelyiPozicio = MozgasiPozicio();

        Quaternion forgatas = Quaternion.Euler(kameraSzoge);

        Vector3 nezettPont =
            kovetesiPont +
            eltolas +
            Vector3.right * eloreAktualis;

        nezettPont = HatarozottPont(nezettPont);

        Vector3 kameraPozicio =
            nezettPont -
            forgatas * Vector3.forward * tavolsag;

        transform.SetPositionAndRotation(
            kameraPozicio,
            forgatas
        );

        kamera.orthographic = false;
        kamera.usePhysicalProperties = false;
        kamera.fieldOfView = latoszog;
        if (hatarokHasznalata && teljesKepHatarolasa)
            KontenerKepHatarolasa();
    }

    // Clamp after smoothing. Side edges fit even at the back wall; vertical
    // edges fit the front opening. The solid floor/roof occlude deeper rays,
    // allowing the floor and puddles to remain visible below the character.
    public void KontenerKepHatarolasa()
    {
        if (kamera == null) kamera = GetComponent<Camera>();
        Vector3 p = hatarReferencia != null
            ? hatarReferencia.InverseTransformPoint(transform.position) : transform.position;
        float left = Mathf.Min(vizszintesHatar.x, vizszintesHatar.y) + kepBiztonsagiSzegely;
        float right = Mathf.Max(vizszintesHatar.x, vizszintesHatar.y) - kepBiztonsagiSzegely;
        float bottom = Mathf.Min(fuggolegesHatar.x, fuggolegesHatar.y) + kepBiztonsagiSzegely;
        float top = Mathf.Max(fuggolegesHatar.x, fuggolegesHatar.y) - kepBiztonsagiSzegely;
        float front = Mathf.Min(melysegiHatar.x, melysegiHatar.y);
        float back = Mathf.Max(melysegiHatar.x, melysegiHatar.y);
        Vector3 scale = hatarReferencia != null ? hatarReferencia.lossyScale : Vector3.one;
        float aspect = Mathf.Max(0.01f, kamera.aspect);
        float maxHalfHeight = Mathf.Max(0.01f, (top - bottom) * Mathf.Abs(scale.y) * 0.5f);
        float maxHalfWidth = Mathf.Max(0.01f, (right - left) * Mathf.Abs(scale.x) * 0.5f);
        float depth = (back - front) * Mathf.Abs(scale.z);
        float clearance = kamera.nearClipPlane + 0.02f;
        float tan = Mathf.Min(Mathf.Tan(kamera.fieldOfView * Mathf.Deg2Rad * 0.5f),
            Mathf.Min(maxHalfHeight / clearance, maxHalfWidth / ((depth + clearance) * aspect)));
        kamera.fieldOfView = 2f * Mathf.Atan(tan) * Mathf.Rad2Deg;
        float maxDistance = Mathf.Min(maxHalfHeight / tan + depth, maxHalfWidth / (tan * aspect));
        p.z = Mathf.Clamp(p.z, back - maxDistance / Mathf.Abs(scale.z),
            front - (kamera.nearClipPlane + 0.02f) / Mathf.Abs(scale.z));
        float halfX = (back - p.z) * Mathf.Abs(scale.z) * tan * aspect / Mathf.Abs(scale.x);
        float halfY = (front - p.z) * Mathf.Abs(scale.z) * tan / Mathf.Abs(scale.y);
        p.x = Mathf.Clamp(p.x, left + halfX, Mathf.Max(left + halfX, right - halfX));
        p.y = Mathf.Clamp(p.y, bottom + halfY, Mathf.Max(bottom + halfY, top - halfY));
        transform.SetPositionAndRotation(hatarReferencia != null ? hatarReferencia.TransformPoint(p) : p,
            hatarReferencia != null ? hatarReferencia.rotation : Quaternion.identity);
    }

    private void KovetesFrissitese(
        SentinelKameraBeallitasok b,
        float dt)
    {
        float atmenet = Mathf.Max(0.01f, zonavaltasSimítása);

        // Képkockasebességtől független átmenet.
        float s = 1f - Mathf.Exp(-dt / atmenet);

        vizSim = Mathf.Lerp(vizSim, b.vizszintesSimítás, s);
        fuggSim = Mathf.Lerp(fuggSim, b.fuggolegesSimítás, s);
        melySim = Mathf.Lerp(melySim, b.melysegiSimítás, s);
        vizHolt = Mathf.Lerp(vizHolt, b.vizszintesHoltzona, s);
        fuggHolt = Mathf.Lerp(fuggHolt, b.fuggolegesHoltzona, s);

        Vector3 helyiPozicio = MozgasiPozicio();
        Vector3 elmozdulas = helyiPozicio - elozoHelyiPozicio;

        if (mozgasiReferencia != null)
        {
            elmozdulas =
                mozgasiReferencia.TransformVector(elmozdulas);
        }

        float nyersSebesseg = elmozdulas.x / dt;

        mertSebesseg = Mathf.SmoothDamp(
            mertSebesseg,
            nyersSebesseg,
            ref mertSebessegValtozas,
            Mathf.Max(0.01f, sebessegSimítása),
            Mathf.Infinity,
            dt
        );

        if (Mathf.Abs(mertSebesseg) > megallasKuszob)
        {
            float arany = Mathf.Clamp(
                mertSebesseg /
                Mathf.Max(0.01f, b.teljesEloretekintesSebessege),
                -1f,
                1f
            );

            eloreCel = arany * b.eloretekintes;
        }
        else if (!eloretekintesMegtartasa)
        {
            eloreCel = 0f;
        }

        // Új zónában a kisebb előretekintési határt is betartja.
        eloreCel = Mathf.Clamp(
            eloreCel,
            -b.eloretekintes,
            b.eloretekintes
        );

        eloreAktualis = Mathf.SmoothDamp(
            eloreAktualis,
            eloreCel,
            ref eloreSebesseg,
            Mathf.Max(0.01f, eloretekintesSimítása),
            Mathf.Infinity,
            dt
        );

        Vector3 cel = karakter.position;

        cel.x = HoltZonaCel(
            kovetesiPont.x,
            cel.x,
            Mathf.Max(0f, vizHolt)
        );

        cel.y = HoltZonaCel(
            kovetesiPont.y,
            cel.y,
            Mathf.Max(0f, fuggHolt)
        );

        kovetesiPont.x = Mathf.SmoothDamp(
            kovetesiPont.x, cel.x,
            ref kovetesiSebesseg.x,
            Mathf.Max(0.01f, vizSim),
            Mathf.Infinity, dt
        );

        kovetesiPont.y = Mathf.SmoothDamp(
            kovetesiPont.y, cel.y,
            ref kovetesiSebesseg.y,
            Mathf.Max(0.01f, fuggSim),
            Mathf.Infinity, dt
        );

        kovetesiPont.z = Mathf.SmoothDamp(
            kovetesiPont.z, cel.z,
            ref kovetesiSebesseg.z,
            Mathf.Max(0.01f, melySim),
            Mathf.Infinity, dt
        );

        eltolas = Vector3.SmoothDamp(
            eltolas,
            b.celpontEltolas,
            ref eltolasSebesseg,
            atmenet,
            Mathf.Infinity,
            dt
        );

        tavolsag = Mathf.SmoothDamp(
            tavolsag,
            Mathf.Max(0.1f, b.tavolsag),
            ref tavolsagSebesseg,
            atmenet,
            Mathf.Infinity,
            dt
        );

        latoszog = Mathf.SmoothDamp(
            latoszog,
            Mathf.Clamp(b.latoszog, 15f, 80f),
            ref latoszogSebesseg,
            atmenet,
            Mathf.Infinity,
            dt
        );
    }
}
