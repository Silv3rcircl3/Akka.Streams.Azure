﻿using Akka.Streams.Supervision;

namespace Akka.Streams.Azure.StorageQueue
{
    internal static class Utils
    {
        public static Decider GetDeciderOrDefault(this Attributes attributes)
        {
            var attr = attributes.GetAttribute<ActorAttributes.SupervisionStrategy>(null);
            return attr != null ? attr.Decider : Deciders.StoppingDecider;
        }
    }
}
