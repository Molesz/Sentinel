using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

public static class SentinelContainerSetup
{
    const string ScenePath = "Assets/_Sentinel/Scenes/Lighting/LightingNew.unity";
    const string ArtPath = "Assets/_Sentinel/Art/Materials/";

    // Explicit editor command: no automatic scene mutation on import or Play.
    [MenuItem("Sentinel/Set up container gameplay")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (Object.FindAnyObjectByType<SentinelContainerPlayer>() != null)
            throw new InvalidOperationException("Container gameplay is already configured.");
        var container = Object.FindAnyObjectByType<KontenerMozgas>();
        if (container == null) throw new InvalidOperationException("Missing container.");
        foreach (var filter in container.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
        var frame = new GameObject("Container Gameplay Frame").transform;
        frame.SetParent(container.transform, false);
        frame.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // The cutaway side is visually open, but the player must stay inside.
        Boundary(frame, "Front movement boundary", new Vector3(-2.45f, 0, -1.04f), new Vector3(10, 2.5f, 0.12f));
        Boundary(frame, "Back movement boundary", new Vector3(-2.45f, 0, 1.04f), new Vector3(10, 2.5f, 0.12f));
        Boundary(frame, "Left movement boundary", new Vector3(-7.39f, 0, 0), new Vector3(0.12f, 2.5f, 2));
        Boundary(frame, "Right movement boundary", new Vector3(2.44f, 0, 0), new Vector3(0.12f, 2.5f, 2));

        var visual = GameObject.Find("Capsule");
        if (visual == null) throw new InvalidOperationException("Missing placeholder character.");
        foreach (var collider in visual.GetComponents<Collider>()) Object.DestroyImmediate(collider);
        var root = new GameObject("Player");
        root.tag = "Player";
        root.transform.SetParent(frame, false);
        root.transform.localPosition = new Vector3(-5.9f, -1.17f, 0);
        visual.name = "Player Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0, 0.475f, 0);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.28f, 0.475f, 0.28f);
        var controller = root.AddComponent<CharacterController>();
        controller.height = 0.95f;
        controller.radius = 0.14f;
        controller.center = new Vector3(0, 0.475f, 0);
        controller.skinWidth = 0.015f;
        controller.stepOffset = 0.12f;
        controller.minMoveDistance = 0;
        var player = root.AddComponent<SentinelContainerPlayer>();
        player.movementReference = frame;
        player.visual = visual.transform;
        ConfigureCamera(root.transform, frame);

        var rainMaterial = new Material(Shader.Find("HDRP/Unlit")) { name = "MAT_Container_Rain" };
        rainMaterial.SetColor("_UnlitColor", new Color(0.32f, 0.44f, 0.55f, 1));
        rainMaterial.SetFloat("_DoubleSidedEnable", 1);
        HDMaterial.ValidateMaterial(rainMaterial);
        AssetDatabase.CreateAsset(rainMaterial, ArtPath + "MAT_Container_Rain.mat");
        var rainObject = new GameObject("Roof Rain and Splashes");
        rainObject.transform.SetParent(frame, false);
        var rain = rainObject.AddComponent<SentinelContainerRain>();
        rain.rainMaterial = rainMaterial;

        var wetMaterial = new Material(Shader.Find("HDRP/Lit")) { name = "MAT_Container_WetFloor" };
        wetMaterial.SetColor("_BaseColor", new Color(0.055f, 0.07f, 0.08f, 1));
        wetMaterial.SetFloat("_Smoothness", 0.94f);
        wetMaterial.SetFloat("_Metallic", 0.12f);
        HDMaterial.ValidateMaterial(wetMaterial);
        AssetDatabase.CreateAsset(wetMaterial, ArtPath + "MAT_Container_WetFloor.mat");
        controller.enabled = false;
        Physics.SyncTransforms();
        CreateWetFloor(frame, wetMaterial);
        controller.enabled = true;

        var probeObject = new GameObject("Wet Floor Reflection Probe");
        probeObject.transform.SetParent(frame, false);
        probeObject.transform.localPosition = new Vector3(-6.1f, -0.4f, 0);
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(10, 3, 3);
        probe.resolution = 128;
        probe.nearClipPlane = 0.05f;
        var hdProbe = probeObject.GetComponent<HDAdditionalReflectionData>();
        if (hdProbe == null) hdProbe = probeObject.AddComponent<HDAdditionalReflectionData>();
        hdProbe.mode = ProbeSettings.Mode.Realtime;
        hdProbe.realtimeMode = ProbeSettings.RealtimeMode.OnEnable;
        hdProbe.influenceVolume.boxSize = new Vector3(20, 4, 4);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var ssr = profile.Add<ScreenSpaceReflection>(true);
        ssr.enabled.Override(true);
        AssetDatabase.CreateAsset(profile, ArtPath + "ContainerRainReflections.asset");
        AssetDatabase.AddObjectToAsset(ssr, profile);
        var volume = new GameObject("Wet Floor Reflections").AddComponent<Volume>();
        volume.transform.SetParent(frame, false);
        volume.isGlobal = true;
        volume.priority = 5;
        volume.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CONTAINER_SETUP_OK");
        PolishVisuals();
        ValidateCamera();
    }

    public static void RefreshCameraBindings()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var player = Object.FindAnyObjectByType<SentinelContainerPlayer>();
        ConfigureCamera(player.transform, player.movementReference);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Reload proves the prefab overrides actually survived serialization.
        EditorSceneManager.OpenScene(ScenePath);
        ValidateCamera();
    }

