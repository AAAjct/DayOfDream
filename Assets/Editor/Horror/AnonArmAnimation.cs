using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DayOfDream.Editor
{
    internal static class AnonArmAnimation
    {
        private const string Folder = "Assets/Anims/AnonHouse/";
        private const string Hips = "Anon_Model/____/____/____/_";
        private const string Chest = Hips + "/___/___2/";
        private const string Legs = Hips + "/___ 1/";
        private const int Rate = 40;

        internal static void Apply(GameObject hunter, Animation motion)
        {
            var clone = Object.Instantiate(hunter);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var clips = new AnimationClip[4];
                int index = 0;
                foreach (string name in new[] { "Idle", "Walk", "Run", "Attack" })
                {
                    var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Hunter" +
                        (name == "Attack" ? "Idle" : name) + ".anim");
                    var path = Folder + "Club" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    var generated = name == "Attack" ? new AnimationClip { legacy = true, frameRate = Rate } :
                        Object.Instantiate(source);
                    generated.name = System.IO.Path.GetFileNameWithoutExtension(path);
                    generated.wrapMode = name == "Attack" ? WrapMode.ClampForever : WrapMode.Loop;
                    BakeArms(clone.transform, source, generated, name);
                    if (clip == null) { AssetDatabase.CreateAsset(generated, path); clip = generated; }
                    else { EditorUtility.CopySerialized(generated, clip); Object.DestroyImmediate(generated); }
                    clips[index++] = clip;
                }
                // Legacy Animation serializes renamed AddClip states as scene-local copies.
                var serialized = new SerializedObject(motion);
                var list = serialized.FindProperty("m_Animations");
                list.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                serialized.FindProperty("m_Animation").objectReferenceValue = clips[0];
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally { Object.DestroyImmediate(clone); }
        }

        internal static AnimationClip MenuIdle(GameObject model)
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "HunterIdle.anim");
            var generated = Object.Instantiate(source);
            generated.name = "MenuIdle";
            generated.wrapMode = WrapMode.Loop;
            // Keep the standing pose from frame zero, including the hips that carry both feet.
            foreach (var binding in AnimationUtility.GetCurveBindings(source)
                .Where(binding => binding.type == typeof(Transform) &&
                    (binding.path == Hips || binding.path.StartsWith(Legs, StringComparison.Ordinal))))
            {
                float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(0);
                AnimationUtility.SetEditorCurve(generated, binding, AnimationCurve.Constant(0, source.length, value));
            }
            BakeArms(model.transform, generated, generated, "MenuIdle");
            string path = Folder + "MenuIdle.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, clip);
            Object.DestroyImmediate(generated);
            return clip;
        }

        private static void BakeArms(Transform root, AnimationClip source, AnimationClip clip, string state)
        {
            bool menu = state == "MenuIdle";
            var left = Arm(root.Find(Chest + "__/__"));
            var right = Arm(root.Find(Chest + "__ 1/__"));
            var leftFingers = left[2].GetComponentsInChildren<Transform>().Where(t => t != left[2]).ToArray();
            var rightFingers = right[2].GetComponentsInChildren<Transform>()
                .Where(t => t != right[2] && t.name != "Wooden Club").ToArray();
            var bones = new[] { left[0], left[1], left[2], right[0], right[1], right[2] }
                .Concat(leftFingers).Concat(rightFingers).ToArray();
            if (state == "Attack") bones = bones.Concat(root.GetComponentInChildren<SkinnedMeshRenderer>().bones).Distinct().ToArray();
            var curves = new AnimationCurve[bones.Length, 4];
            for (int i = 0; i < bones.Length; i++)
                for (int axis = 0; axis < 4; axis++) curves[i, axis] = new AnimationCurve();
            var previous = new Quaternion[bones.Length];
            float length = state == "Attack" ? 2.65f : source.length;
            int frames = Mathf.CeilToInt(length * Rate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float phase = (float)frame / frames;
                float time = phase * length;
                source.SampleAnimation(root.gameObject, state == "Attack" ? 0 : time);
                float swing = state == "Idle" ? Mathf.Sin(phase * 2 * Mathf.PI) * .018f :
                    Mathf.Sin(phase * 2 * Mathf.PI) * (state == "Run" ? .19f : .115f);
                Vector3 leftTarget = new(-.245f, .94f, .06f - swing);
                Vector3 rightTarget = new(.26f, 1.03f, .19f + swing * .45f);
                Vector3 club = new(.04f, .38f, .93f);
                if (state == "Attack")
                {
                    // Give the player time to hit the floor, then lift with BOTH hands.
                    float raise = Smooth(time, .60f, 1.42f);
                    float strike = Smooth(time, 1.69f, 1.98f);
                    rightTarget = Vector3.Lerp(new(.16f, 1.10f, .30f), new(.07f, 1.86f, .04f), raise);
                    rightTarget = Vector3.Lerp(rightTarget, new(.07f, .90f, .49f), strike);
                    club = Vector3.Slerp(new(0, .32f, 1), new(0, .90f, -.44f), raise);
                    club = Vector3.Slerp(club, new(0, -.76f, .65f), strike);
                }
                if (menu)
                {
                    HangArm(root, left, -1);
                    HangArm(root, right, 1);
                }
                else
                {
                    Pose(root, right, rightTarget, new(1, -.16f, -.55f));
                    Vector3 shaft = root.TransformDirection(club.normalized);
                    Vector3 finger = Vector3.ProjectOnPlane(right[2].position - right[1].position, shaft).normalized;
                    right[2].rotation = Quaternion.LookRotation(shaft, Vector3.Cross(shaft, finger)) *
                        Quaternion.Inverse(Quaternion.Euler(0, 9, 0));
                    Quaternion leftGrip = right[2].rotation;
                    if (state == "Attack")
                    {
                        var weapon = right[2].Find("Wooden Club");
                        Vector3 grip = weapon.TransformPoint(new Vector3(0, 0, .095f));
                        Vector3 palmOffset = leftGrip * Vector3.Scale(new Vector3(-.026f, -.010f, .002f), left[2].lossyScale);
                        leftTarget = root.InverseTransformPoint(grip - palmOffset);
                    }
                    Pose(root, left, leftTarget, new(-1, -.16f, -.55f));
                    if (state == "Attack") left[2].rotation = leftGrip;
                }
                Curl(right[2], !menu, 1);
                Curl(left[2], state == "Attack", -1);
                for (int i = 0; i < bones.Length; i++)
                {
                    var q = bones[i].localRotation;
                    if (frame > 0 && Quaternion.Dot(previous[i], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    previous[i] = q;
                    for (int axis = 0; axis < 4; axis++) curves[i, axis].AddKey(time, q[axis]);
                }
            }
            var bindings = new EditorCurveBinding[bones.Length * 4];
            var values = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bones.Length; i++)
                for (int axis = 0; axis < 4; axis++)
                {
                    bindings[i * 4 + axis] = EditorCurveBinding.FloatCurve(
                        AnimationUtility.CalculateTransformPath(bones[i], root), typeof(Transform),
                        "m_LocalRotation." + "xyzw"[axis]);
                    values[i * 4 + axis] = curves[i, axis];
                }
            AnimationUtility.SetEditorCurves(clip, bindings, values);
            clip.EnsureQuaternionContinuity();
        }

        private static void Curl(Transform wrist, bool gripping, float mirror)
        {
            foreach (Transform root in wrist)
            {
                if (root.name == "Wooden Club") continue;
                bool thumb = root.name == "____ 1";
                var bone = root;
                for (int joint = 0; joint < 3 && bone != null; joint++)
                {
                    float angle = gripping ? (thumb ? (joint == 0 ? 25 : 42) : joint == 0 ? 38 : 72) : 0;
                    bone.localRotation = Quaternion.Euler(thumb && gripping ? -25 : 0, 0, mirror * angle);
                    bone = bone.childCount > 0 ? bone.GetChild(0) : null;
                }
            }
        }

        private static Transform[] Arm(Transform upper)
        {
            var elbow = upper.Find("___");
            return new[] { upper, elbow, elbow.Find("___ 1") };
        }

        private static float Smooth(float value, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

        private static void HangArm(Transform root, Transform[] arm, float side)
        {
            // Tilt the whole arm out just enough to clear the skirt.
            Vector3 direction = root.TransformDirection(new Vector3(side * .19f, -1, .03f));
            var upper = arm[0];
            var elbow = arm[1];
            var wrist = arm[2];
            upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, direction) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(wrist.position - elbow.position, direction) * elbow.rotation;
        }

        private static void Pose(Transform root, Transform[] arm, Vector3 wrist, Vector3 pole)
        {
            var upper = arm[0]; var elbow = arm[1]; var hand = arm[2];
            Vector3 delta = root.TransformPoint(wrist) - upper.position;
            Vector3 axis = delta.normalized;
            float a = Vector3.Distance(upper.position, elbow.position);
            float b = Vector3.Distance(elbow.position, hand.position);
            float reach = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .01f, a + b - .005f);
            float along = (a * a + reach * reach - b * b) / (2 * reach);
            Vector3 side = Vector3.ProjectOnPlane(root.TransformDirection(pole), axis).normalized;
            Vector3 elbowTarget = upper.position + axis * along + side * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 wristTarget = upper.position + axis * reach;
            upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, elbowTarget - upper.position) * upper.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, wristTarget - elbow.position) * elbow.rotation;
        }
    }
}
