using System;

namespace TsMap
{
    public class TsNode
    {
        public ulong Uid { get; }

        public float X { get; }
        public float Z { get; }
        public float Rotation { get; }

        public TsItem ForwardItem { get; set; }
        public ulong ForwardItemUID { get; private set; }
        public TsItem BackwardItem { get; set; }
        public ulong BackwardItemUID { get; private set; }

        public TsNode(TsSector sector, int fileOffset)
        {
            Uid = BitConverter.ToUInt64(sector.Stream, fileOffset);
            
            ForwardItemUID = BitConverter.ToUInt64(sector.Stream, fileOffset + 0x2C);
            BackwardItemUID = BitConverter.ToUInt64(sector.Stream, fileOffset + 0x24);
            ForwardItem = null;
            BackwardItem = null;
            
            X = BitConverter.ToInt32(sector.Stream, fileOffset += 0x08) / 256f;
            Z = BitConverter.ToInt32(sector.Stream, fileOffset += 0x08) / 256f;

            var rX = BitConverter.ToSingle(sector.Stream, fileOffset += 0x04);
            var rZ = BitConverter.ToSingle(sector.Stream, fileOffset + 0x08);

            var rot = Math.PI - Math.Atan2(rZ, rX);
            Rotation = (float) (rot % Math.PI * 2);
        }
    }
}
