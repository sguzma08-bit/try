using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRTraining;

/// <summary>
/// Tools > VR Training > Move Items To Table + Add Keypad Door
///  1. Places the Green Herb, Red Herb and Mix Station on top of Wall_Table_01.
///  2. Turns Intercom_01 into a poke keypad: any 4 digits opens the front door (new Task 7).
/// </summary>
public static class TrainingSceneUpgrade
{
    const string TableName = "Wall_Table_01";
    const string DoorName = "Door_Vert_01";
    const string IntercomName = "Intercom_01";

    [MenuItem("Tools/VR Training/Move Items To Table + Add Keypad Door")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("VR Training upgrade");

        var tasksRoot = all.FirstOrDefault(t => t.name == "Tasks" && t.parent == null);
        var manager = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<TrainingManager>(true)).FirstOrDefault(m => m);
        if (!tasksRoot || !manager)
        {
            EditorUtility.DisplayDialog("VR Training", "Run 'Build Training Scene' first.", "OK");
            return;
        }

        MoveItemsToTable(all);
        AddKeypadDoor(all, tasksRoot, manager);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // ---------------------------------------------------------------- table
    static void MoveItemsToTable(List<Transform> all)
    {
        var table = all.FirstOrDefault(t => t.name == TableName);
        var green = all.FirstOrDefault(t => t.name == "Grabbable_GreenHerb");
        var red = all.FirstOrDefault(t => t.name == "Grabbable_RedHerb");
        var station = all.FirstOrDefault(t => t.name == "MixStation");
        var spray = all.FirstOrDefault(t => t.name == "Grabbable_HealingSpray");
        var spawn = all.FirstOrDefault(t => t.name == "Spray_SpawnPoint");
        if (!table || !green || !red || !station)
        {
            Debug.LogWarning("[VR Training] Table or task items not found - items not moved.");
            return;
        }

        // The table mesh has no collider: give it one so the herbs can rest on it.
        foreach (var mf in table.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.GetComponent<Collider>()) continue;
            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            Debug.Log($"[VR Training] Added MeshCollider to '{mf.name}' so items can rest on the table.");
        }
        var tb = Bounds(table.gameObject);
        bool alongX = tb.size.x >= tb.size.z;
        Vector3 longAxis = alongX ? Vector3.right : Vector3.forward;
        Vector3 shortAxis = alongX ? Vector3.forward : Vector3.right;
        float longLen = alongX ? tb.size.x : tb.size.z;
        float shortLen = alongX ? tb.size.z : tb.size.x;

        // Lean toward the room side (away from the wall): toward the dummy / room center.
        var roomRef = all.FirstOrDefault(t => t.name == "Patient_Dummy");
        float side = 0f;
        if (roomRef) side = Mathf.Sign(Vector3.Dot(roomRef.position - tb.center, shortAxis)) * shortLen * 0.12f;

