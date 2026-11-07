using System.Collections.Generic;
using UnityEngine;

namespace VRTraining
{
    /// <summary>
    /// Leaves green spray residue on the dummy where the spray is pointed while treating.
    /// A hidden collider is baked from the dummy's mesh so the residue lands on the body surface.
    /// </summary>
    public class SprayMist : MonoBehaviour
    {
        public Transform dummy;
        public Transform nozzle;                       // Spray_ParticleFX (points out of the can)
        public TrainingTriggerZone treatmentZone;
        public TrainingInteractionEvents sprayHeld;    // only while the spray is held
        public Color mistColor = new Color(0.45f, 1f, 0.5f, 0.55f);
        public float splatSize = 0.12f;
        public float secondsBetweenSplats = 0.12f;
        public int maxSplats = 60;

        readonly List<Transform> _splats = new List<Transform>();
        Material _mat;
        float _next;
        int _maskLayer = 2;   // "Ignore Raycast": not hit by other raycasts in the scene

        void Start()
        {
            if (!dummy) return;
            foreach (var smr in dummy.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked);
                var go = new GameObject("Mist_Collider");
                go.layer = _maskLayer;
                go.transform.SetParent(smr.transform, false);
                go.AddComponent<MeshCollider>().sharedMesh = baked;
            }
            _mat = new Material(Shader.Find("Sprites/Default"));
            _mat.mainTexture = SoftDot();
        }

        void Update()
        {
            if (!nozzle || !treatmentZone || !treatmentZone.IsAnythingInside) return;
            if (sprayHeld && !sprayHeld.IsSelected) return;
            if (Time.time < _next || _splats.Count >= maxSplats) return;
            _next = Time.time + secondsBetweenSplats;

            int mask = 1 << _maskLayer;
            var dir = nozzle.forward + Random.insideUnitSphere * 0.15f;
            if (!Physics.Raycast(nozzle.position, dir, out var hit, 1.5f, mask, QueryTriggerInteraction.Ignore))
            {
                var toBody = dummy.position + Vector3.up * 1.2f - nozzle.position;
                if (!Physics.Raycast(nozzle.position, toBody + Random.insideUnitSphere * 0.2f, out hit, 2f, mask, QueryTriggerInteraction.Ignore)) return;
            }
            Splat(hit);
        }

        void Splat(RaycastHit hit)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Mist_Splat";
            q.transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f)));
            q.transform.SetParent(hit.collider.transform, true);
            var parentScale = q.transform.parent.lossyScale;
            float s = splatSize * Random.Range(0.6f, 1.3f);
            q.transform.localScale = new Vector3(s / parentScale.x, s / parentScale.y, s / parentScale.z);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            var c = mistColor; c.a *= Random.Range(0.5f, 1f);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
            _splats.Add(q.transform);
        }

        static Texture2D SoftDot()
        {
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.25f, y * 0.25f));
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            return t;
        }
    }
}
