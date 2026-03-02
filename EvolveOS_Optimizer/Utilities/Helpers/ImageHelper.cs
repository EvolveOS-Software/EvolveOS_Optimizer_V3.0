using System.IO;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class ImageHelper
    {
        public static async Task<BitmapImage?> LoadFromBytesAsync(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }

            var image = new BitmapImage();

            using (var memStream = new MemoryStream(imageData))
            using (var randomAccessStream = memStream.AsRandomAccessStream())
            {
                await image.SetSourceAsync(randomAccessStream);
            }

            return image;
        }
    }
}