using System;

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
