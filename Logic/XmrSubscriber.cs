﻿using NetMQ;
using NetMQ.Sockets;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using XiboClient.Log;

namespace XiboClient.Logic
{
    class XmrSubscriber
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;

        /// <summary>
        /// Last Heartbeat packet received
        /// </summary>
        public DateTime LastHeartBeat;

        /// <summary>
        /// Client Hardware key
        /// </summary>
        public HardwareKey HardwareKey
        {
            set
            {
                _hardwareKey = value;
            }
        }
        private HardwareKey _hardwareKey;

        /// <summary>
        /// Client Info Form
        /// </summary>
        public ClientInfo ClientInfoForm
        {
            set
            {
                _clientInfoForm = value;
            }
        }
        private ClientInfo _clientInfoForm;

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            try
            {
                // Check we have an address to connect to.
                if (string.IsNullOrEmpty(ApplicationSettings.Default.XmrNetworkAddress))
                    throw new Exception("Empty XMR Network Address");

                // Get the Private Key
                AsymmetricCipherKeyPair rsaKey = _hardwareKey.getXmrKey();

                // Connect to XMR
                using (NetMQContext context = NetMQContext.Create())
                {
                    using (SubscriberSocket socket = context.CreateSubscriberSocket())
                    {
                        // Bind
                        socket.Connect(ApplicationSettings.Default.XmrNetworkAddress);
                        socket.Subscribe("H");
                        socket.Subscribe(_hardwareKey.Channel);

                        while (!_forceStop)
                        {
                            lock (_locker)
                            {
                                try
                                {
                                    NetMQMessage message = socket.ReceiveMultipartMessage();

                                    // Deal with heart beat
                                    if (message[0].ConvertToString() == "H")
                                    {
                                        LastHeartBeat = DateTime.Now;
                                        continue;
                                    }

                                    // Decrypt the message
                                    string opened = OpenSslInterop.decrypt(message[2].ConvertToString(), message[1].ConvertToString(), rsaKey.Private);

                                    // See what we need to do with this message
                                    Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Message: " + opened), LogType.Error.ToString());
                                }
                                catch (Exception ex)
                                {
                                    // Log this message, but dont abort the thread
                                    Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                                    _clientInfoForm.XmrSubscriberStatus = "Error. " + ex.Message;
                                }
                            }
                        }
                    }
                }

                Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Subscriber Stopped"), LogType.Info.ToString());
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("XmrSubscriber - Run", "Unable to Subscribe to XMR: " + e.Message), LogType.Error.ToString());
                _clientInfoForm.XmrSubscriberStatus = e.Message;
            }
        }
    }
}
