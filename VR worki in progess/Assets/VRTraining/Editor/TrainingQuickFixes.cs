using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRTraining;

/// <summary>Tools > VR Training > Fix Grabs + Panel Follow + Dummy Floor</summary>
public static class TrainingQuickFixes
{
    [MenuItem("Tools/VR Training/Fix Grabs + Panel Follow + Dummy Floor")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var log = new StringBuilder("[VR Training] Quick fixes:\n");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // ---------- 1. Grabbables: every grab interactable must point at its OWN grabbable + rigidbody
        foreach (var g in all.Select(t => t.GetComponent<Grabbable>()).Where(g => g))
        {
            var rb = g.GetComponent<Rigidbody>();
            foreach (var it in g.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m is HandGrabInteractable || m is GrabInteractable))
            {
                var so = new SerializedObject(it);
                var pe = so.FindProperty("_pointableElement");
                var rp = so.FindProperty("_rigidbody");
                bool wrong = (pe != null && pe.objectReferenceValue != g) || (rp != null && rp.objectReferenceValue != rb);
                if (pe != null) pe.objectReferenceValue = g;
                if (rp != null) rp.objectReferenceValue = rb;
                // No snapping to (possibly offset) hand poses: the object just follows the hand naturally.
                var align = so.FindProperty("_handAligment");
                if (align != null) align.enumValueIndex = 0;   // None
                so.ApplyModifiedProperties();
                if (wrong)
                {
                    Undo.RecordObject(it.transform, "Reset grab point");
                    if (it.transform != g.transform) { it.transform.localPosition = Vector3.zero; it.transform.localRotation = Quaternion.identity; }
                    log.AppendLine($"  FIXED '{it.name}' on '{g.name}': it was pointing at another object");
                }
            }
        }
        log.AppendLine("  all grab interactables now point at their own object; hand alignment set to None (no snapping)");

        // ---------- 2. Instruction panel follows into the medical bay
        var prompt = all.Select(t => t.GetComponent<TrainingPromptUI>()).FirstOrDefault(p => p);
        var spot = all.FirstOrDefault(t => t.name == "PanelSpot_MedBay");
        var t3 = all.Select(t => t.GetComponent<TrainingTask>()).FirstOrDefault(t => t && t.name.StartsWith("Task_03"));
        var table = all.FirstOrDefault(t => t.name == "Wall_Table_01");
        var dummy = all.FirstOrDefault(t => t.name == "Patient_Dummy");
        if (prompt && t3)
        {
            if (!spot)
            {
                var go = new GameObject("PanelSpot_MedBay");
                Undo.RegisterCreatedObjectUndo(go, "spot");
                go.transform.SetParent(t3.transform, false);
                spot = go.transform;
                spot.position = new Vector3(1.5f, 1.9f, 4.7f);
            }
            // A spot left at the world origin was never placed: put it on the far wall of the medical bay.
            if (spot.position.magnitude < 0.5f)
            {
                Undo.RecordObject(spot, "Place spot");
                spot.position = new Vector3(1.6f, 2.0f, 4.9f);
            }
            // Face the room: the reader stands on the -forward side of a UI panel.
            Vector3 room = table && dummy ? (table.position + dummy.position) * 0.5f : spot.position - Vector3.forward;
            Vector3 toRoom = room - spot.position; toRoom.y = 0;
            if (toRoom.sqrMagnitude > 0.01f)
            {
                Undo.RecordObject(spot, "Face room");
                spot.rotation = Quaternion.LookRotation(-toRoom.normalized, Vector3.up);
            }
            var mover = prompt.GetComponent<MoveToSpot>();
            if (!mover) mover = Undo.AddComponent<MoveToSpot>(prompt.gameObject);
            Undo.RecordObject(t3, "Wire panel move");
            for (int i = t3.onTaskStarted.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = t3.onTaskStarted.GetPersistentTarget(i);
                if (target == null || t3.onTaskStarted.GetPersistentMethodName(i) == "MoveTo")
                    UnityEventTools.RemovePersistentListener(t3.onTaskStarted, i);
            }
            UnityEventTools.AddObjectPersistentListener<Transform>(t3.onTaskStarted, mover.MoveTo, spot);
            EditorUtility.SetDirty(t3);
            log.AppendLine($"  panel glides to PanelSpot_MedBay at {spot.position} when Step 3 starts (facing the room)");
        }

