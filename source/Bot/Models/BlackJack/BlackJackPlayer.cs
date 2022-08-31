using Discord;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bot.Models.BlackJack
{

    public sealed class BlackJackPlayer
    {

        /// <summary>
        ///     Gets the current User
        /// </summary>
        public EileenUserData User { get; private set; }

        /// <summary>
        ///     Gets the User's hand
        /// </summary>
        public List<Card> Hand { get; private set; } = new();

        /// <summary>
        ///     Indicates if this is the Dealer or not
        /// </summary>
        public bool IsDealer => User == null;


        /// <summary>
        ///     Calculates the current value of the <see cref="Hand"/>
        /// </summary>
        /// <returns></returns>
        public int Value
        {
            get
            {
                var total = Hand.Where(c => c.Face != Face.Ace).Sum(c => c.Value);
                var aceCount = Hand.Count(c => c.Face == Face.Ace);

                // This part is fun. You have to brain for a minute. I didn't when this was first written.
                // Here's the steps of ace handling.
                //  1) If we have no aces, return the calculated total.
                //  2) If the total, plus 10, plus ace count is greater than 21, return total + acecount.
                //      This fucking step might throw you for a bit. But that's OK. It threw me and my buddy here
                //      had to slowdown to fuckin turtle speed to explain why this was OK. This line basically
                //      treats ALL aces as one. It is pretty fucking obvious now but because I'm mentally handicapped
                //      it wasn't at first. Jesus Christ I need more alcohol (actually in hindsight, I need less).
                //  3) The last line is just the false part. Basically, treat the first ace like 11 and add 1 for all remainders. Magic.
                //      The others get treated like one automatically due to blackjack rules. You can choose if aces are 1 or 11 unless
                //      making them both 11 busts you. In which case you're retarded and the rules save you from fucking yourself in the ass.
                //      So we treat all remainder aces as 1. Get used to it. (11 * 2 > 21 = you busting like an idiot).
                if (aceCount == 0) return total;
                if (total + 10 + aceCount > 21) return total + aceCount;
                return total + 10 + aceCount;
            }
        }

        /// <summary>
        ///     Gets whether or not this hand is a bust.
        /// </summary>
        public bool IsBust
        {
            get
            {
                return Value > 21;
            }
        }

        /// <summary>
        ///     Gets whether this hand is blackjack or not.
        /// </summary>
        public bool IsBlackJack
        {
            get
            {
                return Hand.Count == 2 && Value == 21;
            }
        }

        /// <summary>
        ///     Gets whether this Hand can be split into two hands.
        /// </summary>
        public bool IsSplittable
        {
            get
            {
                return Hand.Count == 2 && Hand[0].Face == Hand[1].Face;
            }
        }

        /// <summary>
        ///     Gets or sets the player's bet
        /// </summary>
        public ulong Bet { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether this was from a split
        /// </summary>
        public bool IsFromSplit { get; } = false;



        /// <summary>
        ///     Creates a new Player object
        /// </summary>
        /// <param name="user"></param>
        public BlackJackPlayer(EileenUserData user, bool isFromSplit = false)
        {
            User = user;
            IsFromSplit = isFromSplit;
        }



        /// <summary>
        ///     Converts the player hand into attachments to be sent through Discord
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<FileAttachment>> GetHandAsAttachment()
        {
            List<Image<Rgba32>> images = new();
            MemoryStream output = new ();

            foreach (var hand in Hand)
            {
                images.Add(await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(Path.Combine("Resources", hand.GetImageName)));
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
            return new[] { new FileAttachment(output, "hand.png") };
        }

        /// <summary>
        ///     Converts the hand to a representation
        /// </summary>
        /// <returns><see cref="string"/></returns>
        public string GetHandAsString()
        {
            if (Hand.Count == 0) return "";
            if (Hand.Count == 1) return Hand[0].GetDisplayName;
            if (Hand.Count == 2) return $"{Hand[0].GetDisplayName} and {Hand[1].GetDisplayName}";
            return $"{string.Join(", ", Hand.Take(Hand.Count - 1).Select(h => h.GetDisplayName))}, and {Hand.TakeLast(1).First().GetDisplayName}";
        }

    }
}
