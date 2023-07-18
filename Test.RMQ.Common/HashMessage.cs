using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.RMQ.Common
{
    public class HashMessage
    {
        public HashMessage(string sha1)
        {
            Sha1 = sha1;
        }

        public string Sha1 { get; set; }
    }
}