    static void ConfigureCamera(Transform player, Transform frame)
    {
        var camera = Object.FindAnyObjectByType<SentinelFollowCamera>();
        camera.karakter = player;
        camera.mozgasiReferencia = frame;
        camera.hatarReferencia = frame;
        camera.hatarokHasznalata = camera.teljesKepHatarolasa = true;
        camera.vizszintesHatar = new Vector2(-7.3f, 2.34f);
        camera.fuggolegesHatar = new Vector2(-1.20f, 1.1f);
        camera.melysegiHatar = new Vector2(-0.98f, 0.98f);
        camera.kameraSzoge = Vector3.zero;
        camera.alap.tavolsag = 4f;
        camera.alap.latoszog = 35;
        camera.alap.celpontEltolas = new Vector3(0, 0.55f, 0);
        camera.alap.eloretekintes = 0.25f;
        camera.alap.vizszintesHoltzona = 0.08f;
        camera.GetComponent<Camera>().nearClipPlane = 0.03f;
        camera.transform.position = player.position + new Vector3(0, 0.55f, -4f);
        camera.KontenerKepHatarolasa();
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.GetComponent<Camera>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
    }

    public static void PolishVisuals()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var player = Object.FindAnyObjectByType<SentinelContainerPlayer>();
        ConfigureCamera(player.transform, player.movementReference);
        var rain = Object.FindAnyObjectByType<SentinelContainerRain>();
        rain.streakWidth = 0.004f;
        var rainMaterial = rain.rainMaterial;
        rainMaterial.SetFloat("_SurfaceType", 1);
        rainMaterial.SetFloat("_BlendMode", 0);
        rainMaterial.SetColor("_UnlitColor", new Color(0.12f, 0.18f, 0.24f, 0.55f));
        // Keep rain readable across the scene's strongly changing exposure.
        rainMaterial.SetColor("_EmissiveColor", new Color(0.24f, 0.36f, 0.48f));
        rainMaterial.SetFloat("_EmissiveExposureWeight", 0);
        rainMaterial.SetTexture("_UnlitColorMap", CreateSoftTexture("ContainerRainSoft", false));
        HDMaterial.ValidateMaterial(rainMaterial);
        EditorUtility.SetDirty(rainMaterial);

