using PolypolyGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Developer_Server
{
    public class ToggleableLogger : SimpleLogger
    {
        public bool isPrinting;

        public ToggleableLogger(bool log)
        {
            isPrinting = log;
        }

        public override void Print(string msg)
        {
            if (isPrinting)
            {
                Console.WriteLine(msg);
            }
        }
    }
}
