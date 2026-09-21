using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Bounded, allocation-free wave updates. HDRP Lit shades the changing normals,
// so the reflected scene is distorted by actual impacts, not painted rings.
public sealed class SentinelContainerWater : MonoBehaviour
{
    [Min(1)] public float fillSeconds = 100;
    [Tooltip("Blend local room captures with planar reflections for the shallow 2.5D camera angle.")]
    public bool sideViewReflections = true;
    public int WaterImpactCount { get; private set; }
    public float Fill { get; private set; }
    public int SurfaceCount => surfaces.Count;
    public int WallStreamCount => streams.Count;
    struct Ripple { public Vector3 point; public float born; public bool active; }
    sealed class Surface
    {
        public Mesh mesh;
        public Vector3[] rest, points, normals;
        public Transform transform;
        public Vector3 center;
        public bool growing;
    }
    readonly List<Surface> surfaces = new List<Surface>();
    readonly List<GameObject> generated = new List<GameObject>();
    readonly Ripple[] ripples = new Ripple[48];
    readonly List<Transform> streams = new List<Transform>();
    readonly List<Vector3> streamScales = new List<Vector3>();
    readonly List<Mesh> streamMeshes = new List<Mesh>();
    readonly List<Vector3[]> streamNormals = new List<Vector3[]>();
    readonly List<HDAdditionalReflectionData> roomProbes = new List<HDAdditionalReflectionData>();
    float nextReflectionUpdate;
    int nextRipple;
    Material material;
    Material wallMaterial;
    Renderer original;
    bool originalEnabled;
    Vector2 flow, velocity;
    float collected;

