using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Bot.Services
{
    public sealed class StupidTextService
    {
        
        private readonly List<string> _statements = new List<string>();

        private Random _rnd = new Random();


        public StupidTextService()
        {
            var ass = Assembly.GetEntryAssembly();
            var resource = ass.GetManifestResourceStream("EmbeddedResource.resources.text.txt");
            using(var reader = new StreamReader(resource))
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