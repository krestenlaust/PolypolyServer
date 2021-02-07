using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolypolyGameServer
{
    public class SimpleLogger
    {
        public virtual void Print(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
