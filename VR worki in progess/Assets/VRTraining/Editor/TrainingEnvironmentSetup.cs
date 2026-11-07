using System.Linq;
using System.Text;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using VRTraining;

/// <summary>
/// Tools > VR Training > Add Environment FX + Sound Slots
/// Fog, floating dust, flickering light, red emergency beacon, steam vents, and empty sound slots.
/// Safe to run more than once.
/// </summary>
public static class TrainingEnvironmentSetup
{
    const string Settings = "Assets/VRTraining/Settings";

    static T Find<T>(string name = null) where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include).FirstOrDefault(c => name == null || c.name == name || c.name.StartsWith(name));
    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : Undo.AddComponent<T>(go);
    static AudioSource Src(GameObject go, float spatial = 1f, bool loop = false)
    {
        var a = GetOrAdd<AudioSource>(go);
        a.playOnAwake = false; a.spatialBlend = spatial; a.loop = loop; a.minDistance = 1f; a.maxDistance = 20f;
        return a;
    }
    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t) return t;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
    static void Listen(UnityEvent ev, UnityAction a)
    {
        for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            if (ev.GetPersistentTarget(i) == (Object)a.Target && ev.GetPersistentMethodName(i) == a.Method.Name) return;
        UnityEventTools.AddPersistentListener(ev, a);
    }

    // A soft, see-through particle material (URP Particles/Unlit, alpha blended).
    static Material SmokeMaterial()
    {
        var path = Settings + "/FX_SoftParticle.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        if (tex) m.SetTexture("_BaseMap", tex);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material BeaconMaterial()
    {
        var path = Settings + "/FX_RedBeacon.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.5f, 0.02f, 0.02f));
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Steam(Transform parent, string name, Vector3 pos, Vector3 dir, StringBuilder log)
    {
        var t = Child(parent, name);
        t.position = pos;
        t.rotation = Quaternion.LookRotation(dir);
        var ps = GetOrAdd<ParticleSystem>(t.gameObject);
        var main = ps.main;
        main.duration = 1f; main.loop = true; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startColor = new Color(0.85f, 0.88f, 0.9f, 0.35f);
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 22;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 0.05f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.2f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.5f, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = g;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.4f;
        var r = t.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = SmokeMaterial(); r.sortMode = ParticleSystemSortMode.Distance;
        var vent = GetOrAdd<SteamVent>(t.gameObject);
        vent.audioSource = Src(t.gameObject);
        EditorUtility.SetDirty(vent);
        log.AppendLine($"  steam vent '{name}' at {pos}");
    }

    [MenuItem("Tools/VR Training/Add Environment FX + Sound Slots")]
    public static void Setup()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        if (!AssetDatabase.IsValidFolder(Settings)) AssetDatabase.CreateFolder("Assets/VRTraining", "Settings");
        var log = new StringBuilder("[VR Training] Environment FX:\n");

        var manager = Find<TrainingManager>();
        var fx = Child(manager.transform, "FX_Environment");
        var head = Camera.main ? Camera.main.transform : GameObject.Find("CenterEyeAnchor")?.transform;
        var tasks = Object.FindObjectsByType<TrainingTask>(FindObjectsInactive.Include);
        TrainingTask T(string p) => tasks.FirstOrDefault(t => t.name.StartsWith(p));

        // 1) Fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.fogColor = new Color(0.10f, 0.12f, 0.11f);
        log.AppendLine("  fog on (Lighting window > Environment): exponential, density 0.035, dark green-grey");

        // 2) Floating dust around the player
        var dust = Child(fx, "FX_Dust");
        var dps = GetOrAdd<ParticleSystem>(dust.gameObject);
        var dm = dps.main;
        dm.loop = true; dm.playOnAwake = true; dm.startLifetime = 8f;
        dm.startSpeed = 0.02f; dm.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.018f);
        dm.startColor = new Color(1f, 0.97f, 0.9f, 0.45f);
        dm.simulationSpace = ParticleSystemSimulationSpace.World; dm.maxParticles = 400;
        var de = dps.emission; de.rateOverTime = 45;
        var ds = dps.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(6f, 3f, 6f);
        var dn = dps.noise; dn.enabled = true; dn.strength = 0.05f; dn.frequency = 0.2f;
        var dc = dps.colorOverLifetime; dc.enabled = true;
        var dg = new Gradient();
        dg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                   new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
        dc.color = dg;
        dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMaterial();
        GetOrAdd<FollowHead>(dust.gameObject).head = head;
        log.AppendLine("  floating dust follows the player's head (6 x 3 x 6 m box)");

        // 3) Flickering light: the real light closest to the player start
        var rig = GameObject.Find("[BuildingBlock] Camera Rig");
        var start = rig ? rig.transform.position : Vector3.zero;
        var flick = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
            .Where(l => l.type != LightType.Directional && !l.transform.IsChildOf(manager.transform))
            .OrderBy(l => Vector3.Distance(l.transform.position, start)).FirstOrDefault();
        if (flick)
        {
            var lf = GetOrAdd<LightFlicker>(flick.gameObject);
            lf.audioSource = Src(flick.gameObject, 1f, true);
            EditorUtility.SetDirty(lf);
            log.AppendLine($"  flicker on light '{flick.name}' at {flick.transform.position:F1}");
        }

        // 4) Red emergency beacon on the medical bay ceiling, switched on by the scare
        var em = Child(fx, "FX_EmergencyBeacon");
        em.position = new Vector3(-0.55f, 4.0f, 2.3f);
        var beaconT = em.Find("Beacon_Housing");
        if (!beaconT)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(b, "beacon");
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.name = "Beacon_Housing";
            b.transform.SetParent(em, false);
            b.transform.localPosition = new Vector3(0, 0.1f, 0);
            b.transform.localScale = new Vector3(0.22f, 0.1f, 0.22f);
            b.GetComponent<MeshRenderer>().sharedMaterial = BeaconMaterial();
            beaconT = b.transform;
        }
        var spin = Child(em, "Spinner");
        spin.localPosition = Vector3.zero;
        var spotT = Child(spin, "Red_Spot");
        spotT.localPosition = Vector3.zero;
        spotT.localRotation = Quaternion.Euler(35, 0, 0);
        var spot = GetOrAdd<Light>(spotT.gameObject);
        spot.type = LightType.Spot; spot.color = new Color(1f, 0.08f, 0.04f);
        spot.intensity = 12f; spot.range = 12f; spot.spotAngle = 70f; spot.shadows = LightShadows.None;
        var el = GetOrAdd<EmergencyLight>(em.gameObject);
        el.spinner = spin; el.spot = spot; el.beacon = beaconT.GetComponent<Renderer>();
        el.audioSource = Src(em.gameObject, 0.6f, true);
        EditorUtility.SetDirty(el);
        var scare = Find<ScareSequence>();
        if (scare) { Listen(scare.onLightsOut, el.Activate); EditorUtility.SetDirty(scare); }
        log.AppendLine("  red emergency beacon on the med bay ceiling, turns on when the lights cut out");

        // 5) Steam vents
        Steam(fx, "FX_Steam_MedBay", new Vector3(-3.1f, 0.35f, 0.1f), Vector3.up, log);
        Steam(fx, "FX_Steam_Pipes", new Vector3(5.4f, 2.0f, -2.5f), new Vector3(0, 0.3f, -1f), log);

        // 6) Sound slots
        var hooksT = Child(manager.transform, "Sound_Hooks");
        var hooks = GetOrAdd<TrainingSoundHooks>(hooksT.gameObject);
        hooks.manager = manager; hooks.audioSource = Src(hooksT.gameObject, 0f);
        hooks.mixTask = T("Task_05"); hooks.patientTask = T("Task_07");
        EditorUtility.SetDirty(hooks);
        log.AppendLine("  Sound_Hooks: intro announcement, step start, step complete, mix, stabilized");

        foreach (var n in new[] { "Grabbable_GreenHerb", "Grabbable_RedHerb", "Grabbable_HealingSpray" })
        {
            var g = Find<Grabbable>(n);
            if (!g) continue;
            var gs = GetOrAdd<GrabSound>(g.gameObject);
            gs.pointable = g; gs.audioSource = Src(g.gameObject);
            EditorUtility.SetDirty(gs);
            log.AppendLine($"  grab sound slots on {n}");
        }

        var sprayFx = Find<SprayEffect>();
        if (sprayFx && !sprayFx.loopAudio)
        {
            sprayFx.loopAudio = Src(sprayFx.gameObject, 1f, true);
            EditorUtility.SetDirty(sprayFx);
        }
        if (sprayFx) log.AppendLine($"  spray hiss: AudioSource on '{sprayFx.name}' (loops while spraying)");

        var dummy = GameObject.Find("Patient_Dummy");
        if (dummy)
        {
            var monT = Child(dummy.transform.parent, "Patient_Monitor_Audio");
            monT.position = dummy.transform.position + Vector3.up * 1.4f;
            var mon = GetOrAdd<PatientMonitor>(monT.gameObject);
            mon.treatTask = T("Task_07"); mon.audioSource = Src(monT.gameObject);
            EditorUtility.SetDirty(mon);
            log.AppendLine("  heart monitor beep beside the patient");
        }

        if (rig)
        {
            var fs = GetOrAdd<Footsteps>(rig);
            fs.head = head; fs.audioSource = Src(rig, 0f);
            EditorUtility.SetDirty(fs);
            log.AppendLine("  footsteps on the camera rig");
        }

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }
}
