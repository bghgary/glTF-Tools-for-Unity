using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Gltf.Serialization
{
    [Flags]
    public enum Extensions
    {
        None                                = 0x00000000,
        KHR_materials_pbrSpecularGlossiness = 0x00000001,
    }

    public enum ImageFormat
    {
        JPG,
        PNG,
    }

    public struct ExportSettings
    {
        public Formatting JsonFormatting;
        public ImageFormat ImageFormat;
        public Extensions Extensions;

        public ExportSettings(Formatting jsonFormatting, ImageFormat imageFormat, Extensions extensions)
        {
            this.JsonFormatting = jsonFormatting;
            this.ImageFormat = imageFormat;
            this.Extensions = extensions;
        }

        public static ExportSettings Default = new ExportSettings
        {
            JsonFormatting = Formatting.Indented,
            ImageFormat = ImageFormat.PNG,
            Extensions = Extensions.None,
        };
    }

    public static class GameObjectExtensions
    {
        public static void Export(this GameObject inputObject, string outputDirectory, string outputName, bool outputBinary, ExportSettings settings)
        {
            new[] { inputObject }.Export(outputDirectory, outputName, outputBinary, settings);
        }

        public static void Export(this IEnumerable<GameObject> inputObjects, string outputDirectory, string outputName, bool outputBinary, ExportSettings settings)
        {
            using (var exporter = new Exporter())
            {
                exporter.Export(inputObjects, outputDirectory, outputName, outputBinary, settings);
            }
        }
    }

    internal sealed partial class Exporter : IDisposable
    {
        private static class Binary
        {
            public const uint Magic = 0x46546C67U;
            public const uint Version = 2;

            public static class ChunkFormat
            {
                public const uint JSON = 0x4E4F534AU;
                public const uint BIN = 0x004E4942U;
            }
        }

        public struct OcclusionMetallicInfo
        {
            public OcclusionInfo Occlusion;
            public MetallicInfo Metallic;
        }

        private string outputDirectory;
        private string outputName;
        private bool outputBinary;
        private ExportSettings settings;

        private readonly ObjectTracker objectTracker;
        private readonly PbrMaterialManager pbrMaterialManager;

        private readonly Dictionary<OcclusionMetallicInfo, Texture2D> occlusionMetallicInfoToTextureCache = new Dictionary<OcclusionMetallicInfo, Texture2D>();

        private BinaryWriter dataWriter;
        private readonly Dictionary<UnityEngine.Object, int> objectToIndexCache = new Dictionary<UnityEngine.Object, int>();
        private readonly List<Extension> extensions = new List<Extension>();

        private readonly List<Schema.Accessor> accessors = new List<Schema.Accessor>();
        private readonly List<Schema.Animation> animations = new List<Schema.Animation>();
        private readonly List<Schema.Buffer> buffers = new List<Schema.Buffer>();
        private readonly List<string> extensionsUsed = new List<string>();
        private readonly List<Schema.BufferView> bufferViews = new List<Schema.BufferView>();
        private readonly List<Schema.Image> images = new List<Schema.Image>();
        private readonly List<Schema.Mesh> meshes = new List<Schema.Mesh>();
        private readonly List<Schema.Material> materials = new List<Schema.Material>();
        private readonly List<Schema.Node> nodes = new List<Schema.Node>();
        private readonly List<Schema.Texture> textures = new List<Schema.Texture>();

        public Exporter()
        {
            this.objectTracker = new ObjectTracker();
            this.pbrMaterialManager = new PbrMaterialManager(this.objectTracker);
        }

        public void Export(IEnumerable<GameObject> inputObjects, string outputDirectory, string outputName, bool outputBinary, ExportSettings settings)
        {
            this.outputDirectory = outputDirectory;
            this.outputName = outputName;
            this.outputBinary = outputBinary;
            this.settings = settings;

            this.dataWriter = new BinaryWriter(new MemoryStream());
            this.objectToIndexCache.Clear();
            this.extensions.Clear();

            this.accessors.Clear();
            this.buffers.Clear();
            this.extensionsUsed.Clear();
            this.bufferViews.Clear();
            this.images.Clear();
            this.meshes.Clear();
            this.materials.Clear();
            this.nodes.Clear();
            this.textures.Clear();

            Directory.CreateDirectory(this.outputDirectory);

            if ((this.settings.Extensions & Extensions.KHR_materials_pbrSpecularGlossiness) != 0)
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

            this.ExportAnimations(inputObjects);

            var gltf = new Schema.Gltf
            {
                Accessors = this.accessors,
                Animations = this.animations,
                Asset = new Schema.Asset { Generator = "glTF Tools for Unity", Version = "2.0" },
                BufferViews = this.bufferViews,
                Buffers = this.buffers,
                ExtensionsUsed = this.extensionsUsed,
                Images = this.images,
                Meshes = this.meshes,
                Materials = this.materials,
                Nodes = this.nodes,
                Scene = 0,
                Scenes = scenes,
                Textures = this.textures,
            };

            var jsonSerializer = new JsonSerializer
            {
                Formatting = this.settings.JsonFormatting,
                ContractResolver = new CamelCasePropertyNamesContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy
                    {
                        ProcessDictionaryKeys = false
                    }
                }
            };

            this.dataWriter.Flush();
            var dataBytes = ((MemoryStream)this.dataWriter.BaseStream).ToArray();

            if (this.outputBinary)
            {
                Debug.Assert(this.buffers.Count == 0);
                var alignedDataByteLength = Align(dataBytes.Length, 4);
                this.ExportBuffer(null, alignedDataByteLength);

                byte[] jsonBytes;
                using (var jsonWriter = new StreamWriter(new MemoryStream()))
                {
                    jsonSerializer.Serialize(jsonWriter, gltf);
                    jsonWriter.Flush();
                    jsonBytes = ((MemoryStream)jsonWriter.BaseStream).ToArray();
                }

                var alignedJsonByteLength = Align(jsonBytes.Length, 4);

                using (var fileStream = new FileStream(Path.Combine(this.outputDirectory, this.outputName + ".glb"), FileMode.Create))
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    // 12-byte header (magic, version, length)
                    binaryWriter.Write(Binary.Magic);
                    binaryWriter.Write(Binary.Version);
                    binaryWriter.Write(checked(12 + 8 + alignedJsonByteLength + 8 + alignedDataByteLength));

                    // Chunk 0 - JSON
                    binaryWriter.Write(alignedJsonByteLength);
                    binaryWriter.Write(Binary.ChunkFormat.JSON);
                    binaryWriter.Write(jsonBytes);
                    for (var i = jsonBytes.Length; i < alignedJsonByteLength; i++)
                    {
                        binaryWriter.Write(' ');
                    }

                    // Chunk 1 - Binary Buffer
                    binaryWriter.Write(alignedDataByteLength);
                    binaryWriter.Write(Binary.ChunkFormat.BIN);
                    binaryWriter.Write(dataBytes);
                    for (var i = dataBytes.Length; i < alignedDataByteLength; i++)
                    {
                        binaryWriter.Write(byte.MinValue);
                    }

                    binaryWriter.Flush();
                    fileStream.Flush();
                }
            }
            else
            {
                var bufferUri = this.outputName + ".bin";
                this.ExportBuffer(bufferUri, dataBytes.Length);
                File.WriteAllBytes(Path.Combine(this.outputDirectory, bufferUri), dataBytes);

                using (var streamWriter = new StreamWriter(Path.Combine(this.outputDirectory, this.outputName + ".gltf")))
                {
                    jsonSerializer.Serialize(streamWriter, gltf);
                    streamWriter.Flush();
                }
            }
        }

        public void Dispose()
        {
            this.objectTracker.Dispose();
        }

        private static int Align(int value, int size)
        {
            var remainder = value % size;
            return (remainder == 0 ? value : checked(value + size - remainder));
        }

        private static readonly Matrix4x4 InvertZMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, 1, -1));
        private static Matrix4x4 GetRightHandedMatrix(Matrix4x4 matrix)
        {
            return InvertZMatrix * matrix * InvertZMatrix;
        }

        private static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);
            rotation = Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
            scale = new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
        }

        private int ExportNode(GameObject gameObject)
        {
            int index;
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
                DecomposeMatrix(GetRightHandedMatrix(transform.worldToLocalMatrix), out position, out rotation, out scale);

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
                    var meshFilter = gameObject.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        var unityMesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                        if (unityMesh.triangles.Any())
                        {
                            var unityMaterial = renderer.sharedMaterial;
                            var materialIndex = this.ExportMaterial(unityMaterial);
                            node.Mesh = this.ExportMesh(unityMesh, materialIndex);
                        }
                    }
                }

                index = this.nodes.Count;
                this.nodes.Add(node);
            }

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

        private static string FormatMaterialTextureName(string type, int index)
        {
            return index == 0 ? type : type + index;
        }

        private int ExportMaterial(Material unityMaterial)
        {
            int index;
            if (this.objectToIndexCache.TryGetValue(unityMaterial, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportMaterial(unityMaterial, out index)))
            {
                this.ExportMaterialCore(unityMaterial, true, out index);
            }

            this.ApplyExtensions(extension => extension.PostExportMaterial(index, unityMaterial));

            this.objectToIndexCache.Add(unityMaterial, index);
            return index;
        }

        private void ExportMaterialCore(Material unityMaterial, bool packOcclusion, out int index)
        {
            var info = new OcclusionMetallicInfo
            {
                Metallic = this.pbrMaterialManager.ConvertToMetallic(unityMaterial).GetInfo<MetallicInfo>(),
                Occlusion = packOcclusion ? unityMaterial.GetInfo<OcclusionInfo>() : default(OcclusionInfo),
            };

            if (info.Metallic._SmoothnessTextureChannel != 0)
            {
                throw new NotImplementedException();
            }

            if (info.Occlusion._OcclusionMap == null)
            {
                packOcclusion = false;
            }

            index = this.materials.Count;

            var material = new Schema.Material
            {
                Name = unityMaterial.name,
                PbrMetallicRoughness = new Schema.MaterialPbrMetallicRoughness
                {
                    BaseColorFactor = ColorToArray(info.Metallic._Color.linear, 4),
                },
            };

            if (info.Metallic._MainTex != null)
            {
                material.PbrMetallicRoughness.BaseColorTexture = new Schema.MaterialTexture
                {
                    Index = this.ExportTexture(info.Metallic._MainTex, FormatMaterialTextureName("baseColor", index)),
                };
            }

            if (info.Metallic._MetallicGlossMap == null && !packOcclusion)
            {
                material.PbrMetallicRoughness.MetallicFactor = Mathf.GammaToLinearSpace(info.Metallic._Metallic);
                material.PbrMetallicRoughness.RoughnessFactor = 1.0f - info.Metallic._Glossiness;
            }
            else
            {
                material.PbrMetallicRoughness.MetallicFactor = 1.0f;
                material.PbrMetallicRoughness.RoughnessFactor = 1.0f;

                Texture2D texture;
                if (!this.occlusionMetallicInfoToTextureCache.TryGetValue(info, out texture))
                {
                    var pixels = info.Metallic._MetallicGlossMap.GetPixels();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = new Color(0.0f,
                            1.0f - (pixels[i].a * info.Metallic._GlossMapScale),
                            Mathf.GammaToLinearSpace(pixels[i].grayscale));
                    }

                    if (packOcclusion)
                    {
                        var occlusionPixels = info.Occlusion._OcclusionMap.GetPixels();

                        if (occlusionPixels.Length != pixels.Length)
                        {
                            throw new NotSupportedException();
                        }

                        for (int i = 0; i < pixels.Length; i++)
                        {
                            pixels[i].r = Mathf.GammaToLinearSpace(occlusionPixels[i].grayscale);
                        }
                    }

                    texture = this.objectTracker.Add(new Texture2D(info.Metallic._MetallicGlossMap.width, info.Metallic._MetallicGlossMap.height, TextureFormat.RGB24, false));
                    texture.SetPixels(pixels);
                    texture.Apply();

                    this.occlusionMetallicInfoToTextureCache.Add(info, texture);
                }

                var textureName = packOcclusion ? "occlusionRoughnessMetallic" : "roughnessMetallic";
                var textureIndex = this.ExportTexture(texture, FormatMaterialTextureName(textureName, index));

                material.PbrMetallicRoughness.MetallicRoughnessTexture = new Schema.MaterialTexture
                {
                    Index = textureIndex,
                };

                if (packOcclusion)
                {
                    material.OcclusionTexture = new Schema.MaterialOcclusionTexture
                    {
                        Index = textureIndex,
                        Strength = info.Occlusion._OcclusionStrength,
                    };
                }
            }

            this.materials.Add(material);

            this.ExportMaterialAlpha(unityMaterial, index);
            this.ExportMaterialNormal(unityMaterial, index);
            this.ExportMaterialEmissive(unityMaterial, index);

            // For now, assume MASK materials are always double-sided
            if (material.AlphaMode == Schema.AlphaMode.MASK)
            {
                material.DoubleSided = true;
            }
        }

        private void ExportMaterialAlpha(Material unityMaterial, int index)
        {
            var info = unityMaterial.GetInfo<AlphaInfo>();
            var material = this.materials[index];

            switch ((int)info._Mode)
            {
                case 0: // Opaque
                    material.AlphaMode = Schema.AlphaMode.OPAQUE;
                    break;
                case 1: // Cutout
                    material.AlphaMode = Schema.AlphaMode.MASK;
                    break;
                case 2: // Fade
                case 3: // Transparent
                    material.AlphaMode = Schema.AlphaMode.BLEND;
                    break;
                default:
                    throw new NotSupportedException();
            }

            material.AlphaCutoff = (material.AlphaMode == Schema.AlphaMode.BLEND ? info._Cutoff : 0.5f);
        }

        private void ExportMaterialNormal(Material unityMaterial, int index)
        {
            var info = unityMaterial.GetInfo<NormalInfo>();
            var material = this.materials[index];

            if (info._BumpMap != null)
            {
                var texture = this.UnpackNormals(info._BumpMap);

                material.NormalTexture = new Schema.MaterialNormalTexture
                {
                    Index = this.ExportTexture(texture, FormatMaterialTextureName("normal", index)),
                    Scale = info._BumpScale,
                };
            }
        }

        private void ExportMaterialEmissive(Material unityMaterial, int index)
        {
            var info = unityMaterial.GetInfo<EmissiveInfo>();
            var material = this.materials[index];

            material.EmissiveFactor = ColorToArray(info._EmissionColor.linear, 3);

            if (info._EmissionMap != null)
            {
                material.EmissiveTexture = new Schema.MaterialTexture
                {
                    Index = this.ExportTexture(info._EmissionMap, FormatMaterialTextureName("emissive", index)),
                };
            }
        }

        private static bool TextureFormatHasAlpha(TextureFormat textureFormat)
        {
            switch (textureFormat)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                    return true;
            }

            return false;
        }

        private int ExportTexture(Texture2D unityTexture, string name)
        {
            int index;
            if (this.objectToIndexCache.TryGetValue(unityTexture, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportTexture(unityTexture, name, out index)))
            {
                var imageFormat = (TextureFormatHasAlpha(unityTexture.format) ? ImageFormat.PNG : this.settings.ImageFormat);
                var textureBytes = (imageFormat == ImageFormat.PNG ? unityTexture.EncodeToPNG() : unityTexture.EncodeToJPG(90));
                var imageFormatString = imageFormat.ToString().ToLower();

                int imageIndex;

                if (this.outputBinary)
                {
                    var bufferViewIndex = this.ExportBufferView(0, checked((int)this.dataWriter.BaseStream.Position), textureBytes.Length);

                    imageIndex = this.images.Count;
                    this.images.Add(new Schema.Image
                    {
                        BufferView = bufferViewIndex,
                        MimeType = "image/" + imageFormatString,
                    });

                    this.dataWriter.Write(textureBytes);
                }
                else
                {
                    imageIndex = this.images.Count;
                    var imageUri = string.Format("{0}_{1}.{2}", this.outputName, name, imageFormatString);
                    this.images.Add(new Schema.Image
                    {
                        Uri = imageUri,
                    });

                    File.WriteAllBytes(Path.Combine(this.outputDirectory, imageUri), textureBytes);
                }

                index = this.textures.Count;
                this.textures.Add(new Schema.Texture
                {
                    Source = imageIndex,
                });
            }

            this.ApplyExtensions(extension => extension.PostExportTexture(index, unityTexture));

            this.objectToIndexCache.Add(unityTexture, index);
            return index;
        }

        private int ExportMesh(Mesh unityMesh, int materialIndex)
        {
            int index;
            if (this.objectToIndexCache.TryGetValue(unityMesh, out index))
            {
                return index;
            }

            if (!this.ApplyExtensions(extension => extension.ExportMesh(unityMesh, materialIndex, out index)))
            {
                var attributes = new Dictionary<string, int>();

                if (unityMesh.colors.Any())
                {
                    attributes.Add("COLOR_0", this.ExportData(unityMesh.colors));
                }

                if (unityMesh.uv.Any())
                {
                    attributes.Add("TEXCOORD_0", this.ExportData(InvertY(unityMesh.uv)));
                }

                if (unityMesh.uv2.Any())
                {
                    attributes.Add("TEXCOORD_1", this.ExportData(InvertY(unityMesh.uv2)));
                }

                if (unityMesh.normals.Any())
                {
                    attributes.Add("NORMAL", this.ExportData(InvertZ(unityMesh.normals)));
                }

                if (unityMesh.tangents.Any())
                {
                    attributes.Add("TANGENT", this.ExportData(InvertW(unityMesh.tangents)));
                }

                attributes.Add("POSITION", this.ExportData(InvertZ(unityMesh.vertices)));

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

        private static IEnumerable<Vector2> InvertY(IEnumerable<Vector2> values)
        {
            return values.Select(value => new Vector2(value.x, -value.y));
        }

        private static IEnumerable<Vector3> InvertZ(IEnumerable<Vector3> values)
        {
            return values.Select(value => new Vector3(value.x, value.y, -value.z));
        }

        private static IEnumerable<Vector4> InvertW(IEnumerable<Vector4> values)
        {
            return values.Select(value => new Vector4(value.x, value.y, value.z, -value.w));
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

        private Texture2D UnpackNormals(Texture2D texture)
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

            texture = this.objectTracker.Add(new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false));
            texture.SetPixels(normals);
            texture.Apply();

            return texture;
        }
    }
}
