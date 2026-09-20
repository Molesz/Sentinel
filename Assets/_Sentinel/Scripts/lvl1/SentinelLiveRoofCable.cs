using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DefaultExecutionOrder(50)]
public sealed class SentinelLiveRoofCable : MonoBehaviour
{
    const int PointCount = 17;
    const int StrandCount = 3;
    const int StrandPoints = 5;
    const int ConstraintPasses = 16;
    const int MaxArcs = 6;
    const int MaxArcKnots = 11;
    const int MaxEmbers = 40;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void PlaceInLoadedScene()
    {
        if (FindAnyObjectByType<SentinelLiveRoofCable>() != null) return;
        var motion = FindAnyObjectByType<KontenerMozgas>();
        if (motion == null) return;
        var rain = FindAnyObjectByType<SentinelContainerRain>();
        Transform parent = rain != null ? rain.transform.parent : motion.transform;
        var go = new GameObject("Live Roof Cable");
        go.transform.SetParent(parent, false);
        go.AddComponent<SentinelLiveRoofCable>();
    }

    readonly Vector3[] points = new Vector3[PointCount];
    readonly Vector3[] previous = new Vector3[PointCount];
    readonly Vector3[][] strandPoints = new Vector3[StrandCount][];
    readonly Vector3[][] strandPrevious = new Vector3[StrandCount][];
    readonly LineRenderer[] strands = new LineRenderer[StrandCount];
    readonly Arc[] arcs = new Arc[MaxArcs];
    readonly Ember[] embers = new Ember[MaxEmbers];
    readonly List<Vector3> vertices = new List<Vector3>(1024);
    readonly List<Vector2> uv = new List<Vector2>(1024);
    readonly List<Color> colors = new List<Color>(1024);
    readonly List<int> indices = new List<int>(1536);

    KontenerMozgas container;
    Transform anchor;
    LineRenderer cable;
    Mesh sparkMesh;
    MeshFilter sparkFilter;
    Texture2D filament;
    Light flash;
    HDAdditionalLightData flashHd;
    Vector3 lastAnchor;
    Vector3 lastAnchorVelocity;
    float nextDischarge;
    float flashPower;
    bool built;

    struct Arc
    {
        public bool active;
        public int knots;
        public int branchFrom;
        public int branchKnots;
        public float age;
        public float life;
        public float width;
        public Vector3[] path;
        public Vector3[] branch;
    }

    struct Ember
    {
        public bool active;
        public Vector3 position;
        public Vector3 velocity;
        public float age;
        public float life;
        public float size;
    }

    void Awake()
    {
        var rain = GetComponentInParent<SentinelContainerRain>();
        if (rain == null) rain = FindAnyObjectByType<SentinelContainerRain>();
        container = GetComponentInParent<KontenerMozgas>();
        if (container == null) container = FindAnyObjectByType<KontenerMozgas>();
        Build(rain);
    }

