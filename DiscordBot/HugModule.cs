using Discord.Commands;

namespace DiscordBot
{
    public class HugModule : ModuleBase<SocketCommandContext>
    {
        [Command("hugme")]
        [Summary("Hugs you!")]
        public async Task HugAsync()
        {
            await ReplyAsync($"{Context.User.Username} :people_hugging:");
        }
    }
}
