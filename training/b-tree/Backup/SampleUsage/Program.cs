using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Sop.Collections.Generic.BTree;

namespace BTreeSample
{
    class Program
    {
        static void Main(string[] args)
        {
            BTreeDictionary<string, string> WordLookup = new BTreeDictionary<string, string>();
            WordLookup.Add("Hello", "Cool World!");
            WordLookup.Add("Hello", "Cooler World!");
            WordLookup.Add("Brown", "Fox!");
            WordLookup.Add("Foo", "Bar");
            string v;
            if (WordLookup.TryGetValue("Foo", out v))
                Console.WriteLine("Foo was found! Value = {0}", v);

            if (WordLookup.MoveFirst())
            {
                Console.WriteLine();
                Console.WriteLine("Words in Lookup:");
                do
                {
                    Console.WriteLine("Key:{0}, Value={1} ", WordLookup.CurrentKey, WordLookup.CurrentValue);
                } while (WordLookup.MoveNext());
            }

            Console.ReadLine();
        }
    }
}
