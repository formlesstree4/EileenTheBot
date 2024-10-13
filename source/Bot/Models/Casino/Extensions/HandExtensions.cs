using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bot.Models.Casino.Extensions
{
    public static class HandExtensions
    {

        /// <summary>
        /// Gets a single image file that is the given <see cref="CasinoHand"/>
        /// </summary>
        /// <returns>A promise to create a <see cref="FileAttachment"/></returns>
        public static async Task<FileAttachment> GetHandAsAttachment(this CasinoHand hand, bool hideFirstCard = false)
        {
            var imageStream = await hand.GetHandAsImage(hideFirstCard);
            return new FileAttachment(imageStream, "hand.png");
        }

        /// <summary>
        /// Gets a <see cref="Stream"/> that contains the image data for the given <see cref="CasinoHand"/>
        /// </summary>
        /// <param name="hand">The <see cref="CasinoHand"/> to create an image of</param>
        /// <param name="hideFirstCard">If true, the first card will be hidden. This is typically used for the dealer</param>
        /// <returns>A promise to create a <see cref="Stream"/> that contains the appropriate image data</returns>
        private static async Task<Stream> GetHandAsImage(this CasinoHand hand, bool hideFirstCard = false)
        {
            var hideMask = Enumerable.Repeat<byte>(0, hand.Cards.Count).ToArray();
            if (hideFirstCard) hideMask[0] = 1;
            return await hand.GetHandAsImage(hideMask);
        }

        /// <summary>
        /// Gets a <see cref="Stream"/> that contains the image data for the given <see cref="CasinoHand"/>
        /// </summary>
        /// <param name="hand">The <see cref="CasinoHand"/> to create an image of</param>
        /// <param name="cardsToHide">A byte array that is the same size as <see cref="CasinoHand.Cards"/> where if the matching index equals to 1, the card is hidden</param>
        /// <returns>A promise to create a <see cref="Stream"/> that contains the appropriate image data</returns>
        private static async Task<Stream> GetHandAsImage(this CasinoHand hand, byte[] cardsToHide)
        {
            if (cardsToHide.Length != hand.Cards.Count) throw new ArgumentException($"Argument size mismatch - {nameof(cardsToHide)} ({cardsToHide.Length}) must be equal in size to number of cards in {nameof(hand)} ({hand.Cards.Count})!", nameof(cardsToHide));
            List<Image<Rgba32>> images = new();
            MemoryStream output = new();

            Image<Rgba32> backImage = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(Path.Combine("Resources", "Cards", "Basic", "back.png"));
            Dictionary<string, Image<Rgba32>> cachedImages = new();

            var hideIndex = 0;
            foreach (var card in hand.Cards)
            {
                if (cardsToHide[hideIndex] == 1)
                {
                    images.Add(backImage);
                }
                else
                {
                    var key = card.GetImageName;
                    if (!cachedImages.ContainsKey(key))
                    {
                        cachedImages.Add(key, await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(Path.Combine("Resources", "Cards", "Basic", card.GetImageName)));
                    }
                    images.Add(cachedImages[key]);
                }
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
                    }
                    o.Resize(width / 4, height / 4);
                });
                await handImage.SaveAsync(output, new PngEncoder());
            }
            foreach (var image in cachedImages.Values) image.Dispose();
            backImage.Dispose();
            return output;
        }

    }
}
