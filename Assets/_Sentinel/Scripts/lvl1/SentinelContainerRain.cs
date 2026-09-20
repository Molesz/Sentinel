using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SentinelContainerRain : MonoBehaviour
{
    [Tooltip("Local-space roof opening. Rays reject any covered part of this rectangle.")]
    public Vector3 openingCenter = new Vector3(-6.25f, 1.45f, -0.1f);
    public Vector2 openingSize = new Vector2(0.9f, 0.6f);
    [Range(1, 250)] public float dropsPerSecond = 110;
    public float fallSpeed = 6f;
    public float streakLength = 0.14f;
    public float streakWidth = 0.004f;
    public Material rainMaterial;
    public int ImpactCount { get; private set; }

    struct Drop { public Vector3 position, impact, normal; public float speed; public bool active; }
    struct Splash { public Vector3 position, normal; public float age; public bool active; }
    readonly Drop[] drops = new Drop[160];
    readonly Splash[] splashes = new Splash[100];
    readonly RaycastHit[] surfaceHits = new RaycastHit[24];
    readonly List<Vector3> vertices = new List<Vector3>(12000);
    readonly List<Vector2> uv = new List<Vector2>(12000);
    readonly List<int> indices = new List<int>(18000);
    Mesh mesh;
    float emission;
    int nextSplash;

    void Awake()
    {
        mesh = new Mesh { name = "Container rain and impact rings" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = rainMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        emission = Mathf.Min(emission + dropsPerSecond * dt, 20);
        for (int i = 0; i < drops.Length && emission >= 1; i++)
        {
            if (drops[i].active) continue;
            emission--;
            Vector3 start = openingCenter + new Vector3(Random.Range(-0.5f, 0.5f) * openingSize.x, 0,
                Random.Range(-0.5f, 0.5f) * openingSize.y);
            if (!FindEnvironmentSurface(start, out var hit)) continue;
            Vector3 point = transform.InverseTransformPoint(hit.point);
            // Covered roof samples must never emit through metal.
            if (point.y > openingCenter.y - 0.6f) continue;
            drops[i] = new Drop { active = true, position = start, impact = point,
                normal = transform.InverseTransformDirection(hit.normal), speed = fallSpeed * Random.Range(0.85f, 1.15f) };
        }
        vertices.Clear();
        uv.Clear();
        indices.Clear();
        for (int i = 0; i < drops.Length; i++)
        {
            if (!drops[i].active) continue;
            var d = drops[i];
            Vector3 previous = d.position;
            d.position.y -= d.speed * dt;
            // Re-test the travelled segment so a moving character can catch a drop.
            Vector3 a = transform.TransformPoint(previous);
            Vector3 b = transform.TransformPoint(d.position);
            if (Physics.Linecast(a, b, out var hit, ~0, QueryTriggerInteraction.Ignore))
            {
                d.impact = transform.InverseTransformPoint(hit.point);
                d.normal = transform.InverseTransformDirection(hit.normal);
                d.position = d.impact;
            }
            if (d.position.y <= d.impact.y)
            {
                d.active = false;
                splashes[nextSplash] = new Splash { active = true, position = d.impact + d.normal * 0.009f, normal = d.normal };
                nextSplash = (nextSplash + 1) % splashes.Length;
                ImpactCount++;
            }
            else Quad(d.position, d.position + Vector3.up * Mathf.Min(streakLength, openingCenter.y - d.position.y), Vector3.right * streakWidth * 0.5f);
            drops[i] = d;
        }
        for (int i = 0; i < splashes.Length; i++)
        {
            var splash = splashes[i];
            if (!splash.active) continue;
            splash.age += dt;
            if (splash.age > 0.42f) { splash.active = false; splashes[i] = splash; continue; }
            float progress = splash.age / 0.42f;
            float radius = Mathf.Lerp(0.008f, 0.10f, progress);
            float thickness = 0.005f * (1 - progress);
            Vector3 tangent = Vector3.Cross(splash.normal, Vector3.forward).normalized;
            if (tangent.sqrMagnitude < 0.1f) tangent = Vector3.right;
            Vector3 bitangent = Vector3.Cross(splash.normal, tangent);
            for (int segment = 0; segment < 12; segment++)
            {
                float angle = segment * Mathf.PI / 6;
                float next = (segment + 1) * Mathf.PI / 6;
                Vector3 u = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                Vector3 v = tangent * Mathf.Cos(next) + bitangent * Mathf.Sin(next);
                Face(splash.position + u * radius, splash.position + v * radius,
                    splash.position + v * (radius + thickness), splash.position + u * (radius + thickness));
            }
            if (splash.age < 0.2f)
                for (int spray = 0; spray < 3; spray++)
                {
                    float angle = spray * 2.0944f;
                    Vector3 p = splash.position + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * splash.age * 0.3f
                        + splash.normal * Mathf.Max(0, 0.9f * splash.age - 4.5f * splash.age * splash.age);
                    Quad(p, p + splash.normal * 0.015f, tangent * 0.003f * (1 - progress));
                }
            splashes[i] = splash;
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
    }

    bool FindEnvironmentSurface(Vector3 start, out RaycastHit closest)
    {
        closest = default;
        float distance = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(transform.TransformPoint(start), -transform.up, surfaceHits, 5,
            ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = surfaceHits[i];
            // A moving character is tested during flight, never cached as a
            // future impact point: otherwise walking away leaves air splashes.
            if (hit.collider.GetComponentInParent<SentinelContainerPlayer>() != null || hit.distance >= distance) continue;
            closest = hit;
            distance = hit.distance;
        }
        return !float.IsPositiveInfinity(distance);
    }

    void Quad(Vector3 bottom, Vector3 top, Vector3 halfWidth) => Face(bottom - halfWidth, top - halfWidth, top + halfWidth, bottom + halfWidth);
    void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1));
        uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
        indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
    }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
    void OnDisable() { if (mesh != null) mesh.Clear(); }
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(openingCenter, new Vector3(openingSize.x, 0.04f, openingSize.y));
    }
}
