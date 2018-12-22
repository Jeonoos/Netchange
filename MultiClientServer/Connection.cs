using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace MultiClientServer
{
    class Connection
    {
        public StreamReader Read;
        public StreamWriter Write;
        public bool ready = false;
        // Connection heeft 2 constructoren: deze constructor wordt gebruikt als wij CLIENT worden bij een andere SERVER
        public Connection(int port)
        {
            TcpClient client = null;
            while (client == null)
            {
                try
                {
                    client = new TcpClient("localhost", port);
                }
                catch { }
            }
            Read = new StreamReader(client.GetStream());
            Write = new StreamWriter(client.GetStream());
            Write.AutoFlush = true;
            // De server kan niet zien van welke poort wij client zijn, dit moeten we apart laten weten
            Write.WriteLine("Poort: " + Program.MijnPoort);



            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // Deze constructor wordt gebruikt als wij SERVER zijn en een CLIENT maakt met ons verbinding
        public Connection(StreamReader read, StreamWriter write)
        {
            Read = read; Write = write;

            // Start het reader-loopje
            new Thread(ReaderThread).Start();
        }

        // LET OP: Nadat er verbinding is gelegd, kun je vergeten wie er client/server is (en dat kun je aan het Connection-object dus ook niet zien!)

        // Deze loop leest wat er binnenkomt en print dit
        public void ReaderThread() {
            ready = true;
            lock (Program.prefBuurLock) { }
            try
            {
                while (true) {
                    string input = Read.ReadLine();
                    Console.WriteLine("received: " + input);
                    string[] message = input.Split(new char[] { ' ' }, 2);
                    string[] args = message[1].Split();
                    switch (message[0]) {
                        case "mydist":
                            lock (Program.prefBuurLock)
                            {
                                if (!Program.Nodes.Contains(int.Parse(args[1])))
                                    Program.Nodes.Add(int.Parse(args[1]));

                                if (!Program.Afstand.ContainsKey(int.Parse(args[1])))
                                    Program.Afstand[int.Parse(args[1])] = Program.Netwerkgrootte;

                                Program.BuurAfstand[new Tuple<int, int>(int.Parse(args[0]), int.Parse(args[1]))] = int.Parse(args[2]);
                                Program.Recompute(int.Parse(args[1]));
                            }
                            break;
                        case "B":
                            string[] splitMessage = message[1].Split(new char[] { ' ' }, 2);
                            if (int.Parse(splitMessage[0]) == Program.MijnPoort)
                                Console.WriteLine(splitMessage[1]);
                            else
                            {
                                Console.WriteLine(string.Format("Bericht voor {0} doorgestuurd naar {1}",splitMessage[0], Program.PrefBuur[int.Parse(splitMessage[0])]));
                                Program.Buren[Program.PrefBuur[int.Parse(splitMessage[0])]].Write.WriteLine(input);
                            }
                            break;
                        case "C":
                            int port = int.Parse(args[0]);
                            //if (!Program.Nodes.Contains(port))
                            //    Program.Nodes.Add(port);
                            while (!Program.Buren.ContainsKey(port)) { }
                            lock (Program.prefBuurLock)
                            {
                                foreach (KeyValuePair<int, int> node in Program.Afstand)
                                {
                                    Program.Buren[port].Write.WriteLine(Program.MyDistance(node.Key, node.Value));
                                }
                            }
                            break;
                        case "D":
                            port = int.Parse(args[0]);
                            if (Program.Buren.ContainsKey(port))
                            {
                                lock (Program.prefBuurLock)
                                {
                                    Program.Buren.Remove(port);

                                    foreach (int node in Program.Nodes)
                                    {
                                        Program.Recompute(node);
                                    }
                                }
                            }
                            else
                                Console.WriteLine(string.Format("Poort {0} is niet bekend", port));
                            break;
                    }
                }
        }
            catch(Exception e) {
                Console.WriteLine("thread broke: " + e.StackTrace);
            } // Verbinding is kennelijk verbroken
}
    }
}
