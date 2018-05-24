﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Newtonsoft.Json;
using StardewValley;
using StardewModdingAPI.Events;

namespace PyTK.Types
{
    public interface IPyResponder
    {
        void start();
        void stop();
    }

    public class PyResponder<TOut, TIn> : IPyResponder
    {
        public string address;
        public int interval;
        public XmlSerializer xmlSerializer;
        public Func<TIn, TOut> requestHandler;
        public SerializationType serializationType;
        public SerializationType requestSerialization;
        private int tick;

        public PyResponder(string address, Func<TIn, TOut> requestHandler, int interval = 1, SerializationType serializationType = SerializationType.PLAIN, SerializationType requestSerialization = SerializationType.PLAIN, XmlSerializer xmlSerializer = null)
        {
            this.serializationType = serializationType;
            this.requestSerialization = requestSerialization;
            this.interval = interval;
            this.requestHandler = requestHandler;
            this.address = address;

            if (xmlSerializer == null)
                xmlSerializer= new XmlSerializer(typeof(TOut));
        }

        public void start()
        {
            tick = 0;
            GameEvents.UpdateTick += checkForRequests;
        }

        public void stop()
        {
            GameEvents.UpdateTick -= checkForRequests;
        }

        private void checkForRequests(object sender, EventArgs e)
        {
            tick++;
            if (tick != interval)
                return;

            foreach (MPMessage request in receive())
                respond(request);

            tick = 0;
        }

        private void respond(MPMessage msg)
        {
            TOut response = requestHandler(deserialize(requestSerialization, msg.message));
            if(response != null)
                respondToFarmer(msg.sender.UniqueMultiplayerID, msg.type, response);
        }

        private TIn deserialize(SerializationType type, object data)
        {
            if (type == (int)SerializationType.PLAIN)
                return (TIn)data;

            return (type == SerializationType.XML ? (TIn)xmlSerializer.Deserialize(new StringReader(data.ToString())) : JsonConvert.DeserializeObject<TIn>(data.ToString()));
        }


        private void respondToFarmer(long farmer, int id, params TOut[] dataSet)
        {
            foreach (TOut data in dataSet)
            {
                object objectData = data;

                if (serializationType == SerializationType.XML)
                {
                    StringWriter writer = new StringWriter();
                    xmlSerializer.Serialize(writer, data);
                    objectData = writer.ToString();
                }
                else if (serializationType == SerializationType.JSON)
                    objectData = JsonConvert.SerializeObject(data);

                PyNet.sendMessage(new MPMessage(address + "." + id, Game1.player, objectData, (int)serializationType, farmer));
            }
        }

        private IEnumerable<MPMessage> receive()
        {
            foreach (MPMessage msg in PyNet.getNewMessages(address, -1, -1))
                yield return (msg);
        }

    }

}
