// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "I'm not fixing things I don't want to use right now", Scope = "member", Target = "~T:Bot.Models.Danbooru.Post")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "e621 is the proper convention", Scope = "member", Target = "~M:Bot.Modules.BooruModule.e621SearchAsync(System.String)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "I don't think I can mark these as static as the web builder stuff invokes them", Scope = "member", Target = "~M:Bot.Startup.ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection)")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "I don't think I can mark these as static as the web builder stuff invokes them", Scope = "member", Target = "~M:Bot.Startup.Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder,Microsoft.AspNetCore.Hosting.IWebHostEnvironment)")]
