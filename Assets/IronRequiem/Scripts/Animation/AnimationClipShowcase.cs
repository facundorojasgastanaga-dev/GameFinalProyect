using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace IronRequiem
{
    // Showcase/test only: plays one clip on an Animator through a PlayableGraph (no controller needed).
    // Looping clips loop; one-shot clips replay after a short pause so recoil and hit reactions can be watched.
    [DisallowMultipleComponent]
    public sealed class AnimationClipShowcase : MonoBehaviour
    {
        [SerializeField] Animator target;
        [SerializeField] AnimationClip clip;
        [SerializeField, Min(0)] float pauseBetween = .8f;
        [SerializeField, Range(.1f, 2f)] float speed = 1f;

        PlayableGraph graph;
        AnimationClipPlayable playable;
        bool oneShot;

        public void Configure(Animator animator, AnimationClip animation) { target = animator; clip = animation; }
        public float Speed { get => speed; set => speed = Mathf.Clamp(value, .1f, 2f); }

        void OnEnable()
        {
            if (!target || !clip) return;
            graph = PlayableGraph.Create(name + "_Showcase");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph, "Showcase", target).SetSourcePlayable(playable);
            oneShot = !clip.isLooping;
            graph.Play();
        }

        void Update()
        {
            if (!graph.IsValid()) return;
            playable.SetSpeed(speed);
            if (oneShot && playable.GetTime() >= clip.length + pauseBetween * speed) playable.SetTime(0);
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
