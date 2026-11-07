using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrainingRoomFix
{
    [MenuItem("Tools/VR Training/Debug: Room Pieces Near Start")]
    public static void Pieces()
    {
        var sb = new StringBuilder();
        var env = GameObject.Find("Environment");
        foreach (Transform g in env.transform)
        {
            sb.AppendLine($"== {g.name} pos {g.position:F3} rot {g.eulerAngles:F1} scale {g.localScale:F2} prefab:{PrefabUtility.IsPartOfPrefabInstance(g)}");
            foreach (Transform c in g)
            {
                var rs = c.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { sb.AppendLine($"   {c.name} (no renderer) {c.position:F2}"); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (g.name == "Prefab_Room_Large_02" && b.max.z < -12) continue;
                sb.AppendLine($"   {c.name}  pos {c.position:F2} rotY {c.eulerAngles.y:F0}  bounds {b.min:F2}..{b.max:F2} cols:{c.GetComponentsInChildren<Collider>(true).Length} active:{c.gameObject.activeInHierarchy}");
            }
        }
        System.IO.File.WriteAllText("room_pieces.txt", sb.ToString());
        Debug.Log("[VR Training] room_pieces.txt written");
    }

    // Top-down shots of the start area: all / old Level_Geometry only / added prefab only
    [MenuItem("Tools/VR Training/Debug: Snapshot Start Area")]
    public static void Shots()
    {
        var env = GameObject.Find("Environment");
        var all = env.GetComponentsInChildren<Renderer>(false).Where(r => r.enabled).ToList();
        Shot(all, "area_all.png");
        Shot(all.Where(r => r.transform.IsChildOf(env.transform.Find("Level_Geometry"))).ToList(), "area_old.png");
        Shot(all.Where(r => r.transform.IsChildOf(env.transform.Find("Prefab_Room_Large_02"))).ToList(), "area_new.png");
        Debug.Log("[VR Training] area shots written");
    }

    static void Shot(System.Collections.Generic.List<Renderer> show, string file)
    {
        var every = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        var off = every.Where(r => r.enabled && (!show.Contains(r) || r.bounds.min.y > 2.3f)).ToList();
        foreach (var r in off) r.enabled = false;
        var go = new GameObject("TMP_Cam"); var cam = go.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 9f;
        cam.transform.position = new Vector3(0, 12, -5); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.magenta; cam.farClipPlane = 30;
        var rt = new RenderTexture(1000, 1000, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1000, 1000, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
        // grid lines every 2 m (white), axes labels: x -9..9, z -14..4
        for (int i = 0; i < 1000; i++) for (float w = -8; w <= 8; w += 2) { int px = Mathf.RoundToInt((w + 9) / 18f * 1000); int pz = Mathf.RoundToInt((w + 9) / 18f * 1000); if (px >= 0 && px < 1000) tex.SetPixel(px, i, Color.white); if (pz >= 0 && pz < 1000) tex.SetPixel(i, pz, Color.white); }
        tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release();
        foreach (var r in off) r.enabled = true;
    }

    // ------------------------------------------------------------------ the actual fix
    static readonly Vector3 OldGridOffset = new Vector3(0.45f, 0.19f, 0.54f);   // added room -> original room grid

    [MenuItem("Tools/VR Training/Fix Added Room (Align + Colliders)")]
    public static void FixRoom()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[VR Training] Fix Added Room:\n");
        var env = GameObject.Find("Environment").transform;
        var room = env.Find("Prefab_Room_Large_02");
        var old = env.Find("Level_Geometry");

        // 1) Snap the added room onto the original 6 m grid (x/z) and floor height (0.19)
        if (room)
        {
            if (Vector3.Distance(room.position, new Vector3(16f, 4f, -1.2f)) < 0.01f)
            {
                Undo.RecordObject(room, "Align room");
                room.position += OldGridOffset;
                log.AppendLine($"  moved Prefab_Room_Large_02 by {OldGridOffset} -> {room.position} (floors now at y 0.19, walls on the same grid as the medical bay)");
            }
            else log.AppendLine($"  room already moved ({room.position}) - left as is");
        }

        // 2) Hide old entry-room pieces that now sit inside / on top of the added room
        string[] dupes = { "Wall_Simple_04_Long", "Wall_Simple_01_Coin", "Wall_Simple_01_InvertCoin", "Roof_Squared_02_6x6", "Roof_Squared_01_6x6", "Wall_Gear_01_Half" };
        if (old)
            foreach (Transform c in old)
                if (dupes.Contains(c.name) && c.gameObject.activeSelf)
                {
                    Undo.RecordObject(c.gameObject, "Hide duplicate");
                    c.gameObject.SetActive(false);
                    log.AppendLine($"  hid old '{c.name}' at {c.position:F2} (overlapped the added room)");
                }

        // 3) Colliders: every visible mesh in the added room gets one, so you can't fall/walk through
        int added = 0;
        if (room)
            foreach (var mr in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponent<Collider>()) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var mc = Undo.AddComponent<MeshCollider>(mr.gameObject);
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
        log.AppendLine($"  added {added} mesh colliders to the added room");

        // 4) Player start on the new floor height
        var rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig && rig.transform.position.y < 0.1f)
        {
            Undo.RecordObject(rig.transform, "Rig height");
            var p = rig.transform.position; rig.transform.position = new Vector3(p.x, 0.19f, p.z);
            log.AppendLine("  player start raised to floor height 0.19");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/VR Training/Fix Added Room: Fill East Floor Gap")]
    public static void FillGap()
    {
        var room = GameObject.Find("Environment").transform.Find("Prefab_Room_Large_02");
        if (room.Find("Floor_Fill_A")) { Debug.Log("[VR Training] gap already filled"); return; }
        // a floor tile right next to the gap (x 2.45..8.45, z -14.66..-8.66)
        Transform src = null;
        foreach (Transform c in room)
        {
            if (c.name != "Roof_Squared_02_6x6") continue;
            var b = c.GetComponentInChildren<Renderer>().bounds;
            if (Mathf.Abs(b.center.x - 5.45f) < 0.3f && Mathf.Abs(b.center.z + 11.66f) < 0.3f) { src = c; break; }
        }
        if (!src) { Debug.LogWarning("[VR Training] source floor tile not found"); return; }
        Undo.IncrementCurrentGroup();
        foreach (var (n, off) in new[] { ("Floor_Fill_A", new Vector3(6, 0, 0)), ("Floor_Fill_B", new Vector3(6, 0, -6)) })
        {
            var copy = Object.Instantiate(src.gameObject, room);
            Undo.RegisterCreatedObjectUndo(copy, "Fill floor");
            copy.name = n;
            copy.transform.position = src.position + off;
            copy.transform.rotation = src.rotation;
            var b = copy.GetComponentInChildren<Renderer>().bounds;
            Debug.Log($"[VR Training] {n} floor tile added: x {b.min.x:F2}..{b.max.x:F2} z {b.min.z:F2}..{b.max.z:F2} top {b.max.y:F2}");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
