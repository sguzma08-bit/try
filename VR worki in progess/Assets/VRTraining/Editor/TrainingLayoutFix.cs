using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VRTraining;

/// <summary>
/// Tools > VR Training > Layout Fix (Start Outside, Door Lock First, Rest Items, VR-safe FX)
/// </summary>
public static class TrainingLayoutFix
{
    [MenuItem("Tools/VR Training/Layout Fix (Start Outside, Door Lock First, Rest Items, VR-safe FX)")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var log = new StringBuilder("[VR Training] Layout fix:\n");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        FixFX(log);
        FitCollidersAndSettle(all, log);
        ReorderAndStartOutside(all, log);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------ 1. VR-safe post FX
    static void FixFX(StringBuilder log)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/VRTraining/Settings/FieldCam_PostFX.asset");
        if (!profile) return;
        if (profile.TryGet(out Vignette v)) { v.active = false; v.intensity.Override(0f); }
        if (profile.TryGet(out ChromaticAberration ch)) { ch.active = false; }
        if (profile.TryGet(out FilmGrain g)) { g.intensity.Override(0.15f); g.type.Override(FilmGrainLookup.Thin1); }
        if (profile.TryGet(out ColorAdjustments ca)) { ca.saturation.Override(-25f); ca.contrast.Override(10f); }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        log.AppendLine("  post FX: vignette + chromatic aberration off (they break in XR), lighter grain, grade kept; step pulses now tint the view");
    }

