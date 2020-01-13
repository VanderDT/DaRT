using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaRT
{
    public struct LogItem
    {
        public LogType Type
        {
            get { return _type; }
        }
        public string Message
        {
            get { return _message.ToString(); }
        }
        public bool Important
        {
            get { return _important; }
        }
        public DateTime Date
        {
            get { return _date; }
        }
        private LogType _type;
        private object _message;
        private bool _important;
        private DateTime _date;

        public LogItem(LogType type, object message, bool important, DateTime date)
        {
            _type = type;
            _message = message;
            _important = important;
            _date = date;
        }
        public string ToJson()
        {
            TimeSpan timestamp = (_date.ToUniversalTime() - new DateTime(1970, 1, 1));

            return String.Format("{{\"timestamp\":{0:G},\"type\":\"{1}\",\"impotant\":{2},\"text\":\"{3}\"}}", (long)(timestamp.TotalMilliseconds), _type.ToString(), _important.ToString(), ReplaceString.ToLiteral(_message.ToString()));
        }
    }
}
