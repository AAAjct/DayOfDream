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
                var clips = new AnimationClip[6];
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
                BakeExtractions(clone.transform, clips);
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

        internal static void InstallExtractions(GameObject hunter, Animation motion)
        {
            var clone = Object.Instantiate(hunter);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var clips = new AnimationClip[6];
                clips[3] = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "ClubAttack.anim");
                BakeExtractions(clone.transform, clips);
                var serialized = new SerializedObject(motion);
                var list = serialized.FindProperty("m_Animations");
                foreach (var clip in clips.Skip(4))
                {
                    if (Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == clip))
                        continue;
                    int index = list.arraySize++;
                    list.GetArrayElementAtIndex(index).objectReferenceValue = clip;
                }
                serialized.ApplyModifiedProperties();
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static void BakeExtractions(Transform root, AnimationClip[] clips)
        {
            var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "HunterIdle.anim");
            for (int i = 4; i < 6; i++)
            {
                bool bed = i == 4;
                var clip = new AnimationClip
                {
                    name = bed ? "BedExtraction" : "WardrobeExtraction",
                    legacy = true, frameRate = 24, wrapMode = WrapMode.ClampForever
                };
                BakeArms(root, idle, clip, clip.name, clips[3], bed ? 1.38f : 1.48f);
                string path = Folder + clip.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) AssetDatabase.CreateAsset(clip, path);
                else
                {
                    EditorUtility.CopySerialized(clip, existing);
                    Object.DestroyImmediate(clip);
                    clip = existing;
                }
                clips[i] = clip;
            }
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

        private static void BakeArms(Transform root, AnimationClip source, AnimationClip clip, string state,
            AnimationClip strikeClip = null, float extraction = 0)
        {
            bool menu = state == "MenuIdle";
            var left = Arm(root.Find(Chest + "__/__"));
            var right = Arm(root.Find(Chest + "__ 1/__"));
            var leftFingers = left[2].GetComponentsInChildren<Transform>().Where(t => t != left[2]).ToArray();
            var rightFingers = right[2].GetComponentsInChildren<Transform>()
                .Where(t => t != right[2] && t.name != "Wooden Club").ToArray();
            var bones = new[] { left[0], left[1], left[2], right[0], right[1], right[2] }
                .Concat(leftFingers).Concat(rightFingers).ToArray();
            if (state == "Attack" || strikeClip != null)
                bones = bones.Concat(root.GetComponentInChildren<SkinnedMeshRenderer>().bones).Distinct().ToArray();
            var hips = root.Find(Hips);
            var chest = root.Find(Chest.TrimEnd('/'));
            var hipsPosition = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var curves = new AnimationCurve[bones.Length, 4];
            for (int i = 0; i < bones.Length; i++)
                for (int axis = 0; axis < 4; axis++) curves[i, axis] = new AnimationCurve();
            var previous = new Quaternion[bones.Length];
            float length = strikeClip != null ? extraction :
                state == "Attack" ? 2.65f : source.length;
            int frames = Mathf.CeilToInt(length * clip.frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float phase = (float)frame / frames;
                float time = phase * length;
                source.SampleAnimation(root.gameObject, state == "Attack" || strikeClip != null ? 0 : time);
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
                if (strikeClip != null)
                {
                    if (time >= extraction) strikeClip.SampleAnimation(root.gameObject, time - extraction);
                    else PoseExtraction(root, hips, chest, left, right, state == "BedExtraction", time, extraction, strikeClip);
                    for (int axis = 0; axis < 3; axis++) hipsPosition[axis].AddKey(time, hips.localPosition[axis]);
                }
                else if (menu)
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
                if (strikeClip == null)
                {
                    Curl(right[2], !menu, 1);
                    Curl(left[2], state == "Attack", -1);
                }
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
            if (strikeClip != null)
            {
                for (int axis = 0; axis < 3; axis++)
                    clip.SetCurve(Hips, typeof(Transform), "m_LocalPosition." + "xyz"[axis], hipsPosition[axis]);
            }
            clip.EnsureQuaternionContinuity();
        }

        private static void PoseExtraction(Transform root, Transform hips, Transform chest, Transform[] left,
            Transform[] right, bool bed, float time, float duration, AnimationClip strike)
        {
            strike.SampleAnimation(root.gameObject, 0);
            float bend = Smooth(time, .02f, bed ? .48f : .33f) * (1 - Smooth(time, bed ? .88f : 1.06f, duration));
            float yank = Smooth(time, bed ? .54f : .74f, bed ? .95f : 1.14f);
            float reach = Smooth(time, .05f, bed ? .48f : .28f) * (1 - Smooth(time, duration - .30f, duration));
            var strikeLeft = root.InverseTransformPoint(left[2].position);
            var strikeRight = root.InverseTransformPoint(right[2].position);
            var leftRotation = left[2].rotation;
            var rightRotation = right[2].rotation;
            // Fold down to the bed frame. At the wardrobe, reach for the handle before switching to the player.
            chest.rotation = Quaternion.AngleAxis((bed ? 57f : 19f) * bend, root.right) * chest.rotation;
            if (bed)
            {
                // Bend at the waist with planted feet.
                hips.rotation = Quaternion.AngleAxis(18f * bend, root.right) * hips.rotation;
            }
            Vector3 grab = bed ? new(-.10f, .34f, .65f) : new(-.18f, 1.19f, .66f);
            if (!bed)
            {
                float door = Smooth(time, .20f, .57f);
                grab = Vector3.Lerp(grab, new(-.43f, 1.17f, .28f), door);
                grab = Vector3.Lerp(grab, new(-.11f, 1.31f, .67f), Smooth(time, .52f, .74f));
            }
            grab = Vector3.Lerp(grab, new(-.16f, bed ? .72f : 1.02f, .26f), yank);
            Pose(root, left, Vector3.Lerp(strikeLeft, grab, reach), new(-1, -.2f, -.35f));
            Pose(root, right, Vector3.Lerp(strikeRight, new Vector3(.32f, bed ? .78f : .97f, -.04f), reach),
                new(1, -.14f, -.45f));
            left[2].rotation = Quaternion.Slerp(leftRotation, root.rotation * Quaternion.Euler(18, -35, -30), reach);
            right[2].rotation = Quaternion.Slerp(rightRotation, root.rotation * Quaternion.Euler(-36, 18, 0), reach);
            Curl(left[2], time >= (bed ? .48f : .21f), -1);
            Curl(right[2], true, 1);
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
