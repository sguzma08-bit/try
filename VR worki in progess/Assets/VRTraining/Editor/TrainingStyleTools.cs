using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRTraining;

/// <summary>Tools > VR Training > Apply Terminal Style + Fix Spray Shape</summary>
public static class TrainingStyleTools
{
    [MenuItem("Tools/VR Training/Apply Terminal Style + Fix Spray Shape")]
    public static void Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

        // ---------- CRT terminal style on the prompt panel and keypad ----------
        foreach (var canvas in all.Select(t => t.GetComponent<Canvas>()).Where(c => c))
        {
            bool isPrompt = canvas.GetComponent<TrainingPromptUI>();
            var keypad = canvas.GetComponent<PokeKeypad>();
            if (!isPrompt && !keypad) continue;
            if (!canvas.GetComponent<TerminalCRTEffect>()) Undo.AddComponent<TerminalCRTEffect>(canvas.gameObject);
            if (keypad)
            {
                foreach (var b in canvas.GetComponentsInChildren<Button>(true))
                {
                    var img = b.GetComponent<Image>();
                    if (img) { Undo.RecordObject(img, "Style"); img.color = new Color(0.22f, 0.03f, 0.05f, 0.95f); }
                    var cb = b.colors;
                    cb.highlightedColor = new Color(1f, 0.55f, 0.55f);
                    cb.pressedColor = new Color(1f, 0.15f, 0.15f);
                    Undo.RecordObject(b, "Style"); b.colors = cb;
                    var lbl = b.GetComponentInChildren<TMP_Text>(true);
                    if (lbl) { Undo.RecordObject(lbl, "Style"); lbl.color = new Color(0.95f, 0.92f, 0.9f); lbl.fontStyle = FontStyles.Bold; }
                }
                if (keypad.display)
                {
                    Undo.RecordObject(keypad.display, "Style");
                    keypad.display.richText = true;
                    keypad.display.color = Color.white;
                }
            }
            Debug.Log($"[VR Training] Terminal style applied to '{canvas.name}'.");
        }

        // ---------- Spray shape ----------
        var spray = all.FirstOrDefault(t => t.name == "Grabbable_HealingSpray");
        var model = spray ? spray.Find("Spray_Model") : null;
        if (spray && model)
        {
            Undo.RecordObject(model, "Fix spray");
            var ps = spray.lossyScale;
            // Uniform world scale of 1 first, then size the can to ~20 cm tall.
            var src = PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) as GameObject;
            var srcRot = src ? src.transform.localRotation : Quaternion.identity;
            var srcScale = src ? src.transform.localScale : Vector3.one;
            model.rotation = srcRot;   // the prefab's own upright orientation, in world space
            model.localScale = new Vector3(srcScale.x / ps.x, srcScale.y / ps.y, srcScale.z / ps.z);
            var b = Bounds(model.gameObject);
            // Stand the can up if its longest side is horizontal.
            if (b.size.y < Mathf.Max(b.size.x, b.size.z) * 0.9f)
            {
                var axis = b.size.x > b.size.z ? Vector3.forward : Vector3.right;
                model.RotateAround(b.center, axis, 90f);
                b = Bounds(model.gameObject);
            }
            float u = 0.2f / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z));
            model.localScale *= u;
            b = Bounds(model.gameObject);
            model.position += spray.position - b.center;
            b = Bounds(model.gameObject);

            // Fit the grab collider to the can.
            var box = spray.GetComponent<BoxCollider>();
            if (box)
            {
                Undo.RecordObject(box, "Fit collider");
                box.center = spray.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x / ps.x, b.size.y / ps.y, b.size.z / ps.z);
            }
            var fx = spray.Find("Spray_ParticleFX");
            if (fx) { Undo.RecordObject(fx, "FX"); fx.position = new Vector3(b.center.x, b.max.y, b.center.z); }
            Debug.Log($"[VR Training] Spray fixed: parent scale {ps}, can size {b.size} (uniform).");
        }
        else Debug.LogWarning("[VR Training] Grabbable_HealingSpray/Spray_Model not found.");

        EditorSceneManager.MarkSceneDirty(scene);
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }
}
