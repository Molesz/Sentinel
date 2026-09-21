using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

// Explicit, isolated batch entry point. Leaves transport and light scripts running.
[InitializeOnLoad]
public static class SentinelWaterVisualVerification
{
    const string Key = "Sentinel.WaterVisualVerification";
    static int stage;
    static double deadline;
    static readonly float[] Times = { 2, 15, 65 };

    static SentinelWaterVisualVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            stage = 0;
            deadline = EditorApplication.timeSinceStartup + 150;
            Time.timeScale = 3;
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        EditorSceneManager.OpenScene("Assets/_Sentinel/Scenes/Lighting/LightingNew.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Water visual capture timed out.");
            if (Time.timeSinceLevelLoad < Times[stage]) return;
            Capture($"Logs/water-{Times[stage]:00}");
            stage++;
            if (stage == Times.Length) Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }

    static void Capture(string path)
    {
        var water = Object.FindAnyObjectByType<SentinelContainerWater>();
        var camera = Object.FindAnyObjectByType<SentinelFollowCamera>().GetComponent<Camera>();
        var planar = water.GetComponentInChildren<PlanarReflectionProbe>();
        var hd = camera.GetComponent<HDAdditionalCameraData>();
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        camera.aspect = 1280f / 720;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        target.Create();
        try
        {
            for (int i = 0; i < 4; i++) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            Save(target, path + ".png");
            if (planar.realtimeTexture != null) Save(planar.realtimeTexture, path + "-reflection.png");
            Debug.Log($"WATER_VISUAL time={Time.timeSinceLevelLoad} camera={water.transform.InverseTransformPoint(camera.transform.position):F4} rootScale={water.transform.lossyScale} surfaces={water.SurfaceCount} streams={water.WallStreamCount}");
            Debug.Log($"WATER_PROBE capture={water.transform.InverseTransformPoint(planar.realtimeRenderData.capturePosition):F4} mirror={planar.settingsRaw.proxySettings.mirrorPositionProxySpace} rotation={planar.settingsRaw.proxySettings.mirrorRotationProxySpace.eulerAngles}");
            foreach (var renderer in water.GetComponentsInChildren<MeshRenderer>())
                Debug.Log($"WATER_RENDERER {renderer.name} bounds={renderer.bounds} enabled={renderer.enabled}");
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                Vector3 p = water.transform.InverseTransformPoint(light.transform.position);
                Vector3 c = water.transform.InverseTransformPoint(camera.transform.position);
                float floor = -1.16f;
                Vector3 mirror = new Vector3(p.x, 2 * floor - p.y, p.z);
                float t = (floor - c.y) / (mirror.y - c.y);
                Debug.Log($"WATER_LIGHT {light.name} local={p:F3} intensity={light.intensity} enabled={light.enabled} reflectionOnFloor={Vector3.LerpUnclamped(c, mirror, t):F3}");
            }
        }
        finally { target.Release(); Object.DestroyImmediate(target); }
    }

    static void Save(RenderTexture source, string path)
    {
        var previous = RenderTexture.active;
        var pixels = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = source;
            pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }
}
