using System;

namespace PolypolyGame
{
    /// <summary>
    /// Helper class for custom logging.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Writes a specified string.
        /// </summary>
        /// <param name="msg">Message to print.</param>
        public virtual void Print(string msg) => Console.WriteLine(msg);
    }
}
