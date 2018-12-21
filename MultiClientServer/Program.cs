using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MultiClientServer
{
    class Program
    {
        static public int MijnPoort;
        static public int Netwerkgrootte = 20;
        static public Dictionary<int, Connection> Buren = new Dictionary<int, Connection>();
        static public Dictionary<int, int> Afstand = new Dictionary<int, int>();
        static public Dictionary<int, int> PrefBuur = new Dictionary<int, int>();
        static public Dictionary<Tuple<int, int>, int> BuurAfstand = new Dictionary<Tuple<int, int>, int>();
        static public List<int> Nodes = new List<int>();
        static public object prefBuurLock = new object();
        static public bool Initializing = true;

        static void Main(string[] args) {
            if (args.Length > 0)
                MijnPoort = int.Parse(args[0]);
            Console.WriteLine("ik ben poort " + MijnPoort);

            new Server(MijnPoort);
            for (int i = 1; i < args.Length; i++)
            {
                int node = int.Parse(args[i]);
                Nodes.Add(node);
                Afstand[node] = Netwerkgrootte;
                if (node > MijnPoort)
                    Buren.Add(node, new Connection(node));
            }
            Nodes.Add(MijnPoort);
            while (Buren.Count < args.Length - 1) { }
            foreach (KeyValuePair<int, Connection> buur in Buren)
            {
                foreach (int node in Nodes)
                {
                    Console.WriteLine(string.Format("adding {0} {1}", buur.Key, node));
                    if (!BuurAfstand.ContainsKey(new Tuple<int, int>(buur.Key, node)))
                        BuurAfstand[new Tuple<int, int>(buur.Key, node)] = Netwerkgrootte;
                    else
                        Console.WriteLine("yeet");

                }
                Afstand[MijnPoort] = 0;
                PrefBuur[MijnPoort] = MijnPoort;
            }
            foreach (KeyValuePair<int, Connection> buur in Buren)
            {
                buur.Value.Write.WriteLine(MyDistance(MijnPoort, 0));
            }

            Initializing = false;
            while (true)
            {
                string input = Console.ReadLine();
                string[] splitInput = input.Split(new char[] { ' ' }, 2);
                switch (splitInput[0])
                {
                    case "R":
                        Console.WriteLine("routing table");
                        lock (prefBuurLock)
                        {
                            foreach (int node in Nodes)
                            {
                                if (node == MijnPoort)
                                    Console.WriteLine(string.Format("{0} {1} {2}", node, Afstand[node], "local"));
                                else
                                    Console.WriteLine(string.Format("{0} {1} {2}", node, Afstand[node], PrefBuur[node]));
                            }
                        }
                        break;
                    case "B":
                        string[] splitMessage = splitInput[1].Split(new char[] { ' ' }, 2);
                        if (PrefBuur.ContainsKey(int.Parse(splitMessage[0])))
                            Buren[PrefBuur[int.Parse(splitMessage[0])]].Write.WriteLine(input);
                        else
                            Console.WriteLine(string.Format("Poort {0} is niet bekend",splitMessage[0]));
                        break;
                    case "C":
                        if (Buren.ContainsKey(int.Parse(splitInput[1])))
                            Console.WriteLine("already connected");
                        else
                        {
                            int port = int.Parse(splitInput[1]);

                            Buren.Add(int.Parse(splitInput[1]), new Connection(int.Parse(splitInput[1])));
                            Buren[int.Parse(splitInput[1])].Write.WriteLine(string.Format("C {0}", MijnPoort));
                            //if (!Nodes.Contains(int.Parse(splitInput[1])))
                            //    Nodes.Add(int.Parse(splitInput[1]));
                            while (!Buren[int.Parse(splitInput[1])].ready) { }

                            foreach (int node in Nodes)
                            {
                                Buren[int.Parse(splitInput[1])].Write.WriteLine(MyDistance(node, Afstand[node]));
                            }
                        }
                        break;
                    case "D":
                        int disconnectedNode = int.Parse(splitInput[1]);
                        if (Buren.ContainsKey(disconnectedNode))
                        {
                            lock (prefBuurLock)
                            {
                                Buren[disconnectedNode].Write.WriteLine("D " + MijnPoort);
                                Buren.Remove(disconnectedNode);
                                foreach (int node in Nodes)
                                {
                                    Recompute(node);
                                }
                            }
                        }
                        else
                            Console.WriteLine(string.Format("Poort {0} is niet bekend", disconnectedNode));
                        break;
                    default: break;
                }
            }
        }
        static public string MyDistance(int node, int cost) {
            return string.Format("mydist {0} {1} {2}", MijnPoort, node, cost);
        }
        static public void Recompute(int port) {
            if (port == MijnPoort)
            {
                BuurAfstand[new Tuple<int, int>(MijnPoort, port)] = 0;
                PrefBuur[port] = port;
            }
            else
            {
                if (!Nodes.Contains(port))
                    Nodes.Add(port);
                int oudeAfstand = Afstand[port];
                int d = Netwerkgrootte;
                int prefN = -1;
                if (Buren.ContainsKey(port))
                {
                    Afstand[port] = 1;
                    PrefBuur[port] = port;
                }
                else
                {
                    foreach (KeyValuePair<int, Connection> buur in Buren)
                    {
                        if (!BuurAfstand.ContainsKey(new Tuple<int, int>(buur.Key, port)))
                            Console.WriteLine("bestaat niet: " + buur.Key + " naar " + port);
                        else if (BuurAfstand[new Tuple<int, int>(buur.Key, port)] < d)
                        {
                            d = BuurAfstand[new Tuple<int, int>(buur.Key, port)] + 1;
                            prefN = buur.Key;
                        }
                    }
                    Console.WriteLine(string.Format("Afstand naar {0} is nu {1} via {2}", port, d, prefN));
                    Afstand[port] = d;
                    Console.WriteLine("oude afstand: " + oudeAfstand + ", nieuwe afstand: " + d);
                    PrefBuur[port] = (prefN == -1) ? port : prefN;
                    if (d >= Netwerkgrootte) {
                        Nodes.Remove(port);
                        Afstand.Remove(port);
                        foreach (KeyValuePair<int, Connection> buur in Buren)
                        {
                            buur.Value.Write.WriteLine(MyDistance(port, 20));
                        }
                    }
                }
                if (Afstand[port] != oudeAfstand)
                {
                    Console.WriteLine("afstand veranderd");
                    foreach (KeyValuePair<int, Connection> buur in Buren)
                    {
                        Console.WriteLine("sending to buur " + buur.Key);
                        buur.Value.Write.WriteLine(MyDistance(port, Afstand[port]));
                    }
                }

            }
        }
    }
}
