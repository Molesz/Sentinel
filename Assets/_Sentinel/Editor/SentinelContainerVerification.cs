using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SentinelContainerVerification
{
    const string RunningKey = "Sentinel.ContainerVerification";
    static int frames;
    static double started;
    static bool captured;
    static SentinelContainerPlayer player;

    static SentinelContainerVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                frames = 0;
                captured = false;
                EditorApplication.update += Tick;
            }
        };
    }

    // Run with -batchmode -executeMethod, without -quit or -nographics.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/_Sentinel/Scenes/Lighting/LightingNew.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying) return;
            frames++;
            if (frames == 5)
            {
                player = Object.FindAnyObjectByType<SentinelContainerPlayer>();
                Object.FindAnyObjectByType<KontenerMozgas>().SzallitasLeallitasa();
                player.enabled = false;
                VerifyMovement(player);
                SentinelContainerSetup.ValidateCamera();
                // Frame the roof opening for the visual capture.
                Teleport(player, new Vector3(-5.65f, -1.16f, 0));
                Step(player, Vector2.zero, 60);
                Object.FindAnyObjectByType<SentinelFollowCamera>().AzonnaliIgazitas();
            }
            if (frames > 60 && EditorApplication.timeSinceStartup - started > 8 && !captured)
            {
                var rain = Object.FindAnyObjectByType<SentinelContainerRain>();
                Require(rain.ImpactCount > 20, "Rain never reached the floor.");
                Require(rain.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "Rain mesh is empty.");
                var renderer = rain.GetComponent<MeshRenderer>();
                Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported, "Rain shader unsupported.");
                CaptureCamera();
                Debug.Log($"RAIN_VALIDATION_OK: {rain.ImpactCount} impacts");
                captured = true;
                started = EditorApplication.timeSinceStartup;
            }
            if (captured && EditorApplication.timeSinceStartup - started > 3)
            {
                Require(File.Exists("Logs/container-gameplay.png"), "Screenshot was not saved.");
                Debug.Log("CONTAINER_PLAYMODE_VALIDATION_OK");
                Finish(0);
            }
            else if (!captured && EditorApplication.timeSinceStartup - started > 90)
                throw new Exception("Playmode verification timed out.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }

    static void CaptureCamera()
    {
        var camera = Object.FindAnyObjectByType<SentinelFollowCamera>().GetComponent<Camera>();
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        target.Create();
        float oldAspect = camera.aspect;
        var oldActive = RenderTexture.active;
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.aspect = 1280f / 720;
            camera.GetComponent<SentinelFollowCamera>().KontenerKepHatarolasa();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/container-gameplay.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.aspect = oldAspect;
            RenderTexture.active = oldActive;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(pixels);
        }
    }

    static void VerifyMovement(SentinelContainerPlayer motor)
    {
        Vector3 spawn = new Vector3(-4.5f, -1.16f, 0);
        Teleport(motor, spawn);
        Step(motor, Vector2.zero, 90);
        Require(motor.IsGrounded, "Character did not settle on the floor.");
        float groundY = motor.transform.localPosition.y;
        float walk = Travel(motor, spawn, false, false, false, Vector2.right);
        float run = Travel(motor, spawn, true, false, false, Vector2.right);
        float slow = Travel(motor, spawn, false, true, false, Vector2.right);
        float crouch = Travel(motor, spawn, false, false, true, Vector2.right);
        Require(run > walk * 1.5f && walk > slow * 1.5f && slow > crouch, "Movement speeds do not respect modes.");
        float diagonal = Travel(motor, spawn, false, false, false, new Vector2(1, 1), 15);
        float straight = Travel(motor, spawn, false, false, false, Vector2.right, 15);
        Require(Mathf.Abs(diagonal - straight) < 0.035f, "Diagonal movement is faster.");
        Teleport(motor, spawn);
        Step(motor, Vector2.up, 180);
        Require(motor.transform.localPosition.z > 0.3f && motor.transform.localPosition.z < 0.9f, "Back boundary failed.");
        Step(motor, Vector2.down, 240);
        Require(motor.transform.localPosition.z < -0.3f && motor.transform.localPosition.z > -0.9f, "Front boundary failed.");
        Teleport(motor, spawn);
        Step(motor, Vector2.left, 360, true);
        Require(motor.transform.localPosition.x > -7.3f && motor.transform.localPosition.x < -6.8f, "Left boundary failed.");
        // The existing crate at x=1 blocks the middle lane. Go behind it.
        Teleport(motor, new Vector3(-4.5f, -1.16f, 0.65f));
        Step(motor, Vector2.zero, 60);
        Step(motor, Vector2.right, 720, true);
        Require(motor.transform.localPosition.x < 2.35f && motor.transform.localPosition.x > 1.5f,
            $"Right boundary failed: {motor.transform.localPosition}");
        Teleport(motor, spawn);
        Step(motor, Vector2.zero, 60);
        float peak = motor.transform.localPosition.y;
        for (int i = 0; i < 120; i++)
        {
            motor.Simulate(Vector2.zero, false, false, false, i == 0, true, 1f / 60);
            peak = Mathf.Max(peak, motor.transform.localPosition.y);
        }
        Require(peak > groundY + 0.35f && motor.IsGrounded, "Jump or landing failed.");
        motor.SetCrouch(true);
        var blocker = new GameObject("Verification overhead obstruction");
        blocker.transform.position = motor.transform.position + Vector3.up * 0.8f;
        blocker.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.15f, 0.8f);
        Physics.SyncTransforms();
        motor.SetCrouch(false);
        Require(motor.IsCrouching, "Character stood through a ceiling.");
        blocker.SetActive(false);
        Object.Destroy(blocker);
        Physics.SyncTransforms();
        motor.SetCrouch(false);
        Require(!motor.IsCrouching, "Character cannot stand in clear space.");
        Debug.Log($"MOVEMENT_VALIDATION_OK walk={walk:F3} run={run:F3} slow={slow:F3} crouch={crouch:F3} jump={peak-groundY:F3}");
    }

    static float Travel(SentinelContainerPlayer motor, Vector3 spawn, bool run, bool slow, bool crouch, Vector2 direction, int frames = 45)
    {
        Teleport(motor, spawn);
        Step(motor, Vector2.zero, 60);
        Vector3 start = motor.transform.localPosition;
        Step(motor, direction, frames, run, slow, crouch);
        Vector3 delta = motor.transform.localPosition - start;
        return new Vector2(delta.x, delta.z).magnitude;
    }
    static void Teleport(SentinelContainerPlayer motor, Vector3 position)
    {
        var controller = motor.GetComponent<CharacterController>();
        controller.enabled = false;
        motor.transform.localPosition = position;
        controller.enabled = true;
        motor.SendMessage("OnDisable");
        Physics.SyncTransforms();
    }
    static void Step(SentinelContainerPlayer motor, Vector2 direction, int frames, bool run = false, bool slow = false, bool crouch = false)
    {
        for (int i = 0; i < frames; i++) motor.Simulate(direction, run, slow, crouch, false, false, 1f / 60);
    }
    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
