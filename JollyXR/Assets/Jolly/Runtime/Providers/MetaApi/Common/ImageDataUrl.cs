using System;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Common
{
    static class ImageDataUrl
    {
        const string Scheme = "data:";
        const string Base64Marker = ";base64,";

        /// <summary><c>data:&lt;mime&gt;;base64,&lt;bytes&gt;</c> built in one string allocation (no intermediate base64 copy).</summary>
        public static string Of(EncodedImage image)
        {
            var length = Scheme.Length + image.MimeType.Length + Base64Marker.Length + (image.Bytes.Length + 2) / 3 * 4;
            return string.Create(length, image, static (span, img) =>
            {
                Scheme.AsSpan().CopyTo(span);
                var at = Scheme.Length;
                img.MimeType.AsSpan().CopyTo(span.Slice(at));
                at += img.MimeType.Length;
                Base64Marker.AsSpan().CopyTo(span.Slice(at));
                at += Base64Marker.Length;
                Convert.TryToBase64Chars(img.Bytes, span.Slice(at), out _);
            });
        }
    }
}