        var items = new[] { green, station, red };
        var fractions = new[] { -0.3f, 0f, 0.3f };
        Physics.SyncTransforms();
        // Room side of the table along the short axis
        float roomSign = roomRef ? Mathf.Sign(Vector3.Dot(roomRef.position - tb.center, shortAxis)) : 1f;
        for (int i = 0; i < items.Length; i++)
        {
            Vector3? found = null;
            foreach (float f in new[] { fractions[i], fractions[i] * 0.7f, fractions[i] * 1.2f, fractions[i] * 0.4f })
            {
                // walk from the room-side edge toward the wall, casting down from 1.25 m
                for (float k = 0.5f; k >= -0.5f && found == null; k -= 0.04f)
                {
                    var p = tb.center + longAxis * (f * longLen) + shortAxis * (roomSign * k * shortLen);
                    var origin = new Vector3(p.x, 1.25f, p.z);
                    var hit = Physics.RaycastAll(origin, Vector3.down, 1.25f, ~0, QueryTriggerInteraction.Ignore)
                                     .Where(h => !items.Any(it => h.collider.transform.IsChildOf(it)))
                                     .OrderBy(h => h.distance).FirstOrDefault();
                    if (hit.collider && hit.collider.transform.IsChildOf(table) && hit.point.y > 0.5f && hit.point.y < 1.2f && hit.normal.y > 0.85f)
                        found = hit.point - shortAxis * (roomSign * 0.12f);   // a bit in from the edge
                }
                if (found != null) break;
            }
            if (found == null) { Debug.LogWarning($"[VR Training] No table surface found for {items[i].name}."); continue; }
            var target = found.Value;
            var it2 = items[i];
            Undo.RecordObject(it2, "Move to table");
            if (it2 == station) it2.position = target;
            else
            {
                var b = Bounds(it2.gameObject);
                float lift = it2.position.y - b.min.y;
                it2.position = new Vector3(target.x, target.y + lift + 0.005f, target.z);
            }
            Debug.Log($"[VR Training] {it2.name} placed at {it2.position}.");
        }
        if (spray && spawn)
        {
            Undo.RecordObject(spray, "Move spray");
            spray.position = spawn.position;
        }
        Debug.Log($"[VR Training] Moved Green Herb, Mix Station and Red Herb onto '{table.name}'.");
    }

    static float SurfaceY(Vector3 xz, Bounds tb, Transform table, Transform[] ignore)
    {
        var origin = new Vector3(xz.x, tb.max.y + 1f, xz.z);
        var hits = Physics.RaycastAll(origin, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore)
                          .Where(h => h.collider.transform.IsChildOf(table) || h.collider.transform == table)
                          .OrderBy(h => h.distance).ToArray();
        if (hits.Length > 0) return hits[0].point.y;
        return tb.max.y;
    }

    // ---------------------------------------------------------------- keypad + door
    static void AddKeypadDoor(List<Transform> all, Transform tasksRoot, TrainingManager manager)
    {
        if (all.Any(t => t.name == "Task_07_Unlock_Door"))
        {
            Debug.LogWarning("[VR Training] Task_07_Unlock_Door already exists - keypad not added again.");
            return;
        }
        var door = all.FirstOrDefault(t => t.name == DoorName && t.childCount > 0 && t.Find(IntercomName));
        var intercom = door ? door.Find(IntercomName) : all.FirstOrDefault(t => t.name == IntercomName);
        var promptPanel = all.Select(t => t.GetComponent<TrainingPromptUI>()).FirstOrDefault(p => p);
        if (!door || !intercom || !promptPanel)
        {
            EditorUtility.DisplayDialog("VR Training", "Could not find Door_Vert_01 with Intercom_01, or the prompt panel.", "OK");
            return;
        }

        // ---- Task 7
        var taskGO = new GameObject("Task_07_Unlock_Door");
        Undo.RegisterCreatedObjectUndo(taskGO, "Task 7");
        taskGO.transform.SetParent(tasksRoot, false);
        var task = Undo.AddComponent<TrainingTask>(taskGO);
        task.taskTitle = "Unlock the Exit";
        task.instructions = "Walk to the door at the front of the room.\nPoke the keypad (Intercom) with your index finger and enter ANY 4-digit code to open the door.";
        task.completionMessage = "Door unlocked - you may exit.";

        // ---- Keypad panel: clone the working poke canvas so the poke setup is identical
        var keypadGO = Object.Instantiate(promptPanel.gameObject);
        Undo.RegisterCreatedObjectUndo(keypadGO, "Keypad");
        keypadGO.name = "Keypad_PokePanel (Intercom)";
        keypadGO.transform.SetParent(taskGO.transform, true);
        TMP_FontAsset font = null;
        foreach (var mb in keypadGO.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is TrainingPromptUI || mb.GetType().Name == "UmbrellaTerminalScroller") Object.DestroyImmediate(mb);
        }
        foreach (var txt in keypadGO.GetComponentsInChildren<TMP_Text>(true).ToArray())
        {
            if (!font) font = txt.font;
            Object.DestroyImmediate(txt.gameObject);
        }

        var canvasRT = keypadGO.GetComponent<RectTransform>();
        float w = canvasRT.rect.width, h = canvasRT.rect.height;

        // Place it on the intercom, facing into the room.
        var ib = Bounds(intercom.gameObject);
        var roomRef = all.FirstOrDefault(t => t.name == TableName);
        if (!roomRef) roomRef = all.FirstOrDefault(t => t.name == "Patient_Dummy");
        Vector3 toRoom = roomRef ? roomRef.position - ib.center : -intercom.forward;
        toRoom.y = 0;
        var candidates = new[] { intercom.forward, -intercom.forward, intercom.right, -intercom.right }
            .Select(v => new Vector3(v.x, 0, v.z).normalized).Where(v => v.sqrMagnitude > 0.5f).ToArray();
        Vector3 n = candidates.OrderByDescending(v => Vector3.Dot(v, toRoom.normalized)).First();
        float depth = Mathf.Abs(Vector3.Dot(ib.extents, new Vector3(Mathf.Abs(n.x), 0, Mathf.Abs(n.z))));
        float targetWidth = 0.32f;
        float s = targetWidth / w;
        keypadGO.transform.localScale = Vector3.one * s;
        keypadGO.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
        keypadGO.transform.position = ib.center + n * (depth + 0.02f);

        // Display
        var display = NewText(canvasRT, "Display", font, "ENTER CODE:  _ _ _ _", new Vector2(0.04f, 0.70f), new Vector2(0.78f, 0.96f));
        // Buttons: 1-5 / 6-0 and CLR
        string[] digits = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
        var keypad = Undo.AddComponent<PokeKeypad>(keypadGO);
        keypad.display = display;
        var src = keypadGO.GetComponent<AudioSource>();
        if (!src) src = Undo.AddComponent<AudioSource>(keypadGO);
        keypad.audioSource = src;
        keypad.audioSource.playOnAwake = false;
        keypad.audioSource.spatialBlend = 1f;
        keypad.clickSound = LoadClip("sfx_keypadClick");
        keypad.grantedSound = LoadClip("sfx_keypadGranted");

        for (int i = 0; i < digits.Length; i++)
        {
            int row = i / 5, col = i % 5;
            float x0 = 0.04f + col * 0.186f, y1 = 0.64f - row * 0.31f;
            var b = NewButton(canvasRT, "Key_" + digits[i], font, digits[i], new Vector2(x0, y1 - 0.27f), new Vector2(x0 + 0.168f, y1));
            UnityEventTools.AddStringPersistentListener(b.onClick, keypad.PressDigit, digits[i]);
        }
        var clr = NewButton(canvasRT, "Key_Clear", font, "CLR", new Vector2(0.80f, 0.72f), new Vector2(0.96f, 0.94f));
        UnityEventTools.AddPersistentListener(clr.onClick, keypad.Clear);

        // Poke events for the keypad (hover hint / select feedback)
        var poke = keypadGO.GetComponentInChildren<PokeInteractable>(true);
        if (poke)
        {
            var evGO = new GameObject("Events_Poke_Keypad");
            Undo.RegisterCreatedObjectUndo(evGO, "Events");
            evGO.transform.SetParent(taskGO.transform, false);
            var ev = Undo.AddComponent<TrainingInteractionEvents>(evGO);
            ev.interactionType = TrainingInteractionEvents.InteractionType.Poke;
            ev.pointable = poke;
            ev.onlyDuringTask = task;
            UnityEventTools.AddStringPersistentListener(ev.onHover, promptPanel.ShowHint, "Keypad ready - poke the numbers.");
        }

        // ---- Door
        Undo.SetTransformParent(door, taskGO.transform, "Parent door");
        var panel = door.GetComponentsInChildren<Transform>(true)
                        .Where(t => t != door && t.name == DoorName).FirstOrDefault();
        if (!panel)
            panel = door.GetComponentsInChildren<Renderer>(true)
                        .Where(r => !r.transform.IsChildOf(intercom))
                        .OrderByDescending(r => r.bounds.size.sqrMagnitude).Select(r => r.transform).FirstOrDefault();
        var opener = Undo.AddComponent<DoorOpener>(taskGO);
        opener.doorPanel = panel;
        opener.openDistance = panel ? Bounds(panel.gameObject).size.y * 0.95f : 2.2f;
        opener.doorSound = door.GetComponent<AudioSource>();
        opener.disableAnimators = door.GetComponentsInChildren<Animator>(true);

        UnityEventTools.AddPersistentListener(keypad.onUnlocked, opener.Open);
        UnityEventTools.AddPersistentListener(keypad.onUnlocked, task.CompleteTask);

        // ---- Manager
        Undo.RecordObject(manager, "Add task");
        manager.tasks.Add(task);
        manager.completionMessage = "You combined the herbs into a healing spray, stabilized the patient and unlocked the exit.\nWell done, medic.";
        EditorUtility.SetDirty(manager);

        Debug.Log($"[VR Training] Keypad added on '{intercom.name}' (facing {n}), door panel '{(panel ? panel.name : "none")}' opens up {opener.openDistance:0.00} m. Task 7 added.");
        Selection.activeGameObject = keypadGO;
    }

    /// <summary>Moves the keypad (and the intercom it sits on) to a comfortable poke height.</summary>
    [MenuItem("Tools/VR Training/Lower Keypad To Reach Height (1.4 m)")]
    public static void LowerKeypad()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var intercom = all.FirstOrDefault(t => t.name == IntercomName && t.parent && t.parent.name == DoorName);
        if (!keypad) return;
        float floorY = 0f;
        var floor = all.FirstOrDefault(t => t.name == "Floor");
        if (floor) floorY = Bounds(floor.gameObject).max.y;
        float dy = (floorY + 1.4f) - keypad.transform.position.y;
        Undo.RecordObject(keypad.transform, "Lower keypad");
        keypad.transform.position += Vector3.up * dy;
        if (intercom)
        {
            Undo.RecordObject(intercom, "Lower intercom");
            intercom.position += Vector3.up * dy;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[VR Training] Keypad and intercom moved {dy:0.00} m (keypad now at {keypad.transform.position.y:0.00} m, floor at {floorY:0.00}).");
    }

    /// <summary>Puts the keypad flat on the first wall surface between the player start and the intercom.</summary>
    [MenuItem("Tools/VR Training/Snap Keypad To Wall Facing Player")]
    public static void SnapKeypad()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var rig = all.FirstOrDefault(t => t.name.Contains("Camera Rig"));
        if (!keypad || !rig) { Debug.LogWarning("[VR Training] Keypad or camera rig not found."); return; }
        Physics.SyncTransforms();
        var opener = all.Select(t => t.GetComponent<DoorOpener>()).FirstOrDefault(o => o);
        Transform doorPanel = opener ? opener.doorPanel : null;
        var baseTarget = keypad.transform.position;
        var from = new Vector3(rig.position.x, baseTarget.y, rig.position.z);
        var toward = baseTarget - from; toward.y = 0;
        var tangent = Vector3.Cross(Vector3.up, toward.normalized);
        RaycastHit best = default; bool found = false;
        // Try the original spot, then step sideways until we hit wall that is not the moving door.
        for (int i = 0; i <= 24 && !found; i++)
        {
            float off = (i == 0 ? 0 : ((i + 1) / 2) * 0.25f * (i % 2 == 1 ? 1 : -1));
            var target = baseTarget + tangent * off;
            var dir = target - from;
            var hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude + 2f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(h => !h.collider.transform.IsChildOf(keypad.transform))
                              .Where(h => Mathf.Abs(h.normal.y) < 0.3f)
                              .OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0) continue;
            var h0 = hits[0];
            if (doorPanel && h0.collider.transform.IsChildOf(doorPanel)) continue;
            // need a little clear wall around the point (keypad is ~0.32 m wide)
            bool edgeOk = true;
            foreach (float e in new[] { -0.2f, 0.2f })
            {
                var t2 = h0.point + tangent * e - h0.normal * 0.05f;
                var d2 = t2 - from;
                var hh = Physics.RaycastAll(from, d2.normalized, d2.magnitude + 0.5f, ~0, QueryTriggerInteraction.Ignore)
                                .Where(h => !h.collider.transform.IsChildOf(keypad.transform)).OrderBy(h => h.distance).FirstOrDefault();
                if (hh.collider == null || (doorPanel && hh.collider.transform.IsChildOf(doorPanel))) edgeOk = false;
            }
            if (!edgeOk) continue;
            best = h0; found = true;
        }
        if (!found) { Debug.LogWarning("[VR Training] No wall found beside the door."); return; }
        var n = new Vector3(best.normal.x, 0, best.normal.z).normalized;
        Undo.RecordObject(keypad.transform, "Snap keypad");
        keypad.transform.position = best.point + n * 0.015f;
        keypad.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
        var hit = best;
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = keypad.gameObject;
        Debug.Log($"[VR Training] Keypad snapped to '{hit.collider.name}' at {keypad.transform.position}, facing {n}.");
    }

    /// <summary>Mounts the keypad on the room side of the door panel (where the intercom is) so it slides up with the door.</summary>
    [MenuItem("Tools/VR Training/Mount Keypad On Door")]
    public static void MountKeypadOnDoor()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var opener = all.Select(t => t.GetComponent<DoorOpener>()).FirstOrDefault(o => o);
        var rig = all.FirstOrDefault(t => t.name.Contains("Camera Rig"));
        var intercom = all.FirstOrDefault(t => t.name == IntercomName && t.parent && t.parent.name == DoorName);
        if (!keypad || !opener || !opener.doorPanel || !rig) { Debug.LogWarning("[VR Training] Keypad, door or rig not found."); return; }
        var panel = opener.doorPanel;
        var pb = Bounds(panel.gameObject);
        float x = intercom ? intercom.position.x : pb.center.x;
        float z = intercom ? intercom.position.z : pb.center.z;
        var target = new Vector3(x, 1.4f, z);
        var from = new Vector3(rig.position.x, 1.4f, rig.position.z);
        Physics.SyncTransforms();
        var dir = target - from;
        var hit = Physics.RaycastAll(from, dir.normalized, dir.magnitude + 3f, ~0, QueryTriggerInteraction.Ignore)
                         .Where(h => h.collider.transform.IsChildOf(panel)).OrderBy(h => h.distance).FirstOrDefault();
        if (hit.collider == null) { Debug.LogWarning("[VR Training] Could not hit the door panel from the player position."); return; }
        var n = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
        Undo.RecordObject(keypad.transform, "Mount keypad");
        keypad.transform.position = hit.point + n * 0.015f;
        keypad.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
        Undo.SetTransformParent(keypad.transform, panel, "Mount keypad");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = keypad.gameObject;
        Debug.Log($"[VR Training] Keypad mounted on door panel at {keypad.transform.position}, facing {n}. It slides up with the door.");
    }

    [MenuItem("Tools/VR Training/Debug: Describe Table")]
    public static void DescribeTable()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var table = all.FirstOrDefault(t => t.name == TableName);
        if (!table) return;
        var sb = new System.Text.StringBuilder();
        var tb = Bounds(table.gameObject);
        sb.AppendLine($"TABLE bounds center {tb.center} size {tb.size}");
        foreach (var r in table.GetComponentsInChildren<Renderer>(true))
            sb.AppendLine($"  R {r.name} c={r.bounds.center} s={r.bounds.size}");
        foreach (var c in table.GetComponentsInChildren<Collider>(true))
            sb.AppendLine($"  C {c.name} {c.GetType().Name} c={c.bounds.center} s={c.bounds.size}");
        Physics.SyncTransforms();
        for (float dx = -0.5f; dx <= 0.5f; dx += 0.25f)
        for (float dz = -0.5f; dz <= 0.5f; dz += 0.25f)
        {
            var o = new Vector3(tb.center.x + dx * tb.size.x, 2.0f, tb.center.z + dz * tb.size.z);
            var hs = Physics.RaycastAll(o, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).Take(3)
                            .Select(h => $"{h.collider.name}@{h.point.y:0.00}");
            sb.AppendLine($"  ray ({dx:0.00},{dz:0.00}) {o.x:0.00},{o.z:0.00}: " + string.Join(" | ", hs));
        }
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("Assets/VRTraining/table_debug.txt", sb.ToString());
    }

    // ---------------------------------------------------------------- helpers
    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
        var b = rs[0].bounds;
        foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    static AudioClip LoadClip(string name)
    {
        var guid = AssetDatabase.FindAssets(name + " t:AudioClip").FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
    }

    static RectTransform NewRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, name);
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition3D = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, -1f);
        rt.SetAsLastSibling();
        return rt;
    }

    static TMP_Text NewText(Transform parent, string name, TMP_FontAsset font, string text, Vector2 min, Vector2 max)
    {
        var rt = NewRect(parent, name, min, max);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.enableAutoSizing = true;
        t.fontSizeMin = 8; t.fontSizeMax = 200;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = new Color(1f, 0.75f, 0.2f);
        t.raycastTarget = false;
        return t;
    }

    static Button NewButton(Transform parent, string name, TMP_FontAsset font, string label, Vector2 min, Vector2 max)
    {
        var rt = NewRect(parent, name, min, max);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.12f, 0.35f, 0.42f, 0.95f);
        var btn = rt.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.6f, 0.9f, 1f);
        colors.pressedColor = new Color(0.3f, 1f, 0.5f);
        btn.colors = colors;
        var t = NewText(rt, "Label", font, label, Vector2.zero, Vector2.one);
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        return btn;
    }
}