    void Start()
    {
        var existing = transform.parent.Find("Wet Floor Under Roof Opening");
        if (existing == null) { enabled = false; return; }
        original = existing.GetComponent<Renderer>();
        originalEnabled = original.enabled;
        material = new Material(original.sharedMaterial) { name = "Container water (runtime)" };
        // Keep a dielectric response and preserve specular lighting through alpha.
        material.SetFloat("_MaterialID", 1);
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", 0.94f);
        material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
        material.SetFloat("_ReceivesSSRTransparent", 1);
        material.SetFloat("_TransparentDepthPrepassEnable", 1);
        material.SetFloat("_AlphaCutoffPrepass", 0.1f);
        material.SetColor("_BaseColor", new Color(0.10f, 0.10f, 0.095f, 0.72f));
        HDMaterial.ValidateMaterial(material);
        wallMaterial = new Material(material) { name = "Container wall water film (runtime)" };
        wallMaterial.SetFloat("_Smoothness", 0.82f);
        wallMaterial.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f, 0.28f));
        HDMaterial.ValidateMaterial(wallMaterial);
        original.enabled = false;
        Physics.SyncTransforms();
        bool backfaces = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            CreatePatch(new Vector3(-6.23f, -1.16f, -0.05f), new Vector2(0.68f, 0.48f), true);
            CreatePatch(new Vector3(-4.6f, -1.16f, 0.3f), new Vector2(0.30f, 0.20f), false);
            CreatePatch(new Vector3(-2.7f, -1.16f, -0.35f), new Vector2(0.38f, 0.17f), false);
            CreatePatch(new Vector3(0.4f, -1.16f, 0.15f), new Vector2(0.25f, 0.23f), false);
            CreateWallStreams();
        }
        finally { Physics.queriesHitBackfaces = backfaces; }
        if (surfaces.Count > 0)
        {
            var go = MakeObject("Water planar reflection");
            go.transform.localPosition = new Vector3(-2.5f, surfaces[0].center.y, 0);
            var probe = go.AddComponent<PlanarReflectionProbe>();
            probe.mode = ProbeSettings.Mode.Realtime;
            probe.realtimeMode = ProbeSettings.RealtimeMode.EveryFrame;
            probe.weight = sideViewReflections ? 0.25f : 1;
            probe.settingsRaw.proxySettings.mirrorRotationProxySpace = Quaternion.Euler(-90, 0, 0);
            probe.influenceVolume.boxSize = new Vector3(11, 2, 3);
            // The projection proxy describes the room, not a box straddling the floor.
            var proxyObject = MakeObject("Container reflection room proxy");
            proxyObject.transform.localPosition = new Vector3(-2.45f, 0, 0);
            var proxy = proxyObject.AddComponent<ReflectionProxyVolumeComponent>();
            proxy.proxyVolume.boxSize = new Vector3(10, 2.4f, 2.1f);
            probe.proxyVolume = proxy;
            probe.settingsRaw.proxySettings.mirrorPositionProxySpace = proxyObject.transform.InverseTransformPoint(go.transform.position);
        }
        if (sideViewReflections) CreateRoomReflections();
    }

    void CreateRoomReflections()
    {
        // Local cubemaps are a deliberate 2.5D approximation: exact planar lamp
        // images lie in front of the cutaway floor at this camera's grazing angle.
        // Capture real fixture geometry/light, retaining changes when power is cut.
        foreach (var light in FindObjectsByType<Light>())
        {
            Vector3 p = transform.InverseTransformPoint(light.transform.position);
            if (light.name != "Point Light" || p.y < 0.5f || p.y > 1.3f) continue;
            var go = MakeObject("Wet surface room reflection");
            go.transform.localPosition = p + new Vector3(0.25f, -0.32f, -0.85f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.resolution = 256;
            probe.nearClipPlane = 0.03f;
            var hd = go.GetComponent<HDAdditionalReflectionData>();
            if (hd == null) hd = go.AddComponent<HDAdditionalReflectionData>();
            hd.mode = ProbeSettings.Mode.Realtime;
            hd.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
            hd.settingsRaw.proxySettings.useInfluenceVolumeAsProxyVolume = false;
            hd.influenceVolume.boxSize = new Vector3(4.5f, 4.5f, 4);
            hd.influenceVolume.boxBlendDistancePositive = Vector3.one * 0.3f;
            hd.influenceVolume.boxBlendDistanceNegative = Vector3.one * 0.3f;
            hd.importance = 64;
            roomProbes.Add(hd);
            hd.RequestRenderNextUpdate();
        }
    }

    GameObject MakeObject(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        generated.Add(go);
        return go;
    }

    void CreatePatch(Vector3 center, Vector2 radius, bool growing)
    {
        var points = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        const float step = 0.025f;
        // Validate all corners against actual floor geometry, excluding cargo tops.
        for (float x = -radius.x; x < radius.x - step; x += step)
        for (float z = -radius.y; z < radius.y - step; z += step)
        {
            var corners = new Vector3[4];
            bool valid = true;
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = center + new Vector3(x + (k >= 2 ? step : 0), 0, z + (k == 1 || k == 2 ? step : 0));
                Vector2 q = new Vector2((p.x - center.x) / radius.x, (p.z - center.z) / radius.y);
                if (q.sqrMagnitude > 0.95f + 0.035f * Mathf.Sin(q.x * 21 + q.y * 17)) { valid = false; break; }
                Vector3 start = new Vector3(p.x, 0.85f, p.z);
                if (!Physics.Raycast(transform.TransformPoint(start), -transform.up, out var hit, 3, ~0, QueryTriggerInteraction.Ignore)) { valid = false; break; }
                p = transform.InverseTransformPoint(hit.point);
                if (p.y > -1 || Mathf.Abs(Vector3.Dot(hit.normal, transform.up)) < 0.9f) { valid = false; break; }
                corners[k] = p + Vector3.up * 0.008f;
            }
            if (!valid) continue;
            int index = points.Count;
            foreach (var p in corners)
            {
                points.Add(p);
                uv.Add(new Vector2((p.x - center.x) / (radius.x * 2) + 0.5f, (p.z - center.z) / (radius.y * 2) + 0.5f));
            }
            triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
        }
        if (points.Count == 0) return;
        center.y = points[0].y;
        var go = MakeObject(growing ? "Growing rain puddle" : "Reflective damp floor");
        var mesh = new Mesh { name = go.name };
        mesh.MarkDynamic();
        mesh.SetVertices(points); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        surfaces.Add(new Surface { mesh = mesh, rest = points.ToArray(), points = points.ToArray(),
            normals = mesh.normals, transform = go.transform, center = center, growing = growing });
    }

    public void Impact(Vector3 point, Vector3 normal)
    {
        if (normal.y < 0.8f) return;
        foreach (var surface in surfaces)
        {
            Vector3 local = surface.transform.InverseTransformPoint(transform.TransformPoint(point));
            var bounds = surface.mesh.bounds;
            if (Mathf.Abs(local.y - surface.center.y) > 0.04f || local.x < bounds.min.x || local.x > bounds.max.x || local.z < bounds.min.z || local.z > bounds.max.z) continue;
            ripples[nextRipple] = new Ripple { point = point, born = Time.time, active = true };
            nextRipple = (nextRipple + 1) % ripples.Length;
            WaterImpactCount++;
            if (surface.growing) collected += 1f / (55f * Mathf.Max(1, fillSeconds));
            break;
        }
    }

    void Update()
    {
        if (Time.unscaledTime >= nextReflectionUpdate && roomProbes.Count > 0)
        {
            roomProbes[Mathf.FloorToInt(Time.unscaledTime * 6) % roomProbes.Count].RequestRenderNextUpdate();
            nextReflectionUpdate = Time.unscaledTime + 1f / 6;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        Vector3 gravity = transform.InverseTransformDirection(Physics.gravity);
        Vector2 target = Vector2.ClampMagnitude(new Vector2(gravity.x, gravity.z) * 0.025f, 0.055f);
        velocity += (target - flow) * (18 * dt);
        velocity *= Mathf.Exp(-4 * dt);
        flow += velocity * dt;
        Fill = Mathf.Clamp01(collected);
        for (int i = 0; i < ripples.Length; i++)
            if (ripples[i].active && Time.time - ripples[i].born > 1.3f) ripples[i].active = false;
        foreach (var s in surfaces)
        {
            float scale = s.growing ? Mathf.Lerp(0.6f, 1, Fill) : 1;
            // Keep the validated footprint fixed; grow through a centered scale.
            s.transform.localScale = new Vector3(scale, 1, scale);
            s.transform.localPosition = new Vector3(s.center.x * (1 - scale), 0, s.center.z * (1 - scale));
            for (int i = 0; i < s.rest.Length; i++)
            {
                Vector3 p = s.rest[i];
                Vector3 actual = s.transform.localPosition + Vector3.Scale(p, s.transform.localScale);
                Vector2 slope = flow * (0.3f * Mathf.Sin(Time.time * 3.2f + p.x * 3 + p.z * 2));
                float height = 0;
                foreach (var r in ripples)
                {
                    if (!r.active) continue;
                    float age = Time.time - r.born;
                    Vector2 delta = new Vector2(actual.x - r.point.x, actual.z - r.point.z);
                    float distance = delta.magnitude;
                    float front = distance - age * 0.48f;
                    if (Mathf.Abs(front) > 0.13f) continue;
                    float envelope = Mathf.Exp(-front * front * 450) * Mathf.Exp(-age * 3) * Mathf.Min(age * 15, 1);
                    float phase = front * 90;
                    height += 0.0007f * envelope * Mathf.Sin(phase);
                    float derivative = 0.0007f * envelope * (90 * Mathf.Cos(phase) - 900 * front * Mathf.Sin(phase));
                    slope += delta / Mathf.Max(distance, 0.001f) * derivative;
                }
                s.points[i] = p + Vector3.up * height;
                s.normals[i] = new Vector3(-slope.x * scale, 1, -slope.y * scale).normalized;
            }
            s.mesh.vertices = s.points;
            s.mesh.normals = s.normals;
            s.mesh.RecalculateBounds();
        }
        for (int i = 0; i < streams.Count; i++)
        {
            Vector3 size = streamScales[i];
            size.y *= 0.94f + 0.06f * Mathf.Sin(Time.time * 0.7f + i * 2.3f);
            streams[i].localScale = size;
            var normals = streamNormals[i];
            for (int j = 0; j < normals.Length; j++)
            {
                float down = (j / 3) / 32f;
                // Travelling changes in film thickness carry highlights down the wall.
                float slope = 0.07f * Mathf.Sin(down * 37 - Time.time * (3 + i * 0.13f));
                // Compensate the narrow ribbon's nonuniform scale before Unity's
                // inverse-transpose normal transform, otherwise edge normals lie flat.
                normals[j] = new Vector3((j % 3 - 1) * 0.2f * size.x, slope * size.y, -1).normalized;
            }
            streamMeshes[i].normals = normals;
        }
    }

    void CreateWallStreams()
    {
        for (int i = 0; i < 12; i++)
        {
            Vector3 origin = new Vector3(-6.6f + i * 0.72f, 0.72f, 0);
            RaycastHit hit = default;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in Physics.RaycastAll(transform.TransformPoint(origin), transform.forward, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (candidate.collider.name.Contains("movement boundary") || candidate.distance >= nearest) continue;
                hit = candidate;
                nearest = candidate.distance;
            }
            if (float.IsPositiveInfinity(nearest) || Mathf.Abs(Vector3.Dot(hit.normal, transform.up)) > 0.2f) continue;
            if (Vector3.Dot(hit.normal, transform.forward) > 0) hit.normal = -hit.normal;
            var go = MakeObject("Thin wall runoff");
            go.transform.position = hit.point + hit.normal * 0.003f;
            go.transform.rotation = Quaternion.LookRotation(-hit.normal, transform.up);
            // Top-anchored strip, facing into the container; alpha texture softens edges.
            var mesh = new Mesh { name = "Wall water film" };
            var points = new Vector3[33 * 3];
            var uv = new Vector2[points.Length];
            var triangles = new List<int>();
            for (int row = 0; row <= 32; row++)
            for (int column = 0; column < 3; column++)
            {
                int index = row * 3 + column;
                float down = row / 32f;
                points[index] = new Vector3((column - 1) * 0.5f + 0.08f * Mathf.Sin(down * 19 + i), -down, 0);
                uv[index] = new Vector2(column * 0.5f, 1 - down);
                if (row < 32 && column < 2)
                    triangles.AddRange(new[] { index, index + 1, index + 3, index + 1, index + 4, index + 3 });
            }
            mesh.vertices = points;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = wallMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.localScale = new Vector3(0.009f + i % 3 * 0.004f, 0.9f + i % 4 * 0.18f, 1);
            streams.Add(go.transform); streamScales.Add(go.transform.localScale);
            streamMeshes.Add(mesh); streamNormals.Add(mesh.normals);
        }
    }

    void OnDestroy()
    {
        if (original != null) original.enabled = originalEnabled;
        foreach (var go in generated)
        {
            if (go == null) continue;
            var filter = go.GetComponent<MeshFilter>();
            if (filter != null) Destroy(filter.sharedMesh);
            Destroy(go);
        }
        if (material != null) Destroy(material);
        if (wallMaterial != null) Destroy(wallMaterial);
    }
}
