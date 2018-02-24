using System.Collections.Generic;
using System.IO;

namespace Bacs.Archive.Web.Backend
{
    internal static class Utils
    {
        public static IEnumerable<byte> AsEnumerable(this Stream stream)
        {
            if (stream == null) yield break;
            for (var i = stream.ReadByte(); i != -1; i = stream.ReadByte())
                yield return (byte)i;
        }
    }
}