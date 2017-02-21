using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gltf.Serialization
{
    [Flags]
    public enum Extensions
    {
        None = 0x00000000,
        KHR_materials_pbrSpecularGlossiness = 0x00000001,
    }

    public class ExportWindow : EditorWindow
    {
        private static class PrefKeys
        {
            public const string OutputDirectory = "Gltf.Serialization.ExportWindow.OutputDirectory";
            public const string OutputBinary = "Gltf.Serialization.ExportWindow.OutputBinary";
            public const string JsonFormatting = "Gltf.Serialization.ExportWindow.JsonFormatting";
            public const string ShowExtensions = "Gltf.Serialization.ExportWindow.ShowExtension";
            public const string Extension_KHR_materials_pbrSpecularGlossiness = "Gltf.Serialization.ExportWindow.Extension.KHR_materials_pbrSpecularGlossiness";
        }

        private string outputDirectory;
        private bool outputBinary;
        private Formatting jsonFormatting;
        private bool showExtensions;
        private bool extension_KHR_materials_pbrSpecularGlossiness;

        [MenuItem("Assets/glTF Tools for Unity/Export")]
        public static new void Show()
        {
            GetWindow(typeof(ExportWindow), true, "glTF Tools for Unity - Export").ShowUtility();
        }

        private void Awake()
        {
            this.outputDirectory = EditorPrefs.GetString(PrefKeys.OutputDirectory, "Output");
            this.outputBinary = EditorPrefs.GetBool(PrefKeys.OutputBinary, false);
            this.jsonFormatting = (Formatting)EditorPrefs.GetInt(PrefKeys.JsonFormatting, (int)Formatting.Indented);
            this.showExtensions = EditorPrefs.GetBool(PrefKeys.ShowExtensions, true);
            this.extension_KHR_materials_pbrSpecularGlossiness = EditorPrefs.GetBool(PrefKeys.Extension_KHR_materials_pbrSpecularGlossiness, false);
        }

        private void OnDestroy()
        {
            EditorPrefs.SetString(PrefKeys.OutputDirectory, this.outputDirectory);
            EditorPrefs.SetBool(PrefKeys.OutputBinary, this.outputBinary);
            EditorPrefs.SetInt(PrefKeys.JsonFormatting, (int)this.jsonFormatting);
            EditorPrefs.SetBool(PrefKeys.ShowExtensions, this.showExtensions);
            EditorPrefs.SetBool(PrefKeys.Extension_KHR_materials_pbrSpecularGlossiness, this.extension_KHR_materials_pbrSpecularGlossiness);
        }

        private void OnGUI()
        {
            this.outputDirectory = EditorGUILayout.TextField("Output Directory", this.outputDirectory);
            this.outputBinary = EditorGUILayout.Toggle("Output Binary", this.outputBinary);
            this.jsonFormatting = (Formatting)EditorGUILayout.EnumPopup("JSON Formatting", this.jsonFormatting);

            EditorGUILayout.Separator();

            this.showExtensions = EditorGUILayout.Foldout(this.showExtensions, "Extensions");
            if (this.showExtensions)
            {
                EditorGUI.indentLevel++;
                this.extension_KHR_materials_pbrSpecularGlossiness = EditorGUILayout.ToggleLeft("KHR_materials_pbrSpecularGlossiness", this.extension_KHR_materials_pbrSpecularGlossiness);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.TextArea(string.Empty, GUI.skin.horizontalSlider);

            if (GUILayout.Button("Export", GUILayout.ExpandWidth(false)))
            {
                var extensions = Extensions.None;
                if (this.extension_KHR_materials_pbrSpecularGlossiness)
                {
                    extensions |= Extensions.KHR_materials_pbrSpecularGlossiness;
                }

                Selection.gameObjects.ForEach(gameObject =>
                {
                    new[] { gameObject }.Export(this.outputDirectory, gameObject.name, this.outputBinary, this.jsonFormatting, extensions);
                    Debug.LogFormat(gameObject, "[{0}] Exported {1}", DateTime.Now, gameObject.name);
                });
            }
        }
    }

    public static class GameObjectExtensions
    {
        public static void Export(this IEnumerable<GameObject> inputObjects, string outputDirectory, string outputName, bool outputBinary, Formatting jsonFormatting, Extensions extensions)
        {
            var exporter = new Exporter();
            exporter.Export(inputObjects, outputDirectory, outputName, outputBinary, jsonFormatting, extensions);
        }
    }

    internal sealed partial class Exporter
    {
        private enum BinaryContentFormat
        {
            JSON = 0,
        }

        private string outputDirectory;
        private string outputName;
        private bool outputBinary;
        private BinaryWriter binaryBodyWriter;
        private Dictionary<UnityEngine.Object, int> objectToIndexCache;
        private List<Extension> extensions;

        private List<Schema.Accessor> accessors;
        private List<Schema.Buffer> buffers;
        private List<string> extensionsUsed;
        private List<Schema.BufferView> bufferViews;
        private List<Schema.Image> images;
        private List<Schema.Mesh> meshes;
        private List<Schema.Material> materials;
        private List<Schema.Node> nodes;
        private List<Schema.Sampler> samplers;
        private List<Schema.Texture> textures;

        public void Export(IEnumerable<GameObject> inputObjects, string outputDirectory, string outputName, bool outputBinary, Formatting jsonFormatting, Extensions extensions)
        {
            this.outputDirectory = outputDirectory;
            this.outputName = outputName;
            this.outputBinary = outputBinary;

            this.accessors = new List<Schema.Accessor>();
            this.buffers = new List<Schema.Buffer>();
            this.extensionsUsed = new List<string>();
            this.bufferViews = new List<Schema.BufferView>();
            this.images = new List<Schema.Image>();
            this.meshes = new List<Schema.Mesh>();
            this.materials = new List<Schema.Material>();
            this.nodes = new List<Schema.Node>();
            this.samplers = new List<Schema.Sampler>();
            this.textures = new List<Schema.Texture>();

            if (this.outputBinary)
            {
                this.binaryBodyWriter = new BinaryWriter(new MemoryStream());
            }

            Directory.CreateDirectory(this.outputDirectory);

            this.objectToIndexCache = new Dictionary<UnityEngine.Object, int>();

            this.extensions = new List<Extension>();
            if ((extensions & Extensions.KHR_materials_pbrSpecularGlossiness) != 0)
            {
                this.extensions.Add(new KHR_materials_pbrSpecularGlossiness(this));
            };

            var scenes = new[]
            {
                new Schema.Scene
                {
                    Nodes = inputObjects.Select(inputObject => this.ExportNode(inputObject)).ToArray(),
                }
            };

            var gltf = new Schema.Gltf
            {
                Accessors = this.accessors,
                Asset = new Schema.Asset { Version = "2.0" },
                BufferViews = this.bufferViews,
                Buffers = this.buffers,
                ExtensionsUsed = this.extensionsUsed,
                Images = this.images,
                Meshes = this.meshes,
                Materials = this.materials,
                Nodes = this.nodes,
                Samplers = this.samplers,
                Scene = 0,
                Scenes = scenes,
                Textures = this.textures,
            };

            var jsonSerializer = new JsonSerializer
            {
                Formatting = jsonFormatting,
                ContractResolver = new CamelCasePropertyNamesContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = false
                    }
                }
            };

            if (outputBinary)
            {
                this.binaryBodyWriter.Flush();
                var binaryBodyBytes = ((MemoryStream)this.binaryBodyWriter.BaseStream).ToArray();

                Debug.Assert(this.buffers.Count == 0);
                this.ExportBuffer(null, binaryBodyBytes.Length);

                byte[] binaryContentBytes;
                using (var binaryContentWriter = new StreamWriter(new MemoryStream()))
                {
                    jsonSerializer.Serialize(binaryContentWriter, gltf);
                    binaryContentWriter.Flush();

                    // Start of binary data must be 4-byte aligned, so must pad scene json with spaces.
                    binaryContentWriter.Write(new string(' ', (int)(4 - (binaryContentWriter.BaseStream.Position % 4))));
                    binaryContentWriter.Flush();

                    binaryContentBytes = ((MemoryStream)binaryContentWriter.BaseStream).ToArray();
                }

                using (var fileStream = new FileStream(Path.Combine(this.outputDirectory, this.outputName + ".glb"), FileMode.Create))
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    // 20-byte header
                    byte[] magic = Encoding.ASCII.GetBytes("glTF");
                    uint version = 1;
                    uint length = checked((uint)(20 + binaryContentBytes.Length + binaryBodyBytes.Length));
                    uint contentLength = checked((uint)binaryContentBytes.Length);
                    uint contentFormat = (uint)BinaryContentFormat.JSON;

                    binaryWriter.Write(magic);
                    binaryWriter.Write(version);
                    binaryWriter.Write(length);
                    binaryWriter.Write(contentLength);
                    binaryWriter.Write(contentFormat);
                    binaryWriter.Write(binaryContentBytes);
                    binaryWriter.Write(binaryBodyBytes);

                    binaryWriter.Flush();
                    fileStream.Flush();
                }
            }
            else
            {
                using (var streamWriter = new StreamWriter(Path.Combine(this.outputDirectory, this.outputName + ".gltf")))
                {
                    jsonSerializer.Serialize(streamWriter, gltf);
                    streamWriter.Flush();
                }
            }
        }

        private void GetRightHandedTRS(Transform transform, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            var invertZMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
            var transformMatrix = invertZMatrix * Matrix4x4.TRS(transform.localPosition, transform.localRotation, transform.localScale) * invertZMatrix;

            position = transformMatrix.GetColumn(3);
            rotation = Quaternion.LookRotation(transformMatrix.GetColumn(2), transformMatrix.GetColumn(1));
            scale = new Vector3(transformMatrix.GetColumn(0).magnitude, transformMatrix.GetColumn(1).magnitude, transformMatrix.GetColumn(2).magnitude);
        }

        private int ExportNode(GameObject gameObject)
        {
            int index = -1;
            if (this.objectToIndexCache.TryGetValue(gameObject, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportNode(gameObject, out index)))
            {
                var transform = gameObject.transform;
                var children = transform.Cast<Transform>().Where(childTransform => childTransform.gameObject.activeSelf).Select(childTransform => childTransform.gameObject);

                Vector3 position;
                Quaternion rotation;
                Vector3 scale;
                GetRightHandedTRS(transform, out position, out rotation, out scale);

                var node = new Schema.Node
                {
                    Name = gameObject.name,
                    Children = children.Select(child => this.ExportNode(child)).ToArray(),
                    Translation = new[] { position.x, position.y, position.z },
                    Rotation = new[] { rotation.x, rotation.y, rotation.z, rotation.w },
                    Scale = new[] { scale.x, scale.y, scale.z },
                };

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var unityMaterial = renderer.sharedMaterial;
                    var materialIndex = this.ExportMaterial(unityMaterial);

                    var meshFilter = gameObject.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        var unityMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                        if (unityMesh.triangles.Any())
                        {
                            node.Mesh = this.ExportMesh(unityMesh, materialIndex);
                        }
                    }
                }

                index = this.nodes.Count;
                this.nodes.Add(node);
            }

            Debug.Assert(index != -1);
            this.ApplyExtensions(extension => extension.PostExportNode(index, gameObject));

            this.objectToIndexCache.Add(gameObject, index);
            return index;
        }

        private static float[] ColorToArray(Color color, int size)
        {
            var array = new float[size];
            for (var i = 0; i < size; i++)
            {
                array[i] = color[i];
            }
            return array;
        }

        private int ExportMaterial(Material unityMaterial)
        {
            int index = -1;
            if (this.objectToIndexCache.TryGetValue(unityMaterial, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportMaterial(unityMaterial, out index)))
            {
                var info = unityMaterial.ToMetallic().GetInfo<MetallicMaterialInfo>();

                if (info._SmoothnessTextureChannel != 0)
                {
                    throw new NotImplementedException();
                }

                var material = new Schema.Material
                {
                    Name = unityMaterial.name,
                    PbrMetallicRoughness = new Schema.MaterialPbrMetallicRoughness
                    {
                        BaseColorFactor = ColorToArray(info._Color.linear, 4),
                    },
                };

                if (info._MainTex != null)
                {
                    material.PbrMetallicRoughness.BaseColorTexture = new Schema.MaterialTexture
                    {
                        Index = this.ExportTexture(info._MainTex),
                    };
                }

                if (info._MetallicGlossMap == null)
                {
                    material.PbrMetallicRoughness.MetallicFactor = Mathf.GammaToLinearSpace(info._Metallic);
                    material.PbrMetallicRoughness.RoughnessFactor = 1.0f - info._Glossiness;
                }
                else
                {
                    material.PbrMetallicRoughness.MetallicFactor = 1.0f;
                    material.PbrMetallicRoughness.RoughnessFactor = 1.0f;

                    var pixels = info._MetallicGlossMap.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i].r = Mathf.GammaToLinearSpace(pixels[i].r);
                        pixels[i].g = 1.0f - (pixels[i].a * info._GlossMapScale);
                        pixels[i].b = 0.0f;
                        pixels[i].a = 1.0f;
                    }

                    var metallicRoughnessTexture = new Texture2D(info._MetallicGlossMap.width, info._MetallicGlossMap.height, TextureFormat.ARGB32, false);
                    metallicRoughnessTexture.SetPixels(pixels);
                    metallicRoughnessTexture.Apply();

                    material.PbrMetallicRoughness.MetallicRoughnessTexture = new Schema.MaterialTexture
                    {
                        Index = this.ExportTexture(metallicRoughnessTexture),
                    };
                }

                this.ExportMaterialCommon(unityMaterial, material);

                index = this.materials.Count;
                this.materials.Add(material);
            }

            Debug.Assert(index != -1);
            this.ApplyExtensions(extension => extension.PostExportMaterial(index, unityMaterial));

            this.objectToIndexCache.Add(unityMaterial, index);
            return index;
        }

        private void ExportMaterialCommon(Material unityMaterial, Schema.Material material)
        {
            var info = unityMaterial.GetInfo<CommonMaterialInfo>();

            if (info._BumpMap != null)
            {
                material.NormalTexture = new Schema.MaterialNormalTexture
                {
                    Index = this.ExportTexture(info._BumpMap, true),
                    Scale = info._BumpScale,
                };
            }

            if (info._OcclusionMap != null)
            {
                material.OcclusionTexture = new Schema.MaterialOcclusionTexture
                {
                    Index = this.ExportTexture(info._OcclusionMap),
                    Strength = info._OcclusionStrength,
                };
            }

            material.EmissiveFactor = ColorToArray(info._EmissionColor.linear, 3);

            if (info._EmissionMap != null)
            {
                material.EmissiveTexture = new Schema.MaterialTexture
                {
                    Index = this.ExportTexture(info._EmissionMap),
                };
            }
        }

        private int ExportTexture(Texture2D unityTexture, bool normalMap = false)
        {
            int index = -1;
            if (this.objectToIndexCache.TryGetValue(unityTexture, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportTexture(unityTexture, normalMap, out index)))
            {
                if (normalMap)
                {
                    unityTexture = UnpackNormals(unityTexture);
                }

                var textureBytes = unityTexture.EncodeToPNG();

                int imageIndex;

                if (this.outputBinary)
                {
                    var bufferViewIndex = this.ExportBufferView(0, this.binaryBodyWriter.BaseStream.Position, textureBytes.Length);

                    imageIndex = this.images.Count;
                    this.images.Add(new Schema.Image
                    {
                        BufferView = bufferViewIndex,
                        MimeType = "image/png",
                    });

                    this.binaryBodyWriter.Write(textureBytes);
                }
                else
                {
                    imageIndex = this.images.Count;
                    var imageUri = string.Format("{0}_image{1}.png", this.outputName, imageIndex);
                    this.images.Add(new Schema.Image
                    {
                        Uri = imageUri,
                    });

                    File.WriteAllBytes(Path.Combine(this.outputDirectory, imageUri), textureBytes);
                }

                var samplerIndex = this.samplers.Count;
                this.samplers.Add(new Schema.Sampler
                {
                });

                index = this.textures.Count;
                this.textures.Add(new Schema.Texture
                {
                    Sampler = samplerIndex,
                    Source = imageIndex,
                });
            }

            Debug.Assert(index != -1);
            this.ApplyExtensions(extension => extension.PostExportTexture(index, unityTexture, normalMap));

            this.objectToIndexCache.Add(unityTexture, index);
            return index;
        }

        private int ExportMesh(Mesh unityMesh, int materialIndex)
        {
            int index = -1;
            if (this.objectToIndexCache.TryGetValue(unityMesh, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportMesh(unityMesh, materialIndex, out index)))
            {
                var attributes = new Dictionary<string, int>();

                if (unityMesh.uv.Any())
                {
                    attributes.Add("TEXCOORD_0", this.ExportData(FlipY(unityMesh.uv)));
                }

                if (unityMesh.uv2.Any())
                {
                    attributes.Add("TEXCOORD_1", this.ExportData(FlipY(unityMesh.uv2)));
                }

                if (unityMesh.uv3.Any())
                {
                    attributes.Add("TEXCOORD_2", this.ExportData(FlipY(unityMesh.uv3)));
                }

                if (unityMesh.uv4.Any())
                {
                    attributes.Add("TEXCOORD_3", this.ExportData(FlipY(unityMesh.uv4)));
                }

                if (unityMesh.normals.Any())
                {
                    attributes.Add("NORMAL", this.ExportData(LeftHandToRightHand(unityMesh.normals)));
                }

                if (unityMesh.tangents.Any())
                {
                    attributes.Add("TANGENT", this.ExportData(LeftHandToRightHand(unityMesh.tangents)));
                }

                attributes.Add("POSITION", this.ExportData(LeftHandToRightHand(unityMesh.vertices)));

                index = this.meshes.Count;
                this.meshes.Add(new Schema.Mesh
                {
                    Name = unityMesh.name,
                    Primitives = new[]
                    {
                        new Schema.MeshPrimitive
                        {
                            Attributes = attributes,
                            Indices = this.ExportData(FlipFaces(unityMesh.triangles).Select(triangle => (ushort)triangle).ToArray()),
                            Material = materialIndex,
                            Mode = Schema.PrimitiveMode.TRIANGLES,
                        },
                    },
                });
            }

            Debug.Assert(index != -1);
            this.ApplyExtensions(extension => extension.PostExportMesh(index, unityMesh, materialIndex));

            this.objectToIndexCache.Add(unityMesh, index);
            return index;
        }

        private bool ApplyExtensions(Func<Extension, bool> func)
        {
            foreach (var extension in this.extensions)
            {
                if (func(extension))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyExtensions(Action<Extension> action)
        {
            foreach (var extension in this.extensions)
            {
                action(extension);
            }
        }

        private static IEnumerable<Vector2> FlipY(IEnumerable<Vector2> values)
        {
            return values.Select(value => new Vector2(value.x, -value.y));
        }

        private static IEnumerable<Vector3> LeftHandToRightHand(IEnumerable<Vector3> values)
        {
            return values.Select(value => new Vector3(value.x, value.y, -value.z));
        }

        private static IEnumerable<Vector4> LeftHandToRightHand(IEnumerable<Vector4> values)
        {
            return values.Select(value => new Vector4(value.x, value.y, -value.z, value.w));
        }

        private static IEnumerable<int> FlipFaces(int[] triangles)
        {
            if ((triangles.Length % 3) != 0)
            {
                throw new ArgumentException("Indices length must be divisible by 3");
            }

            for (int i = 0; i < triangles.Length; i += 3)
            {
                yield return triangles[i + 2];
                yield return triangles[i + 1];
                yield return triangles[i + 0];
            }
        }

        private static Texture2D UnpackNormals(Texture2D texture)
        {
            var normals = texture.GetPixels();
            for (var i = 0; i < normals.Length; i++)
            {
                normals[i].r = normals[i].a;
                normals[i].g = normals[i].g;
                var x = normals[i].a + 0.5f;
                var y = normals[i].g + 0.5f;
                normals[i].b = Mathf.Sqrt(x * x + y * y);
                normals[i].a = 1;
            }

            texture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
            texture.SetPixels(normals);
            texture.Apply();

            return texture;
        }
    }
}
