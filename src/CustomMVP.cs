using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API;
using static Dapper.SqlMapper;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Attributes;
using Newtonsoft.Json;

namespace CustomMVP;

public class ConfigGen : BasePluginConfig
{
    [JsonPropertyName("DatabaseType")]
    public string DatabaseType { get; set; } = "SQLite";
    [JsonPropertyName("DatabaseFilePath")]
    public string DatabaseFilePath { get; set; } = "/csgo/addons/counterstrikesharp/plugins/CustomMVP/franug-CustomMVP-db.sqlite";
    [JsonPropertyName("DatabaseHost")]
    public string DatabaseHost { get; set; } = "";
    [JsonPropertyName("DatabasePort")]
    public int DatabasePort { get; set; }
    [JsonPropertyName("DatabaseUser")]
    public string DatabaseUser { get; set; } = "";
    [JsonPropertyName("DatabasePassword")]
    public string DatabasePassword { get; set; } = "";
    [JsonPropertyName("DatabaseName")]
    public string DatabaseName { get; set; } = "";
    [JsonPropertyName("DatabaseTable")]
    public string DatabaseTable { get; set; } = "mvpsounds";
    [JsonPropertyName("Comment")]
    public string Comment { get; set; } = "Use SQLite or MySQL as Database Type.";
}

[MinimumApiVersion(179)]
public class CustomMVP : BasePlugin, IPluginConfig<ConfigGen>
{
    public override string ModuleName => "Custom MVP Sound";
    public override string ModuleAuthor => "Franc1sco Franug";
    public override string ModuleVersion => "0.0.6";
    public ConfigGen Config { get; set; } = null!;
    public void OnConfigParsed(ConfigGen config) { Config = config; }
    internal static Dictionary<int, string?> gSelectedSong = new Dictionary<int, string?>();
    private MySQLStorage? storage;

    List<Songs> songs = new List<Songs>();



    public override void Load(bool hotReload)
    {
        storage = new MySQLStorage(
                Config.DatabaseHost,
                Config.DatabasePort,
                Config.DatabaseUser,
                Config.DatabasePassword,
                Config.DatabaseName,
                Config.DatabaseTable,
                Config.DatabaseType == "SQLite",
                Config.DatabaseFilePath
            );
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
                connectedUser(player);
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
            var emitsound = gSelectedSong[(int)player.Index];
            if (emitsound == null || emitsound == "none")
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
            info.DontBroadcast = true;
            return HookResult.Continue;
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

    [ConsoleCommand("css_xmvp", "Select MVP songs.")]
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
            _ = storage?.SetSound(player.SteamID, "none");
        });
        foreach (var song in songs)
        {
            menu.AddMenuOption(song.Name, (player, option) => {

                gSelectedSong[(int)player.Index] = song.Dir;
                _ = storage?.SetSound(player.SteamID, song.Dir);
            });
        }
        menu.PostSelectAction = PostSelectAction.Close;
        MenuManager.OpenChatMenu(player, menu);
    }

    private void connectedUser(CCSPlayerController player)
    {
        ulong steamID64 = player.SteamID;

        if (storage == null)
        {

            Console.WriteLine("Storage has not been initialized.");
            Console.WriteLine("Storage has not been initialized.");
            return;
        }

        Server.NextFrame(() =>
        {
            Task.Run(async () =>
            {
                await storage.FirstTimeRegister(steamID64);

            }).ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    Console.WriteLine($"Error registering player: {task.Exception}");
                    return;
                }
                var taskNext = Task.Run(async () => await storage.LoadPlayerFromDatabase(steamID64));
                taskNext.ContinueWith(task =>
                {
                    gSelectedSong[(int)player.Index] = task.Result;
                });
            });
        });
    }


    private bool IsValidClient(CCSPlayerController? client, bool isAlive = false)
    {
        return client != null && client.IsValid && client.PlayerPawn != null && client.PlayerPawn.IsValid && (!isAlive || client.PawnIsAlive) && !client.IsBot;
    }
}

public class MySQLStorage
{
    private string ip;
    private int port;
    private string user;
    private string password;
    private string database;
    private string table;
    private bool isSQLite;

