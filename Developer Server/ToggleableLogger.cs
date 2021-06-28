// <copyright file="ToggleableLogger.cs" company="PolyPoly Team">
// Copyright (c) PolyPoly Team. All rights reserved.
// </copyright>

using PolypolyGame;
using System;

namespace Developer_Server
{
    public class ToggleableLogger : Logger
    {
        public bool isPrinting;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToggleableLogger"/> class.
        /// </summary>
        /// <param name="log"></param>
        public ToggleableLogger(bool log)
        {
            isPrinting = log;
        }

        /// <inheritdoc/>
        public override void Print(string msg)
        {
            if (isPrinting)
            {
                Console.WriteLine(msg);
            }
        }
    }
}
