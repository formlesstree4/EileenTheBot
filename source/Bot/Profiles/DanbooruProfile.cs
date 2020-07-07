using AutoMapper;

namespace Bot.Profiles
{

    public sealed class DanbooruProfile: Profile
    {

        public DanbooruProfile()
        {
            CreateMap<Models.Danbooru.Post, Models.EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => src.tag_string_artist ?? "N/A"))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.GetDownloadUrl()))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => src.GetPostUrl()));
        }

    }

}