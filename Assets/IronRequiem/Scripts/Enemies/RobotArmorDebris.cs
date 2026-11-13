using System.Collections.Generic;
using UnityEngine;

namespace IronRequiem
{
    // Cosmetic ballistic debris: no Rigidbody, Collider, damage or forces on characters.
    public sealed class RobotArmorDebris : MonoBehaviour
    {
        MeshRenderer view;
        MeshFilter filter;
        Vector3 velocity, spin, originalScale;
        float age, lifetime;
        int bounces;
        LayerMask groundMask;
        readonly Material[] materials = new Material[2];
        Mesh baked;
        readonly List<Vector3> vertices=new List<Vector3>(5000);
        public bool InUse { get; private set; }
        public static float PersistSeconds = 600f;
        public float Age => age;
        void Awake() { view = GetComponent<MeshRenderer>(); filter = GetComponent<MeshFilter>(); }
        public void Begin(Mesh mesh, Material material, Material interior, Vector3 position, Quaternion rotation, Vector3 scale, float seconds, LayerMask mask)
        {
            filter.sharedMesh = mesh; materials[0] = material; materials[1] = interior; view.sharedMaterials = materials;
            transform.SetPositionAndRotation(position, rotation); transform.localScale = originalScale = scale;
            velocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3f), Random.Range(-1.5f, 1.5f));
            spin = Random.onUnitSphere * Random.Range(80, 220); age = 0; bounces = 0; lifetime = Mathf.Max(.3f, seconds, PersistSeconds); groundMask = mask; // F4: wreckage stays until the sector unloads (pool recycles the oldest).
            InUse = true; gameObject.SetActive(true);
        }
        public void BeginArm(SkinnedMeshRenderer source,Material material,Material interior,float seconds,LayerMask mask)
        {
            if(!baked){baked=new Mesh{name="PooledDetachedArm"};baked.MarkDynamic();}
            source.BakeMesh(baked,false);var center=baked.bounds.center;
            vertices.Clear();baked.GetVertices(vertices);for(int i=0;i<vertices.Count;i++)vertices[i]-=center;
            baked.SetVertices(vertices);baked.RecalculateBounds();
            Begin(baked,material,interior,source.transform.TransformPoint(center),source.transform.rotation,source.transform.lossyScale,seconds,mask);
        }
        void Update()
        {
            float dt = Time.deltaTime; age += dt;
            if (age >= lifetime) { Return(); return; }
            if (bounces < 2)
            {
                velocity += Physics.gravity * dt; Vector3 delta = velocity * dt;
                if (Physics.Raycast(transform.position, delta.normalized, out var hit, delta.magnitude + .015f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * .02f;
                    velocity = Vector3.Reflect(velocity, hit.normal) * .22f; spin *= .3f; bounces++;
                }
                else transform.position += delta;
                transform.Rotate(spin * dt, Space.World);
            }
            transform.localScale = originalScale * Mathf.SmoothStep(0, 1, Mathf.Clamp01((lifetime - age) / .25f));
        }
        public void Return() { InUse = false; velocity = spin = Vector3.zero; materials[0] = materials[1] = null; view.sharedMaterials = materials; filter.sharedMesh = null; gameObject.SetActive(false); }
        void OnDestroy(){if(baked)Destroy(baked);}
    }
}