    void Build(SentinelContainerRain rain)
    {
        Vector3 localAnchor = rain != null
            ? rain.openingCenter + new Vector3(-rain.openingSize.x * 0.5f + 0.07f, 0.03f, 0.02f)
            : new Vector3(-6.63f, 1.48f, -0.08f);

        anchor = new GameObject("Cable Anchor").transform;
        anchor.SetParent(transform, false);
        anchor.localPosition = localAnchor;

        Vector3 pin = anchor.position;
        Vector3 down = -transform.up;
        const float spacing = 0.062f;
        for (int i = 0; i < PointCount; i++)
            points[i] = previous[i] = pin + down * (spacing * i);

        cable = gameObject.AddComponent<LineRenderer>();
        cable.positionCount = PointCount;
        cable.useWorldSpace = true;
        cable.widthMultiplier = 0.026f;
        cable.numCapVertices = 5;
        cable.numCornerVertices = 3;
        cable.shadowCastingMode = ShadowCastingMode.On;
        cable.alignment = LineAlignment.View;
        cable.textureMode = LineTextureMode.Tile;
        cable.sharedMaterial = MakeMaterial("MAT_Runtime_LiveCable", new Color(0.045f, 0.04f, 0.038f), 0f);

        var clamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clamp.name = "Cable Clamp";
        clamp.transform.SetParent(anchor, false);
        clamp.transform.localPosition = new Vector3(0f, 0.018f, 0f);
        clamp.transform.localScale = new Vector3(0.07f, 0.028f, 0.055f);
        clamp.GetComponent<MeshRenderer>().sharedMaterial =
            MakeMaterial("MAT_Runtime_CableClamp", new Color(0.12f, 0.11f, 0.1f), 0f);
        Destroy(clamp.GetComponent<Collider>());

        var copper = MakeMaterial("MAT_Runtime_CableCopper", new Color(0.42f, 0.22f, 0.08f), 0f);
        Vector3 tip = points[PointCount - 1];
        for (int s = 0; s < StrandCount; s++)
        {
            strandPoints[s] = new Vector3[StrandPoints];
            strandPrevious[s] = new Vector3[StrandPoints];
            Vector3 offset = transform.TransformDirection(new Vector3((s - 1) * 0.006f, 0f, (s == 1 ? 0.004f : -0.003f)));
            for (int i = 0; i < StrandPoints; i++)
                strandPoints[s][i] = strandPrevious[s][i] = tip + offset + down * (0.011f * i);
            var strandObject = new GameObject("Copper Strand " + s);
            strandObject.transform.SetParent(transform, false);
            var line = strandObject.AddComponent<LineRenderer>();
            line.positionCount = StrandPoints;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.0034f;
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.sharedMaterial = copper;
            strands[s] = line;
        }

        for (int i = 0; i < MaxArcs; i++)
            arcs[i] = new Arc { path = new Vector3[MaxArcKnots], branch = new Vector3[6] };

        var sparkObject = new GameObject("Cable Discharges");
        sparkObject.transform.SetParent(transform, false);
        sparkFilter = sparkObject.AddComponent<MeshFilter>();
        var sparkRenderer = sparkObject.AddComponent<MeshRenderer>();
        sparkMesh = new Mesh { name = "Live cable discharges" };
        sparkMesh.MarkDynamic();
        sparkFilter.sharedMesh = sparkMesh;
        filament = CreateFilamentTexture();
        sparkRenderer.sharedMaterial = MakeSparkMaterial(filament);
        sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
        sparkRenderer.receiveShadows = false;
        sparkRenderer.lightProbeUsage = LightProbeUsage.Off;

        var lightObject = new GameObject("Arc Flash");
        lightObject.transform.SetParent(transform, false);
        flash = lightObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(0.72f, 0.84f, 1f);
        flash.range = 0.28f;
        flash.intensity = 0f;
        flash.shadows = LightShadows.None;
        flash.enabled = false;
        flashHd = lightObject.GetComponent<HDAdditionalLightData>();
        if (flashHd == null) flashHd = lightObject.AddComponent<HDAdditionalLightData>();
        flashHd.affectsVolumetric = false;
        flashHd.volumetricDimmer = 0f;

        lastAnchor = pin;
        lastAnchorVelocity = Vector3.zero;
        nextDischarge = Random.Range(0.2f, 0.7f);
        built = true;
    }

