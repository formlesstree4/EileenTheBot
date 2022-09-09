using Bot.Models.Casino;
using Discord;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Models.Extensions
{
    public static class HandExtensions
    {

        /// <summary>
        ///     Gets a single image file that is the given <see cref="Hand"/>
        /// </summary>
        /// <returns>A promise to create a <see cref="FileAttachment"/></returns>
        public static async Task<FileAttachment> GetHandAsAttachment(this Hand hand, bool hideFirstCard = false)
        {
            var imageStream = await hand.GetHandAsImage(hideFirstCard);
            return new FileAttachment(imageStream, "hand.png");
        }

        /// <summary>
        ///     Gets a <see cref="Stream"/> that contains the image data for the given <see cref="Hand"/>
        /// </summary>
        /// <param name="hand">The <see cref="Hand"/> to create an image of</param>
        /// <param name="hideFirstCard">If true, the first card will be hidden. This is typically used for the dealer</param>
        /// <returns>A promise to create a <see cref="Stream"/> that contains the appropriate image data</returns>
        public static async Task<Stream> GetHandAsImage(this Hand hand, bool hideFirstCard = false)
        {
            List<Image<Rgba32>> images = new();
            MemoryStream output = new();

            foreach (var card in hand.Cards)
            {
                if (images.Count == 0 && hideFirstCard)
                {
                    images.Add(await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(Path.Combine("Resources", "back.png")));
                    continue;
                }
                images.Add(await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(Path.Combine("Resources", card.GetImageName)));
            }

            // Height shouldn't change but width will
            // So let's just grab the first item in the hand
            var height = images.First().Height;
            var width = images.Sum(image => image.Width);
            using (Image<Rgba32> handImage = new(width, height))
            {
                handImage.Mutate(o =>
                {
                    for (var imgIndex = 0; imgIndex < images.Count; imgIndex++)
                    {
                        var img = images[imgIndex];
                        o.DrawImage(img, new Point(img.Width * imgIndex, 0), 1.0f);
                        img.Dispose();
                    }
                    o.Resize(width / 4, height / 4);
                });
                handImage.Save(output, new PngEncoder());
            }
            images.Clear();
            return output;
        }


    }
}
