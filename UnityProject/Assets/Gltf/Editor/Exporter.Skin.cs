using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gltf.Serialization
{
    internal sealed partial class Exporter
    {
        private static readonly Matrix4x4 InvertZMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
        private static readonly Matrix4x4 InvertZMatrixInverse = InvertZMatrix.inverse;

        private struct Skin
        {
            public Matrix4x4[] BindPoses;
            public Transform RootBone;
            public Transform[] Bones;

            public override bool Equals(object obj) 
            {
                var skin = (Skin)obj;
                return
                    this.BindPoses.SequenceEqual(skin.BindPoses) &&
                    this.RootBone == skin.RootBone &&
                    this.Bones.SequenceEqual(skin.Bones);
            }

            public override int GetHashCode()
            {
                int hashCode = this.RootBone.GetHashCode();
                foreach (var bindPose in this.BindPoses)
                {
                    hashCode ^= bindPose.GetHashCode();
                }
                foreach (var bone in this.Bones)
                {
                    hashCode ^= bone.GetHashCode();
                }
                return hashCode;
            }
        }

        private static Matrix4x4 GetRightHandedMatrix(Matrix4x4 matrix)
        {
            return InvertZMatrixInverse * matrix * InvertZMatrix;
        }

        private int ExportSkin(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            var skin = new Skin
            {
                BindPoses = skinnedMeshRenderer.sharedMesh.bindposes,
                RootBone = skinnedMeshRenderer.rootBone,
                Bones = skinnedMeshRenderer.bones,
            };

            int index;
            if (this.objectToIndexCache.TryGetValue(skin, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportSkin(skinnedMeshRenderer, out index)))
            {
                index = this.skins.Count;

                this.skins.Add(new Schema.Skin
                {
                    InverseBindMatrices = this.ExportData(skin.BindPoses.Select(bindpose => GetRightHandedMatrix(bindpose))),
                    Skeleton = this.objectToIndexCache[skin.RootBone.gameObject],
                    Joints = skin.Bones.Select(bone => this.objectToIndexCache[bone.gameObject]).ToArray(),
                });
            }

            this.ApplyExtensions(extension => extension.PostExportSkin(index, skinnedMeshRenderer));

            this.objectToIndexCache.Add(skin, index);
            return index;
        }

        private void ExportSkins(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var nodeIndex = this.objectToIndexCache[skinnedMeshRenderer.gameObject];

                    if (skinnedMeshRenderer.sharedMesh.bindposes != null && skinnedMeshRenderer.sharedMesh.bindposes.Any() &&
                        skinnedMeshRenderer.rootBone != null ||
                        skinnedMeshRenderer.bones != null && skinnedMeshRenderer.bones.Any())
                    {
                        this.nodes[nodeIndex].Skin = this.ExportSkin(skinnedMeshRenderer);
                    }
                }
            }
        }
    }
}