    void FixedUpdate()
    {
        if (!built) return;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        Vector3 pin = anchor.position;
        Vector3 gravity = Physics.gravity;
        Vector3 anchorVelocity = (pin - lastAnchor) / dt;
        Vector3 acceleration = (anchorVelocity - lastAnchorVelocity) / dt;
        lastAnchor = pin;
        lastAnchorVelocity = anchorVelocity;

        bool shaking = container != null && container.MozgasAktiv();
        float accel = acceleration.magnitude;
        float motion = shaking ? Mathf.Clamp01(0.45f + accel * 0.012f) : Mathf.Clamp01(accel * 0.008f);
        float damping = Mathf.Lerp(0.948f, 0.996f, motion);
        Vector3 inertia = shaking || accel > 0.4f
            ? -acceleration * (Mathf.Lerp(0.008f, 0.028f, motion) * dt * dt)
            : Vector3.zero;

        float swayTime = Time.time;
        float idle = 1f - motion;
        Vector3 breeze = transform.TransformDirection(new Vector3(
            Mathf.Sin(swayTime * 0.63f) * 0.22f + Mathf.Sin(swayTime * 1.17f + 0.7f) * 0.08f,
            0f,
            Mathf.Sin(swayTime * 0.81f + 1.3f) * 0.14f));
        Vector3 sway = breeze * (idle * idle);

        const float spacing = 0.062f;
        points[0] = pin;
        previous[0] = pin;
        for (int i = 1; i < PointCount; i++)
        {
            float tip = i / (float)(PointCount - 1);
            Vector3 current = points[i];
            Vector3 velocity = (current - previous[i]) * damping;
            previous[i] = current;
            points[i] = current + velocity + (gravity + sway * (tip * tip)) * (dt * dt) + inertia * (tip * tip);
        }

        for (int pass = 0; pass < ConstraintPasses; pass++)
        {
            points[0] = pin;
            for (int i = 0; i < PointCount - 1; i++)
            {
                Vector3 delta = points[i + 1] - points[i];
                float distance = delta.magnitude;
                if (distance < 0.00001f) continue;
                Vector3 correction = delta * ((distance - spacing) / distance);
                if (i == 0) points[i + 1] -= correction;
                else
                {
                    points[i] += correction * 0.5f;
                    points[i + 1] -= correction * 0.5f;
                }
            }
        }
        points[0] = pin;

        Vector3 cableTip = points[PointCount - 1];
        Vector3 cableTangent = (points[PointCount - 1] - points[PointCount - 2]).normalized;
        float strandDamping = Mathf.Lerp(0.91f, 0.985f, motion);
        for (int s = 0; s < StrandCount; s++)
        {
            Vector3 side = transform.TransformDirection(new Vector3((s - 1) * 0.0055f, 0f, s == 1 ? 0.0035f : -0.0028f));
            strandPoints[s][0] = cableTip + side;
            strandPrevious[s][0] = strandPoints[s][0];
            const float strandSpacing = 0.0105f;
            for (int i = 1; i < StrandPoints; i++)
            {
                float tip = i / (float)(StrandPoints - 1);
                Vector3 current = strandPoints[s][i];
                Vector3 velocity = (current - strandPrevious[s][i]) * strandDamping;
                strandPrevious[s][i] = current;
                Vector3 twitch = shaking
                    ? Random.insideUnitSphere * (0.35f * motion * dt * dt)
                    : Vector3.zero;
                strandPoints[s][i] = current + velocity + gravity * (dt * dt) + twitch + inertia * tip;
            }
            for (int pass = 0; pass < 8; pass++)
            {
                strandPoints[s][0] = cableTip + side;
                for (int i = 0; i < StrandPoints - 1; i++)
                {
                    Vector3 delta = strandPoints[s][i + 1] - strandPoints[s][i];
                    float distance = delta.magnitude;
                    if (distance < 0.00001f) continue;
                    Vector3 correction = delta * ((distance - strandSpacing) / distance);
                    if (i == 0) strandPoints[s][i + 1] -= correction;
                    else
                    {
                        strandPoints[s][i] += correction * 0.5f;
                        strandPoints[s][i + 1] -= correction * 0.5f;
                    }
                }
            }
            strandPoints[s][0] = cableTip + side + cableTangent * 0.004f;
        }
    }

    void LateUpdate()
    {
        if (!built) return;
        for (int i = 0; i < PointCount; i++) cable.SetPosition(i, points[i]);
        for (int s = 0; s < StrandCount; s++)
            for (int i = 0; i < StrandPoints; i++)
                strands[s].SetPosition(i, strandPoints[s][i]);
        UpdateElectricity();
    }

    void UpdateElectricity()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool shaking = container != null && container.MozgasAktiv();
        float accel = lastAnchorVelocity.magnitude;
        float motion = shaking ? Mathf.Clamp01(0.5f + accel * 0.8f) : 0f;

