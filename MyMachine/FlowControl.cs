using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyMachine
{
    public class FlowControl
    {
        private Thread thread;
        public int Step = 0;
        public string StateDescirbe = "";
        public FlowControl()
        {
            thread = new Thread();
        }
    }
}
