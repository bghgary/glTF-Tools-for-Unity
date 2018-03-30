using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
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
            blendShape = Schema.AnimationChannelTargetPath.weights,
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

        // TODO: ideally new keys should be inserted instead of checking if it can be exported
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
                if (!remainingCurves.All(curve => Mathf.Approximately(curve.keys[keyIndex].time, firstCurve.keys[keyIndex].time)))
                {
                    return false;
                }
            }

            return true;
        }

        private Schema.AnimationSampler ExportAnimationSampler<T>(IEnumerable<AnimationCurve> curves, Func<int, T> getInTangent, Func<int, T> getValue, Func<int, T> getOutTangent, Func<float, T> evaluate, Func<IEnumerable<T>, int> exportData)
        {
            if (!this.settings.BakeAnimations && CanExportCurvesAsSpline(curves))
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
                    Input = this.ExportData(input, minMax: true),
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
                    Input = this.ExportData(input, minMax: true),
                    Interpolation = Schema.AnimationSamplerInterpolation.LINEAR,
                    Output = exportData(output),
                };
            }
        }

        private Schema.AnimationSampler ExportAnimationSamplerPosition(AnimationCurve curveX, AnimationCurve curveY, AnimationCurve curveZ)
        {
            return this.ExportAnimationSampler(
                new[] { curveX, curveY, curveZ },
                keyIndex => GetRightHandedVector(new Vector3(curveX.keys[keyIndex].inTangent, curveY.keys[keyIndex].inTangent, curveZ.keys[keyIndex].inTangent)),
                keyIndex => GetRightHandedVector(new Vector3(curveX.keys[keyIndex].value, curveY.keys[keyIndex].value, curveZ.keys[keyIndex].value)),
                keyIndex => GetRightHandedVector(new Vector3(curveX.keys[keyIndex].outTangent, curveY.keys[keyIndex].outTangent, curveZ.keys[keyIndex].outTangent)),
                time => GetRightHandedVector(new Vector3(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time))),
                values => this.ExportData(values));
        }

        private static Quaternion CreateNormalizedQuaternion(float x, float y, float z, float w)
        {
            var factor = 1.0f / Mathf.Sqrt(x * x + y * y + z * z + w * w);
            return new Quaternion(x * factor, y * factor, z * factor, w * factor);
        }

        private Schema.AnimationSampler ExportAnimationSamplerRotation(AnimationCurve curveX, AnimationCurve curveY, AnimationCurve curveZ, AnimationCurve curveW)
        {
            return this.ExportAnimationSampler(
                new[] { curveX, curveY, curveZ, curveW },
                keyIndex => GetRightHandedQuaternion(new Quaternion(curveX.keys[keyIndex].inTangent, curveY.keys[keyIndex].inTangent, curveZ.keys[keyIndex].inTangent, curveW.keys[keyIndex].inTangent)),
                keyIndex => GetRightHandedQuaternion(CreateNormalizedQuaternion(curveX.keys[keyIndex].value, curveY.keys[keyIndex].value, curveZ.keys[keyIndex].value, curveW.keys[keyIndex].value)),
                keyIndex => GetRightHandedQuaternion(new Quaternion(curveX.keys[keyIndex].outTangent, curveY.keys[keyIndex].outTangent, curveZ.keys[keyIndex].outTangent, curveW.keys[keyIndex].outTangent)),
                time => GetRightHandedQuaternion(CreateNormalizedQuaternion(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time), curveW.Evaluate(time))),
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

        private Schema.AnimationSampler ExportAnimationSamplerWeight(IEnumerable<AnimationCurve> weightCurves)
        {
            return this.ExportAnimationSampler(
                weightCurves,
                keyIndex => weightCurves.Select(curve => curve.keys[keyIndex].inTangent / 100),
                keyIndex => weightCurves.Select(curve => curve.keys[keyIndex].value / 100),
                keyIndex => weightCurves.Select(curve => curve.keys[keyIndex].outTangent / 100),
                time => weightCurves.Select(curve => curve.Evaluate(time) / 100),
                values => this.ExportData(values.SelectMany(value => value)));
        }

        /// <summary>
        /// Groups the editor curve bindings into path/property/member buckets.
        /// </summary>
        private Dictionary<string, Dictionary<string, Dictionary<string, EditorCurveBinding>>> GroupAnimationCurveBindings(IEnumerable<EditorCurveBinding> editorCurveBindings)
        {
            var bindings = new Dictionary<string, Dictionary<string, Dictionary<string, EditorCurveBinding>>>();

            foreach (var editorCurveBinding in editorCurveBindings)
            {
                Dictionary<string, Dictionary<string, EditorCurveBinding>> propertyBindings;
                if (!bindings.TryGetValue(editorCurveBinding.path, out propertyBindings))
                {
                    propertyBindings = new Dictionary<string, Dictionary<string, EditorCurveBinding>>();
                    bindings.Add(editorCurveBinding.path, propertyBindings);
                }

                var split = editorCurveBinding.propertyName.Split(new[] { '.' }, 2);
                var property = split[0];

                Dictionary<string, EditorCurveBinding> memberBindings;
                if (!propertyBindings.TryGetValue(property, out memberBindings))
                {
                    memberBindings = new Dictionary<string, EditorCurveBinding>();
                    propertyBindings.Add(property, memberBindings);
                }

                var member = split[1];
                memberBindings.Add(member, editorCurveBinding);
            }

            return bindings;
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
                        var property = (Property)Enum.Parse(typeof(Property), kvpProperty.Key);
                        var memberCurves = kvpProperty.Value;
                        var targetGameObject = gameObject.transform.Find(path).gameObject;

                        channels.Add(new Schema.AnimationChannel
                        {
                            Sampler = samplers.Count,
                            Target = new Schema.AnimationChannelTarget
                            {
                                Node = this.objectToIndexCache[targetGameObject],
                                Path = (Schema.AnimationChannelTargetPath)property,
                            }
                        });

                        switch (property)
                        {
                            case Property.m_LocalPosition:
                                samplers.Add(this.ExportAnimationSamplerPosition(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["x"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["y"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["z"])));
                                break;

                            case Property.m_LocalScale:
                                samplers.Add(this.ExportAnimationSamplerScale(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["x"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["y"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["z"])));
                                break;

                            case Property.m_LocalRotation:
                                samplers.Add(this.ExportAnimationSamplerRotation(
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["x"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["y"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["z"]),
                                    AnimationUtility.GetEditorCurve(unityAnimationClip, memberCurves["w"])));
                                break;

                            case Property.blendShape:
                                var skinnedMeshRenderer = targetGameObject.GetComponent<SkinnedMeshRenderer>();
                                var sharedMesh = skinnedMeshRenderer.sharedMesh;
                                var weightCurves = new AnimationCurve[sharedMesh.blendShapeCount];
                                for (var i = 0; i < sharedMesh.blendShapeCount; i++)
                                {
                                    var blendShapeName = sharedMesh.GetBlendShapeName(i);

                                    EditorCurveBinding binding;
                                    if (memberCurves.TryGetValue(blendShapeName, out binding))
                                    {
                                        weightCurves[i] = AnimationUtility.GetEditorCurve(unityAnimationClip, binding);
                                    }
                                    else
                                    {
                                        var blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(i);
                                        weightCurves[i] = AnimationCurve.Linear(0, blendShapeWeight, unityAnimationClip.length, blendShapeWeight);
                                    }
                                }
                                samplers.Add(this.ExportAnimationSamplerWeight(weightCurves));
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

        private void ExportAnimations(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                foreach (var animator in gameObject.GetComponentsInChildren<Animator>())
                {
                    var animatorController = (AnimatorController)animator.runtimeAnimatorController;
                    if (animatorController == null)
                    {
                        continue;
                    }

                    foreach (var layer in animatorController.layers)
                    {
                        foreach (var state in layer.stateMachine.states.Select(childState => childState.state))
                        {
                            if (state.motion is BlendTree)
                            {
                                throw new NotSupportedException();
                            }

                            var animationClip = (AnimationClip)state.motion;
                            if (animationClip != null)
                            {
                                this.ExportAnimation(gameObject, animationClip);
                            }
                        }
                    }
                }
            }
        }
    }
}
