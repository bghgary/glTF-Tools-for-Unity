using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Gltf.Serialization
{
    internal sealed partial class Exporter
    {
        private int ExportBuffer(string uri, int byteLength)
        {
            int index = this.buffers.Count;

            this.buffers.Add(new Schema.Buffer
            {
                Uri = uri,
                ByteLength = byteLength,
            });

            return index;
        }

        private int ExportBufferView(int bufferIndex, int byteOffset, int byteLength)
        {
            int index = this.bufferViews.Count;

            this.bufferViews.Add(new Schema.BufferView
            {
                Buffer = bufferIndex,
                ByteOffset = byteOffset,
                ByteLength = byteLength,
            });

            return index;
        }

        private int ExportAccessor(int bufferViewIndex, Schema.AccessorComponentType componentType, int count, Schema.AccessorType type, IEnumerable<object> min, IEnumerable<object> max, string name = null)
        {
            int index = this.accessors.Count;

            this.accessors.Add(new Schema.Accessor
            {
                BufferView = bufferViewIndex,
                ByteOffset = 0,
                ComponentType = componentType,
                Count = count,
                Type = type,
                Min = min,
                Max = max,
                Name = name
            });

            return index;
        }

        private int ExportData(Schema.AccessorType type, Schema.AccessorComponentType componentType, int componentSize, int count, IEnumerable<object> min, IEnumerable<object> max, int byteLength, Action<BinaryWriter> writeData, string accessorName = null)
        {
            // The offset of the data must be aligned to a multiple of the component size.
            var position = checked((int)this.dataWriter.BaseStream.Position);
            var alignedPosition = Align(position, componentSize);
            for (var i = position; i < alignedPosition; i++)
            {
                this.dataWriter.Write(byte.MinValue);
            }

            var bufferViewIndex = this.ExportBufferView(0, alignedPosition, byteLength);
            var accessorIndex = this.ExportAccessor(bufferViewIndex, componentType, count, type, min, max, accessorName);

            writeData(this.dataWriter);

            return accessorIndex;
        }

        private int ExportData(IEnumerable<ushort> values)
        {
            return this.ExportData(
                Schema.AccessorType.SCALAR,
                Schema.AccessorComponentType.UNSIGNED_SHORT,
                sizeof(ushort),
                values.Count(),
                new object[] { values.Min() },
                new object[] { values.Max() },
                sizeof(ushort) * values.Count(),
                binaryWriter => values.ForEach(value => binaryWriter.Write(value)));
        }

        private int ExportData(IEnumerable<Vector2> values)
        {
            return this.ExportData(
                Schema.AccessorType.VEC2,
                Schema.AccessorComponentType.FLOAT,
                sizeof(float),
                values.Count(),
                new object[] { values.Select(value => value.x).Min(), values.Select(value => value.y).Min() },
                new object[] { values.Select(value => value.x).Max(), values.Select(value => value.y).Max() },
                sizeof(float) * 2 * values.Count(),
                binaryWriter => values.ForEach(value => binaryWriter.Write(value)));
        }

        private int ExportData(IEnumerable<Vector3> values, string accessorName = null)
        {
            return this.ExportData(
                Schema.AccessorType.VEC3,
                Schema.AccessorComponentType.FLOAT,
                sizeof(float),
                values.Count(),
                new object[] { values.Select(value => value.x).Min(), values.Select(value => value.y).Min(), values.Select(value => value.z).Min() },
                new object[] { values.Select(value => value.x).Max(), values.Select(value => value.y).Max(), values.Select(value => value.z).Max() },
                sizeof(float) * 3 * values.Count(),
                binaryWriter => values.ForEach(value => binaryWriter.Write(value)),
                accessorName);
        }

        private int ExportData(IEnumerable<Vector4> values)
        {
            return this.ExportData(
                Schema.AccessorType.VEC4,
                Schema.AccessorComponentType.FLOAT,
                sizeof(float),
                values.Count(),
                new object[] { values.Select(value => value.x).Min(), values.Select(value => value.y).Min(), values.Select(value => value.z).Min(), values.Select(value => value.w).Min() },
                new object[] { values.Select(value => value.x).Max(), values.Select(value => value.y).Max(), values.Select(value => value.z).Max(), values.Select(value => value.w).Max() },
                sizeof(float) * 4 * values.Count(),
                binaryWriter => values.ForEach(value => binaryWriter.Write(value)));
        }

        private int ExportData(IEnumerable<Color> values)
        {
            return this.ExportData(
                Schema.AccessorType.VEC4,
                Schema.AccessorComponentType.FLOAT,
                sizeof(float),
                values.Count(),
                new object[] { values.Select(value => value.r).Min(), values.Select(value => value.g).Min(), values.Select(value => value.b).Min(), values.Select(value => value.a).Min() },
                new object[] { values.Select(value => value.r).Max(), values.Select(value => value.g).Max(), values.Select(value => value.b).Max(), values.Select(value => value.a).Max() },
                sizeof(float) * 4 * values.Count(),
                binaryWriter => values.ForEach(value => binaryWriter.Write(value)));
        }
    }
}
