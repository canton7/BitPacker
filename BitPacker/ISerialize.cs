using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface ISerialize
    {
        void Serialize(BinaryWriter writer);
    }
}
