using System.Collections.Generic;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace IronRequiem
{
    // Presentation only. Health, colliders, navigation, LOD enable flags and destruction remain owned by their systems.
    public sealed class LabRoomVisibility : MonoBehaviour
    {
        [SerializeField] PlayerCharacterController player;
        [SerializeField] bool stacked;
        [SerializeField] float boundary = 29;
        [SerializeField] float upperFloorEntryHeight = 6.8f;
        sealed class View { public Renderer Renderer; public int Room; public bool Hidden, Previous; }
        sealed class Lamp { public Light Light; public int Room; public bool Hidden, Previous; }
        sealed class Actor { public GameObject Root; public int Room; }
        readonly Dictionary<Renderer, View> views = new Dictionary<Renderer, View>();
        readonly Dictionary<Light, Lamp> lamps = new Dictionary<Light, Lamp>();
        readonly List<Actor> actors = new List<Actor>();
        public int CurrentRoom { get; private set; }
        public int SuppressedRenderers { get; private set; }
        public void Configure(PlayerCharacterController target, bool floors) { player = target; stacked = floors; boundary = floors ? 4 : 29; }
        void Start() { CurrentRoom = SelectRoom(); Refresh(); }
        // A leap onto a Big Bot on the lower floor is not entry into the upper room.
        int SelectRoom() => !player ? 1 : ActorRoom(player.transform.position);
        int ActorRoom(Vector3 position) => (stacked ? position.y >= upperFloorEntryHeight : position.z >= boundary) ? 2 : 1;
        void LateUpdate()
        {
            int room = SelectRoom(); if (room != CurrentRoom) { CurrentRoom = room; Refresh(); }
            // Existing AI may pursue back through the door/stairs. Its presentation follows its real room.
            foreach (var actor in actors)
            {
                if (!actor.Root) continue;
                int actual = ActorRoom(actor.Root.transform.position); if (actual == actor.Room) continue;
                actor.Room = actual; foreach (var renderer in actor.Root.GetComponentsInChildren<Renderer>(true)) Add(renderer, actual); Apply();
            }
        }
        public void Track(GameObject actor, int room)
        {
            actors.Add(new Actor { Root = actor, Room = room });
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true)) Add(renderer, room);
            Apply();
        }
        bool Excluded(Renderer renderer)
        {
            if (renderer.transform.IsChildOf(player.transform)) return true;
            var canvas = renderer.GetComponentInParent<Canvas>();
            return canvas && canvas.renderMode != RenderMode.WorldSpace;
        }
        public void Refresh()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (Excluded(renderer)) continue;
                    // Keep passage/stair structures and boundary shells: they prevent holes while crossing.
                    var bounds = renderer.bounds;
                    float low = stacked ? bounds.min.y : bounds.min.z, high = stacked ? bounds.max.y : bounds.max.z;
                    int room = low < boundary && high >= boundary || renderer.name.StartsWith("Stair") || renderer.name.StartsWith("Step") || renderer.name.StartsWith("Sector03") ? 0 : high < boundary ? 1 : 2;
                    var enemy = renderer.GetComponentInParent<Unity.FPS.AI.EnemyController>();
                    if (enemy) room = ActorRoom(enemy.transform.position);
                    Add(renderer, room);
                }
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional || light.transform.IsChildOf(player.transform)) continue;
                    int room = light.name.StartsWith("Stair") ? 0 : (stacked ? light.transform.position.y < 11 : light.transform.position.z < boundary) ? 1 : 2;
                    if (!lamps.TryGetValue(light, out var entry)) { entry = new Lamp { Light = light }; lamps.Add(light, entry); }
                    entry.Room = room;
                }
            }
            Apply();
        }
        void Add(Renderer renderer, int room)
        {
            if (!views.TryGetValue(renderer, out var entry)) { entry = new View { Renderer = renderer }; views.Add(renderer, entry); }
            entry.Room = room;
        }
        void Apply()
        {
            SuppressedRenderers = 0;
            foreach (var entry in views.Values)
            {
                if (!entry.Renderer) continue;
                bool hide = entry.Room != 0 && entry.Room != CurrentRoom;
                if (hide && !entry.Hidden) { entry.Previous = entry.Renderer.forceRenderingOff; entry.Renderer.forceRenderingOff = true; }
                if (!hide && entry.Hidden) entry.Renderer.forceRenderingOff = entry.Previous;
                entry.Hidden = hide; if (hide) SuppressedRenderers++;
            }
            foreach (var entry in lamps.Values)
            {
                if (!entry.Light) continue;
                bool hide = entry.Room != 0 && entry.Room != CurrentRoom;
                if (hide && !entry.Hidden) { entry.Previous = entry.Light.enabled; entry.Light.enabled = false; }
                if (!hide && entry.Hidden) entry.Light.enabled = entry.Previous;
                entry.Hidden = hide;
            }
        }
        void OnDisable()
        {
            foreach (var entry in views.Values) if (entry.Renderer && entry.Hidden) { entry.Renderer.forceRenderingOff = entry.Previous; entry.Hidden = false; }
            foreach (var entry in lamps.Values) if (entry.Light && entry.Hidden) { entry.Light.enabled = entry.Previous; entry.Hidden = false; }
        }
    }
}
