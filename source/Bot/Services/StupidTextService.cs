using System;
using System.Collections.Generic;
using System.IO;

namespace Bot.Services
{
    public sealed class StupidTextService
    {
        
        private readonly List<string> _statements = new List<string>();

        private Random _rnd = new Random();


        public StupidTextService()
        {
            using(var reader = new StreamReader("text.txt"))
            {
                while (!reader.EndOfStream)
                {
                    _statements.Add(reader.ReadLine());
                }
            }
        }


        public string GetRandomStupidText()
        {
            lock (this)
            {
                return _statements[_rnd.Next(0, _statements.Count - 1)];
            }
        }


    }
}