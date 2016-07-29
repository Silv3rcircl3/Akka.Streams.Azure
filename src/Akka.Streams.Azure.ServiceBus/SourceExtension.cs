﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Streams.Dsl;
using Microsoft.ServiceBus.Messaging;

namespace Akka.Streams.Azure.ServiceBus
{
    public static class SourceExtension
    {
        /// <summary>
        /// Shurtcut for running this <see cref="Source{TOut,TMat}"/> with a <see cref="ServiceBusSink"/>.
        /// The returned <see cref="Task"/> will be completed with Success when reaching the
        /// normal end of the stream, or completed with Failure if there is a failure signaled in the stream.
        /// </summary>
        public static Task ToServiceBus<TMat>(this Source<IEnumerable<BrokeredMessage>, TMat> source, QueueClient client, IMaterializer materializer)
        {
            return source.RunWith(new ServiceBusSink(client), materializer);
        }

        /// <summary>
        /// Shurtcut for running this <see cref="Source{TOut,TMat}"/> with a <see cref="ServiceBusSink"/>.
        /// The returned <see cref="Task"/> will be completed with Success when reaching the
        /// normal end of the stream, or completed with Failure if there is a failure signaled in the stream.
        /// </summary>
        public static Task ToServiceBus<TMat>(this Source<IEnumerable<BrokeredMessage>, TMat> source, TopicClient client, IMaterializer materializer)
        {
            return source.RunWith(new ServiceBusSink(client), materializer);
        }
    }
}
