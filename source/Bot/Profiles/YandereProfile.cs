using AutoMapper;
using Bot.Models.Booru;
using Bot.Models.Booru.Yandere;

namespace Bot.Profiles
{
    public sealed class YandereProfile : Profile
    {
        public YandereProfile()
        {
            CreateMap<Post, EmbedPost>()
                .ForMember(dest => dest.ArtistName, opt => opt.MapFrom(src => string.IsNullOrWhiteSpace(src.author) ? "N/A" : src.author))
                .ForMember(dest => dest.ImageUrl, opt => opt.MapFrom(src => src.file_url))
                .ForMember(dest => dest.PageUrl, opt => opt.MapFrom(src => $"https://yande.re/post/show/{src.id}"));
        }

    }

}