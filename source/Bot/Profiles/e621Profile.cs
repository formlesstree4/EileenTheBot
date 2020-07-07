using System.Linq;
using AutoMapper;

namespace Bot.Profiles
{

    public sealed class e621Profile: Profile
    {

        public e621Profile()
        {
            CreateMap<Models.e621.Post, Models.EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => src.Tags.Artist.Any() ? string.Join(",", src.Tags.Artist) : "N/A"))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.File.Url))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => src.GetPostUrl()));
        }

    }

}