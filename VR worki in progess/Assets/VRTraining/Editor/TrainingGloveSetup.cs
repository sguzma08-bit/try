using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRTraining;
using System.Collections.Generic;

public static class TrainingGloveSetup
{
    [MenuItem("Tools/VR Training/Gloves: 1. Install glTF Importer")]
    public static void InstallGltf()
    {
        UnityEditor.PackageManager.Client.Add("com.unity.cloud.gltfast");
        Debug.Log("[VR Training] Installing com.unity.cloud.gltfast (Unity's glTF importer)... watch the progress bar.");
    }

    [MenuItem("Tools/VR Training/Debug: Hand Info")]
    public static void HandInfo()
    {
        var sb = new StringBuilder("HAND INFO\n");
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } });
        var jid = types.FirstOrDefault(t => t.FullName == "Oculus.Interaction.Input.HandJointId");
        if (jid != null) sb.AppendLine("HandJointId: " + string.Join(", ", Enum.GetNames(jid).Select(n => $"{n}={(int)Enum.Parse(jid, n)}")));
        var hv = types.FirstOrDefault(t => t.Name == "HandVisual" && t.Namespace == "Oculus.Interaction");
        if (hv != null)
        {
            sb.AppendLine("HandVisual members: " + string.Join(", ", hv.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name + ":" + p.PropertyType.Name)));
            sb.AppendLine("HandVisual fields: " + string.Join(", ", hv.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name + ":" + p.FieldType.Name)));
            foreach (var c in UnityEngine.Object.FindObjectsByType(hv, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>())
            {
                sb.AppendLine($"\n== HandVisual at {Path(c.transform)} active:{c.gameObject.activeInHierarchy}");
                var joints = hv.GetProperty("Joints")?.GetValue(c) as System.Collections.IList;
                if (joints != null) for (int i = 0; i < joints.Count; i++) { var t = joints[i] as Transform; sb.AppendLine($"   joint[{i}] = {(t ? t.name + " " + t.position.ToString("F3") : "null")}"); }
                foreach (var r in c.GetComponentsInChildren<Renderer>(true)) sb.AppendLine($"   renderer {r.GetType().Name} {Path(r.transform)} enabled:{r.enabled} mat:{(r.sharedMaterial ? r.sharedMaterial.name : "-")}");
            }
        }
        System.IO.File.WriteAllText("hand_info.txt", sb.ToString());
        Debug.Log("[VR Training] hand_info.txt written");
    }

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    [MenuItem("Tools/VR Training/Debug: Dummy + Bed Info")]
    public static void DummyInfo()
    {
        var sb = new StringBuilder("DUMMY INFO\n");
        var d = GameObject.Find("Patient_Dummy");
        if (d)
        {
            sb.AppendLine($"dummy {Path(d.transform)} pos {d.transform.position:F3} rot {d.transform.eulerAngles:F1} scale {d.transform.localScale:F2}");
            foreach (var c in d.GetComponents<Component>()) sb.AppendLine("  comp " + c.GetType().Name);
            var an = d.GetComponentInChildren<Animator>(true);
            if (an) sb.AppendLine($"  animator on {an.name} enabled:{an.enabled} controller:{(an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "none")} avatar:{(an.avatar ? an.avatar.name + " human:" + an.avatar.isHuman : "none")}");
            var smr = d.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr) { var b = smr.bounds; sb.AppendLine($"  smr {smr.name} bounds {b.min:F3}..{b.max:F3} root {smr.rootBone?.name}"); }
            foreach (var t in d.GetComponentsInChildren<Transform>(true))
                sb.AppendLine($"  bone {t.name} parent {(t.parent ? t.parent.name : "-")} pos {t.position:F3} localRot {t.localEulerAngles:F1} fwd {t.forward:F2} up {t.up:F2} right {t.right:F2}");
            foreach (Transform s2 in d.transform.parent) sb.AppendLine($"  sibling {s2.name} pos {s2.position:F3} rot {s2.eulerAngles:F1} scale {s2.localScale:F2}");
        }
        var bed = GameObject.Find("Wall_Bed_01");
        if (bed)
            foreach (var r in bed.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"bed part {Path(r.transform)} bounds {r.bounds.min:F3}..{r.bounds.max:F3}");
        System.IO.File.WriteAllText("dummy_info.txt", sb.ToString());
        Debug.Log("[VR Training] dummy_info.txt written");
    }

    const string GlovePath = "Assets/VRTraining/model/hands/vr_hand_glove.glb";

    [MenuItem("Tools/VR Training/Gloves: 2. Put Gloves On Hands")]
    public static void PutOn()
    {
        AssetDatabase.ImportAsset(GlovePath, ImportAssetOptions.ForceUpdate);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GlovePath);
        if (!asset) { Debug.LogError("[VR Training] Glove didn't import - is glTFast installed? (Gloves: 1. Install glTF Importer)"); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[VR Training] Gloves:\n");
        var xr = GameObject.Find("XR Setup");
        var parent = xr ? xr.transform.Find("Gloves") : null;
        if (!parent)
        {
            var g = new GameObject("Gloves"); Undo.RegisterCreatedObjectUndo(g, "Gloves");
            if (xr) g.transform.SetParent(xr.transform, false);
            parent = g.transform;
        }
        foreach (var (side, mirror) in new[] { ("Right", false), ("Left", true) })
        {
            var hv = UnityEngine.Object.FindObjectsByType<Oculus.Interaction.HandVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                           .FirstOrDefault(h => h.name == "OVRHandVisual" + side);
            if (!hv) { log.AppendLine($"  (no OVRHandVisual{side})"); continue; }
            var old = parent.Find("Glove_" + side);
            if (old) Undo.DestroyObjectImmediate(old.gameObject);
            var c = new GameObject("Glove_" + side);
            Undo.RegisterCreatedObjectUndo(c, "Glove");
            c.transform.SetParent(parent, false);
            c.transform.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
            var model = (GameObject)UnityEngine.Object.Instantiate(asset, c.transform);
            model.name = "vr_hand_glove";
            var gh = Undo.AddComponent<GloveHand>(c);
            gh.source = hv;
            gh.gloveModel = model.transform;
            gh.hideRenderers = hv.GetComponentsInChildren<SkinnedMeshRenderer>(true).Cast<Renderer>().ToArray();
            int bones = model.GetComponentsInChildren<Transform>(true).Count(t => t.name.Contains("_R_"));
            log.AppendLine($"  Glove_{side}{(mirror ? " (mirrored copy of the right glove)" : "")} follows {hv.name}; {bones} bones; hides {gh.hideRenderers.Length} default hand meshes");
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------------ dummy on the bed
    [MenuItem("Tools/VR Training/Pose Dummy On Med Bay Bed")]
    public static void PoseDummy()
    {
        var log = new StringBuilder("[VR Training] Dummy on bed:\n");
        var d = GameObject.Find("Patient_Dummy");
        var bed = GameObject.Find("Environment")?.transform.Find("Level_Geometry/Wall_Bed_01");
        if (!d || !bed) { Debug.LogError("Patient_Dummy or Level_Geometry/Wall_Bed_01 not found"); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.RegisterFullObjectHierarchyUndo(d.transform.parent.gameObject, "Pose dummy");

        // 1) bed surface: give the bed a collider (it had none) and probe it with rays
        foreach (var mr in bed.GetComponentsInChildren<MeshRenderer>(true))
            if (!mr.GetComponent<Collider>()) { var mc = Undo.AddComponent<MeshCollider>(mr.gameObject); mc.sharedMesh = mr.GetComponent<MeshFilter>().sharedMesh; }
        Physics.SyncTransforms();
        var hits = new List<Vector2>();   // (z, y)
        for (float z = 4.0f; z <= 6.0f; z += 0.04f)
            if (Physics.Raycast(new Vector3(-0.6f, 3.5f, z), Vector3.down, out var h, 4f, ~0, QueryTriggerInteraction.Ignore) && h.collider.transform.IsChildOf(bed))
                hits.Add(new Vector2(z, h.point.y));
        if (hits.Count == 0) { Debug.LogError("Could not find the bed surface"); return; }
        float bedTop = hits.GroupBy(v => Mathf.Round(v.y * 50f) / 50f).OrderByDescending(g => g.Count()).First().Key;
        var surf = hits.Where(v => Mathf.Abs(v.y - bedTop) < 0.04f).ToList();
        float front = surf.Min(v => v.x), back = surf.Max(v => v.x), mid = front + 0.32f;   // body near the room-side edge so the arm can hang off
        log.AppendLine($"  bed surface y {bedTop:F2}, z {front:F2}..{back:F2} (probe: {string.Join(" ", hits.Select(v => $"{v.x:F2}:{v.y:F2}"))})");

        // 2) normal human size, lying on its back, head towards the west wall, face up
        var an = d.GetComponent<Animator>(); if (an) an.enabled = false;
        float feetX = 0.4f;
        d.transform.localScale = Vector3.one;
        d.transform.SetPositionAndRotation(new Vector3(feetX, bedTop + 0.15f, mid), Quaternion.LookRotation(Vector3.up, Vector3.left));

        var bones = d.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
        void Aim(string bone, string child, Vector3 dir)
        {
            if (!bones.TryGetValue(bone, out var b) || !bones.TryGetValue(child, out var c)) return;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
        }
        // left arm (wall side): down along the body
        Aim("B-upperArm.L", "B-forearm.L", new Vector3(0.97f, -0.05f, 0.22f));
        Aim("B-forearm.L", "B-hand.L", new Vector3(0.98f, 0.02f, 0.12f));
        Aim("B-hand.L", "B-middleFinger01.L", new Vector3(1f, -0.1f, 0.05f));
        // right arm (room side): hanging off the edge of the bed
        Aim("B-upperArm.R", "B-forearm.R", new Vector3(0.15f, -0.12f, -1f));
        Aim("B-forearm.R", "B-hand.R", new Vector3(0.05f, -1f, -0.12f));
        Aim("B-hand.R", "B-middleFinger01.R", new Vector3(0.05f, -1f, -0.05f));
        // head rolled towards the room, legs slightly apart
        if (bones.TryGetValue("B-neck", out var neck)) neck.rotation = Quaternion.AngleAxis(-12f, Vector3.right) * neck.rotation;
        if (bones.TryGetValue("B-head", out var head)) head.rotation = Quaternion.AngleAxis(-20f, Vector3.right) * head.rotation;
        Aim("B-thigh.L", "B-shin.L", new Vector3(1f, 0f, 0.08f));
        Aim("B-thigh.R", "B-shin.R", new Vector3(1f, 0f, -0.08f));

        // 3) drop it onto the mattress (ignore the hanging arm when measuring)
        var smr = d.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh(); smr.BakeMesh(baked, true);
        var m = smr.transform.localToWorldMatrix;
        float minY = float.MaxValue; Vector3 bmin = Vector3.one * 1e9f, bmax = -bmin;
        foreach (var v in baked.vertices)
        {
            var w = m.MultiplyPoint3x4(v);
            bmin = Vector3.Min(bmin, w); bmax = Vector3.Max(bmax, w);
            if (w.z > front + 0.05f) minY = Mathf.Min(minY, w.y);
        }
        UnityEngine.Object.DestroyImmediate(baked);
        d.transform.position += Vector3.up * (bedTop + 0.01f - minY);
        log.AppendLine($"  dummy lying: x {bmin.x:F2}..{bmax.x:F2}, z {bmin.z:F2}..{bmax.z:F2}, back on the mattress at y {bedTop:F2}");

        // 4) move the treatment zone, label and monitor with it
        var parent = d.transform.parent;
        float cx = feetX - 0.95f;
        var zone = parent.Find("Treatment_TriggerZone");
        if (zone)
        {
            zone.SetPositionAndRotation(new Vector3(cx, bedTop + 0.35f, mid - 0.1f), Quaternion.identity);
            var box = zone.GetComponent<BoxCollider>();
            if (box) { Undo.RecordObject(box, "zone"); box.center = Vector3.zero; box.size = new Vector3(2.3f, 1.0f, 1.6f); }
            log.AppendLine($"  treatment zone moved over the body {(box ? "(box 2.3 x 1.0 x 1.6 m)" : "")}");
        }
        var label = parent.Find("Label");
        if (label) label.position = new Vector3(cx, bedTop + 1.0f, mid - 0.3f);
        var mon = parent.Find("Patient_Monitor_Audio");
        if (mon) mon.position = new Vector3(feetX - 1.9f, bedTop + 0.6f, back);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("dummy_pose.txt", log.ToString());
    }

    [MenuItem("Tools/VR Training/Debug: Snapshot Dummy Closeup")]
    public static void DummyShot()
    {
        var d = GameObject.Find("Patient_Dummy");
        var smr = d.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh(); smr.BakeMesh(baked, true);
        var m = smr.transform.localToWorldMatrix; var c = Vector3.zero;
        foreach (var v in baked.vertices) c += m.MultiplyPoint3x4(v);
        c /= baked.vertexCount; UnityEngine.Object.DestroyImmediate(baked);
        var go = new GameObject("TMP_Cam"); var cam = go.AddComponent<Camera>();
        cam.transform.position = c + new Vector3(0.6f, 0.7f, -1.5f);
        cam.transform.LookAt(c);
        cam.fieldOfView = 55; cam.nearClipPlane = 0.02f;
        var rt = new RenderTexture(1280, 800, 24); cam.targetTexture = rt; cam.aspect = 1.6f; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes("dummy_closeup.png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(go); rt.Release();
        Debug.Log("[VR Training] dummy_closeup.png written");
    }
}
