namespace Relead.CrossPromo
{
    /// <summary>
    /// Checks a downloaded icon before Texture2D.LoadImage decodes it: PNG or JPEG only, file and pixel size capped,
    /// so a bad catalog or host can't make the game decode a huge image (decompression bomb, OOM).
    /// </summary>
    public static class ImageGuard
    {
        public const int MaxIconPixels = 1024;

        public static bool IsAcceptableIcon(byte[] data)
        {
            if (data == null || data.Length > Prefetcher.MaxIconBytes) return false;
            int w, h;
            return TryGetSize(data, out w, out h) && w > 0 && h > 0 && w <= MaxIconPixels && h <= MaxIconPixels;
        }

        /// <summary>Width/height from the PNG IHDR or the JPEG SOF header. False for anything else.</summary>
        public static bool TryGetSize(byte[] b, out int width, out int height)
        {
            width = height = 0;
            if (b == null || b.Length < 24) return false;
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[12] == 0x49 && b[13] == 0x48 && b[14] == 0x44 && b[15] == 0x52)
            {
                width = BigEndian32(b, 16);
                height = BigEndian32(b, 20);
                return width > 0 && height > 0;
            }
            if (b[0] == 0xFF && b[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) return false;
                    byte marker = b[i + 1];
                    if (marker == 0xFF) { i++; continue; }
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                    int len = (b[i + 2] << 8) | b[i + 3];
                    if (len < 2) return false;
                    bool sof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                    if (sof)
                    {
                        height = (b[i + 5] << 8) | b[i + 6];
                        width = (b[i + 7] << 8) | b[i + 8];
                        return width > 0 && height > 0;
                    }
                    i += 2 + len;
                }
            }
            return false;
        }

        static int BigEndian32(byte[] b, int o)
        {
            long v = ((long)b[o] << 24) | ((long)b[o + 1] << 16) | ((long)b[o + 2] << 8) | b[o + 3];
            return v > int.MaxValue ? int.MaxValue : (int)v;
        }
    }
}
