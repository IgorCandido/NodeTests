using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroMq
{
    class ZMQReceiver
    {

        ZMQ.Context context = new ZMQ.Context();

        public ZMQReceiver()
        {
            
            var puller = context.Socket(ZMQ.SocketType.PULL);

            var pool = puller.CreatePollItem(ZMQ.IOMultiPlex.POLLIN);

            pool.PollInHandler += puller_PollInHandler;
            pool.PollErrHandler +=  Program_PollErrHandler;

            puller.Connect("tcp://192.168.1.75:12345");

            while (true)
            {

                context.Poll(new ZMQ.PollItem[]{ pool }, 500);

            }
                        
        }

        void Program_PollErrHandler(ZMQ.Socket socket, ZMQ.IOMultiPlex revents)
        {

            Console.WriteLine("ERROR");

        }

        void puller_PollInHandler(ZMQ.Socket socket, ZMQ.IOMultiPlex revents)
        {

            var message = socket.Recv(UTF8Encoding.UTF8);

            Console.WriteLine("Message received" + message);

        }
    }
}