        nextDischarge -= dt;
        if (nextDischarge <= 0f)
        {
            SpawnDischarge(motion);
            if (shaking && Random.value < 0.35f) SpawnDischarge(motion);
            nextDischarge = shaking
                ? Random.Range(0.035f, 0.16f)
                : Random.Range(0.55f, 2.4f) * Random.Range(0.7f, 1.3f);
        }

        Vector3 view = ViewForward();
        vertices.Clear();
        uv.Clear();
        colors.Clear();
        indices.Clear();
        flashPower = Mathf.MoveTowards(flashPower, 0f, dt * 28f);
        Vector3 flashAt = strandPoints[0][StrandPoints - 1];

        for (int i = 0; i < MaxArcs; i++)
        {
            if (!arcs[i].active) continue;
            arcs[i].age += dt;
            if (arcs[i].age >= arcs[i].life)
            {
                arcs[i].active = false;
                continue;
            }
            float fade = 1f - arcs[i].age / arcs[i].life;
            fade *= fade;
            DrawArc(arcs[i].path, arcs[i].knots, arcs[i].width, fade, view);
            if (arcs[i].branchKnots > 1)
                DrawArc(arcs[i].branch, arcs[i].branchKnots, arcs[i].width * 0.55f, fade * 0.8f, view);
            flashPower = Mathf.Max(flashPower, fade);
            flashAt = arcs[i].path[0];
        }

        for (int i = 0; i < MaxEmbers; i++)
        {
            if (!embers[i].active) continue;
            embers[i].age += dt;
            if (embers[i].age >= embers[i].life)
            {
                embers[i].active = false;
                continue;
            }
            embers[i].velocity += Physics.gravity * (1.6f * dt);
            embers[i].velocity *= 1f - 2.8f * dt;
            embers[i].position += embers[i].velocity * dt;
            float t = 1f - embers[i].age / embers[i].life;
            Color heat = Blackbody(t);
            float size = embers[i].size * Mathf.Lerp(0.35f, 1f, t);
            SparkQuad(embers[i].position, embers[i].position + embers[i].velocity.normalized * (size * 1.8f),
                size * 0.5f, heat, view);
        }

        sparkMesh.Clear();
        if (vertices.Count > 0)
        {
            sparkMesh.SetVertices(vertices);
            sparkMesh.SetUVs(0, uv);
            sparkMesh.SetColors(colors);
            sparkMesh.SetTriangles(indices, 0);
            sparkMesh.RecalculateBounds();
        }

