using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Tools > VR Training > Debug: Room Audit - writes room_report.txt + room_top.png in the project folder.</summary>
public static class TrainingRoomAudit
{
    static readonly string[] NotRoom = { "XR Setup", "UI", "Tasks", "Training Manager", "EventSystem" };

    public static List<Renderer> RoomRenderers() =>
        EditorSceneManager.GetActiveScene().GetRootGameObjects()
            .Where(r => !NotRoom.Contains(r.name))
            .SelectMany(r => r.GetComponentsInChildren<Renderer>(true))
            .Where(r => (r is MeshRenderer) && r.enabled && r.gameObject.activeInHierarchy)
            .ToList();

    public static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    [MenuItem("Tools/VR Training/Debug: Room Audit")]
    public static void Audit()
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder("ROOM AUDIT\n\nROOTS:\n");
        foreach (var r in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            sb.AppendLine($"  {r.name} children:{r.transform.childCount} active:{r.activeSelf}");

        var rs = RoomRenderers();
        var all = rs[0].bounds; foreach (var r in rs) all.Encapsulate(r.bounds);
        sb.AppendLine($"\nRoom bounds {all.min} .. {all.max}, renderers {rs.Count}");

        // group by second-level parent
        sb.AppendLine("\nGROUPS (first 2 levels):");
        foreach (var g in rs.GroupBy(r => { var p = Path(r.transform).Split('/'); return string.Join("/", p.Take(Mathf.Min(2, p.Length))); }).OrderBy(g => g.Key))
        {
            var b = g.First().bounds; foreach (var r in g) b.Encapsulate(r.bounds);
            int noCol = g.Count(r => !r.GetComponent<Collider>());
            sb.AppendLine($"  {g.Key}: {g.Count()} meshes, {noCol} without collider, bounds {b.min:F2}..{b.max:F2}");
        }

        sb.AppendLine("\nFLOOR-LIKE PIECES (flat, > 0.5 m2):");
        foreach (var r in rs.Where(r => r.bounds.size.y < 0.35f && r.bounds.size.x * r.bounds.size.z > 0.5f && r.bounds.max.y < 1.2f).OrderBy(r => r.bounds.center.x).ThenBy(r => r.bounds.center.z))
            sb.AppendLine($"  top y {r.bounds.max.y:F3}  x {r.bounds.min.x:F2}..{r.bounds.max.x:F2} z {r.bounds.min.z:F2}..{r.bounds.max.z:F2}  col:{(r.GetComponent<Collider>() ? r.GetComponent<Collider>().GetType().Name : "NONE")}  {Path(r.transform)}");

        sb.AppendLine("\nCOLLIDERS IN ROOM (non-renderer or big):");
        foreach (var c in EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(r => !NotRoom.Contains(r.name)).SelectMany(r => r.GetComponentsInChildren<Collider>(true)).Where(c => c.enabled && !c.GetComponent<Renderer>()))
            sb.AppendLine($"  {c.GetType().Name} {c.bounds.min:F2}..{c.bounds.max:F2} trigger:{c.isTrigger} {Path(c.transform)}");

        // map
        var floors = rs.Where(r => r.bounds.size.y < 0.35f && r.bounds.max.y < 1.2f).ToList();
        sb.AppendLine("\nMAP x->right z up. '.' walkable collider floor, 'v' visible floor but NO collider, '#' blocked at 1m (wall), ' ' nothing, R rig, D door, M dummy");
        var rig = GameObject.Find("[BuildingBlock] Camera Rig"); var door = GameObject.Find("Door_Vert_01"); var dm = GameObject.Find("Patient_Dummy");
        float x0 = Mathf.Floor(all.min.x) - 1, x1 = Mathf.Ceil(all.max.x) + 1, z0 = Mathf.Floor(all.min.z) - 1, z1 = Mathf.Ceil(all.max.z) + 1;
        float step = Mathf.Max(0.5f, Mathf.Max(x1 - x0, z1 - z0) / 70f);
        for (float z = z1; z >= z0; z -= step)
        {
            var line = new StringBuilder($"{z,6:0.0} ");
            for (float x = x0; x <= x1; x += step)
            {
                bool near(GameObject g) => g && Mathf.Abs(g.transform.position.x - x) < step / 2 && Mathf.Abs(g.transform.position.z - z) < step / 2;
                if (near(rig)) { line.Append('R'); continue; }
                if (near(door)) { line.Append('D'); continue; }
                if (near(dm)) { line.Append('M'); continue; }
                var o = new Vector3(x, 1.0f, z);
                bool blocked = Physics.CheckSphere(o, 0.2f, ~0, QueryTriggerInteraction.Ignore);
                var h = Physics.RaycastAll(o + Vector3.up * 0.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore).Where(q => q.normal.y > 0.7f).OrderBy(q => q.distance).FirstOrDefault();
                bool vis = floors.Any(f => f.bounds.min.x <= x && f.bounds.max.x >= x && f.bounds.min.z <= z && f.bounds.max.z >= z);
                line.Append(blocked ? '#' : h.collider ? '.' : vis ? 'v' : ' ');
            }
            sb.AppendLine(line.ToString());
        }
        sb.AppendLine($"x from {x0} step {step:0.00}");
        System.IO.File.WriteAllText("room_report.txt", sb.ToString());

        // top-down picture (ceilings hidden)
        var hidden = rs.Where(r => r.bounds.min.y > 2.3f).ToList();
        foreach (var r in hidden) r.enabled = false;
        var go = new GameObject("TMP_TopCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(all.size.x, all.size.z) * 0.55f;
        cam.transform.position = new Vector3(all.center.x, all.max.y + 5, all.center.z);
        cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        cam.farClipPlane = all.size.y + 20; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.magenta;
        var rt = new RenderTexture(1400, 1400, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1400, 1400, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1400, 1400), 0, 0); tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes("room_top.png", tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release();
        foreach (var r in hidden) r.enabled = true;
        Debug.Log("[VR Training] Room audit written: room_report.txt, room_top.png (magenta = nothing there)");
    }
}