        var wetMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "MAT_Container_WetFloor.mat");
        wetMaterial.SetFloat("_SurfaceType", 1);
        wetMaterial.SetFloat("_BlendMode", 0);
        wetMaterial.SetColor("_BaseColor", new Color(0.055f, 0.07f, 0.08f, 0.65f));
        wetMaterial.SetTexture("_BaseColorMap", CreateSoftTexture("ContainerPuddleSoft", true));
        HDMaterial.ValidateMaterial(wetMaterial);
        EditorUtility.SetDirty(wetMaterial);
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        Physics.SyncTransforms();
        CreateWetFloor(player.movementReference, wetMaterial);
        controller.enabled = true;
        var footprint = AssetDatabase.LoadAssetAtPath<Mesh>(ArtPath + "ContainerWetFootprint.asset");
        var positions = footprint.vertices;
        var uv = new Vector2[positions.Length];
        for (int i = 0; i < uv.Length; i++)
            uv[i] = new Vector2(Mathf.InverseLerp(-6.8f, -5.65f, positions[i].x), Mathf.InverseLerp(-0.5f, 0.4f, positions[i].z));
        footprint.uv = uv;
        EditorUtility.SetDirty(footprint);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CONTAINER_VISUALS_OK");
    }

    static Texture2D CreateSoftTexture(string name, bool puddle)
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f;
            float alpha;
            if (puddle)
            {
                float radius = new Vector2((u - 0.5f) * 2, (v - 0.5f) * 2).magnitude;
                float edge = 0.82f + 0.13f * Mathf.PerlinNoise(u * 9, v * 9);
                alpha = Mathf.SmoothStep(0, 1, Mathf.Clamp01((edge - radius) / 0.2f));
            }
            else alpha = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f) * Mathf.Sin(v * Mathf.PI);
            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        texture.Apply();
        string path = ArtPath + name + ".png";
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Boundary(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void CreateWetFloor(Transform frame, Material material)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const float step = 0.06f;
        for (float x = -6.8f; x < -5.65f; x += step)
        for (float z = -0.5f; z < 0.4f; z += step)
        {
            Vector3[] corners = { new Vector3(x, 1.5f, z), new Vector3(x, 1.5f, z + step),
                new Vector3(x + step, 1.5f, z + step), new Vector3(x + step, 1.5f, z) };
            bool valid = true;
            for (int i = 0; i < 4; i++)
            {
                if (!Physics.Raycast(frame.TransformPoint(corners[i]), -frame.up, out var hit, 4,
                        ~0, QueryTriggerInteraction.Ignore)) { valid = false; break; }
                corners[i] = frame.InverseTransformPoint(hit.point) + Vector3.up * 0.006f;
                if (corners[i].y > -1f) { valid = false; break; }
            }
            if (!valid) continue;
            int start = vertices.Count;
            vertices.AddRange(corners);
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        if (vertices.Count == 0) throw new InvalidOperationException("Roof opening did not reach floor.");
        var mesh = new Mesh { name = "Roof opening wet footprint" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ArtPath + "ContainerWetFootprint.asset");
        if (existingMesh == null) AssetDatabase.CreateAsset(mesh, ArtPath + "ContainerWetFootprint.asset");
        else
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
            Object.DestroyImmediate(mesh);
            mesh = existingMesh;
            EditorUtility.SetDirty(mesh);
        }
        var existingObject = frame.Find("Wet Floor Under Roof Opening");
        var go = existingObject != null ? existingObject.gameObject
            : new GameObject("Wet Floor Under Roof Opening", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(frame, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    [MenuItem("Sentinel/Validate container camera")]
    public static void ValidateCamera()
    {
        var follow = Object.FindAnyObjectByType<SentinelFollowCamera>();
        var camera = follow.GetComponent<Camera>();
        var reference = follow.hatarReferencia;
        var savedPosition = camera.transform.position;
        var savedRotation = camera.transform.rotation;
        var savedReferenceRotation = reference.rotation;
        float savedAspect = camera.aspect, savedFov = camera.fieldOfView;
        int cases = 0;
        try
        {
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9, 32f / 9, 9f / 16 })
            foreach (float x in new[] { -12f, -6f, 0f, 8f })
            foreach (float y in new[] { -3f, 0f, 4f })
            foreach (float z in new[] { -12f, -2f, 1f })
            foreach (float tilt in new[] { 0f, 4f })
            {
                reference.rotation = Quaternion.Euler(tilt, 0, tilt);
                camera.aspect = aspect;
                camera.fieldOfView = 35;
                camera.transform.position = reference.TransformPoint(new Vector3(x, y, z));
                follow.KontenerKepHatarolasa();
                foreach (float depth in new[] { follow.melysegiHatar.x, follow.melysegiHatar.y })
                for (int corner = 0; corner < 4; corner++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0));
                    Vector3 origin = reference.InverseTransformPoint(ray.origin);
                    Vector3 direction = reference.InverseTransformDirection(ray.direction);
                    Vector3 hit = origin + direction * ((depth - origin.z) / direction.z);
                    bool outsideVerticalOpening = depth == follow.melysegiHatar.x &&
                        (hit.y < follow.fuggolegesHatar.x - 0.001f || hit.y > follow.fuggolegesHatar.y + 0.001f);
                    if (hit.x < follow.vizszintesHatar.x - 0.001f || hit.x > follow.vizszintesHatar.y + 0.001f || outsideVerticalOpening)
                        throw new InvalidOperationException($"Camera escaped at {aspect}, {x}, {y}, {z}: {hit}");
                }
                cases++;
            }
        }
        finally
        {
            reference.rotation = savedReferenceRotation;
            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            camera.aspect = savedAspect;
            camera.fieldOfView = savedFov;
        }
        Debug.Log($"CAMERA_VALIDATION_OK: {cases} cases, 8 corner intersections each");
    }
}
