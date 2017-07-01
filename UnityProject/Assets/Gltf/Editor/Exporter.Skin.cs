using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gltf.Serialization
{
    internal sealed partial class Exporter
    {
        private static readonly Matrix4x4 InvertZMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));

        private static Matrix4x4 GetRightHandedMatrix(Matrix4x4 matrix)
        {
            return InvertZMatrix * matrix * InvertZMatrix;
        }

        private int ExportSkin(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            int index;
            if (this.objectToIndexCache.TryGetValue(skinnedMeshRenderer, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportSkin(skinnedMeshRenderer, out index)))
            {
                index = this.skins.Count;
                this.skins.Add(new Schema.Skin
                {
                    InverseBindMatrices = this.ExportData(skinnedMeshRenderer.sharedMesh.bindposes.Select(bindpose => GetRightHandedMatrix(bindpose))),
                    Skeleton = this.objectToIndexCache[skinnedMeshRenderer.rootBone.gameObject],
                    Joints = skinnedMeshRenderer.bones.Select(bone => this.objectToIndexCache[bone.gameObject]).ToArray(),
                });
            }

            this.ApplyExtensions(extension => extension.PostExportSkin(index, skinnedMeshRenderer));

            this.objectToIndexCache.Add(skinnedMeshRenderer, index);
            return index;
        }

        private void ExportSkins(IEnumerable<GameObject> gameObjects)
        {
            foreach (var gameObject in gameObjects)
            {
                foreach (var skinnedMeshRenderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var nodeIndex = this.objectToIndexCache[skinnedMeshRenderer.gameObject];
                    this.nodes[nodeIndex].Skin = this.ExportSkin(skinnedMeshRenderer);
                }
            }
        }
    }
}
