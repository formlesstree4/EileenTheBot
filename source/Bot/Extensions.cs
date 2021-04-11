using System.Collections.Generic;
using Discord.Commands;

namespace Bot
{
    public static class Extensions
    {
        public static string GetFullCommandPath(this CommandInfo command)
        {
            var path = new Stack<string>();
            path.Push(command.Name);

            var moduleInfo = command.Module;

            while(moduleInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(moduleInfo.Group))
                {
                    path.Push(moduleInfo.Group);
                }
                moduleInfo = moduleInfo.Parent;
            }

            return string.Join(" ", path);
        }
    }
}