    // ------------------------------------------------------------ 2. colliders + resting
    static void FitCollidersAndSettle(List<Transform> all, StringBuilder log)
    {
        var grabbables = all.Select(t => t.GetComponent<Grabbable>()).Where(g => g && g.gameObject.activeInHierarchy).ToList();
        // a) fit each grab collider to the visible model (pot, can...) so it rests on its base
        foreach (var g in grabbables)
        {
            var box = g.GetComponent<BoxCollider>();
            var own = g.GetComponent<Renderer>();
            var rs = g.GetComponentsInChildren<Renderer>(true).Where(r => r != own && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            if (!box || rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
            var t = g.transform; var s = t.lossyScale;
            Undo.RecordObject(box, "Fit collider");
            box.center = t.InverseTransformPoint(b.center);
            box.size = new Vector3(Mathf.Abs(b.size.x / s.x), Mathf.Abs(b.size.y / s.y), Mathf.Abs(b.size.z / s.z));
        }
        Physics.SyncTransforms();
        // b) settle each item so its base sits on whatever is under it
        int n = 0;
        foreach (var g in grabbables)
        {
            var box = g.GetComponent<BoxCollider>();
            if (!box) continue;
            var b = box.bounds;
            var origin = new Vector3(b.center.x, b.center.y, b.center.z);
            var hit = Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore)
                             .Where(h => !h.collider.transform.IsChildOf(g.transform) && !grabbables.Any(o => o != g && h.collider.transform.IsChildOf(o.transform)))
                             .OrderBy(h => h.distance).FirstOrDefault();
            if (!hit.collider) continue;
            float dy = (hit.point.y + 0.003f) - b.min.y;
            if (Mathf.Abs(dy) < 0.0005f) continue;
            if (dy < -0.3f) { log.AppendLine($"  '{g.name}': nothing solid right under it ({hit.collider.name} is {-dy:0.00} m below) - left in place"); continue; }
            Undo.RecordObject(g.transform, "Settle");
            g.transform.position += Vector3.up * dy;
            n++;
        }
        Physics.SyncTransforms();
        log.AppendLine($"  {grabbables.Count} grab colliders fitted to their models; {n} items settled onto the surface below them");
    }

    // ------------------------------------------------------------ 3. new order + start outside + door block
    static void ReorderAndStartOutside(List<Transform> all, StringBuilder log)
    {
        var manager = all.Select(t => t.GetComponent<TrainingManager>()).FirstOrDefault(m => m);
        var unlock = all.Select(t => t.GetComponent<TrainingTask>()).FirstOrDefault(t => t && t.name.Contains("Unlock_Door"));
        var opener = all.Select(t => t.GetComponent<DoorOpener>()).FirstOrDefault(o => o);
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var prompt = all.Select(t => t.GetComponent<TrainingPromptUI>()).FirstOrDefault(p => p);
        var rig = all.FirstOrDefault(t => t.name.Contains("Camera Rig"));
        var head = rig ? rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CenterEyeAnchor") : null;
        var table = all.FirstOrDefault(t => t.name == "Wall_Table_01");
        if (!manager || !unlock || !opener || !opener.doorPanel || !keypad || !prompt || !rig)
        { log.AppendLine("  (missing objects - reorder skipped)"); return; }

        // a) Task order: onboarding -> unlock door -> herbs... -> treat patient
        Undo.RecordObject(manager, "Reorder");
        var list = manager.tasks.Where(t => t && t != unlock).ToList();
        list.Insert(1, unlock);
        manager.tasks = list;
        manager.completionMessage = "You combined the herbs into a healing spray and stabilized the patient.\nWell done, medic.";
        string[] names = {
            "Task_01_Onboarding_PokeToStart", "Task_02_Unlock_Door", "Task_03_Grab_GreenHerb", "Task_04_Grab_RedHerb",
            "Task_05_Combine_Herbs", "Task_06_PickUp_Spray", "Task_07_Treat_Patient" };
        for (int i = 0; i < list.Count && i < names.Length; i++)
        {
            Undo.RecordObject(list[i].gameObject, "Rename");
            list[i].gameObject.name = names[i];
            list[i].transform.SetSiblingIndex(i);
        }
        Undo.RecordObject(unlock, "Text");
        unlock.taskTitle = "Enter the Medical Bay";
        unlock.instructions = "The medical bay is sealed.\nWalk to the door and poke ANY 4-digit code into the keypad to unlock it.";
        unlock.completionMessage = "Access granted - enter the medical bay.";
        var first = list[0];
        Undo.RecordObject(first, "Text");
        first.instructions = first.instructions.Replace("Poke this screen to begin training.", "Poke this screen to begin. Your first procedure is to unlock the medical bay door.");
        EditorUtility.SetDirty(manager);
        log.AppendLine("  new order: Onboarding -> Unlock Door -> Green Herb -> Red Herb -> Combine -> Spray -> Treat Patient");

        // b) Which way is the room? (from the door toward the table)
        var panel = opener.doorPanel;
        var pb = Bounds(panel.gameObject);
        Vector3 toRoom = table ? table.position - pb.center : Vector3.forward;
        toRoom.y = 0;
        // snap to the door's dominant horizontal axis
        Vector3 roomDir = Mathf.Abs(toRoom.x) > Mathf.Abs(toRoom.z) ? new Vector3(Mathf.Sign(toRoom.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toRoom.z));

        // c) Keypad on the OUTSIDE face of the door, at the intercom
        var intercom = all.FirstOrDefault(t => t.name == "Intercom_01" && t.parent && t.parent.name == "Door_Vert_01");
        float lateralX = intercom ? intercom.position.x : pb.center.x, lateralZ = intercom ? intercom.position.z : pb.center.z;
        var outsidePoint = new Vector3(lateralX, 1.4f, lateralZ) - roomDir * 3f;
        Physics.SyncTransforms();
        var hit = Physics.RaycastAll(outsidePoint, roomDir, 6f, ~0, QueryTriggerInteraction.Ignore)
                         .Where(h => h.collider.transform.IsChildOf(panel)).OrderBy(h => h.distance).FirstOrDefault();
        Vector3 doorFace;
        if (hit.collider)
        {
            doorFace = hit.point;
            Undo.SetTransformParent(keypad.transform, panel.parent ? panel.parent : panel, "Unparent");
            Undo.RecordObject(keypad.transform, "Move keypad");
            keypad.transform.position = hit.point - roomDir * 0.015f;
            keypad.transform.rotation = Quaternion.LookRotation(roomDir, Vector3.up);
            Undo.SetTransformParent(keypad.transform, panel, "Mount keypad");
            log.AppendLine($"  keypad moved to the OUTSIDE of the door at {keypad.transform.position}");
        }
        else
        {
            doorFace = pb.center - roomDir * pb.extents.magnitude * 0.2f;
            log.AppendLine("  (could not find the outside face of the door - keypad left in place)");
        }
        keypad.onlyDuringTask = unlock;
        EditorUtility.SetDirty(keypad);

        // d) Player starts outside, in front of the start screen
        var pr = prompt.transform;
        var f = pr.forward; f.y = 0; f.Normalize();            // canvas forward points away from the reader
        var start = pr.position - f * 1.6f;
        float floorY = FloorY(start, rig.position.y);
        float rigFloorOffset = rig.position.y - FloorY(rig.position, 0f);
        Undo.RecordObject(rig, "Move player start");
        rig.position = new Vector3(start.x, floorY + rigFloorOffset, start.z);
        rig.rotation = Quaternion.LookRotation(f, Vector3.up);
        log.AppendLine($"  player now starts outside at {rig.position}, facing the start screen");

        // e) Door blocker
        var blocker = unlock.GetComponent<DoorBlocker>();
        if (!blocker) blocker = Undo.AddComponent<DoorBlocker>(unlock.gameObject);
        blocker.door = opener;
        blocker.rig = rig;
        blocker.head = head ? head : rig;
        blocker.roomDirection = roomDir;
        blocker.doorPlanePoint = doorFace;
        EditorUtility.SetDirty(blocker);
        log.AppendLine($"  door blocker: player cannot pass into the room (direction {roomDir}) until the code is entered");
    }

    [MenuItem("Tools/VR Training/Place Player Start On Floor")]
    public static void PlacePlayerOnFloor()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var rig = all.FirstOrDefault(t => t.name.Contains("Camera Rig"));
        if (!rig) return;
        Physics.SyncTransforms();
        var o = new Vector3(rig.position.x, rig.position.y + 1.5f, rig.position.z);
        var h = Physics.RaycastAll(o, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore)
                       .Where(x => x.normal.y > 0.8f && !x.collider.transform.IsChildOf(rig))
                       .OrderBy(x => x.distance).FirstOrDefault();
        if (!h.collider) { o.y = 5f; h = Physics.RaycastAll(o, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore).Where(x => x.normal.y > 0.8f).OrderByDescending(x => x.point.y).FirstOrDefault(); }
        if (!h.collider) { Debug.LogWarning("[VR Training] No floor found under the player start."); return; }
        Undo.RecordObject(rig, "Player on floor");
        rig.position = new Vector3(rig.position.x, h.point.y, rig.position.z);
        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        Debug.Log($"[VR Training] Player start placed on '{h.collider.name}' at y={h.point.y:0.00}.");
    }

