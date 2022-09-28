using AutoMapper;
using Bot.Models.Booru;

namespace Bot.Profiles
{

    public sealed class DanbooruProfile : Profile
    {

        public DanbooruProfile()
        {
            CreateMap<Models.Danbooru.Post, EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => string.IsNullOrEmpty(src.tag_string_artist) ? "N/A" : src.tag_string_artist))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.GetDownloadUrl()))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => src.GetPostUrl()));
        }

    }

}