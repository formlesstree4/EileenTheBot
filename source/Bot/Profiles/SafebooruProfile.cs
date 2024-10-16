using AutoMapper;
using Bot.Models.Booru;
using Bot.Models.Booru.Safebooru;

namespace Bot.Profiles
{

    public sealed class SafebooruProfile : Profile
    {

        public SafebooruProfile()
        {
            CreateMap<Post, EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.Owner) ? "N/A" : src.Owner))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => $"https://safebooru.org/images/{src.Directory}/{src.Image}"))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => $"https://safebooru.org/index.php?page=post&s=view&id={src.Id}"));
        }

    }

}