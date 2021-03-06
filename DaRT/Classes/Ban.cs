﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaRT
{
    public class Ban
    {
        private int _id;
        private string _name;
        private string _guid;
        private string _ip;
        private int _duration;
        private string _reason;
        private bool _online;

        public int ID
        {
            get { return _id; }
        }
        public string Name
        {
            get { return _name; }
        }
        public string GUID
        {
            get { return _guid; }
            set { _guid = value; }
        }
        public string IP
        {
            get { return _ip; }
            set { _ip = value; }
        }
        public int Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }
        public string Reason
        {
            get { return _reason; }
            set { _reason = value; }
        }
        public bool Online
        {
            get { return _online; }
            set { _online = value; }
        }
        public override string ToString()
        {
            return String.Format("{0}\t{1}\t{2}",_guid,_duration,_reason);
        }

        public string ToJson()
        {
            return String.Format("{{\"id\":{0},\"guid\":\"{1}\",\"name\":\"{2}\",\"ip\":\"{3}\",\"duration\":\"{4}\",\"reason\":\"{5}\"}}", _id, _guid, _name, _ip, _duration, _reason);
        }

        public Ban(int id, string guid, int duration, string reason)
        {
            _id = id;
            _guid = guid;
            _duration = duration;
            _reason = reason;
            _online = false;
        }

        public Ban(string guid, int duration, string reason)
        {
            _guid = guid;
            _duration = duration;
            _reason = reason;
            _online = false;
        }
        public Ban(int id, string name, string guid, string ip, int duration, string reason, bool online)
        {
            _id = id;
            _name = name;
            _guid = guid;
            _ip = ip;
            _duration = duration;
            _reason = reason;
            _online = online;
        }
        public Ban(int id, string name, string guid, string ip, bool online)
        {
            _id = id;
            _name = name;
            _guid = guid;
            _ip = ip;
            _online = online;
        }
    }
}
