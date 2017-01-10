using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleNET;
using DaRT.Properties;
using System.Threading;
using System.Net;
using System.Drawing;
using System.Text;

namespace DaRT
{
    public class RCon
    {
        private BattlEyeClient _client;
        private BattlEyeLoginCredentials _credentials;
        private bool _error = false;
        private bool _initialized = false;
        private bool _reconnecting = false;

        private HashSet<string> _filters;

        private String _pending = "";
        private bool _pendingLeft = false;

        private List<int> _sent = new List<int>();
        private Dictionary<int, string> _received = new Dictionary<int, string>();
        private GUImain _form;
        private List<String> _admins = new List<String>();
        private List<Player> _players = new List<Player>();
        private List<Ban> _bans = new List<Ban>();
        private string[] _hilight;

        public bool Connected
        {
            get { return _initialized && _client.Connected && !_reconnecting; }
        }
        public bool Error
        {
            get { return _initialized && _error; }
        }

        public string Pending
        {
            get { return _pending; }
            set { _pending = value; }
        }
        public bool PendingLeft
        {
            get { return _pendingLeft; }
            set { _pendingLeft = value; }
        }

        public bool Reconnecting
        {
            get { return _reconnecting; }
            set { _reconnecting = value; }
        }

        public RCon(GUImain form)
        {
            _form = form;

            // Initializing date
            this.lastsent = new DateTime();

            // Initializing filters
            _filters = new HashSet<string>();

            string[] filters = Settings.Default.filters.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string filter in filters)
            {
                _filters.Add(filter);
            }
        }

        private DateTime lastsent;

        private int Send(BattlEyeCommand command)
        {
            // Prevent sending packets too fast if necessary
            while ((DateTime.Now - lastsent).TotalMilliseconds <= 10) { Thread.Sleep(10); }

            // Sending command and saving timestamp
            int id = _client.SendCommand(command);
            lastsent = DateTime.Now;

            // Logging sent packet
            if (!_sent.Contains(id))
                _sent.Add(id);
            return id;
        }

