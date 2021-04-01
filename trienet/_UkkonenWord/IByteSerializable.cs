using System;
using System.IO;

namespace Gma.DataStructures.StringSearch.Word
{
    interface IByteSerializable {
        void ToBytes(MemoryStream memoryStream);
        void FromBytes(MemoryStream memoryStream);
    }
}