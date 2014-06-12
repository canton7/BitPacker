using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitPacker
{
    public interface IDeserialize
    {
        void Deserialize(BinaryReader reader);
    }
}