        private void Received(int id, string response)
        {
            // Logging received packet
            if (!_received.ContainsKey(id))
            {
                _received.Add(id, response);
            }
        }
        private string GetResponse(int id)
        {
            // Polling for response
            if (_received.ContainsKey(id))
            {
                string response = _received[id];
                this.Remove(id);

                return response;
            }
            else
                return null;
        }
        private bool Remove(int id)
        {
            // Removing packet
            if (_sent.Contains(id))
            {
                _sent.Remove(id);
            }
            else
            {
                if (_received.ContainsKey(id))
                    _received.Remove(id);

                return false;
            }

            if (_received.ContainsKey(id))
            {
                _received.Remove(id);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Connect(IPAddress host, int port, string password)
        {
            _credentials = new BattlEyeLoginCredentials
            {
                Host = host,
                Port = port,
                Password = password
            };

            _client = new BattlEyeClient(_credentials);
            _client.BattlEyeMessageReceived += HandleMessage;
            _client.BattlEyeConnected += HandleConnect;
            _client.BattlEyeDisconnected += HandleDisconnect;
            _client.ReconnectOnPacketLoss = false;

            _initialized = true;
            _client.Connect();
            string[] strArray = new string[Settings.Default.hilight.Count];
            Settings.Default.hilight.CopyTo(strArray, 0);

            _hilight = strArray;

        }
        public void Disconnect()
        {
            _client.Disconnect();
            _initialized = false;
        }

        public List<Player> getPlayers()
        {

            int id = this.Send(BattlEyeCommand.Players);

            string response;
            int ticks = 0;
            while ((response = this.GetResponse(id)) == null && ticks < Settings.Default.playerTicks)
            {
                Thread.Sleep(10);
                ticks++;
            }

            if (response == null)
            {
                if(_players.Count > 0) return _players;
                if (!_reconnecting)
                    _form.Log(Resources.Strings.Player_timeout, LogType.Console, false);
                return _players;
            }
            this.parsePlayers(response);

            return _players;
        }
        public List<Ban> getBans()
        {
            int id = this.Send(BattlEyeCommand.Bans);

            string response;
            int ticks = 0;
            while ((response = this.GetResponse(id)) == null && ticks < Settings.Default.banTicks)
            {
                Thread.Sleep(10);
                ticks++;
            }
            
            if (response == null)
            {
                if (_bans.Count > 0) return _bans;
                if (!_reconnecting)
                    _form.Log(Resources.Strings.Ban_timeout, LogType.Console, false);
                return _bans;
            }
            this.parseBans(response);
            return _bans;
        }
        public List<String> getAdmins()
        {

            int id = this.Send(BattlEyeCommand.Admins);

            string response;
            int ticks = 0;
            while ((response = this.GetResponse(id)) == null && ticks < Settings.Default.playerTicks)
            {
                Thread.Sleep(10);
                ticks++;
            }

            if (response == null)
            {
                if (!_reconnecting)
                    _form.Log(Resources.Strings.Admin_timeout, LogType.Console, false);
                return _admins;
            }

            this.parseAdmins(response);

            return _admins;
        }

        private void parseAdmins(String message)
        {
            StringReader reader = new StringReader(message);
            _admins.Clear();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                String[] row = line.Split(' ');
                int id;
                if(row.Count() >= 2 && int.TryParse(row[0],out id))_admins.Add(row[1]);
            }
        }
        private void parsePlayers(String response)
        {
            _players.Clear();
            _form.Log("reciving players", LogType.Debug, false);
            using (StringReader reader = new StringReader(response))
            {
                string line;
                int row = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    row++;
                    if (row > 3 && !line.StartsWith("(") && line.Length > 0)
                    {
                        String[] items = line.Split(new char[] { ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                        int number;
                        if (items.Length == 5 && int.TryParse(items[0],out number))
                        {
                            String ip = items[1].Split(':')[0];
                            String ping = items[2];
                            String guid = items[3].Replace("(OK)", "").Replace("(?)", "");
                            String name = items[4];
                            String status = "Unknown";

                            if (guid.Length == 32)
                            {
                                if (guid == "-")
                                {
                                    status = "Initializing";
                                }

                                if (name.EndsWith(" (Lobby)"))
                                {
                                    name = name.Replace(" (Lobby)", "");
                                    status = "Lobby";
                                }
                                else
                                    status = "Ingame";

                                _players.Add(new Player(number, ip, ping, guid, name, status));
                            }
                        }
                    }
                }
            }
        }
        public void parseBans(String response)
        {
            _bans.Clear();
            _form.Log("reciving bans", LogType.Debug, false);
            using (StringReader reader = new StringReader(response))
            {
                String line;
                int row = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    row++;
                    if (row > 3 && !line.StartsWith("IP Bans:") && !line.StartsWith("[#]") && !line.StartsWith("----------------------------------------------") && line.Length > 0)
                    {
                        String[] items = line.Split(new char[] { ' ' }, 4, StringSplitOptions.RemoveEmptyEntries);

                        if (items.Length == 4)
                        {
                            String number = items[0];
                            String ipguid = items[1];
                            String time = items[2];
                            String reason = items[3];

                            if (time == "-")
                                time = "expired";

                            _bans.Add(new Ban(number, ipguid, time, reason));
                        }
                        else if (items.Length == 3)
                        {
                            String number = items[0];
                            String ipguid = items[1];
                            String time = items[2];

                            if (time == "-")
                                time = "expired";

                            _bans.Add(new Ban(number, ipguid, time, "(No reason)"));
                        }
                    }
                }
            }
        }

        private void checkMessage(string message, bool ban)
        {
            string[] check;
            if (ban)
            {
                check = new string[Settings.Default.autoBan.Count];
                Settings.Default.autoBan.CopyTo(check, 0);
            } else
            {
                check = new string[Settings.Default.autoKick.Count];
                Settings.Default.autoKick.CopyTo(check, 0);
            }
            foreach(string item in check)
            {
                bool found = false;
                string msg = "";
                try
                {
                    System.Text.RegularExpressions.Match r = new System.Text.RegularExpressions.Regex(item.ToLower()).Match(message.ToLower());
                    found = item.Length > 3 && r.Success;
                    if (found && r.Groups.Count > 1) msg = r.Groups[1].Value; else msg=r.Value;
                }
                catch
                {
                    found = false;
                }
                if (found)
                {
                    string name = message.Split(' ')[1].Replace(":", "");
                    Player player = null;

                    foreach(Player p in _players)
                        if (!name.Equals("") && p.name.Equals(name)) { player = p; break; }

                    if(player != null)
                    {
                        if (ban)
                        {
                            _form.Log(String.Format(Resources.Strings.Autoban_for ,player.name ,item), LogType.Console, true);
                            Ban _ban = new Ban(player.number, player.name, player.guid, "", 0, String.Format(Resources.Strings.Autoban_for,"",msg) , true);
                            this.Ban(_ban);
                        }
                        else
                        {
                            _form.Log(String.Format(Resources.Strings.Autokick_for,player.name,item), LogType.Console, true);
                            Kick _kick = new Kick(player.number, player.name, String.Format(Resources.Strings.Autokick_for,"",msg));
                            this.kick(_kick);
                        }
                    }
                    break;
                }
                
            }

        }

        public void scripts()
        {
            _client.SendCommand("loadScripts");
            if(_form != null) _form.Log(Resources.Strings.Server_relscripts, LogType.Console, false);
        }
        public void bans()
        {
            _client.SendCommand("loadBans");
            if(_form != null) _form.Log(Resources.Strings.Server_relbans, LogType.Console, false);
        } 
        public void events()
        {
            _client.SendCommand("loadEvents");
            if(_form != null) _form.Log(Resources.Strings.Server_relevent, LogType.Console, false);
        }
        public void lockServer()
        {
            _client.SendCommand("#lock");
            if(_form != null) _form.Log(Resources.Strings.Server_locked, LogType.Console, false);
        }
        public void unlockServer()
        {
            _client.SendCommand("#unlock");
            if (_form != null) _form.Log(Resources.Strings.Server_unlocked, LogType.Console, false);
        }
        public void shutdown()
        {
            _client.SendCommand("#shutdown");
            if (_form != null) _form.Log(Resources.Strings.Server_shutdown, LogType.Console, false);
        }
        public void execute(String command)
        {
            _client.SendCommand(command);
            if (_form != null) _form.Log(command, LogType.Console, false);
        }
        public void sayPrivate(Message message)
        {
            string name = "";
            if (!string.IsNullOrEmpty(Settings.Default.name))
                name = "[" + Settings.Default.name + "] ";

            _client.SendCommand(BattlEyeCommand.Say, message.id + " " + name + message.message);
        }
        public void say(String message)
        {
            string name = "";
            if (!string.IsNullOrEmpty(Settings.Default.name))
                name = "[" + Settings.Default.name + "] ";

            _client.SendCommand(BattlEyeCommand.Say, "-1 " + name + message);
        }
        public void kick(Kick kick)
        {
            string name = "";
            if (!string.IsNullOrEmpty(Settings.Default.name))
                name = "[" + Settings.Default.name + "] ";

            _client.SendCommand(BattlEyeCommand.Kick, kick.id + " " + name + kick.reason);
            if (_form != null) _form.Log(String.Format(Resources.Strings.Kicked, kick.name), LogType.Console, false);
        }
        public void Execute(string command)
        {
            _client.SendCommand(command);
        }
        public void Ban(Ban ban)
        {
            string name = "";
            if (!string.IsNullOrEmpty(Settings.Default.name))
                name = "[" + Settings.Default.name + "] ";

            if (ban.Online)
            {
                if (!string.IsNullOrEmpty(ban.GUID) && !string.IsNullOrEmpty(ban.IP))
                {
                    _client.SendCommand(string.Format("banIP {0} {1} {2}", ban.ID, ban.Duration, name + ban.Reason));
                    _client.SendCommand(string.Format("addBan {0} {1} {2}", ban.GUID, ban.Duration, name + ban.Reason));
                }
                else if (!string.IsNullOrEmpty(ban.GUID))
                    _client.SendCommand(string.Format("ban {0} {1} {2}", ban.ID, ban.Duration, name + ban.Reason));
                else if (!string.IsNullOrEmpty(ban.IP))
                    _client.SendCommand(string.Format("banIP {0} {1} {2}", ban.ID, ban.Duration, name + ban.Reason));

                if (_form != null) _form.Log(String.Format(Resources.Strings.Banned, ban.Name), LogType.Console, false);
            }
            else
            {
                _client.SendCommand(string.Format("addBan {0} {1} {2}", ban.GUID, ban.Duration, name + ban.Reason));

                if (_form != null) _form.Log(String.Format(Resources.Strings.Banned ,ban.Name), LogType.Console, false);
            }
        }
        public void banIP(BanIP ban)
        {
            _client.SendCommand("banIP " + ban.id + " " + ban.duration + " " + ban.reason);
            if (_form != null) _form.Log(String.Format(Resources.Strings.Banned, ban.name), LogType.Console, false);
        }
        public void banOffline(BanOffline ban)
        {
            _client.SendCommand("addBan " + ban.guid + " " + ban.duration + " " + ban.reason);
            if (ban.name != "")
            {
                if (_form != null) _form.Log(String.Format(Resources.Strings.Banned, ban.name), LogType.Console, false);
            }
            else
            {
                if (_form != null) _form.Log(String.Format(Resources.Strings.Banned, ban.guid), LogType.Console, false);
            }
        }
        public void unban(String id)
        {
            _client.SendCommand("removeBan " + id);
            if (_form != null) _form.Log(Resources.Strings.Ban_removed, LogType.Console, false);
        }

        private void HandleMessage(BattlEyeMessageEventArgs args)
        {
            string message = args.Message;
            if (_initialized)
            {
                if (message.Contains("Connected RCon admins:")) this.parseAdmins(message);
                if (message.Contains("Players on server:")) this.parsePlayers(message);
                if (message.StartsWith("GUID Bans:")) this.parseBans(message);
                if (Settings.Default.autoKicks && message.StartsWith("(")) this.checkMessage(message, false);
                if (Settings.Default.autoBans && message.StartsWith("(")) this.checkMessage(message, true);
                // Message filtering
                if (args.Id != 256)
                    this.Received(args.Id, message);
                else
                {
                    if (_form != null)
                    {
                        // Global chat
                        if (message.StartsWith("(Global)"))
                        {
                            if (Settings.Default.showGlobalChat)
                                _form.Log(message, LogType.GlobalChat, this.IsCall(message));
                        }
                        // Side chat
                        else if (message.StartsWith("(Side)"))
                        {
                            if (Settings.Default.showSideChat)
                                _form.Log(message, LogType.SideChat, this.IsCall(message));
                        }
                        // Direct chat
                        else if (message.StartsWith("(Direct)"))
                        {
                            if (Settings.Default.showDirectChat)
                                _form.Log(message, LogType.DirectChat, this.IsCall(message));
                        }
                        // Vehicle chat
                        else if (message.StartsWith("(Vehicle)"))
                        {
                            if (Settings.Default.showVehicleChat)
                                _form.Log(message, LogType.VehicleChat, this.IsCall(message));
                        }
                        // Command chat
                        else if (message.StartsWith("(Command)"))
                        {
                            if (Settings.Default.showCommandChat)
                                _form.Log(message, LogType.CommandChat, this.IsCall(message));
                        }
                        // Group chat
                        else if (message.StartsWith("(Group)"))
                        {
                            if (Settings.Default.showGroupChat)
                                _form.Log(message, LogType.GroupChat, this.IsCall(message));
                        }
                        // Unknown chat
                        else if (message.StartsWith("(Unknown)"))
                        {
                            if (Settings.Default.showUnknownChat)
                                _form.Log(message, LogType.UnknownChat, this.IsCall(message));
                        }
                        else if (message.StartsWith("Player #"))
                        {
                            if (_pending != "" && message.EndsWith(" " + _pending + " disconnected"))
                                _pendingLeft = true;

                            if (Settings.Default.refreshOnJoin && message.EndsWith("disconnected") && !_form.pendingPlayers)
                            {
                                Thread thread = new Thread(new ThreadStart(_form.thread_Player));
                                thread.IsBackground = true;
                                thread.Start();
                            }

                            // Connect/disconnect/kick/ban messages
                            if (Settings.Default.showPlayerConnectMessages)
                                _form.Log(message, LogType.Console, false);
                        }
                        else if (message.StartsWith("Verified GUID ("))
                        {
                            // GUID verification messages
                            if (Settings.Default.showVerificationMessages)
                                _form.Log(message, LogType.Console, false);

                            if (Settings.Default.refreshOnJoin && !_form.pendingPlayers)
                            {
                                Thread thread = new Thread(new ThreadStart(_form.thread_Player));
                                thread.IsBackground = true;
                                thread.Start();
                            }
                        }
                        else if (message.StartsWith("RCon admin #"))
                        {
                            // Admin login
                            if (Settings.Default.showAdminMessages && message.EndsWith("logged in"))
                                _form.Log(message, LogType.Console, false);
                            else if (Settings.Default.showAdminChat)
                                _form.Log(message, LogType.AdminChat, false);
                        }
                        else if (message.StartsWith("Failed to open") || message.StartsWith("Incompatible filter file"))
                        {
                            // Log errors
                            if (Settings.Default.showLogErrors)
                                _form.Log(message, LogType.Console, false);
                        }
                        // Scripts log
                        else if (message.StartsWith("Scripts Log:"))
                        {
                            if (Settings.Default.showScriptsLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.ScriptsLog, false);
                        }
                        // CreateVehicle log
                        else if (message.StartsWith("CreateVehicle Log:"))
                        {
                            if (Settings.Default.showCreateVehicleLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.CreateVehicleLog, false);
                        }
                        // DeleteVehicle log
                        else if (message.StartsWith("DeleteVehicle Log:"))
                        {
                            if (Settings.Default.showDeleteVehicleLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.DeleteVehicleLog, false);
                        }
                        // PublicVariable log
                        else if (message.StartsWith("PublicVariable Log:"))
                        {
                            if (Settings.Default.showPublicVariableLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.PublicVariableLog, false);
                        }
                        // PublicVariableVal log
                        else if (message.StartsWith("PublicVariable Value Log:"))
                        {
                            if (Settings.Default.showPublicVariableValLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.PublicVariableValLog, false);
                        }
                        // RemoteExec log
                        else if (message.StartsWith("RemoteExec Log:"))
                        {
                            if (Settings.Default.showRemoteExecLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.RemoteExecLog, false);
                        }
                        // RemoteControl log
                        else if (message.StartsWith("RemoteControl Log:"))
                        {
                            if (Settings.Default.showRemoteControlLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.RemoteControlLog, false);
                        }
                        // SetDamage log
                        else if (message.StartsWith("SetDamage Log:"))
                        {
                            if (Settings.Default.showSetDamageLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.SetDamageLog, false);
                        }
                        // SetVariable log
                        else if (message.StartsWith("SetVariable Log:"))
                        {
                            if (Settings.Default.showSetVariableLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.SetVariableLog, false);
                        }
                        // SetVariableVal log
                        else if (message.StartsWith("SetVariable Value Log:"))
                        {
                            if (Settings.Default.showSetVariableValLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.SetVariableValLog, false);
                        }
                        // SetPos log
                        else if (message.StartsWith("SetPos Log:"))
                        {
                            if (Settings.Default.showSetPosLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.SetPosLog, false);
                        }
                        // AddMagazineCargo log
                        else if (message.StartsWith("AddMagazineCargo Log:"))
                        {
                            if (Settings.Default.showAddMagazineCargoLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.AddMagazineCargoLog, false);
                        }
                        // AddWeaponCargo log
                        else if (message.StartsWith("AddWeaponCargo Log:"))
                        {
                            if (Settings.Default.showAddWeaponCargoLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.AddWeaponCargoLog, false);
                        }
                        // AddBackpackCargo log
                        else if (message.StartsWith("AddBackpackCargo Log:"))
                        {
                            if (Settings.Default.showAddBackpackCargoLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.AddBackpackCargoLog, false);
                        }
                        // AttachTo log
                        else if (message.StartsWith("AttachTo Log:"))
                        {
                            if (Settings.Default.showAttachToLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.AttachToLog, false);
                        }
                        // MPEventHandler log
                        else if (message.StartsWith("MPEventHandler Log:"))
                        {
                            if (Settings.Default.showMPEventHandlerLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.MPEventHandlerLog, false);
                        }
                        // TeamSwitch log
                        else if (message.StartsWith("TeamSwitch Log:"))
                        {
                            if (Settings.Default.showTeamSwitchLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.TeamSwitchLog, false);
                        }
                        // SelectPlayer log
                        else if (message.StartsWith("SelectPlayer Log:"))
                        {
                            if (Settings.Default.showSelectPlayerLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.SelectPlayerLog, false);
                        }
                        // WaypointCondition log
                        else if (message.StartsWith("WaypointCondition Log:"))
                        {
                            if (Settings.Default.showWaypointConditionLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.WaypointConditionLog, false);
                        }
                        // WaypointStatement log
                        else if (message.StartsWith("WaypointStatement Log:"))
                        {
                            if (Settings.Default.showWaypointStatementLog && !this.ShallFilter(message))
                                _form.Log(message, LogType.WaypointStatementLog, false);
                        }
                        else
                        {
                            if (_form != null) _form.Log("UNKNOWN: " + message, LogType.Debug, false);
                        }
                    }
                }
            }
        }
        private void HandleConnect(BattlEyeConnectEventArgs args)
        {
            switch (args.ConnectionResult)
            {
                case BattlEyeConnectionResult.Success:
                    if (Settings.Default.showConnectMessages && _form != null)
                    {
                        if (!_reconnecting && _form.connect.Enabled == true)
                            _form.Log(Resources.Strings.Connected, LogType.Console, false);
                        else
                            _form.Log(Resources.Strings.Reconnected, LogType.Console, false);
                    }
                    _error = false;
                    break;
                case BattlEyeConnectionResult.InvalidLogin:
                    if (_form != null) _form.Log(Resources.Strings.Error_login, LogType.Console, false);
                    _error = true;
                    break;
                case BattlEyeConnectionResult.ConnectionFailed:
                    if (_form.connect.Enabled)
                        if (_form != null) _form.Log(Resources.Strings.Error_connect, LogType.Console, false);
                    _error = true;
                    break;
                default:
                    if (_form != null) _form.Log(Resources.Strings.Error_occ, LogType.Console, false);
                    _error = true;
                    break;
            }
        }
        private void HandleDisconnect(BattlEyeDisconnectEventArgs args)
        {
            switch (args.DisconnectionType)
            {
                case BattlEyeDisconnectionType.ConnectionLost:
                    if (!_reconnecting)
                    {
                        if (_form != null) _form.Log(Resources.Strings.Error_conlost, LogType.Console, false);
                        this.Reconnect();
                    }
                    break;
                case BattlEyeDisconnectionType.Manual:
                    // Handle manual reconnect
                    break;

                case BattlEyeDisconnectionType.SocketException:
                    if (_form.connect.Enabled)
                    {
                        if (_form != null) _form.Log(Resources.Strings.Error_sel_host, LogType.Console, false);
                    }
                    else
                    {
                        if (!_reconnecting)
                        {
                            if (_form != null) _form.Log(Resources.Strings.Error_sdown, LogType.Console, false);
                            this.Reconnect();
                        }
                    }
                    break;
                default:
                    if (_form != null) _form.Log(Resources.Strings.Error_occ, LogType.Console, false);
                    break;
            }
        }

        private bool IsCall(string message)
        {
            try
            {
                if (Settings.Default.showAdminCalls)
                {
                    message = message.Split(new char[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries)[1];

                    bool important = false;
                    foreach (string test in _hilight) important = important || message.IndexOf(test, StringComparison.OrdinalIgnoreCase) >= 0;

                    if (Settings.Default.useNameForAdminCalls && !string.IsNullOrEmpty(Settings.Default.name) && !important)
                        important = important || message.IndexOf(Settings.Default.name, StringComparison.OrdinalIgnoreCase) >= 0;

                    return important;
                }
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
        private bool ShallFilter(string message)
        {
            foreach (string filter in _filters)
            {
                if (message.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private void Reconnect()
        {
            _reconnecting = true;
            Thread thread = new Thread(new ThreadStart(HandleReconnect));
            thread.IsBackground = true;
            thread.Start();
        }
        private void HandleReconnect()
        {
            while (_reconnecting && _initialized && !_client.Connected)
            {
                Thread.Sleep(5000);
                this.Disconnect();
                this.Connect(_credentials.Host, _credentials.Port, _credentials.Password);
            }
            _reconnecting = false;
        }

        public bool isIP(String ip)
        {
            try
            {
                IPAddress.Parse(ip);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
