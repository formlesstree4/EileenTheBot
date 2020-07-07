using AutoMapper;

namespace Bot.Profiles
{

    public sealed class GelbooruProfile: Profile
    {

        public GelbooruProfile()
        {
            CreateMap<Models.Gelbooru.Post, Models.EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Owner) ? "N/A" : src.Owner))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.Image))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => $"https://gelbooru.com/index.php?page=post&s=view&id={src.Id}"));
        }

    }

}