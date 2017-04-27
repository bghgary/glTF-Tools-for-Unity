using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gltf.Serialization
{
    internal sealed partial class Exporter
    {
        private enum Property
        {
            m_LocalPosition = Schema.AnimationChannelTargetPath.translation,
            m_LocalRotation = Schema.AnimationChannelTargetPath.rotation,
            m_LocalScale = Schema.AnimationChannelTargetPath.scale,
        }

        private enum Member
        {
            x,
            y,
            z,
            w,
        }

        private static Vector3 GetRightHandedPosition(float x, float y, float z)
        {
            return new Vector3(x, y, -z);
        }

        private static Quaternion GetRightHandedRotation(float x, float y, float z, float w)
        {
            return new Quaternion(x, y, -z, -w);
        }

        private static bool CanExportTangentModeAsSpline(AnimationUtility.TangentMode tangentMode)
        {
            switch (tangentMode)
            {
                case AnimationUtility.TangentMode.Auto:
                case AnimationUtility.TangentMode.ClampedAuto:
                case AnimationUtility.TangentMode.Free:
                    return true;
            }

            return false;
        }

        private static bool CanExportCurvesAsSpline(IEnumerable<AnimationCurve> curves)
        {
            var firstCurve = curves.First();
            var remainingCurves = curves.Skip(1);

            // All curves must have the same number of keys.
            if (!remainingCurves.All(curve => curve.keys.Length == firstCurve.keys.Length))
            {
                return false;
            }

            for (int keyIndex = 0; keyIndex < firstCurve.keys.Length; keyIndex++)
            {
                // All curves must have the same time values.
                if (!remainingCurves.All(curve => curve.keys[keyIndex].time == firstCurve.keys[keyIndex].time))
                {
                    return false;
                }
            }

            return true;
        }

        private Schema.AnimationSampler ExportAnimationSampler<T>(IEnumerable<AnimationCurve> curves, Func<int, T> getInTangent, Func<int, T> getValue, Func<int, T> getOutTangent, Func<float, T> evaluate, Func<IEnumerable<T>, int> exportData)
        {
            if (CanExportCurvesAsSpline(curves))
            {
                var firstCurve = curves.First();

                var input = new float[firstCurve.keys.Length];
                var output = new T[firstCurve.keys.Length * 3];
                for (int keyIndex = 0; keyIndex < firstCurve.keys.Length; keyIndex++)
                {
                    input[keyIndex] = firstCurve.keys[keyIndex].time;

                    output[keyIndex * 3 + 0] = getInTangent(keyIndex);
                    output[keyIndex * 3 + 1] = getValue(keyIndex);
                    output[keyIndex * 3 + 2] = getOutTangent(keyIndex);
                }

                return new Schema.AnimationSampler
                {
                    Input = this.ExportData(input),
                    Interpolation = Schema.AnimationSamplerInterpolation.CUBICSPLINE,
                    Output = exportData(output),
                };
            }
            else
            {
                var input = new List<float>();
                var output = new List<T>();
                var maxTime = curves.Max(curve => curve.keys.Last().time);
                for (float time = 0; time < maxTime; time += 1.0f / 30.0f)
                {
                    input.Add(time);
                    output.Add(evaluate(time));
                }

                return new Schema.AnimationSampler
                {
                    Input = this.ExportData(input),
                    Interpolation = Schema.AnimationSamplerInterpolation.LINEAR,
                    Output = exportData(output),
                };
            }
        }

        private Schema.AnimationSampler ExportAnimationSamplerPosition(AnimationCurve curveX, AnimationCurve curveY, AnimationCurve curveZ)
        {
            return this.ExportAnimationSampler(
                new[] { curveX, curveY, curveZ },
                keyIndex => GetRightHandedPosition(curveX.keys[keyIndex].inTangent, curveY.keys[keyIndex].inTangent, curveZ.keys[keyIndex].inTangent),
                keyIndex => GetRightHandedPosition(curveX.keys[keyIndex].value, curveY.keys[keyIndex].value, curveZ.keys[keyIndex].value),
                keyIndex => GetRightHandedPosition(curveX.keys[keyIndex].outTangent, curveY.keys[keyIndex].outTangent, curveZ.keys[keyIndex].outTangent),
                time => GetRightHandedPosition(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time)),
                values => this.ExportData(values));
        }

        private Schema.AnimationSampler ExportAnimationSamplerRotation(AnimationCurve curveX, AnimationCurve curveY, AnimationCurve curveZ, AnimationCurve curveW)
        {
            return this.ExportAnimationSampler(
                new[] { curveX, curveY, curveZ, curveW },
                keyIndex => GetRightHandedRotation(curveX.keys[keyIndex].inTangent, curveY.keys[keyIndex].inTangent, curveZ.keys[keyIndex].inTangent, curveW.keys[keyIndex].inTangent),
                keyIndex => GetRightHandedRotation(curveX.keys[keyIndex].value, curveY.keys[keyIndex].value, curveZ.keys[keyIndex].value, curveW.keys[keyIndex].value),
                keyIndex => GetRightHandedRotation(curveX.keys[keyIndex].outTangent, curveY.keys[keyIndex].outTangent, curveZ.keys[keyIndex].outTangent, curveW.keys[keyIndex].outTangent),
                time => GetRightHandedRotation(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time), curveW.Evaluate(time)),
                values => this.ExportData(values));
        }

        private Schema.AnimationSampler ExportAnimationSamplerScale(AnimationCurve curveX, AnimationCurve curveY, AnimationCurve curveZ)
        {
            return this.ExportAnimationSampler(
                new[] { curveX, curveY, curveZ },
                keyIndex => new Vector3(curveX.keys[keyIndex].inTangent, curveY.keys[keyIndex].inTangent, curveZ.keys[keyIndex].inTangent),
                keyIndex => new Vector3(curveX.keys[keyIndex].value, curveY.keys[keyIndex].value, curveZ.keys[keyIndex].value),
                keyIndex => new Vector3(curveX.keys[keyIndex].outTangent, curveY.keys[keyIndex].outTangent, curveZ.keys[keyIndex].outTangent),
                time => new Vector3(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time)),
                values => this.ExportData(values));
        }

        /// <summary>
        /// Groups the editor curve bindings into path/property/member buckets.
        /// </summary>
        private Dictionary<string, Dictionary<Property, Dictionary<Member, EditorCurveBinding>>> GroupAnimationCurveBindings(IEnumerable<EditorCurveBinding> editorCurveBindings)
        {
            var bindings = new Dictionary<string, Dictionary<Property, Dictionary<Member, EditorCurveBinding>>>();

            foreach (var editorCurveBinding in editorCurveBindings)
            {
                Dictionary<Property, Dictionary<Member, EditorCurveBinding>> propertyBindings;
                if (!bindings.TryGetValue(editorCurveBinding.path, out propertyBindings))
                {
                    propertyBindings = new Dictionary<Property, Dictionary<Member, EditorCurveBinding>>();
                    bindings.Add(editorCurveBinding.path, propertyBindings);
                }

                var split = editorCurveBinding.propertyName.Split('.');
                var property = (Property)Enum.Parse(typeof(Property), split[0]);

                Dictionary<Member, EditorCurveBinding> memberBindings;
                if (!propertyBindings.TryGetValue(property, out memberBindings))
                {
                    memberBindings = new Dictionary<Member, EditorCurveBinding>();
                    propertyBindings.Add(property, memberBindings);
                }

                var member = (Member)Enum.Parse(typeof(Member), split[1]);
                memberBindings.Add(member, editorCurveBinding);
            }

            return bindings;
        }

        private void ExportAnimations(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                foreach (var animation in gameObject.GetComponentsInChildren<Animation>())
                {
                    foreach (AnimationState animationState in animation)
                    {
                        this.ExportAnimation(gameObject, animationState.clip);
                    }
                }
            }
        }

        private int ExportAnimation(GameObject gameObject, AnimationClip unityAnimationClip)
        {
            int index;
            if (this.objectToIndexCache.TryGetValue(unityAnimationClip, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportAnimation(gameObject, unityAnimationClip, out index)))
            {
                var channels = new List<Schema.AnimationChannel>();
                var samplers = new List<Schema.AnimationSampler>();

                foreach (var kvpPath in this.GroupAnimationCurveBindings(AnimationUtility.GetCurveBindings(unityAnimationClip)))
                {
                    var path = kvpPath.Key;
                    var propertyCurves = kvpPath.Value;

                    foreach (var kvpProperty in propertyCurves)
                    {
                        var property = kvpProperty.Key;
                        var memberCurves = kvpProperty.Value;

                        channels.Add(new Schema.AnimationChannel
                        {
                            Sampler = samplers.Count,
                            Target = new Schema.AnimationChannelTarget
                            {
                                Node = this.objectToIndexCache[gameObject.transform.Find(path).gameObject],
                                Path = (Schema.AnimationChannelTargetPath)property,
                            }
                        });

                        switch (property)
                        {
                            case Property.m_LocalPosition:
                                samplers.Add(this.ExportAnimationSamplerPosition(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.x]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.y]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.z])));
                                break;

                            case Property.m_LocalScale:
                                samplers.Add(this.ExportAnimationSamplerScale(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.x]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.y]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.z])));
                                break;

                            case Property.m_LocalRotation:
                                samplers.Add(this.ExportAnimationSamplerRotation(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.x]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.y]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.z]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves[Member.w])));
                                break;

                            default:
                                throw new NotSupportedException();
                        }
                    }
                }

                index = this.animations.Count;
                this.animations.Add(new Schema.Animation
                {
                    Name = unityAnimationClip.name,
                    Channels = channels,
                    Samplers = samplers,
                });
            }

            this.ApplyExtensions(extension => extension.PostExportAnimation(index, gameObject, unityAnimationClip));

            this.objectToIndexCache.Add(unityAnimationClip, index);
            return index;
        }
    }
}
