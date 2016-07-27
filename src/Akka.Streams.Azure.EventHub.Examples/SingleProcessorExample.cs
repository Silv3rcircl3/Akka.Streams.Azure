﻿using System;
using System.Text;
using Akka.Actor;
using Akka.Streams.Dsl;
using Microsoft.ServiceBus.Messaging;

namespace Akka.Streams.Azure.EventHub.Examples
{
    public static class SingleProcessorExample
    {
        private const string EventHubConnectionString = "{Event Hub connection string}";
        private const string EventHubName = "{Event Hub name}";
        private const string StorageAccountName = "{storage account name}";
        private const string StorageAccountKey = "{storage account key}";
        private static readonly string StorageConnectionString = $"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey}";

        public static void Run()
        {
            using (var sys = ActorSystem.Create("EventHubSystem"))
            {
                using (var mat = sys.Materializer())
                {
                    var processor = Source.FromGraph(new EventHubSource(false))
                        .Select(t => t.Item2)
                        .Select(e => Encoding.UTF8.GetString(e.GetBytes()))
                        .ToMaterialized(Sink.ForEach((string s) => Console.WriteLine(s)), Keep.Left)
                        .Run(mat);
                    
                    var eventProcessorHostName = Guid.NewGuid().ToString();
                    var eventProcessorHost = new EventProcessorHost(eventProcessorHostName, EventHubName, EventHubConsumerGroup.DefaultGroupName, EventHubConnectionString, StorageConnectionString);
                    Console.WriteLine("Registering EventProcessor...");
                    var options = new EventProcessorOptions();
                    options.ExceptionReceived += (sender, e) => { Console.WriteLine(e.Exception); };
                    eventProcessorHost.RegisterEventProcessorFactoryAsync(new SingelProcessorFactory(processor));

                    Console.WriteLine("Receiving. Press enter key to stop worker.");
                    Console.ReadLine();
                    eventProcessorHost.UnregisterEventProcessorAsync().Wait();
                }
            }
        }
    }
}
