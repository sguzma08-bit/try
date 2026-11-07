using System.Linq;
using System.Text;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using VRTraining;

/// <summary>
/// Tools > VR Training > Add Atmosphere (Door Close, Banging, Twitch, Mist, Inspect, Ambient)
/// Safe to run more than once - it reuses what is already there.
/// </summary>
public static class TrainingAtmosphereSetup
{
    static T Find<T>(string name = null) where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include)
              .FirstOrDefault(c => name == null || c.name == name || c.name.StartsWith(name));

    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : Undo.AddComponent<T>(go);

    static bool HasListener(UnityEventBase ev, Object target, string method)
    {
        for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            if (ev.GetPersistentTarget(i) == target && ev.GetPersistentMethodName(i) == method) return true;
        return false;
    }
    static void Listen(UnityEvent ev, UnityAction action)
    {
        if (!HasListener(ev, (Object)action.Target, action.Method.Name)) UnityEventTools.AddPersistentListener(ev, action);
    }

    static AudioSource Audio3D(GameObject go)
    {
        var a = GetOrAdd<AudioSource>(go);
        a.playOnAwake = false; a.spatialBlend = 1f; a.minDistance = 1.5f; a.maxDistance = 25f;
        return a;
    }

    [MenuItem("Tools/VR Training/Add Atmosphere (Door Close, Banging, Twitch, Mist, Inspect, Ambient)")]
    public static void Setup()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[VR Training] Atmosphere:\n");

        var door = Find<DoorOpener>();
        var blocker = Find<DoorBlocker>();
        var t7 = Object.FindObjectsByType<TrainingTask>(FindObjectsInactive.Include).FirstOrDefault(t => t.name.StartsWith("Task_07"));
        var manager = Find<TrainingManager>();
        var head = Camera.main ? Camera.main.transform : GameObject.Find("CenterEyeAnchor")?.transform;
        var fxRoot = manager ? manager.transform : null;

        // ---------- 1) Door closes behind the player ----------
        if (door && blocker)
        {
            var ac = GetOrAdd<DoorAutoClose>(door.gameObject);
            ac.door = door;
            ac.head = blocker.head ? blocker.head : head;
            ac.roomDirection = blocker.roomDirection;
            ac.doorPlanePoint = blocker.doorPlanePoint;
            EditorUtility.SetDirty(ac);
            log.AppendLine("  door closes once the player is 1.5 m inside the medical bay");
        }
        else log.AppendLine("  (skip door close - DoorOpener/DoorBlocker not found)");

        // ---------- 2) Banging ----------
        DoorBanger banger = null;
        if (door && door.doorPanel)
        {
            banger = GetOrAdd<DoorBanger>(door.gameObject);
            banger.doorPanel = door.doorPanel;
            banger.audioSource = Audio3D(door.doorPanel.gameObject);
            banger.roomDirection = blocker ? blocker.roomDirection : Vector3.forward;
            EditorUtility.SetDirty(banger);
            log.AppendLine("  door banger on " + door.name + " (sound slot 'Bang Clips' is empty - add your clips)");
        }

        // ---------- 3) Dummy twitch ----------
        DummyTwitch twitch = null;
        var dummy = GameObject.Find("Patient_Dummy") ?? Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name.StartsWith("Patient_Dummy"))?.gameObject;
        if (dummy)
        {
            twitch = GetOrAdd<DummyTwitch>(dummy);
            var bones = dummy.GetComponentsInChildren<Transform>(true);
            twitch.headBone = bones.FirstOrDefault(b => b.name.ToLower().EndsWith("head")) ?? bones.FirstOrDefault(b => b.name.ToLower().Contains("head") && !b.name.ToLower().Contains("end") && !b.name.ToLower().Contains("top"));
            twitch.armBone = bones.FirstOrDefault(b => b.name.ToLower().Contains("rightforearm")) ?? bones.FirstOrDefault(b => b.name.ToLower().Contains("forearm"));
            twitch.audioSource = Audio3D(dummy);
            EditorUtility.SetDirty(twitch);
            log.AppendLine($"  dummy twitch: head bone '{(twitch.headBone ? twitch.headBone.name : "NOT FOUND")}', arm bone '{(twitch.armBone ? twitch.armBone.name : "none")}'");
        }

        // ---------- 4) Scare sequence after the patient is stabilized ----------
        if (fxRoot && t7)
        {
            var seqT = fxRoot.Find("FX_ScareSequence");
            if (!seqT) { var go = new GameObject("FX_ScareSequence"); Undo.RegisterCreatedObjectUndo(go, "scare"); go.transform.SetParent(fxRoot, false); seqT = go.transform; }
            var seq = GetOrAdd<ScareSequence>(seqT.gameObject);
            var lighting = GameObject.Find("Lighting");
            seq.lights = lighting ? lighting.GetComponentsInChildren<Light>(true) : Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            seq.viewFX = Find<PlayerViewFX>();
            seq.twitch = twitch;
            seq.banger = banger;
            seq.door = door;
            seq.audioSource = GetOrAdd<AudioSource>(seqT.gameObject);
            seq.audioSource.playOnAwake = false; seq.audioSource.spatialBlend = 0f;
            EditorUtility.SetDirty(seq);
            Listen(t7.onTaskCompleted, seq.Play);
            EditorUtility.SetDirty(t7);
            log.AppendLine($"  scare sequence: {t7.name} complete -> 2 s -> lights out ({seq.lights.Length} lights) + twitch -> 3 s -> banging");
        }

        // ---------- 5) Spray mist ----------
        var zone = Find<TrainingTriggerZone>("Treatment_TriggerZone");
        var nozzle = Find<Transform>("Spray_ParticleFX");
        var sprayEvents = Find<TrainingInteractionEvents>("Events_Grab_HealingSpray");
        if (dummy && nozzle && zone)
        {
            var mist = GetOrAdd<SprayMist>(zone.gameObject);
            mist.dummy = dummy.transform; mist.nozzle = nozzle; mist.treatmentZone = zone; mist.sprayHeld = sprayEvents;
            EditorUtility.SetDirty(mist);
            log.AppendLine("  spray mist leaves green residue on the dummy");
        }

        // ---------- 6) Item inspection ----------
        void Inspect(string obj, string title, string desc, string hint)
        {
            var g = Find<Grabbable>(obj);
            if (!g) { log.AppendLine($"  (skip inspect {obj})"); return; }
            var l = GetOrAdd<ItemInspectLabel>(g.gameObject);
            l.pointable = g; l.itemName = title; l.description = desc; l.hint = hint;
            EditorUtility.SetDirty(l);
            log.AppendLine($"  inspect card on {obj}");
        }
        Inspect("Grabbable_GreenHerb", "GREEN HERB", "Restores mild tissue damage. Weak on its own.", "Combine with a RED HERB at the mix station.");
        Inspect("Grabbable_RedHerb", "RED HERB", "No effect alone. Boosts the potency of other herbs.", "Combine with a GREEN HERB at the mix station.");
        Inspect("Grabbable_HealingSpray", "FIRST AID SPRAY", "Mixed herb compound. Fully treats wounds.", "Spray directly on the patient.");

        // ---------- 7) Ambient sound ----------
        if (fxRoot)
        {
            var amb = fxRoot.Find("Ambient_Soundscape");
            if (!amb) { var go = new GameObject("Ambient_Soundscape"); Undo.RegisterCreatedObjectUndo(go, "amb"); go.transform.SetParent(fxRoot, false); amb = go.transform; }
            var sc = GetOrAdd<AmbientSoundscape>(amb.gameObject);
            sc.listener = head;
            sc.heartbeatTask = t7;
            EditorUtility.SetDirty(sc);
            log.AppendLine("  ambient soundscape (hum / creaks / heartbeat slots empty - add your clips)");
        }

        // ---------- 8) Ending: door cracks open, fade to black, title ----------
        if (fxRoot && banger)
        {
            var endT = fxRoot.Find("FX_Ending");
            if (!endT) { var go = new GameObject("FX_Ending"); Undo.RegisterCreatedObjectUndo(go, "ending"); go.transform.SetParent(fxRoot, false); endT = go.transform; }
            var end = GetOrAdd<EndingSequence>(endT.gameObject);
            end.doorPanel = banger.doorPanel;
            end.openDirection = door ? door.openDirection : Vector3.up;
            end.roomDirection = banger.roomDirection;
            end.head = head;
            end.audioSource = GetOrAdd<AudioSource>(endT.gameObject);
            end.audioSource.playOnAwake = false; end.audioSource.spatialBlend = 0f;
            EditorUtility.SetDirty(end);
            Listen(banger.onFinished, end.Play);
            EditorUtility.SetDirty(banger);
            log.AppendLine("  ending: banging finished -> door forced open a crack (red light) -> fade to black -> TRAINING COMPLETE / TO BE CONTINUED");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }
}
