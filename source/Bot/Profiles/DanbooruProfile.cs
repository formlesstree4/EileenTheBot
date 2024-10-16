using AutoMapper;
using Bot.Models.Booru;
using Bot.Models.Booru.Danbooru;

namespace Bot.Profiles
{

    public sealed class DanbooruProfile : Profile
    {

        public DanbooruProfile()
        {
            CreateMap<Post, EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => string.IsNullOrEmpty(src.tag_string_artist) ? "N/A" : src.tag_string_artist))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.GetDownloadUrl()))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => src.GetPostUrl()));
        }

    }

}