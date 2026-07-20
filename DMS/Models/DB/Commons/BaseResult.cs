using System;
using System.Collections.Generic;
using System.Linq;

namespace DMS.Models.DB.Commons
{
    public class BaseResult
    {
        public const string VALUE_SUCCESS = "SUCCESS";
        public const string VALUE_ERROR = "ERROR";

        public string Result { set; get; }
        public string ProcessId { set; get; }
        public string[] SuccMesgs { set; get; }
        public string[] ErrMesgs { set; get; }
        public object[] Params { set; get; }
    }
}