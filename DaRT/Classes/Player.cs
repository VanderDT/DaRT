using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaRT
{
    public class Player
    {
        public int number = -1;
        public String ip = "";
        public String ping = "";
        public String guid = "";
        public String name = "";
        public String status = "";
        public String lastseen = "";
        public String lastseenon = "";
        public String location = "unknown";
        public String comment = "";
        public String world = "";


        public override string ToString()
        {
            return String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", number, ip, ping, guid, name, status);
        }

        public string ToJson()
        {
            return String.Format("{{\"id\":{0},\"loc\":\"{6}\",\"ip\":\"{1}\",\"ping\":{2},\"guid\":\"{3}\",\"name\":\"{4}\",\"comment\":\"{8}\",\"status\":\"{5}\",\"world\":[{7}]}}", number, ip, ping, guid, name, status, location, world, comment);
        }
        public Player()
        { 
        
        }

        public Player(int number, String ip, String name)
        {
            this.number = number;
            this.ip = ip;
            this.ping = "-1";
            this.guid = "NOGUID";
            this.name = name;
            this.status = "Initializing";
        }

        public Player(int number, String ip, String ping, String guid, String name, String status)
        {
            this.number = number;
            this.ip = ip;
            this.ping = ping;
            this.guid = guid;
            this.name = name;
            this.status = status;
        }

        public Player(int number, String ip, String ping, String guid, String name, String status, String lastseenon)
        {
            this.number = number;
            this.ip = ip;
            this.ping = ping;
            this.guid = guid;
            this.name = name;
            this.status = status;
            this.lastseenon = lastseenon;
        }

        public Player(int number, String ip, String ping, String guid, String name, String status, String lastseen, String lastseenon, String location)
        {
            this.number = number;
            this.ip = ip;
            this.ping = ping;
            this.guid = guid;
            this.name = name;
            this.status = status;
            this.lastseen = lastseen;
            this.lastseenon = lastseenon;
            this.location = location;
        }

        public Player(int number, String ip, String lastseen, String guid, String name, String lastseenon, String comment, bool doComment)
        {
            this.number = number;
            this.ip = ip;
            this.guid = guid;
            this.name = name;
            this.lastseen = lastseen;
            this.lastseenon = lastseenon;
            this.comment = comment;
        }
    }
}
