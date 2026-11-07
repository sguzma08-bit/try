using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRTraining;

/// <summary>
/// Tools > VR Training > Add End Screen Menu (Restart / Quit)
/// Tools > VR Training > Debug: Sound Report
/// </summary>
public static class TrainingFinalCheck
{
    static T Find<T>(string name = null) where T : Component =>
        Object.FindObjectsByType<T>(FindObjectsInactive.Include).FirstOrDefault(c => name == null || c.name == name || c.name.StartsWith(name));
    static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var c) ? c : Undo.AddComponent<T>(go);

    static Button MakeButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 min, Vector2 max)
    {
        var existing = parent.Find(name);
        if (existing) return existing.GetComponent<Button>();
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, name);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var b = go.GetComponent<Button>();
        var cb = b.colors;
        cb.normalColor = new Color(0.35f, 0.02f, 0.03f, 0.95f);
        cb.highlightedColor = new Color(0.6f, 0.05f, 0.06f, 1f);
        cb.pressedColor = new Color(0.2f, 0.7f, 0.25f, 1f);
        cb.selectedColor = cb.normalColor;
        b.colors = cb;

        var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        t.layer = go.layer;
        var trt = (RectTransform)t.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (font) tmp.font = font;
        tmp.text = label; tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 4; tmp.fontSizeMax = 200;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold; tmp.raycastTarget = false;
        return b;
    }

    [MenuItem("Tools/VR Training/Add End Screen Menu (Restart, Quit)")]
    public static void AddMenu()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        var log = new StringBuilder("[VR Training] End screen menu:\n");
        var panel = Find<TrainingPromptUI>();
        var ending = Find<EndingSequence>();
        if (!panel || !ending) { Debug.LogError("Need the prompt panel and FX_Ending (run Add Atmosphere first)."); return; }

        var canvas = panel.promptText ? panel.promptText.GetComponentInParent<Canvas>(true) : panel.GetComponentInChildren<Canvas>(true);
        var root = canvas.transform.Find("End_Buttons");
        if (!root)
        {
            var go = new GameObject("End_Buttons", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "End buttons");
            go.layer = canvas.gameObject.layer;
            root = go.transform;
            root.SetParent(canvas.transform, false);
            var rt = (RectTransform)root;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        root.SetAsLastSibling();
        var font = panel.promptText ? panel.promptText.font : null;
        var restart = MakeButton(root, "Button_Restart", "RESTART", font, new Vector2(0.10f, 0.05f), new Vector2(0.45f, 0.19f));
        var quit = MakeButton(root, "Button_Quit", "QUIT", font, new Vector2(0.55f, 0.05f), new Vector2(0.90f, 0.19f));

        var menu = GetOrAdd<EndScreenMenu>(ending.gameObject);
        menu.panel = panel;
        menu.buttonsRoot = root.gameObject;
        menu.head = Camera.main ? Camera.main.transform : null;
        menu.message = "--- TO BE CONTINUED ---\n\n<color=#FFB000>> Poke a button below.</color>";
        var fx = GameObject.Find("FX_Environment");
        menu.hideDuringMenu = fx ? new[] { fx } : new GameObject[0];
        EditorUtility.SetDirty(menu);
        ending.menu = menu;
        EditorUtility.SetDirty(ending);

        void Wire(Button b, UnityEngine.Events.UnityAction a)
        {
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(b.onClick, i);
            UnityEventTools.AddPersistentListener(b.onClick, a);
            EditorUtility.SetDirty(b);
        }
        Wire(restart, menu.Restart);
        Wire(quit, menu.Quit);
        root.gameObject.SetActive(false);
        log.AppendLine($"  RESTART / QUIT buttons added to '{canvas.name}' (hidden until the end)");
        log.AppendLine("  FX_Ending: title -> 3 s -> world goes dark, panel moves in front of you, buttons appear");

        // Restart needs the scene in File > Build Profiles scene list
        var path = EditorSceneManager.GetActiveScene().path;
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == path))
        {
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine($"  added '{path}' to the build scene list (needed for RESTART)");
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(log.ToString());
    }

    static string PathOf(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    [MenuItem("Tools/VR Training/Debug: Sound Report")]
    public static void SoundReport()
    {
        var filled = new List<string>(); var empty = new List<string>(); var noSource = new List<string>();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (!mb || mb.GetType().Namespace != "VRTraining") continue;
            var so = new SerializedObject(mb);
            var it = so.GetIterator();
            bool hasClipField = false, sourceOk = true;
            while (it.NextVisible(true))
            {
                if (it.isArray && it.arrayElementType == "PPtr<$AudioClip>" && it.arraySize == 0)
                { hasClipField = true; empty.Add($"{PathOf(mb.transform)}  >  {mb.GetType().Name}.{it.displayName} (list is empty)"); continue; }
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                string label = $"{PathOf(mb.transform)}  >  {mb.GetType().Name}.{it.displayName}";
                if (it.type == "PPtr<$AudioClip>")
                {
                    hasClipField = true;
                    if (it.objectReferenceValue) filled.Add($"{label} = {it.objectReferenceValue.name}");
                    else if (!it.propertyPath.Contains(".Array.data[")) empty.Add(label);
                }
                else if (it.type == "PPtr<$AudioSource>" && !it.objectReferenceValue && it.name.ToLower().Contains("audio")) sourceOk = false;
            }
            if (hasClipField && !sourceOk) noSource.Add($"{PathOf(mb.transform)} > {mb.GetType().Name}");
        }
        // Lists (Creaks, Bang Clips...) count as filled if they have any element
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            if (src.clip) filled.Add($"{PathOf(src.transform)}  >  AudioSource.clip = {src.clip.name}");

        var sb = new StringBuilder("SOUND REPORT\n\nFILLED:\n");
        foreach (var f in filled.Distinct()) sb.AppendLine("  " + f);
        sb.AppendLine("\nEMPTY (silent until you add a clip):");
        foreach (var e in empty.Distinct()) sb.AppendLine("  " + e);
        sb.AppendLine("\nPROBLEM - has clips but no AudioSource to play them:");
        foreach (var n in noSource.Distinct()) sb.AppendLine("  " + n);
        System.IO.File.WriteAllText("sound_report.txt", sb.ToString());
        Debug.Log($"[VR Training] Sound report: {filled.Distinct().Count()} filled, {empty.Distinct().Count()} empty, {noSource.Distinct().Count()} missing AudioSource. See sound_report.txt");
    }

    [MenuItem("Tools/VR Training/Fix Missing Audio Sources")]
    public static void FixSources()
    {
        int n = 0;
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (!mb || mb.GetType().Namespace != "VRTraining") continue;
            var so = new SerializedObject(mb);
            var prop = so.FindProperty("audioSource");
            if (prop == null || prop.objectReferenceValue) continue;
            var src = GetOrAdd<AudioSource>(mb.gameObject);
            src.playOnAwake = false;
            src.spatialBlend = mb is TrainingPromptUI ? 0.5f : 1f;
            prop.objectReferenceValue = src;
            so.ApplyModifiedProperties();
            Debug.Log($"[VR Training] AudioSource added + linked on {PathOf(mb.transform)} ({mb.GetType().Name})");
            n++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[VR Training] Fixed {n} missing audio sources.");
    }

    [MenuItem("Tools/VR Training/Debug: Snapshot Player View")]
    public static void SnapshotView()
    {
        var head = Camera.main ? Camera.main.transform : null;
        if (!head) { Debug.LogError("No main camera"); return; }
        var go = new GameObject("TMP_ViewCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(head.position, head.rotation);
        cam.fieldOfView = 90; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        var rt = new RenderTexture(1600, 1200, 24); cam.targetTexture = rt; cam.aspect = 1600f / 1200f; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1600, 1200, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); tex.Apply(); RenderTexture.active = null;
        System.IO.File.WriteAllBytes("player_view.png", tex.EncodeToPNG());
        Object.DestroyImmediate(go); rt.Release();
        Debug.Log("[VR Training] player_view.png written");
    }
}