    private MySqlConnection? conn;
    private SqliteConnection? connLocal;
    public MySQLStorage(string ip, int port, string user, string password, string database, string table, bool isSQLite, string sqliteFilePath)
    {
        string connectStr = $"server={ip};port={port};user={user};password={password};database={database};";
        this.ip = ip;
        this.port = port;
        this.user = user;
        this.password = password;
        this.database = database;
        this.table = table;
        this.isSQLite = isSQLite;

        if (isSQLite)
        {
            string dbFilePath = Server.GameDirectory + sqliteFilePath;

            var connectionString = $"Data Source={dbFilePath};";

            connLocal = new SqliteConnection(connectionString);

            connLocal.Open();

            var query = $@"
                CREATE TABLE IF NOT EXISTS `{table}` (
                steamid INTEGER PRIMARY KEY,
                sound TEXT NOT NULL DEFAULT 'none' 
                );";

            using (SqliteCommand command = new SqliteCommand(query, connLocal))
            {
                command.ExecuteNonQuery();
            }
            connLocal.Close();
        }
        else
        {
            conn = new MySqlConnection(connectStr);
            conn.Execute($@"
                CREATE TABLE IF NOT EXISTS `{table}` (
                    `steamid` varchar(64) NOT NULL PRIMARY KEY,
                    `sound` TEXT NOT NULL DEFAULT 'none'     
                );");
        }
    }
    public async Task FirstTimeRegister(ulong SteamID)
    {
        if (isSQLite)
        {
            if (connLocal == null)
            {
                Console.WriteLine("Error connection");
                return;
            }
            await connLocal.OpenAsync();
            var exists = await connLocal.QueryFirstOrDefaultAsync($"SELECT steamid FROM {table} WHERE steamid = @SteamID", new { SteamID });
            if (exists == null)
            {
                var query = $@"
        INSERT OR IGNORE INTO `{table}` (`steamid`) VALUES (@SteamID);
        ";
                var command = new SqliteCommand(query, connLocal);
                command.Parameters.AddWithValue("@SteamID", SteamID);
                await command.ExecuteNonQueryAsync();
            }
            connLocal.Close();
        }
        else
        {
            await conn!.OpenAsync();
            var exists = await conn.QueryFirstOrDefaultAsync($"SELECT `steamid` FROM `{table}` WHERE `steamid` = @SteamID", new { SteamID });

            if (exists == null)
            {
                var sql = $@"
        INSERT INTO `{table}` (`steamid`) VALUES (@SteamID) ON DUPLICATE KEY UPDATE `steamid` = @SteamID;
        ";
                await conn.ExecuteAsync(sql, new { SteamID });
            }
            conn.CloseAsync();
        }
    }
    public async Task SetSound(ulong steamID, string value)
    {
        if (isSQLite)
        {
            try
            {
                await connLocal.OpenAsync();
                var sql = $@"UPDATE {table} SET sound = @Value WHERE steamid = @SteamID;";
                await connLocal.ExecuteAsync(sql, new { SteamID = steamID, Value = value });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetSound: {ex.Message}");
            }
        }
        else
        {
            try
            {
                await conn.OpenAsync();
                var sql = $@"UPDATE `{table}` SET sound = @Value WHERE `steamid` = @SteamID;";
                await conn.ExecuteAsync(sql, new { SteamID = steamID, Value = value });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SetSound: {ex.Message}");
            }
            finally
            {
                await conn?.CloseAsync();
            }
        }

    }
    public async Task<String?> LoadPlayerFromDatabase(ulong steamID)
    {
        if (isSQLite)
        {
            try
            {
                await connLocal!.OpenAsync();
                var sql = $@"SELECT sound FROM {table} WHERE steamid = @SteamID;";
                var command = new SqliteCommand(sql, connLocal);
                command.Parameters.AddWithValue("@SteamID", steamID);
                var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    return Convert.ToString(reader["sound"]);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPlayerFromDatabase: {ex.Message}");
                return null; 
            }
            finally
            {
                await connLocal?.CloseAsync();
            }
        }
        else
        {
            try
            {
                await conn.OpenAsync();
                var sql = $@"SELECT sound FROM {table} WHERE steamid = @SteamID;";
                var command = new MySqlCommand(sql, conn);
                command.Parameters.AddWithValue("@SteamID", steamID);
                var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    return Convert.ToString(reader["sound"]);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPlayerFromDatabase: {ex.Message}");
                return null; 
            }
            finally
            {
                await conn?.CloseAsync();
            }
        }
    }

}

class Songs
{
    public string Name { get; set; }
    public string Dir { get; set; }
}

