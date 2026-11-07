using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using VRTraining;

/// <summary>
/// Tools > VR Training > Build Training Scene
/// Organizes the open scene into Environment / XR Setup / UI / Tasks and wires
/// the herb-to-spray first-aid training with grab, poke and trigger Unity Events.
/// A backup copy of the scene is saved first.
/// </summary>
public static class TrainingSceneBuilder
{
    const string MatFolder = "Assets/VRTraining/Materials";

    [MenuItem("Tools/VR Training/Build Training Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        if (roots.Any(r => r.name == "Tasks"))
        {
            EditorUtility.DisplayDialog("VR Training", "This scene already has a 'Tasks' group. Undo or delete the training groups before building again.", "OK");
            return;
        }

        // ---------- Backup ----------
        if (!string.IsNullOrEmpty(scene.path))
        {
            var dir = Path.GetDirectoryName(scene.path);
            var backup = Path.Combine(dir, Path.GetFileNameWithoutExtension(scene.path) + " BACKUP before training.unity").Replace('\\', '/');
            EditorSceneManager.SaveScene(scene, backup, true);
            Debug.Log($"[VR Training] Backup saved: {backup}");
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build VR Training Scene");

        // ---------- Find existing objects ----------
        var grabbables = roots.SelectMany(r => r.GetComponentsInChildren<Grabbable>(true)).ToList();
        Grabbable green = grabbables.FirstOrDefault(g => HasChild(g.gameObject, "GreenPot") || HasChild(g.gameObject, "Herb_Green"));
        Grabbable red = grabbables.Where(g => g != green && (HasChild(g.gameObject, "RedPot") || HasChild(g.gameObject, "Herb_Red")))
                                  .OrderBy(g => green ? Vector3.Distance(g.transform.position, green.transform.position) : 0f)
                                  .FirstOrDefault();
        if (!green || !red)
        {
            EditorUtility.DisplayDialog("VR Training", "Could not find a grabbable Green Herb and Red Herb (a Grabbable with Herb_GreenPot / Herb_RedPot inside). Nothing was changed.", "OK");
            return;
        }

        // Best template for the spray: a grabbable with both hand + controller grab interactables.
        Grabbable template = grabbables
            .Where(g => g.GetComponentInChildren<HandGrabInteractable>(true) && g.GetComponentInChildren<GrabInteractable>(true))
            .OrderBy(g => HasChild(g.gameObject, "Herb") ? 1 : 0)
            .FirstOrDefault() ?? green;

        GameObject dummy = roots.FirstOrDefault(r => r.name.Contains("Dummy"));
        Canvas canvas = roots.Select(r => r.GetComponent<Canvas>()).FirstOrDefault(c => c && c.GetComponentInChildren<PokeInteractable>(true));
        if (!canvas) canvas = roots.Select(r => r.GetComponentInChildren<Canvas>(true)).FirstOrDefault(c => c && c.GetComponentInChildren<PokeInteractable>(true));
        PokeInteractable poke = canvas ? canvas.GetComponentInChildren<PokeInteractable>(true) : null;

        // ---------- Groups ----------
        var xrGroup = NewGO("XR Setup", null);
        var managerGO = NewGO("Training Manager", null);
        var uiGroup = NewGO("UI", null);
        var tasksGroup = NewGO("Tasks", null);
        var envGroup = NewGO("Environment", null);
        var envLevel = NewGO("Level_Geometry", envGroup.transform);
        var envLight = NewGO("Lighting", envGroup.transform);
        var envProps = NewGO("Props_Extra_Grabbables", envGroup.transform);

        // ---------- UI / prompt panel ----------
        TrainingPromptUI promptUI = null;
        if (canvas)
        {
            Undo.SetTransformParent(canvas.transform, uiGroup.transform, "Parent");
            Undo.RecordObject(canvas.gameObject, "Rename");
            canvas.gameObject.name = "Onboarding_Prompt_Panel (Poke)";
            var text = canvas.GetComponentsInChildren<TMP_Text>(true)
                             .OrderByDescending(t => t.text.Contains("PLACEHOLDER") ? 1 : 0)
                             .ThenByDescending(t => t.rectTransform.rect.width * t.rectTransform.rect.height)
                             .FirstOrDefault();
            promptUI = Undo.AddComponent<TrainingPromptUI>(canvas.gameObject);
            promptUI.promptText = text;
            if (text)
            {
                Undo.RecordObject(text, "Prompt text");
                text.enableAutoSizing = true;
                text.fontSizeMin = 8;
                text.fontSizeMax = Mathf.Max(text.fontSize, 24);
                text.richText = true;
                text.text = "VR FIRST-AID TRAINING";
                DisableTerminalScrollers(text);
            }
        }
        else Debug.LogWarning("[VR Training] No poke canvas found - add a world-space canvas with Poke interaction and assign TrainingPromptUI by hand.");

        // ---------- Tasks ----------
        var t1 = NewTask(tasksGroup, "Task_01_Onboarding_PokeToStart", "Welcome, Field Medic",
            "LEARNING OBJECTIVE: Prepare a healing spray by combining a Green Herb and a Red Herb, then use it to treat an injured patient.\n\n" +
            "CONTROLS\n" +
            "• GRAB (hands): reach toward an object and make a fist or pinch.\n" +
            "• GRAB (controllers): squeeze the GRIP button. Let go to drop.\n" +
            "• POKE: point your index finger and push into this screen.\n\n" +
            "Poke this screen to begin training.",
            "Training started.");
        var t2 = NewTask(tasksGroup, "Task_02_Grab_GreenHerb", "Grab the Green Herb",
            "Reach toward the GREEN herb pot. When your hand is close enough you will see a hint (Hover).\nClose your hand or squeeze GRIP to pick it up (Select).",
            "Green Herb collected.");
        var t3 = NewTask(tasksGroup, "Task_03_Grab_RedHerb", "Grab the Red Herb",
            "Now pick up the RED herb pot.\nTip: you can hold one herb in each hand.",
            "Red Herb collected.");
        var t4 = NewTask(tasksGroup, "Task_04_Combine_Herbs", "Combine the Herbs",
            "Place BOTH the Green Herb and the Red Herb on the glowing MIX STATION.\nWhen both herbs are inside, they combine into a Healing Spray.",
            "Herbs combined into a Healing Spray!");
        var t5 = NewTask(tasksGroup, "Task_05_PickUp_Spray", "Pick Up the Healing Spray",
            "Grab the Healing Spray that appeared on the mix station.",
            "Healing Spray ready.");
        var t6 = NewTask(tasksGroup, "Task_06_Treat_Patient", "Treat the Patient",
            "Hold the spray close to the injured dummy and keep it there for 3 seconds until the status reads STABILIZED.\nIf you let go or move away, the treatment pauses.",
            "Patient stabilized.");

        // Task 1: Poke
        if (poke)
        {
            var ev = NewEvents(t1, "Events_Poke_TerminalScreen", TrainingInteractionEvents.InteractionType.Poke, poke);
            if (promptUI) UnityEventTools.AddStringPersistentListener(ev.onHover, promptUI.ShowHint, "Finger detected - push in to press.");
            UnityEventTools.AddPersistentListener(ev.onSelect, t1.CompleteTask);
        }

        EnsureControllerGrab(green);
        EnsureControllerGrab(red);

        // Task 2: Green herb
        Rename(green.gameObject, "Grabbable_GreenHerb");
        Undo.SetTransformParent(green.transform, t2.transform, "Parent");
        var greenEv = NewEvents(t2, "Events_Grab_GreenHerb", TrainingInteractionEvents.InteractionType.Grab, green);
        if (promptUI) UnityEventTools.AddStringPersistentListener(greenEv.onHover, promptUI.ShowHint, "In range - close your hand / squeeze GRIP to grab.");
        UnityEventTools.AddPersistentListener(greenEv.onSelect, t2.CompleteTask);
        t2.completeIfAlreadySelected = greenEv;

        // Task 3: Red herb
        Rename(red.gameObject, "Grabbable_RedHerb");
        Undo.SetTransformParent(red.transform, t3.transform, "Parent");
        var redEv = NewEvents(t3, "Events_Grab_RedHerb", TrainingInteractionEvents.InteractionType.Grab, red);
        if (promptUI) UnityEventTools.AddStringPersistentListener(redEv.onHover, promptUI.ShowHint, "In range - grab the Red Herb.");
        UnityEventTools.AddPersistentListener(redEv.onSelect, t3.CompleteTask);
        t3.completeIfAlreadySelected = redEv;

        // Task 4: Mix station (trigger)
        var greenB = Bounds(green.gameObject);
        var redB = Bounds(red.gameObject);
        var mid = (green.transform.position + red.transform.position) * 0.5f;
        float tableY = Mathf.Min(greenB.min.y, redB.min.y);
        var station = NewGO("MixStation", t4.transform);
        station.transform.position = new Vector3(mid.x, tableY, mid.z);

        var tray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(tray, "Tray");
        tray.name = "MixStation_Tray (visual)";
        Object.DestroyImmediate(tray.GetComponent<Collider>());
        tray.transform.SetParent(station.transform, false);
        tray.transform.localPosition = new Vector3(0, 0.01f, 0);
        tray.transform.localScale = new Vector3(0.4f, 0.01f, 0.4f);
        tray.GetComponent<Renderer>().sharedMaterial = GlowMaterial("MixStation_Glow", new Color(0.1f, 0.9f, 0.6f));

        var mixZoneGO = NewGO("MixStation_TriggerZone", station.transform);
        mixZoneGO.transform.localPosition = new Vector3(0, 0.15f, 0);
        var mixBox = Undo.AddComponent<BoxCollider>(mixZoneGO);
        mixBox.isTrigger = true;
        mixBox.size = new Vector3(0.45f, 0.3f, 0.45f);
        var mixZone = Undo.AddComponent<TrainingTriggerZone>(mixZoneGO);
        mixZone.acceptedObjects = new List<GameObject> { green.gameObject, red.gameObject };
        mixZone.onlyDuringTask = t4;
        if (promptUI) UnityEventTools.AddStringPersistentListener(mixZone.onTriggerEntered, promptUI.ShowHint, "Herb detected on the mix station.");
        UnityEventTools.AddPersistentListener(mixZone.onAllRequiredInside, t4.CompleteTask);
        t4.completeIfZoneSatisfied = mixZone;

        var spawn = NewGO("Spray_SpawnPoint", station.transform);
        spawn.transform.localPosition = new Vector3(0, 0.12f, 0);

        var label = NewLabel(station.transform, "MIX STATION\n<size=70%>Green + Red Herb</size>", new Vector3(0, 0.35f, 0));

        // Spray (cloned from a working grabbable so the hand + controller grab setup is identical)
        var spray = BuildSpray(template, green, t5.transform);
        spray.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);

        var combiner = Undo.AddComponent<HerbCombiner>(station);
        combiner.ingredients = new[] { green.gameObject, red.gameObject };
        combiner.result = spray;
        combiner.spawnPoint = spawn.transform;
        combiner.zone = mixZone;
        UnityEventTools.AddPersistentListener(t4.onTaskCompleted, combiner.Combine);

        // Task 5: Pick up spray
        var sprayGrab = spray.GetComponent<Grabbable>();
        var sprayEv = NewEvents(t5, "Events_Grab_HealingSpray", TrainingInteractionEvents.InteractionType.Grab, sprayGrab);
        if (promptUI) UnityEventTools.AddStringPersistentListener(sprayEv.onHover, promptUI.ShowHint, "In range - grab the Healing Spray.");
        UnityEventTools.AddPersistentListener(sprayEv.onSelect, t5.CompleteTask);
        t5.completeIfAlreadySelected = sprayEv;

        // Task 6: Treat patient (trigger stay while held)
        var fx = spray.GetComponentInChildren<SprayEffect>(true);
        if (dummy)
        {
            Rename(dummy, "Patient_Dummy");
            Undo.SetTransformParent(dummy.transform, t6.transform, "Parent");
            var db = Bounds(dummy);
            var zoneGO = NewGO("Treatment_TriggerZone", t6.transform);
            zoneGO.transform.position = db.center;
            var box = Undo.AddComponent<BoxCollider>(zoneGO);
            box.isTrigger = true;
            box.size = new Vector3(Mathf.Max(db.size.x, 0.5f) + 0.4f, Mathf.Max(db.size.y, 1.2f), Mathf.Max(db.size.z, 0.4f) + 0.4f);
            var healZone = Undo.AddComponent<TrainingTriggerZone>(zoneGO);
            healZone.acceptedObjects = new List<GameObject> { spray };
            healZone.requireHeld = sprayEv;
            healZone.requiredStaySeconds = 3f;
            healZone.onlyDuringTask = t6;
            if (fx)
            {
                UnityEventTools.AddPersistentListener(healZone.onTriggerEntered, fx.StartSpray);
                UnityEventTools.AddPersistentListener(healZone.onTriggerExited, fx.StopSpray);
                UnityEventTools.AddPersistentListener(t6.onTaskCompleted, fx.StopSpray);
            }
            if (promptUI) UnityEventTools.AddStringPersistentListener(healZone.onTriggerEntered, promptUI.ShowHint, "Spraying... hold steady.");
            UnityEventTools.AddPersistentListener(healZone.onStayTimeReached, t6.CompleteTask);

            var status = Undo.AddComponent<PatientStatus>(t6.gameObject);
            status.treatmentZone = healZone;
            status.tintRenderers = dummy.GetComponentsInChildren<Renderer>(true);
            status.statusLabel = NewLabel(t6.transform, "PATIENT STATUS: CRITICAL", new Vector3(db.center.x, db.max.y + 0.3f, db.center.z), worldSpace: true);
            UnityEventTools.AddPersistentListener(t6.onTaskCompleted, status.Heal);
        }
        else Debug.LogWarning("[VR Training] No object with 'Dummy' in its name was found - Task 6 has no patient.");

        // ---------- Manager ----------
        var manager = Undo.AddComponent<TrainingManager>(managerGO);
        manager.promptUI = promptUI;
        manager.tasks = new List<TrainingTask> { t1, t2, t3, t4, t5, t6 };

        // ---------- Sort remaining roots ----------
        var groups = new HashSet<GameObject> { xrGroup, managerGO, uiGroup, tasksGroup, envGroup };
        foreach (var r in scene.GetRootGameObjects())
        {
            if (groups.Contains(r)) continue;
            if (IsXR(r)) Undo.SetTransformParent(r.transform, xrGroup.transform, "Parent");
            else if (r.GetComponentInChildren<Grabbable>(true)) Undo.SetTransformParent(r.transform, envProps.transform, "Parent");
            else if (r.GetComponent<Canvas>() || r.name.Contains("EventSystem")) Undo.SetTransformParent(r.transform, uiGroup.transform, "Parent");
            else if (r.GetComponent<Light>() || r.GetComponent<ReflectionProbe>() || r.GetComponent<LightProbeGroup>() || r.name.Contains("Light"))
                Undo.SetTransformParent(r.transform, envLight.transform, "Parent");
            else Undo.SetTransformParent(r.transform, envLevel.transform, "Parent");
        }
        foreach (var g in new[] { xrGroup, managerGO, uiGroup, tasksGroup, envGroup }) g.transform.SetAsLastSibling();

        EnsureEventSystem(uiGroup.transform);
        if (promptUI) StretchPromptText(promptUI);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = tasksGroup;
        EditorGUIUtility.PingObject(tasksGroup);
        Debug.Log($"[VR Training] Built: green='{green.name}', red='{red.name}', spray from template '{template.name}', dummy='{(dummy ? dummy.name : "none")}', poke='{(poke ? poke.name : "none")}'. Press Play and use N to skip steps without a headset.");
    }

    [MenuItem("Tools/VR Training/Apply Fixes (EventSystem + Prompt Layout)")]
    public static void ApplyFixes()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var ui = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "UI");
        EnsureEventSystem(ui ? ui.transform : null);
        foreach (var p in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrainingPromptUI>(true)))
            StretchPromptText(p);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[VR Training] Fixes applied.");
    }

    /// <summary>Poke/ray canvases need an EventSystem with Meta's PointableCanvasModule.</summary>
    static void EnsureEventSystem(Transform parent)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var es = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).FirstOrDefault();
        if (!es)
        {
            var go = NewGO("EventSystem", parent);
            es = Undo.AddComponent<UnityEngine.EventSystems.EventSystem>(go);
        }
        if (!es.GetComponent<PointableCanvasModule>())
        {
            Undo.AddComponent<PointableCanvasModule>(es.gameObject);
            Debug.Log($"[VR Training] Added PointableCanvasModule to '{es.name}'.");
        }
    }

    /// <summary>Make the prompt text fill the panel so instructions are readable in VR.</summary>
    static void StretchPromptText(TrainingPromptUI ui)
    {
        var text = ui.promptText;
        if (!text) return;
        Undo.RecordObject(text.rectTransform, "Stretch prompt");
        Undo.RecordObject(text, "Prompt text");
        var canvasRT = ui.GetComponent<RectTransform>();
        var rt = text.rectTransform;
        if (rt.parent != canvasRT) Undo.SetTransformParent(rt, canvasRT, "Parent");
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        rt.anchorMin = new Vector2(0.06f, 0.08f);
        rt.anchorMax = new Vector2(0.94f, 0.92f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition3D = new Vector3(0, 0, rt.anchoredPosition3D.z);
        rt.SetAsLastSibling();
        float h = canvasRT.rect.height * 0.84f;
        text.enableAutoSizing = true;
        text.fontSizeMax = h / 9f;
        text.fontSizeMin = h / 40f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        EditorUtility.SetDirty(text);
    }

    // ================= helpers =================

    static GameObject NewGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        if (parent) go.transform.SetParent(parent, false);
        return go;
    }

    static TrainingTask NewTask(GameObject parent, string goName, string title, string body, string done)
    {
        var go = NewGO(goName, parent.transform);
        var t = Undo.AddComponent<TrainingTask>(go);
        t.taskTitle = title;
        t.instructions = body;
        t.completionMessage = done;
        return t;
    }

    static TrainingInteractionEvents NewEvents(TrainingTask task, string goName, TrainingInteractionEvents.InteractionType type, MonoBehaviour pointable)
    {
        var go = NewGO(goName, task.transform);
        var ev = Undo.AddComponent<TrainingInteractionEvents>(go);
        ev.interactionType = type;
        ev.pointable = pointable;
        ev.onlyDuringTask = task;
        return ev;
    }

    static void Rename(GameObject go, string name)
    {
        Undo.RecordObject(go, "Rename");
        go.name = name;
    }

    static bool HasChild(GameObject go, string part) =>
        go.GetComponentsInChildren<Transform>(true).Any(t => t != go.transform && t.name.Contains(part));

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
        var b = rs[0].bounds;
        foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    static bool IsXR(GameObject r)
    {
        if (r.GetComponentInChildren<Grabbable>(true)) return false;
        string n = r.name;
        return n.Contains("Camera Rig") || n.Contains("CameraRig") || n.Contains("[BuildingBlock]") || n.Contains("Interaction") ||
               n.Contains("Passthrough") || n.Contains("Hands") || n.Contains("OVR") || n.Contains("XR") || n == "TrackingSpace";
    }

    static void DisableTerminalScrollers(TMP_Text target)
    {
        foreach (var mb in EditorSceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
        {
            if (!mb || mb.GetType().Name != "UmbrellaTerminalScroller") continue;
            var f = mb.GetType().GetField("textDisplayField");
            if (f != null && (Object)f.GetValue(mb) == target)
            {
                Undo.RecordObject(mb, "Disable scroller");
                mb.enabled = false;
                Debug.Log($"[VR Training] Disabled UmbrellaTerminalScroller on '{mb.name}' (it wrote to the same text as the prompts).");
            }
        }
    }

    static TextMeshPro NewLabel(Transform parent, string text, Vector3 pos, bool worldSpace = false)
    {
        var go = NewGO("Label", parent);
        if (worldSpace) go.transform.position = pos; else go.transform.localPosition = pos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 0.6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        go.AddComponent<FaceCamera>();
        return tmp;
    }

    static Material GlowMaterial(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder("Assets/VRTraining")) AssetDatabase.CreateFolder("Assets", "VRTraining");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/VRTraining", "Materials");
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        mat.SetColor("_BaseColor", c);
        mat.SetColor("_Color", c);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", c * 1.5f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Herbs made with the hand-grab block only work with hands; add a GrabInteractable so controllers work too.</summary>
    static void EnsureControllerGrab(Grabbable g)
    {
        if (g.GetComponentInChildren<GrabInteractable>(true)) return;
        var rb = g.GetComponent<Rigidbody>();
        var go = NewGO("ControllerGrabInteractable", g.transform);
        var gi = Undo.AddComponent<GrabInteractable>(go);
        var so = new SerializedObject(gi);
        var pe = so.FindProperty("_pointableElement");
        var rbp = so.FindProperty("_rigidbody");
        if (pe != null) pe.objectReferenceValue = g;
        if (rbp != null) rbp.objectReferenceValue = rb;
        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[VR Training] Added controller GrabInteractable to '{g.name}' (pointable={(pe != null)}, rigidbody={(rbp != null)}).");
    }

    static GameObject BuildSpray(Grabbable template, Grabbable sizeReference, Transform parent)
    {
        var refBounds = Bounds(sizeReference.gameObject);
        var spray = Object.Instantiate(template.gameObject);
        Undo.RegisterCreatedObjectUndo(spray, "Spray");
        spray.name = "Grabbable_HealingSpray";
        spray.transform.SetParent(parent, true);
        spray.transform.localScale = template.transform.lossyScale;

        // Remove the template's herb model, keep the grab setup (interactables, hand pose).
        foreach (Transform child in spray.transform.Cast<Transform>().ToArray())
            if (child.name.Contains("Herb") || child.name.Contains("Pot")) Object.DestroyImmediate(child.gameObject);

        // Match the cube's visible state to the herbs (usually the cube mesh is hidden).
        var srcMR = sizeReference.GetComponent<MeshRenderer>();
        var mr = spray.GetComponent<MeshRenderer>();
        if (mr && srcMR) mr.enabled = srcMR.enabled;

        var guid = AssetDatabase.FindAssets("Spray_FatGreen t:Prefab").FirstOrDefault()
                   ?? AssetDatabase.FindAssets("Spray_ t:Prefab").FirstOrDefault();
        if (guid != null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, spray.transform);
            model.name = "Spray_Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Size the can like the herb pot and center it on the grab cube.
            var mb = Bounds(model);
            float k = Mathf.Max(refBounds.size.x, refBounds.size.y, refBounds.size.z) / Mathf.Max(0.0001f, Mathf.Max(mb.size.x, mb.size.y, mb.size.z));
            model.transform.localScale *= k;
            mb = Bounds(model);
            model.transform.position += spray.transform.position - mb.center;

            // Spray particles from the top of the can.
            var fxGO = new GameObject("Spray_ParticleFX");
            fxGO.transform.SetParent(spray.transform, false);
            fxGO.transform.position = new Vector3(mb.center.x, Bounds(model).max.y, mb.center.z);
            fxGO.transform.rotation = Quaternion.LookRotation(Vector3.up);
            var ps = fxGO.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.6f;
            main.startSpeed = 1.2f;
            main.startSize = 0.03f;
            main.startColor = new Color(0.6f, 1f, 0.7f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 80;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 0.005f;
            var pr = fxGO.GetComponent<ParticleSystemRenderer>();
            var pmat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            if (pmat) pr.sharedMaterial = pmat;
            var fx = spray.AddComponent<SprayEffect>();
            fx.particles = ps;
        }
        else Debug.LogWarning("[VR Training] Spray prefab not found - the spray uses the cube only.");

        return spray;
    }
}
