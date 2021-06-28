// <copyright file="Logger.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

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

        /// <summary>
        /// Calls ToString method on object and writes it to the output.
        /// </summary>
        /// <param name="value">Object to print.</param>
        public void Print(object value) => Print(value.ToString());
    }
}
