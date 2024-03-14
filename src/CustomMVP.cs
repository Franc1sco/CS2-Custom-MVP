using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using Newtonsoft.Json;

namespace CustomMVP;


[MinimumApiVersion(179)]
public class CustomMVP : BasePlugin
{
    public override string ModuleName => "Custom MVP Sound";
    public override string ModuleAuthor => "Franc1sco Franug";
    public override string ModuleVersion => "0.0.1";
    internal static Dictionary<int, string?> gSelectedSong = new Dictionary<int, string?>();

    List<Songs> songs = new List<Songs>();



    public override void Load(bool hotReload)
    {
        loadList();
        if (hotReload)
        {
            Utilities.GetPlayers().ForEach(player =>
            {
                if (!player.IsBot)
                {
                    gSelectedSong.Add((int)player.Index, null);
                }
            });
        }

        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            var player = @event.Userid;

            if (player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                gSelectedSong.Add((int)player.Index, null);
                return HookResult.Continue;
            }
        });

        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;

            if (player.IsBot || !player.IsValid)
            {
                return HookResult.Continue;

            }
            else
            {
                if (gSelectedSong.ContainsKey((int)player.Index))
                {
                    gSelectedSong.Remove((int)player.Index);
                }
                return HookResult.Continue;
            }
        });

        RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
        {
            foreach (var song in songs)
            {
                manifest.AddResource(song.Dir);
            }
        });

        RegisterEventHandler<EventRoundMvp>((@event, info) =>
        {
            var player = @event.Userid;
            if (!IsValidClient(player))
            {
                return HookResult.Continue;
            }
            var nomusic = @event.Nomusic;
            nomusic = 1;
            var emitsound = gSelectedSong[(int)player.Index];
            if (emitsound == null)
            {
                Random random = new Random();
                int randomIndex = random.Next(0, songs.Count);
                emitsound = songs[randomIndex].Dir;
            }
            AddTimer(0.2f, () =>
            {
                Utilities.GetPlayers().ForEach(players =>
                {
                    if (!players.IsBot)
                    {
                        players.ExecuteClientCommand($"play {emitsound}");
                    }
                });
            });
            return HookResult.Changed;
        }, HookMode.Pre);
    }

    private void loadList()
    {
        var filePath = Server.GameDirectory + $"/csgo/addons/counterstrikesharp/plugins/CustomMVP/songs.json";
        songs = readFile(filePath);
    }

    private List<Songs> readFile(string nombreArchivo)
    {
        List<Songs> canciones = new List<Songs>();

        try
        {
            string json = File.ReadAllText(nombreArchivo);
            canciones = JsonConvert.DeserializeObject<List<Songs>>(json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error reading JSON: " + e.Message);
        }

        return canciones;
    }

    [ConsoleCommand("css_mvp", "Select MVP songs.")]
    [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void setupMainMenu(CCSPlayerController? player, CommandInfo info)
    {
        if (!IsValidClient(player))
        {
            return;
        }

        setupMenu(player);
    }

    private void setupMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("MVP Sounds");
        menu.AddMenuOption("Random sound", (player, option) => {

            gSelectedSong[(int)player.Index] = null;
            player.PrintToChat("Set MVP song to Random");
        });
        foreach (var song in songs)
        {
            menu.AddMenuOption(song.Name, (player, option) => {

                gSelectedSong[(int)player.Index] = song.Dir;
                player.PrintToChat("Set MVP song to " + song.Name);
            });
        }
        menu.PostSelectAction = PostSelectAction.Close;
        MenuManager.OpenChatMenu(player, menu);
    }

    private bool IsValidClient(CCSPlayerController? client, bool isAlive = false)
    {
        return client != null && client.IsValid && client.PlayerPawn != null && client.PlayerPawn.IsValid && (!isAlive || client.PawnIsAlive) && !client.IsBot;
    }
}

class Songs
{
    public string Name { get; set; }
    public string Dir { get; set; }
}

