using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FartRaft
{
    // One look a seat can wear. Plays the model's idle / walk / attack clips.
    // glTFast imports clips either as legacy clips (Animation component) or as Mecanim clips;
    // both are handled so the import setting does not matter.
    public class CharacterSkin : MonoBehaviour
    {
        public enum Move { Idle, Run, Fart }

        public GameObject model;
        public Transform leftArm;
        public Transform rightArm;
        public AnimationClip[] clips;

        AnimationClip idleClip, runClip, actionClip, current;
        Animation legacy;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationPlayableOutput output;
        float cheerUntil;
        Move move;

        void Awake()
        {
            idleClip = Find("idle");
            runClip = Find("walk") ?? Find("run") ?? idleClip;
            actionClip = Find("attack") ?? runClip;
        }

        void OnEnable()
        {
            current = null;
            Play(Time.time < cheerUntil ? actionClip : ClipFor(move));
        }

        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            current = null;
        }

        AnimationClip Find(string part)
        {
            if (clips == null) return null;
            foreach (var clip in clips)
            {
                if (clip != null && clip.name.ToLowerInvariant().Contains(part)) return clip;
            }
            return null;
        }

        AnimationClip ClipFor(Move m) => m == Move.Fart ? actionClip : m == Move.Run ? runClip : idleClip;

        public void SetMove(Move m)
        {
            move = m;
            if (Time.time >= cheerUntil) Play(ClipFor(m));
        }

        public void Cheer(float duration = 1.2f)
        {
            cheerUntil = Time.time + duration;
            current = null;
            Play(actionClip);
        }

        void Update()
        {
            if (Time.time >= cheerUntil && current == actionClip && move != Move.Fart) Play(ClipFor(move));

            if (playable.IsValid() && current != null && current.length > 0f)
            {
                double t = playable.GetTime();
                if (t > current.length) playable.SetTime(t % current.length);
            }
        }

        void Play(AnimationClip clip)
        {
            if (clip == null || clip == current || model == null || !isActiveAndEnabled) return;
            current = clip;

            if (clip.legacy)
            {
                if (legacy == null) legacy = model.GetComponent<Animation>();
                if (legacy == null) legacy = model.AddComponent<Animation>();
                if (legacy.GetClip(clip.name) == null) legacy.AddClip(clip, clip.name);
                legacy[clip.name].wrapMode = WrapMode.Loop;
                legacy.CrossFade(clip.name, 0.15f);
                return;
            }

            if (!graph.IsValid())
            {
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create(name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            }
            if (playable.IsValid()) playable.Destroy();
            playable = AnimationClipPlayable.Create(graph, clip);
            output.SetSourcePlayable(playable);
            graph.Play();
        }
    }
}
