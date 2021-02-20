# Eileen

Eileen is a Discord bot that I've been working on for a decent amount of time that has a small number of very useless, but enjoyable, features.

If you want to add this Bot to your own Server, hit me up on Discord: formlesstree4#2035

# Features
Some features are fully fleshed out while others are still a bit of a work in progress. For that, I'm sorry. That's just the nature of the beast. This bot has generally been my playground for fun new features to attempt and thus far I tend to jump around all over the place with no regard for anything.

1. Multiple Booru Support
1. Dungeoneering Mini-Game (similar to the Munchkin card game)
1. Markov Chain discussions when you say the key phrase (erector)
1. A currency system that will eventually tie into other features of the Bot
1. Command Permissions (restricted to Dungeoneering but the framework is in place for others)
1. Background Job running
1. Eventually more stuff

## Booru
The Booru system actually supports several different Booru sites:
- Danbooru
- Gelbooru
- e621
- SafeBooru
- Yandere

The BooruService base class makes it fairly easy to add more Boorus that follow the same sort of setup like Danbooru.

## Dungeoneering
The mini-game of Dungeoneering is a simplistic infinitely scaling mini-game where you currently can simply fight monsters and level up.

Each time you beat a Monster you level up until you reach a level cap. At that level cap you can prestige. The prestige system is very similar to CoD where your level resets but you get a prestige counter. This prestige counter means you'll get better loot percentages (marginal). And the progress carries over across servers.

Eventually, similarly to Munchkin (the inspiration for this mini-game), other Users on the same server will be able to assist you or the Monster you're fighting by providing temporary buffs (via equipment).

There's no theoretical cap to the system right now. However, at some point, engaging in fights will being to cost currency which means any excess gear / loot not being used should be sold!

Long term goals are to put items up into an auction house to attempt to milk higher than market value out of a rare item drop.

## Auto-Responses
The Bot supports two methodologies for responding to incoming requests:
1. GPT
1. Markov

Currently only one server uses GPT due to the fact that it's a huge pain in the ass to train a dataset and it's extremely slow. And that server is my Discord server. And it will probably stay that way until I get a server farm. Which is probably never.

## Currency
I mentioned Currency once or twice earlier. But effectively, the Currency system is sort of like this passive system that allows you to acquire levels via money! And no, not your actual monies but this fake currency that the Bot gives a damn about. It will eventually be integral to several games but needless to say you need to carefully manage your wallet in order to grow it correctly.

Currently, your level dictates your MAX amount of currency you can have. If something happens and you get a huge windfall of currecy, you're boned if your wallet can't hold it all. This is not the permanent system and I'm working on designing something that's not crap but that's how it is for now.

For now you passively earn currency and you can also acquire more by using the `.dailyc` command.

Somewhere I'll document all the commands for this bot.

## Command Permissions
Some more complex modules (such as Dungeoneering) require your permission to run in specific channels. The entire `.dungeoneering` module is one instance where that needs to happen. A server owner / admin can allow the command to run in a channel by using a specific administrative command.

# ToDo
There's a lot currently on the docket for this Bot, such as:

1. A more fully fledged implementation of Dungeoneering. Things like server-wide raids, PVP, and more
1. A text-based pseudo-RPG system (think like Zork)
1. A casino system (Blackjack, Texas Hold 'Em, slot machines, and more)
1. Fully document all commands in a cohesive manner as there are a lot of them and quite a few of them are useless to most people