    [MenuItem("Tools/VR Training/Fix Start Room (screen faces player, player in front)")]
    public static void FixStartRoom()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var prompt = all.Select(t => t.GetComponent<TrainingPromptUI>()).FirstOrDefault(p => p);
        var blocker = all.Select(t => t.GetComponent<DoorBlocker>()).FirstOrDefault(b => b);
        var rig = all.FirstOrDefault(t => t.name.Contains("Camera Rig") && t.GetComponent<MonoBehaviour>());
        if (!prompt || !blocker || !rig) { Debug.LogWarning("[VR Training] prompt panel, door blocker or rig missing"); return; }
        var roomDir = blocker.roomDirection.normalized;     // toward the medical bay (door side)
        var panel = prompt.transform;

        // Screen faces into the small start room (toward the door side), at eye level.
        Undo.RecordObject(panel, "Turn start screen");
        panel.rotation = Quaternion.LookRotation(-roomDir, Vector3.up);   // reader stands on the +roomDir side
        panel.position = new Vector3(panel.position.x, 1.65f, panel.position.z);

        // Player stands 1.6 m in front of it, facing it.
        var start = panel.position + roomDir * 1.6f;
        Physics.SyncTransforms();
        var h = Physics.RaycastAll(new Vector3(start.x, 1.2f, start.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)
                       .Where(x => x.normal.y > 0.8f && !x.collider.transform.IsChildOf(rig)).OrderBy(x => x.distance).FirstOrDefault();
        float y = h.collider ? h.point.y : 0f;
        Undo.RecordObject(rig, "Player start");
        rig.position = new Vector3(start.x, y, start.z);
        rig.rotation = Quaternion.LookRotation(-roomDir, Vector3.up);
        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        Debug.Log($"[VR Training] Start screen now faces the start room at {panel.position}; player starts at {rig.position} on '{(h.collider ? h.collider.name : "none")}', facing the screen. The door/keypad is behind the player.");
    }

