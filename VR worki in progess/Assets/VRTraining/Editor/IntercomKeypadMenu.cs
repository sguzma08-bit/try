using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRTraining;

public static class IntercomKeypadMenu
{
    [MenuItem("Tools/VR Training/Use Intercom Keys As Keypad (debug outlines)")]
    public static void SetupDebug() => Setup(true);

    [MenuItem("Tools/VR Training/Use Intercom Keys As Keypad (final, invisible)")]
    public static void SetupFinal() => Setup(false);

    static void Setup(bool debug)
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var keypad = all.Select(t => t.GetComponent<PokeKeypad>()).FirstOrDefault(k => k);
        var intercom = all.FirstOrDefault(t => t.name == "Intercom_01" && t.GetComponentsInChildren<Renderer>(true).Length > 0 && t.parent && (t.parent.name == "Door_Vert_01"));
        var blocker = all.Select(t => t.GetComponent<DoorBlocker>()).FirstOrDefault(b => b);
        if (!keypad || !intercom) { Debug.LogWarning("[VR Training] keypad or Intercom_01 not found"); return; }
        // The intercom is on the door leaf: make it slide up with the door when it opens.
        var opener = all.Select(t => t.GetComponent<DoorOpener>()).FirstOrDefault(o => o);
        if (opener && opener.doorPanel && !intercom.IsChildOf(opener.doorPanel))
            Undo.SetTransformParent(intercom, opener.doorPanel, "Intercom on door");
        var layout = keypad.GetComponent<IntercomKeypadLayout>();
        if (!layout) layout = Undo.AddComponent<IntercomKeypadLayout>(keypad.gameObject);
        layout.intercom = intercom;
        layout.keypad = keypad;
        if (blocker) layout.intoWall = blocker.roomDirection;
        layout.showKeyOutlines = debug;
        // Measured from the intercom model (Debug: Snapshot Intercom)
        layout.keyGrid = new Rect(0.173f, 0.101f, 0.410f, 0.292f);
        layout.displayArea = new Rect(0.16f, 0.68f, 0.68f, 0.20f);
        layout.keyFill = 0.82f;
        layout.Apply();
        Selection.activeGameObject = keypad.gameObject;
        EditorSceneManager.MarkSceneDirty(keypad.gameObject.scene);
    }

    [MenuItem("Tools/VR Training/Debug: Snapshot Intercom")]
    public static void Snapshot()
    {
        var all = EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var layout = all.Select(t => t.GetComponent<IntercomKeypadLayout>()).FirstOrDefault(l => l);
        if (!layout || !layout.intercom) return;
        var rs = layout.intercom.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer).ToArray();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var fwd = layout.intoWall; fwd.y = 0; fwd.Normalize();
        var right = Vector3.Cross(Vector3.up, fwd);
        float faceW = 2f * Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z))));
        float faceH = b.size.y;
        float depth = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(fwd.x), 0, Mathf.Abs(fwd.z))));
        var go = new GameObject("TMP_SnapshotCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        float size = Mathf.Max(faceH, faceW) * 0.5f * 1.05f;
        cam.orthographicSize = size;
        cam.nearClipPlane = 0.01f; cam.farClipPlane = 3f;
        go.transform.position = b.center - fwd * (depth + 0.5f);
        go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        var rt = new RenderTexture(1024, 1024, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("intercom_snapshot.png", tex.EncodeToPNG());
        float ppm = 1024f / (2f * size);
        float left = 512 - faceW * 0.5f * ppm, rightPx = 512 + faceW * 0.5f * ppm;
        float top = 512 - faceH * 0.5f * ppm, bottom = 512 + faceH * 0.5f * ppm;
        System.IO.File.WriteAllText("intercom_snapshot.txt", $"face px: left {left:0} right {rightPx:0} top {top:0} bottom {bottom:0} (image y down)\nkeyGrid {layout.keyGrid}\ndisplay {layout.displayArea}");
        Object.DestroyImmediate(go);
        rt.Release();
        Debug.Log("[VR Training] Intercom snapshot saved to project folder: intercom_snapshot.png");
    }
}
