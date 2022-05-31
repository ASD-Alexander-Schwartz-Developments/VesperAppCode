using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public class RawUpdate
    {
        public byte Index;
        public Color Color;

        public void Offset(int offset)
            => Index = (byte)(Index + offset);

        public RawUpdate Clone() => new RawUpdate(Index, Color);

        public RawUpdate(int index, Color color)
        {
            Index = (byte)index;
            Color = color.Clone();
        }

        public RawUpdate(RawUpdate n, int offset)
        {
            Index = (byte)(n.Index + offset);
            Color = n.Color.Clone();
        }


    }
}