        // ---------- 3. Dummy on the floor
        if (dummy)
        {
            var rs = dummy.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            Physics.SyncTransforms();
            var hit = Physics.RaycastAll(new Vector3(b.center.x, b.center.y, b.center.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore)
                             .Where(h => !h.collider.transform.IsChildOf(dummy) && h.normal.y > 0.8f)
                             .OrderBy(h => h.distance).FirstOrDefault();
            if (hit.collider)
            {
                float dy = hit.point.y - b.min.y;
                Undo.RecordObject(dummy, "Dummy on floor");
                dummy.position += Vector3.up * dy;
                foreach (var sib in dummy.parent.Cast<Transform>().Where(s => s.name == "Treatment_TriggerZone" || s.name == "Label"))
                {
                    Undo.RecordObject(sib, "Move with dummy");
                    sib.position += Vector3.up * dy;
                }
                log.AppendLine($"  dummy raised {dy:0.00} m so its feet rest on '{hit.collider.name}' (y={hit.point.y:0.00})");
            }
            else log.AppendLine("  (no floor found under the dummy)");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/VR Training/Debug: Snapshot Medical Bay")]
    public static void SnapshotBay()
    {
        var go = new GameObject("TMP_SnapCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 75f;
        go.transform.position = new Vector3(1.3f, 2.1f, -0.6f);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -0.25f, 1f));
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("medbay_snapshot.png", tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release();
        Debug.Log("[VR Training] medbay_snapshot.png saved in the project folder");
    }

    [MenuItem("Tools/VR Training/Final Tweaks (dummy on tiles, panel faces door, hide extra spray)")]
    public static void FinalTweaks()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var log = new StringBuilder("[VR Training] Final tweaks:\n");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // Dummy: feet on the visible floor tiles (the 'Floor' box collider sits higher than the tiles)
        var dummy = all.FirstOrDefault(t => t.name == "Patient_Dummy");
        if (dummy)
        {
            var rs = dummy.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float tileTop = 0.19f;
            var tile = all.Where(t => t.name.StartsWith("Floor_Squared")).Select(t => t.GetComponentInChildren<Renderer>()).Where(r => r)
                          .FirstOrDefault(r => r.bounds.min.x <= b.center.x && r.bounds.max.x >= b.center.x && r.bounds.min.z <= b.center.z && r.bounds.max.z >= b.center.z);
            if (tile) tileTop = tile.bounds.max.y;
            float dy = (tileTop + 0.003f) - b.min.y;
            Undo.RecordObject(dummy, "Dummy on tiles");
            dummy.position += Vector3.up * dy;
            foreach (var sib in dummy.parent.Cast<Transform>().Where(s => s.name == "Treatment_TriggerZone" || s.name == "Label"))
            { Undo.RecordObject(sib, "Move with dummy"); sib.position += Vector3.up * dy; }
            log.AppendLine($"  dummy moved {dy:0.00} m so its feet touch the floor tiles (y={tileTop:0.00}){(tile ? " on " + tile.name : "")}");
        }

        // Panel spot: on the far wall, facing the entrance (reader stands toward the door)
        var spot = all.FirstOrDefault(t => t.name == "PanelSpot_MedBay");
        if (spot)
        {
            Undo.RecordObject(spot, "Panel spot");
            spot.position = new Vector3(1.2f, 2.2f, 4.95f);
            spot.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            log.AppendLine($"  panel spot at {spot.position}, facing the doorway");
        }

        // Extra spray can inside the spare practice grabbable: hide it so the only spray is the one made by mixing
        foreach (var t in all.Where(t => t.name.StartsWith("Spray_") && !t.IsChildOf(all.First(x => x.name == "Tasks"))))
        {
            if (!t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, "Hide extra spray");
            t.gameObject.SetActive(false);
            log.AppendLine($"  hid extra spray '{t.name}' under '{t.parent.name}'");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }
}
