using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VRTraining;

/// <summary>
/// Tools > VR Training > Polish (Hide Cubes, Terminal FX, Step Locks, Player View FX)
/// Tools > VR Training > Validate Training Sequence
/// </summary>
public static class TrainingPolishTools
{
    const string ProfilePath = "Assets/VRTraining/Settings/FieldCam_PostFX.asset";

    static List<Transform> All() =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

    static TrainingTask Task(List<Transform> all, string prefix) =>
        all.Select(t => t.GetComponent<TrainingTask>()).FirstOrDefault(t => t && t.name.StartsWith(prefix));

    [MenuItem("Tools/VR Training/Polish (Hide Cubes, Terminal FX, Step Locks, Player View FX)")]
    public static void Polish()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = All();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[VR Training] Polish:\n");

        // 1) Hide the blue building-block cubes
        foreach (var g in all.Select(t => t.GetComponent<Grabbable>()).Where(g => g))
        {
            var mr = g.GetComponent<MeshRenderer>();
            var mf = g.GetComponent<MeshFilter>();
            if (!mr || !mf || !mf.sharedMesh || !mf.sharedMesh.name.Contains("Cube") || !mr.enabled) continue;
            bool hasModel = g.GetComponentsInChildren<Renderer>(true).Any(r => r != mr);
            if (hasModel)
            {
                Undo.RecordObject(mr, "Hide cube");
                mr.enabled = false;
                log.AppendLine($"  hid cube mesh on '{g.name}'");
            }
            else
            {
                Undo.RecordObject(g.gameObject, "Disable empty cube");
                g.gameObject.SetActive(false);
                log.AppendLine($"  turned off empty grab cube '{g.name}' (nothing inside it)");
            }
        }
        // Also any plain building-block cubes that are not grabbable
        foreach (var t in all.Where(t => t.name.StartsWith("[BuildingBlock] Cube")))
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (mr && mr.enabled && !t.GetComponent<Grabbable>())
            {
                Undo.RecordObject(mr, "Hide cube");
                mr.enabled = false;
                log.AppendLine($"  hid cube mesh on '{t.name}'");
            }
        }

        // 2) Terminal / CRT effect on every world-space instruction panel
        foreach (var c in all.Select(t => t.GetComponent<Canvas>()).Where(c => c && c.isRootCanvas && c.renderMode == RenderMode.WorldSpace))
        {
            if (!c.GetComponentInChildren<TMP_Text>(true) && !c.GetComponentInChildren<UnityEngine.UI.Text>(true)) continue;
            if (c.transform.root.name == "XR Setup") continue;
            if (!c.GetComponent<TerminalCRTEffect>())
            {
                Undo.AddComponent<TerminalCRTEffect>(c.gameObject);
                log.AppendLine($"  terminal effect added to '{c.name}'");
            }
        }
        // 3D labels (mix station, patient status) in terminal colors
        foreach (var tmp in all.Where(t => t.root.name == "Tasks").Select(t => t.GetComponent<TextMeshPro>()).Where(t => t))
        {
            Undo.RecordObject(tmp, "Label style");
            tmp.color = new Color(1f, 0.25f, 0.25f);
            tmp.fontStyle = FontStyles.Bold;
        }

        // 3) Step locks: objects only become interactive when their step starts
        Gate(all, "Task_03", "Grabbable_GreenHerb", log);
        Gate(all, "Task_04", "Grabbable_RedHerb", log);
        Gate(all, "Task_06", "Grabbable_HealingSpray", log);
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var t7 = Task(all, "Task_02");
        if (keypad && t7)
        {
            Undo.RecordObject(keypad, "Keypad lock");
            keypad.onlyDuringTask = t7;
            var g = AssetDatabase.FindAssets("sfx_keypadDeniedd t:AudioClip").FirstOrDefault();
            if (g != null) keypad.deniedSound = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
            log.AppendLine("  keypad locked until Step 7 (shows ACCESS DENIED before that)");
        }

        // 4) Player view FX (post processing)
        AddPlayerViewFX(all, log);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    static void Gate(List<Transform> all, string taskPrefix, string objName, StringBuilder log)
    {
        var task = Task(all, taskPrefix);
        var obj = all.FirstOrDefault(t => t.name == objName);
        if (!task || !obj) { log.AppendLine($"  (skip gate {objName})"); return; }
        var gate = task.GetComponent<TaskGate>() ?? null;
        if (!gate) gate = Undo.AddComponent<TaskGate>(task.gameObject);
        var behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true)
                            .Where(b => b is HandGrabInteractable || b is GrabInteractable || b is PokeInteractable)
                            .Cast<Behaviour>().ToArray();
        gate.unlockWhenTaskStarts = task;
        gate.lockedBehaviours = behaviours;
        EditorUtility.SetDirty(gate);
        log.AppendLine($"  '{objName}' can only be grabbed from {task.name} on ({behaviours.Length} interactables locked)");
    }

    static void AddPlayerViewFX(List<Transform> all, StringBuilder log)
    {
        if (all.Any(t => t.GetComponent<PlayerViewFX>())) { log.AppendLine("  player view FX already present"); return; }

        if (!AssetDatabase.IsValidFolder("Assets/VRTraining/Settings")) AssetDatabase.CreateFolder("Assets/VRTraining", "Settings");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.8f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-30f);
            ca.contrast.Override(12f);
            ca.colorFilter.Override(new Color(0.9f, 1f, 0.93f));   // cold, sickly lab tint

            var chrom = profile.Add<ChromaticAberration>(true);
            chrom.intensity.Override(0.08f);

            foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        var xr = all.FirstOrDefault(t => t.name == "XR Setup" && t.parent == null);
        var go = new GameObject("Player_View_FX (Post Processing)");
        Undo.RegisterCreatedObjectUndo(go, "View FX");
        if (xr) go.transform.SetParent(xr, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 10;
        vol.sharedProfile = profile;
        var fx = go.AddComponent<PlayerViewFX>();
        fx.volume = vol;

        // Post processing must be on for the headset cameras.
        foreach (var cam in all.Select(t => t.GetComponent<Camera>()).Where(c => c))
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data && !data.renderPostProcessing)
            {
                Undo.RecordObject(data, "Post FX");
                data.renderPostProcessing = true;
            }
        }

        // Green edge pulse on every verified step; red heartbeat while the patient is critical.
        foreach (var task in all.Select(t => t.GetComponent<TrainingTask>()).Where(t => t))
            UnityEventTools.AddPersistentListener(task.onTaskCompleted, fx.PulseSuccess);
        var t6 = Task(all, "Task_07");
        if (t6)
        {
            UnityEventTools.AddPersistentListener(t6.onTaskStarted, fx.DangerOn);
            UnityEventTools.AddPersistentListener(t6.onTaskCompleted, fx.DangerOff);
        }
        log.AppendLine("  player view FX: vignette, film grain, cold desaturated grade, light chromatic aberration; green pulse on step complete; red heartbeat in Step 6");
    }

    // ------------------------------------------------------------------ validation
    [MenuItem("Tools/VR Training/Validate Training Sequence")]
    public static void Validate()
    {
        var all = All();
        var sb = new StringBuilder("[VR Training] VALIDATION\n");
        int problems = 0;
        var manager = all.Select(t => t.GetComponent<TrainingManager>()).FirstOrDefault(m => m);
        if (!manager) { Debug.LogError("No TrainingManager."); return; }
        sb.AppendLine($"Manager: {manager.tasks.Count} tasks, prompt UI: {(manager.promptUI ? "OK" : "MISSING")}");
        if (!manager.promptUI) problems++;

        for (int i = 0; i < manager.tasks.Count; i++)
        {
            var t = manager.tasks[i];
            if (!t) { sb.AppendLine($"  {i + 1}. MISSING TASK"); problems++; continue; }
            // Who calls CompleteTask on this task?
            var callers = new List<string>();
            foreach (var mb in all.SelectMany(x => x.GetComponents<MonoBehaviour>()).Where(m => m))
            {
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyPath.EndsWith("m_Target") && it.objectReferenceValue == t)
                    {
                        var methodProp = so.FindProperty(it.propertyPath.Replace("m_Target", "m_MethodName"));
                        if (methodProp != null && methodProp.stringValue == "CompleteTask")
                            callers.Add($"{mb.name}.{it.propertyPath.Split('.')[0]}");
                    }
                }
            }
            bool gatedInputs = true;
            foreach (var ev in all.Select(x => x.GetComponent<TrainingInteractionEvents>()).Where(e => e && e.transform.IsChildOf(t.transform)))
                if (ev.onlyDuringTask != t) gatedInputs = false;
            foreach (var z in all.Select(x => x.GetComponent<TrainingTriggerZone>()).Where(z => z && z.transform.IsChildOf(t.transform)))
                if (z.onlyDuringTask != t) gatedInputs = false;

            string status = callers.Count > 0 && gatedInputs ? "OK" : "CHECK";
            if (status != "OK") problems++;
            sb.AppendLine($"  {i + 1}. {t.name}  [{status}]  completed by: {(callers.Count > 0 ? string.Join(", ", callers) : "NOTHING")}{(gatedInputs ? "" : "  (some events not limited to this step)")}");
        }
        sb.AppendLine("Rule: TrainingTask.CompleteTask() is ignored unless that task is the active one, so steps can only finish in order.");
        sb.AppendLine(problems == 0 ? "RESULT: all steps wired in order." : $"RESULT: {problems} item(s) to check.");
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("Assets/VRTraining/validation.txt", sb.ToString());
    }
}