    [MenuItem("Tools/VR Training/Comfort + Face Start Screen")]
    public static void ComfortAndFacing()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var tunnel = all.FirstOrDefault(t => t.name == "SmoothMovementTunneling");
        if (tunnel) { Undo.RecordObject(tunnel.gameObject, "Tunneling off"); tunnel.gameObject.SetActive(false); }
        var rig = all.FirstOrDefault(t => t.name == "[BuildingBlock] Camera Rig");
        var head = rig ? rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CenterEyeAnchor") : null;
        var prompt = all.Select(t => t.GetComponent<TrainingPromptUI>()).FirstOrDefault(p => p);
        var mgr = all.Select(t => t.GetComponent<TrainingManager>()).FirstOrDefault(m => m);
        if (mgr && rig && head && prompt)
        {
            var f = mgr.GetComponent<FaceTargetOnStart>();
            if (!f) f = Undo.AddComponent<FaceTargetOnStart>(mgr.gameObject);
            f.rig = rig; f.head = head; f.target = prompt.transform;
            EditorUtility.SetDirty(f);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[VR Training] Movement tunneling {(tunnel ? "turned off" : "not found")}; player turns to face the start screen on load ({(head ? "ok" : "head missing")}).");
    }

    [MenuItem("Tools/VR Training/Debug: Floor Map")]
    public static void FloorMap()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Physics.SyncTransforms();
        var marks = new Dictionary<string, Vector3>();
        void Mark(string k, string name) { var t = all.FirstOrDefault(x => x.name == name); if (t) marks[k] = t.position; }
        Mark("R", "[BuildingBlock] Camera Rig"); Mark("D", "Door_Vert_01"); Mark("P", "Onboarding_Prompt_Panel (Poke)");
        Mark("T", "Wall_Table_01"); Mark("M", "Patient_Dummy"); Mark("K", "Keypad_PokePanel (Intercom)"); Mark("X", "MixStation");
        var sb = new StringBuilder("FLOOR MAP (x -> right, z up). '.'=floor  '#'=blocked at 1m  ' '=nothing\n");
        for (float z = 10f; z >= -14f; z -= 0.5f)
        {
            var line = new StringBuilder($"{z,6:0.0} ");
            for (float x = -10f; x <= 12f; x += 0.5f)
            {
                string m = marks.FirstOrDefault(kv => Mathf.Abs(kv.Value.x - x) < 0.25f && Mathf.Abs(kv.Value.z - z) < 0.25f).Key;
                if (m != null) { line.Append(m); continue; }
                var o = new Vector3(x, 1.0f, z);
                bool blocked = Physics.CheckSphere(o, 0.2f, ~0, QueryTriggerInteraction.Ignore);
                var h = Physics.RaycastAll(o, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore).Where(q => q.normal.y > 0.8f).OrderBy(q => q.distance).FirstOrDefault();
                line.Append(blocked ? '#' : (h.collider ? '.' : ' '));
            }
            sb.AppendLine(line.ToString());
        }
        sb.AppendLine("x from -10 to 12 step 0.5");
        foreach (var kv in marks) sb.AppendLine($"{kv.Key} = {kv.Value}");
        System.IO.File.WriteAllText("Assets/VRTraining/floor_map.txt", sb.ToString());
        Debug.Log("[VR Training] floor map written");
    }

    static float FloorY(Vector3 p, float fallback)
    {
        var h = Physics.RaycastAll(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore)
                       .Where(x => x.normal.y > 0.8f).OrderBy(x => x.distance).FirstOrDefault();
        return h.collider ? h.point.y : fallback;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
        var b = rs[0].bounds;
        foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }
}
