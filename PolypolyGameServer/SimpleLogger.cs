using System;

namespace PolypolyGame
{
    public class SimpleLogger
    {
        public virtual void Print(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