        bool lit = flashPower > 0.02f;
        flash.enabled = lit;
        if (lit)
        {
            flash.transform.position = flashAt;
            float jitter = 0.55f + Random.value * 0.55f;
            flash.intensity = (8f + flashPower * Mathf.Lerp(35f, 95f, motion)) * jitter;
            flash.color = Color.Lerp(new Color(0.55f, 0.7f, 1f), new Color(0.95f, 0.97f, 1f), Random.value);
        }
    }

    void SpawnDischarge(float motion)
    {
        int slot = -1;
        for (int i = 0; i < MaxArcs; i++)
            if (!arcs[i].active) { slot = i; break; }
        if (slot < 0) slot = Random.Range(0, MaxArcs);

        int strand = Random.Range(0, StrandCount);
        Vector3 origin;
        Vector3 along;
        if (Random.value < 0.78f)
        {
            origin = strandPoints[strand][StrandPoints - 1];
            along = (strandPoints[strand][StrandPoints - 1] - strandPoints[strand][StrandPoints - 2]).normalized;
        }
        else
        {
            float t = 0.78f + Random.value * 0.2f;
            origin = PointAt(t);
            along = TangentAt(t);
        }

        Vector3 side = Vector3.Cross(along, Random.onUnitSphere);
        if (side.sqrMagnitude < 0.0001f) side = transform.right;
        side.Normalize();
        float reach = Mathf.Lerp(0.028f, 0.11f, motion) * Random.Range(0.7f, 1.25f);
        if (Random.value < 0.18f) reach *= 1.6f;

        int knots = Mathf.Clamp(4 + (int)(reach * 40f) + Random.Range(0, 3), 4, MaxArcKnots);
        Vector3 cursor = origin;
        Vector3 dir = (along * Random.Range(0.35f, 0.8f) + side * Random.Range(0.4f, 1f) + Vector3.down * Random.Range(0.2f, 0.7f)).normalized;
        arcs[slot].path[0] = origin;
        for (int k = 1; k < knots; k++)
        {
            float u = k / (float)(knots - 1);
            Vector3 jag = Vector3.Cross(dir, Random.onUnitSphere);
            if (jag.sqrMagnitude < 0.0001f) jag = side;
            jag.Normalize();
            float step = reach * (0.12f + Random.Range(0f, 0.16f)) * (1.15f - u * 0.4f);
            dir = (dir + jag * Random.Range(-0.85f, 0.85f) + Vector3.down * 0.12f).normalized;
            cursor += dir * step;
            arcs[slot].path[k] = cursor;
        }

        int branchKnots = 0;
        if (Random.value < Mathf.Lerp(0.15f, 0.55f, motion) && knots > 4)
        {
            int from = Random.Range(2, knots - 1);
            Vector3 b = arcs[slot].path[from];
            Vector3 bDir = Vector3.Cross(dir, Random.onUnitSphere).normalized;
            if (bDir.sqrMagnitude < 0.01f) bDir = side;
            branchKnots = Random.Range(3, 6);
            arcs[slot].branch[0] = b;
            for (int k = 1; k < branchKnots; k++)
            {
                b += (bDir + Random.insideUnitSphere * 0.8f + Vector3.down * 0.2f).normalized * (reach * Random.Range(0.08f, 0.18f));
                arcs[slot].branch[k] = b;
            }
        }

        arcs[slot].active = true;
        arcs[slot].knots = knots;
        arcs[slot].branchKnots = branchKnots;
        arcs[slot].age = 0f;
        arcs[slot].life = Random.Range(0.018f, 0.042f);
        arcs[slot].width = Random.Range(0.0007f, 0.0016f);
        flashPower = 1f;

        int burst = Random.Range(2, 6 + (int)(motion * 4f));
        for (int e = 0; e < burst; e++) SpawnEmber(origin, dir, motion);
    }

    void SpawnEmber(Vector3 origin, Vector3 dir, float motion)
    {
        int slot = -1;
        for (int i = 0; i < MaxEmbers; i++)
            if (!embers[i].active) { slot = i; break; }
        if (slot < 0) return;
        Vector3 kick = (dir + Random.insideUnitSphere * 0.9f).normalized;
        embers[slot] = new Ember
        {
            active = true,
            position = origin + Random.insideUnitSphere * 0.004f,
            velocity = kick * Random.Range(0.45f, 1.6f + motion * 0.8f) + Vector3.down * Random.Range(0.05f, 0.25f),
            age = 0f,
            life = Random.Range(0.07f, 0.22f),
            size = Random.Range(0.0011f, 0.0024f)
        };
    }

    void DrawArc(Vector3[] path, int knots, float width, float fade, Vector3 view)
    {
        if (knots < 2) return;
        for (int i = 0; i < knots - 1; i++)
        {
            float u = i / (float)(knots - 1);
            float core = width * Mathf.Lerp(1.1f, 0.28f, u);
            Color plasma = Color.Lerp(new Color(0.92f, 0.96f, 1f, fade), new Color(0.55f, 0.72f, 1f, fade * 0.4f), u);
            SparkQuad(path[i], path[i + 1], core, plasma, view);
        }
    }

    void SparkQuad(Vector3 from, Vector3 to, float width, Color color, Vector3 view)
    {
        Vector3 along = to - from;
        if (along.sqrMagnitude < 1e-10f) return;
        Vector3 side = Vector3.Cross(along.normalized, view);
        if (side.sqrMagnitude < 0.05f) side = Vector3.Cross(along.normalized, Vector3.up);
        if (side.sqrMagnitude < 0.05f) side = Vector3.right;
        side = transform.InverseTransformDirection(side.normalized) * (width * 0.5f);
        Vector3 a = ToLocal(from) - side;
        Vector3 b = ToLocal(to) - side;
        Vector3 c = ToLocal(to) + side;
        Vector3 d = ToLocal(from) + side;
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f));
        uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
        colors.Add(color); colors.Add(color); colors.Add(color); colors.Add(color);
        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
    }

    Vector3 ToLocal(Vector3 world) => transform.InverseTransformPoint(world);

    Vector3 PointAt(float t)
    {
        t = Mathf.Clamp01(t);
        float scaled = t * (PointCount - 1);
        int index = Mathf.Min(Mathf.FloorToInt(scaled), PointCount - 2);
        return Vector3.Lerp(points[index], points[index + 1], scaled - index);
    }

    Vector3 TangentAt(float t)
    {
        float step = 1f / (PointCount - 1);
        Vector3 a = PointAt(Mathf.Max(0f, t - step));
        Vector3 b = PointAt(Mathf.Min(1f, t + step));
        Vector3 tangent = b - a;
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.down;
    }

    static Vector3 ViewForward()
    {
        Camera camera = Camera.main;
        return camera != null ? camera.transform.forward : Vector3.forward;
    }

    static Color Blackbody(float t)
    {
        if (t > 0.72f) return Color.Lerp(new Color(1f, 0.82f, 0.35f, 1f), new Color(1f, 0.97f, 0.9f, 1f), (t - 0.72f) / 0.28f);
        if (t > 0.38f) return Color.Lerp(new Color(1f, 0.32f, 0.04f, 1f), new Color(1f, 0.82f, 0.35f, 1f), (t - 0.38f) / 0.34f);
        Color cool = Color.Lerp(new Color(0.08f, 0.03f, 0.01f, 0.15f), new Color(1f, 0.32f, 0.04f, 0.85f), t / 0.38f);
        return cool;
    }

    static Material MakeMaterial(string name, Color color, float emissive)
    {
        var shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", name.Contains("Copper") || name.Contains("Clamp") ? 0.72f : 0.08f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", name.Contains("Copper") ? 0.45f : 0.22f);
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", Color.black * emissive);
        return material;
    }

    static Texture2D CreateFilamentTexture()
    {
        var texture = new Texture2D(64, 16, TextureFormat.RGBA32, false)
        {
            name = "CableArcFilament",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 64; x++)
        {
            float u = (x + 0.5f) / 64f;
            float v = (y + 0.5f) / 16f;
            float line = Mathf.Exp(-Mathf.Pow((u - 0.5f) * 12f, 2f));
            float head = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.35f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, line * head));
        }
        texture.Apply(false, true);
        return texture;
    }

    static Material MakeSparkMaterial(Texture2D filamentMap)
    {
        var shader = Shader.Find("Sentinel/ElectricArc");
        if (shader == null) shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = "MAT_Runtime_CableArc", hideFlags = HideFlags.HideAndDontSave };
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", filamentMap);
        if (material.HasProperty("_UnlitColorMap")) material.SetTexture("_UnlitColorMap", filamentMap);
        if (material.HasProperty("_Intensity")) material.SetFloat("_Intensity", 8f);
        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", new Color(4f, 5.5f, 8f));
        if (material.HasProperty("_EmissiveExposureWeight")) material.SetFloat("_EmissiveExposureWeight", 0f);
        if (material.HasProperty("_DoubleSidedEnable")) material.SetFloat("_DoubleSidedEnable", 1f);
        if (material.HasProperty("_SurfaceType")) material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_BlendMode")) material.SetFloat("_BlendMode", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 1f);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_DOUBLESIDED_ON");
        material.renderQueue = 3100;
        return material;
    }

    void OnDestroy()
    {
        if (sparkMesh != null) Destroy(sparkMesh);
        if (filament != null) Destroy(filament);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        if (anchor != null) Gizmos.DrawWireSphere(anchor.position, 0.04f);
    }
}
