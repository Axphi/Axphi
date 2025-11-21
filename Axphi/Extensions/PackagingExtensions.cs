using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Axphi.Extensions
{
    internal static class PackagingExtensions
    {
        public static void WritePartAllBytes(this Package package, string partUri, string contentType, byte[]? bytes)
        {
            if (bytes is null)
            {
                return;
            }

            var part = package.CreatePart(new Uri(partUri), contentType, CompressionOption.Normal);
            using var partStream = part.GetStream();
            partStream.Write(bytes, 0, bytes.Length);
        }
    }
}
